# 로비 시작 버튼 고정 라벨

## 변경 목적과 구조

기존 Label의 큰 오른쪽 여백을 공유한 채 짧은 잠금 문구로 바꾸던 표시를 두 개의 고정 TMP 오브젝트로 분리한다. 기존 Label의 GameObject/컴포넌트 ID는 유지하고 이름만 UnLockLabel로 변경했다. 표시 전환은 기존 LobbyDifficultyLock의 책임으로 유지하며 Runtime 클래스를 추가하지 않는다.

```text
Canvas_Lobby/StageSelection/CurrentStage/StartButton
├─ UnLockLabel    # 전투 준비 / 기존 Label 그대로, 기본 활성
├─ IconVisual     # 기존 검·자물쇠 아이콘 교체 유지
├─ ClickArea      # 기존 클릭 영역 유지
└─ LockLabel      # 잠김 / 별도 TMP, 기본 비활성
```

- UnLockLabel: Pos(82, 3.5), Size285×68, FontSize47, 기존 Right Margin97.222 유지.
- LockLabel: 최종 수동 Pos(4.9, 2.6), Size187.77795×68, FontSize47, Margin0. 구성 도구는 최초 Pos(33.388977, 3.5)로 기존 글자 영역의 중심을 맞췄으며, 이후 사용자가 조정한 위치를 씬에 보존했다. 같은 폰트·크림색·정렬·기본 scale을 사용한다.
- 두 라벨의 RectTransform·폰트·여백·정렬은 각각 Inspector에서 독립적으로 편집할 수 있다. 런타임이 이 값을 재설정하지 않는다.
- TutorialBridge의 LobbyDifficultyLock에 Lock Label / Un Lock Label을 각각 직접 연결했다. 두 참조는 서로 다른 TMP여야 하며 동시에 빠져 있으면 구형 단일 Label 방식으로 동작한다.

## 상태와 유지 범위

- 잠금: LockLabel 활성, UnLockLabel 비활성, 기존처럼 StartButton.interactable=false.
- 해금: UnLockLabel 활성, LockLabel 비활성. 잠금 코드가 비활성화했던 버튼만 다시 활성화한다.
- 난이도 전환 중에는 시작 버튼 상태/라벨 표시를 갱신하지 않아 선택기의 기존 비활성화·복구 처리를 유지한다.
- 새 라벨이 연결된 경우 text/alignment를 변경하지 않는다. activeSelf가 달라질 때만 SetActive를 호출한다.
- 아직 전환하지 않은 씬은 StartButton/Label을 찾아 기존 문구 변경·복구를 유지한다. 새 라벨을 첫 번째 TMP로 임의 선택하지 않는다.
- 해금 판정, StageClearStore 저장, 선택기·전투 시작 이벤트, 버튼 클릭 영역, 기존 아이콘 교체 및 이미지 원본은 변경하지 않았다.

## 변경 파일과 도구

- Assets/01.Scripts/UIBridge/LobbyDifficultyLock.cs: 라벨 직접 연결 및 표시/버튼 상태 전환 분리, 구형 씬 호환.
- Assets/00.Scenes/Builds/Lobby.unity: 기존 라벨 이름 보존 변경, LockLabel 추가, 두 참조 연결, 최종 수동 라벨 위치와 NextStage_Locked 자물쇠 tint(#E5BA6B) 반영.
- Assets/00.Project/01.Scripts/Editor/LobbyMutedPreviewFontApplier.cs: 폰트 적용 대상 경로를 구형 Label 또는 두 새 라벨로 선택한다. 실제 폰트 적용 메뉴는 실행하지 않았다.
- Assets/Editor/LobbyStartButtonLabelTools.cs 및 Unity 생성 .meta: 일회 구성·데이터 없는 표시 검증. 기존 Runtime 코드와 Editor 작업을 분리하기 위한 Editor 전용 도구다.
- Docs/UI/LobbyStartButtonLabels.md: 구조·수동 편집 위치·검증 기록.

메뉴:

- Tools > UI > Lobby Start Button > Apply Separate Labels: Builds/Lobby의 미전환 버튼만 구성한다. 생성/이름 변경/참조 연결 Undo 지원. 중복 구성 차단, 자동 씬 저장 없음.
- Tools > UI > Lobby Start Button > Validate Separate Labels: 현재 UI를 임시 Preview Scene의 비활성 루트로 복제해 검증하고 제거한다. 원본 변경과 실제 계정·진행 기록 조회는 하지 않는다.

## 확인 결과와 남은 테스트

- 컴파일 완료·Console Error0개, 잠금→해금 반복 전환과 상태별 배타적 표시·버튼 상태 검사 통과.
- 문구·폰트·색·정렬·여백·RectTransform 보존 검사, 전환 중 표시 유지, 기존 단일 Label 복구, 누락 참조 안전 처리 검사 통과.
- 검증 전후 원본 UI 직렬화와 씬 dirty 상태 보존.
- 임시 UI 렌더: Temp/LobbyRecordInline_20261012/Lobby_StartButton_Unlocked.png / Lobby_StartButton_Locked.png. 기존 격리 렌더 경로를 재사용했고 실제 게임/계정은 실행하지 않았다. 라벨 표시용 렌더이며 실제 Play Mode의 버튼 ColorTint 전환 검증은 아니다.
- 실제 Play Mode에서는 잠긴 난이도↔열린 난이도 변경, 디버그 해금, 전환 중 클릭 차단, 재활성화 시 올바른 라벨 하나만 표시되는지 확인해야 한다.
- Inspector: TutorialBridge의 두 TMP 참조, StartButton.onClick의 기존 StartSelected, 두 라벨 Raycast Target=false 확인.
- LayerMask/Tag/Collider/Rigidbody/Animator/Input System 설정 추가 없음. Prefab·이미지·기존 메타·ProjectSettings·Packages 변경 없음. 새 Editor 스크립트의 메타만 Unity에서 생성했다.
- Git 충돌 주의 파일은 Lobby.unity와 LobbyDifficultyLock.cs. 씬 diff는 Label 이름 변경, LockLabel 추가/수동 위치 및 두 참조 연결, NextStage_Locked/VisualLayers/LockOverlay/Lock의 수동 Image Color(0.8980392, 0.7294118, 0.41960785, 1) 변경이다. 기존 전투 준비 TMP·버튼·시작 버튼 아이콘·ClickArea의 설정은 유지했다.
- 라벨 검증은 문구/정렬/위치가 전환 중 보존되는지 검사하므로 특정 수동 Pos에 고정되지 않는다. 기존 LobbyStageLockValidation의 자물쇠 색 검사는 이전 값(1, 0.89, 0.69, 1)에 고정돼 있어 위 수동 tint와 불일치한다. 해당 도구는 이번 커밋 범위에서 변경하지 않는다. 직전 캡처는 최신 수동 위치·색 변경 전의 표시다.
