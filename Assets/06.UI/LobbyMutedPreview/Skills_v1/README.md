# 스킬 세팅 아트 미리보기 v1

## 적용 범위

- 대상 Scene: `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`
- 새 Prefab: `Prefabs/Canvas_SkillSettings.prefab`
- Scene의 `Canvas_Popups/Canvas_SkillSettings`에 배치하고 `Canvas_Lobby`의 `SkillsButton`에서 연다.
- 기존 `Popup_Skills`, 메뉴·설정·특성 Prefab 및 다른 Scene은 변경하지 않는다.
- Scene의 뒤로 버튼만 기존 `UIPopupController.CloseTopPopup`에 연결한 인스턴스 override이다. 다른 씬에 재사용할 때 이 연결과 PopupRoot 등록이 필요하다.
- 배경은 특성창처럼 공용 `LobbyMutedPreview/Sprites/Lobby_Background.png`를 재사용하고, `BackgroundShade`에 검은색 불투명도 30%(Alpha 0.30)를 적용한다. 생성했던 `Skill_Background.png`는 미사용 원본으로 보존한다.

## 구성 / 책임

- 생성 PNG 39개: 최초 37개(미사용 배경 1개 포함) + 주황 호버/금색 선택 프레임 2개. 기존 보라색 선택 프레임은 보존하지만 목록과 상세 화면에서 사용하지 않는다. 글자는 이미지에 포함하지 않는다.
- `UISkillArtButton`: 일반·호버·누름·선택·비활성 Sprite 전환과 별도 TMP Label 색을 담당한다. 카테고리의 실제 선택 상태는 키보드 포커스와 별개이다.
- `UISkillLoadoutPreview`: 전체/딜/버프/디버프 필터, 3개 슬롯 임시 장착/해제, 상세 표시를 담당한다.
- `UISkillPreviewCatalogSO`: 이미지 검토용 12개 스킬의 이름·수치·분류·아이콘만 가진다. 실제 게임 밸런스/해금 데이터가 아니다.
- 런타임 상태는 컴포넌트의 임시 배열에만 저장하며 ScriptableObject를 변경하지 않는다.

## 저장 범위 — 실제 게임 저장 아님

`저장`은 같은 Play Mode 실행 중 다시 열었을 때 유지할 **미리보기 장착 상태**만 확정한다. 저장하지 않은 편집은 창을 닫았다 열면 취소된다. Play Mode 종료 시 초기 시안으로 돌아간다.

기존 `SkillTreeStore`, PlayerPrefs, 전투 장착 데이터는 변경하지 않는다. 실제 시스템 연동은 별도 작업이며 `SaveRequested`는 아직 연결되지 않았다. 실제 연동 시에는 저장뿐 아니라 보유/해금 목록 및 기존 장착 상태를 받아오는 어댑터도 필요하다.

## Inspector 체크

- 문구: 각 버튼의 `Label`에 있는 TMP Text를 수정한다.
- 상태별 이미지: `UISkillArtButton`의 Normal/Hover/Pressed/Chosen/Disabled를 확인한다.
- 목록 카드만 `Keep Chosen While Pressed`가 켜져 있다. 호버는 `Card_Hover_Amber`, 선택은 `Card_Selected_Gold`를 사용하며 선택 후 재누름/이탈에도 금색을 유지한다. 탭/장착 슬롯/기타 버튼 동작은 유지한다.
- 카드와 장착 슬롯은 아이콘과 프레임이 별도 Image이다. 아이콘 교체 시 프레임은 유지한다.
- 폰트: 기존 `01.Font/DOSMyungjo Overlay Pixel.asset`와 Outline Material을 재사용한다. Git에서 제외된 폰트는 공유 드라이브에서 **기존 .meta와 함께** 복원해야 한다.
- 입력: 기존 EventSystem/Input System 및 PopupController를 재사용한다. 새 EventSystem/입력 액션은 추가하지 않았다.
- LayerMask/Tag, Collider/Rigidbody/IsTrigger, Animator/Animation Event 설정 변경은 없다.

## Editor 도구

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

## Git 검토

- 기존 파일 변경: `UI_Lobby_MutedPreview.unity`만. 새 프리팹 인스턴스 추가 및 SkillsButton 대상 변경을 확인한다.
- 신규: 스킬 전용 Runtime 3개, Editor 3개, 스킬 아트/카탈로그/프리팹 및 Unity 생성 .meta, 아트 생성 원본/후처리 도구/문서.
- ProjectSettings/Packages/기존 폰트/기존 메뉴·특성 프리팹 변경 없음.
- Scene은 다른 작업과 충돌할 수 있으므로 병합 전 SkillsButton 연결과 프리팹 인스턴스를 확인한다.
- `git diff --check`는 Unity가 저장한 Scene의 빈 `value:` / `m_Name:` 뒤 공백 2개를 보고한다. Scene 텍스트 직접 편집 금지 규칙에 따라 수동 정리하지 않았다.
- 생성 방식과 프롬프트 구성은 저장소 `Tools/Art/Sources/SkillSettings_v1/GENERATION.md`에 기록한다.
