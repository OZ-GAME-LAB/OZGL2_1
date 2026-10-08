using System.Collections.Generic;
using OZGL2.Augment;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Progression;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕성 배치(그리드)와 증강을 저장본(RunSave)으로 바꾸고, 저장본을 새 그리드 세션에 되돌린다.
    /// 그리드는 id 로 유닛·발판 정의를 찾아 다시 만든다(카탈로그에 없는 id 는 건너뛴다).
    /// </summary>
    public static class RunSnapshotCodec
    {
        public static void Capture(RunSave save, GridManager grid)
        {
            save.floor.Clear(); save.expansions.Clear(); save.blocks.Clear(); save.units.Clear();
            save.initX = grid.Definition.InitialSize.x; save.initY = grid.Definition.InitialSize.y;
            save.maxX = grid.Definition.MaximumSize.x; save.maxY = grid.Definition.MaximumSize.y;
            foreach (var cell in grid.FloorCells) save.floor.Add(new RunCellSave { x = cell.x, y = cell.y });
            foreach (var e in grid.Expansions) save.expansions.Add(new RunExpansionSave { id = e.InstanceId, shapeId = e.Footprint.Id, x = e.Anchor.x, y = e.Anchor.y, rotation = e.Rotation });
            foreach (var b in grid.Blocks)
                save.blocks.Add(new RunBlockSave { id = b.InstanceId, contentId = b.ContentId, placed = b.IsPlaced, mirrored = b.IsMirrored, x = b.Anchor.x, y = b.Anchor.y, rotation = b.Rotation });
            foreach (var u in grid.Units)
                save.units.Add(new RunUnitSave { id = u.InstanceId, unitId = u.Definition.Id, star = u.StarLevel, placed = u.IsPlaced, mirrored = u.IsMirrored, x = u.Anchor.x, y = u.Anchor.y, rotation = u.Rotation });
        }

        /// <summary>고른 증강을 순서대로 모두 적는다(고르는 순간 한 번 실행되는 증강도 목록에는 남기고, 이어할 때 효과는 다시 주지 않는다).</summary>
        public static void CaptureAugments(RunSave save, AugmentRun augments)
        {
            save.augments.Clear();
            if (augments == null) return;
            foreach (var d in augments.PickedList)
                if (d != null) save.augments.Add(d.augmentId);
        }

        public static void Restore(RunSave save, GridRunSession session, GridPrototypeCatalogSO catalog)
        {
            var units = new Dictionary<string, UnitDefinition>();
            var shapes = new Dictionary<string, FootprintDefinition>();
            if (catalog != null)
            {
                foreach (var u in catalog.CreateUnits()) units[u.Id] = u;
                var initialUnit = catalog.CreateInitialUnit();
                if (!units.ContainsKey(initialUnit.Id)) units[initialUnit.Id] = initialUnit;
                foreach (var s in catalog.CreateBlocks()) shapes[s.Id] = s;
                var initialBlock = catalog.CreateInitialBlock();
                if (!shapes.ContainsKey(initialBlock.Id)) shapes[initialBlock.Id] = initialBlock;
            }

            var floor = new List<Vector2Int>();
            foreach (var c in save.floor) floor.Add(new Vector2Int(c.x, c.y));
            var expansions = new List<ExpansionPlacement>();
            foreach (var e in save.expansions)
            {
                var shape = session.Grid.Definition.Expansion;
                foreach (var candidate in session.Grid.Definition.ExpansionShapes) if (candidate.Id == e.shapeId) { shape = candidate; break; }
                expansions.Add(new ExpansionPlacement(e.id, shape, new Vector2Int(e.x, e.y), e.rotation));
            }
            var blocks = new List<BlockPlacement>();
            foreach (var b in save.blocks)
                if (shapes.TryGetValue(b.contentId, out var shape))
                    blocks.Add(new BlockPlacement(b.id, b.contentId, shape, b.placed, new Vector2Int(b.x, b.y), b.rotation, b.mirrored));
            var placedUnits = new List<UnitPlacement>();
            foreach (var u in save.units)
                if (units.TryGetValue(u.unitId, out var definition))
                    placedUnits.Add(new UnitPlacement(u.id, definition, u.placed, new Vector2Int(u.x, u.y), u.rotation, Mathf.Max(1, u.star), u.mirrored));

            int placedCount = placedUnits.FindAll(u => u.IsPlaced).Count;
            Debug.Log("[이어하기] 복원: " + save.clearedRounds + "웨이브 · 바닥 " + floor.Count + "칸 · 발판 " + blocks.Count + "/" + save.blocks.Count
                      + " · 유닛 " + placedUnits.Count + "/" + save.units.Count + "(배치 " + placedCount + ")");
            session.RestoreProgress(save.clearedRounds, save.rewardStage == 1, floor, expansions, blocks, placedUnits);
        }

        public static void RestoreAugments(RunSave save, AugmentRun augments)
        {
            if (augments == null || save == null || save.augments == null) return;
            foreach (var id in save.augments)
                foreach (var d in augments.Pool)
                    if (d != null && d.augmentId == id) { augments.RestoreSaved(d); break; }
        }
    }
}
