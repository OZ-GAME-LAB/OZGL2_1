using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>기존 UI 표시 코드에서 공통 장식과 글꼴을 적용하는 작은 보조 기능입니다.</summary>
    public static class UIHeraldrySupplement
    {
        private const string STYLE_RESOURCE_PATH = "UI/HeraldrySupplementStyle";
        private static UIHeraldrySupplementStyleSO _style;

        public static UIHeraldrySupplementStyleSO Style
        {
            get
            {
                if (_style == null)
                    _style = Resources.Load<UIHeraldrySupplementStyleSO>(STYLE_RESOURCE_PATH);
                return _style;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _style = null;
        }

        public static Image AddFrame(RectTransform target, bool addCrest = false)
        {
            UIHeraldrySupplementStyleSO style = Style;
            if (target == null || style == null || style.FrameSprite == null)
                return null;

            Image frame = CreateImage(target, "HeraldryFrame", style.FrameSprite);
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = style.FrameMultiplier;
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            // 별도 배경 Image가 있는 대화창에서도 금색 테두리가 배경에 가려지지 않게 합니다.
            int frameIndex = 0;
            if (target.childCount > 1)
            {
                Transform first = target.GetChild(0);
                if ((first.name == "Bg" || first.name == "Background") && first.GetComponent<Image>() != null)
                    frameIndex = 1;
            }
            frameRect.SetSiblingIndex(frameIndex);

            if (addCrest && style.CrestSprite != null)
            {
                Image crest = CreateImage(target, "HeraldryCrest", style.CrestSprite);
                crest.preserveAspect = true;
                RectTransform crestRect = crest.rectTransform;
                crestRect.anchorMin = crestRect.anchorMax = new Vector2(0.5f, 1f);
                crestRect.anchoredPosition = new Vector2(0f, -8f);
                crestRect.sizeDelta = new Vector2(88f, 70f);
                // 이후 만들어지는 내용과 기존 텍스트가 장식 위에 표시되게 합니다.
                crestRect.SetSiblingIndex(frameIndex + 1);
            }

            return frame;
        }

        public static Image CreateImage(Transform parent, string name, Sprite sprite)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>배경과 금색 테두리가 하나인 그림으로 모서리 밖 사각 배경을 없앱니다.</summary>
        public static bool ApplyPanel(Image target)
        {
            UIHeraldrySupplementStyleSO style = Style;
            if (target == null || style == null || style.PanelSprite == null)
                return false;
            target.sprite = style.PanelSprite;
            target.type = Image.Type.Sliced;
            target.fillCenter = true;
            target.color = Color.white;
            target.pixelsPerUnitMultiplier = style.FrameMultiplier;
            return true;
        }

        public static void ApplyTitle(TMP_Text label)
        {
            UIHeraldrySupplementStyleSO style = Style;
            if (label != null && style != null && style.TitleFont != null)
                label.font = style.TitleFont;
        }

        public static void ApplyBody(TMP_Text label)
        {
            UIHeraldrySupplementStyleSO style = Style;
            if (label != null && style != null && style.BodyFont != null)
                label.font = style.BodyFont;
        }
    }
}
