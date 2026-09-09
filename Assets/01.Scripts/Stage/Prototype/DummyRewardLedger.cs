using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage.Prototype
{
    /// <summary>더미 지급 결과 자체를 영수증과 함께 저장합니다. 실제 인벤토리 지급을 대신하지 않습니다.</summary>
    public sealed class DummyRewardLedger
    {
        [DataContract]
        private sealed class Ledger
        {
            [DataMember] internal List<string> Applied = new List<string>();
        }
        private readonly string _path;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private Ledger _ledger;
        public DummyRewardLedger(string path = null)
        {
            _path = path;
            _ledger = path != null && File.Exists(path) ? AtomicStageFile.Read<Ledger>(path) : new Ledger();
        }
        public bool Contains(string requestId) => _ledger.Applied.Contains(requestId);
        public async Task<bool> ApplyAsync(string requestId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("Request ID is required.");
            await _gate.WaitAsync(token);
            try
            {
                if (Contains(requestId)) return false;
                var next = new Ledger { Applied = new List<string>(_ledger.Applied) };
                next.Applied.Add(requestId);
                if (_path != null) await Task.Run(() => AtomicStageFile.Write(_path, next, token), token);
                // 원자적 저장이 성공했다면 취소가 도착해도 메모리 영수증을 디스크와 맞춘다.
                _ledger = next;
                return true;
            }
            finally { _gate.Release(); }
        }
    }
}
