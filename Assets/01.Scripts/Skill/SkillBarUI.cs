using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>
    /// 화면 하단 스킬 아이콘 바 (비치 디펜스류). 아이콘 = 원형, 쿨다운은 부채꼴로 채워짐.
    ///  - 즉시형: 아이콘 클릭 → 즉시 발동
    ///  - 조준형: 아이콘을 <b>누른 채로 필드로 드래그</b> → 조준 링 표시 → 떼면 발동 / 바 위에서 떼면 취소
    ///
    /// SkillManager 의 public API 만 사용. SkillSandbox·씬 구조에 의존하지 않는다.
    /// 실제 씬 이전: 프리팹으로 만들어 놓고 Bind(실제 SkillManager, 카메라, 마왕 위치) 만 호출.
    /// 아이콘 이미지는 나중에 희수 아트로 교체.
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        [SerializeField] private float _iconSize = 96f;
        [SerializeField] private float _iconSpacing = 16f;
        [SerializeField] private float _barPadding = 14f;
        [SerializeField] private float _bottomMargin = 28f;

        private SkillManager _manager;
        private Camera _camera;
        private Vector3 _casterPosition;

        private readonly List<Icon> _icons = new List<Icon>();
        private readonly List<IDamageable> _previewBuffer = new List<IDamageable>();

        private Icon _aiming;
        private RectTransform _barRect;
        private Font _font;

        private Transform _reticle;
        private SpriteRenderer _reticleFill;
        private SpriteRenderer _reticleRing;
        private SpriteRenderer _reticleDot;

        private Sprite _discSprite;
        private Sprite _ringSprite;

        private class Icon
        {
            public SkillRuntime Skill;
            public RectTransform Root;
            public Image Background;
            public Image CooldownFill;
            public Text CooldownText;
            public Outline Border;
        }

        public void Bind(SkillManager manager, Camera worldCamera, Vector3 casterWorldPosition)
        {
            _manager = manager;
            _camera = worldCamera != null ? worldCamera : Camera.main;
            _casterPosition = casterWorldPosition;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            BuildReticle();
            RebuildIcons();
        }

        // ─────────────────────────────────────────── UI 생성

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

            var panelGo = new GameObject("BarPanel");
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.SetParent(canvasGo.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, _bottomMargin);

            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.06f, 0.09f, 0.82f);

            var panelOutline = panelGo.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 1f, 1f, 0.14f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            var layout = panelGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = _iconSpacing;
            layout.padding = new RectOffset(
                (int)_barPadding, (int)_barPadding, (int)_barPadding, (int)_barPadding);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _barRect = panelRect;
        }

        private void RebuildIcons()
        {
            foreach (var icon in _icons)
            {
                if (icon.Root != null)
                {
                    Destroy(icon.Root.gameObject);
                }
            }

            _icons.Clear();

            foreach (var skill in _manager.Skills)
            {
                _icons.Add(CreateIcon(skill));
            }
        }

        private Icon CreateIcon(SkillRuntime skill)
        {
            var root = new GameObject($"Icon_{skill.Data.skillId}").AddComponent<RectTransform>();
            root.SetParent(_barRect, false);
            root.sizeDelta = new Vector2(_iconSize, _iconSize);

            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = _iconSize;
            le.preferredHeight = _iconSize;

            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = GetDiscSprite();
            bg.color = ColorFor(skill.Data.effectType);

            var border = root.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.9f);
            border.effectDistance = new Vector2(2.5f, -2.5f);

            var inner = MakeChild(root, "InnerRing", stretch: true);
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = GetRingSprite();
            innerImg.color = new Color(1f, 1f, 1f, 0.35f);
            innerImg.raycastTarget = false;

            var fill = MakeChild(root, "Cooldown", stretch: true);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = GetDiscSprite();
            fillImg.color = new Color(0f, 0f, 0f, 0.68f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = false;
            fillImg.fillAmount = 0f;
            fillImg.raycastTarget = false;

            var cd = MakeChild(root, "CdText", stretch: true);
            var cdText = cd.gameObject.AddComponent<Text>();
            cdText.font = _font;
            cdText.fontSize = 30;
            cdText.fontStyle = FontStyle.Bold;
            cdText.alignment = TextAnchor.MiddleCenter;
            cdText.color = Color.white;
            cdText.raycastTarget = false;
            AddTextOutline(cd.gameObject, 0.9f);

            var nameGo = new GameObject("Name").AddComponent<RectTransform>();
            nameGo.SetParent(root, false);
            nameGo.anchorMin = new Vector2(0.5f, 0f);
            nameGo.anchorMax = new Vector2(0.5f, 0f);
            nameGo.pivot = new Vector2(0.5f, 1f);
            nameGo.anchoredPosition = new Vector2(0f, -6f);
            nameGo.sizeDelta = new Vector2(_iconSize + 40f, 22f);
            var nameText = nameGo.gameObject.AddComponent<Text>();
            nameText.font = _font;
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.UpperCenter;
            nameText.color = Color.white;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.text = skill.Data.castMode == SkillCastMode.Targeted
                ? skill.Data.displayName + "  (드래그)"
                : skill.Data.displayName;
            AddTextOutline(nameGo.gameObject, 0.95f);

            return new Icon
            {
                Skill = skill,
                Root = root,
                Background = bg,
                CooldownFill = fillImg,
                CooldownText = cdText,
                Border = border,
            };
        }

        private void BuildReticle()
        {
            var go = new GameObject("AimReticle");
            go.transform.SetParent(transform);
            _reticle = go.transform;

            _reticleFill = MakeReticlePart("Fill", GetDiscSprite(), 18);
            _reticleRing = MakeReticlePart("Ring", GetRingSprite(), 19);
            _reticleDot = MakeReticlePart("Dot", GetDiscSprite(), 20);
            _reticleDot.transform.localScale = Vector3.one * 0.12f;

            go.SetActive(false);
        }

        private SpriteRenderer MakeReticlePart(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_reticle, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // ─────────────────────────────────────────── 입력 / 조준

        private void Update()
        {
            if (_manager == null || Mouse.current == null)
            {
                return;
            }

            UpdateCooldownVisuals();

            Vector2 mouse = Mouse.current.position.ReadValue();

            if (_aiming == null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    Icon hit = IconUnderPointer(mouse);
                    if (hit != null && hit.Skill.IsReady(Time.time))
                    {
                        if (hit.Skill.Data.castMode == SkillCastMode.Instant)
                        {
                            _manager.TryCastInstant(hit.Skill);
                        }
                        else
                        {
                            BeginAim(hit);
                        }
                    }
                }

                return;
            }

            if (ScreenToWorld(mouse, out Vector3 world))
            {
                _reticle.position = world;
                float diameter = Mathf.Min(_aiming.Skill.Data.radius * 2f, 40f);
                _reticle.localScale = Vector3.one * diameter;
                _reticleDot.transform.localScale = Vector3.one * (0.12f / Mathf.Max(diameter, 0.01f));
                UpdatePreview(world, _aiming.Skill.Data.radius);
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool overBar = RectTransformUtility.RectangleContainsScreenPoint(_barRect, mouse, null);
                if (!overBar && ScreenToWorld(mouse, out Vector3 castPoint))
                {
                    // 발동만 요청. 발사체·착탄 연출은 SkillVfxController(Casted 구독)가 담당.
                    _manager.TryCastTargeted(_aiming.Skill, castPoint);
                }

                EndAim();
            }
        }

        private void BeginAim(Icon icon)
        {
            _aiming = icon;

            Color tint = ReticleColorFor(icon.Skill.Data.effectType);
            _reticleFill.color = new Color(tint.r, tint.g, tint.b, 0.22f);
            _reticleRing.color = new Color(tint.r, tint.g, tint.b, 1f);
            _reticleDot.color = new Color(tint.r, tint.g, tint.b, 1f);

            icon.Border.effectColor = new Color(tint.r, tint.g, tint.b, 1f);
            icon.Border.effectDistance = new Vector2(4f, -4f);

            _reticle.gameObject.SetActive(true);
        }

        private void EndAim()
        {
            if (_aiming != null)
            {
                _aiming.Border.effectColor = new Color(1f, 1f, 1f, 0.9f);
                _aiming.Border.effectDistance = new Vector2(2.5f, -2.5f);
            }

            _aiming = null;
            _reticle.gameObject.SetActive(false);
            ClearPreview();
        }

        private Icon IconUnderPointer(Vector2 screenPoint)
        {
            foreach (var icon in _icons)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(icon.Root, screenPoint, null))
                {
                    return icon;
                }
            }

            return null;
        }

        private void UpdateCooldownVisuals()
        {
            float now = Time.time;
            foreach (var icon in _icons)
            {
                float remaining = icon.Skill.RemainingCooldown(now);
                float ratio = icon.Skill.Data.cooldown > 0f ? remaining / icon.Skill.Data.cooldown : 0f;
                icon.CooldownFill.fillAmount = Mathf.Clamp01(ratio);
                icon.CooldownText.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;

                bool ready = remaining <= 0.05f;
                Color c = icon.Background.color;
                icon.Background.color = new Color(c.r, c.g, c.b, ready ? 1f : 0.75f);
            }
        }

        private void UpdatePreview(Vector3 center, float radius)
        {
            ClearPreview();
            _manager.QueryTargetsInRadius(center, radius, _previewBuffer);
            foreach (var target in _previewBuffer)
            {
                (target as IHighlightable)?.SetHighlight(true);
            }
        }

        private void ClearPreview()
        {
            foreach (var target in _manager.AllTargets)
            {
                (target as IHighlightable)?.SetHighlight(false);
            }
        }

        // ─────────────────────────────────────────── 유틸

        private RectTransform MakeChild(RectTransform parent, string name, bool stretch)
        {
            var rt = new GameObject(name).AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
            }

            return rt;
        }

        private static void AddTextOutline(GameObject go, float alpha)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, alpha);
            o.effectDistance = new Vector2(2f, -2f);
        }


        private bool ScreenToWorld(Vector2 screenPos, out Vector3 world)
        {
            world = default;
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return false;
                }
            }

            var sp = new Vector3(screenPos.x, screenPos.y, -_camera.transform.position.z);
            world = _camera.ScreenToWorldPoint(sp);
            world.z = 0f;
            return true;
        }

        private static Color ColorFor(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.AreaDamage: return new Color(0.82f, 0.30f, 0.18f);
                case SkillEffectType.ChainDamage: return new Color(0.85f, 0.68f, 0.16f);
                case SkillEffectType.AreaStun: return new Color(0.22f, 0.58f, 0.82f);
                case SkillEffectType.HealAllies: return new Color(0.28f, 0.68f, 0.36f);
                default: return new Color(0.4f, 0.4f, 0.45f);
            }
        }

        private static Color ReticleColorFor(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.AreaDamage: return new Color(1f, 0.5f, 0.15f);
                case SkillEffectType.AreaStun: return new Color(0.4f, 0.8f, 1f);
                default: return new Color(1f, 0.85f, 0.3f);
            }
        }

        private Sprite GetDiscSprite()
        {
            if (_discSprite == null)
            {
                _discSprite = MakeCircleSprite(64, innerFraction: 0f);
            }

            return _discSprite;
        }

        private Sprite GetRingSprite()
        {
            if (_ringSprite == null)
            {
                _ringSprite = MakeCircleSprite(96, innerFraction: 0.86f);
            }

            return _ringSprite;
        }

        private static Sprite MakeCircleSprite(int size, float innerFraction)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float outer = size * 0.5f - 1f;
            float inner = outer * innerFraction;
            var center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float a = Mathf.Clamp01(outer - d) * (innerFraction > 0f ? Mathf.Clamp01(d - inner) : 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
