using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 배경음악·효과음 켜기/끄기 설정을 저장하고 소리 시스템(Sfx/Bgm)에 알려 준다.
    /// 로비·전투 메뉴의 설정 창 토글과 타이틀 설정 창이 모두 이 클래스를 거친다. 실행할 때 저장값을 불러와 적용한다.
    /// </summary>
    public static class GameAudioSettings
    {
        private const string BgmKey = "OZGL2.Audio.Bgm";
        private const string SfxKey = "OZGL2.Audio.Sfx";

        public static bool BgmEnabled => !Sfx.BgmMuted;
        public static bool SfxEnabled => !Sfx.SfxMuted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Sfx.BgmMuted = PlayerPrefs.GetInt(BgmKey, 1) == 0;
            Sfx.SfxMuted = PlayerPrefs.GetInt(SfxKey, 1) == 0;
        }

        public static void SetBgm(bool enabled)
        {
            Sfx.BgmMuted = !enabled;
            PlayerPrefs.SetInt(BgmKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            if (!enabled) Sfx.StopBgm();
        }

        public static void SetSfx(bool enabled)
        {
            Sfx.SfxMuted = !enabled;
            PlayerPrefs.SetInt(SfxKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
