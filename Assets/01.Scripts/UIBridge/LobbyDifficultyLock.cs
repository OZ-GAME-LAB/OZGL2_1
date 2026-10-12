using System.Reflection;
using OZGL2.Progression;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 기존 진행 기록으로 로비 난이도 카드의 잠금 표시와 시작 버튼 상태를 맞춘다.
    /// - 보통을 깨야 어려움, 어려움을 깨야 지옥이 열린다(StageClearStore). 잠긴 카드의 실루엣·봉인 표시와 해금 안내를 맞춘다.
    /// - 잠긴 난이도가 선택돼 있으면 시작 단추를 막고 고정된 잠금/해금 라벨을 전환한다.
    /// - 디버그 열기(F8 패널의 「난이도 전부 열기」)를 켜면 전부 열린다.
    /// 슬롯이 다시 그려질 때마다 잠금이 풀리므로 매 프레임 상태를 다시 맞춘다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class LobbyDifficultyLock : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FPrev = typeof(UILobbyDifficultySelector).GetField("_previousSlot", Priv),
            FCur = typeof(UILobbyDifficultySelector).GetField("_currentSlot", Priv),
            FNext = typeof(UILobbyDifficultySelector).GetField("_nextSlot", Priv),
            FStart = typeof(UILobbyDifficultySelector).GetField("_startButton", Priv),
            FCard = typeof(UILobbyDifficultySlotView).GetField("_card", Priv),
            FName = typeof(UILobbyDifficultySlotView).GetField("_nameLabel", Priv),
            FFrameGroup = typeof(UILobbyDifficultySlotView).GetField("_frameGroup", Priv);

        [Header("시작 버튼 고정 라벨 / 둘 다 미연결이면 기존 단일 Label 호환")]
        [SerializeField] private TMP_Text _lockLabel;
        [SerializeField] private TMP_Text _unLockLabel;

        private UILobbyDifficultySelector _selector;
        private Button _start;
        private TMP_Text _startLabel;
        private string _startLabelOriginal;
        private TextAlignmentOptions _startAlignOriginal;
        private bool _forcedOff;
        private float _nextFind;

        private void LateUpdate()
        {
            if (_selector == null)
            {
                if (Time.unscaledTime < _nextFind) return;
                _nextFind = Time.unscaledTime + 0.5f;
                _selector = FindFirstObjectByType<UILobbyDifficultySelector>(FindObjectsInactive.Include);
                if (_selector == null) return;
                _start = FStart != null ? FStart.GetValue(_selector) as Button : null;
                // 새 라벨을 임의로 첫 번째 TMP로 취급해 문구를 덮어쓰지 않는다.
                if (_start != null) _startLabel = _start.transform.Find("Label")?.GetComponent<TMP_Text>();
            }

            // 카드의 잠금은 "그 슬롯이 지금 보여 주는 난이도(이름 글자)"를 따른다. 슬롯 내용은 화면 전환 중 투명할 때 바뀌므로
            // 잠금도 그 순간에 같이 바뀌어, 선택 번호가 바뀌는 즉시 잠금이 튀어나오는 일이 없다.
            ApplyCard(FPrev);
            ApplyCard(FCur);
            ApplyCard(FNext);

            // 시작 단추는 전환이 끝난 뒤에 맞춘다(전환 중에는 카드가 아직 이전 것을 보여 준다)
            if (_start == null || _selector.IsTransitioning) return;
            bool locked = !StageClearStore.IsDifficultyUnlocked(_selector.SelectedIndex);
            ApplyStartButtonState(locked);
        }

        private void ApplyStartButtonState(bool isLocked)
        {
            if (_start == null) return;
            ApplyStartLabels(isLocked);
            if (isLocked)
            {
                _start.interactable = false;
                _forcedOff = true;
            }
            else if (_forcedOff)
            {
                _start.interactable = true;
                _forcedOff = false;
            }
        }

        private void ApplyStartLabels(bool isLocked)
        {
            // 연결된 두 글자의 위치·폰트·여백·정렬은 Inspector 설정 그대로 유지한다.
            if (_lockLabel != null || _unLockLabel != null)
            {
                SetLabelActive(_lockLabel, isLocked);
                SetLabelActive(_unLockLabel, !isLocked);
                return;
            }

            // 아직 전환하지 않은 씬의 기존 단일 Label에만 이전 표시 방식을 적용한다.
            if (isLocked)
            {
                if (_startLabel != null)
                {
                    if (!_forcedOff) { _startLabelOriginal = _startLabel.text; _startAlignOriginal = _startLabel.alignment; }
                    _startLabel.text = "잠김";
                    _startLabel.alignment = TextAlignmentOptions.Center; // 짧은 글자가 한쪽으로 치우치지 않게 글자 칸 가운데에 둔다
                }
            }
            else if (_forcedOff)
            {
                if (_startLabel != null && _startLabelOriginal != null) { _startLabel.text = _startLabelOriginal; _startLabel.alignment = _startAlignOriginal; }
            }
        }

        private static void SetLabelActive(TMP_Text label, bool isActive)
        {
            if (label != null && label.gameObject.activeSelf != isActive)
                label.gameObject.SetActive(isActive);
        }

        private void ApplyCard(FieldInfo slotField)
        {
            if (slotField == null || FCard == null || FName == null) return;
            var slot = slotField.GetValue(_selector) as UILobbyDifficultySlotView;
            if (slot == null) return;
            var card = FCard.GetValue(slot) as UILobbyStageCardView;
            var label = FName.GetValue(slot) as TMP_Text;
            if (card == null || label == null) return;
            int difficultyIndex = System.Array.IndexOf(StageClearStore.DifficultyNames, label.text);
            bool locked = difficultyIndex > 0 && !StageClearStore.IsDifficultyUnlocked(difficultyIndex);
            // 편집기에서 잠긴 상태로 저장한 카드도 현재 슬롯 난이도에 맞는 안내를 갱신한다.
            card.SetLockCondition(locked ? StageClearStore.DifficultyNames[difficultyIndex - 1] + " 난이도 클리어 시 해금" : string.Empty);
            if (card.IsLocked != locked) card.SetLocked(locked);

            // 자물쇠·어두운 막은 팀 카드의 전환 투명도(테두리·그림이 사라졌다 나타나는 값)를 따르지 않아 혼자 계속 떠 있었다.
            // 슬롯이 보여 주는 전환 투명도에 맞춰 같이 사라졌다 나타나게 한다.
            float fade = FFrameGroup?.GetValue(slot) is CanvasGroup frame && frame != null ? frame.alpha : 1f;
            card.SetLockTransitionAlpha(fade);
        }
    }
}
