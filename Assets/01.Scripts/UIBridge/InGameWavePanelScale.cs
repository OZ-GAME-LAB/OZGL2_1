using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 왼쪽 위 「이번 웨이브」 패널을 작게 줄여 가운데 손패 카드의 왼쪽 끝을 가리지 않게 한다.
    /// 패널 묶음(WavePreviewViewport)은 좌상단 pivot 이라, 크기만 줄이면 왼쪽 위 모서리는 그대로 두고 오른쪽·아래로 줄어든다.
    /// 마스크와 안쪽 내용을 같이 줄이므로 펼침/접힘 연출은 그대로 동작한다. 팀 프리팹은 수정하지 않는다.
    /// </summary>
    public sealed class InGameWavePanelScale : MonoBehaviour
    {
        private const string ViewportName = "WavePreviewViewport";

        [SerializeField, Range(0.4f, 1f)] private float _scale = 0.72f;
        private float _next;
        private readonly System.Collections.Generic.List<RectTransform> _viewports = new System.Collections.Generic.List<RectTransform>();

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _viewports.RemoveAll(r => r == null);
            // 패널을 찾는 전체 훑기는 무거우니, 찾은 뒤에는 2초에 한 번만 새로 생긴 것이 없나 확인한다
            _next = Time.unscaledTime + (_viewports.Count == 0 ? 0.5f : 2f);
            foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                if (rect != null && rect.name == ViewportName && rect.gameObject.scene.IsValid() && !_viewports.Contains(rect)) _viewports.Add(rect);
            _next = Time.unscaledTime + (_viewports.Count == 0 ? 0.5f : 2f);
            ApplyScale();
        }

        private void ApplyScale()
        {
            foreach (var rect in _viewports)
                if (rect != null && Mathf.Abs(rect.localScale.x - _scale) > 0.001f) rect.localScale = new Vector3(_scale, _scale, 1f);
        }
    }
}
