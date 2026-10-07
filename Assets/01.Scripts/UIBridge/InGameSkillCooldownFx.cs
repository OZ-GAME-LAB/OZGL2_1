using System.Collections.Generic;
using System.Reflection;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 스킬 슬롯의 쿨타임 표시를 더 보기 좋게 만든다. 팀 파일(UICombatSkillSlotView)은 수정하지 않고,
    /// 슬롯이 이미 가진 값(남은 시간·전체 시간·고리·숫자)을 읽어 위에 효과만 얹는다.
    /// - 아이콘 위로 시계 방향으로 걷히는 어두운 막(쿨타임이 줄어드는 만큼 아이콘이 밝아진다)
    /// - 고리의 줄어드는 끝에서 빛나는 불꽃(진행 방향이 한눈에 보인다)
    /// - 3초 미만이면 소수점 한 자리 숫자 + 따뜻한 색, 마지막 구간엔 고리가 맥동
    /// - 쿨타임이 끝나는 순간: 고리 파동, 아이콘이 톡 튀어나오고 반짝이 퍼진다
    /// - 연결 복구: 팀의 Canvas_Combat 프리팹이 슬롯 뷰의 아이콘·고리·숫자 연결(_icon, _cooldownFill, _remainingText …)을 빈 값으로 덮어쓰고 있어서
    ///   뷰가 그것들을 전혀 갱신하지 못했다(고리가 안 돌고, 숫자가 없고, 모래시계만 제자리에서 켜졌다 꺼졌다). 슬롯 안에서 이름으로 찾아 다시 연결한다
    /// - 모래시계(CooldownIcon)는 원 한가운데에 놓고, 어두운 막 위에 보이게 하며 살살 흔들린다. 초 숫자는 모래시계 아래로 내린다
    /// </summary>
    public sealed class InGameSkillCooldownFx : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.9f)] private float _dimAlpha = 0.58f;
        [SerializeField, Min(0.5f)] private float _preciseUnderSeconds = 3f;
        [SerializeField] private Color _warmTextColor = new Color(1f, 0.86f, 0.45f, 1f);
        [SerializeField] private Color _sparkColor = new Color(1f, 0.95f, 0.8f, 1f);
        [SerializeField, Range(0f, 0.4f)] private float _readyPulse = 0.18f;

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int SparkleCount = 7;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            if (FindFirstObjectByType<OZGL2.InGame.InGamePrototypeBootstrap>() == null) return;
            if (FindFirstObjectByType<InGameSkillCooldownFx>() != null) return;
            new GameObject("SkillCooldownFx").AddComponent<InGameSkillCooldownFx>();
        }

        private static readonly FieldInfo FIcon = F("_icon"), FFill = F("_cooldownFill"), FTrack = F("_cooldownTrack"),
            FText = F("_remainingText"), FFillColor = F("_cooldownFillColor"), FFillOpacity = F("_cooldownFillOpacity"),
            FRemain = F("_runtimeRemainingSeconds"), FDuration = F("_runtimeDuration"), FHasRuntime = F("_hasRuntimeCooldown"),
            FPrevRemain = F("_previewRemainingSeconds"), FPrevDuration = F("_previewDuration"), FCdIcon = F("_cooldownIcon");

        private static FieldInfo F(string name) => typeof(UICombatSkillSlotView).GetField(name, Priv);
        private static readonly MethodInfo MRefresh = typeof(UICombatSkillSlotView).GetMethod("RefreshView", Priv);
        private static readonly FieldInfo FTicks = F("_cooldownTicks"), FFrame = F("_frame"), FTint = F("_slotTint"), FName = F("_skillNameText");

        /// <summary>비어 있는 슬롯 뷰 연결을 슬롯 안의 같은 이름 오브젝트로 채운다.</summary>
        private static void Repair(UICombatSkillSlotView view)
        {
            // CooldownFill 이 들어 있는 가장 가까운 윗단계를 슬롯 뿌리로 본다
            Transform root = view.transform;
            for (var cur = view.transform; cur != null && cur.parent != null; cur = cur.parent)
            {
                bool has = false;
                foreach (var img in cur.GetComponentsInChildren<Image>(true)) if (img.name == "CooldownFill") { has = true; break; }
                if (has) { root = cur; break; }
            }
            bool changed = false;
            changed |= Fix<Image>(view, root, FIcon, "Icon");
            changed |= Fix<Image>(view, root, FFrame, "FrameArt");
            changed |= Fix<Image>(view, root, FTint, "CategorySlotTint");
            changed |= Fix<Image>(view, root, FFill, "CooldownFill");
            changed |= Fix<Image>(view, root, FTrack, "CooldownTrack");
            changed |= Fix<Image>(view, root, FCdIcon, "CooldownIcon");
            changed |= Fix<TMP_Text>(view, root, FText, "CooldownSeconds");
            changed |= Fix<TMP_Text>(view, root, FName, "SkillName");
            if (FTicks != null && (FTicks.GetValue(view) as Image[] ?? new Image[0]).Length == 0)
            {
                var ticks = new List<Image>();
                foreach (var img in root.GetComponentsInChildren<Image>(true)) if (img.name.StartsWith("Tick_")) ticks.Add(img);
                if (ticks.Count > 0) { FTicks.SetValue(view, ticks.ToArray()); changed = true; }
            }
            if (changed)
            {
                Debug.Log("[쿨타임 효과] 슬롯 연결 복구: " + view.name + " (프리팹 덮어쓰기로 비어 있던 아이콘·고리·숫자 참조를 다시 연결)");
                MRefresh?.Invoke(view, null);
            }
        }

        private static bool Fix<T>(UICombatSkillSlotView view, Transform root, FieldInfo field, string childName) where T : Component
        {
            if (field == null || (field.GetValue(view) as Object) != null) return false;
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if (c.name == childName) { field.SetValue(view, c); return true; }
            return false;
        }

        private sealed class SlotFx
        {
            public UICombatSkillSlotView view;
            public Image icon, fill, track, dim, spark, flash, hourglass, hand;
            public TMP_Text text;
            public RectTransform[] sparkles = new RectTransform[SparkleCount];
            public Image[] sparkleImg = new Image[SparkleCount];
            public float lastRatio, readyT = -1f;
            public Color textBase = Color.white;
            public Vector3 iconBaseScale = Vector3.one;
            public float ringRadius = 100f;
            public bool dumped;
        }

        private readonly Dictionary<UICombatSkillSlotView, SlotFx> _slots = new Dictionary<UICombatSkillSlotView, SlotFx>();
        private readonly List<UICombatSkillSlotView> _gone = new List<UICombatSkillSlotView>();
        private Sprite _disc, _dot;
        private float _nextScan;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;
            if (FFill == null) { enabled = false; return; }
            foreach (var view in FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!_slots.ContainsKey(view)) { Repair(view); Build(view); }
            _gone.Clear();
            foreach (var pair in _slots) if (pair.Key == null) _gone.Add(pair.Key);
            foreach (var key in _gone) _slots.Remove(key);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var pair in _slots)
                if (pair.Key != null) Refresh(pair.Value, dt);
        }

        // ───────────── 슬롯마다 효과 이미지 한 번만 만들기

        private void Build(UICombatSkillSlotView view)
        {
            EnsureSprites();
            var fx = new SlotFx
            {
                view = view,
                icon = FIcon.GetValue(view) as Image,
                fill = FFill.GetValue(view) as Image,
                track = FTrack.GetValue(view) as Image,
                text = FText.GetValue(view) as TMP_Text,
                hourglass = FCdIcon != null ? FCdIcon.GetValue(view) as Image : null,
            };
            if (fx.text != null) fx.textBase = fx.text.color;
            _slots[view] = fx;
            Debug.Log("[쿨타임 효과] 슬롯 연결: " + view.name);
            if (fx.icon == null) return;

            var parent = fx.icon.transform.parent as RectTransform;
            if (parent == null) return;
            fx.iconBaseScale = fx.icon.rectTransform.localScale;
            if (fx.fill != null) fx.ringRadius = Mathf.Max(60f, fx.fill.rectTransform.rect.width * 0.5f - 7f);

            // 아이콘 바로 위에 덮이는 어두운 막(숫자·고리보다는 아래)
            float dimSize = Mathf.Max(fx.icon.rectTransform.rect.width * 1.5f, 150f);
            fx.dim = NewImage("CooldownDim", parent, _disc, dimSize);
            fx.dim.type = Image.Type.Filled;
            fx.dim.fillMethod = Image.FillMethod.Radial360;
            fx.dim.fillOrigin = (int)Image.Origin360.Top;
            fx.dim.fillClockwise = true;
            fx.dim.color = new Color(0f, 0f, 0f, _dimAlpha);
            fx.dim.rectTransform.SetSiblingIndex(fx.icon.rectTransform.GetSiblingIndex() + 1);

            // 모래시계: 아이콘과 같은 중심(원 한가운데)에 놓고 막 위로 올린다. 숫자는 그 아래로
            if (fx.hourglass != null)
            {
                var hr = fx.hourglass.rectTransform;
                hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
                hr.pivot = new Vector2(0.5f, 0.5f);
                hr.sizeDelta = new Vector2(38f, 52f);
                hr.anchoredPosition = fx.icon.rectTransform.anchoredPosition;
                hr.SetSiblingIndex(fx.dim.rectTransform.GetSiblingIndex() + 1);
            }
            if (fx.text != null)
            {
                var tr = fx.text.rectTransform;
                tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, fx.icon.rectTransform.anchoredPosition.y - 52f);
                fx.text.transform.SetAsLastSibling();
            }

            fx.spark = NewImage("CooldownSpark", parent, _dot, 46f);
            fx.spark.color = _sparkColor;
            fx.flash = NewImage("ReadyFlash", parent, fx.track != null && fx.track.sprite != null ? fx.track.sprite : _disc, fx.ringRadius * 2f);
            fx.flash.color = new Color(1f, 0.95f, 0.8f, 0f);
            for (int i = 0; i < SparkleCount; i++)
            {
                fx.sparkleImg[i] = NewImage("ReadySparkle" + i, parent, _dot, 16f);
                fx.sparkles[i] = fx.sparkleImg[i].rectTransform;
                fx.sparkleImg[i].color = new Color(1f, 0.95f, 0.8f, 0f);
            }
        }

        /// <summary>쿨타임이 처음 돌 때 슬롯 안 이미지들의 상태를 콘솔에 한 번 남긴다(화면과 맞지 않을 때 원인 확인용).</summary>
        private static void DumpSlot(SlotFx fx, float remaining, float duration)
        {
            var sb = new System.Text.StringBuilder("[쿨타임 효과] 슬롯 상태 " + fx.view.name + " 남은 " + remaining.ToString("0.0") + " / 전체 " + duration.ToString("0.0") + "\n");
            foreach (var img in fx.view.GetComponentsInChildren<Image>(true))
            {
                var r = img.rectTransform;
                sb.Append("  ").Append(img.name).Append(" 켜짐=").Append(img.enabled).Append("/").Append(img.gameObject.activeInHierarchy)
                  .Append(" 위치=").Append(r.anchoredPosition).Append(" 크기=").Append(r.sizeDelta)
                  .Append(" 스프라이트=").Append(img.sprite != null ? img.sprite.name : "없음").Append(" 색=").Append(img.color).Append("\n");
            }
            Debug.Log(sb.ToString());
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, float size)
        {
            var old = parent.Find(name);
            if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // ───────────── 매 프레임

        private void Refresh(SlotFx fx, float dt)
        {
            if (fx.dim == null || fx.view == null) return;

            bool runtime = (bool)FHasRuntime.GetValue(fx.view);
            float remaining = runtime ? (float)FRemain.GetValue(fx.view) : (float)FPrevRemain.GetValue(fx.view);
            float duration = runtime ? (float)FDuration.GetValue(fx.view) : (float)FPrevDuration.GetValue(fx.view);
            bool hasSkill = fx.icon != null && fx.icon.sprite != null; // 쿨타임 중엔 아이콘 대신 모래시계가 켜져 icon.enabled 가 꺼진다
            float ratio = hasSkill && duration > 0.01f ? Mathf.Clamp01(remaining / duration) : 0f;
            bool cooling = ratio > 0.001f;

            // 팀 슬롯 뷰가 매 프레임 켜고 끄는 값과 상관없이 여기서 최종 상태를 정한다:
            // 쿨타임 중에는 모래시계만(원 한가운데), 아니면 스킬 아이콘만 보인다. 숫자·고리도 같이 맞춘다.
            bool useHourglass = fx.hourglass != null && fx.hourglass.sprite != null;
            if (fx.icon != null) fx.icon.enabled = hasSkill && !(cooling && useHourglass);
            if (useHourglass)
            {
                fx.hourglass.enabled = cooling && hasSkill;
                var hr = fx.hourglass.rectTransform;
                hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
                hr.pivot = new Vector2(0.5f, 0.5f);
                hr.sizeDelta = new Vector2(44f, 60f);
                hr.anchoredPosition = fx.icon != null ? fx.icon.rectTransform.anchoredPosition : Vector2.zero;
            }
            if (fx.text != null) fx.text.enabled = cooling;
            if (fx.fill != null && fx.fill.sprite != null)
            {
                fx.fill.enabled = cooling;
                fx.fill.fillAmount = ratio;
            }
            if (cooling && !fx.dumped)
            {
                fx.dumped = true;
                DumpSlot(fx, remaining, duration);
            }

            // 모래시계: 살살 흔들리고, 끝나갈수록 따뜻한 색
            if (fx.hourglass != null && fx.hourglass.enabled)
            {
                float tt = Time.unscaledTime;
                fx.hourglass.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(tt * 2.2f) * 7f);
                float warm = Mathf.Clamp01((_preciseUnderSeconds - remaining) / _preciseUnderSeconds);
                fx.hourglass.color = Color.Lerp(new Color32(248, 242, 235, 255), _warmTextColor, warm);
            }
            else if (fx.hourglass != null) fx.hourglass.rectTransform.localRotation = Quaternion.identity;

            // 어두운 막: 남은 비율만큼 시계 방향으로 덮인다
            fx.dim.enabled = cooling;
            fx.dim.fillAmount = ratio;

            // 바늘: 막의 경계(위에서 시계 방향)를 따라 돈다
            if (fx.hand != null)
            {
                fx.hand.enabled = cooling && ratio < 0.995f;
                fx.hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ratio * 360f);
            }

            // 불꽃: 고리가 줄어드는 끝 지점
            fx.spark.enabled = cooling && ratio < 0.995f;
            if (fx.spark.enabled)
            {
                float angle = ratio * Mathf.PI * 2f; // 위에서 시계 방향
                fx.spark.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * fx.ringRadius;
                float flicker = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 18f);
                fx.spark.color = new Color(_sparkColor.r, _sparkColor.g, _sparkColor.b, flicker);
                fx.spark.rectTransform.localScale = Vector3.one * (0.9f + 0.25f * Mathf.Sin(Time.unscaledTime * 14f));
            }

            // 고리 색: 끝나갈수록 밝게 맥동
            if (fx.fill != null && cooling)
            {
                Color baseColor = FFillColor != null ? (Color)FFillColor.GetValue(fx.view) : fx.fill.color;
                float opacity = FFillOpacity != null ? (float)FFillOpacity.GetValue(fx.view) : fx.fill.color.a;
                float near = Mathf.Clamp01((0.25f - ratio) / 0.25f); // 마지막 25%
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                Color c = Color.Lerp(baseColor, new Color(1f, 0.9f, 0.55f, 1f), near * (0.5f + 0.5f * pulse));
                c.a = opacity;
                fx.fill.color = c;
            }

            // 숫자: 3초 미만은 소수점 한 자리 + 따뜻한 색
            if (fx.text != null && cooling)
            {
                if (remaining < _preciseUnderSeconds)
                {
                    fx.text.text = remaining.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                    fx.text.color = _warmTextColor;
                }
                else fx.text.color = fx.textBase;
            }

            // 쿨타임이 끝나는 순간
            if (fx.lastRatio > 0.001f && !cooling && hasSkill && duration > 0.01f) fx.readyT = 0f;
            fx.lastRatio = ratio;
            AnimateReady(fx, dt);
        }

        private void AnimateReady(SlotFx fx, float dt)
        {
            const float Length = 0.5f;
            if (fx.readyT < 0f)
            {
                if (fx.flash.color.a > 0f) fx.flash.color = new Color(1f, 1f, 1f, 0f);
                return;
            }
            fx.readyT += dt;
            float k = fx.readyT / Length;
            if (k >= 1f)
            {
                fx.readyT = -1f;
                fx.flash.color = new Color(1f, 1f, 1f, 0f);
                for (int i = 0; i < SparkleCount; i++) fx.sparkleImg[i].color = new Color(1f, 1f, 1f, 0f);
                if (fx.icon != null) fx.icon.rectTransform.localScale = fx.iconBaseScale;
                return;
            }

            // 고리 파동
            float ease = 1f - (1f - k) * (1f - k);
            fx.flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.3f, ease);
            fx.flash.color = new Color(1f, 0.95f, 0.8f, (1f - k) * 0.95f);

            // 아이콘 톡
            if (fx.icon != null)
                fx.icon.rectTransform.localScale = fx.iconBaseScale * (1f + _readyPulse * Mathf.Sin(Mathf.Clamp01(k * 1.6f) * Mathf.PI));

            // 반짝이가 바깥으로 퍼진다
            for (int i = 0; i < SparkleCount; i++)
            {
                float a = (i / (float)SparkleCount) * Mathf.PI * 2f + 0.4f;
                float r = fx.ringRadius * Mathf.Lerp(0.9f, 1.45f, ease);
                fx.sparkles[i].anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                fx.sparkles[i].localScale = Vector3.one * Mathf.Lerp(1.2f, 0.3f, k);
                fx.sparkleImg[i].color = new Color(1f, 0.95f, 0.8f, 1f - k);
            }
        }

        // ───────────── 코드로 그린 작은 이미지

        private void EnsureSprites()
        {
            if (_disc != null) return;
            _disc = MakeDisc(128, false);
            _dot = MakeDisc(64, true);
        }

        private static Sprite MakeDisc(int n, bool soft)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = soft ? Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f) : Mathf.Clamp01((1f - d) * r * 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnDestroy()
        {
            foreach (var fx in _slots.Values)
            {
                if (fx.dim != null) Destroy(fx.dim.gameObject);
                if (fx.spark != null) Destroy(fx.spark.gameObject);
                if (fx.hand != null) Destroy(fx.hand.gameObject);
                if (fx.flash != null) Destroy(fx.flash.gameObject);
                foreach (var s in fx.sparkleImg) if (s != null) Destroy(s.gameObject);
                if (fx.icon != null) fx.icon.rectTransform.localScale = fx.iconBaseScale;
            }
            _slots.Clear();
        }
    }
}
