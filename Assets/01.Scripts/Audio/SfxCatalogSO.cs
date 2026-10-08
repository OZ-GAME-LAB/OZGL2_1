using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 효과음 ID. 전투씬에서 반드시 필요한 것만 우선 넣었다 — 새 효과음이 필요하면 여기에 추가하고
/// SfxCatalog.asset을 열면 OnValidate가 슬롯을 자동으로 만들어준다.
/// </summary>
public enum SfxId
{
    BattleStart,   // 웨이브(전투) 시작
    BossAppear,    // 보스 라운드 전투 시작(BattleStart와 함께 재생)
    AttackMelee,   // 근접 공격 스윙/타격 (전사·방패병·도적·근접 보스)
    ShootArrow,    // 궁수 화살 발사
    CastMagic,     // 마법사 마법 발사
    Hit,           // 근접·화살에 맞았을 때(데미지가 실제로 들어갔을 때)
    Heal,          // 회복
    DeathHero,     // 용사(적) 사망
    DeathAlly,     // 마왕군(아군) 사망
    LevelUp,       // 마왕 레벨업
    Victory,       // 전투 승리 / 런 클리어
    Defeat,        // 패배
    HitMagic,      // 마법(마법사 발사체·스킬)에 맞았을 때 — 기존 ID 값이 안 밀리게 맨 뒤에 추가
    RoundClear,    // 웨이브(라운드) 클리어 — 전투 승리 후 보상 단계로 넘어가는 순간
    UiHover,       // 버튼/카드 위에 마우스를 올렸을 때
    UiClick,       // 일반 버튼 클릭
    UiCancel,      // 닫기·취소·뒤로 류 버튼 클릭(오브젝트/글자 이름으로 판별)
    UiError,       // 배치 불가 등 거부
    UnitPlace,     // 유닛/블록을 그리드에 놓았을 때(이동·교환 포함)
    UnitReturn,    // 그리드에서 보관함으로 되돌렸을 때
    UnitGain,      // 보관함에 새 유닛/블록이 생겼을 때(뽑기·보상)
    Fusion,        // 합성 성공(성급 상승)
    RewardOpen,    // 보상/증강 선택 창이 열릴 때
    RewardPick,    // 보상/증강을 고르고 다음 단계로 넘어갈 때
    CoinGain,      // 처치한 재화(금화)가 재화 표시에 도착할 때 — 전용 소리가 없으면 UnitGain 소리를 쓴다
    ListOpen,      // 증강 리스트 등 접이식 목록을 펼칠 때 — 전용 소리가 없으면 UnitPlace 소리를 쓴다
    ListClose,     // 접이식 목록을 접을 때 — 전용 소리가 없으면 UnitReturn 소리를 쓴다
    TutorialBlip,  // 마왕 튜토리얼 말풍선에 글자가 찍힐 때(몇 글자마다 한 번)
    TutorialPop,   // 말풍선이 다음 말로 바뀔 때
    TutorialNext,  // 튜토리얼에서 눌러서 다음으로 넘길 때
    TutorialStart, // 튜토리얼이 시작되며 마왕이 나타날 때
    TutorialEnd,   // 튜토리얼이 끝날 때
}

/// <summary>
/// 효과음 슬롯 모음. Resources/SfxCatalog.asset로 저장돼 있고 Sfx가 지연 로드한다.
/// 사용법: 인스펙터에서 해당 슬롯의 Clips에 오디오 파일을 끌어다 놓기만 하면 바로 재생된다.
/// Clips를 여러 개 넣으면 매번 랜덤으로 골라 쓴다(반복감 완화). 비어 있는 슬롯은 조용히 무시된다.
/// </summary>
[CreateAssetMenu(fileName = "SfxCatalog", menuName = "MajokDefense/Sfx Catalog")]
public sealed class SfxCatalogSO : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [HideInInspector] public string label;   // 인스펙터에서 어떤 슬롯인지 보이게 id 이름으로 자동 채움
        public SfxId id;
        [Tooltip("파일을 여기에 끌어다 놓기. 여러 개면 매번 랜덤 선택.")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생 때마다 피치를 이 범위에서 랜덤으로 (둘 다 1이면 고정). 같은 소리 반복 완화용.")]
        public Vector2 pitchRange = Vector2.one;
        [Min(0f), Tooltip("같은 효과음이 이 시간(초) 안에 또 호출되면 무시 — 난전에서 소리가 뭉개지는 것 방지.")]
        public float minInterval = 0.05f;
    }

    [Range(0f, 1f)] public float masterVolume = 1f;

    [Header("BGM (전투씬 진입 시 루프 재생, 씬을 벗어나면 정지)")]
    public AudioClip battleBgm;
    [Range(0f, 1f)] public float bgmVolume = 0.1f;

    [Header("효과음 슬롯")]
    public List<Entry> entries = new List<Entry>();

    private Dictionary<SfxId, Entry> _lookup;

    public bool TryGet(SfxId id, out Entry entry)
    {
        if (_lookup == null)
        {
            _lookup = new Dictionary<SfxId, Entry>();
            foreach (Entry e in entries)
            {
                if (e != null)
                {
                    _lookup[e.id] = e;
                }
            }
        }

        return _lookup.TryGetValue(id, out entry);
    }

    private void OnEnable() => _lookup = null;

    // enum에 새 ID를 추가하면 에셋을 열 때 슬롯이 자동으로 생기고, 슬롯 이름(label)도 맞춰진다.
    private void OnValidate()
    {
        _lookup = null;
        foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
        {
            Entry existing = entries.Find(e => e != null && e.id == id);
            if (existing == null)
            {
                existing = new Entry { id = id };
                entries.Add(existing);
            }
            existing.label = id.ToString();
        }
    }
}
