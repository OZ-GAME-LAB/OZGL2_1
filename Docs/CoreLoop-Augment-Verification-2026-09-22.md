# 증강 연결 검증 (2026-09-22)

브랜치: feat/core-loop-augment/47. Unity 6000.3.22f1.

## 통과

- InGameAugmentVerification: 기존 단일 등급 API 유지, 혼합 등급/0 가중치 제외, 중복 없는 후보, 선택 대기, 중복 요청/클릭 및 후보 외 선택 차단, 적용 1회 및 배율 반영, 취소, 오래된 실행 차단, 후보 없음 오류, 적용 예외, 새 실행 초기화.
- InGameAugmentPlayVerification: 실제 RealSynergySync/AugmentRun/InGameAugmentRewards와 StageManager 연결. 일반 보상 → 증강 → 준비 → 다음 전투, 최종 라운드 증강 생략, 실행 초기화, 제공자 비활성화/파괴 시 대기 취소. 전투 결과는 ManualStageServices로 주입했다.
- 기존 InGameVerification: 더미 전투 기반 전체 흐름, 패배, 초기 배치, 보관함 초과/취소/버리기, 확장 강제 배치, 취소 및 재시작.
- 기존 InGameSkillLifecycleVerification: 단계별 입력 차단, 지연 효과/장판 정리, 재진입과 이전 실행 소유권.
- 저장된 Builds/InGame: Missing Script 0, 끊어진 직렬화 참조 0, 실제 증강 제공자 연결 확인.
- Builds/InGame 직접 Play: COMBAT 진입, UsesDummyAugments=false, Bootstrap.Error=null.
- 임시 Play 오브젝트의 더미 증강창: 실제 SO 후보 3개와 한국어 설명 표시를 Game View 캡처로 확인. 캡처는 Temp/AugmentSelection.png에만 저장.
- 최종 Console 조회: 오류/경고 0. 컴파일 중 MCP 재연결 경고가 한 번 있었으나 마지막 검사에는 남아 있지 않음.
- 신규 스크립트의 Unity 생성 .meta 확인. 코드·문서 diff 공백 검사 통과. 씬 변경은 Unity가 생성한 빈 필드 공백을 유지.

최초 통합 테스트는 테스트용 수동 로비 완료 신호가 없어 타임아웃이 발생했다. 테스트 로비를 즉시 완료되는 구현으로 교체한 뒤 재실행하여 통과했다. 제품 로직의 타임아웃은 아니었다.

## 범위 및 제한

- 전체 30/50라운드 실제 전투 완주, Player 빌드, 최종 UI 마우스 클릭 검증은 미실시.
- 증강 효과 30종 전체의 전투 결과/밸런스 검증은 미실시. 코드상 실제 연결이 확인된 후보 17개만 제공하며 미연결 13개는 팀 가이드에 기록.
- 테스트의 선택 입력은 제공자 API 호출로 수행. 화면 캡처는 배치·한국어 표시 확인용.
- 검증 후 Edit Mode의 Builds/StageChoice로 복귀, 씬 dirty=false, runInBackground=false. 임시 Play 오브젝트는 제거됨.
- 커밋/Push/PR 생성은 수행하지 않음.
