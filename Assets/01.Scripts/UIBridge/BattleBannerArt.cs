using TMPro;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 웨이브 시작·클리어 배너(InGameBattleFx)가 쓰는 팀 UI 그림 모음. Resources/BattleBanner/BattleBannerArt 에 두고 원본(06.UI)을 그대로 가리킨다 — 복사하지 않는다.
    /// 승리 결과창과 같은 그림(왕관·룬 고리·문장 틀·구분선)이라 게임 UI와 한 벌로 보인다.
    /// </summary>
    public sealed class BattleBannerArt : ScriptableObject
    {
        public Sprite ring;      // 룬이 새겨진 고리
        public Sprite crest;     // 마름모 문장 틀
        public Sprite crown;     // 승리 왕관
        public Sprite divider;   // 가운데에 마름모가 있는 금빛 구분선
        public TMP_FontAsset titleFont;   // 큰 글자(WAVE·번호·WAVE CLEAR) — 결과창 제목과 같은 글꼴
        public TMP_FontAsset bodyFont;    // 작은 글자(한마디·DANGER)
    }
}
