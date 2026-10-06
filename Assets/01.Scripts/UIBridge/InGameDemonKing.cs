using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕이 서 있는 자리(그리드 맨 아래 가운데, KingAnchor)에 데몬킹 프리팹을 세운다.
    /// 그리드가 만든 칸 위치(Surface_x_y)에서 같은 자리를 계산하므로 준비·전투 어느 쪽에서도 같은 곳에 있다.
    /// 준비 화면이 그리던 금색 네모 표시(KingMarker)는 데몬킹이 대신하므로 가린다.
    /// 프리팹은 SPUM 스프라이트뿐이라 전투 로직은 없고, 대기(IDLE) 애니메이션만 재생한다.
    /// </summary>
    public sealed class InGameDemonKing : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0.1f)] private float _scale = 1.2f;
        [SerializeField, Tooltip("칸 단위 위치 보정 — 발 아래 그림자 때문에 눈으로 어긋나 보일 때 손으로 맞춘다.")]
        private Vector2 _cellOffset = Vector2.zero;
        [SerializeField] private int _sortingOrderBoost = 5;

        private GameObject _king;
        private Transform _marker;
        private float _nextScan;

        private void Update()
        {
            if (_prefab == null) { enabled = false; return; }
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;
            if (_king == null) TrySpawn();
        }

        private void LateUpdate()
        {
            if (_king == null) return;
            if (_marker == null)
            {
                var found = GameObject.Find("KingMarker");
                if (found != null) _marker = found.transform;
            }
            if (_marker == null) return;
            var sr = _marker.GetComponent<SpriteRenderer>();
            if (sr != null && sr.enabled) sr.enabled = false;
        }

        private void TrySpawn()
        {
            var g00 = GameObject.Find("Surface_0_0");
            var g10 = GameObject.Find("Surface_1_0");
            if (g00 == null || g10 == null) return;

            int maxX = 0;
            foreach (var t in g00.transform.parent.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Surface_")) continue;
                var parts = t.name.Split('_');
                if (parts.Length == 3 && int.TryParse(parts[1], out int px)) maxX = Mathf.Max(maxX, px);
            }

            var origin = g00.transform.position;
            var right = g10.transform.position - origin;
            var up = FindUp(g00, origin);
            if (up.sqrMagnitude < 1e-6f) return;

            // KingAnchor = ((최대 가로 칸 - 1) / 2, -1)
            var pos = origin + right * (maxX * 0.5f + _cellOffset.x) + up * (-1f + _cellOffset.y);
            _king = Instantiate(_prefab, pos, Quaternion.identity);
            _king.name = "DemonKing";
            _king.transform.localScale = _king.transform.localScale * _scale;
            Prepare(_king);
        }

        private static Vector3 FindUp(GameObject g00, Vector3 origin)
        {
            var g01 = GameObject.Find("Surface_0_1");
            return g01 == null ? Vector3.zero : g01.transform.position - origin;
        }

        private void Prepare(GameObject king)
        {
            // 타일(-100 대)과 격자 위 유닛보다 위에 보이게
            foreach (var r in king.GetComponentsInChildren<SpriteRenderer>(true))
                r.sortingOrder += _sortingOrderBoost;

            var spum = king.GetComponentInChildren<SPUM_Prefabs>(true);
            if (spum == null) return;
            if (!spum.allListsHaveItemsExist()) spum.PopulateAnimationLists();
            spum.OverrideControllerInit();
            if (spum.StateAnimationPairs.TryGetValue(PlayerState.IDLE.ToString(), out var idle) && idle.Count > 0)
                spum.PlayAnimation(PlayerState.IDLE, 0);
        }

        private void OnDestroy()
        {
            if (_king != null) Destroy(_king);
        }
    }
}
