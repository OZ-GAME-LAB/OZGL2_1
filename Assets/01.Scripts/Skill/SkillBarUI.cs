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
        [SerializeField] private float _bottomMargin = 18f;

        private SkillManager _manager;
        private Camera _camera;
        private Vector3 _casterPos;
        private Font _font;

        private readonly List<Icon> _icons = new List<Icon>();
        private readonly List<IDamageable> _previewBuf = new List<IDamageable>();

        private RectTransform _barPanel;
        private Icon _aiming;

        private Transform _reticle;
        private SpriteRenderer _reticleFill;
        private SpriteRenderer _reticleRing;
        private Sprite _disc;
        private Sprite _ring;

        private class Icon
        {
            public SkillRuntime Skill;
            public RectTransform Root;
            public Image Bg;
            public Image CooldownFill;
            public Text CdText;
            public Outline Border;
        }

        public void Bind(SkillManager manager, Camera cam, Vector3 casterPos)
        {
            _manager = manager;
            _camera = cam != null ? cam : Camera.main;
            _casterPos = casterPos;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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

            var panelGo = new GameObject("Panel");
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.SetParent(canvasGo.transform, false);
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = new Vector2(0f, _bottomMargin);

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
            nameGo.sizeDelta = new Vector2(_iconSize + 40f, 18f);
            var nameText = nameGo.gameObject.AddComponent<Text>();
            nameText.font = _font; nameText.fontSize = 14;
            nameText.alignment = TextAnchor.UpperCenter; nameText.color = new Color(0.92f, 0.92f, 0.95f);
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            nameText.text = skill.Data.displayName;

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
                _reticle.position = world;
                float dia = Mathf.Min(_aiming.Skill.EffectiveRadius * 2f, 40f);
                _reticle.localScale = Vector3.one * dia;
                Preview(world, _aiming.Skill.EffectiveRadius);
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool overBar = RectTransformUtility.RectangleContainsScreenPoint(_barPanel, mouse, null);
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
            icon.Border.effectColor = new Color(t.r, t.g, t.b, 1f);
            icon.Border.effectDistance = new Vector2(4f, -4f);
            _reticle.gameObject.SetActive(true);
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
