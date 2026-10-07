using System;
using UnityEngine;

/// <summary>
/// 효과음 재생 진입점. 전투 로직에서는 Sfx.Play(SfxId.Hit)처럼 한 줄만 호출한다.
/// SfxCatalogSO를 Resources에서 지연 로드하고, 카탈로그/슬롯/클립이 비어 있으면 조용히 스킵한다
/// (CombatEffects와 같은 방식 — 효과음은 연출이라 전투 로직에 영향 없음).
/// </summary>
public static class Sfx
{
    private const string CatalogResourcePath = "SfxCatalog";

    private static SfxCatalogSO _catalog;
    private static bool _loaded;
    private static float[] _lastPlayedTime;
    private static int[] _lastClipIndex;

    /// <summary>옵션 화면 등에서 쓸 런타임 마스터 볼륨(0~1). 카탈로그 masterVolume과 곱해진다.</summary>
    public static float MasterVolume { get; set; } = 1f;
    public static bool Muted { get; set; }
    /// <summary>배경음악만 끈다(설정 창의 배경음악 토글). Muted 와 별개.</summary>
    public static bool BgmMuted { get; set; }
    /// <summary>효과음만 끈다(설정 창의 효과음 토글). Muted 와 별개.</summary>
    public static bool SfxMuted { get; set; }

    // 에디터 도메인 리로드 비활성 환경에서도 이전 플레이의 시간 기록/캐시가 남지 않게 초기화.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _catalog = null;
        _loaded = false;
        _lastPlayedTime = null;
        _lastClipIndex = null;
        MasterVolume = 1f;
        Muted = false;
        BgmMuted = false;
        SfxMuted = false;
    }

    private static SfxCatalogSO Catalog
    {
        get
        {
            if (!_loaded)
            {
                _catalog = Resources.Load<SfxCatalogSO>(CatalogResourcePath);
                _loaded = true;
            }
            return _catalog;
        }
    }

    /// <summary>카탈로그에 지정된 전투 BGM을 루프 재생한다(이미 같은 곡이면 이어서). Muted면 재생하지 않는다.</summary>
    public static void PlayBattleBgm()
    {
        SfxCatalogSO catalog = Catalog;
        if (Muted || BgmMuted || catalog == null || catalog.battleBgm == null)
        {
            return;
        }

        Bgm.Play(catalog.battleBgm, catalog.bgmVolume * catalog.masterVolume);
    }

    public static void StopBgm() => Bgm.Stop();

    public static void Play(SfxId id)
    {
        if (Muted || SfxMuted)
        {
            return;
        }

        SfxCatalogSO catalog = Catalog;
        if (catalog == null || !catalog.TryGet(id, out SfxCatalogSO.Entry entry) ||
            entry.clips == null || entry.clips.Length == 0)
        {
            return;
        }

        if (_lastPlayedTime == null)
        {
            int idCount = Enum.GetValues(typeof(SfxId)).Length;
            _lastPlayedTime = new float[idCount];
            _lastClipIndex = new int[idCount];
            for (int i = 0; i < idCount; i++)
            {
                _lastPlayedTime[i] = float.NegativeInfinity;
                _lastClipIndex[i] = -1;
            }
        }

        // 배속(timeScale)을 올려도 실제 시간 기준으로 쓰로틀 — 8배속에서 소리가 몰려 터지지 않게.
        float now = Time.unscaledTime;
        if (now - _lastPlayedTime[(int)id] < entry.minInterval)
        {
            return;
        }

        // 여러 변형(1·2·3)이 있으면 랜덤으로 고르되, 직전에 쓴 것과 같은 변형이 연달아 나오지 않게 한다.
        int count = entry.clips.Length;
        int pick = UnityEngine.Random.Range(0, count);
        if (count > 1 && pick == _lastClipIndex[(int)id])
        {
            pick = (pick + UnityEngine.Random.Range(1, count)) % count;
        }
        _lastClipIndex[(int)id] = pick;

        AudioClip clip = entry.clips[pick];
        if (clip == null)
        {
            return;
        }

        _lastPlayedTime[(int)id] = now;
        float pitch = UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        float volume = entry.volume * catalog.masterVolume * MasterVolume;
        SfxPlayer.Play(clip, volume, pitch);

        // 웨이브 클리어·승패처럼 "들려야 하는" 알림음은 BGM에 묻히기 쉬워서, 재생되는 동안 BGM을 줄여준다.
        if (IsStinger(id))
        {
            Bgm.Duck(Mathf.Min(clip.length, MaxDuckSeconds), DuckedBgmFactor);
        }
    }

    /// <summary>
    /// 카탈로그 슬롯 없이 클립을 직접 재생한다(스킬처럼 소리가 데이터 에셋에 달려 있는 경우).
    /// 마스터 볼륨·음소거 설정은 Play(SfxId)와 똑같이 따른다. duckBgm이면 알림음처럼 재생되는 동안 BGM을 줄인다.
    /// </summary>
    public static void PlayClip(AudioClip clip, float volume = 1f, bool duckBgm = false)
    {
        if (clip == null || Muted || SfxMuted)
        {
            return;
        }

        SfxCatalogSO catalog = Catalog;
        float master = (catalog != null ? catalog.masterVolume : 1f) * MasterVolume;
        SfxPlayer.Play(clip, volume * master, 1f);

        if (duckBgm)
        {
            Bgm.Duck(Mathf.Min(clip.length, MaxDuckSeconds), DuckedBgmFactor);
        }
    }

    private const float DuckedBgmFactor = 0.25f;
    private const float MaxDuckSeconds = 4f;

    private static bool IsStinger(SfxId id)
    {
        switch (id)
        {
            case SfxId.BattleStart:
            case SfxId.BossAppear:
            case SfxId.RoundClear:
            case SfxId.LevelUp:
            case SfxId.Victory:
            case SfxId.Defeat:
                return true;
            default:
                return false;
        }
    }
}

/// <summary>동시 재생 보이스를 돌려 쓰는 전역 오디오소스 풀. 필요할 때 자동으로 하나 생성된다.</summary>
internal sealed class SfxPlayer : MonoBehaviour
{
    private const int VoiceCount = 16;

    private static SfxPlayer _instance;

    private AudioSource[] _voices;
    private int _next;

    internal static void Play(AudioClip clip, float volume, float pitch)
    {
        if (_instance == null)
        {
            var go = new GameObject("SfxPlayer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SfxPlayer>();
            _instance._voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 2D 게임이라 위치 감쇠 없이 그대로 재생
                _instance._voices[i] = source;
            }
        }

        _instance.PlayOnVoice(clip, volume, pitch);
    }

    private void PlayOnVoice(AudioClip clip, float volume, float pitch)
    {
        // 놀고 있는 보이스를 우선 쓰고, 전부 재생 중이면 가장 오래된 순서(라운드 로빈)로 가로챈다.
        AudioSource voice = null;
        for (int i = 0; i < VoiceCount; i++)
        {
            AudioSource candidate = _voices[(_next + i) % VoiceCount];
            if (!candidate.isPlaying)
            {
                voice = candidate;
                _next = (_next + i + 1) % VoiceCount;
                break;
            }
        }

        if (voice == null)
        {
            voice = _voices[_next];
            _next = (_next + 1) % VoiceCount;
        }

        voice.clip = clip;
        voice.volume = Mathf.Clamp01(volume);
        voice.pitch = pitch;
        voice.Play();
    }
}
