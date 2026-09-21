# 스킬 세팅 아트 미리보기 v1

## 적용 범위

- 대상 Scene: `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`
- 새 Prefab: `Prefabs/Canvas_SkillSettings.prefab`
- Scene의 `Canvas_Popups/Canvas_SkillSettings`에 배치하고 `Canvas_Lobby`의 `SkillsButton`에서 연다.
- 기존 `Popup_Skills`, 메뉴·설정·특성 Prefab 및 다른 Scene은 변경하지 않는다.
- Scene의 뒤로 버튼만 기존 `UIPopupController.CloseTopPopup`에 연결한 인스턴스 override이다. 다른 씬에 재사용할 때 이 연결과 PopupRoot 등록이 필요하다.
- 배경은 특성창처럼 공용 `LobbyMutedPreview/Sprites/Lobby_Background.png`를 재사용하고, `BackgroundShade`에 검은색 불투명도 60%(Alpha 0.60)를 적용한다. 생성했던 `Skill_Background.png`는 미사용 원본으로 보존한다.
- `BackgroundBottomGradient`는 로비의 같은 이름 오브젝트에서 텍스처·색·RectTransform을 복사했다. 특성창에도 같은 레이어를 추가했으며, 두 화면 모두 배경 → 검은 오버레이 → 하단 그라데이션 → UI 순서이다.

## 구성 / 책임

- 스킬 분류색 규칙: 저장소 `Docs/UI/SkillCategoryVisualRules.md`. 흰색 본체 + 딜 적색/버프 고금색/디버프 보라색 외곽선, 슬롯 내부 14% 색을 목록·장착·상세에 공통 적용한다.
- `UISkillCategoryStyleSO`와 `Styles/SkillCategoryStyle.asset`은 표시용 공유 Material/슬롯 농도/분류별 장착 프레임 Sprite를 관리한다. 원본 PNG 및 실제 스킬 효과/저장 데이터는 수정하지 않는다.
- `LobbySkillCategoryStyleBuilder`의 적용 메뉴는 기존 스킬 Prefab을 Undo 가능한 방식으로 연결하고, 최초 생성 도구도 같은 표시 설정을 사용한다. 상세 설정/재사용 순서는 위 문서를 따른다.

- 생성 PNG 39개: 최초 37개(미사용 배경 1개 포함) + 주황 호버/금색 선택 프레임 2개. 기존 보라색 선택 프레임은 보존하지만 목록과 상세 화면에서 사용하지 않는다. 글자는 이미지에 포함하지 않는다.
- `UISkillArtButton`: 일반·호버·누름·선택·비활성 Sprite 전환과 별도 TMP Label 색을 담당한다. 카테고리의 실제 선택 상태는 키보드 포커스와 별개이다.
- `UISkillLoadoutPreview`: 전체/딜/버프/디버프 필터, 3개 슬롯 임시 장착/해제, 상세 표시를 담당한다.
- `UISkillPreviewCatalogSO`: 이미지 검토용 12개 스킬의 이름·수치·분류·아이콘만 가진다. 실제 게임 밸런스/해금 데이터가 아니다.
- 런타임 상태는 컴포넌트의 임시 배열에만 저장하며 ScriptableObject를 변경하지 않는다.

## 저장 범위 — 실제 게임 저장 아님

`저장`은 같은 Play Mode 실행 중 다시 열었을 때 유지할 **미리보기 장착 상태**만 확정한다. 현재 슬롯 구성과 저장 구성이 같으면 저장 버튼이 비활성화되고, 다르면 활성화된다. 원래 구성으로 다시 맞추면 저장하지 않아도 비활성화된다. `저장 전 변경사항` 문구는 표시하지 않는다.

미저장 편집 중 뒤로/ESC로 닫으려 하면 `ExitConfirmation`을 연다. 취소(또는 확인창에서 ESC)는 편집을 유지한다. `로비로`를 눌렀을 때만 미저장 변경을 버리고 로비로 돌아간다. 변경이 없으면 바로 닫힌다. Play Mode 종료 시 초기 시안으로 돌아간다.

확인 문구는 별도 TMP이다: `스킬 변경 사항이 저장되지 않았습니다.\n저장하지 않고 로비로 돌아가시겠습니까?`.

기존 `SkillTreeStore`, PlayerPrefs, 전투 장착 데이터는 변경하지 않는다. 실제 시스템 연동은 별도 작업이며 `SaveRequested`는 아직 연결되지 않았다. 실제 연동 시에는 저장뿐 아니라 보유/해금 목록 및 기존 장착 상태를 받아오는 어댑터도 필요하다.

## Inspector 체크

- 문구: 각 버튼의 `Label`에 있는 TMP Text를 수정한다.
- 상태별 이미지: `UISkillArtButton`의 Normal/Hover/Pressed/Chosen/Disabled를 확인한다.
- 목록 카드만 `Keep Chosen While Pressed`가 켜져 있다. 호버는 `Card_Hover_Amber`, 선택은 `Card_Selected_Gold`를 사용하며 선택 후 재누름/이탈에도 금색을 유지한다. 탭/장착 슬롯/기타 버튼 동작은 유지한다.
- 카드와 장착 슬롯은 아이콘과 프레임이 별도 Image이다. 장착 프레임은 스킬 분류로 교체하며, 목록 호버/선택은 기존 상태 Sprite를 유지한다.
- 확인창: Prefab의 `ExitConfirmation/Panel/Message`, `Cancel`, `Leave`. `UISkillLoadoutPreview.Exit Confirmation`과 두 버튼의 취소/나가기 연결을 확인한다.
- 확인창 Canvas는 부모보다 높은 sortingOrder(151), CanvasGroup의 Ignore Parent Groups 활성, 별도 GraphicRaycaster를 사용한다. 편집 UI를 잠근 상태에서도 확인창 버튼은 입력을 받는다.
- 폰트: 기존 `01.Font/DOSMyungjo Overlay Pixel.asset`와 Outline Material을 재사용한다. Git에서 제외된 폰트는 공유 드라이브에서 **기존 .meta와 함께** 복원해야 한다.
- 입력: 기존 EventSystem/Input System 및 PopupController를 재사용한다. 새 EventSystem/입력 액션은 추가하지 않았다.
- LayerMask/Tag, Collider/Rigidbody/IsTrigger, Animator/Animation Event 설정 변경은 없다.

## Editor 도구

### 배경 농도 / 그라데이션 조절

- 스킬: Scene의 `Canvas_Popups/Canvas_SkillSettings/BackgroundShade` → Image → Color → A. 현재 0.60(255 기준 153).
- 특성: `Canvas_LobbyOverlays/TraitsScreen/BackgroundShade` → Image → Color → A. 기존 0.67을 유지한다.
- 두 화면 각각의 `BackgroundBottomGradient` → Raw Image → Color → A로 하단 그라데이션 농도를 조절한다. 현재 로비와 같은 242/255이며, 위로 갈수록 텍스처 자체의 알파가 줄어든다.
- 그라데이션 범위는 Rect Transform에서 조절한다. 현재 하단 고정, Anchor Min (0, 0), Max (1, 0.48), Pivot (0.5, 0), Size Delta Y 약 214.295이다. Raycast Target은 꺼 둔다.
- 재사용 프리팹에 저장하려면 Play Mode를 종료한 뒤 각각의 Prefab Mode에서 수정한다. Scene 인스턴스에서만 수정하면 override로 남는다.

### 생성 / 검증 메뉴

- 생성: `Tools/OZGL2/Lobby/Build Skill Settings Preview`
  - 대상 씬의 편집 모드에서만 실행한다. 이미 프리팹이 있으면 덮어쓰지 않고 중단한다.
  - Scene 오브젝트 추가와 로비 버튼 재연결은 Undo 그룹을 제공한다. 생성된 프로젝트 에셋 파일 자체는 Undo로 삭제하지 않는다.
  - 기존 프리팹이 생성되어 있으므로 완료된 프로젝트에서 재실행할 필요는 없다.
- 검증: Play Mode에서 스킬창을 연 뒤 `Tools/OZGL2/Lobby/Validate Skill Settings Preview`
  - 미리보기 상태만 테스트하고 기본 3개 장착 시안으로 복원한다. 실제 게임 저장은 읽기만 한다.
  - Canvas 활성화 다음 프레임에 `LobbySkillSettingsPreviewValidation.ValidateRaycast()`로 클릭 대상을 별도 검사한다.

## 검증 기록 (2026-09-19)

- Unity 6000.3.22f1 컴파일 성공, 검증 후 Console 오류/경고 0개.
- Play Mode 32개 검사 통과: 4개 필터(전체 12개/분류별 4개), 장착·해제, 중복/3개 한도 차단, 임시 저장/재열기/미저장 취소, 뒤로 및 로비 입력 복원, 5개 Sprite 상태, TMP 연결.
- 별도 Raycast 검사 통과. TMP가 클릭을 가로채지 않고 창 배경이 로비 클릭을 차단한다.
- 새 화면 Missing Script 0개, 끊어진 직렬화 참조 0개.
- 1920×1080 실제 Play Mode 화면에서 글자·여백·아이콘·버튼 배치를 확인했다.
- 최종 화면: 저장소 `Tools/Art/Previews/SkillSettings_v2_AmberGold.png`. 이전 배경/프레임 시안 이미지도 보존한다.
- 배경 통일 후 재검증: 로비와 동일 Sprite 참조 및 Alpha 0.30 확인, 스킬창 열기/뒤로/로비 입력 복원/Raycast 통과, Console 오류·경고 0개. 이번 후속 변경은 프리팹 Image 2개 속성뿐이며 Scene 파일은 변경 전후 동일하다.
- 수동 추가 확인: 실제 ESC 키 입력, 키보드 전체 탐색, 16:9 이외 화면 비율, 플레이어 빌드. 해당 항목은 이번 자동 검증에 포함하지 않았다.

### 카드 상태 후속 검증 (2026-09-20)

- 총 42개 검사 통과(기존 32개 + 카드 상태 10개), Raycast 정상, Console 오류·경고 0개.
- 미선택 호버 주황/이탈 시 기본 복원, 클릭 후 금색/이탈 및 재누름 시 금색 유지, 다른 카드 선택 시 이전 선택 해제, 비활성 우선, 상세 프레임 금색 일치를 검사했다.
- 이번 변경: `UISkillArtButton.cs`(카드 한정 선택 유지 옵션), `LobbySkillSettingsPreviewBuilder.cs`(생성 시 동일 연결), `LobbySkillSettingsPreviewValidation.cs`(회귀 검사), `Canvas_SkillSettings.prefab`(카드/상세 연결), 신규 PNG 2개 및 Unity 생성 .meta, 후처리 도구/기록.
- Scene 파일은 변경 전후 동일하며, ProjectSettings/Packages/메뉴·특성·장착 슬롯·카테고리 아트는 변경하지 않았다.
- Unity MCP 적용은 Undo 그룹 `Apply skill card amber hover and gold selection`으로 기록했다. 생성 이미지 파일 자체는 Undo 대상이 아니다.

### 저장 확인 / 장착 프레임 후속 검증 (2026-09-21)

- `UIPopupPanel`은 선택적 닫기 검사와 Runtime 소유 Controller만 추가했다. 스킬창이 OnEnable에 검사를 등록하고 OnDisable에 제거한다. 기존 `CloseTopPopup`(뒤로/ESC) 경로를 공유하므로 Scene의 버튼 override를 수정하지 않는다.
- 미저장 확인에는 `CloseTopPopup`을 사용한다. `CloseConfirmedPopup`은 선택 완료/Controller 종료를 위한 기존 강제 닫기 API로 유지되므로 일반 뒤로/ESC에는 연결하지 않는다.
- 새 `LobbySkillSaveFlowBuilder`는 확인 UI 조립을 Runtime 상태와 분리한다. 메뉴: `Tools/OZGL2/Lobby/Apply Skill Save Flow And Frames`. 대상 Prefab Mode에서 Undo를 지원하며 자동으로 Scene을 저장하지 않는다. 기존 확인창 수동 레이아웃은 재실행으로 덮어쓰지 않는다.
- 검증 메뉴: `Tools/OZGL2/Lobby/Validate Skill Save Flow And Frames`. 기존 42개 + 분류색 76개 + 저장/프레임 33개 검사 통과. 기존 팝업 4개의 기본 열기/닫기도 확인했다.
- 확인창 취소/로비로 버튼 Raycast와 뒤쪽 UI 입력 차단 통과. 첫 Raycast는 Canvas 렌더 전이라 실패했고, 실제 화면 렌더 뒤 동일 검사로 통과했다. 검사 도구는 활성화/레이아웃 갱신 후 프레임에 실행해야 한다.
- 확인 문구 글리프 누락/잘림 없음, Missing Script/끊어진 Prefab 참조 0개, 최종 Console 오류/경고 0개. 수정 후 컴파일 성공.
- ESC와 동일한 Controller 진입 경로를 검증했다. 실제 하드웨어 ESC 입력, 비 16:9, 플레이어 빌드는 미확인이다.
- 대상 Scene SHA256은 변경 전후 동일. 다른 Scene/메뉴/특성/ProjectSettings/Packages 및 실제 게임 저장 데이터는 이번 변경에서 수정하지 않았다. Git에 남아 있는 특성 그라데이션 변경은 앞선 승인 작업이다.
- 화면: `Tools/Art/Previews/SkillSettings_v4_EquippedCategoryFrames.png`, `SkillSettings_v4_UnsavedConfirmation.png`. 분류 비교를 위해 임시로 딜/버프/디버프를 1개씩 장착한 화면이며 초기 저장 시안을 변경한 것은 아니다.
- 생성 Sprite 3개는 내장 imagegen 편집 모드로 제작하고 승인된 배경 정리/크기 정렬만 후처리했다. 원본·프롬프트: `Tools/Art/Sources/SkillEquippedFrames_v4/GENERATION.md`.

## Git 검토

### 빈 슬롯 무채색 프레임 (2026-09-21)

- 빈 슬롯일 때만 기존 프레임에 `Styles/SkillFrame_Empty.mat`을 적용한다. `SkillCategoryUI.shader`의 `SKILL_EMPTY_FRAME` 모드가 원본 명암/알파를 유지하며 채도를 제거한다. 새 이미지를 생성하거나 기존 PNG를 수정하지 않았다.
- `UISkillCategoryStyleSO.EmptyFrameMaterial`은 표시 설정만 공유한다. `UISkillLoadoutPreview`가 장착/해제 갱신 시 Material을 적용/해제하며, 장착 상태에는 기존 분류별 Sprite/정렬이 유지된다. Material 인스턴스를 매번 생성하지 않는다.
- Editor 생성 도구도 동일 규칙을 적용하도록 동기화했다. 이번 실제 적용은 SO/Material만 저장했고, Scene 및 Prefab 파일 SHA256은 변경 전후 동일하다. Material 연결은 Unity Undo를 지원하지만 신규 에셋 파일 생성 자체는 Undo 삭제 대상이 아니다.
- Play Mode 194개 검사 통과(기존 179개 + 빈 슬롯 3개 무채색/분류별 재장착 복원 15개), 최종 Console 오류/경고 0개, Shader 컴파일 오류 없음. Shader 변경 직후 첫 캡처는 임시 컴파일 표시가 나왔고 렌더 갱신 후 정상 화면을 재확인했다.
- Inspector: `SkillCategoryStyle.asset` → Empty Frame Material = SkillFrame_Empty. 프레임의 Raycast/정렬, 저장/나가기/버튼 상태는 유지한다. LayerMask/Tag, Collider/Rigidbody, Animator/Input System 설정 변경 없음.
- 확인 화면: `Tools/Art/Previews/SkillSettings_v6_EmptyNeutralFrames.png`. 비 16:9 및 플레이어 빌드는 미확인이다.

### 확인창 호버 / 장착 프레임 정렬 후속 변경 (2026-09-21)

- 확인창 Cancel/Leave 모두 같은 `Button_Dialog_244x72`와 `UISkillArtButton` 표시 규칙을 사용한다. Normal은 원래 색, Hover는 금색 계열, Pressed는 어두운 색이다. 0.1초 전환을 공통 사용한다.
- 확인창만 `Separate Pointer Hover`를 켠다. 기본 취소 포커스가 이미 호버색처럼 보이던 문제를 해결하며, 키보드 포커스는 글자 색으로 유지한다. 다른 스킬 버튼은 기본 옵션(false)을 유지한다.
- `LobbySkillFrameAlignmentBuilder`는 표시 영역만 조립하는 Editor 도구이다. 기존 적용 메뉴 `Tools/OZGL2/Lobby/Apply Skill Save Flow And Frames` 안에서 실행되고 같은 Undo 그룹을 사용한다.
- 세 슬롯에 FrameArt를 추가하고 프레임 크기/중심을 원본 프레임의 장식 간격에 맞췄다. 246×246 클릭 영역, 슬롯 위치, 아이콘 중심, 내부 색 레이어, 원본 PNG는 그대로이다. 각 프레임의 보정값은 `SkillCategoryStyle.asset`에 저장한다.
- 주요 코드: `UISkillArtButton`(포커스/호버 분리), `UISkillCategoryStyleSO`(표시 배치 설정), `UISkillLoadoutPreview`(스킬 변경 시 프레임만 보정), Editor 생성/검증 도구. Runtime에서 UnityEditor를 참조하지 않는다.
- Play Mode: 기존 42개 + 분류색 76개 + 저장/나가기 33개 + 정렬/호버 28개 = 179개 검사 통과. 확인창 Raycast 정상, Missing Script/끊어진 참조 0개, Console 오류/경고 0개.
- 포인터 진입/이탈/누름 이벤트를 자동 호출해 두 버튼의 렌더 색을 확인했다. 결정적인 호버 정지 화면 촬영 시에만 Runtime fade를 0으로 지정했고, Prefab의 0.1초 설정은 유지했다. 실제 하드웨어 마우스 이동과 비 16:9/플레이어 빌드는 별도 확인 항목이다.
- Scene SHA256 변경 없음. 이번 수정은 스킬 Prefab/스타일 SO/표시 코드/Editor 도구/문서에 한정하며, PNG·메뉴·특성·실제 저장·ProjectSettings·Packages는 변경하지 않았다.
- Inspector: ExitConfirmation의 두 버튼 공통 Sprite/Separate Pointer Hover, First Selected=Cancel, CancelExit/ConfirmExitWithoutSaving 연결, 각 FrameArt/Slot Rect 참조를 확인한다. 충돌 주의 파일은 스킬 Prefab과 공유 스타일 SO이다.
- 검증 화면: `Tools/Art/Previews/SkillSettings_v5_AlignedFrames.png`, `SkillSettings_v5_CancelHover.png`, `SkillSettings_v5_LeaveHover.png`.

### 스킬 분류색 후속 변경 (2026-09-21)

- 공통 흰색 본체 + 분류 외곽선 Material 3개 및 옅은 슬롯 Material/SO/Shader 추가. UI 갱신 시 같은 설정을 목록·장착·상세에 적용한다.
- 기존 42개 기능 검사와 새 분류색 76개 검사 통과. 카테고리 Raycast 정상, Console 오류·경고 0개, Missing Script/끊어진 Prefab 참조 0개.
- 원본 PNG, 카탈로그 수치, Scene 파일, 실제 스킬 저장 데이터는 변경하지 않았다. 적용 화면은 `Tools/Art/Previews/SkillSettings_v3_CategoryColors.png`.
- 다른 그래픽 API/플레이어 빌드/비 16:9 해상도와 마스크 내부 렌더링은 미확인이다. SpriteAtlas 패킹은 현재 미지원이며 독립 Full Rect Sprite를 사용한다.

### 배경 통일 후속 변경 (2026-09-21)

- 특성·스킬 프리팹에 하단 그라데이션을 각각 1개 추가하고 스킬 검은 배경만 30% → 60%로 변경했다. 특성의 기존 67% 오버레이와 트리 경계 페이드는 유지했다.
- 두 생성 도구도 신규 생성 시 로비의 그라데이션을 복사하도록 동기화했다. 원본 이미지/텍스처, Runtime 기능, 메뉴·설정, Scene 파일은 변경하지 않았다.
- Play Mode 스킬 42개 회귀 검사 통과. 활성화 직후 첫 Raycast 검사는 실패했으나 렌더 완료 후 재검사에서 정상 통과했다(검증 도구는 Canvas 갱신 이후 실행 필요). 특성 중앙으로/초기화 버튼 Raycast, 닫기 및 로비 입력 복원을 확인했다.
- 두 화면의 실제 Game View와 동일 텍스처·색·배치 및 입력 가로채기 없음 확인. Console 오류/경고 0개. 비 16:9 해상도와 플레이어 빌드는 미확인이다.

### 최초 스킬 UI 구현 기록

- 기존 파일 변경: `UI_Lobby_MutedPreview.unity`만. 새 프리팹 인스턴스 추가 및 SkillsButton 대상 변경을 확인한다.
- 신규: 스킬 전용 Runtime 3개, Editor 3개, 스킬 아트/카탈로그/프리팹 및 Unity 생성 .meta, 아트 생성 원본/후처리 도구/문서.
- ProjectSettings/Packages/기존 폰트/기존 메뉴·특성 프리팹 변경 없음.
- Scene은 다른 작업과 충돌할 수 있으므로 병합 전 SkillsButton 연결과 프리팹 인스턴스를 확인한다.
- `git diff --check`는 Unity가 저장한 Scene의 빈 `value:` / `m_Name:` 뒤 공백 2개를 보고한다. Scene 텍스트 직접 편집 금지 규칙에 따라 수동 정리하지 않았다.
- 생성 방식과 프롬프트 구성은 저장소 `Tools/Art/Sources/SkillSettings_v1/GENERATION.md`에 기록한다.
