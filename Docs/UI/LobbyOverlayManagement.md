# 로비 Overlay 관리 규칙

## 적용 범위

- 대상은 `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`이다. 다른 Scene에는 자동 적용하지 않는다.
- 로비의 새 하위 화면은 `Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab` 아래에서 관리한다.
- 메뉴·설정·특성·스킬 세팅·도감·업적을 이 루트의 자식 화면으로 관리한다. 도감/업적 데이터와 해금은 [LobbyCollectionsPreview.md](LobbyCollectionsPreview.md)를 따른다.
- 별도 원본 Prefab을 사용하는 도감/업적은 공용 루트 안에서 중첩 Prefab 연결을 유지한다. 현재 스킬 화면은 부모 Prefab의 일반 자식으로 관리한다. 기존 구조를 임의로 Unpack하거나 화면 전체를 복제하지 않는다.

## 관리 구조

```text
Canvas_LobbyOverlays                 # 공용 루트 / CanvasScaler / UILobbyOverlayView
├─ TraitsScreen                     # 기존 특성
├─ MenuPopup                        # 기존 메뉴
├─ SettingsPopup                    # 기존 설정
├─ Canvas_SkillSettings              # 부모 Prefab에 포함된 일반 자식 화면, 19종 v2
│  └─ ExitConfirmation              # 미저장 확인창
├─ Canvas_UnitCodex                  # 유닛 도감 중첩 Prefab
└─ Canvas_Achievements               # 업적 중첩 Prefab
```

`Assets/06.UI/LobbyMutedPreview/Skills_v1/Prefabs/Canvas_SkillSettings.prefab`은 이전 시안의 참고용 원본으로 보존한다. 현재 부모 안의 스킬 화면은 이 에셋의 중첩 인스턴스가 아니므로 수정 내용을 옛 원본에 Apply하지 않는다. 공통 분류 스타일은 `Skills_v1/Styles`를 재사용하고, 현재 19종 카탈로그/아이콘/궁극기 장식은 `Skills_v2`에 둔다. [SkillRoster_v2.md](SkillRoster_v2.md)의 데이터와 적용 절차를 따른다. `Canvas_Popups`의 옛 팝업들은 보존하며 새 스킬 화면의 중복 인스턴스는 만들지 않는다.

## 화면 전환과 입력

- 로비 `SkillsButton` → `Canvas_LobbyOverlays/UILobbyOverlayView.OpenSkills`.
- 스킬 `Back` → 같은 루트의 `UILobbyOverlayView.CloseTop`.
- 루트 `UIPopupController`가 스킬과 확인창의 스택을 관리한다. `Popup Root`는 자기 Transform, `Screen Group`은 Scene 인스턴스에서 `Canvas_Lobby/CanvasGroup`에 연결한다.
- 도감/업적은 루트 직계 자식에 `UIPopupPanel`을 두고 `UILobbyOverlayView.OpenOverlayPopup(panel)`으로 연다. 로비 `CodexButton`/`ReservedButton`을 각각 연결했다. 메뉴·설정·특성의 기존 처리 방식은 유지한다.
- 새 화면과 기존 화면을 동시에 열지 않는다. 열린 동안 로비 입력을 차단하고 닫을 때 이전 선택을 복원한다.
- 스킬이 열려 있으면 ESC는 Overlay의 Controller만 처리한다. 같은 ESC로 스킬을 닫고 메뉴를 다시 열면 안 된다.
- 일반 뒤로/ESC는 `CloseTopPopup`을 거친다. `CloseConfirmedPopup`으로 미저장 확인을 우회하지 않는다. 강제 정리는 루트 종료 때만 사용한다.
- 스킬 미저장 확인은 기존 `UISkillLoadoutPreview`가 맡는다. 공용 루트는 스킬 장착/포인트/도감/업적 데이터를 소유하지 않는다.
- 실제 게임 저장 연동은 별도이다. 현재 스킬 저장은 같은 Play 세션의 UI 미리보기 상태이다.
- `UILobbyCollectionState`는 같은 루트에 별도 컴포넌트로 두며, 도감·스킬의 해금 및 업적 진행도만 공유한다. 전환 담당 `UILobbyOverlayView`에 데이터 책임을 넣지 않는다.

## Canvas / Prefab 연결

- 루트 기준 해상도 1920×1080, Sorting Order 200. 화면마다 중복 EventSystem/CanvasScaler를 만들지 않는다.
- 스킬 화면의 Canvas는 210, 확인창은 211이다. `Override Sorting`과 각 Canvas의 `GraphicRaycaster`를 유지한다.
- 새 전체 화면도 같은 계층 규칙으로 동시에 하나만 열고, 정렬 값을 무작정 높이지 않는다.
- 스킬 Back/Controller 및 공유 상태 참조는 **부모 Prefab 내부의 일반 자식 참조**로 저장한다. 참고용 `Skills_v1` 원본에는 Scene 객체를 연결하지 않는다. 도감/업적의 부모 연결은 각 중첩 Prefab의 override로 유지한다.
- `Screen Group`, 기존 로비/Legacy Controller/Raycaster 참조는 **Scene 인스턴스 override**이다. 부모 전체 Apply로 다른 Scene 참조나 수동 조정을 밀어 넣지 않는다.
- 시작 시 화면/확인창은 비활성, 공용 루트는 활성이다.
- 아트·TMP·그라데이션·분류색·저장 상태 규칙은 유지한다. [SkillCategoryVisualRules.md](SkillCategoryVisualRules.md)를 함께 따른다.

## Editor 도구

### 공통 뒤로 버튼

- 디자인 기준은 `TraitsScreen/Back`이다. 스킬·도감·업적의 `Back`은 같은 위치 `(765, 451)`, 크기 `268×85`, `Button_Back_268x85` 이미지를 사용한다.
- `Label`은 특성과 동일한 중앙 정렬, 32 크기의 `뒤로` 문구·폰트·머티리얼·색상을 사용한다. 별도 `Arrow`는 삭제하지 않고 비활성화한다.
- 시각 설정만 부모 `Canvas_LobbyOverlays.prefab`에 저장한다. 도감/업적은 중첩 인스턴스 override로 유지하며 원본 Prefab을 변경하지 않는다.
- 기존 Button 컴포넌트와 클릭 연결은 유지한다. 특성은 `CloseTraits`, 나머지 세 탭은 `CloseTop`으로 닫고, 스킬 미저장 확인을 우회하지 않는다.
- 2026-09-22: 세 탭의 렌더 후 EventSystem Raycast/포인터 클릭으로 닫힘 확인. 스킬 미저장 뒤로 → 확인창 → 취소 시 편집 유지 → 폐기 시 닫힘/저장 상태 복구 확인. 스킬 회귀 검사 791항목, 도감/업적 통합 검사 226항목 통과, Console 오류·경고 0개.
- 저장 전후 세 Back 바깥의 2,341개 컴포넌트는 동일했다. 작업 시작 시 존재하던 Scene 미저장 변경과 override 159개는 보존하고 Scene을 별도로 저장하지 않았다.

### 조립 및 검증 메뉴

- 현재 스킬 조립: `Tools/OZGL2/Lobby/Build Skill Roster V2 (19 Skills)`. 대상 Scene의 Edit Mode 또는 부모 `Canvas_LobbyOverlays`의 Prefab Mode에서 실행한다. Scene에서 시작하면 부모 Prefab Mode를 연다.
- 기존 일반 자식 `Canvas_SkillSettings` 안에서 19개 카드/3열 스크롤/설명/메타데이터/궁극기 장식을 갱신한다. 다른 Overlay와 Scene 인스턴스의 수동 override는 전체 Apply하지 않는다.
- Prefab Mode의 오브젝트/참조 변경은 Unity Undo를 지원한다. 결과 확인 후 부모 Prefab은 수동 저장하며 Scene은 자동 저장하지 않는다. 새 카탈로그/폴더 생성 자체는 Undo 삭제 대상이 아니고 카탈로그 값은 에셋에 저장하므로, 복구 시 Git Diff도 함께 확인한다.
- 이전 `Integrate Skill Settings Into Lobby Overlays` 및 12종 스킬 생성 도구는 초기 통합용 기록이다. 현재 일반 자식/v2 화면을 재생성하는 용도로 사용하지 않는다. 참고용 `Skills_v1` 원본은 덮어쓰지 않는다.
- 스킬 검증: `Tools/OZGL2/Lobby/Validate Skill Roster V2`, `Tools/OZGL2/Lobby/Validate Skill Roster V2 Text Layout`. 현재 도감/업적은 [LobbyCollectionsPreview.md](LobbyCollectionsPreview.md)의 검사 방법도 함께 따른다.

## 확인 목록

1. 부모 Prefab과 Scene의 부모 인스턴스에 스킬 화면이 하나씩 있고, 부모 내부에서 일반 자식으로 유지되는지 확인한다. 도감/업적은 각각 중첩 Prefab 연결을 유지한다.
2. SkillsButton/Back, Popup Root/Screen Group, 공유 `UILobbyCollectionState` 참조의 누락을 확인한다.
3. 열기 → 저장 활성/비활성 → 미저장 뒤로/ESC → 취소/로비로 → 재열기를 확인한다.
4. 메뉴·설정·특성, 기존 도감·업적 팝업과 로비 입력 복원을 확인한다.
5. 실제 Raycast, 선택 포커스, Canvas 순서, 화면 비율을 확인한다.
6. Console 오류, Missing Script/Reference, 실제 저장 데이터 불변을 확인한다.
7. 스킬 19개/필터별 9·4·6개, 세로 스크롤 끝의 심판 선택, 티어·발동·해금 SP 설명, 궁극기 목록/상세/장착 장식과 기존 흰색 분류 표시를 확인한다.

Scene/부모 Prefab은 충돌 주의 파일이다. `.unity`/`.prefab`/`.meta`는 텍스트로 수정하지 않는다. ProjectSettings/Packages/입력/물리 설정은 이 통합 범위가 아니다.

## 적용·검증 기록 — 2026-09-21

아래는 최초 12종 시안 통합 당시의 기록이다. 당시의 중첩 스킬 구조와 검사 개수는 현재 일반 자식으로 관리하는 19종 v2 화면의 구조 또는 검증 결과를 의미하지 않는다.

- Unity MCP로 대상 Scene과 부모 Prefab을 저장했다. 스킬 원본 Prefab은 변경하지 않고 중첩 인스턴스 한 개를 추가했으며, 이전 `Canvas_Popups` 아래에는 새 스킬 화면이 남아 있지 않다.
- 작업 시작 때의 미저장 Scene 변경을 승인 범위에 따라 보존해 저장했다. 통합 전후 기존 부모 인스턴스 override 290개가 모두 유지됐다. 메뉴/특성 조정값을 부모 에셋 전체에 Apply하지 않았다.
- `UILobbyOverlayView`: 스킬/향후 하위 화면 진입, 중복 열기 차단, 닫기 위임만 추가했다. 공용 `UIPopupController`, 스킬 Runtime 데이터, 원본 아트/폰트는 변경하지 않았다.
- 새 Editor 클래스는 통합 조립과 검증 책임을 각각 분리했다. 기존 스킬 생성/검증 도구도 새 부모 Controller를 사용한다. Runtime 코드에 UnityEditor 의존성은 추가하지 않았다.
- Play Mode 자동 검사 **227개 통과**: 통합 33 + 기존 스킬 42 + 분류 표시 76 + 저장/프레임 48 + 정렬/호버 28.
- 가상 Keyboard 이벤트와 실행 순서에 따른 컴포넌트별 Update 호출로 ESC 경로 **6개 통과**: 저장 상태 닫기, 미저장 확인, 확인창 취소, 변경 폐기 후 복귀, 로비 메뉴 열기, 메뉴 닫기. 실제 하드웨어 입력은 별도 확인 항목이다.
- 실제 Game View 렌더 후 카테고리 클릭, 확인창 취소/로비로 클릭, 뒤쪽 UI 차단을 확인했다. 렌더 전에는 Graphic depth가 -1이라 Raycast 검사가 실패할 수 있어 렌더 완료 후 검사했다.
- 비활성 Canvas의 정렬은 직렬화된 `m_OverrideSorting`/`m_SortingOrder`로 설정해 저장을 확인했다. Play Mode에서 스킬 210 / 확인창 211을 검증했다.
- 기존 메뉴/설정/특성, 보존된 구형 팝업 4개의 열기/닫기와 로비 입력 복원 통과. 실제 스킬 PlayerPrefs 값 불변.
- 컴파일 성공, 최종 Console 오류·경고 0개. Overlay 하위 Missing Script/끊어진 참조/TMP 폰트 누락 각각 0개.
- 통합 도구 재실행 시 중복 생성이나 Scene dirty 변경 없음. 종료 상태는 편집 모드, Scene 저장 완료이다.
- 실제 마우스/키보드 수동 조작, 비 16:9 해상도, 플레이어 빌드는 미확인이다. 테스트 캡처는 Git 제외 `Temp/LobbyOverlayChecks`에 보관했다.
- 변경 파일: 대상 Scene, 부모 Prefab, `UILobbyOverlayView`, Editor 통합/검증 도구와 기존 스킬 도구, 두 README, 이 문서 및 Unity가 생성한 신규 Editor `.meta`. 다른 Scene/ProjectSettings/Packages 변경 없음.
- 로컬 `AGENTS.md`에도 이 문서를 참조하도록 기록했다. 해당 파일은 기존 Git 제외 상태를 유지하며, 팀 공유 기준은 이 문서와 두 README이다. 커밋/푸시는 수행하지 않았다.
