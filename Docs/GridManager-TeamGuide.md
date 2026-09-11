# GridManager 개발·연결 명세 — 블록/유닛 분리 (2026-09-10)

## 확정 구조

사용 가능한 바닥 → 블록 → 유닛의 3단계입니다.

| 대상 | 규칙 |
| --- | --- |
| 바닥 | 초기 4×3, 최대 8×5. 1×2 확장 보상으로 추가. 확정 후 이동/제거 불가 |
| 블록 | 6종 모양. 모든 셀이 바닥 안에 있어야 하며 다른 블록과 겹칠 수 없음 |
| 유닛 | 요구 모양 ID가 일치하는 블록에만 배치. 블록 전체에 최대 1기 |
| 마왕 | 하단 중앙 별도 칸. 일반 바닥·배치 인원에 포함하지 않음 |

초기 바닥 좌표는 x=2..5, y=0..2이며 전체 경계는 x=0..7, y=0..4입니다. 좌표는 왼쪽 아래에서 위로 y가 증가합니다. 마왕의 독립 중심 좌표는 (3.5,-1)입니다.

기본 블록은 1칸, 일자 2칸, 일자 3칸, ㄱ자 3칸, L자 4칸, T자 4칸입니다. 블록 기준 칸은 상대좌표 (0,0)이며 회전은 기준 칸을 중심으로 (x,y) → (y,-x), 90도 시계 방향입니다. 반전은 없고, 회전해도 모양 ID는 동일합니다.

기본 유닛 → single, 마법사 → corner_three로 설정했습니다. 나머지 4종 유닛은 각 모양을 검증하는 더미 매핑입니다. 실제 캐릭터 종류별 매핑은 콘텐츠 담당이 정합니다. 유닛은 블록의 어느 점유 칸에 드롭해도 같은 블록을 대상으로 인식하고, 캐릭터 표시는 기준 칸에 정렬합니다.

## 실행과 조작

브랜치 feat/grid-manager/5, 씬 Assets/00.Scenes/JOB_KIMGUN.unity에서 Play합니다.

1. BLOCKS 영역에서 블록을 바닥으로 드래그해 배치합니다.
2. UNIT CARDS 영역의 유닛을 요구 모양과 같은 빈 블록에 드래그합니다.
3. R은 블록/확장 조각 회전, Esc는 취소입니다. 유닛 드래그 중 R은 블록을 회전시키지 않습니다.
4. 배치된 유닛의 기준 칸 표시를 드래그하면 유닛만 이동합니다.
5. 블록의 빈 부분을 드래그하거나, Shift를 누르고 드래그하면 유닛이 있는 블록도 이동할 수 있습니다. 1칸 블록의 유닛과 블록을 구별하기 위한 조작입니다.
6. 블록은 BLOCKS 영역, 유닛은 UNIT CARDS 영역에 드롭하면 각 목록으로 돌아갑니다.

블록 이동 중 위의 유닛 카드는 Return pending 상태로 임시 표시합니다. 실제 이동/회전/회수를 확정하면 유닛 연결을 해제하고 카드로 반환합니다. 기존 위치·회전 그대로 드롭하면 유닛을 유지하고 Changed만 통지합니다. 무효 드롭·Esc·포커스 상실은 블록 위치/회전과 유닛 연결을 모두 보존합니다. 유닛만 회수하면 블록과 바닥은 그대로 남습니다. 두 대기 목록은 가로 스크롤로 누적 보상 카드에 접근합니다.

모든 드래그는 실제 상태를 확정 전에 바꾸지 않습니다. 블록 이동 판정에서 자기 기존 점유는 제외하고, 유닛은 자기 현재 블록에 다시 놓을 수 있습니다. 초록색 반투명 고스트는 유효, 빨간색은 무효입니다. 유닛 드래그는 대상 블록 전체를 강조하고 기준 칸에 캐릭터 고스트를 표시합니다. 실제 캐릭터 대신 점과 이름을 사용하는 UI 프로토타입입니다.

## 확장과 전투 진행

Start battle → Simulate round clear → 유닛 후보 A / 유닛 후보 B / Choose floor +2 중 하나를 선택합니다. **유닛 두 기를 한 번에 받는 것이 아닙니다.** 유닛 선택 시 해당 유닛 1기와 요구 모양 블록 1개를 함께 받아 각각 배치합니다. 더미 후보는 기존 SO 카탈로그를 라운드마다 순환하며 실제 확률·해금·밸런싱은 보상 담당 구현으로 교체합니다. 시작 시 6종을 지급하는 기존 테스트 구성을 유지합니다.

GridRunSession은 RunId와 라운드별 PendingRewardId로 한 라운드의 선택을 한 번만 받습니다. 오래된 요청, 중복 선택, 다른 선택지의 후속 요청은 false를 반환합니다. 유닛·블록 추가를 모두 끝낸 뒤 이벤트를 통지하지만 준비 상태는 아직 열지 않습니다. 외부의 준비 허용 요청이 별도로 필요합니다. 이 중복 방지는 메모리 내 실행 단위이며 앱 재시작을 가로지르는 영구 지급 기록은 아닙니다.

확장 보상 선택 → 다음 준비 → 1×2 조각 강제 배치 → 전투입니다. 보관/누적/다음 라운드로 미루기는 없습니다. 배치 취소는 필수 보상을 없애지 않습니다. 조각은 기존 바닥과 변으로 연결되어야 하며 겹침·경계 이탈을 금지합니다. 점선은 확장 조각이 들어갈 수 있는 셀을 표시합니다.

CanExpand는 면적만 보지 않고 실제 유효한 조각 위치를 찾습니다. 보상 담당은 false일 때 확장 후보를 제외해야 합니다. 빈틈없는 배치로 14회에 총 40칸을 채울 수 있습니다.

전투 시작에는 일반 유닛 최소 1기, 필수 확장 완료, 진행 중인 드래그 없음이 필요합니다. 빈 블록·미배치 카드·마왕은 인원에 포함하지 않습니다. 첫 준비는 스킵 금지이며 후속 준비 스킵도 동일한 진행 조건을 적용합니다. 전투 중 두 종류의 배치를 모두 차단합니다.

## 데이터 및 주요 코드

코드는 Assets/_Project/Scripts/Grid, 더미 SO는 Assets/_Project/Data/Grid/Prototype에 있습니다.

| 파일 | 책임 |
| --- | --- |
| BlockShapeSO.cs | 블록 ID, 표시명, 기준 칸 기준 상대 점유 좌표 |
| GridUnitDataSO.cs | 유닛 ID, 표시명, 요구 블록 모양 ID |
| FootprintDefinition.cs | 불변 모양, 회전 및 데이터 검증 |
| GridSettingsSO.cs | 바닥 초기/최대 크기와 확장 모양 |
| GridContracts.cs | BlockPlacement, UnitPlacement, UnitDefinition 및 외부 인터페이스 |
| GridManager.cs | 준비 흐름과 블록/유닛 배치 연결 조정 |
| GridRunSession.cs | 실행 수명, 라운드 경계, 보상 선택 중복 방지, 전투 스냅샷 확정 |
| GridDeploymentSnapshot.cs | 읽기 전용 배치 전달 데이터와 셀→월드 좌표 변환 |
| GridPlacementRules.cs | 블록 점유 및 유닛 요구 모양/중복 검사 |
| UI/GridBoardView.cs | 바닥·블록·유닛·고스트 표시와 패널 좌표 변환 |
| UI/GridDragInput.cs | 두 종류의 드래그, Shift 선택, 회전/취소/포커스 처리 |
| Prototype/ | 분리된 더미 블록/유닛 목록과 진행 버튼 |
| Prototype/GridPrototypeHost.cs | 씬 재로드를 가로지르는 단독 테스트 세션 소유자 |
| Prototype/GridPrototypeBootstrap.cs | 단독 테스트에서만 Host를 생성하는 명시적 시작 컴포넌트 |
| Prototype/GridPrototypeFlow.cs | 테스트용 요청 ID 생성·보상 이후 준비 허용 담당 |
| Prototype/GridPrototypeRewards.cs | SO 스냅샷 기반 더미 유닛 후보 2개 공급 |
| Editor/ | 데이터 설정, 규칙 검사, UI 이벤트 검사 |

기존 UnitFootprintSO 스크립트는 Unity AssetDatabase로 BlockShapeSO.cs로 이름을 변경하여 GUID를 보존했습니다. 기존 6종 모양 SO의 점유 좌표와 참조를 유지하고, 새로운 유닛 매핑 SO 6개를 추가했습니다. SO는 실행 시 스냅샷으로 복사하며 원본을 런타임에 수정하지 않습니다.

## 팀원 연결 API

- AddBlock(instanceId, contentId, footprint): 블록 등록.
- AddUnit(instanceId, unitDefinition): 유닛 등록. UnitDefinition.RequiredBlockId는 BlockShapeSO의 모양 ID와 일치해야 합니다.
- Blocks: 블록 위치, 회전, 모양의 읽기 전용 목록.
- Units: 유닛 요구 조건 및 배치된 BlockId의 읽기 전용 목록.
- FindBlock / FindUnit / GetBlockAt / GetUnitOnBlock: 개별 조회.
- PlacedCount: 배치된 유닛 수. 블록 수가 아닙니다.
- BeginBlockDrag / BeginUnitDrag / BeginExpansionDrag: 배치 작업 시작.
- MovePreview / RotatePreview / GetPreviewFailure / CommitPreview / DropToTray / CancelDrag: 미리보기 및 확정.
- Changed: 미리보기와 단계 변경을 포함한 UI 갱신 신호.
- LayoutChanged: 블록/유닛 등록 또는 배치/회수/확장 확정 후 통지. 미리보기에는 발생하지 않습니다.
- LastObserverError: 동기 이벤트 구독자 오류. 비동기 구독자의 예외는 구독자 책임입니다.

블록과 유닛 인스턴스 ID는 각 목록 내에서 고유해야 하며, 모양 ID와 인스턴스 ID를 혼동하지 않아야 합니다. 같은 모양 블록 여러 개, 같은 유닛 종류 여러 기를 등록할 수 있습니다.

블록 재배치/회수 시 연결된 유닛 반환과 블록 변경을 함께 확정하고 그 후 이벤트를 한 번 통지합니다. 외부 시스템은 이벤트에서 완성된 배치 상태를 읽으며, 준비 중 임시 카드 표시는 IsUnitTemporarilyReturned로 조회합니다.

### StageManager 및 보상

IGridPreparation은 이제 GridRunSession이 구현합니다. GridManager의 단계 변경은 internal이며 화면·팀원 서비스가 직접 호출하지 않습니다. Session은 외부 실행 ID와 GridDefinition을 받아 자기 GridManager를 생성하므로 다른 실행의 배치 모델을 재사용하지 않습니다.

```csharp
var session = new GridRunSession(externalRunId, gridDefinition);
// 최초 콘텐츠 등록 후 준비를 허용합니다.
session.Grid.AddBlock(blockInstanceId, shape.Id, shape);
session.Grid.AddUnit(unitInstanceId, unitDefinition);
session.TryAllowPreparation(externalRunId, 1, canSkip: false);
session.TryBeginBattle(externalRunId, 1);
// 다음 일반 보상이 있는 라운드 승리만 전달합니다.
session.TryFinishBattle(externalRunId, 1, externalRewardRequestId);
session.TryChooseUnit(externalRunId, externalRewardRequestId, unitDefinition, shape);
// 또는 TryChooseExpansion(externalRunId, externalRewardRequestId).
// 증강 선택·저장 등 외부 작업을 마친 뒤에만 허용합니다.
session.TryAllowPreparation(externalRunId, 2, canSkip: true);
```

반환값이 false면 요청이 적용되지 않았으므로 준비 완료 Task나 지급 완료 기록을 성공 처리하지 않습니다. 예시는 호출 순서이며 실제 Stage 어댑터 코드는 아닙니다. 호출은 Unity 메인 스레드에서 직렬로 수행하고, Changed/LayoutChanged 구독자는 상태 조회에 사용합니다. 진행 명령 처리 중 재진입하는 진행 요청은 거절됩니다.

생성 직후 WAITING → 준비 허용 시 PREPARATION → 전투 시작 시 BATTLE → 일반 라운드 승리 시 REWARD → 보상을 받아도 REWARD 유지 → 외부 준비 허용 시 PREPARATION입니다. 확장은 보상 선택 시 필수 상태만 등록하며 다음 준비에서 배치합니다. 첫 준비의 skip은 외부가 true를 보내도 금지합니다. 후속 준비에서도 외부 canSkip=false이면 스킵을 허용하지 않습니다. 유닛 최소 1기·필수 확장 완료·드래그 없음 조건도 함께 적용합니다.

RunId는 외부에서 받고, 일반 보상 RequestId는 TryFinishBattle에 전달합니다. 해당 실행의 예상 라운드 번호, 실행 ID, 미처리 요청 ID를 확인합니다. 중복 완료·다른 실행·이전 라운드·소비한 보상 ID의 재사용은 거절합니다. 단독 테스트용 GUID 생성은 Prototype 코드에만 있습니다. 외부 소유자는 새 실행마다 고유 RunId를 발급하고 늦은 콜백에 원래 ID와 라운드를 전달해야 합니다. 현재 세션의 ID를 콜백 도착 시 새로 읽어 덮어쓰지 않습니다.

### 화면 연결과 종료

GridPrototypeRunner.Bind(session)은 더미 카탈로그 없이 동작합니다. 화면에 들어 있는 단독 검증 버튼을 사용할 때만 Bind(session, prototypeFlow)로 추가 공급합니다. Flow가 없으면 더미 전투 종료·보상 버튼은 숨겨집니다. 실제 준비 완료 Task와 UI 연결은 별도 Stage 어댑터 대상입니다.

Runner는 Host를 자동 생성하지 않습니다. JOB_KIMGUN에는 별도 GridPrototypeBootstrap을 구성했고, 이것이 GridPrototypeHost와 더미 공급자를 생성합니다. Host는 DontDestroyOnLoad로 유지하며 같은 준비 씬 재로드 시 아직 Session이 없는 화면에 다시 연결합니다. 이미 다른 Session에 연결된 화면을 덮어쓰지 않습니다. 실제 통합 화면에는 Bootstrap을 붙이지 않고 Stage 소유자가 Bind합니다.

Bind되지 않은 화면에는 연결 누락 메시지를 표시합니다. 활성 실행을 다른 실행으로 교체할 때는 먼저 Unbind합니다. 화면의 비활성화·파괴는 드래그와 구독만 정리하며 실행 자체를 종료하지 않습니다. 실행 종료 시 Session.Dispose()는 Grid를 ENDED로 잠그고 드래그를 취소하며 화면은 종료 표시로 바뀌고 구독과 입력을 해제합니다. 종료 후 콘텐츠 추가는 예외, 배치/진행 요청은 false로 거절됩니다. 읽기 전용 조회와 기존 스냅샷은 정산에 사용할 수 있습니다.

최종 승리·패배·취소에서는 TryFinishBattle로 다음 일반 보상을 만들지 않고 Dispose를 호출합니다. 단독 Host는 EndRun()으로 제거합니다. 새 스테이지는 새 세션입니다. 실제 StageManager 수정·전투 프리팹 연결·파일 저장은 이번 Grid 브랜치의 구현 범위가 아닙니다.

### 전투·시너지 배치 전달

Session.TryBeginBattle이 성공하면 Session.Deployment에 GridDeploymentSnapshot을 보관합니다. 배치된 유닛만 포함하며 유닛 인스턴스/콘텐츠 ID, 블록 인스턴스/모양 ID, 기준 칸, 회전, 점유 칸, 바닥과 별도 마왕 좌표를 복사합니다. 다음 준비에서 원본 배치가 바뀌어도 이전 스냅샷은 변하지 않습니다. 전투 중 체력·사망은 전투 담당이 별도로 관리하고 준비 배치를 제거하지 않습니다. 다음 라운드의 부활과 실제 프리팹 생성은 전투 담당 영역입니다.

GridWorldMapping(origin, right, up)의 origin은 셀 (0,0)의 중심이며 right/up은 한 칸에 해당하는 월드 벡터입니다. GetWorldPosition(anchor)로 XY 또는 XZ 전투판 위치를 구합니다. 화면 픽셀 좌표를 전투 좌표로 사용하지 않습니다. 실제 원점·셀 크기는 전투 씬 연결 시 결정합니다.

시너지 담당은 기존 LayoutChanged와 Units/Blocks를 읽어 배치된 개체만 계산합니다. UI에는 CanBeginBattle/CanSkipPreparation, GetPreviewFailure, RequiresExpansionPlacement를 제공하며 최종 피그마 레이아웃과 독립적으로 유지합니다.

이번 변경으로 기존 AddUnit(instanceId, contentId, footprint) 방식은 제거되었습니다. 블록과 유닛을 각각 등록해야 합니다. 실제 팀원 SO는 각각 FootprintDefinition과 UnitDefinition으로 변환하여 전달합니다. GridManager가 팀원 SO 타입이나 전투 프리팹 내부에 직접 의존하지 않습니다.

합성·시너지 효과·실제 캐릭터 생성·전투 거리 계산·영구 저장은 이번 범위가 아닙니다. 합성으로 유닛/블록을 교체하는 정책 및 API는 실제 규칙 확정 후 연결해야 합니다.

## 검증

- Edit: OZGL2/Grid/Verify Placement And Preparation → GridVerification.LastResult.
- 새 Play 세션: OZGL2/Grid/Verify Card Input (Play) → GridPlayVerification.LastResult.
- 별도의 새 Play 세션: OZGL2/Grid/Verify Session And Rewards (Play) → GridSessionPlayVerification.LastResult. 반복 지급·스크롤·준비 씬 재로드·필수 확장 상태를 확인합니다.
- Play 검사는 UI Toolkit 포인터/키 이벤트로 실제 콜백을 실행합니다. OS 입력 장치 자체의 수동 QA와 구분합니다.
- 검사 캡처는 Library/GridVerification에 저장하며 Git에서 제외합니다.

Notion은 읽기 전용이며 커밋·Push·병합을 수행하지 않았습니다.

