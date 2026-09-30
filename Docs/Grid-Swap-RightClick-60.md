# 유닛 교환·우클릭 회수 (#60 후속)

## 조작

- 준비 단계에서 배치된 유닛을 다른 배치 유닛의 점유 칸에 맞춰 드롭하면 위치를 교환한다.
- 두 유닛의 현재 성급·회전·반전을 적용한 점유 모양이 같아야 한다. 칸 수만 같은 세로 2칸과 가로 2칸은 교환하지 않는다. 드래그 중 회전으로 기존 모양이 다른 두 유닛을 교환하지 않는다.
- 양쪽 이동 후의 발판·경계·다른 유닛 점유를 모두 검사하여 유효할 때만 한 번에 확정한다. 미리보기에서는 양쪽 목적지를 초록색으로 표시하고 Swap 안내를 제공한다.
- 합성 가능한 조합은 합성을 우선한다. 보관함 유닛은 교환 대상이 아니다.
- 준비 중 유닛의 어느 점유 칸에서든 우클릭하면 유닛만 보관함으로 회수한다. 발판과 성급·회전·반전은 유지한다.
- 보관함이 가득 차면 기존 폐기 선택을 사용한다. 취소 시 원래 배치를 유지하고 확정 시 회수한다. 드래그·폐기 선택·전투 중에는 우클릭 회수를 차단한다.

## 연결

- GridManager의 CanSwapPreview/GetSwapReturnCells: 교환 가능 여부와 상대 유닛의 이동 후 점유 칸. CommitPreview가 합성을 먼저 처리하고 교환을 원자적으로 반영한다.
- TryReturnUnitToTray(id): 준비 상태의 배치 유닛 회수. false라도 PendingStorage가 있으면 폐기 선택 대기 상태이며, 기존 GridRunSession 확인/취소 API를 사용한다.
- 교환은 LayoutChanged 한 번으로 두 유닛을 통지한다. 유닛 수·보관 수·발판은 바뀌지 않는다. 전투 확정본은 교환된 위치를 캡처한다.
- GridDragInput, GridBoardView, GridWorldPreparationView, GridPrototypeRunner에 입력/표시를 연결했다. 씬·프리팹·콘텐츠 SO는 이 후속 작업에서 수정하지 않았다.

## 검증

- GridSwapReturnVerification: 실제 UI Toolkit 포인터를 통한 교환·우클릭, 취소, 한 번의 배치 알림, 동일 칸 수/다른 모양 거부, 보관 유닛 거부, 비기준 점유 칸 우클릭, 드래그 중 회수 차단, 보관함 10개 폐기 취소/확정, 전투 입력 잠금, 합성 우선순위를 검사한다.
- 2성 유닛 교환 후 성급·반전 유지 및 전투 확정본 좌표를 검사한다.
- 기존 GridVerification, GridConnectivityVerification, GridPlacementQAVerification, GridFusionVerification 회귀 검사를 수행한다.
- 실행 빌드와 실제 전체 라운드 완주는 이번 후속 검증 범위에 포함하지 않는다.
