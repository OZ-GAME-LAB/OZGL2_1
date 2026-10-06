using System;
using TMPro;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 팀 프리팹은 건드리지 않고, 이 씬에 보이는 글자 중 지정한 문구만 다른 문구로 바꿔 보여 준다(예: "돌아가기" → "뒤로").
    /// 팝업·화면이 나중에 열려도 잡도록 주기적으로 훑는다. 글자가 문구와 정확히 같을 때만 바꾼다.
    /// </summary>
    public sealed class UiTextRelabel : MonoBehaviour
    {
        [Serializable]
        public struct Pair
        {
            public string from;
            public string to;
        }

        [SerializeField] private Pair[] _pairs = { new Pair { from = "돌아가기", to = "뒤로" } };
        [SerializeField, Min(0.2f)] private float _scanSeconds = 0.6f;
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next || _pairs == null || _pairs.Length == 0) return;
            _next = Time.unscaledTime + _scanSeconds;
            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || !text.gameObject.scene.IsValid()) continue;
                string current = text.text;
                if (string.IsNullOrEmpty(current)) continue;
                string trimmed = current.Trim();
                foreach (var pair in _pairs)
                    if (trimmed == pair.from) { text.text = pair.to; break; }
            }
        }
    }
}
