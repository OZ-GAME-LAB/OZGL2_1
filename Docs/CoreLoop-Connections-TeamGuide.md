# 코어루프 외부 연결 규격

## 범위와 현재 연결 상태

SO 직업, 전투 배율/회복 계산, 스킬 효과, 투사체 내부 코드는 변경하지 않는다.

| 연결 | 상태 |
|---|---|
| 배치/회수/합성 → 시너지 집계 | `InGameSynergyConnection`에서 기존 `RealSynergySync.SetCount` 호출 |
| 증강 초기화 | 실행 시작/종료 시 기존 `RealSynergySync.Augments.ResetRun` 호출. 라운드 사이에는 유지 |
| 실제 증강 선택 | Bootstrap의 Augment Provider가 비어 있으면 기존 더미. 실제 선택 UI는 미연결 |
| 전투 사용 차단/효과 정리/마왕 위치 | 코어루프 호출 규격 구현. 팀원 어댑터가 아직 없으므로 실제 스킬·투사체는 미연결 |
| 동시 전멸 | 패배 반환 후 기존 저장/정산/로비 흐름 사용. 진단 Outcome은 SIMULTANEOUS 유지 |

Bootstrap의 `HasCombatParticipants`, `HasSynergyConnection`, `UsesDummyAugments`로 조립 상태를 확인한다. 참여자 존재는 모든 팀 기능의 연결 완료를 뜻하지 않는다.

## 스킬·투사체 담당자가 제공할 어댑터

`IInGameCombatParticipant`를 구현한 MonoBehaviour를 Bootstrap의 Combat Participants에 등록한다. 코어루프는 팀원의 private 필드나 오브젝트 이름을 찾아 변경하지 않는다.

- `SetCombatEnabled(bool)`: 키보드뿐 아니라 UI와 직접 시전 경로도 해당 시스템에서 차단한다. false는 준비/보상/종료 동안 유지해야 한다.
- `PrepareAsync(InGameCombatContext, CancellationToken)`: RunId, RoundNumber, KingPosition을 전달한다. KingPosition은 확정 배치와 동일한 GridWorldMapping으로 계산한 이번 라운드 위치다. 초기 위치 사본에 의존하지 말고 시스템의 발사 기준을 갱신한다.
- `CleanupAsync(InGameCombatContext)`: 해당 전투의 지연 공격/장판/투사체를 취소·반환하고 완료 후 반환한다. 같은 실행의 반복 호출과 일부만 준비된 상태에서도 안전해야 한다. 이미 취소된 전투 토큰을 정리 작업에 사용하지 않는다.

호출 순서:

1. 실행 초기화 시 사용 차단.
2. 실제 마왕군 배치 완료.
3. 모든 참여자 PrepareAsync 완료.
4. 모든 참여자 사용 허용 후 용사 스폰 시작.
5. 승패/취소/오류 발생 시 사용 차단.
6. 모든 참여자 CleanupAsync 시도 및 완료 대기.
7. 용사 풀 반환 후 보상/종료 단계 진행.

준비 실패 시 전투를 시작하지 않고 정리를 시도한다. 정리는 모든 참여자에게 먼저 요청한 뒤 전체 완료를 기다린다. 한 참여자의 지연이 나머지 정리 요청을 막지 않으며, 모두 완료한 뒤 오류를 수집하고 풀을 반환한다.

설정의 External Operation Timeout Seconds(기본 10초)가 지나면 준비 작업에는 취소를 요청하고, 화면에 TIMEOUT과 작업 이름을 표시한다. 정리 작업은 취소로 버리지 않는다. 늦은 작업이 실제 완료될 때까지 전투/풀 재사용/재시작을 차단하며, 이후에도 시간 초과 오류로 종료한다. 취소를 무시하거나 동기 호출 자체를 멈추는 팀원 코드는 코어루프가 강제 종료할 수 없으므로 수정이 필요하다.

정리 실패 전에 승패가 확정됐다면 `StageBattleCleanupException.ConfirmedResult`로 결과를 전달한다. StageManager는 클리어 수·경험치·라운드 결과를 저장하고 ERROR로 멈춘다. 일반 보상, 정산, 다음 전투와 로비 이동은 자동 진행하지 않는다. 디스크 저장 자체가 실패하면 PersistenceError로 남는다.

이 연결은 `StateChanged` 표시 이벤트에 의존하지 않는다. 기존 스킬 입력/효과/투사체는 어댑터를 등록하기 전까지 차단·정리되지 않는다.

## 증강 담당자

Bootstrap의 Augment Provider는 `IStageRewards` 구현체다. 여기서는 SelectAugmentAsync만 호출한다. 실제 선택·적용 완료 후 Task를 완료하고, 동일 RequestId의 중복 적용을 방지해야 한다. 취소는 Task에 전달한다. StageManager는 성공 반환과 취소 확인 후 지급 완료를 저장한다.

현재 초기화 대상은 `RealSynergySync.Augments`다. 다른 증강 저장 객체를 사용한다면 초기화 연결도 함께 합의해야 한다. 계정 특성/영구 성장 값은 초기화하지 않는다.

## 표시와 데이터

`InGameGridPresentation`은 월드 타일 표시만 담당한다. Bootstrap이 `InGameSynergyConnection`을 소유하며 배치된 유닛만 집계한다. 표시 컴포넌트나 UI 교체가 시너지 구독을 끊지 않는다.

SO의 job 값을 그대로 사용한다. 직업 값 오류는 콘텐츠 담당자가 수정하며, 코어루프는 ID별 직업을 하드코딩하지 않는다.

## 검증 메뉴

- `OZGL2/InGame/Verify Core Connections (Empty Play Scene)`: 빈 임시 씬 Play 모드에서 실행. 연결 순서, 실패/취소 정리, 동시 전멸, 시너지 분리 검사.
- `OZGL2/InGame/Verify Integration`: 기존 더미 전투 기반 전체 코어루프 회귀 검사.
- `OZGL2/InGame/Verify Grid Fusion Rules`: 기존 합성 규칙 회귀 검사.

팀원 어댑터를 연결한 뒤 실제 입력 차단, 장판/투사체 잔존, 스킬 발사 위치를 별도로 플레이 테스트해야 한다.

## 실패 대응과 개발 화면

- Bootstrap 표시 이벤트는 구독자별로 예외를 기록하고 다음 구독자에게 전달한다. 필수 전투 연결 예외는 계속 실행을 차단한다.
- CanRetry는 Stage 생성 전 설정 오류에서도 사용할 수 있다. 실행/정리가 끝나기 전에는 false다.
- 정리 실패가 한 번이라도 보고되면 해당 Bootstrap의 재시도는 계속 차단한다. 후속 빈 Dispose 성공이나 참조 제거는 외부 효과 정리 성공을 증명하지 않는다. RetryBlockedReason으로 이유를 표시한다. 안전한 복구 API는 아직 없으므로 프로토타입 검증 중에는 Play 종료/재실행으로 초기화한다.
- 로비 이동 요청 직전에 재시도를 차단한다. 현재 UISceneNavigator는 이동 성공/실패를 반환하지 않으므로 이동 요청 이후 자동으로 차단을 풀지 않는다. 이동 실패 복구는 팀원의 결과 API 연결 후 구현해야 한다.
- 개발 화면은 시너지 연결, 실제/더미 증강, 등록한 전투 어댑터 이름, 대기 중 작업, UI 알림 오류 수를 표시한다. 어댑터 이름만으로 모든 기능 연결을 보장하지 않는다.
- 설정 검증은 좌표/셀 크기/대기 시간, 모든 라운드 용사 ID의 풀 참조, 지급 후보의 실제 프리팹/스탯 참조를 검사한다. 콘텐츠 값을 보정하지 않는다.
- 팩토리는 조립 중 실패 시 이미 만든 풀과 마왕군 연결부를 회수한다. Bootstrap은 Host 소유 등록 전에도 풀 참조를 보관해 초기화 실패를 정리한다.
- `OZGL2/InGame/Verify Core Failures (Empty Play Scene)`에서 UI 예외, 초기화 실패, 풀 생성 실패, 결과 디스크 보존, 비협조적인 외부 Task의 지연 처리를 검증한다.
