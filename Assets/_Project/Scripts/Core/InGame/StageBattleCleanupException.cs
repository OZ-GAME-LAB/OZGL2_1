using System;
using System.Collections.Generic;

namespace OZGL2.Stage
{
    /// <summary>정리는 실패했지만 이미 판정된 전투 결과는 저장할 수 있도록 전달한다.</summary>
    public sealed class StageBattleCleanupException : AggregateException
    {
        public RoundResult ConfirmedResult { get; }
        public StageBattleCleanupException(RoundResult result, IEnumerable<Exception> errors)
            : base("Battle cleanup failed after the result was confirmed.", errors)
        { ConfirmedResult = result ?? throw new ArgumentNullException(nameof(result)); }
    }
}
