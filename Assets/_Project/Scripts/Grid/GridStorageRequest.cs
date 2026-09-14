using System;
using System.Collections.Generic;

namespace OZGL2.Grid
{
    public enum eGridStorageOperation { REWARD, LAYOUT }

    /// <summary>유닛/발판 ID 영역이 겹쳐도 폐기 대상을 구분한다.</summary>
    public sealed class GridStoredItem : IEquatable<GridStoredItem>
    {
        public eGridDragKind Kind { get; }
        public string InstanceId { get; }
        public string DisplayName { get; }
        public GridStoredItem(eGridDragKind kind, string instanceId, string displayName = null)
        {
            if (kind != eGridDragKind.UNIT && kind != eGridDragKind.BLOCK) throw new ArgumentException("Storage item must be a unit or block.");
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Instance ID required.");
            Kind = kind; InstanceId = instanceId; DisplayName = displayName ?? instanceId;
        }
        public bool Equals(GridStoredItem other) => other != null && Kind == other.Kind && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => Equals(obj as GridStoredItem);
        public override int GetHashCode() => ((int)Kind * 397) ^ InstanceId.GetHashCode();
    }

    /// <summary>확정 전 원본 배치/보관함은 변경하지 않는다. 화면 재생성과 무관하게 세션이 보유한다.</summary>
    public sealed class GridStorageRequest
    {
        public string RequestId { get; } = Guid.NewGuid().ToString("N");
        public eGridStorageOperation Operation { get; }
        public int RequiredDiscardCount { get; }
        public IReadOnlyList<GridStoredItem> Incoming { get; }
        public IReadOnlyList<GridStoredItem> DiscardCandidates { get; }
        internal List<BlockPlacement> Blocks { get; }
        internal List<UnitPlacement> Units { get; }
        internal Action OnCommitted { get; }
        internal GridStorageRequest(eGridStorageOperation operation, int required, List<GridStoredItem> incoming,
            List<GridStoredItem> candidates, List<BlockPlacement> blocks, List<UnitPlacement> units, Action onCommitted)
        {
            Operation = operation; RequiredDiscardCount = required;
            Incoming = incoming.AsReadOnly(); DiscardCandidates = candidates.AsReadOnly();
            Blocks = blocks; Units = units; OnCommitted = onCommitted;
        }
    }
}
