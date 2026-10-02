using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕군 유닛 위의 성급(1성·2성·3성) 표시를 별 이미지 배지로 바꾼다.
    /// - 배치(준비) 단계: 그리드가 유닛 위에 붙이는 "★N" 글자(PreparationStarLevel)를 숨기고 같은 자리에 배지를 단다.
    /// - 전투 단계: 마왕군 유닛의 체력바 바로 위에 배지를 단다(전투 프리팹은 건드리지 않고 런타임에 자식으로 붙인다).
    /// 배지는 어두운 판 + 성급별 테두리색 + 별 N개로 이루어진다. 3성은 판 뒤에서 은은하게 빛이 맥동하고 별이 살짝 떠다닌다.
    /// 성급이 올라가면 별이 크게 튀어나왔다 자리 잡는다. 팀 파일(GridWorldPreparationView, UnitBase)은 수정하지 않는다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InGameStarBadges : MonoBehaviour
    {
        private const string PrepLabelName = "PreparationStarLevel";
        private const string BadgeName = "StarBadge";

        [SerializeField] private Sprite _starSprite;
        [SerializeField, Min(0.05f)] private float _starSize = 0.26f;
        [SerializeField, Min(0.05f)] private float _combatStarSize = 0.2f;
        [SerializeField] private int _prepSortingOrder = 520;
        [SerializeField] private int _combatSortingOrder = 1010;
        [SerializeField, Tooltip("배치 단계: 유닛 기준점에서 배지 중심까지의 높이(칸 단위). 모든 유닛이 같은 높이에 오도록 고정값을 쓴다.")]
        private float _prepHeightCells = 1.05f;
        [SerializeField, Tooltip("전투 단계: 같은 높이 기준(칸 단위). 체력바 위에 오도록 배치 단계보다 조금 높다.")]
        private float _combatHeightCells = 1.25f;
        [SerializeField, Tooltip("마왕군 체력바 높이(칸 단위). 유닛마다 스프라이트 키가 달라 제각각이던 것을 같은 높이로 맞춘다.")]
        private float _healthBarHeightCells = 0.95f;

        // 성급별 테두리·빛 색 (1성 청동, 2성 은청, 3성 금홍)
        private static readonly Color[] RimColors =
        {
            new Color(0.80f, 0.52f, 0.30f), new Color(0.62f, 0.82f, 1.00f), new Color(1.00f, 0.72f, 0.28f),
        };

        private sealed class Badge
        {
            public Transform root;
            public SpriteRenderer glow, plate, rim;
            public SpriteRenderer[] stars = new SpriteRenderer[3];
            public int shown;
            public float pop;
            public float size;
            public int order;
        }

        private readonly Dictionary<int, Badge> _badges = new Dictionary<int, Badge>();
        private readonly List<int> _stale = new List<int>();
        private Sprite _plateSprite, _rimSprite, _glowSprite;
        private float _nextScan;

        private void LateUpdate()
        {
            AlignHealthBars();
            // 그리드가 글자를 새로 만들거나 갱신하는 즉시(렌더 전에) 숨겨서, 스캔 사이에 글자가 비치지 않게 한다.
            foreach (var label in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.gameObject.name != PrepLabelName) continue;
                var meshRenderer = label.GetComponent<MeshRenderer>();
                if (meshRenderer != null && meshRenderer.enabled) meshRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.25f;
                Scan();
            }
            Animate(Time.unscaledDeltaTime);
        }

        // ───────────── 어디에 달지 찾기

        private void Scan()
        {
            EnsureSprites();
            foreach (var label in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.gameObject.name != PrepLabelName) continue;
                int stars = ParseStars(label.text);
                var meshRenderer = label.GetComponent<MeshRenderer>();
                if (meshRenderer != null && meshRenderer.enabled) meshRenderer.enabled = false; // 글자 대신 배지
                float cell = label.characterSize / 0.06f; // 그리드가 characterSize를 칸 크기 × 0.06으로 잡는다
                var anchor = label.transform.parent != null ? label.transform.parent : label.transform;
                Place(label.transform, anchor, stars, _starSize * cell, _prepSortingOrder, _prepHeightCells * cell, false);
            }

            foreach (var unit in UnitRegistry.GetUnits(UnitSide.DemonArmy))
            {
                if (unit == null || unit.currentState == UnitState.Dead || unit.statData == null) continue;
                Place(unit.transform, unit.transform, Mathf.Clamp(unit.statData.starLevel, 1, 3), _combatStarSize, _combatSortingOrder,
                    _combatHeightCells, true);
            }

            // 사라진 대상의 기록 정리
            _stale.Clear();
            foreach (var pair in _badges)
                if (pair.Value.root == null) _stale.Add(pair.Key);
            foreach (var key in _stale) _badges.Remove(key);
        }

        /// <summary>마왕군 체력바를 유닛 기준점 위 같은 높이로 옮긴다. 체력바는 유닛 스프라이트의 맨 위를 따라 만들어져 키가 다르면 높이가 달랐다.</summary>
        private void AlignHealthBars()
        {
            foreach (var unit in UnitRegistry.GetUnits(UnitSide.DemonArmy))
            {
                if (unit == null) continue;
                var bar = unit.transform.Find("HealthBar");
                if (bar == null) continue;
                var p = bar.position;
                bar.position = new Vector3(unit.transform.position.x, unit.transform.position.y + _healthBarHeightCells, p.z);
            }
        }

        private static int ParseStars(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            for (int i = text.Length - 1; i >= 0; i--)
                if (char.IsDigit(text[i])) return Mathf.Clamp(text[i] - '0', 1, 3);
            return 1;
        }

        private void Place(Transform parent, Transform anchor, int stars, float starSize, int order, float heightWorld, bool compensateScale)
        {
            int key = parent.GetInstanceID();
            if (!_badges.TryGetValue(key, out var badge) || badge.root == null)
            {
                badge = Create(parent, starSize, order);
                _badges[key] = badge;
            }
            if (compensateScale)
            {
                Vector3 s = parent.lossyScale;
                badge.root.localScale = new Vector3(1f / SafeAbs(s.x), 1f / SafeAbs(s.y), 1f);
            }
            // 유닛마다 스프라이트 키가 달라도 배지는 같은 높이·가운데에 둔다(기준점 위로 고정 높이)
            badge.root.position = new Vector3(anchor.position.x, anchor.position.y + heightWorld, parent.position.z);
            if (badge.shown != stars)
            {
                if (badge.shown != 0 && stars > badge.shown) badge.pop = 0.35f; // 성급 상승 연출
                badge.shown = stars;
                Layout(badge);
            }
        }

        private static float SafeAbs(float v) => Mathf.Abs(v) > 0.0001f ? Mathf.Abs(v) : 1f;

        // ───────────── 배지 만들기

        private Badge Create(Transform parent, float starSize, int order)
        {
            var root = new GameObject(BadgeName).transform;
            root.SetParent(parent, false);
            var badge = new Badge { root = root, size = starSize, order = order };
            badge.glow = NewRenderer("Glow", root, _glowSprite, order);
            badge.plate = NewRenderer("Plate", root, _plateSprite, order + 1);
            badge.rim = NewRenderer("Rim", root, _rimSprite, order + 2);
            badge.plate.drawMode = badge.rim.drawMode = SpriteDrawMode.Sliced;
            badge.plate.color = new Color(0.06f, 0.04f, 0.1f, 0.82f);
            for (int i = 0; i < 3; i++) badge.stars[i] = NewRenderer("Star" + i, root, _starSprite, order + 3 + i);
            return badge;
        }

        private static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>별 개수에 맞춰 판 크기와 별 위치를 다시 잡는다.</summary>
        private void Layout(Badge b)
        {
            int n = b.shown;
            float star = b.size;
            float step = star * 0.82f;
            float width = star * 0.9f + step * (n - 1) + star * 0.5f;
            float height = star * 1.25f;
            Color rim = RimColors[Mathf.Clamp(n, 1, 3) - 1];

            b.plate.size = new Vector2(width, height);
            b.rim.size = new Vector2(width, height);
            b.rim.color = rim;
            b.glow.transform.localScale = new Vector3(width * 2.3f / 0.64f, height * 2.6f / 0.64f, 1f);
            b.glow.color = new Color(rim.r, rim.g, rim.b, n >= 3 ? 0.45f : (n == 2 ? 0.2f : 0f));

            float starScale = star / (_starSprite != null ? _starSprite.bounds.size.x : 1f);
            for (int i = 0; i < 3; i++)
            {
                var r = b.stars[i];
                bool on = i < n;
                r.enabled = on;
                if (!on) continue;
                r.transform.localPosition = new Vector3((i - (n - 1) * 0.5f) * step, 0f, 0f);
                r.transform.localScale = Vector3.one * starScale * (n >= 3 && i == 1 ? 1.2f : 1f); // 3성은 가운데 별이 가장 크다
                r.color = n == 1 ? new Color(0.92f, 0.85f, 0.75f) : Color.white;
            }
        }

        // ───────────── 움직임

        private void Animate(float dt)
        {
            float t = Time.unscaledTime;
            foreach (var badge in _badges.Values)
            {
                if (badge.root == null || badge.shown == 0) continue;
                float pop = 0f;
                if (badge.pop > 0f)
                {
                    badge.pop = Mathf.Max(0f, badge.pop - dt);
                    pop = Mathf.Sin(badge.pop / 0.35f * Mathf.PI) * 0.6f;
                }
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.2f);
                if (badge.shown >= 3)
                {
                    var c = badge.glow.color;
                    badge.glow.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.3f, 0.6f, pulse));
                }
                for (int i = 0; i < badge.shown; i++)
                {
                    var r = badge.stars[i];
                    float baseY = badge.shown >= 3 ? Mathf.Sin(t * 2.4f + i * 1.1f) * badge.size * 0.06f : 0f;
                    var p = r.transform.localPosition;
                    r.transform.localPosition = new Vector3(p.x, baseY, 0f);
                    float k = 1f + pop;
                    var s = r.transform.localScale;
                    float baseScale = badge.size / (_starSprite != null ? _starSprite.bounds.size.x : 1f)
                                      * (badge.shown >= 3 && i == 1 ? 1.2f : 1f);
                    r.transform.localScale = new Vector3(baseScale * k, baseScale * k, 1f);
                }
            }
        }

        // ───────────── 판·테두리·빛 이미지 (코드로 그린 작은 이미지)

        private void EnsureSprites()
        {
            if (_plateSprite != null) return;
            _plateSprite = MakeRounded(32, 9f, false);
            _rimSprite = MakeRounded(32, 9f, true);
            _glowSprite = MakeGlow(64);
        }

        private static Sprite MakeRounded(int n, float radius, bool outlineOnly)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 둥근 모서리 사각형의 안쪽 거리(양수 = 안쪽)
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (n - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (n - radius), 0f);
                    float dist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    float inside = Mathf.Clamp01(dist + 0.5f);
                    float a = outlineOnly ? Mathf.Clamp01(inside - Mathf.Clamp01(dist - 1.6f + 0.5f)) : inside;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        private static Sprite MakeGlow(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnDestroy()
        {
            foreach (var badge in _badges.Values)
                if (badge.root != null) Destroy(badge.root.gameObject);
            _badges.Clear();
        }
    }
}
