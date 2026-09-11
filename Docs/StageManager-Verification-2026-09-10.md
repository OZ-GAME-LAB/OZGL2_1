# StageManager 경계 보강 검증 — 2026-09-10

브랜치: 4-feat-스테이지-매니저-1. Unity 6000.3.22f1, JOB_KIMGUN.

## 변경 범위

- PooledHero/HeroPool/PooledStageBattle: 사망 집계·연출 시작 예외 시 원래 lease로 반환을 시도하고 전투 Task에 실패를 전달합니다. 반환 실패도 함께 보존하며 동기 반환 후 재대여된 객체는 회수하지 않습니다.
- StageRunHost: 실행 콜백 내부의 ShutdownAsync 대기를 거절하고 RequestShutdownAfterRun으로 실행 완료 이후 정리를 예약합니다. 종료 Task를 먼저 게시해 중복 호출에 같은 Task를 반환합니다.
- StageContracts/StageManager: StageRunContext, StagePreparationRequest, IStagePreparation.EndRun을 통해 실행 ID·라운드·준비 종료 경계를 전달합니다. 실제 Grid 코드 의존성은 없습니다.
- 기존 검증 구현체와 ManualStageServices, README, 팀 공유 계약을 갱신했습니다. 새 경계 검증은 Assets/_Project/Scripts/Stage/Editor에 추가했습니다.

## 실행 결과

| 검사 | 결과 | 확인 사항 |
| --- | --- | --- |
| StageDecouplingVerification.StartChecks | PASS | 기존 30R/50R 흐름·저장 검증 포함, 정산-저장-로비 순서, 구독자 격리, 정산/저장/로비 실패 및 취소 경계 |
| StageBoundaryVerification.StartChecks (Play) | PASS | 실행/라운드 ID 일치, 일반 보상·증강·저장 전 다음 준비 차단, 최종 승리·패배·취소 시 준비 종료, 로비 자기 대기 거절, 지연 종료 및 로비 실패 후 자원 정리 |
| StageCleanupVerification.StartChecks (Play) | PASS | 기존 풀/Host 정리 검사, 연출 실패 시 즉시 반환 시도, 연출+반환 복합 오류 보존, 나머지 용사 회수, 반환 후 재대여에 대한 이전 오류 격리 |
| StageDecouplingVerification.StartPlayChecks | PASS | 지연 사망 연출, 조기/중복/이전 lease 완료 거절, 승리·패배·취소 시 연출 중 용사 회수 |

Unity 컴파일 후 실행했으며 Console 오류·경고 0건, JOB_KIMGUN Missing Script 0개입니다. 새 스크립트 .meta 누락이 없고 git diff --check가 통과했습니다. 검증 후 Play를 종료하고 runInBackground를 원래 false로 복원했습니다.

## 한계 및 후속 연결

실제 Grid 브랜치는 병합하지 않았습니다. Grid 준비 어댑터, 팀원 SO, 실제 전투/로비 UI와 이동은 별도 통합 검증이 필요합니다. 사망 연출 검증은 BeginDeath 호출 중 발생하는 동기 예외이며, 팀원의 비동기 연출 내부 예외 처리는 해당 구현에서도 필요합니다.

씬·프리팹·패키지·Unity 버전 변경, 커밋·Push·병합, Notion 수정은 하지 않았습니다. 변경된 IStagePreparation/IStageSession 시그니처는 팀 공유 문서의 최신 계약으로 연결해야 합니다.
