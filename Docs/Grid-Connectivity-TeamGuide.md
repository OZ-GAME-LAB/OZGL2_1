# 그리드 연결 배치 및 SPUM 표시 (#25)

## 적용 규칙
- 사용 영역: 초기 4×3, 1×2 보상 확장, 최대 8×5. 확장은 기존 바닥과 상하좌우 변을 공유해야 한다.
- 첫 발판은 사용 영역 안에 자유롭게 놓는다. 두 번째부터는 기존 발판과 변을 공유해야 한다. 대각선 접촉은 연결로 인정하지 않는다.
- 발판 이동·회수는 해당 발판을 제외한 나머지가 하나의 연결 영역이어야 한다. 이동 목적지 역시 기존 발판에 연결되어야 한다.
- 동일한 점유 칸으로 되놓거나 대칭 회전하는 것은 회수가 아니므로 허용하고, 올라간 유닛을 보관함으로 보내지 않는다.
- 빈 보드는 허용한다. 전투 시작에는 기존처럼 배치 유닛 1기 이상이 필요하다.
- 유닛끼리의 연결은 요구하지 않는다. 유닛 합성은 발판을 제거하지 않는다.
- 실제 발판 이동·회수 시 해당 발판을 사용하는 유닛도 보관함으로 돌아가는 기존 규칙을 유지한다. 합산 10칸 초과 시 기존 폐기 선택/취소 절차를 사용한다.
- 연결 검사 실패는 원본 배치·유닛·보관함을 바꾸지 않는다. 초기화/보상/취소에도 별도의 연결 우회 API는 없다.

## 팀원 연결 지점
- GridPlacementRules.ValidateBlock: 범위, 소유 바닥, 중복, 회수 후 연결성, 최종 연결성.
- GridManager.GetPreviewFailure / CommitPreview: 미리보기와 확정은 같은 규칙 사용.
- GridManager.GetTrayDropFailure / DropToTray: 회수 가능 여부 및 확정. DropToTray의 false는 규칙 실패 또는 보관함 공간 처리 대기를 의미하므로 PendingStorage도 확인한다.
- LastDropFailure: 최근 실패 원인. 새 드래그에서 초기화. 더미 UI는 DISCONNECTED 안내 문구를 표시한다.
- GridSurfaceLayout.GetBorders: 셀 좌하단을 원점으로 정규화한 외곽선 사각형 목록. 준비 UI와 전투 월드가 공유한다.
- GridBoardThemeSO: 표면 스프라이트/색/테두리 폭. 실제 UI 담당자는 게임 규칙을 복제하지 않고 위 API를 사용하면 된다.
- StageManager, 전투, 스킬, 직업 SO 및 보상 계약은 변경하지 않는다.

## 타일 표시
- InGame의 준비/전투 테마: Assets/_Project/Data/Grid/Prototype/SPUMConnectedTheme.asset
- 밝은 잔디 = 미확장, 어두운 돌바닥 = 사용 가능한 빈 영역, 어두운 보라색 돌바닥 = 발판.
- 바닥 셀의 간격을 제거하고 반복 가능한 중앙 타일을 사용한다.
- 인접 발판 사이 테두리를 없애고 외곽/ㄱ자 안쪽 모서리만 표시한다. 빈 소유 영역은 발판까지 포함한 소유 영역의 외곽을 표시한다.
- SPUM RuleTile 의존성 없이 공유 외곽선 계산으로 테두리를 보완한다. 표시 갱신은 최대 40칸 전체를 대상으로 하며 월드에서는 LayoutChanged 시에만 처리한다.
- 원본 텍스처, 슬라이싱, 외부 에셋 .meta는 변경하지 않는다.
- 원본 경로: Assets/98.ExternalAssets/00.LocalStaging/00.Packages/SPUM/Ultimate Resource Bundle/Res/Maps/BG/Tile01/TP_Tile01.png
- 참조 스프라이트: TP_Tile01_1 / TP_Tile01_144 / TP_Tile01_290.
- 출처는 팀 공유 SPUM Ultimate Resource Bundle이다. 외부 라이선스 재판정이나 원본 재배포는 이번 변경에 포함하지 않으며 원본은 기존 Git 제외/팀 공유 방식을 유지한다.
- 팀원도 동일한 원본과 .meta를 임포트해야 테마 참조가 유지된다.
- OZGL2 > InGame > Apply Connected SPUM Grid: 저장된 InGame 씬에 적용하는 에디터 도구. 기존 RF Castle 적용 메뉴와 테마는 보존한다.

## 검증
- OZGL2 > Grid > Verify Connectivity: 연결/분리, 취소, 동일 점유 회전, 회수와 보관함 초과 원자성, 확장, 외곽/안쪽 모서리.
- OZGL2 > Grid > Verify Connectivity (Empty Play Scene): 빈 임시 Play 씬에서 실제 포인터 우선 선택, 금지 회수 후 선택 해제, 빨강/초록 고스트, UI/월드 테두리 일치.
- Play 검증은 결과 화면을 남긴다. GridConnectivityPlayVerification.Cleanup() 호출 후 Play를 종료한다.
- 기존 GridVerification, GridFusionVerification, GridFusionPlayVerification, InGameVerification으로 보관함·합성·전투/보상 전환 회귀를 확인한다.

## 실행 결과 — 2026-09-16
Unity 6000.3.22f1에서 확인했다.

| 검증 | 결과 |
|---|---|
| GridConnectivityVerification | PASS — 대각선/분리 금지, 회수 원자성, 대칭 회전, 보관함 초과 취소/확정, 모서리 |
| GridVerification | PASS — 기존 배치/보관/보상, 확정본 보존, 14회 확장 |
| GridFusionVerification | PASS — 합성/성급/보관 용량/단계 제한 |
| InGameVerification | PASS — 전체 스테이지, 패배, 초기 자동 배치, 보상 초과/취소, 강제 확장, 재시도 |
| GridConnectivityPlayVerification | PASS — 실제 포인터 입력, 우선 선택, 금지 회수, 빨강/초록 고스트, 준비/월드 테두리 일치 |
| GridFusionPlayVerification | PASS — 실제 UI 합성, 실제 프리팹 성급/위치 반영, 원본 SO 보존 |

준비 화면과 월드 카메라 캡처에서 타일 간 틈 제거, ㄱ자 외곽/안쪽 모서리를 확인했다.
최종 InGame 씬은 저장된 편집 모드로 복귀했으며 Missing Script 0, SPUM 테마 연결 정상, Console 오류/경고 0이다.
신규 Assets 파일의 .meta 존재 및 외부 SPUM 원본 Git 제외를 확인했다.
독립 실행 빌드 및 팀원 전체 기능의 장시간 플레이 테스트는 이번 검증에 포함하지 않았다.
씬 저장 과정에서 이미 제거된 필드 InGameGridPresentation._synergy의 직렬화 잔여 항목도 Unity가 정리했다.
