using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage
{
    /// <summary>도전별 JSON 저장. 임시 파일을 같은 디렉터리에 작성한 뒤 원자적으로 교체합니다.</summary>
    public sealed class FileStageProgressStore : IStageProgressStore
    {
        private readonly string _directory;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        public FileStageProgressStore(string directory) { _directory = Path.GetFullPath(directory); }
        public string GetPath(string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) throw new ArgumentException("Expected run GUID.", nameof(runId));
            return Path.Combine(_directory, runId + ".json");
        }
        public async Task SaveAsync(StageRunResult snapshot, CancellationToken cancellationToken)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string path = GetPath(snapshot.RunId);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() => AtomicStageFile.Write(path, snapshot, cancellationToken), cancellationToken);
            }
            finally { _gate.Release(); }
        }
        public StageRunResult Load(string runId) => AtomicStageFile.Read<StageRunResult>(GetPath(runId));
    }

    internal static class AtomicStageFile
    {
        internal static T Read<T>(string path)
        {
            using (var stream = File.OpenRead(path))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        internal static void Write<T>(string path, T value, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                    stream.Flush(true);
                }
                token.ThrowIfCancellationRequested();
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    /// <summary>디스크에 쓰지 않는 자동 검사 전용 저장소입니다.</summary>
    public sealed class MemoryStageProgressStore : IStageProgressStore
    {
        public StageRunResult LastSnapshot { get; private set; }
        public Task SaveAsync(StageRunResult snapshot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            return Task.CompletedTask;
        }
    }
}
