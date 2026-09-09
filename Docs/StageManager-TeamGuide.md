# StageManager 팀 공유용 구현·연결 안내

작성 기준: 2026-09-09 · 담당: 김건 · Unity 6000.3.22f1

## 구현 범위

StageManager는 순수 C# 진행 제어를 유지합니다. 매 라운드 결과 저장, 경험치 누적, 보상 요청 식별자,
풀링 전투 연결부, 씬과 독립된 실행 소유자를 추가했습니다.
실제 팀원 유닛의 이동·공격·효과, 인벤토리, 계정 성장, 실제 로비·준비·전투 UI는 연결 대상입니다.
이 문서의 인터페이스는 변경된 초안이며 팀원 구현체도 함께 변경해야 합니다.

## 현재 진행 규칙

밸런스 시트 구조를 참고한 더미는 보통 30R / 어려움 50R, 보스 10R 간격입니다.
서브레벨은 없습니다. 라운드 수·보스 여부·스폰 목록은 StageDataSO에서 가져옵니다.
미확정 수치는 검증용 데이터입니다.

```text
초기화 → 초기 기록 저장 → 첫 준비(스킵 불가) → 전투
  패배 → 라운드 결과 저장 → 정산 지급·저장 요청 → 정산 완료 기록 저장 → 로비 복귀 요청
  승리 → 클리어 수 증가·경험치 누적 → 라운드 결과 저장
    최종 → 일반 보상·증강 생략 → 정산 지급·저장 요청 → 정산 완료 기록 저장 → 로비 복귀 요청
    그 외 → 일반 보상 요청·적용 → 적용 기록 저장
          → 보스라면 증강 요청·적용 → 적용 기록 저장
          → 준비 완료 또는 스킵 → 다음 전투
```

스킵은 보상 선택 이후 배치·합성·재배치를 생략합니다.
재도전은 새 RunId로 1라운드부터 시작합니다. 기존 기록을 자동 복원하여 이어하는 기능은 없습니다.
실패 라운드의 처치 경험치도 정산 원자료에 포함하며 실제 지급 배율은 정산 담당자가 적용합니다.

## StageContracts.cs 전체

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage
{
    public interface IStageDataSource
    {
        StageDefinition CreateSnapshot();
    }

    public enum eBattleResult { VICTORY, DEFEAT }

    /// <summary>구현체가 스폰·마왕군 부활·전투 결과 수신을 연결합니다.</summary>
    public interface IStageBattle
    {
        Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken cancellationToken);
    }

    public interface IStageRewards
    {
        // 같은 RequestId의 재호출은 재지급하지 않아야 합니다. 실제 지급과 영수증은 함께 저장합니다.
        Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken cancellationToken);
        Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken cancellationToken);
    }

    public interface IStagePreparation
    {
        Task PrepareAsync(bool canSkip, CancellationToken cancellationToken);
    }

    /// <summary>런 초기화와 정산 지급·저장을 담당합니다. 씬 이동은 포함하지 않습니다.</summary>
    public interface IStageSession
    {
        Task BeginAsync(string stageId, CancellationToken cancellationToken);
        // RunId를 정산 중복 방지 키로 사용합니다. 지급 결과와 처리 키를 함께 저장합니다.
        Task SettleAsync(StageRunResult result, CancellationToken cancellationToken);
    }

    public interface IStageLobby
    {
        // 정산 완료 기록의 저장 성공 이후 호출됩니다. 정산을 재지급하지 않습니다.
        Task ReturnAsync(StageRunResult result, CancellationToken cancellationToken);
    }

    public interface IStageProgressStore
    {
        Task SaveAsync(StageRunResult snapshot, CancellationToken cancellationToken);
    }
}
```

## 변경된 데이터

| 타입 | 역할 |
| --- | --- |
| RoundResult | 확정 승패, 해당 라운드 처치 경험치. 음수 경험치·잘못된 승패 거부 |
| RewardRequest | RunId, RoundNumber, Kind. RequestId는 동일 보상 재요청에도 동일 |
| StageRunProgress | 실행 중 기록 소유. 중복 라운드 결과 거부, 원본 SO와 분리 |
| RoundProgress | 라운드별 승패·경험치·일반 보상·증강 상태 |
| StageRunResult | 읽기 전용 스냅샷. RunId, StageId, CurrentRoundNumber, ClearedRoundCount, EarnedExperience, Status, IsSettled, Rounds |

보상 상태는 NOT_REQUIRED / PENDING / APPLIED입니다.
도달 라운드와 클리어 수를 구분합니다. 예: 11R 패배라면 도달 11, 클리어 10입니다.
IsSettled가 false인 최종 기록은 정산 미완료 가능성을 뜻합니다.
취소·오류가 이미 확정된 승패를 덮어쓰지 않으며, 실행 Task와 StageManager.State로 종료 오류도 확인합니다.

## 저장 규칙

- IStageProgressStore를 생성자의 다섯 번째, IStageLobby를 여섯 번째 인자로 반드시 전달합니다.
- FileStageProgressStore는 도전별 `<RunId>.json`을 저장하고 Load(runId)로 읽습니다.
- 같은 디렉터리의 임시 파일에 직렬화·Flush한 뒤 기존 파일을 교체합니다.
- 같은 저장소 인스턴스의 쓰기는 직렬화합니다. 한 경로는 한 실행 소유자가 관리해야 합니다.
- 저장 실패 시 다음 보상·전투로 진행하지 않고 ERROR로 종료합니다. 자동 진행/재시도는 없습니다.
- 중단 시 상태 기록을 추가로 시도하고, 이 저장도 실패하면 PersistenceError에 노출합니다.
- 프로세스 강제 종료·저장 장치 오류에 대한 완전한 복구나 이어하기는 구현 범위가 아닙니다.
- 현재 최고 클리어 기록 UI나 계정 단위 진행도 집계는 별도 시스템에서 도전 기록을 읽어 구현해야 합니다.

JOB_KIMGUN 테스트는 `Application.persistentDataPath/stage_prototype`에 실제 파일을 씁니다.
더미 지급 영수증은 같은 디렉터리의 `dummy_rewards.json`에 보관합니다.
자동 검사는 Library/StageVerification의 실행별 하위 디렉터리를 사용하며 저장소 커밋 대상이 아닙니다.

## 보상·정산 중복 방지

일반 보상/증강은 RewardRequest.RequestId, 최종 정산은 RunId를 중복 방지 키로 사용합니다.
DummyRewardLedger는 더미 지급 결과 자체를 원자적으로 저장하므로, 파일을 다시 읽어도 같은 키를 재적용하지 않습니다.
이는 실제 기물·블록·증강 효과를 지급하는 구현이 아닙니다.

실제 보상 담당자는 **선택한 콘텐츠 및 지급 결과와 처리 키를 하나의 저장 트랜잭션으로 기록**해야 합니다.
지급과 영수증을 별도 저장하면 강제 종료 시 중복·누락 가능성이 있습니다.
StageManager의 APPLIED 기록만으로 실제 인벤토리의 정확히 한 번 지급을 보장할 수 없습니다.
같은 키로 동시에 들어온 요청도 실제 구현체에서 직렬화해야 합니다.
IStageSession.SettleAsync는 동일 RunId에 재지급하지 않고 정산 저장까지 완료합니다. 씬 이동은 하지 않습니다.
IStageLobby.ReturnAsync는 정산 완료 기록의 저장 성공 후 호출되며 로비 복귀만 담당합니다.
최종 저장 실패는 이동을 차단합니다. 이동 실패는 ERROR로 끝나지만 IsSettled는 유지됩니다.
이동만 다시 요청할 때 SettleAsync를 다시 호출하지 않습니다. 자동 재시도/재개 API는 이번 범위에 없습니다.

## 풀링 전투 연결

| 구성 | 책임 |
| --- | --- |
| HeroPoolCatalogSO | 용사 ID → 프리팹, 초기 풀 크기, 증가 단위, 더미 처치 경험치 |
| HeroPool | 대여·반환·확장. 물리적 객체 재사용, 대여별 식별자 확인 |
| PooledHero | 중복/이전 대여 사망 거부, 재사용 훅 호출 |
| IPooledHeroState | 팀원 컴포넌트의 체력·타깃·상태이상·타이머 초기화, 구독·비동기 작업 해제 |
| PooledStageBattle | 스폰 일정, 생존 수, 경험치, 승패 판정과 정리 |
| IStageDefenders | 라운드 전 마왕군 부활 완료, 현재 생존 수 공급 |
| RoundCompletionEvaluator | 스폰 완료 및 양측 생존 수로 진행/승리/패배/동시 전멸 판정 |

HeroPool.Rent는 상태 초기화된 **비활성** 용사와 LeaseId를 반환합니다.
연결부는 이를 생존 목록에 등록한 뒤 활성화합니다. 실제 사망 이벤트는 시작 시 받은 LeaseId로
PooledHero.TryReportDeath(leaseId)를 호출합니다. 현재 LeaseId를 나중에 다시 읽어 전달하면 과거 이벤트 보호가 무효가 됩니다.
재사용 훅은 예외를 던지지 않아야 하며 ResetForSpawn은 Awake에 의존하지 않고 동작해야 합니다.
처치 확정에서만 생존 수·경험치를 반영합니다. 생존 목록과 미반환 목록을 별도로 관리합니다.
HeroPool.Rent에는 처치 콜백과 반환 준비 콜백을 각각 전달합니다.
선택적 IPooledHeroDeathPresentation.BeginDeath(HeroLease)를 프리팹당 한 컴포넌트가 구현합니다.
이 구현체는 사망한 유닛의 공격/충돌 참여를 중단하고 연출을 시작하며, 완료 시 TryCompleteDeath(leaseId)를 호출합니다.
구현체가 없는 더미 용사는 즉시 완료됩니다. 살아 있는 용사의 완료 통지, 중복 및 과거 대여의 통지는 거부합니다.
라운드 종료·패배·취소 시 미반환 목록 전체를 회수하므로 남은 사망 연출은 중단됩니다.
ResetForReturn에서 연출·이벤트·비동기 작업도 정리해야 합니다.
회수 및 OnDisable은 처치로 취급하지 않으며 추가 경험치를 지급하지 않습니다.

스폰 목록은 순서대로 처리하며 각 용사 스폰 이후 다음 스폰까지 해당 항목의 간격을 사용합니다.
현재 간격은 실시간 Task.Delay입니다. 일시정지/배속은 아직 구현하지 않았습니다.
스폰이 끝나기 전 용사 수가 0이어도 승리하지 않습니다.
양측 동시 전멸은 정책 미정이므로 SIMULTANEOUS로 구분 후 오류 종료하며 정산하지 않습니다.
임의의 승리·패배 우선순위를 넣지 않았습니다.

현재 풀링 화면은 기본 Capsule 프리팹을 사용하며 이동·자동 공격은 없습니다.
더미 마왕군은 생존 수만 관리하고 매 라운드 초기 수로 복구합니다.
실제 SO는 IStageDataSource로 StageDefinition에 변환하고, 실제 용사 프리팹은 카탈로그 또는 별도 풀 조립부에 연결합니다.
밸런스 시트의 공격력·체력 배율 등은 현재 RoundDefinition에 없으므로 데이터/전투 담당자와 추가 전달 규격을 합의해야 합니다.

## 씬과 실행 수명

StageRunHost가 실행 Task·취소 토큰·서비스를 소유하며 DontDestroyOnLoad로 유지됩니다.
정적 Singleton 접근은 없습니다. 화면 비활성화와 제거는 실행 취소가 아닙니다.
실제 부트스트랩은 Host를 하나 생성하고 각 화면에 참조를 전달해야 합니다.
StagePrototypeRunner는 JOB_KIMGUN 테스트용으로 자신의 Host를 생성합니다.
실제 씬을 매번 열어 Runner를 새로 만들며 Host를 중복 생성하는 구조로 사용하면 안 됩니다.

실제 준비/전투/로비 로딩은 각각의 연결부에서 완료를 기다려 구현해야 합니다.
현재 테스트 화면은 씬 이동을 수행하지 않습니다. 임시 씬 생성·제거를 통한 Host 생존 검사는 별도 제공합니다.
Host 종료 시 취소를 전달하고 전투 정리를 기다린 뒤 소유한 풀을 폐기합니다.

## 테스트 화면과 검증

1. JOB_KIMGUN에서 Play.
2. Start Normal/Hard: 기존 수동 승패 버튼으로 진행 순서 검사.
3. Start pooled Normal/Hard: 실제 풀링 용사 생성. Defeat one hero/defender로 사망 입력.
4. 보상·증강·정산은 Confirm 버튼으로 더미 완료. 정산 완료 기록 저장 후 Return to dummy lobby 버튼으로 이동 완료를 전달.
5. 다음 준비부터 Skip preparation 사용 가능. Cancel run은 풀 반환과 중단 기록 저장을 요청.

Editor 메뉴:
- OZGL2/Stage/Verify Dummy Flow
- OZGL2/Stage/Verify Progress And Persistence
- OZGL2/Stage/Verify Pool And Scene Lifetime (Play)

Play 씬 전체 검사: StageFlowVerification.StartSceneChecks().
검사 결과는 StageFlowVerification.LastResult 및 StageEnhancementVerification.LastResult/LastPlayResult에서 조회합니다.
풀 재사용 측정은 StageEnhancementVerification.WarmPoolAllocatedBytes로 조회합니다.
이는 대여/반환 루프의 관리 힙 측정이며 전체 전투·저장·UI의 무할당을 의미하지 않습니다.

## 주요 파일

| 경로 | 용도 |
| --- | --- |
| Assets/01.Scripts/Stage/StageManager.cs | 진행 및 저장 순서 |
| Assets/01.Scripts/Stage/StageContracts.cs | 외부 연결 규격 |
| Assets/01.Scripts/Stage/StageRunProgress.cs | 실행/라운드/보상 기록 |
| Assets/01.Scripts/Stage/FileStageProgressStore.cs | JSON 저장·조회 |
| Assets/01.Scripts/Stage/PooledStageBattle.cs | 풀링 스폰·승패·경험치 |
| Assets/01.Scripts/Stage/HeroPool.cs, PooledHero.cs, HeroPoolCatalogSO.cs | 풀과 유닛 재사용 경계 |
| Assets/01.Scripts/Stage/RoundCompletionEvaluator.cs | 승패 규칙 |
| Assets/01.Scripts/Stage/StageRunHost.cs | 실행 수명 |
| Assets/01.Scripts/Stage/Prototype/ | 더미 서비스·지급 영수증·화면 |
| Assets/01.Scripts/Stage/Editor/ | 검사 및 JOB_KIMGUN 풀 에셋 설정 |
| Assets/_Project/Prefabs/Stage/DummyHero.prefab | 풀링 테스트 프리팹 |
| Assets/03.ScriptableObjects/Stage/Dummy/DummyHeroPoolCatalog.asset | 풀링 더미 설정 |

증강 확률, 동시 전멸 정책, 실제 지급·성장·씬 연결은 후속 합의/통합 대상입니다.
Notion은 읽기 전용으로 유지하며 커밋·Push·병합·팀원에게 전송은 수행하지 않습니다.

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
