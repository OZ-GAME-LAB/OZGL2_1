using System.Collections.Generic;
using System.Globalization;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using UnityEngine;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 실제 Grid 데이터를 연결하지 않고 UI_Battle_MutedPreview에서 손패 외형과 호버를 확인한다.
    /// Scene에는 Presenter와 손패 Prefab 인스턴스만 저장하고 샘플 카드는 Play Mode에서 생성한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Battle/Card Hand Preview Presenter")]
    public sealed class UIBattleCardHandPreviewPresenter : MonoBehaviour
    {
        private const string PREVIEW_SCENE_NAME = "UI_Battle_MutedPreview";

        [SerializeField] private UIBattleCardHandView _handView;
        [SerializeField] private bool _showPreview = true;
        [SerializeField] private UIUnitCatalogSO _unitCatalog;
        [SerializeField] private GridPrototypeCatalogSO _gridCatalog;
        [SerializeField] private UIExpansionCardVisualCatalogSO _expansionCardVisuals;

        private bool _isRefreshing;
        private bool _hasLoadedExpansionCardVisuals;

        private void OnEnable()
        {
            RefreshPreview();
        }

        private void Start()
        {
            // Scene 로드 중 비활성 Grid Adapter의 OnDisable 정리가 끝난 뒤 샘플을 다시 확정한다.
            RefreshPreview();
        }

        [ContextMenu("Refresh Card Hand Preview")]
        public void RefreshPreview()
        {
            if (!Application.isPlaying || _isRefreshing || !_showPreview || !IsPreviewScene()) return;

            if (_handView == null) _handView = GetComponent<UIBattleCardHandView>();
            if (_handView == null) return;

            _isRefreshing = true;
            try
            {
                _handView.SetInteractable(true);
                _handView.SetItems(CreatePreviewItems());
                _handView.RefreshLayoutImmediate();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private bool IsPreviewScene()
        {
            return gameObject.scene.IsValid() && gameObject.scene.isLoaded &&
                   gameObject.scene.name == PREVIEW_SCENE_NAME;
        }

        private BattleHandCardDisplayData[] CreatePreviewItems()
        {
            var items = new List<BattleHandCardDisplayData>();
            if (_unitCatalog == null || _gridCatalog == null) return items.ToArray();
            if (_expansionCardVisuals == null && !_hasLoadedExpansionCardVisuals)
            {
                _hasLoadedExpansionCardVisuals = true;
                _expansionCardVisuals = UIExpansionCardVisualCatalogSO.LoadDefault();
            }

            IReadOnlyList<UnitDefinition> units = _gridCatalog.CreateUnits();
            IReadOnlyList<FootprintDefinition> expansions = _gridCatalog.CreateDefinition().ExpansionShapes;
            AddUnitPreview(items, units, "M_WAR_01", 3);
            AddExpansionPreview(items, expansions, "floor_square4");
            AddUnitPreview(items, units, "M_ARC_01", 2);
            AddExpansionPreview(items, expansions, "floor_line3");
            AddUnitPreview(items, units, "M_MAG_01", 1);
            return items.ToArray();
        }

        private void AddUnitPreview(List<BattleHandCardDisplayData> items,
            IReadOnlyList<UnitDefinition> units, string unitId, int star)
        {
            UIUnitCatalogSO.Entry entry = null;
            foreach (UIUnitCatalogSO.Entry candidate in _unitCatalog.Entries)
                if (candidate != null && candidate.Id == "unit." + unitId) { entry = candidate; break; }
            if (entry == null || entry.BaseStats == null) return;

            FootprintDefinition footprint = null;
            foreach (UnitDefinition unit in units)
                if (unit.Id == unitId) { footprint = unit.GetFootprint(star); break; }
            if (footprint == null) return;

            UnitStatData stats = entry.BaseStats;
            float multiplier = UnitStatData.GetStarMultiplier(star);
            items.Add(new BattleHandCardDisplayData(
                "PREVIEW:UNIT:" + unitId, eBattleHandCardKind.UNIT, entry.DisplayName,
                rankText: star.ToString(CultureInfo.InvariantCulture),
                traitTitle: "점유 칸수",
                traitDescription: footprint.Cells.Count.ToString(CultureInfo.InvariantCulture) + "칸",
                skillTitle: "보유 스킬",
                skillDescription: BattleCardSkillDescription.Build(stats),
                attack: (stats.attackPower * multiplier).ToString("0.#", CultureInfo.InvariantCulture),
                defense: (stats.defensePercent * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%",
                health: Mathf.RoundToInt(stats.maxHealth * multiplier).ToString(CultureInfo.InvariantCulture),
                artwork: entry.GetPortrait(star - 1),
                footprint: CreateDisplayFootprint(footprint),
                showFootprint: false));
        }

        private void AddExpansionPreview(List<BattleHandCardDisplayData> items,
            IReadOnlyList<FootprintDefinition> expansions, string shapeId)
        {
            foreach (FootprintDefinition shape in expansions)
            {
                if (shape.Id != shapeId) continue;
                items.Add(new BattleHandCardDisplayData(
                    "PREVIEW:LAND:" + shapeId, eBattleHandCardKind.LAND_SLOT, "배치 영역 확장",
                    areaTitle: "배치 영역 +" + shape.Cells.Count.ToString(CultureInfo.InvariantCulture) + "칸",
                    areaDescription: "확장할 위치에 배치해\n유닛을 놓을 공간을 넓힙니다.",
                    artwork: _expansionCardVisuals != null ? _expansionCardVisuals.GetArtwork(shapeId) : null,
                    footprint: CreateDisplayFootprint(shape),
                    showFootprint: false));
                break;
            }
        }

        private static Vector2Int[] CreateDisplayFootprint(FootprintDefinition footprint)
        {
            Vector2Int[] cells = footprint.GetCells(Vector2Int.zero, 0, false);
            if (cells.Length == 0) return cells;
            int minX = cells[0].x, maxY = cells[0].y;
            foreach (Vector2Int cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }
            for (int i = 0; i < cells.Length; i++)
                cells[i] = new Vector2Int(cells[i].x - minX, maxY - cells[i].y);
            return cells;
        }
    }
}
