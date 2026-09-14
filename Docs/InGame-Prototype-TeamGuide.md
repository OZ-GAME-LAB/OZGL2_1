# InGame 프로토타입 코어루프

작업 브랜치: `feat/ingame-core-loop/20`

## 실행

- 씬: `Assets/00.Scenes/Builds/InGame.unity`
- 프리팹: `Assets/_Project/Prefabs/InGame/InGamePrototype.prefab`
- 설정: `Assets/_Project/Data/InGame/InGamePrototypeConfig.asset`
- Play를 누르면 기본 1칸 발판과 Basic 유닛 1기를 자동 배치하고 1라운드 더미 전투를 시작한다. 첫 라운드의 내부 배치 검증은 수행하지만 사용자 준비 화면은 열지 않는다.
- `Dummy victory` → 유닛 A + 해당 발판 / 유닛 B + 해당 발판 / 확장 +2 중 하나 선택 → 다음 준비 화면.
- 준비 화면은 기존 GridPrototypeRunner의 드래그, 회전, 미리보기, 보관함 반환 기능을 사용한다. 확장은 배치해야 전투를 시작할 수 있고 유닛은 최소 1기 필요하다.
- 보관함은 유닛과 발판을 각각 세어 총 10개. 보상 공간이 부족하면 기존 항목을 버린 뒤 지급한다. 취소하면 보상을 다시 선택한다.
- 보스 라운드에는 더미 증강 확인 버튼을 누른다. 확률/실제 효과를 연결한 상태는 아니다.
- 최종 라운드 승리에는 일반 보상/증강 선택 없이 정산하고 Lobby로 이동한다. 패배도 정산 후 Lobby로 이동한다.
- 현재 Builds/Lobby 씬에는 로비 UI가 없으므로 빈 화면이 보이는 것이 정상이다. 재도전은 InGame 씬을 다시 실행하면 새 RunId의 1라운드부터 시작한다.

## 연결 구조

| 구성 | 책임 |
| --- | --- |
| InGamePrototypeBootstrap | StageRunHost 및 각 서비스 생성, 실행/종료, 팀 UISceneNavigator를 통한 로비 이동 |
| InGameGridSession | Stage의 RunId로 GridRunSession 생성, 종료, 더미 정산 영수증 |
| StageGridPreparation | 첫 자동 배치 및 이후 준비 요청을 실제 Grid 전투 배치 확정까지 대기 |
| StageGridRewards | 일반 보상 선택과 Grid 실제 지급 완료 연결, 보관함 처리 대기 |
| InGameDummyView | 개발용 버튼과 화면 전환. UIPageGroup으로 준비/더미 화면 전환 |
| InGamePrototypeConfigSO | Stage SO, Grid 카탈로그, 시작 위치, Lobby 경로 |

StageManager와 GridManager 내부 코드는 변경하지 않았다. JOB_KIMGUN 및 JOB_KIMGUN_STAGE도 유지한다. 준비 화면의 기존 독립 GridPrototypeHost/Flow는 InGame에서 생성하지 않는다. 하나의 Stage 실행과 동일한 Grid 세션만 사용한다.

## UI 담당자 연결

- `InGamePrototypeBootstrap.Stage`: 현재 상태, 라운드, 클리어 수, 진행 스냅샷.
- `Changed`: Stage 상태 및 Grid 변경 알림. 알림 중에는 읽기/화면 갱신만 수행하고 게임 명령은 사용자 입력 시 호출한다.
- `GridSession`: Grid 상태/보관함/배치, `Deployment`는 전투 시작 전에 확정된 스냅샷.
- `TryBeginBattle(skip)`: 배치 검증을 거친 전투 시작/준비 스킵.
- `Rewards.Pending.RequestId`: 현재 일반 보상 요청 ID. `TryChooseUnit(id, option)` 또는 `TryChooseExpansion(id)` 사용.
- 유닛 보상의 `false`는 거절뿐 아니라 보관함 공간 확보 대기일 수 있다. `GridSession.Grid.PendingStorage`를 확인해야 한다.
- 버리고 받기: `GridSession.TryConfirmStorage(runId, requestId, discard)`; 취소: `TryCancelStorage`.
- `CancelRun()`: 진행 중 실행 취소. 종료 전 중복 시작은 거절한다.
- `Dummy`의 전투/증강 완료 버튼은 개발용이다. 실제 UI의 게임 규칙으로 사용하지 않는다.

실제 UI 교체 시 InGameDummyView와 GridPrototypeRunner 화면을 제거/교체하고 위 상태·명령에 연결한다. Bootstrap과 게임 규칙은 화면 컴포넌트를 참조하지 않는다. `Changed`는 모든 더미 대기 생성 이벤트를 보장하지 않으므로 실제 전투/증강 UI는 해당 서비스의 요청 인터페이스에 별도 연결한다.

## 데이터와 아직 연결하지 않은 항목

- 현재 설정은 기존 `DummyStageHard` 50라운드와 Grid 더미 카탈로그를 참조한다. 라운드/보스 규칙은 Stage SO를 따른다.
- 초기 유닛/발판은 카탈로그에서 교체하며 현재 프로토타입 시작 조건은 1칸이다. 시작 위치가 유효하지 않으면 전투를 시작하지 않고 오류를 표시한다.
- 실제 유닛 풀, 사망/승패 판정, 스킬·시너지·합성, 계정 경험치/성장, 실제 증강 효과는 이 단계에서 연결하지 않았다. `IStageBattle`, `IStageRewards`, `IStageSession` 구현을 통해 연결해야 한다.
- 팀 실제 GridUnits SO의 `_footprint` 누락은 별도 데이터 결정이 필요하다. 이 작업에서 임의의 유닛 모양을 지정하지 않았으며 해당 SO는 InGame 더미 카탈로그에 연결하지 않았다.
- 진행 저장은 `Application.persistentDataPath/InGamePrototype/Runs/<RunId>.json`, 더미 정산 영수증은 `InGamePrototype/settlements.json`이다. 계정 성장 데이터와 분리되어 있다.
- 진행 저장은 라운드 기록이며 배치/인벤토리 복원이나 중단 지점 재개 기능은 아니다. 재도전은 새 실행으로 시작한다.
- 기본 Build Scene 목록에 InGame/Lobby를 추가했다. 별도 Build Profile이 Scene 목록을 덮어쓰면 해당 프로필에서도 등록해야 한다.

## 검증

Unity 메뉴 `OZGL2/InGame/Verify Integration` 실행 후 `InGameVerification.Result` 확인.

검증 범위: 전체 스테이지, 첫 자동 배치, 최종 라운드 보상 생략, 패배 정산, 준비 스킵, 보관함 초과/취소/버리고 받기, 오래된 보상 요청 거절, 확장 강제, 취소 정리, 새 실행 초기 배치.

실제 UI 레이아웃과 팀 전투 연동 검증은 해당 기능 연결 이후 별도로 진행한다.

### 2026-09-14 실행 결과

- InGame 통합 검사 PASS: 설정된 50라운드 전체, 패배, 보관함 처리, 확장 강제, 취소/새 실행.
- 기존 GridVerification PASS, StageFlowVerification의 30/50라운드 회귀 검사 PASS.
- 실제 InGame Play: 첫 라운드 자동 전투, 유닛 보상 후 2라운드 준비 화면, 6연승 중 보관함 초과 화면 취소/재선택/버리고 받기, 증강 확인, 준비 스킵 통과.
- 실제 패배 후 Builds/Lobby 이동 및 StageRunHost 잔여 개수 0 확인.
- 최종 Unity Console 오류/경고 0, InGame Missing Script 0, 끊어진 컴포넌트 참조 0, 새 에셋 `.meta` 누락 0.
- InGame 씬 저장 후 Play 종료. 현재 Build Profile은 공용 Build Scene 목록을 사용한다.
- 씬 YAML의 빈 `m_Name` 직렬화에 Unity가 생성한 후행 공백 1개가 있으나 코드/문서 공백 검사는 통과했다. 씬 YAML을 외부에서 수정하지 않았다.
- 실제 전투 프리팹 생성/공격/사망 및 최종 UI는 검증 대상이 아니다. 실행 빌드 생성 검증은 수행하지 않았다.
