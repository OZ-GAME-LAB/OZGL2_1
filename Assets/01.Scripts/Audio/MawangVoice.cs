using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕의 말소리(옹알이) 한 음절을 낸다. 튜토리얼 대사와 오프닝 스토리가 쓴다.
    /// 전용 소리 묶음(VoiceBank)을 먼저 쓰고, 없으면 공용 카탈로그의 TutorialVoice 슬롯으로 대신한다. 음소거·음량 설정은 효과음과 똑같이 따른다.
    /// 소리를 못 찾으면 한 번만 경고를 남겨(Player.log) 원인을 알 수 있게 한다.
    /// </summary>
    public static class MawangVoice
    {
        private const float MinInterval = 0.05f;
        private static VoiceBank _bank;
        private static bool _loaded, _warned;
        private static float _last = float.NegativeInfinity;
        private static int _lastIndex = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _bank = null; _loaded = false; _warned = false; _last = float.NegativeInfinity; _lastIndex = -1; }

        public static void Play(float pitchScale = 1f)
        {
            if (!_loaded) { _bank = Resources.Load<VoiceBank>("Voice/MawangVoiceBank"); _loaded = true; }
            if (_bank == null || _bank.clips == null || _bank.clips.Length == 0)
            {
                if (!_warned) { _warned = true; Debug.LogWarning("[마왕 말소리] Resources/Voice/MawangVoiceBank 를 찾지 못해 공용 효과음 슬롯(TutorialVoice)으로 대신합니다."); }
                Sfx.Play(SfxId.TutorialVoice, pitchScale);
                return;
            }
            float now = Time.unscaledTime;
            if (now - _last < MinInterval) return;
            int count = _bank.clips.Length, pick = Random.Range(0, count);
            if (count > 1 && pick == _lastIndex) pick = (pick + Random.Range(1, count)) % count;
            var clip = _bank.clips[pick];
            if (clip == null) return;
            _last = now; _lastIndex = pick;
            Sfx.PlayClip(clip, _bank.volume, false, pitchScale * Random.Range(0.94f, 1.06f));
        }
    }
}
