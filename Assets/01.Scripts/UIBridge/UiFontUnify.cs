using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 화면의 모든 글자가 희수 UI의 글꼴(빛의 계승자 · NEXON Lv2 Gothic)만 쓰게 맞춘다.
    /// 증강 선택창·메뉴 팝업 같은 곳은 따로 만든 다른 글꼴(BattleOverlay Pixel, 기본 글꼴 등)이 붙어 있어 한 화면에서 글꼴이 섞여 보였다.
    /// 허용 목록에 없는 글꼴은 대표 글꼴(빛의 계승자 Bold)로 바꾼다. 허용 목록에 있는 글꼴(희수가 정한 제목·본문 글꼴)은 그대로 둔다.
    /// 새로 열린 창의 글자도 잡도록 주기적으로 훑는다.
    /// </summary>
    public sealed class UiFontUnify : MonoBehaviour
    {
        [SerializeField, Tooltip("대표 TMP 글꼴(빛의 계승자 Bold)")] private TMP_FontAsset _tmpMain;
        [SerializeField, Tooltip("바꾸지 않는 TMP 글꼴들")] private TMP_FontAsset[] _tmpKeep;
        [SerializeField, Tooltip("대표 일반(Text) 글꼴(빛의 계승자 Bold)")] private Font _legacyMain;
        [SerializeField, Tooltip("바꾸지 않는 일반(Text) 글꼴들")] private Font[] _legacyKeep;
        [SerializeField, Min(0.5f)] private float _scanSeconds = 1f;

        private readonly HashSet<int> _keepTmp = new HashSet<int>();
        private readonly HashSet<int> _keepLegacy = new HashSet<int>();
        private float _next;

        private void Awake()
        {
            if (_tmpMain != null) _keepTmp.Add(_tmpMain.GetInstanceID());
            if (_tmpKeep != null) foreach (var f in _tmpKeep) if (f != null) _keepTmp.Add(f.GetInstanceID());
            if (_legacyMain != null) _keepLegacy.Add(_legacyMain.GetInstanceID());
            if (_legacyKeep != null) foreach (var f in _legacyKeep) if (f != null) _keepLegacy.Add(f.GetInstanceID());
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _scanSeconds;

            if (_tmpMain != null)
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (text == null || !text.gameObject.scene.IsValid() || text.font == null) continue;
                    if (_keepTmp.Contains(text.font.GetInstanceID())) continue;
                    text.font = _tmpMain;
                }

            if (_legacyMain != null)
                foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
                {
                    if (text == null || !text.gameObject.scene.IsValid() || text.font == null) continue;
                    if (_keepLegacy.Contains(text.font.GetInstanceID())) continue;
                    text.font = _legacyMain;
                }
        }
    }
}
