using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 전투 HUD 스킬 슬롯의 포인터 입력만 전달한다.
    /// 조준·범위 표시·시전 판정은 기존 SkillBarUI가 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIInGameSkillInputRelay : MonoBehaviour, IPointerDownHandler
    {
        private Action<PointerEventData> _pointerDown;

        public void Configure(Action<PointerEventData> pointerDown)
        {
            _pointerDown = pointerDown;
        }

        public void Clear()
        {
            _pointerDown = null;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            _pointerDown?.Invoke(eventData);
        }
    }
}
