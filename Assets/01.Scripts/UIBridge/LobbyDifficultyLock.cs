using System.Reflection;
using OZGL2.Progression;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 난이도 카드에 잠금을 건다(팀 UI·프리팹은 수정하지 않고 실행 중에 얹는다).
    /// - 보통을 깨야 어려움, 어려움을 깨야 지옥이 열린다(StageClearStore). 잠긴 카드는 희수 UI의 잠금 표시(자물쇠·어두운 막)가 나온다.
    /// - 잠긴 난이도가 선택돼 있으면 시작 단추를 누를 수 없게 하고 글자를 잠금 안내로 바꾼다.
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
            FFrameGroup = typeof(UILobbyDifficultySlotView).GetField("_frameGroup", Priv),
            FLockGroup = typeof(UILobbyStageCardView).GetField("_lockGroup", Priv),
            FShade = typeof(UILobbyStageCardView).GetField("_lockedShade", Priv),
            FShadeColor = typeof(UILobbyStageCardView).GetField("_lockedShadeColor", Priv);

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
                if (_start != null) _startLabel = _start.GetComponentInChildren<TMP_Text>(true);
            }

            // 카드의 잠금은 "그 슬롯이 지금 보여 주는 난이도(이름 글자)"를 따른다. 슬롯 내용은 화면 전환 중 투명할 때 바뀌므로
            // 잠금도 그 순간에 같이 바뀌어, 선택 번호가 바뀌는 즉시 잠금이 튀어나오는 일이 없다.
            ApplyCard(FPrev);
            ApplyCard(FCur);
            ApplyCard(FNext);

            // 시작 단추는 전환이 끝난 뒤에 맞춘다(전환 중에는 카드가 아직 이전 것을 보여 준다)
            if (_start == null || _selector.IsTransitioning) return;
            bool locked = !StageClearStore.IsDifficultyUnlocked(_selector.SelectedIndex);
            if (locked)
            {
                if (_startLabel != null)
                {
                    if (!_forcedOff) { _startLabelOriginal = _startLabel.text; _startAlignOriginal = _startLabel.alignment; }
                    _startLabel.text = "잠김";
                    _startLabel.alignment = TextAlignmentOptions.Center; // 짧은 글자가 한쪽으로 치우치지 않게 글자 칸 가운데에 둔다
                }
                _start.interactable = false;
                _forcedOff = true;
            }
            else if (_forcedOff)
            {
                if (_startLabel != null && _startLabelOriginal != null) { _startLabel.text = _startLabelOriginal; _startLabel.alignment = _startAlignOriginal; }
                _start.interactable = true;
                _forcedOff = false;
            }
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
            if (card.IsLocked != locked) card.SetLocked(locked);

            // 자물쇠·어두운 막은 팀 카드의 전환 투명도(테두리·그림이 사라졌다 나타나는 값)를 따르지 않아 혼자 계속 떠 있었다.
            // 슬롯이 보여 주는 전환 투명도에 맞춰 같이 사라졌다 나타나게 한다.
            float fade = FFrameGroup?.GetValue(slot) is CanvasGroup frame && frame != null ? frame.alpha : 1f;
            if (FLockGroup?.GetValue(card) is CanvasGroup lockGroup && lockGroup != null) lockGroup.alpha = locked ? fade : 0f;
            if (FShade?.GetValue(card) is Image shade && shade != null)
            {
                Color shadeColor = FShadeColor != null ? (Color)FShadeColor.GetValue(card) : shade.color;
                shadeColor.a = locked ? shadeColor.a * fade : 0f;
                shade.color = shadeColor;
            }
        }
    }
}
