using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 이 씬(InGame_2·Lobby_2)의 모든 글자를 지정한 폰트(던파 비트체)로 바꾼다. 프리팹·공용 폰트 에셋은 수정하지 않고,
    /// 이 컴포넌트가 있는 씬에서만 실행 중에 바꾼다 — 원본 InGame·Lobby 씬은 그대로다.
    /// - TextMeshPro 글자: 폰트 파일로 동적 TMP 폰트 에셋을 만들어 적용한다. 원래 폰트는 빠진 글자를 대신 채우는 예비 폰트로 남긴다.
    /// - 기존 Text 글자: 폰트 파일을 그대로 적용한다.
    /// 나중에 생기는 글자(팝업 등)도 잡도록 주기적으로 훑는다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class UiFontOverride : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField, Min(0.1f)] private float _scanSeconds = 0.4f;
        [SerializeField, Range(-0.5f, 0.3f), Tooltip("글자 굵기. 0이 원래 굵기, 음수일수록 가늘어진다(TMP 글자에만 적용)")]
        private float _faceDilate = -0.18f;

        /// <summary>적용 중인 TMP 폰트. 다른 브릿지가 글자를 새로 만들 때 이 폰트를 쓰면 깜빡임 없이 처음부터 같은 폰트가 나온다.</summary>
        public static TMP_FontAsset Current { get; private set; }

        private TMP_FontAsset _asset;
        private float _next;

        private void Awake()
        {
            if (_font == null) { enabled = false; return; }
            _asset = TMP_FontAsset.CreateFontAsset(_font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (_asset == null) { enabled = false; return; }
            _asset.name = _font.name + " (Dynamic)";
            if (_asset.fallbackFontAssetTable == null) _asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>(); // 동적으로 만든 에셋은 목록이 비어(null) 있다
            // 굵기 줄이기: 거리장(SDF) 글자 두께를 조금 깎고, <b> 굵게 효과는 끈다
            var mat = _asset.material;
            if (mat != null && mat.HasProperty(ShaderUtilities.ID_FaceDilate)) mat.SetFloat(ShaderUtilities.ID_FaceDilate, _faceDilate);
            _asset.boldStyle = 0f;
            _asset.boldSpacing = 0f;
            Current = _asset;
            Sweep();
        }

        private void OnDestroy()
        {
            if (Current == _asset) Current = null;
            if (_asset != null) Destroy(_asset);
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _scanSeconds;
            Sweep();
        }

        private void Sweep()
        {
            if (_asset == null) return;
            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || !text.gameObject.scene.IsValid() || text.font == _asset) continue;
                var old = text.font;
                try
                {
                    if (old != null && old != _asset && !_asset.fallbackFontAssetTable.Contains(old)) _asset.fallbackFontAssetTable.Add(old);
                    text.font = _asset;
                    if ((text.fontStyle & FontStyles.Bold) != 0) text.fontStyle &= ~FontStyles.Bold;
                }
                catch (System.Exception e) { Debug.LogWarning("폰트 교체 실패(" + text.name + "): " + e.Message); }
            }
            foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (text == null || !text.gameObject.scene.IsValid() || text.font == _font) continue;
                text.font = _font;
                if (text.fontStyle == FontStyle.Bold) text.fontStyle = FontStyle.Normal;
                else if (text.fontStyle == FontStyle.BoldAndItalic) text.fontStyle = FontStyle.Italic;
            }
        }
    }
}
