using System;
using UnityEngine;

namespace OZGL2.Grid
{
    /// <summary>확장 보상 한 묶음의 배치. 보관함 아이템이나 초기 영역을 나타내지 않는다.</summary>
    public sealed class ExpansionPlacement
    {
        public string InstanceId { get; }
        public FootprintDefinition Footprint { get; }
        public Vector2Int Anchor { get; }
        public int Rotation { get; }
        public ExpansionPlacement(string instanceId, FootprintDefinition footprint, Vector2Int anchor, int rotation)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Expansion ID required.", nameof(instanceId));
            InstanceId = instanceId;
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            Anchor = anchor; Rotation = ((rotation % 4) + 4) % 4;
        }
        public Vector2Int[] GetCells() => Footprint.GetCells(Anchor, Rotation);
        public ExpansionPlacement WithPlacement(Vector2Int anchor, int rotation)
            => new ExpansionPlacement(InstanceId, Footprint, anchor, rotation);
    }
}
