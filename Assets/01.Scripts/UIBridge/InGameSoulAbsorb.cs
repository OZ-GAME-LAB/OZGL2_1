using System.Collections.Generic;
using System.Reflection;
using OZGL2.Progression;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 용사가 죽으면 그 자리에서 영혼(빛 덩어리)이 떠올라 화면 위 경험치바로 날아가 흡수되는 연출.
    /// 도착하면 경험치바가 반짝이며 살짝 부푼다. 경험치 수치 자체는 기존 시스템이 그대로 올린다(연출만 얹는다).
    /// 보스처럼 경험치가 큰 용사는 영혼이 여러 개로 갈라져 날아간다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InGameSoulAbsorb : MonoBehaviour
    {
        [SerializeField] private Color _soulColor = new Color(0.55f, 0.85f, 1f, 1f);
        [SerializeField, Min(0.2f)] private float _riseSeconds = 0.28f;
        [SerializeField, Min(0.2f)] private float _flySeconds = 0.75f;
        [SerializeField, Min(8f)] private float _orbSize = 46f;
        [SerializeField, Range(1, 8)] private int _bossOrbCount = 4;
        [SerializeField, Min(1)] private int _bossExpThreshold = 20;
        [SerializeField, Min(10)] private int _maxActiveOrbs = 60;

        private const int TrailCount = 5;

        private sealed class Orb
        {
            public RectTransform root;
            public Image glow, core;
            public RectTransform[] trail = new RectTransform[TrailCount];
            public Image[] trailImg = new Image[TrailCount];
            public Vector2 start, rise, control;
            public float delay, t, size;
            public bool flying;
            public int xp; // 이 영혼이 도착해야 경험치바에 반영되는 경험치
        }

        private readonly List<Orb> _active = new List<Orb>();
        private readonly Stack<Orb> _pool = new Stack<Orb>();
        private readonly List<(RectTransform rect, Image img, float t)> _flashes = new List<(RectTransform, Image, float)>();
        private Canvas _canvas;
        private Sprite _glowSprite, _ringSprite;
        private FieldInfo _fillField;

        /// <summary>지금 화면에서 경험치바를 채우고 있는 그림(복제본). 경험치바 스타일(InGameXpBarStyle)이 여기에 꾸밈을 얹는다.</summary>
        public static Image FillImage { get; private set; }
        /// <summary>바에 지금 보여 주는 채움 비율(0~1) — 영혼이 도착한 만큼만 오른 값.</summary>
        public static float ShownNorm { get; private set; }
        /// <summary>지정하면 채움 그림을 팀 스프라이트 대신 이것으로 그린다(꾸밈용 단색 흰 그림).</summary>
        public static Sprite OverrideFillSprite { get; set; }
        private Image _orig;   // 팀 UI의 경험치바 채움(숨기고 둔다)
        private Image _fill;   // 같은 자리에 둔 우리 쪽 복제본(값·반짝임을 우리가 쓴다 — 팀 코드와 값을 다투지 않는다)
        private float _punch;
        private int _pendingXp;      // 이미 계정에는 들어갔지만 영혼이 아직 도착하지 않아 바에는 반영하지 않은 경험치
        private float _shownNorm = -1f;
        private int _lastLevel;

        private void OnEnable() => UnitBase.OnHeroKilled += OnHeroKilled;

        private void OnDisable()
        {
            UnitBase.OnHeroKilled -= OnHeroKilled;
            ReleaseFill();
        }

        private void OnDestroy()
        {
            ReleaseFill();
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ───────────── 처치 → 영혼 생성

        private void OnHeroKilled(UnitBase hero, int exp)
        {
            if (hero == null) return;
            var cam = Camera.main;
            if (cam == null || !FindFill(out _)) return;
            EnsureCanvas();

            Vector3 s = cam.WorldToScreenPoint(hero.transform.position + Vector3.up * 0.4f);
            if (s.z < 0f) return;
            var start = new Vector2(s.x, s.y);
            int count = exp >= _bossExpThreshold ? _bossOrbCount : 1;
            float size = _orbSize * Mathf.Max(0.8f, Screen.height / 1080f) * (count > 1 ? 1.15f : 1f);

            // 계정에 실제로 들어간 경험치(획득 배율 반영)를 영혼 수로 나눠, 영혼이 도착할 때마다 그만큼씩 바가 오른다.
            var mawang = MawangXpBridge.Mawang;
            int gain = exp > 0 ? Mathf.Max(1, Mathf.RoundToInt(exp * (mawang != null ? Mathf.Max(0f, mawang.XpGainMult) : 1f))) : 0;
            int share = gain / count, remainder = gain - share * count;
            for (int i = 0; i < count && _active.Count < _maxActiveOrbs; i++)
            {
                int xp = share + (i == count - 1 ? remainder : 0);
                Spawn(start, size, i * 0.07f, count > 1, xp);
            }
        }

        private void Spawn(Vector2 start, float size, float delay, bool scatter, int xp)
        {
            var orb = _pool.Count > 0 ? _pool.Pop() : CreateOrb();
            orb.size = size;
            orb.delay = delay;
            orb.t = 0f;
            orb.flying = false;
            orb.xp = xp;
            _pendingXp += xp;
            float spread = scatter ? Random.Range(-70f, 70f) : Random.Range(-18f, 18f);
            orb.start = start;
            orb.rise = start + new Vector2(spread, Random.Range(36f, 70f) * Mathf.Max(0.8f, Screen.height / 1080f));
            orb.root.gameObject.SetActive(true);
            orb.root.sizeDelta = new Vector2(size, size);
            orb.root.localScale = Vector3.zero;
            orb.root.anchoredPosition = start;
            ApplyColor(orb, 0f);
            _active.Add(orb);
        }

        private Orb CreateOrb()
        {
            var orb = new Orb();
            orb.root = NewImage("Soul", _canvas.transform, out orb.glow, _glowSprite);
            orb.glow.color = Color.white;
            var coreRect = NewImage("Core", orb.root, out orb.core, _glowSprite);
            coreRect.anchorMin = coreRect.anchorMax = new Vector2(0.5f, 0.5f);
            coreRect.sizeDelta = Vector2.zero;
            coreRect.anchorMin = new Vector2(0.3f, 0.3f);
            coreRect.anchorMax = new Vector2(0.7f, 0.7f);
            coreRect.offsetMin = coreRect.offsetMax = Vector2.zero;
            for (int i = 0; i < TrailCount; i++)
            {
                orb.trail[i] = NewImage("Trail" + i, _canvas.transform, out orb.trailImg[i], _glowSprite);
                orb.trail[i].gameObject.SetActive(false);
            }
            return orb;
        }

        private RectTransform NewImage(string name, Transform parent, out Image image, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return rect;
        }

        // ───────────── 매 프레임: 떠오르기 → 곡선 비행 → 흡수

        private void Update()
        {
            float dt = Time.unscaledDeltaTime * Mathf.Clamp(Time.timeScale, 1f, 3f);
            bool hasTarget = FindFill(out Vector2 target);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var o = _active[i];
                o.t += dt;
                float local = o.t - o.delay;
                if (local < 0f) continue;

                if (!hasTarget) { Release(o, i); continue; } // 경험치바가 사라지면(씬 전환 등) 조용히 정리

                if (!o.flying)
                {
                    float k = Mathf.Clamp01(local / _riseSeconds);
                    float e = 1f - (1f - k) * (1f - k); // ease-out
                    o.root.anchoredPosition = Vector2.LerpUnclamped(o.start, o.rise, e);
                    float pop = Mathf.Sin(k * Mathf.PI * 0.5f);
                    o.root.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.1f, pop);
                    ApplyColor(o, Mathf.Clamp01(k * 1.5f));
                    HideTrail(o);
                    if (k >= 1f)
                    {
                        o.flying = true;
                        o.t = o.delay; // 비행 시간은 다시 0부터
                        Vector2 mid = (o.rise + target) * 0.5f;
                        Vector2 dir = (target - o.rise).normalized;
                        Vector2 perp = new Vector2(-dir.y, dir.x);
                        o.control = mid + perp * Random.Range(-1f, 1f) * Mathf.Min(260f, (target - o.rise).magnitude * 0.45f);
                    }
                    continue;
                }

                float f = Mathf.Clamp01(local / _flySeconds);
                float ease = f * f * (3f - 2f * f);
                ease = Mathf.Lerp(f * f, ease, 0.5f); // 처음엔 천천히, 끝에서 빨려 들어가듯 가속
                Vector2 p = Bezier(o.rise, o.control, target, ease);
                o.root.anchoredPosition = p;
                o.root.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.55f, f);
                ApplyColor(o, 1f);
                for (int j = 0; j < TrailCount; j++)
                {
                    float lag = Mathf.Max(0f, f - (j + 1) * 0.045f);
                    float le = Mathf.Lerp(lag * lag, lag * lag * (3f - 2f * lag), 0.5f);
                    var tr = o.trail[j];
                    tr.gameObject.SetActive(f > 0.02f);
                    tr.anchoredPosition = Bezier(o.rise, o.control, target, le);
                    float ts = o.size * Mathf.Lerp(0.7f, 0.2f, (j + 1f) / TrailCount);
                    tr.sizeDelta = new Vector2(ts, ts);
                    var c = _soulColor;
                    o.trailImg[j].color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.55f, 0.08f, (j + 1f) / TrailCount));
                }

                if (f >= 1f)
                {
                    Absorb(target);
                    Release(o, i);
                }
            }

            UpdateFlashes(dt);
        }

        private void LateUpdate()
        {
            if (_orig == null || _fill == null) return;
            float dt = Time.unscaledDeltaTime;
            if (_orig.enabled) _orig.enabled = false; // 팀 UI가 다시 켜도 매 프레임 끈다
            _fill.enabled = true;
            _fill.sprite = OverrideFillSprite != null ? OverrideFillSprite : _orig.sprite;
            _fill.material = _orig.material;
            UpdateXpDisplay(dt);
            UpdatePunch(dt);
        }

        /// <summary>
        /// 경험치바가 처치 순간에 한꺼번에 오르지 않고, 영혼이 하나 도착할 때마다 그 몫만큼 차오르게 보여 준다.
        /// 실제 경험치는 처치 즉시 올라 있으므로, 아직 도착하지 않은 몫(_pendingXp)만큼을 빼서 표시한다.
        /// 영혼이 모두 도착하면 실제 값으로 부드럽게 따라붙는다.
        /// </summary>
        private void UpdateXpDisplay(float dt)
        {
            var mawang = MawangXpBridge.Mawang;
            if (mawang == null) return;
            int need = Mathf.Max(1, mawang.XpToNext);
            float real = Mathf.Clamp01(mawang.Xp / (float)need);
            float target = _pendingXp > 0 ? Mathf.Clamp01((mawang.Xp - _pendingXp) / (float)need) : real;
            if (_shownNorm < 0f) _shownNorm = real;
            if (mawang.Level != _lastLevel)
            {
                if (_lastLevel != 0) _shownNorm = _pendingXp > 0 || mawang.Level > _lastLevel ? 0f : real; // 레벨이 바뀌면 바가 비워진 채 다시 차오른다
                _lastLevel = mawang.Level;
            }
            _shownNorm = Mathf.Lerp(_shownNorm, target, 1f - Mathf.Exp(-14f * dt));
            ShownNorm = _shownNorm;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = _orig.fillMethod;
            _fill.fillOrigin = _orig.fillOrigin;
            _fill.fillClockwise = _orig.fillClockwise;
            _fill.fillAmount = _shownNorm;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private void ApplyColor(Orb o, float alpha)
        {
            var c = _soulColor;
            o.glow.color = new Color(c.r, c.g, c.b, 0.65f * alpha);
            o.core.color = new Color(1f, 1f, 1f, alpha);
        }

        private static void HideTrail(Orb o)
        {
            for (int j = 0; j < TrailCount; j++)
                if (o.trail[j].gameObject.activeSelf) o.trail[j].gameObject.SetActive(false);
        }

        private void Release(Orb o, int index)
        {
            _pendingXp = Mathf.Max(0, _pendingXp - o.xp); // 도착(또는 사라짐) — 이 몫만큼 바에 반영된다
            o.xp = 0;
            HideTrail(o);
            o.root.gameObject.SetActive(false);
            _active.RemoveAt(index);
            _pool.Push(o);
        }

        // ───────────── 흡수 효과 (경험치바가 반짝 + 부푼다)

        private void Absorb(Vector2 target)
        {
            _punch = 0.22f;
            var ring = NewImage("AbsorbFlash", _canvas.transform, out var img, _ringSprite);
            ring.anchoredPosition = target;
            ring.sizeDelta = Vector2.one * 30f;
            img.color = new Color(_soulColor.r, _soulColor.g, _soulColor.b, 0.9f);
            _flashes.Add((ring, img, 0f));
        }

        private void UpdateFlashes(float dt)
        {
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var (rect, img, t) = _flashes[i];
                t += dt;
                float k = t / 0.3f;
                if (k >= 1f || rect == null) { if (rect != null) Destroy(rect.gameObject); _flashes.RemoveAt(i); continue; }
                float s = Mathf.Lerp(30f, 90f, k) * Mathf.Max(0.8f, Screen.height / 1080f);
                rect.sizeDelta = new Vector2(s, s);
                var c = img.color; c.a = 0.9f * (1f - k); img.color = c;
                _flashes[i] = (rect, img, t);
            }
        }

        private void UpdatePunch(float dt)
        {
            if (_fill == null) return;
            if (_punch > 0f) _punch = Mathf.Max(0f, _punch - dt);
            float k = _punch > 0f ? Mathf.Sin(_punch / 0.22f * Mathf.PI) : 0f;
            _fill.rectTransform.localScale = new Vector3(1f, 1f + 0.35f * k, 1f);
            Color baseColor = _orig != null ? _orig.color : Color.white;
            _fill.color = Color.Lerp(baseColor, new Color(1.5f, 1.5f, 1.8f, baseColor.a), k);
        }

        // ───────────── 경험치바 찾기

        /// <summary>지금 화면에 보이는 경험치바 채움 이미지를 찾아 복제본을 만들고, 현재 채워진 끝 지점의 화면 좌표를 돌려준다.</summary>
        private bool FindFill(out Vector2 target)
        {
            target = Vector2.zero;
            if (_orig == null || !_orig.gameObject.activeInHierarchy)
            {
                ReleaseFill();
                _fillField = _fillField ?? typeof(UIBattleMutedPreviewView).GetField("_experienceFill", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_fillField == null) return false;
                foreach (var view in FindObjectsByType<UIBattleMutedPreviewView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var img = _fillField.GetValue(view) as Image;
                    if (img != null && img.gameObject.activeInHierarchy) { _orig = img; break; }
                }
                if (_orig == null) return false;
                CreateFillClone();
            }
            if (_fill == null) return false;

            var canvas = _fill.canvas;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            _fill.rectTransform.GetWorldCorners(corners);
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            float fillAmount = Mathf.Clamp(_shownNorm >= 0f ? _shownNorm : 0.04f, 0.04f, 1f);
            target = new Vector2(Mathf.Lerp(bl.x, tr.x, fillAmount), (bl.y + tr.y) * 0.5f);
            return true;
        }

        private void CreateFillClone()
        {
            var go = Instantiate(_orig.gameObject, _orig.transform.parent);
            go.name = "ExperienceFillSoul";
            // 복제본은 그림만 남긴다 — 팀 스크립트가 복제돼 같이 동작하지 않게
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is Graphic) && mb != null) Destroy(mb);
            go.transform.SetSiblingIndex(_orig.transform.GetSiblingIndex() + 1);
            _fill = go.GetComponent<Image>();
            FillImage = _fill;
            _fill.raycastTarget = false;
            _shownNorm = -1f;
        }

        /// <summary>복제본을 지우고 원래 경험치바를 되돌린다.</summary>
        private void ReleaseFill()
        {
            if (_fill != null) Destroy(_fill.gameObject);
            _fill = null;
            FillImage = null;
            if (_orig != null) _orig.enabled = true;
            _orig = null;
            _shownNorm = -1f;
        }

        // ───────────── 캔버스·스프라이트

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("SoulAbsorbCanvas", typeof(RectTransform));
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 26; // 떠오르는 숫자(25) 위, 시너지 설명(30)·팝업(100) 아래
            _glowSprite = MakeRadialSprite(64, 0.25f, 1.6f);
            _ringSprite = MakeRingSprite(64);
        }

        private static Sprite MakeRadialSprite(int size, float hardness, float falloff)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, falloff);
                    a = Mathf.Lerp(a, a > 0f ? 1f : 0f, hardness * Mathf.Clamp01((0.35f - d) * 6f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRingSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) / 0.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
