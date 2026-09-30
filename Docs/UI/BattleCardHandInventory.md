# 전투 카드 손패 UI 연결 가이드

## 목적

`BattleCardHand.prefab`은 보유 유닛과 땅 슬롯을 기존 전투 카드 아트로 표시하는 uGUI 손패다.
카드 목록의 논리 순서와 호버 렌더 순서를 분리하며, 실제 보유 데이터와 배치 규칙은 소유하지 않는다.
기본 상태는 왼쪽에서 오른쪽으로 겹치는 부채꼴이며, 호버한 카드만 정면으로 펴져 확대된다.

## 구성

- `UIBattleCardHandView`: 외부 표시 목록, 겹침 배치, 가로 스크롤, 호버를 담당한다.
- `UIBattleCardHandSlot`: 고정 입력 영역과 상승·확대되는 비주얼을 분리한다. 호버 시 비주얼 전용
  nested `Canvas`만 손패 Canvas보다 한 단계 앞으로 올려 카드 입력 sibling 순서는 바꾸지 않는다.
- `UIGridStorageHandAdapter`: 현재 `GridManager`를 표시 데이터와 기존 배치 명령에 연결한다.
- `BattleHandEventSystemInstaller`: uGUI `EventSystem`이 없는 Scene에서만 런타임 입력 모듈을 보완한다.
- `BattleHandCardDisplayData`: 다른 인벤토리 구현도 전달할 수 있는 표시 전용 계약이다.

팀원의 인벤토리로 교체할 때는 `UIBattleCardHandView.SetItems(...)`에 같은 표시 목록을 전달하고
`UIGridStorageHandAdapter`만 제거하거나 대체한다. View와 카드 Prefab은 그대로 재사용할 수 있다.

## UI_Battle_MutedPreview 미리보기

`Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`의 Scene 루트에
`BattleCardHand_Preview` Prefab 인스턴스를 배치했다.

- `UIGridStorageHandAdapter`는 비활성화해 실제 `GridManager` 데이터에 연결하지 않는다.
- `UIBattleCardHandPreviewPresenter`가 Play Mode에서만 확인용 카드 6장을 공급한다.
- 샘플 카드는 Scene 또는 Prefab에 저장하지 않으므로 실제 보유 데이터와 섞이지 않는다.
- 미리보기 Scene을 실행하면 자동 정렬, 겹침, 상승·확대 호버를 바로 확인할 수 있다.
- 기존 정적 카드 3장은 중복 표시를 피하기 위해 `CardPrefabInstances`에서 비활성화했다.
- 손패 배경과 상단 테두리는 표시하지 않고 기존 전투 화면 위에 카드만 배치한다.
- 이 연결은 `InGame.unity`와 `InGamePrototype.prefab`에 영향을 주지 않는다.

## 현재 InGame 연결

김건 팀의 `InGamePrototype.prefab`, `GridPrototypeRunner`, `GridDragInput`, `GridManager`에는 충돌 방지를 위해
직접 변경을 넣지 않았다. 실제 연결 시 다음 한 단계가 필요하다.

1. `Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCardHand.prefab`을
   `InGamePrototype` GameObject의 자식으로 배치한다.

같은 계층에 배치하면 Adapter가 `InGamePrototypeBootstrap`, `InGamePhasePresentation`, `UIDocument`를 자동 탐색한다.
별도 Scene 루트에 둘 경우에는 Adapter의 세 참조를 Inspector에서 직접 연결해야 한다.

현재 Canvas sorting order는 `10`이고 호버 비주얼은 `11`을 사용한다. Grid UI Toolkit 패널(`0`)보다 위에서 카드 입력을 받고,
증강 선택·결과 등 상위 팝업 Canvas(`100`)보다 아래에 머문다. 빈 배경과 Viewport는 Raycast를 차단하지 않으므로
카드·Scrollbar 밖에서는 기존 전장과 준비 버튼 입력이 유지된다. 실제 InGame 연결 후에는 하단 Start/Skip/Reward 컨트롤과
시각적으로 겹치지 않는지 확인하고, 위치 조정이 필요하면 김건 팀 UI 레이아웃과 함께 조율한다.

## 기존 Grid 연결 범위

- `GridManager.GetStoredItems()` 순서를 그대로 사용한다.
- 손패에서 전장으로 유닛/땅 슬롯을 배치할 수 있다.
- 손패의 다른 유닛 위에 놓으면 기존 `TryFuseUnits` 규칙으로 합성을 시도한다.
- 손패 내부에 놓으면 기존 `DropToTray`를 사용한다.
- 드래그 중 `R`, `F`, `Esc` 입력을 지원한다.
- 기존 UI Toolkit tray는 레이아웃과 `worldBound`를 유지한 채 숨겨 전장 시작 드래그의 반환 판정을 보존한다.

전장 시작 드래그는 기존의 숨겨진 tray 좌표를 사용한다. 새 손패의 전폭 350 UI 단위 드롭 영역과 좌표가 완전히 같은지는
실제 InGame Play Mode에서 확인해야 한다. 좌표가 다르면 김건 팀과 `GridDragInput`의 drop-zone 계약을 추가하는 것이 권장된다.

## Inspector 조정값

`UIBattleCardHandView`에서 다음 값을 조정한다.

- `_comfortableSpacing`: 카드 수가 적을 때 간격
- `_minimumRevealWidth`: 겹침 상태의 최소 노출 폭
- `_cardScale`: 기본 카드 배율
- `_fanArcHeight`: 중앙 카드가 올라오는 부채꼴 높이, 기본값 `32`
- `_maxFanAngle`: 좌우 끝 카드의 최대 회전, 기본값 `8`
- `_hoverScale`: 기본값 `1.44`
- `_hoverRise`: 기본값 `60`
- `_transitionDuration`: 기본값 `0.15`
- `_bottomPadding`: 기본 카드 하단 여백

## 생성 및 검증 메뉴

- 생성: `Tools/OZGL/UI/Battle/Create Or Update Card Hand Prefab`
- 부채꼴 설정 적용: `Tools/OZGL/UI/Battle/Apply Card Hand Fan Layout`
- Edit Mode 검증: `Tools/OZGL/UI/Battle/Validate Card Hand Prefab`
- Play Mode 검증: `Tools/OZGL/UI/Battle/Validate Card Hand Runtime (Play Mode)`

Edit Mode 검증은 직렬화 연결과 0장, 1장, 여러 장, 초과 카드 스크롤, 단일 호버, 호버 카드 삭제,
일시 상태 초기화를 확인한다. Play Mode 검증은 임시 Prefab 인스턴스와 외부 `SetItems` 목록으로 `timeScale = 0` 호버,
빠른 전환, 입력 sibling 순서 유지, 호버 비주얼 Canvas 전면 표시, 추가·삭제, 40장 스크롤을 확인한다.
Play Mode 검증에서는 Grid Adapter와 EventSystem Installer를 끄므로
실제 Grid 드래그, UI Toolkit 포인터 우선순위, 하단 준비 버튼과의 시각적 겹침은 InGame 연결 후 별도로 확인해야 한다.
