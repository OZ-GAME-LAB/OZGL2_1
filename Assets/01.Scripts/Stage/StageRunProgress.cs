using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace OZGL2.Stage
{
    public enum eRunStatus { RUNNING, CLEARED, FAILED, CANCELLED, ERROR }
    public enum eRewardKind { GENERAL, AUGMENT }
    public enum eRewardStatus { NOT_REQUIRED, PENDING, APPLIED }

    public sealed class RoundResult
    {
        public eBattleResult Outcome { get; }
        public long EarnedExperience { get; }
        public RoundResult(eBattleResult outcome, long earnedExperience)
        {
            if (!Enum.IsDefined(typeof(eBattleResult), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
            if (earnedExperience < 0) throw new ArgumentOutOfRangeException(nameof(earnedExperience));
            Outcome = outcome;
            EarnedExperience = earnedExperience;
        }
    }

    public sealed class RewardRequest
    {
        public string RunId { get; }
        public int RoundNumber { get; }
        public eRewardKind Kind { get; }
        public string RequestId => RunId + ":" + RoundNumber + ":" + Kind;
        public RewardRequest(string runId, int roundNumber, eRewardKind kind)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) throw new ArgumentException("Expected run GUID.", nameof(runId));
            if (roundNumber <= 0) throw new ArgumentOutOfRangeException(nameof(roundNumber));
            if (!Enum.IsDefined(typeof(eRewardKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            RunId = runId;
            RoundNumber = roundNumber;
            Kind = kind;
        }
    }

    [DataContract]
    public sealed class RoundProgress
    {
        [DataMember] public int RoundNumber { get; private set; }
        [DataMember] public eBattleResult Outcome { get; private set; }
        [DataMember] public long EarnedExperience { get; private set; }
        [DataMember] public eRewardStatus GeneralReward { get; private set; }
        [DataMember] public eRewardStatus Augment { get; private set; }
        internal RoundProgress(int number, RoundResult result, eRewardStatus general, eRewardStatus augment)
        {
            RoundNumber = number; Outcome = result.Outcome; EarnedExperience = result.EarnedExperience;
            GeneralReward = general; Augment = augment;
        }
        internal RoundProgress Apply(eRewardKind kind) => new RoundProgress(RoundNumber,
            new RoundResult(Outcome, EarnedExperience),
            kind == eRewardKind.GENERAL ? eRewardStatus.APPLIED : GeneralReward,
            kind == eRewardKind.AUGMENT ? eRewardStatus.APPLIED : Augment);
    }

    [DataContract]
    public sealed class StageRunResult
    {
        [DataMember] public string RunId { get; private set; }
        [DataMember] public string StageId { get; private set; }
        [DataMember] public int CurrentRoundNumber { get; private set; }
        [DataMember] public int ClearedRoundCount { get; private set; }
        [DataMember] public long EarnedExperience { get; private set; }
        [DataMember] public eRunStatus Status { get; private set; }
        [DataMember] public bool IsSettled { get; private set; }
        [DataMember(Name = "Rounds")] private RoundProgress[] _rounds;
        public IReadOnlyList<RoundProgress> Rounds => Array.AsReadOnly(_rounds);
        public bool IsCleared => Status == eRunStatus.CLEARED;
        internal StageRunResult(string runId, string stageId, int current, int cleared, long experience,
            eRunStatus status, bool isSettled, RoundProgress[] rounds)
        {
            RunId = runId; StageId = stageId; CurrentRoundNumber = current;
            ClearedRoundCount = cleared; EarnedExperience = experience;
            Status = status; IsSettled = isSettled; _rounds = rounds;
        }
    }

    /// <summary>한 번의 도전 기록입니다. 원본 SO나 과거 도전 기록을 변경하지 않습니다.</summary>
    public sealed class StageRunProgress
    {
        private readonly StageDefinition _stage;
        private readonly List<RoundProgress> _rounds = new List<RoundProgress>();
        private readonly string _runId = Guid.NewGuid().ToString("N");
        private int _current;
        private int _cleared;
        private long _experience;
        private eRunStatus _status;
        private bool _isSettled;
        public StageRunProgress(StageDefinition stage) { _stage = stage ?? throw new ArgumentNullException(nameof(stage)); }
        public void BeginRound(int number)
        {
            if (_status != eRunStatus.RUNNING || number != _rounds.Count + 1 || number > _stage.Rounds.Count)
                throw new InvalidOperationException("Round is not the next round.");
            _current = number;
        }
        public void RecordRound(int number, RoundResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_status != eRunStatus.RUNNING || number != _current || number != _rounds.Count + 1)
                throw new InvalidOperationException("Stale or duplicate round result.");
            long total = checked(_experience + result.EarnedExperience);
            bool hasRewards = result.Outcome == eBattleResult.VICTORY && number < _stage.Rounds.Count;
            _rounds.Add(new RoundProgress(number, result,
                hasRewards ? eRewardStatus.PENDING : eRewardStatus.NOT_REQUIRED,
                hasRewards && _stage.Rounds[number - 1].IsBossRound ? eRewardStatus.PENDING : eRewardStatus.NOT_REQUIRED));
            _experience = total;
            if (result.Outcome == eBattleResult.VICTORY) _cleared++;
            if (result.Outcome == eBattleResult.DEFEAT) _status = eRunStatus.FAILED;
            else if (_cleared == _stage.Rounds.Count) _status = eRunStatus.CLEARED;
        }
        public RewardRequest CreateRewardRequest(eRewardKind kind) => new RewardRequest(_runId, _current, kind);
        public void MarkRewardApplied(eRewardKind kind)
        {
            var round = _rounds[_rounds.Count - 1];
            var state = kind == eRewardKind.GENERAL ? round.GeneralReward : round.Augment;
            if (state != eRewardStatus.PENDING) throw new InvalidOperationException("Reward is not pending.");
            if (kind == eRewardKind.AUGMENT && round.GeneralReward != eRewardStatus.APPLIED)
                throw new InvalidOperationException("General reward must finish first.");
            _rounds[_rounds.Count - 1] = round.Apply(kind);
        }
        public void MarkSettled() { _isSettled = true; }
        public void MarkInterrupted(bool isCancelled) { _status = isCancelled ? eRunStatus.CANCELLED : eRunStatus.ERROR; }
        public StageRunResult CreateSnapshot() => new StageRunResult(_runId, _stage.StageId, _current, _cleared,
            _experience, _status, _isSettled, _rounds.ToArray());
    }
}
