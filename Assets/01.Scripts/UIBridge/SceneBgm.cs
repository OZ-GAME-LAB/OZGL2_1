using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 타이틀·로비에서 배경음악을 틀어 준다. 전투 BGM 은 소리 시스템(BattleSfxWatcher)이 전투 씬에서 알아서 틀고 끄므로
    /// 이 컴포넌트는 타이틀·로비 씬에만 붙인다. 전투에서 돌아올 때 전투 BGM 을 끄는 순간과 겹치지 않게
    /// 주기적으로 "이 곡이 나오고 있는지" 다시 맞춘다(같은 곡이 이미 나오고 있으면 이어서 재생). 배경음악을 끈 설정이면 틀지 않는다.
    /// </summary>
    public sealed class SceneBgm : MonoBehaviour
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f), Tooltip("소리 카탈로그의 전투 BGM 과 같은 크기가 기본")] private float _volume = 0.1f;
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next || _clip == null) return;
            _next = Time.unscaledTime + 0.5f;
            if (Sfx.Muted || Sfx.BgmMuted) { Sfx.StopBgm(); return; }
            Bgm.Play(_clip, _volume);
        }
    }
}
