# 전투 결과 및 재도전 (#55)

## 흐름

일반 라운드 승리는 기존 보상 → 준비로 이어진다. 최종 승리·패배는 StageManager의 기존 정산·저장을 완료하고 Bootstrap에서 전투 자원을 해제한 후 결과를 표시한다. 자동 로비 이동은 하지 않는다. 결과 표시 동안 실행 Host, 유닛, 스킬 입력과 지속 효과를 남겨두지 않는다.

결과 화면은 도달 라운드와 클리어한 라운드 수를 구분한다. 레벨과 현재 레벨의 XP는 종료 시점 스냅샷이며 누적 획득 XP가 아니다. 결과 표시 또는 버튼 클릭은 XP/LP를 지급하지 않는다. 기존 DummyRewardLedger는 정산 영수증만 저장하며 실제 계정 보상 지급으로 대체되지 않았다.

## 실제 UI 연결

- `bootstrap.Result`가 null이 아니면 결과 화면을 표시한다. 결과의 `RunId`를 버튼과 함께 보관한다.
- `Result.Progress`에서 승패, StageId, 도달·클리어 라운드를 읽는다. `TotalRounds`, `Level`, `CurrentLevelXp`는 표시용 값이다.
- `CanChooseResult`일 때만 입력을 활성화한다.
- 재도전: `TryRetryResult(runId)`.
- 스테이지 선택: `TryReturnToStageSelection(runId)`.
- 로비: `TryReturnToLobby(runId)`.
- `Changed`에 구독하여 갱신하고 화면 종료 시 해제한다. Unity 메인 스레드에서 호출한다.
- 씬 이동 접수 실패는 결과를 유지하고 `ResultActionError`를 제공한다. 오래된 ID·중복 클릭은 false를 반환한다.
- InGameDummyView의 결과 그리기는 교체 가능한 표시부다. Bootstrap의 결과 계약은 유지한다.

씬 이동은 팀 UISceneNavigator를 사용한다. Config의 Stage Selection Scene Path와 Lobby Scene Path에 실제 빌드 씬을 설정한다. 잘못된 경로는 정상 이동으로 처리하지 않는다.

## 재도전과 소유권

동일 SelectedStageSource에서 새로운 StageManager/RunId/Grid 세션을 만든다. 첫 전투 자동 배치 규칙을 재사용한다. RealSynergySync.BeginRun을 통해 레벨 1·XP 0, 증강 초기화 및 장착 스킬 재구성을 수행한다. LP·특성·영구 해금은 팀원의 기존 저장 규칙을 따른다. BeginRun 이전에 선택·설정·표시 설정을 검사하여 잘못된 진입으로 진행도가 초기화되지 않게 한다.

정산·저장·정리 실패 및 취소는 정상 승패 결과로 표시하지 않는다. 기존 오류 경로와 정리 실패 시 재실행 차단은 유지한다. 결과의 세 행동은 정리 완료 후에만 가능하다. StageManager 내부의 RETURNING_TO_LOBBY는 기존 결과 전달 계약이며, 실제 씬 전환은 이제 결과 선택 이후다.

실제 계정 정산, 최초 클리어·해금 지급, 강제 종료 복구/이어하기 및 최종 UI 디자인은 이번 범위 밖이다.
