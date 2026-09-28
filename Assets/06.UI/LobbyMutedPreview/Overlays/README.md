# 로비 메뉴 / 특성 Overlay 시안

## 현재 관리 기준 — 2026-09-21

- 스킬 세팅도 `Canvas_LobbyOverlays/Canvas_SkillSettings` 중첩 Prefab으로 관리한다. 원본 스킬 Prefab과 디자인은 유지한다.
- 앞으로 **도감·업적도 Canvas_LobbyOverlays 아래에 추가**한다. 기존 구형 팝업의 즉시 재구현은 이번 범위가 아니다.
- 현재 구조·연결·Undo·검증 규칙은 `Docs/UI/LobbyOverlayManagement.md`를 따른다.
- 아래는 최초 메뉴/특성 시안 제작 당시 기록이다. 과거의 “스킬은 기존 팝업 사용” 및 특성 미구현 설명은 현재 상태를 나타내지 않는다.

## 적용 범위

- 적용 씬: `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`만.
- 기존 `UI_Lobby.unity`, 공용 `UIPopupController`, 기존 특성 시스템은 변경하지 않았습니다.
- 기존 `Popup_Settings`, `Popup_Traits`는 비활성 상태로 보존합니다. 로비 진입 버튼 2개만 새 Canvas를 열도록 연결했습니다.
- 스킬/도감 버튼은 기존 팝업을 그대로 엽니다.
- 특성 트리 전체/연결선/능력 효과/포인트 소비는 이번 구현 범위가 아닙니다.

## Prefab 구성

- `Prefabs/Canvas_LobbyOverlays.prefab`: Screen Space Overlay Canvas, Sorting Order 200, 1920 x 1080 기준. 메뉴, 설정, 전체 화면 특성을 포함합니다.
- `Prefabs/TraitFrame_Normal.prefab`: 상아색 일반 특성 테두리, 별도 Icon Image.
- `Prefabs/TraitFrame_Specialized.prefab`: 가넷색 특화 특성 테두리, 별도 Icon Image.
- 두 특성 Prefab은 Canvas 내부에 각각 하나씩 미리보기로 배치했습니다. 실제 특성 배치를 의미하지 않습니다.
- Prefab에는 외부 씬 참조가 없습니다. Canvas 씬 인스턴스에만 아래 세 참조가 override로 연결됩니다.

## Inspector 연결

`Canvas_LobbyOverlays / UILobbyOverlayView`

- Lobby Group: `Canvas_Lobby / CanvasGroup`
- Legacy Popup Controller: `UI_Root / UIPopupController`
- Legacy Raycaster: `Canvas_Popups / GraphicRaycaster`
- 내부 메뉴/설정/특성 화면 및 첫 선택 버튼: 연결 완료.
- EventSystem은 기존 씬의 하나를 사용합니다. Prefab 안에 중복 생성하지 않습니다.
- LayerMask / Tag / Collider / Rigidbody / Animator / Input Action 설정 변경 없음.

## 동작

- 메뉴 버튼 또는 로비에서 ESC: 메뉴 열기.
- 메뉴 > 설정: 동일 암막 강도의 설정창으로 전환. 돌아가기/ESC는 메뉴로 복귀.
- 메뉴 > 돌아가기/ESC: 원래 화면으로 복귀.
- 특성: 상하좌우 드래그 이동, 중앙으로 버튼으로 시점 복원.
- 특성 클릭: 우측 설명창 표시. 호버 설명과 클릭 강화는 연결하지 않았습니다.
- 특성에서 ESC: 설명창이 있으면 먼저 닫고, 없으면 로비로 복귀.
- 특성의 뒤로 버튼: 설명창 포함 특성 화면 닫기.
- 새 Overlay가 열린 동안 로비 입력 및 기존 팝업 입력을 차단합니다. 닫거나 컴포넌트를 비활성화하면 이전 상태를 복원합니다.
- 기존 스킬/도감이 열려 있을 때 ESC는 기존 창만 닫습니다. 같은 키로 새 메뉴가 다시 열리지 않습니다.

## 이후 특성 배치

1. 두 프레임 Prefab 중 하나를 `TraitsScreen/TraitTreeViewport/TraitTreeContent` 아래 배치합니다.
2. `Icon / Image / Source Image`에 아이콘을 지정합니다. 비어 있던 Icon은 Play Mode에서 자동 활성화됩니다. 편집 화면에서도 바로 보려면 Image의 Enabled를 켭니다.
3. `UITraitFrameView`의 Display Name / Description을 지정합니다.
4. 런타임 데이터 연결은 `SetContent(Sprite icon, string displayName, string description)`을 사용합니다.
5. 실제 포인트 표시에는 `UITraitOverlayView.SetAvailablePoints(int)`를 호출합니다. 연결 전에는 `-`로 표시합니다.
6. 최종 트리 배치 시 `Preview_...` 2개와 `PreviewLabel_...`, `PreviewNote`, `TreePending` 안내만 제거/대체합니다.

테두리, 아이콘, 선택 표시, 설명 텍스트는 독립되어 있습니다. 프레임을 다시 생성할 필요가 없습니다.

## 기능 연결 대기 항목

- 음향: 기존에도 오디오 연결이 없었습니다. 이번에는 메뉴/설정의 토글 상태 동기화와 `Bgm Enabled Changed`, `Sfx Enabled Changed` 이벤트만 제공합니다. 실제 AudioMixer/저장 시스템 연결은 후속 작업입니다.
- 게임 종료: 기존 임시 항목을 대신해 `Quit Requested` 이벤트를 준비했습니다. 연결 전 버튼은 비활성입니다. 실제 종료 기능 연결 후 Button의 Interactable을 켭니다.
- 특성 데이터/해금/획득/강화/저장 및 전투 일시정지(Time.timeScale)는 구현하지 않았습니다.
- 레벨 표시는 기존 로비 표시를 복제한 시안이며, 새 데이터 구독은 추가하지 않았습니다.
- 기존 기획에 없는 해상도/키 설정 등의 항목은 추가하지 않았습니다.

## 코드 책임

- `UILobbyOverlayView.cs`: 화면 전환, ESC 우선순위, 입력 잠금/복원, 음향 UI 상태 및 외부 이벤트.
- `UITraitOverlayView.cs`: 클릭된 특성의 우측 설명 표시, 포인트 표시, 시점 초기화.
- `UITraitFrameView.cs`: 교체 가능한 아이콘/이름/설명과 클릭 선택. 기존 `UITraitNodeView`와 독립.
- `LobbyOverlayPreviewBuilder.cs`: Editor 전용 리소스 가져오기/Canvas 및 Prefab 조립.

Editor 메뉴: `Tools/OZGL2/Lobby/Build Menu And Traits Overlay Preview`

기존 Canvas/Prefab을 덮어쓰거나 중복 생성하지 않도록 가드가 있습니다. 조립 완료 후에는 기존 Prefab을 편집하세요. 씬 객체 생성과 로비 버튼 연결은 Undo 그룹에 기록되며, 디스크에 생성한 이미지/폰트/Prefab 에셋 파일 생성 자체는 Undo 대상이 아닙니다.

## 리소스

- 내장 image_gen으로 6종 생성: Panel_Frame, Button_Frame, Header_Ornament, Points_Frame, Trait_Frame_Normal, Trait_Frame_Specialized.
- `Source`: 생성 원본, `Sprites`: 투명화한 Unity용 PNG, `Fonts`: 기존 로비와 독립된 DOSMyungjo 픽셀 아틀라스/외곽선 Material.
- 외부 원본 폰트/기존 로비 폰트 에셋은 수정하지 않았습니다. 기존 Pixel TMP Outline Shader는 재사용합니다.
- 프레임과 버튼은 9-slice, 장식 및 노드 프레임은 비율 유지. 스프라이트는 Point / No Mipmaps / Uncompressed.
- 좌우 그라데이션은 별도 UI 텍스처이며 입력을 가리지 않습니다.
- 생성 프롬프트와 배경 처리 과정은 `GENERATION.md` 참조.

## 검증 기록 (2026-09-17)

- Unity 컴파일 완료, Console Error/Warning 0개 확인.
- 전체 씬 Missing Script 0개, 새 Canvas 필수 Inspector 참조 연결 확인.
- Play Mode 1920 x 1080 메뉴/특성/우측 설명창 렌더 확인.
- 메뉴 입력 차단, 메뉴-설정 왕복, 음향 표시 동기화, 닫기/컴포넌트 비활성 시 입력 복구 확인.
- 특성 클릭 설명, 단일 선택 표시, 선택 시 포인트 표시 불변 확인.
- ScrollRect 드래그로 X/Y 양쪽 이동 및 중앙 복원 확인.
- 아이콘 교체가 노드/설명창에 반영되는 것 확인.
- 입력 이벤트를 주입해 ESC 메뉴 열기/닫기 확인. 기존 스킬 창 ESC 종료 시 새 메뉴가 열리지 않는 것 확인. 테스트용 입력 장치는 제거했습니다.
- 실제 UI Raycast가 메뉴 암막에 막혀 뒤쪽 로비 버튼까지 전달되지 않는 것 확인.
- 기존 스킬/도감 팝업 열기/닫기 확인.
- 플레이어 빌드, 실제 오디오 변경/저장, 실제 종료 동작은 미검증/미연결입니다.

## Git 검토

- Scene 변경: `UI_Lobby_MutedPreview.unity` (충돌 주의). 새 Canvas 인스턴스와 메뉴/특성 버튼 연결을 확인하세요.
- Prefab: 새 파일 3개. 기존 Prefab 변경 없음.
- Meta: 새 에셋/폴더/스크립트에 대해 Unity가 자동 생성했습니다. 직접 편집하지 않았습니다.
- ProjectSettings / Packages 변경 없음. 작업 시작 전부터 있던 TMP Essentials/ShaderGraph 변경은 보존했습니다.
- 원본 `UI_Lobby.unity`, `UIPopupController.cs`, 기존 `ShaderGraphSettings.asset`은 작업 전후 해시 동일.
- 가장 큰 신규 파일은 폰트 아틀라스 약 8.4 MB이며 50 MB 초과 파일 없음.
- Unity가 직렬화하는 빈 필드의 공백 때문에 씬 `git diff --check`에 trailing whitespace가 표시됩니다. Scene 텍스트를 직접 수정하지 않았습니다.
- 커밋/푸시하지 않았습니다.
