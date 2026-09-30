# 마왕성 디펜스 (Majok Castle Defense)

오즈게임랩 2기 1팀

그리드에 마왕군을 배치해 성을 지키는 **백팩형 그리드 디펜스 로그라이크**입니다.
같은 유닛을 합쳐 성급을 올리고(합성), 라운드가 진행될수록 강해지는 용사 웨이브를 막아내며,
10라운드마다 등장하는 보스를 넘어서는 것이 목표입니다.

> **범위 안내**: 이 문서는 **InGame 씬(`Assets/00.Scenes/Builds/InGame.unity`,
> `InGamePrototype.prefab` / `InGamePrototypeBootstrap.cs` 기준)에서 실제로 도는 것**만 다룹니다.
> 로비·스테이지 선택·특성트리·스킬트리는 별도 씬이라 이번 정리에서는 제외했습니다(맨 아래 별도 표기).

## InGame 씬 핵심 흐름

`InGamePrototypeBootstrap.RunAsync()`가 아래를 순서대로 묶어서 실행합니다.

1. **준비(배치)** — `PreparationPage` → `GridWorldPreparationView` + `GridPrototypeRunner`(UI Toolkit 보관함/버튼)로 그리드에 마왕군 배치
2. **전투** — `RealStageBattleFactory`가 만든 `PooledStageBattle`(+`HeroPool`, `RealDefenders`)을 `StageManager`가 라운드 단위로 실행
3. **보상 / 증강** — 일반 라운드는 개발용 보상 버튼, 보스 라운드는 `InGameAugmentRewards`가 실제 후보를 뽑아 팝업 표시
4. **다음 라운드로 반복**, 최종 라운드 클리어 또는 패배 시 결과 표시 후 재도전/로비 복귀 플로우로 이동

## 시스템별 구현 현황 (코드 확인 기준)

### ✅ 실제로 붙어서 동작 중

- **그리드 배치** — `GridRunSession`이 실제 `GridManager`를 생성하고, `InGamePhasePresentation`이 이를 `GridWorldPreparationView`에, `GridPrototypeRunner`(준비 화면)에도 같은 세션을 바인딩. 발판 배치/보관함/전투 스냅샷까지 전부 실동작.
- **합성(성급)** — 같은 `GridManager` 인스턴스의 합성 로직 사용. 같은 유닛 2개 합성 시 성급 상승(최대 3성), 성급별 전용 차지 칸 모양 적용. 자리가 부족하면 합성은 허용하되 빨간 오버레이로 표시하고 전투 시작만 막음.
- **유닛 & 전투 (본 파트)** — `RealStageBattleFactory` → `HeroPool` + `RealDefenders` + `PooledStageBattle` → `StageManager`가 유일한 실제 전투 경로. 6직업(전사/방패병/궁수/마법사/도적/힐러) 근접·원거리·스플래시·도발·힐·CC 로직, 직업 기믹(궁수·도적 최저 체력 우선 타겟, 성급 2+ 스턴콤보/처형 보너스, 마왕군 전용), 성급별 힐러·궁수 사거리 증가, 힐러 전투종료 시 치료중단·자힐, 중간보스 2종+최종보스 1종, 라운드가 갈수록 용사가 강해지는 HP/공격력 배율 커브, 히어로 외형 랜덤화까지 전부 이 경로에서 실동작.
- **코어루프 연결 계약** — `IInGameCombatParticipant` 계약으로 `ProjectileCombatParticipant`가 실제로 등록되어, 전투 시작/종료에 맞춰 발사체 정리가 연동됨.
- **카메라 전환** — `InGameCameraTransition`이 씬 메인 카메라에 실제로 물려 있고, 준비↔전투 전환 시 `InGamePhasePresentation`이 이를 구동.

### 🔶 로직은 실제, 화면은 개발용(정식 UI 아님)

- **보스 라운드 증강** — `InGameAugmentRewards`가 `RealSynergySync`와 연동해 실제 후보를 뽑지만, 화면은 `InGameAugmentDummyView`의 `OnGUI()` 팝업(정식 Canvas/UI 아님).
- **일반 라운드 보상(유닛/발판/그리드 확장)** — 로직 자체가 아직 더미 경로(`ManualStageServices`)이고, 화면도 `InGameDummyView`의 개발용 버튼.
- **런 결과(승패) 화면** — 전용 화면 GameObject 없이, 같은 `InGameDummyView`의 `OnGUI()`로 승패 텍스트 + 재도전/스테이지선택/로비 버튼만 표시.

### ❌ 인게임 씬에 아직 안 붙어 있음

- **배틀 HUD / 준비 카드 / 스킬 슬롯** — 씬의 `DummyBattlePage`는 컴포넌트 없는 빈 오브젝트. HUD·카드·스킬 슬롯 UI는 별도 프리팹으로 설계·제작만 되어 있고 InGame 씬에는 아직 연결 전.

## InGame 씬 범위 밖 (별도 씬/시스템)

아래는 실제로 존재하고 상당 부분 구현되어 있지만, **InGame 씬 자체에는 포함되지 않는** 별도 씬/스크립트입니다.

- **로비 / 스테이지 선택** — `Lobby.unity`, `StageChoice.unity` 별도 빌드 씬. `StageSelectionController`가 난이도 선택 후 InGame 씬으로 핸드오프.
- **특성 트리(로비 메타 성장)** — `UITraitTreeView` 등 별도 UI. 몬스터/히어로/스킬/경제 4분기 40노드 구조는 구현되어 있으나 InGame 씬에는 없음.
- **스킬트리 · 마왕 레벨업** — `Progression/{TraitTree, SkillTreeStore, MawangLevel, MawangXpBridge}` 등 별도 시스템. InGame 씬은 결과 화면에 표시할 `MawangXpBridge`의 레벨/XP 값만 읽어올 뿐, 레벨업·스킬트리 UI 자체는 갖고 있지 않음.

## 기술 스택
- Unity 6.3 LTS (6000.3.22f1)
- SPUM(Soonsoon Pixel Unit Maker) 스프라이트/애니메이션 에셋
- New Input System
- ScriptableObject 기반 데이터 주도 설계 (유닛 스탯, 스테이지/라운드, 그리드 유닛 정의 등 코드 재빌드 없이 밸런싱 가능)

## 참고 문서
- `Docs/` — 시스템별 팀 가이드(TeamGuide) 및 검증(Verification) 문서 (설계 의도 확인용 — 실제 연동 여부는 이 README와 다를 수 있음)
- `마왕레벨업_스킬트리_특성트리_기획.md` — 메타 성장(레벨업/특성/스킬트리) 기획 원본
