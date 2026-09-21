# 스테이지 선택 → InGame 연결

## 연결 흐름

`StageSelectionController.StartStage(stageId)` → ID/데이터/용사 풀 검증 → 요청 접수 → `UISceneNavigator` → InGame에서 요청 1회 소비 → 카탈로그 조회 → 읽기 전용 스냅샷으로 기존 StageManager 시작.

선택 데이터는 런타임 요청함에만 보관한다. 원본 SO나 PlayerPrefs에 선택을 쓰지 않는다. 도메인 재로드를 끈 Play에서도 런타임 초기화 시 요청함을 새로 만든다.

## UI 담당자 연결

- `StageSelectionPrototype.prefab`의 `StageSelectionController`와 `UISceneNavigator`를 재사용한다. `StageSelectionDummyView`만 실제 UI로 교체한다.
- 버튼 UnityEvent: `StartStage(string)`에 정확한 StageId를 전달한다.
- 코드 연결: `TryStartStage(string)`의 bool 결과와 `Error`를 표시한다. true는 씬 이동 요청 접수이며 게임 초기화 완료를 의미하지 않는다.
- `IsLoading` 동안 중복 선택을 막는다. 이동 요청 실패 시 Error가 설정되고 잠금과 요청이 해제된다.
- 목록은 `GetStages()`로 읽는다. 표시 이름·이미지·해금 여부 등 실제 UI 데이터는 담당 UI/콘텐츠 시스템에서 연결한다.
- 뒤로 이동은 `TryReturnToLobby()`를 호출한다. 전투 시작 요청과 별개로 스킬 장착 상태를 저장하지 않는다.
- 기존 UI `UISceneNavigator.LoadScene(string)`은 유지한다. 스테이지 시작 버튼은 LoadScene을 직접 호출하지 않고 반드시 선택 Controller를 거친다.

현재 카탈로그:

| ID | 팀원 원본 SO | 라운드 |
|---|---|---|
| `stage_normal_30` | `Assets/03.ScriptableObjects/Stage/Balance/StageNormal30.asset` | 30 |
| `stage_hard_50` | `Assets/03.ScriptableObjects/Stage/Balance/StageHard50.asset` | 50 |

두 SO는 `Assets/_Project/Data/InGame/StageCatalog.asset`에 참조로 등록되어 있다. 원본 밸런스 데이터나 ID는 수정하지 않았다.

## InGame과 재도전

- `InGamePrototypeConfig`의 Stage Catalog가 선택 Controller와 InGame 양쪽의 공용 목록이다.
- 선택한 스테이지의 스폰 ID가 실제 HeroPoolCatalog에 모두 있는지 시작 전에 검사한다.
- `SelectedStageId`와 `Stage.Progress.StageId`로 실행 선택을 확인할 수 있다. 진행 저장은 기존 RunId별 파일 저장을 사용한다.
- 진행 중인 스냅샷은 고정된다. 같은 InGame 인스턴스의 `TryRetryStage()`는 그 스냅샷으로 새 RunId·초기 배치·보상 상태를 구성한다.
- 재도전은 기존 `CanRetry` 조건을 만족할 때만 허용한다. 실행 중·정리 중·정리 실패·로비 이동 중에는 시작할 수 없다.
- 정상 클리어/패배의 기존 자동 로비 복귀는 유지한다. 결과 화면에서 재도전을 선택하는 UI 흐름은 별도 작업이다. 현재 재도전 API는 취소/실행 오류 후 씬에 남아 있고 정리가 끝난 경우 사용할 수 있다.
- 선택 취소, 다른 씬으로 이동, 요청 소비 후에는 이전 요청이 남지 않는다. 취소는 요청 ID가 일치할 때만 수행하여 오래된 취소가 새 선택을 지우지 않게 한다.
- 누락·알 수 없는 ID·잘못된 SO는 오류로 차단하며 테스트 스테이지로 대체하지 않는다. 잘못된 요청으로 실패한 인스턴스는 재시작해도 같은 선택 오류를 유지한다.

## 직접 실행과 테스트

- `Builds/StageChoice.unity`를 열고 Play하면 더미 선택 화면에서 두 스테이지를 시작할 수 있다.
- `Builds/InGame.unity`를 직접 Play할 때만, 설정의 Allow Editor Direct Start를 켜두면 기존 테스트 Stage를 사용할 수 있다. 선택 흐름에 진입한 뒤 선택 없이 InGame으로 이동하면 허용하지 않는다.
- 실제 Player에서는 Editor 테스트 대체 실행을 허용하지 않는다. Lobby·실제 UI의 시작 버튼을 위 Controller로 연결해야 한다.
- 기존 빌드 시작 씬 순서와 Lobby 씬은 바꾸지 않았다. StageChoice를 빌드 목록에 추가했으며, 로비에서 그 씬으로 이동하는 최종 UI 연결은 UI 담당자 작업이다.
- 초기 기본 유닛 자동 배치와 준비→전투→보상 규칙, 스킬 수명주기는 기존 코어루프를 사용한다.
- 개인 SandboxLoopState 및 샌드박스 전투 보정 로직은 사용하지 않는다. 원본 SO가 같아도 샌드박스의 별도 보정이 실제 전투에 적용되는 것은 아니다.

계약 검증: `OZGL2/InGame/Verify Stage Selection Contracts`.
설정 재구성: 저장된 씬의 Edit Mode에서 `OZGL2/InGame/Set Up Stage Selection`. 기존 팀원 SO를 참조하고 Unity API로 데이터·프리팹·StageChoice 씬을 구성한다.
