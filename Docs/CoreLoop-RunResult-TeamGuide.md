# 전투 결과 및 재도전 (#55)

## 흐름

InGame 씬의 일반 라운드 승리는 전투 정리·기록 저장 → 웨이브 결과 확인 → 기존 일반 보상 → 준비로 이어진다. 중간 보스는 결과 확인 → 일반 보상 → 증강 → 준비 순서를 따른다. 최종 승리·패배는 웨이브 결과를 생략하고 StageManager의 기존 정산·저장을 완료한 뒤 Bootstrap에서 전투 자원을 해제하고 최종 결과를 표시한다. 자동 로비 이동은 하지 않는다. 결과 표시 동안 실행 Host, 유닛, 스킬 입력과 지속 효과를 남겨두지 않는다.

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

## 웨이브 결과 연결 (#75)

- `StageManager`는 선택 의존성 `IStageWaveResults`가 연결되면 `WAVE_RESULT`에서 확인을 기다린다. 최종 라운드/패배/전투 정리 오류에는 호출하지 않는다. 기존 enum 값은 유지하고 새 상태를 끝에 추가했다.
- `StageWaveResult`는 RunId, 웨이브 번호, 승패, 해당 웨이브 획득 XP의 불변 데이터다. `WaveResultConfirmation`은 확인 대기와 RequestId 검증만 맡는다. 이전 웨이브·이전 런·중복 클릭은 진행시킬 수 없다.
- InGame의 Bootstrap에 `InGameWaveResultPresenter`를 연결했다. Presenter는 항상 활성인 UI 호스트에 두고, 비활성 팝업 아래에 두지 않는다. 미연결 씬은 기존 흐름을 유지하므로 `InGame_2`로 통합할 때도 이 연결이 필요하다.
- 재사용 프리팹: `Assets/_Project/Prefabs/InGame/Popup_WaveResult.prefab`. 뷰는 웨이브 번호, 클리어 여부, 획득 경험치와 확인 버튼만 가진다. 팀 UI 교체는 `InGameWaveResultView`의 텍스트/버튼 참조와 Presenter의 View/Popup 참조를 바꾸면 된다. 전투/보상 로직은 UI 안에 작성하지 않는다.
- 폰트는 기존 `BattleOverlay Pixel.asset`을 공유한다. 웨이브 표기에 필요한 `웨`, `브` 두 글리프를 기존 동적 아틀라스에 추가했으며 폰트 GUID와 원본 폰트는 유지했다.
- Popup은 기존 `UIPopupController`의 Popup Root 하위에 배치하고 `Can Dismiss`를 끈다. 확인 전 Esc/배경 클릭으로 건너뛰지 않는다. 팝업 시스템의 기존 입력 차단을 재사용하며 확인 후 해제한다.
- `InGameExperienceBattle`은 실제 전투 전후 `MawangLevel.TotalEarnedXp`의 차이를 `RoundResult`에 기록한다. 레벨업으로 소비된 XP, 특성·증강 배율, 처치 추가 XP 및 전투 중 레벨 보너스를 포함한다. 일반 보상/증강 선택 등 전투 밖에서 지급된 XP는 해당 웨이브 획득량에 포함하지 않는다. 연결된 InGame의 최종 누적 XP도 이 웨이브 기록을 합산한다.
- 획득 XP 카운터는 표시/기록용이며 기존 XP·LP 지급 방식과 저장 키를 바꾸지 않는다. 새 런 및 저장 초기화 시 0으로 리셋한다. 팝업 표시와 확인은 XP를 지급하지 않는다.
- 런 취소/씬 종료 시 확인 대기를 취소하고 창을 닫는다. 재도전에는 새 RunId를 사용한다.

검증 메뉴: `OZGL2/InGame/Verify Wave Results`. 일반·보스 순서, 최종/패배 생략, 중복/오래된 확인 거부, 취소/재도전, 정리 오류, 실제 지급 XP·레벨업·보너스를 검증한다. 테스트 경험치는 비영속 `MawangLevel(false)`를 사용한다.
