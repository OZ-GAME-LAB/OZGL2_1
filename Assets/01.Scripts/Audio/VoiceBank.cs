using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕이 말할 때 나는 옹알이 소리 12종. 공용 효과음 카탈로그(SfxCatalog)와 별개로, 마왕 말소리가 늘 들리도록 이 에셋이 클립을 직접 들고 있다.
    /// Resources/Voice/MawangVoiceBank 에 두고 MawangVoice 가 읽는다.
    /// </summary>
    public sealed class VoiceBank : ScriptableObject
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.5f;
    }
}
