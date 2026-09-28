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

        private bool _isRefreshing;

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

        private static BattleHandCardDisplayData[] CreatePreviewItems()
        {
            return new[]
            {
                new BattleHandCardDisplayData(
                    "PREVIEW:UNIT:SHADOW",
                    eBattleHandCardKind.UNIT,
                    "그림자 검사",
                    rankText: "3",
                    traitTitle: "암영",
                    traitDescription: "인접한 적에게 추가 피해를 줍니다.",
                    skillTitle: "그림자 베기",
                    skillDescription: "전방의 적을 빠르게 공격합니다.",
                    attack: "28",
                    defense: "12%",
                    health: "180",
                    footprint: new[] { Vector2Int.zero, Vector2Int.right }),
                new BattleHandCardDisplayData(
                    "PREVIEW:LAND:EMBER",
                    eBattleHandCardKind.LAND_SLOT,
                    "잿불 대지",
                    rankText: "2",
                    areaTitle: "배치 영역",
                    areaDescription: "연결 가능한 땅 슬롯을 확장합니다.",
                    footprint: new[] { Vector2Int.zero, Vector2Int.down, Vector2Int.right }),
                new BattleHandCardDisplayData(
                    "PREVIEW:RELIC:CHALICE",
                    eBattleHandCardKind.RELIC,
                    "심연의 성배",
                    rankText: "유물",
                    areaTitle: "보유 효과",
                    areaDescription: "전투 시작 시 아군의 체력을 회복합니다."),
                new BattleHandCardDisplayData(
                    "PREVIEW:UNIT:ARCHER",
                    eBattleHandCardKind.UNIT,
                    "잿빛 궁수",
                    rankText: "2",
                    traitTitle: "원거리",
                    traitDescription: "후방에서 안정적으로 공격합니다.",
                    skillTitle: "관통 사격",
                    skillDescription: "직선상의 적을 관통합니다.",
                    attack: "23",
                    defense: "8%",
                    health: "135",
                    footprint: new[] { Vector2Int.zero }),
                new BattleHandCardDisplayData(
                    "PREVIEW:LAND:FROST",
                    eBattleHandCardKind.LAND_SLOT,
                    "빙결 대지",
                    rankText: "3",
                    areaTitle: "배치 영역",
                    areaDescription: "세로 방향으로 진형을 확장합니다.",
                    footprint: new[] { Vector2Int.zero, Vector2Int.down, Vector2Int.down * 2 }),
                new BattleHandCardDisplayData(
                    "PREVIEW:UNIT:MAGE",
                    eBattleHandCardKind.UNIT,
                    "황금 마도사",
                    rankText: "4",
                    traitTitle: "마력",
                    traitDescription: "스킬 피해가 증가합니다.",
                    skillTitle: "황혼 폭발",
                    skillDescription: "넓은 범위에 마법 피해를 줍니다.",
                    attack: "35",
                    defense: "10%",
                    health: "155",
                    footprint: new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.down }),
            };
        }
    }
}
