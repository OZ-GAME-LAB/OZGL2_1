# 전투 스킬 HUD

## 현재 전투 연결 — 2026-10-08

- 적용 씬: `Assets/00.Scenes/Builds/InGame.unity`, `Assets/00.Scenes/Builds/InGame_UIIntegration.unity`.
- 편집 원본: `Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Prefabs/CombatSkillSlot.prefab` (현재 원형 디자인). 폴더의 v1은 에셋 경로이며 아래 최초 마름모 디자인 기록과 현재 외형을 구분해야 한다.
- 공용 화면: `Assets/02.Prefabs/UI/UI_Panel/Canvas_Combat.prefab`.
- `UIInGameSkillBarController`의 `Slot Prefab`에 원본 View를 연결한다. 씬의 `SkillSlot_1~3`은 편집 미리보기이고, 실행 시 제거한 뒤 원본 프리팹에서 실제 장착 개수(최대5개)만큼 `SkillSlot_Runtime_N`을 생성한다. 장착이 줄거나 Controller가 꺼지면 자신이 생성한 슬롯만 회수한다.
- `_sceneSlots`와 기존 `Configure()`는 다른 씬과의 호환을 위해 유지한다. 원본이 미연결인 기존 씬은 기존 연결 방식을 사용하며, 두 적용 씬은 더 이상 씬 슬롯을 복제하지 않는다.
- 공용 화면의 비활성 `Legacy_SkillSlot_1~3`(하위 포함9개 오브젝트)을 제거했다. 새 미리보기 3개를 지우거나 실제 장착 수에 맞춰 영구 변경할 필요는 없다.
- 중첩 슬롯의 아이콘·프레임·틴트·쿨타임Fill·모래시계·트랙·숫자·이름표를 null로 덮던24개 수정 기록을 Unity 공식 API로 되돌려 원본 연결을 상속한다. 이름표는 원본 안의 `SkillName` 한 곳만 사용한다.
- `InGameSkillBarFit`은 슬롯/프레임/이름표의 고정 Rect만 측정하며, 움직이는 불꽃·모래시계·재사용 연출은 배치 계산에서 제외한다. 장착 구성, 해상도, 영역 크기, 화면 활성 상태가 바뀔 때만 원래 기준에서 계산하고 X/Y 중심을 함께 보정한다.
- 기존 `InGameSkillBarScale`은 원본 프리팹 연결 모드에서는 배치와 추가 이름표 생성을 건너뛰고, 자체 이름표가 있는 슬롯에는 중복 이름표를 만들지 않는다.
- 실제 장착·쿨타임·시전·조준·취소는 기존 `SkillManager` / `InGamePrototypeBootstrap` / `SkillBarUI` 경로를 유지한다. 스킬 효과·수치·해금·저장 계산과 `InGameSkillCooldownFx`는 변경하지 않았다.

### Inspector / 충돌 검토

- 두 씬의 `BattleInGameUI/UIInGameSkillBarController`: Bootstrap, Slot Container, Slot Prefab, Catalog 연결 확인.
- 원본 슬롯: Button, UICombatSkillSlotView, SkillName 및 위8개 표시 참조 확인. 입력 Relay는 실행 시 연결한다.
- 공용 Canvas_Combat을 사용하는 다른 씬도 Legacy 제거와 표시 참조 복원이 상속된다. InGame_2 및 다른 씬 파일은 저장하지 않았다.
- Unity 저장 중 미리보기 슬롯의 LayoutGroup 구동 좌표가 직렬화되며, 기존에 불일치하던 CooldownSeconds의 Color32/Color 값도 동기화됐다. 이들은 원본 스타일을 바꾸는 별도 아트 수정이 아니다.
- 씬별 추가 변경은 Slot Prefab 연결1곳씩이다. 이번 작업 전 경험치 바 변경은 그대로 보존했다.
- LayerMask/Tag/Collider/Rigidbody/Animator/Input Action 추가 설정은 없다.

### 이번 검증

- 실제 공용 Canvas_Combat/CombatSkillSlot, 기존 SkillManager·SkillBarUI·쿨타임FX를 사용한 별도 Play Mode 테스트로 검증했다. 계정/보상 데이터는 바꾸지 않았다.
- 장착0→1→2→3→4→5→2: 실제 생성 개수 일치, 미장착 미리보기 회수, 슬롯당 이름표1개, 입력 Relay 연결, 중앙 정렬 통과.
- 각 개수에서 Canvas 닫기/열기와 Controller 비활성/재활성 후 개수·위치 복원 통과. Configure로 컨테이너 교체/복귀 시 이전 배치 복원도 통과.
- 쿨타임1→0, 실제 CooldownSpark와 ReadyFlash가 표시되는3.5초 동안 슬롯 위치/스케일 불변 확인. 즉시형 입력→시전, 쿨타임 입력 차단, 조준형 입력→조준→취소, UI 입력 차단 및 구형 런타임 바 숨김 통과.
- 저장한 두 씬을 다시 읽어 원본 연결과 Missing Script0을 확인했다. 편집 미리보기 좌표는 `(726,-938)`, `(960,-938)`, `(1194,-938)`로 유지된다.
- Play Mode 최종 Console Error0/Warning0. 최종 재컴파일 Error0, 기존 `InGameCardSizing._rewardRestingYOffset` 미사용 경고1개는 범위 밖이라 유지했다.
- 전체 웨이브 완주, 완전한 실제 마우스 드래그, 다른 화면비와 빌드 실행은 별도 확인이 필요하다. 실제 InGame 추가 검증 결과는 아래에 구분한다.
- 임시 검증 씬은 Unity 에셋 관리로 제거했다. 백업/캡처는 Git에서 제외되는 `Temp/CombatSkillRefactor_20261008/`에 보관한다.

### 실제 InGame 추가 검증 — 2026-10-08

- 저장된 런의 17웨이브 전투 완료 → 일반 보상 선택 → 18웨이브 준비/전투/패배 결과 표시를 확인했다. 30웨이브 완주 검증은 아니다.
- 18웨이브 실제 전투의 10,264개 관찰 프레임에서 슬롯 위치/스케일 변화 0건이었다. 준비 화면에서 숨김/전투에서 복귀할 때 두 슬롯 위치도 유지됐다.
- 새 1웨이브 런에서 기존 장착 2개에 강철 피부를 런타임으로만 임시 장착해 3개 생성·중앙 정렬을 확인했다. 해금/장착 저장본을 바꾸지 않았다. 각 슬롯에 SkillName 오브젝트가 정확히 1개였다.
- 강철 피부는 화면 슬롯의 실제 마우스 클릭으로 발동했다. 모래시계·남은 25초·테두리 쿨타임 표시와 재클릭 차단을 확인했다.
- 조준형은 기존 TryBeginSkillInput으로 조준만 시작한 뒤 실제 전장 클릭의 release로 시전했다. 화염구 투사체/착탄과 대상 3명의 체력 60→20, 남은 12초 표시를 확인했다. 준비/시전/쿨타임 연출 중 세 슬롯의 위치/배율이 유지됐다.
- 완전한 실제 드래그는 미확인이다. 자동 마우스 도구의 드래그를 InputSystem.onAfterUpdate로 관찰했을 때 누름과 놓음이 모두 전장 목적지 좌표로 들어와 슬롯의 PointerDown이 발생하지 않았다. 이를 게임의 시전 버그나 통과 결과로 간주하지 않는다.
- 추가 확인 당시의 입력 문제: 조준 중 이름표에 놓으면 취소 대신 시전됐다. 원본 슬롯 Root(180×180) 아래에 있는 SkillName이 취소 영역에 포함되지 않아, 연쇄 번개를 조준한 뒤 이름표에 실제 클릭하면 시전 횟수가 2→3으로 늘어났다. 이 확인 단계에서는 코드를 수정하지 않았으며, 후속 승인에 따라 아래와 같이 보정했다. 슬롯 사이/하단 바 빈 부분의 취소 정책은 변경하지 않았다.
- 게임 Console Error는 0개였다. 기존 손패 Viewport 높이 경고 1개와 최초 일반 타격에서 외부 VFX Missing Script 경고 5개를 확인했다. 스킬 HUD 수정과 구분해야 하며 Warning 0으로 보고하지 않는다.
- 외부 경고 대상: `Assets/98.ExternalAssets/00.LocalStaging/00.Packages/SPUM/Ultimate Resource Bundle/Res/Effect/Prefabs/` 아래 Eff_Damaged, Eff_SaintHeal, Eff_MagicCast, Eff_SaintCast, Eff_Slow. 공통 누락 스크립트 GUID는 `b9ef225a7c73246439ba77ade789fc53`이다. CombatEffectCatalog.asset이 참조하며 Resources.Load 시 함께 경고가 난다. 외부 원본은 수정하지 않았다.
- 테스트 전 게임 PlayerPrefs와 InGamePrototype 저장 파일을 백업했다. 종료 후 기존 OZGL2 저장 값 전부를 대조·복원했고, 저장 파일 107개의 해시가 백업과 모두 일치했다. 테스트 생성 런 2개와 변경된 보상 원장은 삭제 대신 `Temp/CombatSkillFullBattle_20261008/`에 보관했다.
- 원래 Title 씬의 Edit Mode로 복귀했다. 추가 Scene/Prefab/C#/Meta/ProjectSettings/Packages 저장은 없고, 이 검증 기록만 갱신했다. 기존 Lobby 씬 변경과 InGameSkillCooldownFx.cs의 해시는 작업 전과 동일하다. 커밋/push는 하지 않았다.

### 이름표 시전 취소 보정 — 2026-10-08

- `UIInGameSkillBarController.TryBeginCast`에서 활성 슬롯 Root와 함께 해당 View의 활성 SkillNameText RectTransform을 기존 취소 영역 목록에 전달한다. 누락/파괴 참조는 건너뛰고 기존 Root 취소는 유지한다.
- HUD 입력 코드만 수정했다. 새 직렬화 필드·public API·Inspector 연결은 없으며, 팀원 SkillBarUI·스킬 효과·수치·해금·저장 계산 및 기존 Scene/Prefab은 추가로 변경하지 않았다.
- 발표 후 재개 시 Lobby 씬이 사용자의 조작으로 달라진 것을 확인했다. 재개 시점의 저장본을 기준으로 보존하며, Lobby·InGame·InGame_UIIntegration·Canvas_Combat 및 팀원 입력/쿨타임 코드의 해시를 검증 전후 대조했다.
- 명시적 승인 후 Git에서 제외되는 `Assets/InitTestScene_HudNameCancel_20261008.unity`를 Unity API로 생성했다. 실제 게임 초기화 대신 비활성 Bootstrap/RealSynergySync 연결 프록시, 원본 슬롯 프리팹, 기존 SkillManager/SkillBarUI를 사용하는 격리 Play Mode 테스트로 검증했다.
- InputSystem의 임시 Mouse 상태 이벤트와 실제 입력 Relay/SkillBarUI Update 경로를 사용한 12개 검사 통과: 5개 이름표의 Root 밖 지점에서 release → 조준 종료·시전 없음·쿨타임 없음, 슬롯 Root 취소, 전장 정상 시전/쿨타임 시작, 쿨타임 재입력 차단, 즉시형 정상 시전, 비활성 이름표 제외, 이름표 참조 누락 안전 처리, UI 입력 차단 유지.
- 위 검증은 자동 상태 이벤트 기반이며 실제 손으로 하는 전체 드래그나 웨이브 진행을 다시 검증한 것은 아니다. 실제 전투 효과는 앞선 추가 검증 기록과 구분한다.
- 격리 테스트 Console Error 0 / Warning 0, 컴파일된 HUD에 이름표 접근 코드가 포함됨을 확인했다. 게임 계정/보상 저장 API는 실행하지 않았다.
- 검증 후 현재 Lobby의 Edit Mode로 복귀했고 미저장 변경은 없다. 임시 씬과 그 Meta는 Unity 에셋 관리로 제거했다. 사용자의 기존 변경사항은 보존했고 커밋/push는 하지 않았다.

아래는 최초 구성 시점의 기록이다. **기존 Noble Diamond Builder/Polisher를 현재 원형 화면에 다시 실행하지 않는다.** 구형 아트와 배치를 다시 적용할 수 있다.

## 최초 디자인 기록 — 01 Noble Diamond

## 적용 범위

- Scene: `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`
- 계층: `UI_BattleScreens/Canvas_Combat`
- 공통 Prefab: `Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Prefabs/CombatSkillSlot.prefab`
- 현재 아트: `Assets/06.UI/BattleMutedPreview/CombatSkills_v2/Sprites/` (v1 원본 보존)
- 01번 레퍼런스: `Docs/UI/References/CombatSkillHUD_20260926/01_NobleDiamond.png`

승인된 스킬 3칸과 하단 배경만 변경했다. 준비 화면, 상단 HUD, 시너지, 카드, 중앙 배치 영역은 수정 대상이 아니다. v2 적용 직전 사용자가 수정한 Scene 상태(벽 제거·테스트 버튼 위치 등)는 먼저 저장해 보존했다. 원본 `UI_Battle` 및 로비 Scene도 유지한다. 기존 `BottomStrip`과 `Legacy_SkillSlot_1~3`은 비활성 상태로 보존한다.

현재 구성은 **UI 미리보기**이다. 실제 스킬 발동, 자동 쿨다운 진행, 로비 저장 장착 정보와는 연결하지 않았다. 기존 슬롯과 마찬가지로 Button은 비활성이고 클릭 이벤트는 없다.

## 편집 위치

| 대상 | 편집 위치 | 역할 |
| --- | --- | --- |
| 공통 레이아웃 | `CombatSkillSlot.prefab` | 원본 수정 시 세 인스턴스에 공통 반영 |
| 어두운 마름모 | `Plate` | 단순 Unity Image, 회전 45도 |
| 분류별 내부색 | `CategorySlotTint` | 기존 공용 SO/Material, 아이콘 원본은 보존 |
| 분류 테두리 | `FrameArt` | 3종 Sprite를 카탈로그 분류에 따라 교체 |
| 원형 바탕 | `CooldownTrack` | 분류색의 어두운 원형 트랙 |
| 남은 비율 | `CooldownFill` | 밝은 금속색, Filled / Radial360 / Top / Clockwise |
| 사방 눈금 | `CooldownTicks` | 링을 끊어 보이는 Gap과 별도 Tick Image 4개 |
| 스킬 그림 | `Icon` | 기존 화염구·강철 피부·시간 정지 아이콘 재사용 |
| 남은 초 | `CooldownSeconds` | TMP, 올림한 초 표시, 준비 완료 시 숨김 |
| 하단 벨벳 | `Canvas_Combat/BottomSkillBackground` | 단일 PNG, Image Color 기본 RGB 0.6 / A 1 |

숫자/스킬 이름은 이미지에 합치지 않는다. 아이콘 본체는 공용 스타일의 흰색을 유지하고 분류색은 외곽선·옅은 내부색으로 구분한다. 프레임·배경·쿨다운 링은 새 리소스이며 기존 스킬 아이콘/공유 Material/SO는 수정하지 않았다.

세 인스턴스의 중심은 1920×1080에서 `(586,-878)`, `(960,-878)`, `(1334,-878)`이며 위쪽 왼쪽 앵커를 기준으로 한다. 공통 Prefab은 336×336, Scene 인스턴스 배율은 1이다. 세 프레임은 외곽 알파 크기 314×314와 중심 pivot을 공유한다. 기존 아이콘은 Sprite·Material·카탈로그를 교체하지 않고, 표시 크기를 기존과 같은 117×117(기존 104×1.125)로 유지했다.

하단 배경은 아래쪽 가로 Stretch, Size Delta `(0,256)`, Position `(0,12)`이다. 중앙 높이 약 238px, 양쪽 낮은 부분 약 137px, 어깨 경사 약 101×101px로 정렬했다. 슬롯은 중앙 윗변 위로 약 100px 돌출되고, 아래 꼭짓점과 실제 금속 하단선은 약 14px 떨어진다. 패널의 가장자리 장식은 하단선보다 약 10px 더 내려간다. 기존 벨벳 질감의 좌표만 정렬했으며 Image Color RGB 0.6은 보존했다. 장식 Image/TMP의 Raycast Target은 모두 꺼져 있다.

## 표시 코드와 값 조절

`Assets/00.Project/01.Scripts/UI/UICombatSkillSlotView.cs`는 **표시 책임만 분리한 컴포넌트**다. 실제 전투/저장 시스템을 참조하지 않는다.

- Inspector `Preview Catalog` / `Preview Entry Id`: 카탈로그에서 ID로 아이콘과 분류를 읽는다.
- `Preview Remaining Seconds` / `Preview Duration`: 쿨다운 표시 미리보기 값이다.
- `Cooldown Track Opacity`: 트랙 농도.
- `Cooldown Fill Color` / `Cooldown Fill Opacity`: 진행 링 색상/농도.
- `Cooldown Ticks`: 사방 눈금 연결. 트랙과 같은 분류색을 사용하고 빈 슬롯에서는 숨긴다.
- `Slot Tint Strength`: 공유 SO를 변경하지 않는 이 HUD 전용 내부색 강도. 기본 0.28로 공용 14%의 약 4%만 표시한다.
- v2 링은 200×200 캔버스에 외경 184px이며, 얇은 트랙과 굵은 진행 호를 서로 다른 Sprite로 사용한다. 기본 트랙 농도 0.78, 진행 호 농도 1이다.
- 기본 샘플: 화염구 0/12, 강철 피부 0/25, 시간 정지 8/40. 이는 정지된 아트 미리보기 수치다.

```csharp
view.ShowSkill(entry);       // 아이콘과 분류 변경. 새 스킬은 쿨다운 표시 초기화.
view.SetCooldown(8f, 40f);   // 남은 시간 / 전체 시간 = 0.2, TMP에는 8.
view.ShowSkill(null);        // 빈 슬롯: 무채색 프레임, 아이콘/링/숫자 숨김.
view.UsePreviewValues();     // 외부 표시 상태를 해제하고 Inspector 미리보기로 복귀.
```

`Fill Amount` 자체도 Image에서 조절할 수 있으나, View가 갱신되면 전달받은 남은 시간 비율이 우선한다. 미리보기에서는 위 시간 필드를, 실제 전투 연결 후에는 `SetCooldown`을 사용한다. 1은 쿨다운 전체, 0은 사용 가능이다. 음수/NaN/Infinity/0 이하 전체 시간은 안전 처리한다. 런타임 API 값은 비직렬화 필드에만 보관하며 SO/미리보기 필드에 쓰지 않는다.

편집 모드에서도 표시를 확인할 수 있도록 `OnValidate`는 갱신 요청만 남기고 Canvas 렌더 직전에 반영한다. Update 루프는 없으며 Canvas 이벤트 구독은 비활성 시 해제한다.

## 후속 실제 스킬 연결

- 실제 장착 시스템이 확정되면 선택된 스킬을 명시적으로 UI Entry로 변환해 `ShowSkill`을 호출한다. 현재 로비의 편집중 장착 배열을 저장본으로 간주하지 않는다.
- 실제 `SkillRuntime.RemainingCooldown` / `EffectiveCooldown`을 `SetCooldown`에 전달한다. UI 내부에서 임의로 시간을 진행하거나 실제 SO의 수치를 바꾸지 않는다.
- 실제 `SkillCategory`와 `eSkillPreviewCategory`는 BUFF/DEBUFF 순서가 다르므로 enum 숫자 캐스팅을 하지 않는다.
- 클릭/조준/발동 로직을 연결할 때만 Button과 필요한 Raycast를 별도 활성화한다. 이번 작업에서 시전 기능을 추가한 것은 아니다.

## 생성/후처리 기록

### v2 — 레퍼런스 비율 보정, 기존 스킬 아이콘 유지

- 새 생성: 분류별 프레임 3개, 얇은 트랙, 굵은 Fill. 아이콘은 생성하지 않았다.
- 원본/전체 프롬프트: `Tools/Art/Sources/CombatSkills_v2/generation.json` 및 같은 폴더의 `*_source.png`.
- 후처리: `Tools/Art/PrepareCombatSkillArtV2.py`, 정렬 기록: `Tools/Art/Sources/CombatSkills_v2/normalization.json`.
- 프레임 336×336(실제 314), 링 200×200(실제 184), 패널 1920×256. 실제 투명 배경, 중앙 pivot, FullRect, Point, 무압축, Mipmap Off, Clamp.
- 생성된 체크무늬/잔여 배경을 제거하고 여백·크기·중심·대칭을 정렬했다. 패널은 v1의 기존 벨벳과 금속 픽셀을 좌표 정렬한 파생본이며 원본은 덮어쓰지 않는다.

### v1 — 최초 적용 기록

- 내장 `image_gen` 사용. CLI/API fallback은 사용하지 않았다.
- 생성 원본 5개와 전체 최종 프롬프트: `Tools/Art/Sources/CombatSkills_v1/generation.json`
- 원본: 같은 폴더의 `*_source.png`. 원본은 변경하지 않는다.
- 후처리: `Tools/Art/PrepareCombatSkillArt.py`
- 정렬/알파 검사: `Tools/Art/Sources/CombatSkills_v1/normalization.json`

생성 결과의 체크무늬 배경을 제거하고 투명 RGB를 정리했다. 여백/크기/중심 정렬과 좌우 대칭 보정만 수행했으며 코드로 문양을 그리거나 재색칠하지 않았다. 패널 1920×256, 프레임/링 256×256. 모든 아트에 실제 알파, 중심 pivot, FullRect, Point, 무압축, Mipmap 없음, Clamp를 적용했다. `.meta`는 Unity에서 생성했다.

## Editor 도구와 Undo

- v2 보정 파일: `Assets/Editor/CombatSkillHudReferencePolisher.cs`
- v2 메뉴: `Tools > OZGL2 > Battle > Polish Combat Skill HUD (Reference)`
- 기존 공통 Prefab GUID와 세 인스턴스를 유지하며 프레임·링·눈금·배경·배치만 보정한다. 아이콘/미리보기 카탈로그 참조를 보존한다.
- Scene 배치 변경은 Undo 지원. Prefab 에셋 저장과 이미지 import 설정은 Undo 대상이 아니므로 Git diff로 별도 확인한다.
- 아래 v1 Builder는 최초 구성 기록이며 v2 화면에는 다시 실행하지 않는다. 기존 객체를 덮어쓰지 않는 보호 검사 때문에 v2 배경에서는 중단될 수 있다.

- 파일: `Assets/Editor/CombatSkillHudBuilder.cs`
- 메뉴: `Tools > OZGL2 > Battle > Build Combat Skill HUD (Noble Diamond)`
- 지정 Scene의 Edit Mode에서만 실행한다. 필수 에셋/대상/중복 이름/기존 Prefab 참조를 먼저 검사한다.
- 승인된 현재 Scene 미저장 변경은 먼저 저장한 뒤 적용한다. 대상 계층 수정은 Undo 지원, 새 폴더/Prefab 생성 및 이미지 import는 Undo 제외다.
- 재실행은 기존 Prefab·생성 슬롯·배경을 재사용한다. 사용자 수정 디자인을 덮어쓰지 않으므로 코드 기본값을 바꿔도 기존 객체에는 자동 소급되지 않는다.
- 기존 임시 UI는 비활성 보존한다. 다른 Canvas는 변경하지 않는다.

## v2 검증 — 2026-09-26

- Unity 컴파일 완료, Play Mode 중 최종 Console Error 0 / Warning 0.
- 필수 참조 누락 0, Scene Missing Script 0, 동일 공통 Prefab 연결 3개 확인.
- 새 Sprite 6개의 실제 알파·대칭·크기 및 중앙 pivot / Point / Mipmap Off / 무압축 설정을 각 1회 확인.
- 기존 전투 시작 이벤트로 준비 화면 비활성 / Combat 활성 확인. 세 슬롯별 0 / 0.5 / 1 비율, 비정상 시간, 빈 슬롯 눈금 숨김, 미리보기 복귀 후 아이콘 보존: 총 18개 검사 통과.
- 1920×1080 Game View 1회 확인. 결과: `Tools/Art/Previews/UI_Battle_CombatSkills_v2.png`.
- 기존 아이콘 3개의 Sprite / Material / 카탈로그 참조와 PNG SHA256이 작업 전후 동일하다. 공유 SO / Material은 수정하지 않았다.
- 원본 `UI_Battle.unity`와 `UI_Lobby_MutedPreview.unity`의 SHA256이 작업 전후 동일하다.
- 사용자 Scene 편집은 먼저 보존했으며 벽 제거·PreviewTriggers 위치 `(30,-319.31)`를 되돌리지 않았다. 작업 중 발견된 대상 밖 개별 Icon / Title 편집도 덮어쓰지 않았다.
- Edit Mode로 복귀해 저장 완료. 코드/문서 공백 검사 통과. 다른 화면비·실제 전투 시전·자동 쿨다운 진행·빌드는 미확인.
- Inspector 연결은 도구가 완료했다. LayerMask / Tag / Collider / Rigidbody / Animator / Input Action 추가 설정은 없다.

## v1 검증 — 2026-09-26 (최초 적용 시점)

- Unity 컴파일 완료. 최종 Console Error 0 / Warning 0.
- 세 슬롯의 공통 Prefab 연결, 필수 참조 누락 0, Scene Missing Script 0 확인.
- Sprite 5개의 실제 알파/중앙 pivot/Point/Mipmap Off 설정 확인.
- Play Mode에서 기존 전투 시작 이벤트 호출로 준비 화면 비활성/Combat 활성/새 슬롯 3개 표시 확인. 실제 마우스 입력 전체 테스트는 미실행.
- 3개 View 각각 0, 0.5, 1 상한 비율, NaN/Infinity, 카테고리 교체, 빈 슬롯 표시 검사 통과.
- 1920×1080 화면 확인 후 01 레퍼런스와 슬롯 간격/돌출 높이를 맞췄다. 최종 화면: `Tools/Art/Previews/UI_Battle_CombatSkills_v1.png`.
- 기존 Scene 블록 중 변경은 BottomStrip 활성 상태, 기존 슬롯 3개 이름/활성 상태, Canvas_Combat/SkillSlots의 자식 목록만이다.
- 원본 `UI_Battle.unity`와 `UI_Lobby_MutedPreview.unity`의 SHA256은 작업 전후 동일하다.
- Edit Mode 복귀, 저장 완료, dirty false. 다른 화면비·실제 전투·빌드는 미확인.
- LayerMask/Tag/Collider/Rigidbody/Animator/Input Action 추가 설정은 없다.

## v2 Git / 리뷰 범위

- Scene 변경 Yes: 전투 미리보기 씬의 슬롯 3개 배치와 하단 배경. 사용자가 별도로 편집한 내용도 포함되어 있으므로 커밋 시 구분한다.
- Prefab 변경 Yes: 기존 `CombatSkillSlot.prefab`의 표시 레이아웃·분류 프레임·링·눈금. GUID 유지.
- Meta 변경 Yes: 새 v2 아트와 Editor 코드의 Unity 생성 메타. 기존 아이콘 메타 불변.
- Runtime: `UICombatSkillSlotView.cs`에 이 화면용 내부색 강도와 눈금 표시만 추가. 실제 스킬/저장/시전 API 변경 없음.
- Editor: `CombatSkillHudReferencePolisher.cs` 추가. v1 Builder 유지.
- 생성 PNG 5개 + 기존 벨벳 정렬 파생본 1개, 생성 원본/프롬프트/후처리/적용 화면/이 문서 포함.
- ProjectSettings / Packages 변경 No. 커밋/push는 하지 않았다.

## v1 Git / 리뷰 범위 (최초 적용 시점)

- Scene 변경 Yes: 프리뷰 씬 1개(다른 작업과 충돌 주의).
- Prefab 변경 Yes: 새 공통 스킬 슬롯 1개.
- Meta 변경 Yes: 신규 아트/폴더/코드/Prefab의 Unity 생성 메타.
- ProjectSettings/Packages 변경 No. 기존 Runtime/공유 스킬 데이터/원본 아이콘은 유지한다.
- 새 Runtime View, Editor Builder, PNG 5개, 생성 원본·프롬프트·후처리 스크립트·문서가 추가되었다. 커밋/push는 하지 않았다.
- `git diff --check`의 Scene `value: ` / `m_Name: ` 공백은 Unity 직렬화 출력이다. 이를 제거하려고 Scene YAML을 직접 수정하지 않는다.
