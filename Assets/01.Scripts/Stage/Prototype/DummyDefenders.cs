using System;
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage.Prototype
{
    public sealed class DummyDefenders : IStageDefenders
    {
        private readonly int _deployedCount;
        public int AliveCount { get; private set; }
        public DummyDefenders(int deployedCount)
        {
            if (deployedCount <= 0) throw new ArgumentOutOfRangeException(nameof(deployedCount));
            _deployedCount = deployedCount;
        }
        public Task PrepareRoundAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            AliveCount = _deployedCount;
            return Task.CompletedTask;
        }
        public void DefeatOne() { if (AliveCount > 0) AliveCount--; }
    }
}
