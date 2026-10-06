using System;
using OZGL2.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.InGame
{
    /// <summary>표시 전용 뷰. 팀 UI로 교체해도 전투 집계와 진행 순서는 변경하지 않는다.</summary>
    public sealed class InGameWaveResultView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _outcomeText;
        [SerializeField] private TMP_Text _experienceText;
        [SerializeField] private Button _confirmButton;
        private Action<string> _confirm;
        private string _requestId;

        public void ValidateSetup()
        {
            if (_waveText == null || _outcomeText == null || _experienceText == null || _confirmButton == null)
                throw new InvalidOperationException("Wave result UI references are missing.");
        }

        public void Present(StageWaveResult result, Action<string> confirm)
        {
            ValidateSetup();
            _requestId = result.RequestId;
            _confirm = confirm;
            _waveText.text = $"웨이브 {result.WaveNumber}";
            _outcomeText.text = result.IsCleared ? "클리어" : "실패";
            _experienceText.text = $"획득 경험치  +{result.EarnedExperience:N0} XP";
            _confirmButton.interactable = true;
            _confirmButton.onClick.RemoveListener(Confirm);
            _confirmButton.onClick.AddListener(Confirm);
        }

        public void Clear()
        {
            _confirm = null;
            _requestId = null;
            if (_confirmButton == null) return;
            _confirmButton.onClick.RemoveListener(Confirm);
            _confirmButton.interactable = false;
        }

        private void Confirm() => _confirm?.Invoke(_requestId);
        private void OnDestroy() => Clear();
    }
}
