# 일반 보상 연결 (#49)

## 적용 범위

Builds/InGame의 일반 보상은 `GeneralRewardPool.asset`을 사용한다. 기존 독립 GridPrototypeRunner/Flow의 순환 후보는 테스트용으로 유지하며 실제 InGame은 사용하지 않는다. 스테이지 진행, 증강 효과, 계정 경험치/최초 클리어 정산, 유닛별 점유 모양은 변경하지 않는다.

## 데이터와 해금

- 설정: `Assets/_Project/Data/InGame/GeneralRewardPool.asset`. InGamePrototypeConfig의 General Reward Pool에 연결한다.
- 항목별 Unit, Block, Weight, Is Initially Unlocked를 설정한다. 현재 기존 실제 유닛 6종을 기본 해금, 가중치 1로 명시했다. 밸런스 확정 수치는 아니다.
- 모두 1성 유닛과 해당 발판을 함께 지급한다. 현재 1성 점유/지급 발판은 1×1이며 확장은 1×2다. 최신 dev_2의 2·3성 점유 모양도 UnitDefinition에 그대로 보존하므로 이후 합성 시 성급별 점유가 적용된다.
- 음수/NaN/무한 가중치, 중복 ID, 누락 참조, 유닛과 보상 발판 ID 불일치는 설정 오류다. 가중치 0은 추첨 제외다.
- 현재 계정 유닛 해금 저장소가 없어 `DefaultUnitRewardUnlocks`가 명시적 기본 해금 목록을 사용한다. 계정 조회는 `IUnitRewardUnlocks.IsUnlocked(unitId)`로 연결하고 조립 시 `config.CreateRewardSource(accountUnlocks)`를 호출한다. 기본 해금과 계정 해금의 합집합은 계정 어댑터에서 반환한다. 해금 저장·최초 클리어 해금 지급은 이 작업의 구현 범위가 아니다.
- 런 시작 시 SO를 불변 데이터로 복사하며 원본 SO를 런타임에 변경하지 않는다. 보상 요청마다 해금 조회를 수행하고, 이미 제시한 후보는 다시 계산하지 않는다.

## 추첨과 지급

- 확장 가능: 유닛 2개 + 확장 1개, 총 3개 중 하나 선택.
- 확장 불가능: 유닛 3개 중 하나 선택. 최대 40칸뿐 아니라 빈 칸 모양 때문에 1×2를 놓을 수 없는 경우도 포함한다.
- 양수 가중치와 해금 조건을 통과한 유닛 중 가중치 추첨한다. 후보 중복 없이 먼저 종류를 소진하고, 종류가 부족할 때만 다시 풀을 채운다. 추첨 대상이 0개면 진행을 중단하고 설정 오류를 표시한다.
- 보상 요청당 한 번만 추첨한다. 화면 재표시나 보관함 정리 취소로 재추첨하지 않는다. 다음 라운드는 새로 추첨한다. 별도 리롤 기능은 없다.
- 보관함은 유닛과 발판을 각각 세어 최대 10개다. 두 항목이 모두 들어갈 공간을 확보한 후 함께 지급한다. 버리고 받기 취소 시 기존 후보로 돌아온다.
- 클릭 성공만으로 다음 준비로 이동하지 않는다. Grid의 지급 완료를 기다린다. 같은 요청의 재호출, 이전 요청 클릭, 취소 후 클릭은 재지급하지 않는다.

## UI 연결

1. `bootstrap.Rewards.Pending`이 있으면 `Pending.RequestId`와 `Candidates`를 함께 읽는다.
2. `GeneralRewardOption.Kind`로 UNIT/EXPANSION을 구분한다. UNIT은 Unit, Block, StarLevel(1)을 표시한다. 발판은 지급 정보이며 해당 모양에만 유닛 배치를 강제한다는 의미가 아니다.
3. 표시한 요청 ID와 후보 인덱스로 `TrySelect(requestId, index)`를 Unity 메인 스레드에서 호출한다.
4. 반환값 false만 보고 오류 처리하지 않는다. `GridSession.Grid.PendingStorage`가 있으면 버리고 받기 화면을 표시한다. 그 외에는 오래된 요청/취소/유효하지 않은 선택일 수 있다.
5. `TryConfirmStorage`/`TryCancelStorage`는 기존 GridRunSession API를 사용한다. 지급 대기 중에는 다음 단계로 이동하지 않는다.
6. 요청 종료 시 Candidates는 비워진다. 화면 종료 시에도 오래된 요청 ID로 선택하지 않는다. 매 프레임 조회 또는 기존 Bootstrap.Changed와 단계 변경 알림으로 갱신한다.

`InGameDummyView`는 이 후보 목록을 표시하는 더미 UI다. 실제 UI 담당자는 같은 API를 사용하며 고정된 유닛 버튼 2개/확장 버튼 1개를 가정하지 않는다.

## 검증

- `OZGL2/InGame/Verify General Rewards`: 추첨 분포/필터, 후보 부족, 최대 영역의 세 번째 유닛, 보관함 초과/취소/동시 지급, 요청 취소/중복 검사. 메모리 그리드와 더미 정산만 사용한다.
- `Verify Integration`: 새로운 보상 제공자로 전체 라운드·패배·확장 강제·보관함 회귀 검사.
- `Verify Augment Integration (Empty Play Scene)`: 실제 증강 제공자와 새 일반 보상의 단계 연결 검사. 전투는 수동 더미다.

현재 보상 후보/지급 영수증은 실행 중 메모리 범위다. 강제 종료 후 이어하기는 제공하지 않는다.
