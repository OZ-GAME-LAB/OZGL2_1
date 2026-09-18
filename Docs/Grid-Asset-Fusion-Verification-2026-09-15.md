# RF Castle / 합성 검증 결과 — 2026-09-15

브랜치: `feat/grid-asset-fusion/23`. Unity: `6000.3.22f1`.

## 변경 파일 영역

- InGame.unity: 준비 화면 테마와 InGameGridPresentation 연결.
- GridContracts / GridDeploymentSnapshot: 실행 인스턴스의 성급 보존.
- GridManager / GridRunSession: 외부 합성 판정 주입, 합성 확정 및 점유 반환.
- GridBoardThemeSO / GridBoardView / GridWorldBoardView: 세 가지 바닥 상태와 실제 스프라이트 표시.
- GridDragInput / GridPrototypeRunner: 그리드 및 보관함 카드 드롭 합성, 성급 표시.
- GridFusionPolicy / InGameGridPresentation: 팀 합성 규칙과 직업별 시너지 연결.
- RealDefenders / RealStageBattleFactory / InGamePrototypeBootstrap: 전투 배치 생성·이동·제거·성급 및 실행 수명 정리.
- 더미 GridUnitDataSO 5개 및 데이터 생성 도구: 1×1 점유로 변경. 실제 마왕군 배치 SO는 이미 1×1이었다.
- Editor 검증 코드와 공유 문서 추가/갱신.

## 수행 결과

| 검증 | 결과 |
|---|---|
| Unity 컴파일 및 최종 Console 오류 | 통과, 오류 0건 |
| GridFusionVerification | 통과 |
| 기존 GridVerification | 통과 |
| 기존 InGameVerification | 통과 |
| GridFusionPlayVerification | 통과 |
| 기존 GridPlayVerification | 통과 |
| 저장된 InGame의 Missing Script | 0개 |
| InGame 설정 Validate | 통과 |

실제 포인터 이벤트로 그리드 합성, 보관함 합성, 보관함→그리드 합성, 초록 미리보기, 잘못된 드롭 복원을 검증했다. 실제 마왕군 프리팹을 두 번의 배치 확정본으로 동기화하여 소모된 유닛 제거, 기존 유닛 위치 이동, 3성 반영 및 원본 스탯 SO 보존을 확인했다.

기존 검증으로 회전, 보관함 복귀, 확장 강제 배치, 전투 입력 잠금, 14회 확장, 보관함 초과/폐기 취소, 정상 종료·패배·취소·재시작 흐름을 확인했다.

테스트 중 발견한 합성 미리보기 오표시를 수정했다. 알림 중 읽기 판정은 허용하고 재진입 상태 변경만 차단한다. 카드 레이아웃 갱신 전 드롭 대상을 확보하도록 처리했다.

## 검증 환경과 한계

- Play 테스트는 저장하지 않는 빈 임시 씬에서 진행했다. 용사를 생성하거나 처치하지 않았다.
- InGameVerification은 더미 전투 서비스로 코어루프를 검증하며, 실제 전투 전체를 끝까지 플레이한 결과는 아니다.
- 실제 마왕군과 RF Castle 스프라이트를 사용한 어댑터/표시 검증은 별도로 수행했다.
- 팀원의 스킬·전투 밸런스 전체와 최종 UI 연결은 이번 검증 범위 밖이다.
- 외부 RF Castle 파일과 메타는 Git ignore 대상임을 확인했다.
- 최종 에디터는 Play 종료 후 저장된 InGame 씬으로 복구했다.
- 커밋·Push·병합은 수행하지 않았다.
- `git diff --check`에는 Unity가 저장한 씬 YAML의 빈 값 뒤 공백 3건이 표시된다. 씬을 외부 편집기로 고치지 않고 Unity 직렬화 결과를 유지했다. 코드·문서에는 공백 오류가 없다.
