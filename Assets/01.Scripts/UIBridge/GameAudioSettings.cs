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
        private const string BgmVolKey = "OZGL2.Audio.BgmVolume";
        private const string SfxVolKey = "OZGL2.Audio.SfxVolume";

        public static bool BgmEnabled => !Sfx.BgmMuted;
        public static bool SfxEnabled => !Sfx.SfxMuted;

        /// <summary>배경음악·효과음 음량(0~1). 켜기·끄기와 별개로 저장되고, 기본은 1(100%).</summary>
        public static float BgmVolume => Sfx.BgmVolume;
        public static float SfxVolume => Sfx.SfxVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Sfx.BgmMuted = PlayerPrefs.GetInt(BgmKey, 1) == 0;
            Sfx.SfxMuted = PlayerPrefs.GetInt(SfxKey, 1) == 0;
            Sfx.BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolKey, 1f));
            Sfx.SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolKey, 1f));
        }

        /// <summary>슬라이더를 끄는 동안에는 저장하지 않고(디스크 쓰기 방지), 손을 뗄 때 CommitVolumes 로 한 번에 저장한다.</summary>
        public static void SetBgmVolume(float volume)
        {
            Sfx.BgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(BgmVolKey, Sfx.BgmVolume);
            Sfx.RefreshBgmVolume();
        }

        public static void SetSfxVolume(float volume)
        {
            Sfx.SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxVolKey, Sfx.SfxVolume);
        }

        public static void CommitVolumes() => PlayerPrefs.Save();

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
