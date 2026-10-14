using TMPro;
using UnityEngine;

namespace OZGL2.UIFlow
{
    /// <summary>보조 UI의 이미지와 글꼴만 공유하며 게임 데이터는 보관하지 않습니다.</summary>
    [CreateAssetMenu(menuName = "OZGL2/UI/Heraldry Supplement Style", fileName = "HeraldrySupplementStyle")]
    public sealed class UIHeraldrySupplementStyleSO : ScriptableObject
    {
        [Header("이미지")]
        [SerializeField] private Sprite _frameSprite;
        [SerializeField] private Sprite _crestSprite;
        [SerializeField] private Sprite _nameplateSprite;
        [SerializeField] private Sprite _medallionSprite;
        [SerializeField] private Sprite _tailSprite;
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _dialogueSprite;
        [SerializeField] private Sprite _resultPanelSprite;
        [SerializeField] private Sprite _resultTrackSprite;
        [SerializeField] private Sprite _resultFillSprite;
        [SerializeField] private Sprite _darkButtonSprite;
        [SerializeField] private Sprite _redButtonSprite;

        [Header("글꼴")]
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;
        [SerializeField] private TMP_FontAsset _numberFont;

        [Header("프레임")]
        [SerializeField] private Color _backgroundColor = new Color32(18, 16, 18, 255);
        [SerializeField, Min(0.1f)] private float _frameMultiplier = 4f;

        public Sprite FrameSprite => _frameSprite;
        public Sprite CrestSprite => _crestSprite;
        public Sprite NameplateSprite => _nameplateSprite;
        public Sprite MedallionSprite => _medallionSprite;
        public Sprite TailSprite => _tailSprite;
        public Sprite PanelSprite => _panelSprite;
        public Sprite DialogueSprite => _dialogueSprite;
        public Sprite ResultPanelSprite => _resultPanelSprite;
        public Sprite ResultTrackSprite => _resultTrackSprite;
        public Sprite ResultFillSprite => _resultFillSprite;
        public Sprite DarkButtonSprite => _darkButtonSprite;
        public Sprite RedButtonSprite => _redButtonSprite;
        public TMP_FontAsset TitleFont => _titleFont;
        public TMP_FontAsset BodyFont => _bodyFont;
        public TMP_FontAsset NumberFont => _numberFont;
        public Color BackgroundColor => _backgroundColor;
        public float FrameMultiplier => Mathf.Max(0.1f, _frameMultiplier);
    }
}
