# 스킬 UI 19종 표시 데이터 — v2

## 기준과 적용 범위

2026-09-22 사용자가 첨부한 최신 스킬 표(화염구부터 심판까지 19행)를 UI 표시 데이터로 옮긴다. 원본 표의 순서는 딜 6종 → 디버프 6종 → 버프 4종 → 궁극기 3종이다. 사용자의 요청에 따라 궁극기 3종도 UI의 `DAMAGE` 탭과 분류색을 사용한다. 최종 UI 분류는 **DAMAGE 9종 / DEBUFF 6종 / BUFF 4종**이다.

데이터 원본은 [SkillRoster_v2.json](../../Tools/Art/SkillRoster_v2.json)이며 최상위 구조는 항목 19개의 배열이다. 실제 `SkillData`, `SkillRuntime`, 전투 효과, 계정 SP, 해금 및 PlayerPrefs 저장 규칙은 이 UI 데이터로 변경하지 않는다. 특히 `절대 영도`의 UI 분류가 `DAMAGE`여도 표에 없는 피해를 추가하지 않는다.

모든 항목의 `DefaultUnlocked`는 기존 UI 미리보기 정책을 유지하는 `true`이다. `UnlockSp`는 표의 비용을 표시하기 위한 값이며, 이 데이터만으로 SP를 차감하거나 실제 스킬을 해금하지 않는다. 궁극기의 비용은 `Tier = 4`에서 계산하지 않고 표에 명시된 **5 SP**를 사용한다.

## 필드 계약

| 필드 | 의미 |
| --- | --- |
| `Id` | UI 미리보기의 고정 키. 실제 `SkillData.skillId`와 별도이다. |
| `Name` | 표의 스킬 이름. |
| `Description` | 핵심 효과와 수치를 담은 최대 2문장의 짧은 설명. |
| `Category` | UI 분류 문자열 `DAMAGE`, `BUFF`, `DEBUFF`. |
| `Tier` / `UnlockSp` | 표의 티어 및 해금 비용. |
| `Activation` | 표의 `지정` 또는 `즉시`. |
| `CooldownSeconds` | 표의 재사용 대기시간을 초 단위 숫자로 저장. |
| `EffectLabel` / `EffectValue` | 기존 상세 UI의 한 줄 요약에 사용할 표시 문구. 전체 효과를 대체하지 않는다. |
| `IsUltimate` | 표에서 궁극기로 분류된 유성우·절대 영도·심판만 `true`. |
| `DefaultUnlocked` | UI 미리보기 기본 표시 상태. 19개 모두 `true`. |
| `StatsText` | 표의 수치 열을 보존한 원문. 짧은 설명에서 생략한 보조 수치도 포함한다. |
| `IconPath` | Unity Sprite 에셋 경로. 현재 19개 모두 `Skills_v2/Sprites`의 아이콘에 연결되어 있다. |

## 현재 화면과 아트 구성

- 사용 카탈로그: `Assets/06.UI/LobbyMutedPreview/Skills_v2/SkillRosterCatalog.asset` (`UISkillPreviewCatalogSO`). JSON의 설명과 `StatsText`를 상세 설명에 연결하고, 티어·발동 방식·해금 SP는 별도 메타데이터 행에 표시한다.
- 실제 화면: `Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab`의 **일반 자식** `Canvas_SkillSettings`. 현재 스킬 화면은 중첩 Prefab 인스턴스가 아니다. `Skills_v1/Prefabs/Canvas_SkillSettings.prefab`은 이전 시안의 참고용 원본으로 보존한다.
- 목록: 19개 카드를 3열 세로 스크롤로 표시한다. 필터 결과는 전체 19 / 딜 9 / 디버프 6 / 버프 4개이며, 초기 장착은 화염구·연쇄 번개·공허 붕괴이다.
- 아이콘: `Skills_v2/Sprites`의 19개 Sprite를 사용한다. 9개는 새로 생성했고 10개는 기존 아트의 파생 복사본이다. 이전 원본 아트는 덮어쓰지 않는다.
- 궁극기 장식: `Ultimate_Card_Ornament.png`는 목록/상세, `Ultimate_Equipped_Ornament.png`는 장착 슬롯에 사용한다. 적색·고금색 장식을 별도 Image로 얹으며, 흰색 아이콘 본체·DAMAGE 외곽선·기존 호버/선택 표시는 유지한다. 잠긴 궁극기의 장식은 숨긴다.
- 생성 원본과 프롬프트 기록: `Tools/Art/Sources/SkillRoster_v2/generation.json` 및 같은 폴더의 원본 PNG. `Tools/Art/PrepareSkillRosterArt.ps1`은 알파 영역과 크기를 정렬하며, `normalization.json`에 파생 이미지의 원본 경로와 영역을 남긴다.

## Editor 적용 방법

1. Play Mode를 종료하고 `UI_Lobby_MutedPreview` 또는 부모 `Canvas_LobbyOverlays`의 Prefab Mode를 연다.
2. `Tools/OZGL2/Lobby/Build Skill Roster V2 (19 Skills)`를 실행한다. Scene에서 실행하면 부모 Prefab Mode를 열고, 그 안의 기존 스킬 화면만 조립한다.
3. 19개 카드, 스크롤, 상세 메타데이터, 궁극기 장식과 공유 상태 참조를 확인한 뒤 부모 Prefab을 수동 저장한다. Scene 자동 저장이나 Scene 인스턴스의 부모 전체 Apply는 하지 않는다.
4. Prefab Mode의 오브젝트/참조 변경 및 기존 카탈로그 값 변경은 Undo를 지원한다. 새 카탈로그·폴더 생성 자체는 Undo 삭제 대상이 아니며, PNG 생성·디스크 저장의 복구는 Git Diff와 함께 확인한다. 도구는 카탈로그 에셋을 저장한다.

변경 대상은 부모 Prefab의 `Canvas_SkillSettings` 하위와 v2 카탈로그/아트이다. 기존 도감·업적·메뉴·특성의 설정이나 실제 전투 자산을 이 도구의 대상에 포함하지 않는다.

## 최신 표 전사

반경·폭·길이·이동 속도에 표에 없는 단위를 덧붙이지 않는다. `번개 회오리`의 3.5초는 장판 지속시간이며, 개별 적의 정지 지속시간으로 바꾸지 않는다.

| 표 분류 | 티어 | 스킬 | 발동 | 쿨타임(초) | 해금 SP | 효과 | 수치 |
| --- | ---: | --- | --- | ---: | ---: | --- | --- |
| 딜 | 1 | 화염구 | 지정 | 12 | 1 | 지정 위치 범위 피해 | 피해 120, 반경 1.5 |
| 딜 | 1 | 얼음 가시 | 지정 | 10 | 1 | 직선 관통 + 감속 | 피해 90, 폭 0.9, 길이 12, 투사체 속도 16, 감속 배율 0.6(2초) |
| 딜 | 2 | 연쇄 번개 | 지정 | 15 | 2 | 가까운 대상부터 연쇄 | 피해 80, 연쇄 5체, 반경 3 |
| 딜 | 2 | 화염 회오리 | 지정 | 22 | 2 | 이동하는 지속 장판 | 도트 24/0.2초, 반경 1.6, 지속 4초, 이동 속도 8 |
| 딜 | 3 | 운석 낙하 | 지정 | 30 | 3 | 하늘에서 떨어지는 범위 피해 | 피해 400, 반경 4, 시전 지연 0.8초 |
| 딜 | 3 | 공허 붕괴 | 지정 | 28 | 3 | 끌어당김 + 피해 | 피해 150, 반경 2.5, 끌어당김 힘 7, 도트 10/0.5초(3초) |
| 디버프 | 1 | 넉백 파동 | 지정 | 10 | 1 | 반경 안 적 밀어냄 | 반경 2.5, 힘 8 |
| 디버프 | 1 | 감속 늪 | 지정 | 14 | 1 | 고정 장판, 감속 + 도트 | 반경 2, 지속 5초, 감속 배율 0.5, 도트 8/0.5초 |
| 디버프 | 2 | 저주 낙인 | 지정 | 16 | 2 | 고정 장판, 받는 피해 증가 + 도트 | 반경 2, 지속 4초, 받는 피해 ×1.3, 도트 6/0.5초 |
| 디버프 | 2 | 파도 | 지정 | 16 | 2 | 이동 장판, 감속 + 밀어냄 | 반경 2.6, 지속 3초, 이동 속도 7, 감속 배율 0.5, 밀어내기 힘 3 |
| 디버프 | 3 | 번개 회오리 | 지정 | 22 | 3 | 이동 장판, 닿으면 정지 | 반경 1.4, 지속 3.5초, 이동 속도 5 |
| 디버프 | 3 | 시간 정지 | 즉시 | 40 | 3 | 화면 전체 적 정지 | 범위 전체, 1.5초 |
| 버프 | 2 | 광폭화 | 즉시 | 25 | 2 | 전 아군 공격 속도 증가 | ×1.35, 8초 |
| 버프 | 2 | 강철 피부 | 즉시 | 25 | 2 | 전 아군 방어 증가 | ×1.3, 8초 |
| 버프 | 3 | 흡혈 의식 | 즉시 | 25 | 3 | 전 아군 체력 회복 | 최대 체력의 40% |
| 버프 | 3 | 망자 부활 | 즉시 | 35 | 3 | 죽은 아군 부활 | 3기 |
| 궁극기 | 4 | 유성우 | 즉시 | 60 | 5 | 화면 전체 연속 낙하 | 범위 전체, 발당 피해 150, 20연발 |
| 궁극기 | 4 | 절대 영도 | 즉시 | 90 | 5 | 화면 전체 적 정지 | 범위 전체, 4초 |
| 궁극기 | 4 | 심판 | 즉시 | 90 | 5 | 화면 전체 대피해 | 범위 전체, 피해 600 |

## UI ID와 실제 스킬의 관계

아래 5개 ID는 기존 UI 아트 ID를 최신 표의 정의에 맞추어 재사용한다. 이 재사용은 실제 게임 저장 키의 변경이나 마이그레이션을 의미하지 않는다. 특히 `ui_preview_strike`, `ui_preview_abyss`, `ui_preview_moon`의 이전 시안 이름·효과는 새 표로 대체된다.

| 보존 ID | 이전 UI 시안 이름 | 새 UI 정의 |
| --- | --- | --- |
| `ui_preview_fire` | 화염구 | 화염구 |
| `ui_preview_strike` | 번개 강타 | 연쇄 번개 |
| `ui_preview_abyss` | 심연의 파동 | 공허 붕괴 |
| `ui_preview_curse` | 저주 낙인 | 저주 낙인 |
| `ui_preview_moon` | 감속의 달 | 감속 늪 |

나머지 ID는 `ui_preview_ice_spike`, `ui_preview_fire_tornado`, `ui_preview_meteor`, `ui_preview_knockback_wave`, `ui_preview_wave`, `ui_preview_lightning_tornado`, `ui_preview_time_stop`, `ui_preview_berserk`, `ui_preview_iron_skin`, `ui_preview_vampiric_rite`, `ui_preview_raise_dead`, `ui_preview_meteor_shower`, `ui_preview_absolute_zero`, `ui_preview_judgment`를 사용한다.

기존 실제 스킬 자산은 `Assets/01.Scripts/Sandbox/Resources/Skills`에 있다. 현재 실제 `SkillData.skillId`는 영어 자산 파일명이나 `ui_preview_*`가 아니라 해당 스킬의 한국어 표시명이다. 이번 표와 겹치는 실제 스킬은 17종이며, `파도`와 `번개 회오리`에 대응하는 실제 자산은 현재 이 폴더에 없다.

## 실제 전투 자산과의 차이

아래는 이번 작업 시작 시 실제 `SkillData` 자산 및 실행 코드와 비교한 차이이다. 최신 표를 UI에 표시하는 것과 실제 전투를 이 표에 맞추는 것은 별도 작업이다.

| 항목 | 현재 실제 자산/동작 | 최신 UI 표 |
| --- | --- | --- |
| 명단 | 빙결 결계, 축복의 오라 포함 | 두 스킬을 제외하고 파도, 번개 회오리 포함 |
| 얼음 가시 | `AreaDamage`, 피해 90, 반경 1.3 | 직선 관통 + 감속, 폭 0.9, 길이 12, 속도 16, 감속 0.6배 2초 |
| 연쇄 번개 | 자산 반경 1.5 | 반경 3 |
| 공허 붕괴 | 끌어당김 + 피해 150, 별도 지속 피해 없음 | 10/0.5초의 지속 피해 3초 추가 |
| 감속 늪 | 감속 장판, 별도 지속 피해 없음 | 8/0.5초의 지속 피해 추가 |
| 저주 낙인 | 받는 피해 증가 장판, 별도 지속 피해 없음 | 6/0.5초의 지속 피해 추가 |
| 유성우 | 반경 5, 12연발 | 화면 전체, 20연발 |
| 궁극기 UI 분류 | 실제 `SkillCategory.Ultimate` | UI는 `DAMAGE`, `IsUltimate = true`, 해금 비용 5 SP 유지 |

실제 `SkillData`에는 효과 유형이 사용하지 않는 기본값도 남아 있다. 예를 들어 화염 회오리의 `skillPower = 100`, 절대 영도의 `skillPower = 100`을 피해 표시로 복사하면 잘못된 설명이 된다. 화염 회오리는 24/0.2초의 지속 피해, 절대 영도는 4초 정지로 표시한다. 흡혈 의식의 `duration = 0.4`는 현재 실제 구현에서 회복 비율이며 0.4초 지속시간으로 표시하지 않는다.

## 연결 및 확인 항목

- JSON은 표시 데이터 원본이며 자체적으로 Unity의 SO나 Scene을 수정하지 않는다.
- JSON을 바꾼 뒤에는 위 Builder로 v2 카탈로그와 카드 연결을 갱신하고 결과를 확인한다. 원본 전투 SO와 실제 게임 저장은 수정하지 않는다.
- 아이콘·카드·장착 슬롯·상세 화면은 [SkillCategoryVisualRules.md](SkillCategoryVisualRules.md)의 공통 흰색 본체 및 분류 외곽선 규칙을 유지한다.
- 19개 항목의 순서, 고유 ID, 분류별 개수, 티어/쿨타임/SP, 궁극기 3개 및 전부 기본 해금인 미리보기 상태를 확인한다.
- Play Mode에서 `Tools/OZGL2/Lobby/Validate Skill Roster V2`와 `Tools/OZGL2/Lobby/Validate Skill Roster V2 Text Layout`를 실행한다. 검사 결과는 실행 로그로 확인하며 이 문서에는 미실행 결과를 기록하지 않는다.
- 실제 포인터로 목록을 맨 아래까지 스크롤해 심판을 선택하고, 탭 전환·장착/해제·궁극기 장식·잠금/해금·미저장 뒤로/ESC·취소/폐기·재열기를 확인한다. 상세 설명과 수치가 잘리지 않는지 목록/상세/장착 크기 및 다른 화면 비율에서 확인한다.
- Console/Shader 오류, Missing Script/Reference, 실제 PlayerPrefs·전투 SO 불변을 확인한다. Git Diff에서는 부모 Prefab과 UI 카탈로그/아트의 변경 범위를 검토한다.
- 새 명단의 수치가 실제 전투에 이미 적용됐다고 안내하지 않는다. 실제 전투 연동은 별도 검증이 필요하다.

## 적용 및 검증 기록 — 2026-09-22

- Unity 6000.3.22f1의 `UI_Lobby_MutedPreview`에서 부모 Prefab의 스킬 영역을 저장했다. 스킬 외 1,801개 컴포넌트의 저장 전후 직렬화 값이 동일했고, Scene의 수동 override 112개를 유지했다. 이번 적용에서 Scene을 다시 저장하지 않았다.
- Play Mode: 스킬 구성/분류/해금/장착/저장 확인 793항목, 19종 상세 TMP 글리프/줄넘침 검사 469항목, 렌더 후 EventSystem 휠·마지막 카드 클릭 검사 17항목 통과.
- 기존 도감/업적/스킬 공유 해금 회귀 검사 226항목 통과. 검증 도구의 스킬 12개 고정 가정만 현재 카탈로그 개수로 일반화했고 유닛 12종 검사는 유지했다.
- 실제 Game View에서 전체 목록과 딜 탭, 유성우의 목록·상세·장착 장식을 확인했다. 임시 장착 변경은 미저장 확인창에서 폐기하여 기존 구성으로 복구했고 Play Mode를 종료했다.
- Console Error/Warning 0개. 누락 Inspector 참조와 실제 PlayerPrefs 장착/SP 불변을 검사했다. 해상도별 수동 플레이 및 실제 전투 효과 검증은 이번 결과에 포함하지 않는다.
- Runtime/Editor 코드와 문서의 공백 검사는 통과했다. 전체 Git 공백 검사에는 Unity가 직렬화한 빈 YAML 필드의 끝 공백이 표시되며, 이를 없애기 위해 Scene/Prefab을 텍스트 편집하지 않았다.
- 새 생성 원본은 built-in imagegen 출력 그대로 보존하고, 후처리는 승인된 투명 여백 정리와 표시 크기 정렬로 제한했다. 패키지·ProjectSettings·실제 전투 스킬 자산은 변경하지 않았다.
