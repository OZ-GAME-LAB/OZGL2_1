using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 이 씬의 글자 폰트를 정하는 컴포넌트. 프리팹·공용 폰트 에셋은 수정하지 않고 이 컴포넌트가 있는 씬에서만 동작한다.
    /// 두 가지 방식이 있다.
    /// ① 폰트 파일(_font)을 주면: 던파 비트체처럼 TTF 로 동적 TMP 폰트를 만들어 씬의 모든 글자를 그것으로 바꾼다.
    /// ② 미리 만든 TMP 폰트 에셋(_tmpFont)을 주면: 새 폰트 패키지(빛의 계승자·넥슨 Lv2 고딕)를 그대로 쓴다.
    ///    _replaceExisting 을 끄면 기존 글자는 건드리지 않고(희수 UI는 자기 폰트를 쓴다), 내 브릿지가 새로 만드는 글자만
    ///    UiFontOverride.Current 를 통해 이 폰트로 만든다.
    /// 나중에 생기는 글자(팝업 등)도 잡도록 주기적으로 훑는다(바꾸는 방식일 때만).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class UiFontOverride : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField, Tooltip("미리 만든 TMP 폰트 에셋(새 폰트 패키지). 지정하면 위 폰트 파일 대신 이것을 쓴다.")] private TMP_FontAsset _tmpFont;
        [SerializeField, Tooltip("끄면 씬에 이미 있는 글자는 그대로 두고, 브릿지가 새로 만드는 글자만 이 폰트를 쓴다.")] private bool _replaceExisting = true;
        [SerializeField, Min(0.1f)] private float _scanSeconds = 0.4f;
        [SerializeField, Range(-0.5f, 0.3f), Tooltip("글자 굵기. 0이 원래 굵기, 음수일수록 가늘어진다(폰트 파일로 만들 때의 TMP 글자에만 적용)")]
        private float _faceDilate = -0.18f;

        /// <summary>적용 중인 TMP 폰트. 다른 브릿지가 글자를 새로 만들 때 이 폰트를 쓰면 깜빡임 없이 처음부터 같은 폰트가 나온다.</summary>
        public static TMP_FontAsset Current { get; private set; }

        private TMP_FontAsset _asset;
        private bool _owned; // 이 컴포넌트가 만든(= 지워도 되는) 동적 폰트인가
        private float _next;

        private void Awake()
        {
            if (_tmpFont != null)
            {
                _asset = _tmpFont;
                _owned = false;
                Current = _asset;
                if (!_replaceExisting) { enabled = false; return; }
                Sweep();
                return;
            }

            if (_font == null) { enabled = false; return; }
            _asset = TMP_FontAsset.CreateFontAsset(_font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (_asset == null) { enabled = false; return; }
            _owned = true;
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
            if (_owned && _asset != null) Destroy(_asset);
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
                    // 공용 에셋(미리 만든 폰트)의 예비 폰트 목록은 건드리지 않는다
                    if (_owned && old != null && old != _asset && !_asset.fallbackFontAssetTable.Contains(old)) _asset.fallbackFontAssetTable.Add(old);
                    text.font = _asset;
                    if (_owned && (text.fontStyle & FontStyles.Bold) != 0) text.fontStyle &= ~FontStyles.Bold;
                }
                catch (System.Exception e) { Debug.LogWarning("폰트 교체 실패(" + text.name + "): " + e.Message); }
            }
            if (_font == null) return;
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
