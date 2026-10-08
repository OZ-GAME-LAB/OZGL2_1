using UnityEngine;

/// <summary>
/// 배경음악 재생기. 효과음(Sfx)과 달리 한 번에 한 곡만, 루프로 재생한다.
/// 같은 곡이 이미 재생 중이면 다시 시작하지 않고(씬 재진입/재시도 때 끊김 방지), 다른 곡이면 교체한다.
/// 곡/볼륨은 SfxCatalog.asset의 BGM 항목에서 정한다. 호출은 BattleSfxWatcher가 전투씬 진입/이탈에 맞춰 한다.
/// 웨이브 클리어·승패 같은 짧은 알림음은 BGM에 묻히기 쉬워서, 그 소리가 나는 동안 BGM을 잠깐 줄인다(Duck).
/// </summary>
internal sealed class Bgm : MonoBehaviour
{
    private const float DuckFadeSpeed = 4f; // 초당 볼륨 배율 변화량 — 약 0.2초에 걸쳐 줄고 되돌아옴

    private static Bgm _instance;

    private AudioSource _source;
    private float _baseVolume;
    private float _rawVolume = 1f;
    private float _duckFactor = 1f;
    private float _duckTarget = 1f;
    private float _duckUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    internal static void Play(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            Stop();
            return;
        }

        Bgm bgm = Instance();
        bgm._rawVolume = Mathf.Clamp01(volume);
        bgm._baseVolume = bgm._rawVolume * Sfx.MasterVolume * Sfx.BgmVolume;
        if (bgm._source.clip == clip && bgm._source.isPlaying)
        {
            bgm.ApplyVolume(); // 볼륨만 갱신
            return;
        }

        bgm._source.clip = clip;
        bgm.ApplyVolume();
        bgm._source.Play();
    }

    /// <summary>설정 창에서 음량을 바꿨을 때, 지금 재생 중인 곡의 볼륨만 다시 계산한다.</summary>
    internal static void RefreshVolume()
    {
        if (_instance == null) return;
        _instance._baseVolume = _instance._rawVolume * Sfx.MasterVolume * Sfx.BgmVolume;
        _instance.ApplyVolume();
    }

    internal static void Stop()
    {
        if (_instance != null && _instance._source != null)
        {
            _instance._source.Stop();
            _instance._source.clip = null;
        }
    }

    /// <summary>seconds 동안 BGM을 factor 배(0~1)로 줄였다가 부드럽게 되돌린다. BGM이 안 나오고 있으면 무시.</summary>
    internal static void Duck(float seconds, float factor)
    {
        if (_instance == null || _instance._source == null || !_instance._source.isPlaying)
        {
            return;
        }

        _instance._duckTarget = Mathf.Clamp01(factor);
        _instance._duckUntil = Mathf.Max(_instance._duckUntil, Time.unscaledTime + seconds);
    }

    private void Update()
    {
        float target = Time.unscaledTime < _duckUntil ? _duckTarget : 1f;
        if (!Mathf.Approximately(_duckFactor, target))
        {
            _duckFactor = Mathf.MoveTowards(_duckFactor, target, DuckFadeSpeed * Time.unscaledDeltaTime);
            ApplyVolume();
        }
    }

    private void ApplyVolume()
    {
        if (_source != null)
        {
            _source.volume = _baseVolume * _duckFactor;
        }
    }

    private static Bgm Instance()
    {
        if (_instance == null)
        {
            var go = new GameObject("Bgm");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Bgm>();
            _instance._source = go.AddComponent<AudioSource>();
            _instance._source.playOnAwake = false;
            _instance._source.loop = true;
            _instance._source.spatialBlend = 0f;
        }
        return _instance;
    }
}
