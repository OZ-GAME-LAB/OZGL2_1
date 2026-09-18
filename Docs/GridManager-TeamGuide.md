# GridManager 개발·연결 명세 — 칸 점유와 공용 보관함 (2026-09-11)

> 2026-09-15 변경: 프로토타입 유닛 점유는 모두 1×1이며, 6종 발판은 유지한다. 합성 및 실제 바닥 에셋 연결은 [최신 연결 명세](Grid-Asset-Fusion-TeamGuide.md)를 따른다. 아래 다칸 점유 설명은 기반 엔진의 지원 범위이며 현재 프로토타입 유닛 데이터에는 적용하지 않는다.

## 실행

작업 브랜치: feat/grid-manager/14. 테스트 씬: Assets/00.Scenes/JOB_KIMGUN.unity.
초기 지급은 기본 유닛 1기 + 1칸 발판 1개이다. 6종 모양과 유닛 데이터는 카탈로그에 유지하며 보상 후보는 더미 순환 방식이다.

## 배치 규칙

- 초기 바닥 4×3, 최대 8×5. 바닥 좌표 x=2..5/y=0..2, 최대 x=0..7/y=0..4.
- 마왕 중심 (3.5,-1)은 별도 위치이며 일반 배치 수에 포함하지 않는다.
- 발판의 모든 칸은 바닥 안에 있어야 하며 다른 발판과 겹칠 수 없다.
- 유닛의 모든 점유 칸은 배치된 발판으로 받쳐져야 하며 다른 유닛과 겹칠 수 없다.
- 발판당 유닛 수 제한과 요구 발판 모양 일치 제한은 없다. 한 발판에 여러 유닛, 여러 발판에 걸친 한 유닛을 허용한다.
- 블록/유닛 점유 모양은 FootprintDefinition이다. 상대 (0,0)이 기준이며 R은 (x,y)→(y,-x) 시계 방향 회전이다.

## 조작

1. 공용 STORAGE 보관함에서 발판을 바닥으로 드래그한다.
2. 유닛을 발판으로 덮인 영역에 드래그한다. 유닛 자신의 점유 칸 전체를 미리보기로 표시한다.
3. 유닛/발판/확장 조각 모두 R 회전, Esc 취소. 초록 고스트는 배치 가능, 빨강은 불가능이다.
4. 클릭한 칸에 유닛이 있으면 항상 유닛 우선 선택한다. 큰 유닛은 모든 점유 칸에서 집을 수 있다. Shift로 발판 우선 선택하는 구버전 동작은 제거했다.
5. 유닛 없는 발판 칸을 잡으면 발판을 드래그한다. 바닥만 있는 칸은 드래그하지 않는다.
6. 유닛과 발판 모두 STORAGE 영역에 드롭하여 회수한다. 그 외 배치 불가 위치는 원상 복원한다.

유닛 회수는 발판을 유지한다. 발판을 실제로 이동/회전/회수하면 그 발판과 점유 칸이 겹치는 모든 유닛을 반환한다. 여러 발판에 걸친 유닛도 전체 반환한다. 실제 이동에서는 발판을 계속 배치하고, 회수에서는 발판도 반환한다. 제자리 드롭/4회 회전 원상 복귀/무효 드롭/취소는 유닛을 반환하지 않는다.

## 공용 보관함과 공간 확보

미배치 유닛과 발판은 각각 1개이며 합계 한도는 GridSettingsSO.StorageCapacity 설정(프로토타입 10)이다. 배치된 항목과 필수 바닥 확장 조각은 보관 개수에서 제외한다.

- 유닛 보상은 후보 A / 후보 B / 바닥 확장 중 하나를 선택한다. 유닛 선택은 유닛 1기와 RewardBlockId의 발판 1개를 함께 지급하므로 2칸을 사용한다.
- RewardBlockId는 보상 구성용이다. 해당 발판에서만 배치할 수 있다는 뜻이 아니다. 유닛의 Footprint와 별도로 설정한다.
- 회수/발판 이동으로 반환되는 유닛 또는 보상 지급이 한도를 초과하면 Grid.PendingStorage에 요청을 보존한다. 원래 배치와 보관함은 아직 변경되지 않는다.
- 화면은 들어올 항목과 부족한 개수를 표시한다. 기존 보관 항목에서 정확히 필요한 수만 선택하여 확정한다. 새로 받을 항목이나 배치 중인 항목은 폐기 대상으로 선택하지 않는다.
- 폐기와 지급/회수를 한 번에 적용한다. 유닛과 발판은 개별 삭제하며 부분 보상 지급은 없다.
- 취소 시 원래 상태를 유지한다. 보상 취소는 미수령 상태로 돌아가며 다음 준비 단계로 넘어가지 않는다.
- 대기 중 드래그, 직접 추가, 다른 보상, 전투 시작/스킵을 막는다. 장면 재생성은 대기를 취소하지 않는다. 실행 종료는 대기를 제거하고 ENDED로 잠근다.
- 저장은 메모리 내 실행 상태다. 앱 재시작을 가로지르는 저장/복원은 구현 범위에 포함하지 않는다.

## 데이터 및 API 변경

UnitDefinition(id, displayName, footprint, rewardBlockId)는 유닛 점유 모양과 동봉 보상 발판을 분리한다. GridUnitDataSO._footprint에 모양 SO를 지정한다. 구 _requiredBlockId는 _rewardBlockId로 직렬화 이름을 이전한다. 기존 프로토타입 SO GUID는 유지한다.

UnitPlacement는 IsPlaced, Anchor, Rotation, GetCells()를 제공한다. 단일 BlockId와 WithBlock은 제거했다. GridManager.GetUnitAt(cell)로 유닛을 조회하고 GetUnitsOnBlock(blockId)로 해당 발판에 걸친 전체 유닛을 조회한다. GetUnitOnBlock, PreviewTargetBlock은 더 이상 사용하지 않는다.

GridManager.GetStoredItems()는 Kind(UNIT/BLOCK), InstanceId, DisplayName을 반환한다. 동일 ID 문자열도 종류와 함께 식별한다. StoredCount, Definition.StorageCapacity, PendingStorage를 표시용으로 사용한다.

```csharp
// runId는 원래 요청의 실행 ID를 전달한다.
bool granted = session.TryChooseUnit(runId, rewardId, unitDefinition, rewardBlock);
// false + PendingStorage != null: 실패가 아니라 공간 확보 대기일 수 있다.
var request = session.Grid.PendingStorage;
// 사용자 확정 시: 현재 화면 요청의 RequestId와 선택 항목을 전달한다.
bool applied = session.TryConfirmStorage(runId, request.RequestId, selectedItems);
// 취소 시:
bool cancelled = session.TryCancelStorage(runId, request.RequestId);
```

TryChooseUnit이 false라면 먼저 PendingStorage를 확인한다. 보상 대기 중에는 PendingRewardId가 유지된다. 확정 시 실제 지급과 PendingRewardId 해제가 완료된 뒤 Changed/LayoutChanged를 통지한다. 중복/이전 RequestId, 다른 실행 ID, 중복 폐기 항목, 후보 외 항목, 부족/초과 선택은 false로 거절한다.

Changed는 미리보기/대기 등 화면 상태, LayoutChanged는 실제 배치/지급/회수/폐기 확정을 통지한다. 이벤트는 읽기 전용 관찰용이다. 콜백 내 재진입 변경은 거절하며 관찰자 예외는 LastObserverError에 기록하고 다른 관찰자에 통지한다. 다음 단계 호출은 현재 API 반환 이후에 진행한다.

초기 AddBlock/AddUnit은 즉시 등록 API로 한도 초과 시 예외를 반환한다. 대기 중 또는 드래그 중에는 사용할 수 없다. 일반 보상은 GridRunSession.TryChooseUnit을 통해 처리한다.

## 진행 및 외부 시스템 연결

- GridRunSession(runId, definition)이 실행과 보상 요청을 소유한다. GridManager는 외부 전투/SO 타입에 직접 의존하지 않는다.
- 시작 WAITING → TryAllowPreparation(runId, 1, false) → PREPARATION.
- TryBeginBattle(runId, round, skip)는 유닛 최소 1기, 드래그/보관 대기 없음, 필수 확장 완료를 확인한다. 첫 준비 스킵은 불가하다.
- TryFinishBattle(runId, round, rewardRequestId) → REWARD. 보상 ID 중복/이전 라운드/실행 불일치를 거절한다.
- 보상 지급만으로 준비가 자동으로 열리지 않는다. 보상 및 증강/저장 처리 후 외부 소유자가 TryAllowPreparation을 호출한다.
- 공간 확보 확정 후에도 같은 규칙이다. 단독 GridPrototypeFlow만 테스트 편의상 지급 완료 뒤 준비를 연다.
- 확장 선택은 다음 준비에서 1×2 바닥 조각 배치를 강제한다. 변 연결/경계/중복을 검사하고 CanExpand=false이면 확장 후보를 제외한다. 빈틈없이 14회 배치하면 40칸이다.
- 최종 승리/패배/취소는 다음 보상을 생성하지 않고 Session.Dispose().

GridPrototypeRunner.Bind(session)는 더미 공급자 없이 연결 가능하다. Bind(session, prototypeFlow)는 단독 검증 버튼을 추가한다. 활성 세션 교체 전 Unbind가 필요하다. Bootstrap/Host는 JOB_KIMGUN에서만 세션을 만들며 같은 씬 재진입에 기존 세션을 주입한다. 실제 Stage 연결은 별도 어댑터 작업이다.

## 전투 배치 전달

성공한 TryBeginBattle 이후 Session.Deployment는 불변 스냅샷이다. 유닛마다 InstanceId, ContentId, ShapeId, Anchor, Rotation, Cells를 복사한다. 단일 BlockId는 제거되었고 기준 좌표와 점유 모양은 유닛 자신의 값이다. 바닥 칸과 별도 마왕 위치도 유지한다.

GridWorldMapping(origin, right, up)은 셀 좌표를 XY/XZ 월드로 바꾼다. 전투 중 체력/사망은 이 스냅샷을 수정하지 않는다. 실제 캐릭터 스폰, 합성/시너지 효과, 영구 저장, 팀원 Unit/Fusion 코드 연결은 이번 작업에서 변경하지 않았다.

## 검증 메뉴

- Edit: OZGL2/Grid/Verify Placement And Preparation → GridVerification.LastResult.
- 새 Play: OZGL2/Grid/Verify Card Input (Play) → GridPlayVerification.LastResult.
- 별도 새 Play: OZGL2/Grid/Verify Session And Rewards (Play) → GridSessionPlayVerification.LastResult.
- Play 검사는 실제 UI Toolkit 이벤트를 통해 입력 콜백을 실행한다. OS 마우스 자체 수동 QA와는 구분한다.
