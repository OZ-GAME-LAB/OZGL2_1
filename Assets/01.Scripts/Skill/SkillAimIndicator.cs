using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>
    /// 스킬 조준 표시(사거리·범위·방향). 코드로 만든 고해상도 텍스처로 또렷하게 그리고 부드럽게 움직인다.
    /// - 원형 범위: 가장자리가 밝은 반투명 면 + 얇은 외곽선(안쪽 은은한 빛) + 천천히 도는 점선 고리 + 사방 눈금 + 중심 십자 + 등장 시 톡 튀어나오는 효과 + 숨쉬듯 깜빡이는 외곽선.
    ///   위쪽에 "×N" 배지로 범위 안에 들어온 대상 수를 보여 준다(0이면 흐리게).
    /// - 방향형: 실제 피해 폭(반경×2)만큼의 통로(둥근 띠) + 꼬리에서 머리로 흐르는 화살표(그라데이션 몸통, 흘러가는 쉐브론, 윤곽 있는 화살촉, 끝 고리).
    /// 색은 스킬 종류에 따라 호출자가 정한다. 모든 움직임은 unscaledTime 기준이라 배속·일시정지에서도 일정하게 보인다.
    /// </summary>
    public sealed class SkillAimIndicator : MonoBehaviour
    {
        private const int CircleOrder = 18;
        private const int ArrowOrder = 18;
        private const int TexSize = 256;

        private SkillAimIndicatorStyleSO _style; // Resources/SkillAimIndicatorStyle (없으면 코드로 그린 기본 표시)
        private readonly Dictionary<SpriteRenderer, Vector3> _norm = new Dictionary<SpriteRenderer, Vector3>();
        private Color _accent = Color.white;
        private float _shownAt;
        private bool _directional;

        // 원형
        private Transform _circle;
        private SpriteRenderer _fill, _ring, _dashes, _ticks, _cross;
        private TextMesh _count;
        private Transform _countAnchor;

        // 방향형
        private Transform _arrow;
        private SpriteRenderer _corridor, _shaft, _head, _endRing;
        private readonly List<SpriteRenderer> _chevrons = new List<SpriteRenderer>();

        // 만든 스프라이트(정리용)
        private readonly List<Sprite> _sprites = new List<Sprite>();
        private Sprite _spEnd;
        private Sprite _spFill, _spRing, _spDash, _spTicks, _spCross, _spCorridor, _spShaft, _spHead, _spChevron;

        private void Awake()
        {
            _style = Resources.Load<SkillAimIndicatorStyleSO>("SkillAimIndicatorStyle");
            BuildCircle();
            BuildArrow();
            Hide();
        }

        private void OnDestroy()
        {
            foreach (var sprite in _sprites)
                if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
        }

        // ───────────── 공개 API

        public void Begin(Color accent, bool directional)
        {
            _accent = accent;
            _directional = directional;
            _shownAt = Time.unscaledTime;
            _circle.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
            Tint();
        }

        public void ShowCircle(Vector3 center, float radius, int targetCount)
        {
            if (_directional) return;
            _arrow.gameObject.SetActive(false);
            _circle.gameObject.SetActive(true);
            float diameter = Mathf.Min(radius * 2f, 40f);
            float age = Time.unscaledTime - _shownAt;
            float popTime = _style != null ? _style.popSeconds : 0.18f;
            float pop = popTime <= 0f ? 1f : EaseOutBack(Mathf.Clamp01(age / popTime)); // 처음 나타날 때 톡 튀어나옴
            float breathe = 1f + 0.012f * Mathf.Sin(Time.unscaledTime * 3.2f); // 아주 미세하게 숨쉼
            _circle.position = center;
            _circle.localScale = Vector3.one * (diameter * Mathf.Max(0.01f, pop) * breathe);

            float dashSpeed = _style != null ? _style.dashSpinSpeed : 16f;
            float tickSpeed = _style != null ? _style.tickSpinSpeed : -6f;
            _dashes.transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * dashSpeed);
            _ticks.transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * tickSpeed);
            float glow = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 4.2f);
            SetAlpha(_ring, glow);
            SetAlpha(_fill, 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.4f));

            // 대상 수 배지: 원 위쪽에 고정 크기로
            float badgeScale = Mathf.Clamp(diameter * 0.06f, 0.18f, 0.5f);
            _countAnchor.localPosition = new Vector3(0f, 0.5f + 0.09f, 0f);
            _countAnchor.localScale = Vector3.one * (badgeScale / Mathf.Max(0.01f, diameter));
            _count.text = "×" + targetCount;
            _count.color = targetCount > 0 ? new Color(1f, 0.97f, 0.85f, 1f) : new Color(1f, 1f, 1f, 0.45f);
        }

        public void ShowDirection(Vector3 from, Vector3 to, float radius, int targetCount)
        {
            if (!_directional) return;
            _circle.gameObject.SetActive(false);
            Vector3 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.05f) { _arrow.gameObject.SetActive(false); return; }
            _arrow.gameObject.SetActive(true);

            Vector3 dir = delta / len;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, angle);
            float age = Time.unscaledTime - _shownAt;
            float pop = Mathf.Clamp01(age / 0.14f);
            float corridorW = Mathf.Max(radius * 2f, 0.6f);          // 실제로 맞는 폭
            float shaftW = Mathf.Clamp(radius * 0.55f, 0.28f, 0.9f);
            float headLen = Mathf.Min(shaftW * 2.6f, len * 0.6f);
            float headW = shaftW * 2.4f;
            float shaftLen = Mathf.Max(len - headLen, 0.02f);

            // 통로(피해가 들어가는 범위)
            _corridor.transform.SetPositionAndRotation(from + dir * (len * 0.5f), rot);
            _corridor.size = new Vector2(len, corridorW * pop);
            SetAlpha(_corridor, 0.9f);

            // 몸통(꼬리 → 머리로 진해지는 그라데이션)
            _shaft.transform.SetPositionAndRotation(from + dir * (shaftLen * 0.5f), rot);
            SetScale(_shaft, shaftLen, shaftW * 0.55f);

            // 화살촉 + 끝 고리
            _head.transform.SetPositionAndRotation(from + dir * (shaftLen + headLen * 0.5f), rot);
            SetScale(_head, headLen, headW);
            _endRing.transform.SetPositionAndRotation(to, Quaternion.identity);
            float endSize = Mathf.Max(corridorW * 0.9f, 0.5f);
            SetScale(_endRing, endSize, endSize);
            SetAlpha(_endRing, 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f));

            // 꼬리에서 머리로 흘러가는 쉐브론
            float spacing = Mathf.Max(shaftW * 2.2f, 0.55f);
            int wanted = Mathf.Clamp(Mathf.FloorToInt(shaftLen / spacing), 0, 12);
            while (_chevrons.Count < wanted) _chevrons.Add(MakeRenderer("chevron", _arrow, _spChevron, ArrowOrder + 1));
            float flowSpeed = _style != null ? _style.flowSpeed : 1.6f;
            float flow = (Time.unscaledTime * spacing * flowSpeed) % spacing;
            for (int i = 0; i < _chevrons.Count; i++)
            {
                var chevron = _chevrons[i];
                if (i >= wanted) { chevron.enabled = false; continue; }
                float d = i * spacing + flow;
                if (d > shaftLen) { chevron.enabled = false; continue; }
                chevron.enabled = true;
                float fade = Mathf.Sin(Mathf.Clamp01(d / shaftLen) * Mathf.PI); // 양 끝에서 사라짐
                chevron.transform.SetPositionAndRotation(from + dir * d, rot);
                SetScale(chevron, shaftW * 0.9f, shaftW * 0.9f);
                var c = _accent; c.a = Mathf.Clamp01(0.15f + 0.85f * fade);
                chevron.color = Color.Lerp(c, Color.white, 0.35f * fade);
            }
        }

        /// <summary>범위 안 대상 수 배지는 방향형에서는 쓰지 않는다(통로 안의 대상은 하이라이트로 보여 준다).</summary>
        public void Hide()
        {
            if (_circle != null) _circle.gameObject.SetActive(false);
            if (_arrow != null) _arrow.gameObject.SetActive(false);
        }

        // ───────────── 색 적용

        private void Tint()
        {
            Color a = _style != null && !_style.tintWithSkillColor ? Color.white : _accent;
            _fill.color = new Color(a.r, a.g, a.b, 1f);
            _ring.color = new Color(a.r, a.g, a.b, 1f);
            _dashes.color = new Color(Mathf.Lerp(a.r, 1f, 0.35f), Mathf.Lerp(a.g, 1f, 0.35f), Mathf.Lerp(a.b, 1f, 0.35f), 0.85f);
            _ticks.color = new Color(1f, 1f, 1f, 0.9f);
            _cross.color = new Color(1f, 1f, 1f, 0.9f);
            _corridor.color = new Color(a.r, a.g, a.b, 0.9f);
            _shaft.color = new Color(a.r, a.g, a.b, 1f);
            _head.color = new Color(Mathf.Lerp(a.r, 1f, 0.2f), Mathf.Lerp(a.g, 1f, 0.2f), Mathf.Lerp(a.b, 1f, 0.2f), 1f);
            _endRing.color = new Color(a.r, a.g, a.b, 0.7f);
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            var c = renderer.color; c.a = alpha; renderer.color = c;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ───────────── 구성

        private SpriteRenderer MakeRenderer(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            Vector3 norm = Vector3.one;
            if (sprite != null && sprite.bounds.size.x > 0.0001f && sprite.bounds.size.y > 0.0001f)
                norm = new Vector3(1f / sprite.bounds.size.x, 1f / sprite.bounds.size.y, 1f);
            _norm[renderer] = norm;
            go.transform.localScale = norm;
            return renderer;
        }

        private void BuildCircle()
        {
            _spFill = Pick(_style != null ? _style.fill : null, MakeFill);
            _spRing = Pick(_style != null ? _style.ring : null, MakeRing);
            _spDash = Pick(_style != null ? _style.dashRing : null, MakeDashRing);
            _spTicks = Pick(_style != null ? _style.ticks : null, MakeTicks);
            _spCross = Pick(_style != null ? _style.cross : null, MakeCross);

            _circle = new GameObject("Circle").transform;
            _circle.SetParent(transform, false);
            _fill = MakeRenderer("fill", _circle, _spFill, CircleOrder);
            _dashes = MakeRenderer("dashes", _circle, _spDash, CircleOrder + 1);
            _ticks = MakeRenderer("ticks", _circle, _spTicks, CircleOrder + 1);
            _ring = MakeRenderer("ring", _circle, _spRing, CircleOrder + 2);
            _cross = MakeRenderer("cross", _circle, _spCross, CircleOrder + 2);

            var anchor = new GameObject("CountAnchor").transform;
            anchor.SetParent(_circle, false);
            _countAnchor = anchor;
            _count = anchor.gameObject.AddComponent<TextMesh>();
            _count.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _count.fontSize = 64;
            _count.characterSize = 0.1f;
            _count.fontStyle = FontStyle.Bold;
            _count.anchor = TextAnchor.LowerCenter;
            _count.alignment = TextAlignment.Center;
            var meshRenderer = anchor.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _count.font.material;
            meshRenderer.sortingOrder = CircleOrder + 5;
            if (_style == null || !_style.showTargetCount) anchor.gameObject.SetActive(false);
        }

        private void BuildArrow()
        {
            _spCorridor = Pick(_style != null ? _style.corridor : null, MakeCorridor);
            _spShaft = Pick(_style != null ? _style.shaft : null, MakeShaft);
            _spHead = Pick(_style != null ? _style.head : null, MakeHead);
            _spChevron = Pick(_style != null ? _style.chevron : null, MakeChevron);
            _spEnd = _style != null && _style.endRing != null ? _style.endRing : _spRing;

            _arrow = new GameObject("Arrow").transform;
            _arrow.SetParent(transform, false);
            _corridor = MakeRenderer("corridor", _arrow, _spCorridor, ArrowOrder);
            _corridor.drawMode = SpriteDrawMode.Sliced;
            _shaft = MakeRenderer("shaft", _arrow, _spShaft, ArrowOrder + 1);
            _head = MakeRenderer("head", _arrow, _spHead, ArrowOrder + 2);
            _endRing = MakeRenderer("endRing", _arrow, _spEnd, ArrowOrder + 1);
        }

        /// <summary>에셋에 이미지가 지정돼 있으면 그것을, 아니면 코드로 그린 기본 이미지를 쓴다. 기본 이미지만 정리 대상(Register)이다.</summary>
        private Sprite Pick(Sprite custom, System.Func<Sprite> makeDefault)
        {
            return custom != null ? custom : Register(makeDefault());
        }

        /// <summary>이미지 한 장의 원래 크기(월드 단위)를 1x1로 맞추는 배율 — 어떤 크기의 이미지를 넣어도 같은 규칙으로 늘어나게 한다.</summary>
        private void SetScale(SpriteRenderer renderer, float x, float y)
        {
            Vector3 n = _norm.TryGetValue(renderer, out var v) ? v : Vector3.one;
            renderer.transform.localScale = new Vector3(x * n.x, y * n.y, 1f);
        }

        private Sprite Register(Sprite sprite)
        {
            _sprites.Add(sprite);
            return sprite;
        }

        // ───────────── 텍스처 생성 (전부 흰색+알파로 만들고 SpriteRenderer.color로 물들인다)

        private static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        }

        private static float Aa(float distanceToEdge, float px) => Mathf.Clamp01(0.5f + distanceToEdge / px);

        /// <summary>가장자리로 갈수록 진해지는 반투명 면(가운데는 거의 비침) + 바깥 경계는 부드럽게 마감.</summary>
        private static Sprite MakeFill()
        {
            var tex = NewTex(TexSize, TexSize);
            float c = (TexSize - 1) * 0.5f, px = 1f / (TexSize * 0.5f);
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (TexSize * 0.5f);
                    float edge = Aa(0.985f - r, px * 1.5f);
                    float body = Mathf.Lerp(0.07f, 0.30f, Mathf.SmoothStep(0.25f, 1f, r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, body * edge));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
        }

        /// <summary>또렷한 외곽선 + 안쪽으로 번지는 은은한 빛.</summary>
        private static Sprite MakeRing()
        {
            var tex = NewTex(TexSize, TexSize);
            float c = (TexSize - 1) * 0.5f, half = TexSize * 0.5f, px = 1f / half;
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / half;
                    float core = Aa(0.985f - r, px) * Aa(r - 0.945f, px);                 // 선명한 선 (두께 약 4%)
                    float innerGlow = r < 0.945f ? Mathf.Exp(-(0.945f - r) * 22f) * 0.45f : 0f; // 안쪽 빛
                    float outerGlow = r > 0.985f ? Mathf.Exp(-(r - 0.985f) * 80f) * 0.25f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(core + innerGlow + outerGlow)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
        }

        /// <summary>안쪽 점선 고리(천천히 돈다).</summary>
        private static Sprite MakeDashRing()
        {
            var tex = NewTex(TexSize, TexSize);
            const int dashes = 40;
            float c = (TexSize - 1) * 0.5f, half = TexSize * 0.5f, px = 1f / half;
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    Vector2 p = new Vector2(x - c, y - c) / half;
                    float r = p.magnitude;
                    float angle = (Mathf.Atan2(p.y, p.x) + Mathf.PI) / (2f * Mathf.PI) * dashes;
                    float within = angle - Mathf.Floor(angle);                  // 0..1 한 칸 안의 위치
                    float dash = Aa(0.3f - Mathf.Abs(within - 0.5f), 0.08f); // 한 칸의 약 60%만 칠한 점
                    float radial = Aa(0.012f - Mathf.Abs(r - 0.86f), px);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, radial * dash));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
        }

        /// <summary>외곽선 안쪽 사방(상하좌우)의 작은 삼각 눈금.</summary>
        private static Sprite MakeTicks()
        {
            var tex = NewTex(TexSize, TexSize);
            float c = (TexSize - 1) * 0.5f, half = TexSize * 0.5f, px = 1f / half;
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    Vector2 p = new Vector2(x - c, y - c) / half;
                    float a = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        float ang = k * Mathf.PI * 0.5f;
                        // 눈금 축 기준 좌표로 회전
                        float u = p.x * Mathf.Cos(ang) + p.y * Mathf.Sin(ang);   // 바깥 방향
                        float v = -p.x * Mathf.Sin(ang) + p.y * Mathf.Cos(ang);  // 옆 방향
                        float depth = 0.93f - u;                                  // 외곽선 바로 안쪽에서 안으로
                        if (depth < 0f || depth > 0.075f) continue;
                        float halfWidth = 0.045f * (1f - depth / 0.075f);          // 안쪽으로 갈수록 좁아지는 삼각
                        a = Mathf.Max(a, Aa(halfWidth - Mathf.Abs(v), px) * Aa(depth, px) * Aa(0.075f - depth, px));
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
        }

        /// <summary>정중앙 점 + 가는 십자(가운데는 비워 둔다).</summary>
        private static Sprite MakeCross()
        {
            const int s = 128;
            var tex = NewTex(s, s);
            float c = (s - 1) * 0.5f, half = s * 0.5f, px = 1f / half;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    Vector2 p = new Vector2(x - c, y - c) / half;
                    float dot = Aa(0.045f - p.magnitude, px);
                    float armLen = Mathf.Clamp01((0.22f - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y))) * 40f);
                    float gap = Mathf.Clamp01((Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - 0.09f) * 40f);
                    float arms = Mathf.Max(Aa(0.008f - Mathf.Abs(p.x), px), Aa(0.008f - Mathf.Abs(p.y), px)) * armLen * gap;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(dot, arms) * 0.9f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        /// <summary>방향형 통로: 둥근 모서리의 반투명 띠(9분할로 늘어난다) + 얇은 테두리.</summary>
        private static Sprite MakeCorridor()
        {
            const int s = 64;
            const float corner = 18f;
            var tex = NewTex(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // 둥근 사각형의 부호 있는 거리
                    float qx = Mathf.Abs(x + 0.5f - s * 0.5f) - (s * 0.5f - corner);
                    float qy = Mathf.Abs(y + 0.5f - s * 0.5f) - (s * 0.5f - corner);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
                    float inside = Aa(-d - 0.5f, 1f);
                    float border = inside * Mathf.Clamp01(1f - Mathf.Abs(d + 1.2f) / 1.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(inside * 0.16f + border * 0.6f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
        }

        /// <summary>몸통: 꼬리는 투명, 머리 쪽으로 갈수록 진해지고 가운데가 가장 밝다.</summary>
        private static Sprite MakeShaft()
        {
            const int w = 128, h = 16;
            var tex = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float along = (x + 0.5f) / w;
                    float across = 1f - Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                    float a = Mathf.SmoothStep(0f, 1f, along) * (0.25f + 0.75f * Mathf.SmoothStep(0f, 1f, across));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * 0.85f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }

        /// <summary>+x 방향을 가리키는 화살촉: 속이 찬 삼각형 + 밝은 윤곽.</summary>
        private static Sprite MakeHead()
        {
            const int s = 128;
            var tex = NewTex(s, s);
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s;                        // 0 = 밑변, 1 = 끝점
                    float halfH = c * (1f - u);                       // 끝으로 갈수록 좁아짐
                    float dy = Mathf.Abs(y - c);
                    float distEdge = Mathf.Min(halfH - dy, (1f - u) * s * 0.45f) ;
                    float fill = Aa(distEdge, 1.2f);
                    float outline = fill * Mathf.Clamp01(1f - distEdge / 5f);
                    float a = Mathf.Clamp01(fill * 0.75f + outline * 0.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        /// <summary>+x 방향 쉐브론(>) — 꼬리에서 머리로 흘러가는 표시.</summary>
        private static Sprite MakeChevron()
        {
            const int s = 64;
            var tex = NewTex(s, s);
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dy = Mathf.Abs(y - c) / c;                    // 0 중앙 .. 1 가장자리
                    float line = 0.82f - dy * 0.55f;                    // x 위치: 가운데가 앞으로 튀어나온 > 모양
                    float dx = Mathf.Abs((x + 0.5f) / s - line);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Aa(0.065f - dx, 0.02f) * (1f - 0.35f * dy)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
