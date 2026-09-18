# StageManager 개발 명세

갱신: 2026-09-09 · Unity 6000.3.22f1

팀 공유용 전체 인터페이스와 연결 방법은 [StageManager-TeamGuide.md](../../../Docs/StageManager-TeamGuide.md)를 기준으로 합니다.

## 책임

- StageManager: 단계 진행과 완료 대기. 전투/보상/준비/정산/저장/로비 구현체 주입.
- StageRunProgress: 도전별 RunId, 라운드 결과, 누적 처치 경험치, 보상 수령 상태.
- FileStageProgressStore: JSON 쓰기·읽기, 임시 파일 Flush 후 교체, 저장 실패 전달.
- RoundCompletionEvaluator: 예정 스폰 완료와 양측 생존 수 판정.
- HeroPool / PooledHero: GameObject 재사용, 대여 ID 검증, 초기화/반환 훅.
- PooledStageBattle: 스폰 일정, 처치 경험치, 생존 수, 전투 종료 시 반환.
- StageRunHost: 화면과 독립된 실행 수명. 정적 Singleton 없음.
- Prototype: 디스크에 기록하는 더미 서비스와 수동/풀링 테스트 화면.
- Editor: 기존 흐름·저장·풀링·씬 수명 검사 및 더미 에셋 설정.

## 진행

초기화 → 첫 준비(스킵 불가) → 전투.
승리 직후 결과 저장 → 최종이 아니면 일반 보상 및 필요 시 증강 → 각각 적용 상태 저장 → 다음 준비.
패배/최종 승리는 결과 저장 → 정산 지급·저장 요청 → 정산 완료 기록 저장 → 로비 복귀 요청.
기존 보통 30R / 어려움 50R / 10R 보스 더미 구조, 최종 일반 보상·증강 생략을 유지합니다.

실패 라운드에서 얻은 경험치도 정산 원자료에 포함합니다.
저장 실패 시 다음 단계로 진행하지 않습니다. 중단 상태 저장 실패는 PersistenceError로 노출합니다.
재도전은 새 RunId, 1R부터이며 기록 읽기는 자동 이어하기가 아닙니다.
보상 담당자는 요청 ID와 실제 지급 결과를 함께 저장해야 합니다.
더미 영수증이 실제 인벤토리의 지급 트랜잭션을 대신하지는 않습니다.

## 데이터와 에셋

StageDataSO → CreateSnapshot() → 읽기 전용 StageDefinition을 사용합니다.
실제 SO가 다른 타입이면 IStageDataSource에서 변환합니다.
공격력/체력 배율 등 외부 전투 수치는 데이터·전투 담당자와 추가 연결이 필요합니다.
HeroPoolCatalogSO의 용사 ID와 프리팹, 초기 수량/증가량/경험치는 Inspector에서 설정합니다.
기존 데이터/스크립트 폴더는 이동하지 않았으며 신규 테스트 프리팹은 Assets/_Project/Prefabs/Stage에 둡니다.
SO 원본과 런타임 기록은 분리됩니다.

## 검증 실행

- Edit: OZGL2/Stage/Verify Dummy Flow
- Edit: OZGL2/Stage/Verify Progress And Persistence
- Play: OZGL2/Stage/Verify Pool And Scene Lifetime (Play)
- Play JOB_KIMGUN_STAGE: StageFlowVerification.StartSceneChecks()
- 결과: 각 검사 클래스 LastResult / LastPlayResult.
- WarmPoolAllocatedBytes: 1,000회 대여·반환 구간의 관리 힙 할당량. 전체 게임 성능 수치가 아닙니다.

JOB_KIMGUN_STAGE 테스트 진행 파일은 Application.persistentDataPath/stage_prototype에 저장됩니다.
자동 검사의 임시 저장은 Library/StageVerification 아래에 남으며 Git 관리 대상이 아닙니다.

## 통합 시 남은 사항

실제 이동/공격, 실제 부활 상태, 실제 기물/블록/증강 지급, 계정 성장 및 실제 씬 UI는 팀원 연결 대상입니다.
준비/전투/로비 씬 로딩 자체는 더미 화면에 구현하지 않았습니다.
Host의 임시 씬 제거 생존 검사는 실제 게임 씬 통합 검사와 구분합니다.
동시 전멸은 정책 미정 오류로 중단합니다. 증강 확률도 미확정입니다.
실제 지급 중복 방지, 취소 시 구독/비동기 해제는 각 구현체가 계약을 준수해야 합니다.

## 디커플링 보강 (2026-09-09)

- UI StateChanged는 구독자별로 동기 호출합니다. 한 구독자의 예외가 다른 구독자나 게임 진행을 중단하지 않습니다.
- NotificationErrorCount / LastNotificationError / LastFailedSubscriber에 횟수와 마지막 오류를 기록하며 새 도전에서 초기화합니다.
- 이는 동기 이벤트 오류 보호입니다. async void 구독자는 사용하지 않고, 비동기 UI 작업은 구독자가 자신의 예외와 수명을 관리해야 합니다.
- 새 종료 상태: SETTLING → RETURNING_TO_LOBBY → CLEARED 또는 FAILED.
- IStageSession.FinishAsync는 SettleAsync로 변경됐으며 로비 이동 책임은 IStageLobby로 분리됐습니다.
- 사망 연출 완료 대기 중에도 이미 죽은 용사는 승패의 생존 수에 포함하지 않습니다.
- 검사 메뉴: OZGL2/Stage/Verify Decoupled Flow, OZGL2/Stage/Verify Delayed Hero Return (Play).
- 검사 결과: StageDecouplingVerification.LastResult / LastPlayResult.
- 실제 SO와 풀의 경험치 데이터 조회 분리는 금요일 데이터 병합 후 연결 대상입니다. 투사체 풀은 이번 변경에 포함하지 않습니다.

## 정리 오류 처리 보강 (2026-09-09)

- `PooledHero`는 반환 초기화 컴포넌트 하나가 실패해도 나머지 훅과 비활성화를 시도합니다.
- `HeroPool`은 생성/반환 초기화에 실패한 용사를 재사용 목록에서 제외하고 파괴합니다. 정상 사망은 기존처럼 풀에 반환합니다.
- `PooledStageBattle`은 모든 대여 객체의 회수를 시도한 뒤 오류를 `AggregateException`으로 보고합니다. 정리 중 오류와 원래 실행 오류·취소 원인을 함께 보존합니다.
- 사망 연출 반환 콜백의 정리 오류는 `CleanupError`에 보관하고 전투 Task에서 관찰합니다. 이 경우 정상 라운드 결과를 반환하지 않습니다.
- `StageRunHost.ShutdownAsync()`는 실행 취소·종료 후 모든 소유 자원의 폐기와 취소 토큰 해제를 시도합니다. 반복 호출은 같은 Task를 반환하며, 정리 오류는 `CleanupError`, 정리 완료 여부는 `IsShutdownComplete`로 확인합니다.
- 명시적으로 Host를 종료할 때는 `ShutdownAsync()`를 await하고 예외를 처리한 뒤 GameObject를 제거합니다. Unity는 `async OnDestroy` 완료를 기다리지 않습니다.
- 검사 메뉴: `OZGL2/Stage/Verify Cleanup Failures (Play)`. 결과: `StageCleanupVerification.LastResult`.

## 연결 계약 보강 (2026-09-10)

초기화는 StageRunContext, 준비는 StagePreparationRequest를 받습니다. Stage가 발급한 실행/라운드 ID를 공유하고, 보상·증강 저장 후에만 다음 준비를 요청합니다. IStagePreparation.EndRun은 최종 종료·취소·오류 시 배치 입력과 대기를 정리합니다.

사망 연출 시작 오류에도 원래 대여의 반환을 시도하고 전투 Task로 오류를 전달합니다. 로비 콜백 내부에서는 ShutdownAsync를 기다릴 수 없으며 RequestShutdownAfterRun으로 정상 종료 후 정리를 예약합니다. 외부 소유자는 ShutdownAsync로 정리 완료를 기다린 뒤 Host를 제거합니다.

자세한 시그니처와 호출 순서는 Docs/StageManager-TeamGuide.md를 참고합니다. Grid와 실제 팀원 SO/전투/로비 구현의 병합은 별도 작업입니다.
