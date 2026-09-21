using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>
    /// 화면 하단 스킬 아이콘 바 — 장착된 스킬만 표시 (로드아웃).
    ///  - 즉시형: 아이콘 클릭 → 즉시 발동
    ///  - 조준형: 아이콘 누른 채 필드로 드래그 → 조준 링 → 떼면 발동 / 바 위에서 떼면 취소
    /// SkillManager public API 만 사용. 장착이 바뀌면 Rebuild() 호출.
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        [SerializeField] private float _iconSize = 78f;
        [SerializeField] private float _iconSpacing = 10f;
        [SerializeField] private float _bottomMargin = 40f;
        [SerializeField] private float _tabHeight = 44f;

        private SkillManager _manager;
        private Camera _camera;
        private Vector3 _casterPos;
        private Font _font;

        private readonly List<Icon> _icons = new List<Icon>();
        private readonly List<IDamageable> _previewBuf = new List<IDamageable>();

        private RectTransform _barRoot;  // 탭 줄 + 아이콘 줄을 감싸는 바깥 컨테이너
        private RectTransform _barPanel; // 아이콘 줄만
        private Icon _aiming;

        // 스킬이 23종이라 한 줄에 다 넣으면 너무 길어져서, 카테고리 탭으로 나눠서 보여준다.
        // null = 전체(필터 없음).
        private SkillCategory? _activeCategory;
        private readonly List<Tab> _tabs = new List<Tab>();
        private static readonly SkillCategory?[] TabCategories =
        {
            null, SkillCategory.Damage, SkillCategory.Debuff, SkillCategory.Buff, SkillCategory.Ultimate,
        };

        private Transform _reticle;
        private SpriteRenderer _reticleFill;
        private SpriteRenderer _reticleRing;
        private Sprite _disc;
        private Sprite _ring;

        // 방향형 스킬(화염 회오리 등)은 원형 사거리 대신 마왕→커서 방향 화살표로 조준을 보여준다.
        private Transform _arrow;
        private SpriteRenderer _arrowShaft;
        private SpriteRenderer _arrowHead;
        private Sprite _square;
        private Sprite _triangle;

        /// <summary>마왕(시전 기준점) 위치 갱신 — 화살표 시작점. 실제 씬에서 왕 위치가 나중에 확정되므로 호출자가 계속 갱신.</summary>
        public void SetCasterPosition(Vector3 pos) => _casterPos = pos;

        private static bool IsDirectional(SkillData d) =>
            d.effectType == SkillEffectType.MovingZone || d.effectType == SkillEffectType.LineDamage;

        private class Icon
        {
            public SkillRuntime Skill;
            public RectTransform Root;
            public Image Bg;
            public Image CooldownFill;
            public Text CdText;
            public Outline Border;
        }

        private class Tab
        {
            public SkillCategory? Category;
            public RectTransform Root;
            public Image Bg;
        }

        public void Bind(SkillManager manager, Camera cam, Vector3 casterPos)
        {
            _manager = manager;
            _camera = cam != null ? cam : Camera.main;
            _casterPos = casterPos;
            // 내장 LegacyRuntime 폰트는 한글을 시스템 폴백으로 그려서 작은 크기에서 흐리게 뭉개짐 — 한글이 있는
            // OS 폰트를 먼저 시도하고, 없으면 기존 내장 폰트로 폴백.
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "NanumGothic", "Noto Sans CJK KR" }, 32);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildCanvas();
            BuildReticle();
            Rebuild();
        }

        // ─────────────────────────────── UI 생성

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("SkillBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 탭 줄 + 아이콘 줄을 세로로 쌓는 바깥 컨테이너 — 화면 아래쪽에 고정.
            var rootGo = new GameObject("BarRoot");
            var rootRt = rootGo.AddComponent<RectTransform>();
            rootRt.SetParent(canvasGo.transform, false);
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, _bottomMargin);

            var vl = rootGo.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6f;
            vl.childAlignment = TextAnchor.LowerCenter;
            vl.childControlWidth = false;
            vl.childControlHeight = false;
            vl.childForceExpandWidth = false;
            vl.childForceExpandHeight = false;

            var rootFit = rootGo.AddComponent<ContentSizeFitter>();
            rootFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rootFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _barRoot = rootRt;

            BuildTabs(rootRt);

            var panelGo = new GameObject("Panel");
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.SetParent(rootRt, false);

            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            panelImg.raycastTarget = false;

            var hl = panelGo.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = _iconSpacing;
            hl.padding = new RectOffset(16, 16, 12, 12);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;

            var fit = panelGo.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _barPanel = panelRt;
        }

        /// <summary>카테고리 탭 5개(전체/딜/디버프/버프/궁극) — 클릭하면 그 카테고리 장착 스킬만 아이콘 줄에 표시.</summary>
        private void BuildTabs(RectTransform parent)
        {
            var rowGo = new GameObject("TabRow");
            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.SetParent(parent, false);

            var hl = rowGo.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;

            var fit = rowGo.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var category in TabCategories)
            {
                var tabGo = new GameObject($"Tab_{TabLabel(category)}");
                var tabRt = tabGo.AddComponent<RectTransform>();
                tabRt.SetParent(rowRt, false);
                tabRt.sizeDelta = new Vector2(100f, _tabHeight);
                var le = tabGo.AddComponent<LayoutElement>();
                le.preferredWidth = 100f;
                le.preferredHeight = _tabHeight;

                var bg = tabGo.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);

                var labelGo = new GameObject("label").AddComponent<RectTransform>();
                labelGo.SetParent(tabRt, false);
                labelGo.anchorMin = Vector2.zero;
                labelGo.anchorMax = Vector2.one;
                labelGo.sizeDelta = Vector2.zero;
                var label = labelGo.gameObject.AddComponent<Text>();
                label.font = _font; label.fontSize = 22; label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter; label.color = new Color(0.9f, 0.9f, 0.95f);
                label.raycastTarget = false;
                label.text = TabLabel(category);

                _tabs.Add(new Tab { Category = category, Root = tabRt, Bg = bg });
            }

            RefreshTabHighlight();
        }

        private static string TabLabel(SkillCategory? c) => c switch
        {
            null => "전체",
            SkillCategory.Damage => "딜",
            SkillCategory.Debuff => "디버프",
            SkillCategory.Buff => "버프",
            SkillCategory.Ultimate => "궁극",
            _ => "",
        };

        /// <summary>현재 선택된 탭만 밝게 — 나머지는 어둡게.</summary>
        private void RefreshTabHighlight()
        {
            foreach (var tab in _tabs)
            {
                bool active = tab.Category == _activeCategory;
                tab.Bg.color = active ? new Color(0.35f, 0.32f, 0.18f, 0.95f) : new Color(0.12f, 0.12f, 0.16f, 0.9f);
            }
        }

        /// <summary>장착 스킬 목록이 바뀌면 호출 — 아이콘을 다시 만든다.</summary>
        public void Rebuild()
        {
            foreach (var icon in _icons)
            {
                if (icon.Root != null) Destroy(icon.Root.gameObject);
            }
            _icons.Clear();

            if (_manager == null) return;
            foreach (var skill in _manager.EquippedSkills)
            {
                if (_activeCategory.HasValue && skill.Data.category != _activeCategory.Value) continue;
                _icons.Add(CreateIcon(skill));
            }
        }

        private Icon CreateIcon(SkillRuntime skill)
        {
            var root = new GameObject($"Icon_{skill.Data.skillId}").AddComponent<RectTransform>();
            root.SetParent(_barPanel, false);
            root.sizeDelta = new Vector2(_iconSize, _iconSize);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = _iconSize;
            le.preferredHeight = _iconSize;

            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = GetDisc();
            bg.color = ColorFor(skill.Data.effectType);

            var border = root.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.85f);
            border.effectDistance = new Vector2(1.5f, -1.5f);

            var fill = Child(root, "cd", true);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = GetDisc();
            fillImg.color = new Color(0f, 0f, 0f, 0.72f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = false;
            fillImg.raycastTarget = false;

            var cd = Child(root, "cdtext", true);
            var cdText = cd.gameObject.AddComponent<Text>();
            cdText.font = _font; cdText.fontSize = 22; cdText.fontStyle = FontStyle.Bold;
            cdText.alignment = TextAnchor.MiddleCenter; cdText.color = Color.white; cdText.raycastTarget = false;

            var nameGo = new GameObject("name").AddComponent<RectTransform>();
            nameGo.SetParent(root, false);
            nameGo.anchorMin = new Vector2(0.5f, 0f);
            nameGo.anchorMax = new Vector2(0.5f, 0f);
            nameGo.pivot = new Vector2(0.5f, 1f);
            nameGo.anchoredPosition = new Vector2(0f, -3f);
            nameGo.sizeDelta = new Vector2(_iconSize + 40f, 26f);
            var nameText = nameGo.gameObject.AddComponent<Text>();
            nameText.font = _font; nameText.fontSize = 19; nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.UpperCenter; nameText.color = new Color(0.92f, 0.92f, 0.95f);
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            nameText.text = skill.Data.displayName;
            var nameOutline = nameGo.gameObject.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            nameOutline.effectDistance = new Vector2(1.2f, -1.2f);

            return new Icon { Skill = skill, Root = root, Bg = bg, CooldownFill = fillImg, CdText = cdText, Border = border };
        }

        private void BuildReticle()
        {
            var go = new GameObject("AimReticle");
            go.transform.SetParent(transform);
            _reticle = go.transform;
            _reticleFill = ReticlePart("fill", GetDisc(), 18);
            _reticleRing = ReticlePart("ring", GetRing(), 19);
            go.SetActive(false);

            var arrowGo = new GameObject("AimArrow");
            arrowGo.transform.SetParent(transform);
            _arrow = arrowGo.transform;
            _arrowShaft = ArrowPart("shaft", GetSquare(), 18);
            _arrowHead = ArrowPart("head", GetTriangle(), 19);
            arrowGo.SetActive(false);
        }

        private SpriteRenderer ArrowPart(string name, Sprite sp, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_arrow, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>마왕에서 커서까지 화살표를 그린다(몸통 + 끝의 삼각 화살촉). 폭은 스킬 반경에 비례.</summary>
        private void UpdateArrow(Vector3 target, float radius)
        {
            Vector3 from = _casterPos; from.z = 0f;
            Vector3 delta = target - from;
            float len = delta.magnitude;
            if (len < 0.05f) { _arrow.gameObject.SetActive(false); return; }
            _arrow.gameObject.SetActive(true);

            Vector3 dir = delta / len;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float shaftW = Mathf.Clamp(radius * 0.5f, 0.35f, 1.2f);
            float headLen = Mathf.Min(shaftW * 2.4f, len);
            float headW = shaftW * 2.4f;
            float shaftLen = Mathf.Max(len - headLen, 0.01f);

            _arrowShaft.transform.SetPositionAndRotation(from + dir * (shaftLen * 0.5f), Quaternion.Euler(0f, 0f, angle));
            _arrowShaft.transform.localScale = new Vector3(shaftLen, shaftW, 1f);
            _arrowHead.transform.SetPositionAndRotation(from + dir * (shaftLen + headLen * 0.5f), Quaternion.Euler(0f, 0f, angle));
            _arrowHead.transform.localScale = new Vector3(headLen, headW, 1f);
        }

        private SpriteRenderer ReticlePart(string name, Sprite s, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_reticle, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        // ─────────────────────────────── 입력

        private void Update()
        {
            if (_manager == null || Mouse.current == null) return;
            UpdateCooldowns();

            Vector2 mouse = Mouse.current.position.ReadValue();

            if (_aiming == null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    var tab = TabUnder(mouse);
                    if (tab != null)
                    {
                        _activeCategory = tab.Category;
                        RefreshTabHighlight();
                        Rebuild();
                        return;
                    }

                    var hit = IconUnder(mouse);
                    if (hit != null && hit.Skill.IsReady(Time.time))
                    {
                        if (hit.Skill.Data.castMode == SkillCastMode.Instant) _manager.TryCastInstant(hit.Skill);
                        else BeginAim(hit);
                    }
                }
                return;
            }

            if (ScreenToWorld(mouse, out Vector3 world))
            {
                if (IsDirectional(_aiming.Skill.Data))
                {
                    Vector3 end = world;
                    var ad = _aiming.Skill.Data;
                    if (ad.effectType == SkillEffectType.LineDamage)
                    {
                        // 직선 관통은 사거리(lineLength)가 고정이라, 커서는 방향만 정하고 화살표는 그 길이만큼.
                        Vector3 origin = _casterPos; origin.z = 0f;
                        Vector3 d = world - origin;
                        if (d.sqrMagnitude > 0.0001f) end = origin + d.normalized * ad.lineLength;
                    }
                    UpdateArrow(end, _aiming.Skill.EffectiveRadius);
                    PreviewAlongPath(end, _aiming.Skill.EffectiveRadius);
                }
                else
                {
                    _reticle.position = world;
                    float dia = Mathf.Min(_aiming.Skill.EffectiveRadius * 2f, 40f);
                    _reticle.localScale = Vector3.one * dia;
                    Preview(world, _aiming.Skill.EffectiveRadius);
                }
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool overBar = RectTransformUtility.RectangleContainsScreenPoint(_barRoot, mouse, null);
                if (!overBar && ScreenToWorld(mouse, out Vector3 cast))
                {
                    _manager.TryCastTargeted(_aiming.Skill, cast);
                }
                EndAim();
            }
        }

        private void BeginAim(Icon icon)
        {
            _aiming = icon;
            Color t = ReticleColor(icon.Skill.Data);
            _reticleFill.color = new Color(t.r, t.g, t.b, 0.22f);
            _reticleRing.color = new Color(t.r, t.g, t.b, 1f);
            _arrowShaft.color = new Color(t.r, t.g, t.b, 0.55f);
            _arrowHead.color = new Color(t.r, t.g, t.b, 0.9f);
            icon.Border.effectColor = new Color(t.r, t.g, t.b, 1f);
            icon.Border.effectDistance = new Vector2(4f, -4f);
            _reticle.gameObject.SetActive(!IsDirectional(icon.Skill.Data));
            _arrow.gameObject.SetActive(false); // 첫 프레임에 커서 위치가 잡히면 UpdateArrow가 켠다
        }

        private void EndAim()
        {
            if (_aiming != null)
            {
                _aiming.Border.effectColor = new Color(1f, 1f, 1f, 0.9f);
                _aiming.Border.effectDistance = new Vector2(2f, -2f);
            }
            _aiming = null;
            _reticle.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
            ClearPreview();
        }

        private Icon IconUnder(Vector2 pt)
        {
            foreach (var icon in _icons)
            {
                if (icon.Root != null && RectTransformUtility.RectangleContainsScreenPoint(icon.Root, pt, null)) return icon;
            }
            return null;
        }

        private Tab TabUnder(Vector2 pt)
        {
            foreach (var tab in _tabs)
            {
                if (tab.Root != null && RectTransformUtility.RectangleContainsScreenPoint(tab.Root, pt, null)) return tab;
            }
            return null;
        }

        private void UpdateCooldowns()
        {
            float now = Time.time;
            foreach (var icon in _icons)
            {
                float rem = icon.Skill.RemainingCooldown(now);
                float total = icon.Skill.EffectiveCooldown;
                icon.CooldownFill.fillAmount = total > 0f ? Mathf.Clamp01(rem / total) : 0f;
                icon.CdText.text = rem > 0.05f ? Mathf.CeilToInt(rem).ToString() : "";
                var c = icon.Bg.color;
                icon.Bg.color = new Color(c.r, c.g, c.b, rem > 0.05f ? 0.7f : 1f);
            }
        }

        private void Preview(Vector3 center, float radius)
        {
            ClearPreview();
            _manager.QueryTargetsInRadius(center, radius, _previewBuf);
            foreach (var t in _previewBuf) (t as IHighlightable)?.SetHighlight(true);
        }

        /// <summary>마왕→커서 선분에서 반경 안에 있는 대상을 하이라이트(방향형 스킬용).</summary>
        private void PreviewAlongPath(Vector3 target, float radius)
        {
            ClearPreview();
            Vector3 a = _casterPos; a.z = 0f;
            Vector3 ab = target - a;
            float abSqr = Mathf.Max(ab.sqrMagnitude, 0.0001f);
            foreach (var t in _manager.AllTargets)
            {
                if (t.IsDead) continue;
                float k = Mathf.Clamp01(Vector3.Dot(t.Position - a, ab) / abSqr);
                if (((a + ab * k) - t.Position).sqrMagnitude <= radius * radius) (t as IHighlightable)?.SetHighlight(true);
            }
        }

        private void ClearPreview()
        {
            foreach (var t in _manager.AllTargets) (t as IHighlightable)?.SetHighlight(false);
        }

        // ─────────────────────────────── 유틸

        private RectTransform Child(RectTransform parent, string name, bool stretch)
        {
            var rt = new GameObject(name).AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; }
            return rt;
        }

        private bool ScreenToWorld(Vector2 sp, out Vector3 world)
        {
            world = default;
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return false; }
            var v = new Vector3(sp.x, sp.y, -_camera.transform.position.z);
            world = _camera.ScreenToWorldPoint(v);
            world.z = 0f;
            return true;
        }

        private static Color ColorFor(SkillEffectType t) => t switch
        {
            SkillEffectType.AreaDamage => new Color(0.82f, 0.30f, 0.18f),
            SkillEffectType.ChainDamage => new Color(0.85f, 0.68f, 0.16f),
            SkillEffectType.LineDamage => new Color(0.5f, 0.75f, 0.95f),
            SkillEffectType.SingleDamage => new Color(0.95f, 0.9f, 0.55f),
            SkillEffectType.Knockback => new Color(0.55f, 0.75f, 0.55f),
            SkillEffectType.Stun => new Color(0.22f, 0.58f, 0.82f),
            SkillEffectType.Vacuum => new Color(0.55f, 0.3f, 0.7f),
            SkillEffectType.MovingZone => new Color(0.8f, 0.45f, 0.25f),
            SkillEffectType.PersistentZone => new Color(0.35f, 0.55f, 0.75f),
            SkillEffectType.HealAllies => new Color(0.3f, 0.75f, 0.4f),
            SkillEffectType.AllyBuff => new Color(0.85f, 0.7f, 0.3f),
            SkillEffectType.Revive => new Color(0.6f, 0.85f, 0.6f),
            _ => new Color(0.4f, 0.4f, 0.45f),
        };

        private static Color ReticleColor(SkillData d)
        {
            if (d.effectType == SkillEffectType.PersistentZone || d.effectType == SkillEffectType.Stun)
                return new Color(0.4f, 0.8f, 1f);
            if (d.effectType == SkillEffectType.HealAllies || d.zoneTarget == ZoneTarget.Allies)
                return new Color(0.4f, 1f, 0.5f);
            return new Color(1f, 0.55f, 0.2f);
        }

        private Sprite GetDisc() => _disc ??= MakeCircle(64, 0f);
        private Sprite GetRing() => _ring ??= MakeCircle(96, 0.84f);
        private Sprite GetSquare()
        {
            if (_square != null) return _square;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return _square = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }

        /// <summary>+x 방향(오른쪽)을 가리키는 1x1 삼각형 — 화살촉.</summary>
        private Sprite GetTriangle()
        {
            if (_triangle != null) return _triangle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float halfWidthAtX = c * (1f - (float)x / (size - 1));
                tex.SetPixel(x, y, Mathf.Abs(y - c) <= halfWidthAtX ? Color.white : Color.clear);
            }
            tex.Apply();
            return _triangle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite MakeCircle(int size, float innerFraction)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float outer = size * 0.5f - 1f;
            float inner = outer * innerFraction;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dd = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(outer - dd) * (innerFraction > 0f ? Mathf.Clamp01(dd - inner) : 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
