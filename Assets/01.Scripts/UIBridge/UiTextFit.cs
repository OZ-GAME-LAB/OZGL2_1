using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 화면의 모든 글자가 자기 칸을 삐져나오지 않고, 단추 안의 글자는 가운데에 오도록 한 번씩 정리한다(팀 프리팹은 건드리지 않고 실행 중에 얹는다).
    /// - 글자가 칸보다 크면 자동으로 줄여 맞춘다(원래 크기가 최대, 55%까지만 줄인다). 내용에 맞춰 칸이 늘어나는 글자(ContentSizeFitter)와
    ///   이미 자동 맞춤이 켜진 글자는 건드리지 않는다.
    /// - 단추(Button·Toggle) 안에서 단추 폭의 대부분을 차지하는 글자는 가로·세로 가운데 정렬로 맞춘다.
    /// 새로 열린 창·카드의 글자도 잡도록 주기적으로 훑고, 한 번 정리한 글자는 다시 건드리지 않는다.
    /// </summary>
    public sealed class UiTextFit : MonoBehaviour
    {
        [SerializeField, Range(0.3f, 1f), Tooltip("글자를 줄일 수 있는 최소 비율(원래 크기 대비)")] private float _minScale = 0.55f;
        [SerializeField, Min(0.2f)] private float _scanSeconds = 0.5f;

        private readonly HashSet<int> _done = new HashSet<int>();
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Mathf.Max(_scanSeconds, 1f); // 전체 훑기는 무거우니 1초 이상 간격

            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || !text.gameObject.scene.IsValid() || !text.gameObject.activeInHierarchy || !_done.Add(text.GetInstanceID())) continue;
                if (text.name.StartsWith("Legacy_") || text.GetComponent<ContentSizeFitter>() != null || text.enableAutoSizing) continue;
                var rect = text.rectTransform.rect;
                if (rect.width < 8f || rect.height < 8f) continue;
                float size = text.fontSize;
                text.enableAutoSizing = true;
                text.fontSizeMax = size;
                text.fontSizeMin = Mathf.Max(8f, size * _minScale);
                if (InButtonFace(text.rectTransform)) text.alignment = TextAlignmentOptions.Center;
            }

            foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (text == null || !text.gameObject.scene.IsValid() || !text.gameObject.activeInHierarchy || !_done.Add(text.GetInstanceID())) continue;
                if (text.name.StartsWith("Legacy_") || text.GetComponent<ContentSizeFitter>() != null || text.resizeTextForBestFit) continue;
                var rect = text.rectTransform.rect;
                if (rect.width < 8f || rect.height < 8f) continue;
                int size = text.fontSize;
                text.resizeTextForBestFit = true;
                text.resizeTextMaxSize = size;
                text.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(size * _minScale));
                if (InButtonFace(text.rectTransform)) text.alignment = TextAnchor.MiddleCenter;
            }
        }

        /// <summary>글자가 단추의 위쪽 몇 단계 안에 있고, 단추 폭의 60% 이상을 차지하는가(작은 부제목·모서리 표시는 제외).</summary>
        private static bool InButtonFace(RectTransform textRect)
        {
            Transform parent = textRect.parent;
            for (int depth = 0; depth < 3 && parent != null; depth++, parent = parent.parent)
            {
                if (parent.GetComponent<Button>() == null && parent.GetComponent<Toggle>() == null) continue;
                var face = parent as RectTransform;
                if (face == null || face.rect.width < 1f) return false;
                return textRect.rect.width >= face.rect.width * 0.6f;
            }
            return false;
        }
    }
}
