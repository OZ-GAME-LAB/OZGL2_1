# WavePreviewPanel 전체 유닛 미리보기

## 적용 범위

`WavePreviewPanel`은 웨이브에 포함된 모든 유닛 종류를 읽고, 한 번에 최대 3종을 표시한다. 4종 이상이면 좌우 버튼 또는 키보드 방향키로 한 종류씩 이동한다. 기존의 `기타 N종` 요약을 화면에서 대체하며, 하단의 `1-1 / 1종` 같은 범위·종류 수 문구는 표시하지 않는다.

- 대상 Prefab: `Assets/06.UI/BattleMutedPreview/Prefabs/WavePreviewPanel.prefab`
- 인게임 확인 Scene: `Assets/00.Scenes/Builds/InGame_UIIntegration.unity`
- 인게임 경로: `BattleInGameUI/UI_BattleScreens/Canvas_GetReady/WavePreviewViewport/WavePreview`
- 기존 펼침/접힘 기능: [BattleWavePreviewDisclosure.md](BattleWavePreviewDisclosure.md)
- 새 Runtime: `UIWavePreviewCarousel`, `UIWavePreviewEnemyItem`
- 배치 Editor 도구: `Assets/Editor/WavePreviewCarouselPrefabBuilder.cs`

기존 `UIInGameBattleBridge`, `InGameCurrencyHud`, `UIBattleMutedPreviewView`의 코드는 변경하지 않는다. 이들은 계속 기존 적 표시 참조에 값을 쓸 수 있으며, 해당 참조가 가리키는 `Enemy_0~2` 오브젝트는 삭제하지 않고 비활성화한다. 프레임·제목과 상위 펼침 구조를 유지한다. 제목 구분선은 기존 `HeaderLine`의 ID·시각적 위치·Y 설정·폭을 유지하고, X Anchor를 왼쪽 기준으로 통일한 뒤 자식 `Artwork`로 새 장식을 표시한다. 웨이브 생성, 전투 수치, 저장 데이터는 변경하지 않는다.

## Prefab 구조

```text
WavePreviewPanel / WavePreview (UIWavePreviewCarousel)
├─ HeraldryFrame
├─ Title
├─ HeaderLine
│  └─ Artwork (Image)             # 부모 좌표를 따른 새 제목 구분선
├─ Enemy_0                         # 기존 HUD 참조 보존, 비활성
├─ Enemy_1                         # 기존 HUD 참조 보존, 비활성
├─ Enemy_2                         # 기존 HUD 참조 보존, 비활성
└─ Carousel                        # 부모 RectTransform 전체 stretch
   ├─ EnemyViewport (RectMask2D)
   │  ├─ Content                   # 이 영역만 좌우 이동
   │  │  ├─ EnemyItemTemplate       # 비활성 템플릿
   │  │  │  ├─ Icon (Image)
   │  │  │  ├─ Count (TextMeshProUGUI)
   │  │  │  └─ Name (TextMeshProUGUI)
   │  │  ├─ DesignPreview          # Edit Mode 미리보기, Play Mode에서 숨김
   │  │  └─ EnemyItem_0...          # Runtime 생성 및 재사용
   │  ├─ Separator_1 (Image)       # Content 밖의 고정 구분선
   │  └─ Separator_2 (Image)
   ├─ PreviousButton (Image + Button)
   │  └─ Arrow (Image)
   └─ NextButton (Image + Button)
      └─ Arrow (Image, Z=180°)
```

아이템은 아이콘과 `×수량`을 위쪽에, 이름을 아래쪽에 표시한다. 같은 `HeroId`의 여러 Spawn은 한 항목으로 합산하며, 항목 순서는 웨이브 데이터에서 처음 등장한 순서를 유지한다.

## 정렬과 이동

| 표시 종류 수 | 정렬 | 구분선 | 좌우 버튼 |
| --- | --- | --- | --- |
| 0 | 빈 목록 | 숨김 | 숨김 |
| 1 | 패널 가운데 | 숨김 | 숨김 |
| 2 | 두 항목을 묶어 가운데 | 두 항목 사이 1개 | 숨김 |
| 3 | 같은 폭의 3칸 | 2개 | 숨김 |
| 4 이상 | 같은 폭의 3칸, 한 종류씩 이동 | 표시 칸 사이 2개 고정 | 표시, 양 끝에서 해당 방향 비활성 |

- 한 칸의 폭은 `EnemyViewport` 실제 폭을 `Visible Count`로 나눈 값이다.
- 이동은 DOTween, 기본 `Duration=0.2`, `Ease=OutCubic`이며 unscaled time을 사용한다.
- 이동 중 추가 입력은 받지 않는다. 이동이 끝나면 버튼 상태를 갱신한다.
- 목록을 비활성화하면 Tween을 정리하고 목표 위치에 맞춘다. 재활성화 시 같은 웨이브의 이동 위치를 유지한다.
- 새 웨이브·새 Run·다른 Stage로 바뀌면 첫 항목으로 돌아간다.
- 구분선은 움직이는 `Content`가 아닌 `EnemyViewport`에 둔다. 1·2종의 가운데 정렬에 맞춰 구분선 위치와 표시 수를 조정한다.
- 유닛 사이 구분선은 기존 `Divider_WaveUnit_Flat.png`의 가는 직선 이미지이며, 표시 영역은 `2×95`이다. 폭을 바꿀 때는 구분선 중심을 유지한다.

### 패널 높이 대응

원본 Prefab은 `592×242`, 확인 Scene의 인스턴스는 높이 오버라이드가 적용된 `592×290`이다. `Carousel`은 부모 전체에 stretch하고, 목록과 버튼은 부모의 세로 중앙 Anchor를 기준으로 배치한다. Scene의 높이에 맞추려고 제목·프레임이나 상위 Prefab을 다시 저장하지 않는다.

| 대상 | Anchor / Pivot | Size Delta | Anchored Position |
| --- | --- | --- | --- |
| `Carousel` | Min `(0,0)`, Max `(1,1)`, Pivot `(0,1)` | `(0,0)` | `(0,0)`, offsets 모두 0 |
| `EnemyViewport` | Min `(0,0.5)`, Max `(1,0.5)`, Pivot `(0,1)` | `(-160,103)` | `(80,9)` |
| `PreviousButton` | Anchor `(0,0.5)`, Pivot `(0,0.5)` | `(40,94)` | `(35,-42.5)` |
| `NextButton` | Anchor `(1,0.5)`, Pivot `(1,0.5)` | `(40,94)` | `(-35,-42.5)` |

592폭에서 Viewport는 432폭, 아이템은 각각 144폭이다. 목록의 윗변은 242높이에서 패널 상단으로부터 112, 290높이에서 136에 위치한다. 아이콘·수량은 아이템의 가로 중앙을 기준으로 배치하고, 이름은 좌우 stretch로 아이템 폭을 따른다. 상위 제목 구분선과 목록이 겹치지 않는지 실제 Game View에서 함께 확인한다.

## 데이터와 책임

`UIWavePreviewCarousel`은 같은 Scene의 `InGamePrototypeBootstrap`을 찾고 `Changed` 이벤트를 구독한다. Prefab에는 Scene 오브젝트 참조를 저장하지 않는다. 초기 데이터가 아직 준비되지 않았으면 0.5초 간격의 realtime Coroutine으로 다시 연결·조회하며, 비활성화 시 이를 정리한다.

| Stage 상태 | 표시할 웨이브 |
| --- | --- |
| `PREPARATION` 등 기본 상태 | `CurrentRoundNumber` |
| `GENERAL_REWARD`, `AUGMENT` | 다음 라운드 |
| 마지막 라운드의 `GENERAL_REWARD`, `AUGMENT` | 다음 라운드가 없으므로 빈 목록 |

목록은 `SelectedStageDefinition.Rounds[].Spawns`를 읽기만 한다. Stage 인스턴스, Run ID, 표시 라운드가 같으면 목록을 다시 만들지 않으므로, 기존 HUD의 주기적인 업데이트나 보상 화면에서 증강 화면으로의 전환이 이동 위치를 초기화하지 않는다.

표시 이름은 HeroPool의 해당 Prefab에 연결된 `UnitBase.statData.displayName`을 우선한다. 없으면 UnitCatalog의 표시 이름, 마지막으로 `HeroId`를 사용한다. 기본 초상화는 UnitCatalog의 HERO 항목인 `unit.{HeroId}`에서 읽으며, `Portrait Overrides`가 있으면 해당 Sprite를 우선한다. Sprite가 없으면 아이콘 Image를 숨긴다.

`UIWavePreviewEnemyItem`은 아이콘·이름·합산 수량을 출력하는 역할만 가진다. 게임플레이 데이터나 입력 처리는 소유하지 않는다. 아이템은 필요한 수만큼 생성하고 이후 재사용한다.

## Inspector 연결

배치 도구가 아래 참조를 연결한다. 직접 조정할 때 연결을 지우거나 기존 `Enemy_0~2`를 삭제하지 않는다.

| `UIWavePreviewCarousel` 필드 | 연결 |
| --- | --- |
| `Viewport` | `Carousel/EnemyViewport` |
| `Content` | `Carousel/EnemyViewport/Content` |
| `Item Template` | `Content/EnemyItemTemplate`의 `UIWavePreviewEnemyItem` |
| `Previous Button`, `Next Button` | 해당 Button |
| `Separators` | Viewport의 `Separator_1`, `Separator_2` |
| `Visible Count` | `3` |
| `Legacy Rows` | 기존 `Enemy_0`, `Enemy_1`, `Enemy_2` |
| `Design Time Preview` | `Content/DesignPreview` |
| `Bootstrap` | Prefab에서는 None, Runtime에 같은 Scene에서 연결 |
| `Unit Catalog` | `Assets/06.UI/LobbyMutedPreview/Heraldry_Codex_v1/UnitCatalog.asset` |
| `Portrait Overrides` | 보스 5종의 HeroId와 전용 Sprite |

`UIWavePreviewEnemyItem`의 `Icon`, `Name Text`, `Count Text`는 각 자식 컴포넌트에 연결한다. 새 이름·수량 텍스트는 `BattleCardBody SDF.asset`과 기존 공통 Material을 사용한다. 텍스트는 Auto Size와 Ellipsis를 사용하며, 이름은 줄바꿈을 허용한다. 텍스트·아이콘·구분선의 Raycast Target은 꺼져 있다.

버튼은 투명한 `40×94` Image를 클릭 영역으로 사용하고, 자식 금색 화살표는 `28×48`로 표시한다. `Button.targetGraphic`은 화살표이다. 버튼의 `Navigation`은 None이며, OnClick 런타임 리스너는 컴포넌트가 등록·해제한다. Inspector에 `MovePrevious` 또는 `MoveNext`를 중복 등록하지 않는다.

`HeaderLine`은 기존 Image 컴포넌트를 비활성화하고 자식 `Artwork`의 Image를 사용한다. 부모의 FileID, `490×1` 크기·Pivot·Y 설정과 시각적 위치를 유지하면서 높이가 필요한 마름모·점 장식을 표시하기 위한 구조이다. `Artwork`는 가로 stretch, Anchor Min `(0,0.5)` / Max `(1,0.5)`, Pivot `(0.5,0.5)`, Size Delta `(0,12)`, Anchored Position `(0,0)`이다. 부모 폭을 따라 12높이로 표시하며 Raycast Target은 꺼져 있다.

원본 `HeaderLine`의 X Anchor는 `0.5`, Anchored Position X는 `-245`였지만, 인게임 Scene의 기존 X 오버라이드는 `51`이었다. 새 자식 그림을 붙였을 때 이 차이로 그림이 오른쪽으로 296만큼 밀리는 문제가 나타났다. 배치 도구는 `position.x += parentWidth * oldAnchorX` 후 X Anchor Min/Max를 `0`으로 바꾼다. 원본 592폭에서는 `(0.5,-245) → (0,51)`이 되어 원본 시각적 위치를 유지하고 Scene의 기존 X 오버라이드와도 일치한다. Scene 파일을 수정하지 않고 해결하며 Y Anchor·Y 좌표·폭·Pivot·ID는 유지한다.

## 입력 차단

다음 상태에서는 좌우 버튼 호출과 방향키 입력을 무시한다.

- 패널 또는 컴포넌트가 비활성 상태
- 같은 Scene의 Bootstrap이 없거나 `IsUiInputBlocked=true`
- 목록 이동 Tween 실행 중
- 상위 `UIWavePreviewDisclosure`가 접혀 있거나 펼침/접힘 이동 중
- 같은 Scene의 활성 `UIPopupController.OpenCount > 0`
- 상위 활성 CanvasGroup의 alpha가 0.01 이하이거나 Interactable / Blocks Raycasts가 꺼짐
- EventSystem 선택 대상이 `TMP_InputField` 또는 기존 `InputField` 아래에 있음

키보드는 기존 Input System의 `Keyboard.current`에서 Left/Right Arrow를 읽는다. 별도의 Input Action 연결이나 ProjectSettings 변경은 필요하지 않다. 양 방향키가 같은 프레임에 함께 눌리면 이동하지 않는다.

## 이미지와 보스 초상화

- 새 장식 PNG/Sprite 폴더: `Assets/06.UI/BattleMutedPreview/WaveCarousel_v1/`
- 이미지 생성 기록: `Tools/Art/Sources/WaveCarousel_v1/Prompt.txt` — built-in imagegen으로 각 장식을 별도 호출해 생성
- 보스 초상화: `WaveCarousel_v1/Portraits/{HeroId}.png` 및 `{HeroId}.asset`

| 용도 | PNG | UI 적용 Sprite | 표시 |
| --- | --- | --- | --- |
| 좌우 이동 | `Arrow_WavePrevious.png` | `Arrow_WavePrevious.asset` | 장식을 줄인 평면적인 차분한 금색 Chevron, 오른쪽은 180° 회전 |
| 제목 아래 구분선 | `Divider_WaveHeader.png` | `Divider_WaveHeader.asset` | 가로선과 마름모·점 장식, `HeaderLine/Artwork`에 적용 |
| 유닛 사이 구분선 | `FlatReference_v1/Sprites/Divider_WaveUnit_Flat.png` | 기존 PNG의 Sprite | 가는 직선, `Separator_1~2`에 `2×95`로 적용 |

화살표와 제목 구분선 PNG는 built-in imagegen으로 생성한 전용 투명 이미지이다. 화살표는 이전의 입체적인 금속 장식에서 단순한 Chevron으로 바꾸며, 같은 Sprite와 회전으로 좌우 스타일을 맞춘다. 배치 도구는 불투명 픽셀 경계와 3px 여백으로 Sprite 영역을 잡는다. 기존 `.asset`이 있으면 내용만 갱신해 GUID와 연결을 유지한다. 유닛 구분선은 기존 `Divider_WaveUnit_Flat.png`를 그대로 사용한다. 생성했던 `Divider_WaveUnits.png/.asset`은 현재 Prefab과 배치 도구에서 사용하지 않는 시안이다.

UnitCatalog에 없는 보스는 원본 조립 Prefab 전체를 `PreviewRenderUtility`로 256×256 투명 PNG에 렌더한다. 임시 복제본의 Canvas와 Shadow만 숨기며 원본 Prefab을 변경하지 않는다.

초기 `EndStaticPreview()`는 결과를 RGB24로 변환해 알파를 잃고 검정 배경을 만들었다. 현재는 `BeginPreview()` / `EndPreview()`의 RenderTexture를 ARGB32/sRGB 임시 RenderTexture로 옮긴 뒤 RGBA32 Texture2D로 읽어 PNG에 저장한다. Linear 프로젝트의 색 공간 변환과 고정 256px 출력을 적용하며, 투명 픽셀과 본체 픽셀이 모두 존재하는지 검사한다. 임시 RenderTexture, 활성 렌더 대상, sRGB 쓰기 상태와 복제본은 종료 시 정리·복구한다.

| HeroId | 원본 Prefab |
| --- | --- |
| `H_BOSS_01` | `Assets/02.Prefabs/Hero_Unit/Boss/Boss_1.prefab` |
| `H_BOSS_02` | `Assets/02.Prefabs/Hero_Unit/Boss/Boss_2.prefab` |
| `H_BOSS_FINAL_01` | `Assets/02.Prefabs/Hero_Unit/Boss/Boss_3.prefab` |
| `H_BOSS_04` | `Assets/02.Prefabs/Hero_Unit/Boss/Boss_4.prefab` |
| `H_BOSS_05` | `Assets/02.Prefabs/Hero_Unit/Boss/Boss_5.prefab` |

이미 PNG가 있으면 다시 렌더하지 않아 디자이너의 수정 결과를 유지한다. 보스 외 사용자 등록 Override도 유지한다. 보스 모습 변경 후 다시 렌더하려면 대상 전용 이미지의 재생성 절차를 별도로 검토한다. 원본 외부 에셋이나 `.meta`를 직접 수정하지 않는다.

## Editor 메뉴와 Undo

모든 메뉴는 Edit Mode 전용이다. `Apply Carousel`은 `Carousel` 내부를 재구성하고 새 장식도 적용한다. 해당 내부의 수동 디자인 수정은 재실행 시 덮어써지므로 배치 도구의 설정과 함께 검토한다. `Apply Artwork`은 기존 Carousel, 아이템과 Runtime 참조를 유지하고 화살표·제목 구분선·유닛 구분선만 바꾼다. 그림만 교체할 때는 `Apply Artwork`을 사용한다.

| 메뉴 | 동작 | Undo |
| --- | --- | --- |
| `Tools/OZGL2/Battle/Wave Preview/Apply Carousel to Saved Prefab` | 대상 Prefab을 로드·배치·저장·언로드 | 저장 파일 변경은 Undo 불가 |
| `Tools/OZGL2/Battle/Wave Preview/Apply Carousel to Open Prefab (Undo)` | 대상 Prefab Mode에서 배치 후 Dirty 처리, 저장은 별도 | 열린 Prefab의 계층·컴포넌트·참조 변경 지원 |
| `Tools/OZGL2/Battle/Wave Preview/Apply Artwork to Saved Prefab` | `ApplySavedArtwork()`으로 기존 배치를 유지하고 장식만 저장 | 저장 파일 변경은 Undo 불가 |
| `Tools/OZGL2/Battle/Wave Preview/Apply Artwork to Open Prefab (Undo)` | `ApplyOpenArtwork()`으로 장식만 변경 후 Dirty 처리, 저장은 별도 | 기존 오브젝트 설정과 `HeaderLine/Artwork` 추가 지원 |

저장형 메뉴는 열린 Prefab에 미저장 변경이 있으면 실행하지 않는다. 열린 Prefab이 있으면 잠시 Main Stage로 전환한 뒤 실행 종료 시 다시 연다. `Apply Carousel`은 장식 이미지와 보스 초상화를 준비하며, `Apply Artwork`은 화살표·제목 구분선 이미지 2개를 준비하고 기존 유닛 구분선 Sprite를 연결한다. TextureImporter 설정·Sprite `.asset` 갱신과 저장 파일 자체는 Undo 대상이 아니다. 모든 메뉴는 Scene을 저장하지 않는다. 장식 메뉴는 Carousel이 이미 설치되어 있어야 실행할 수 있다.

## 캐러셀 기능 적용 검증 — 2026-10-07

Play Mode 검증은 위 인게임 Scene에서 수행했다. 검증용으로 Runtime의 현재 라운드·상태·timeScale 및 데이터 소스를 일시적으로 바꾸어 UI를 검사했다. 일반 30개, Hard 50개, Hell 100개 라운드의 표시 데이터를 검사한 결과이며, 전투를 진행해 180개 웨이브를 완주한 결과는 아니다.

| 항목 | 확인 결과 |
| --- | --- |
| 일반 난이도 30개 웨이브 | 새 Play Mode 진입 후 전체 재검증: 종류 수, HeroId별 합산 수량, 등장 순서, 아이콘 Sprite ID 일치 확인 |
| Hard 50개·Hell 100개 라운드 | Runtime 데이터 소스 일시 교체 후 전체 라운드의 종류 수, 합산 수량, 아이콘 Sprite ID 일치 확인 |
| 일반 웨이브 종류 수 | 1~5: 1종, 6~9: 2종, 10~15: 3종, 16~19: 4종, 20~29: 5종, 30: 6종 |
| 1·2종 | 가운데 정렬과 구분선 수 확인 |
| 4·6종 | 한 칸 이동, 첫/끝 버튼 상태, 6종 모두 접근 확인 |
| 기존 HUD 업데이트 | 이름·수량·웨이브·비용 갱신 후 새 목록과 이동 위치 유지 확인 |
| 비활성화/재활성화 | 반복 후 단일 이동, 이벤트 구독 중복 없음 확인 |
| 보상/증강/준비 전환 | 다음 웨이브 표시, 동일 대상 웨이브 이동 위치 유지, 다른 웨이브 첫 위치 복귀 확인 |
| 마지막 라운드 보상 | 존재하지 않는 다음 라운드를 표시하지 않음 확인 |
| 방향키 입력 | 임시 Input System Keyboard 장치로 Right/Left 한 칸 이동, 길게 누른 상태의 중복 이동 없음, 동시 입력 무시 확인 |
| 접힘·CanvasGroup·전역 입력 차단 | 접힘/재펼침, alpha·Interactable·Blocks Raycasts 각각의 차단, Bootstrap 전역 입력 차단 확인 |
| 실제 팝업 | `Popup_Menu_Lobby`의 OpenCount=1에서 이동 차단, 닫은 뒤 입력 복구 확인 |
| 텍스트 입력 필드 | 활성 `TMP_InputField` 선택 중 이동 차단 확인 |
| 버튼 클릭 | `EventSystem.RaycastAll`에서 NextButton이 최상위 hit임을 확인하고 PointerClick으로 한 칸 이동 확인 |
| Tween·일시정지 | 이동 중 양 버튼 비활성, timeScale=0에서 실제 realtime 경과에 따른 Tween 완료 확인 |
| 패널 치수 변경 | Runtime에서 `500×242`, `592×242`, `592×290`, `750×290`을 일시 적용해 Viewport 폭=아이템 폭×3 및 원래 치수 복구 확인 |
| 1920×1080 Game View | 1종·2종·6종 첫 위치·6종 끝 위치의 가운데 정렬, 제목 구분선 아래 간격과 하단 여백 확인 |
| 보스 5종 이미지 | PNG 5개 모두 RGBA, 모서리 alpha=0, 전체 alpha 범위 0~255 확인 |
| 보스 5종 표시 연결 | 임시 Runtime 목록으로 각 HeroId의 Sprite ID, 투명 배경과 본체에 맞춘 Sprite 영역 확인 |
| 보스 이미지 수정 후 재검증 | 새 Play Mode와 일반 30개 라운드 검사 통과, 6종 끝 위치 Game View에서 보스의 투명 배경 확인 |
| 필수 참조/스크립트 | 최종 Prefab Mode에서 필수 참조 7개, Legacy Rows 3개, 보스 Override 5개 연결 및 Missing Script 0개 확인 |
| 종료·Prefab 재열기 | 검증용 Runtime 상태 복구 후 Play Mode 종료, 대상 Prefab 재열기, isPlaying=false·isDirty=false 확인 |
| 컴파일·Console | 새 적용/Play Mode/180개 라운드 표시/입력 검사 및 최종 종료·Prefab 재열기에서 이전 TMP 오류 재현 없음, 최종 Console error/warning 0개 확인 |

### 검증 범위와 참고 사항

이전 Editor 검증 중 `TMP_SubMeshUI.UpdateMaterial()`에서 일회성 `NullReferenceException`이 기록되었다. 이후 새 적용·Play Mode·데이터 및 입력 검사, 최종 종료와 Prefab Mode 재열기에서는 재현되지 않았으며 최종 Console error/warning은 0개이다.

검사한 Stage 데이터에 등장하지 않는 `H_BOSS_04`, `H_BOSS_05`를 포함한 보스 5종의 표시 연결은 임시 Runtime 목록으로 확인했다. 실제 보스 5종이 나오는 전투를 모두 플레이한 검증은 아니다. 패널 치수 검사는 Runtime RectTransform을 바꾼 검사이다. 물리 키보드/마우스 입력, 다른 Game View 해상도와 빌드 실행은 미확인이다.

최종 캡처는 `Tools/Art/Previews/WaveCarousel_v1/`의 `WavePreviewCarousel_One.png`, `WavePreviewCarousel_Two.png`, `WavePreviewCarousel_Six_First.png`, `WavePreviewCarousel_Six_Last.png`이다. 검증 코드·상세 로그는 Git에서 제외된 `Temp/CardRedesign/`에 보관했다.

### 새 장식 이미지 Play Mode 검증 — 2026-10-07 완료

위 기능 검증은 장식 변경 전 기록이다. 평면 금색 Chevron, 가로 마름모·점 구분선, 세로 마름모 구분선은 아래 항목을 별도로 확인했다.

| 항목 | 확인 결과 |
| --- | --- |
| 투명 이미지·Sprite | PNG Texture 3개 모두 RGBA32, 새 Sprite 연결 확인 |
| Sprite 영역 | Arrow `565×966`, Header `1904×41`, Vertical `121×1182` |
| 제목 구분선 | 왼쪽 X Anchor 정규화 후 Scene의 Header 월드 X 범위 `104.3~594.3`, Artwork `490×12` 확인 |
| 장식 표시 크기 | 화살표 `28×48`, Header Artwork 높이 `12`, 세로 구분선 `10×95` 확인 |
| 1종·2종 | 가운데 정렬, 구분선 각각 0개·1개, 이동 버튼 숨김 확인 |
| 6종·버튼 | 전체 종류 접근, 이전 이동·양 끝 경계 확인, 실제 Raycast 최상위 NextButton과 PointerClick 한 칸 이동 확인 |
| 이동 중 구분선 | Content 이동 중 구분선의 고정 위치 확인 |
| 접힘·재펼침 | 접힌 상태 입력 차단과 재펼침 후 입력 복구 확인 |
| Game View | 1종·2종·6종 첫 위치·6종 끝 위치의 새 장식과 정렬 확인 |
| Console | 장식 적용 후 Play Mode 검사에서 error/warning 0개 확인 |
| 기존 ID·참조 | 장식 교체 전 FileID 194개 모두 보존, Artwork 오브젝트·컴포넌트 ID 4개만 추가 |
| 최종 Edit Mode | Runtime 상태 복구·Play Mode 종료·Prefab 재열기 후 isPlaying=false, isDirty=false, Header X Anchor=0·Position `(51,-94)`, Artwork `490×12`, Missing Script 0개 및 Console error/warning 0개 확인 |

새 장식의 확인 캡처는 `Tools/Art/Previews/WaveCarousel_v2/`의 `WavePreviewArtwork_One.png`, `WavePreviewArtwork_Two.png`, `WavePreviewArtwork_Six_First.png`, `WavePreviewArtwork_Six_Last.png`이다.

### 유닛 구분선 원복 — 2026-10-07

사용자 피드백에 따라 `Separator_1~2`를 기존 `Divider_WaveUnit_Flat.png`와 `2×95` 크기로 복원했다. 기본 Prefab의 위치는 각각 `(143,-4)`, `(287,-4)`이며 유닛 경계의 중심을 유지한다. 배치 도구도 같은 이미지·크기를 사용하도록 갱신했다. Prefab 재열기에서 Sprite·크기·참조와 Missing Script 0개, Console error/warning 0개를 확인했다. 위 마름모 구분선 검증과 v2 캡처는 원복 전 기록이다.

## Git 및 팀 검토

- 충돌 주의 파일은 `WavePreviewPanel.prefab`이다. 기존 오브젝트·컴포넌트 FileID 98개와 `Enemy_0~2`의 HUD 참조를 보존한 것을 확인했다.
- 추가 장식 교체는 기존 캐러셀을 재생성하지 않고, 교체 전 FileID 194개를 모두 유지하며 `HeaderLine/Artwork`의 ID 4개만 추가했다.
- 기존 직렬화 내용은 `HeaderLine`, `Separator_1~2`의 Image·RectTransform 구간 6개만 바뀌었다. 화살표 Image는 기존 Sprite GUID를 유지해 참조 변경이 없다.
- Runtime 스크립트 2개와 Editor 스크립트 1개가 분리되며, Runtime에는 `UnityEditor` 참조를 추가하지 않는다.
- 검토할 신규 에셋은 화살표·제목 구분선·유닛 구분선 PNG/Sprite와 보스 5종 PNG/Sprite, Unity가 생성한 `.meta`이다.
- 상위 `Canvas_GetReady.prefab`과 확인 Scene의 SHA-256은 작업 전 기준과 동일하다. 새 장식 적용 시 카드 Prefab 및 기존 화살표 PNG/Sprite의 `.meta` 파일도 기준과 동일함을 확인했다. ProjectSettings, Packages와 기존 팀원 Runtime 파일의 변경도 없다.
- LayerMask, Tag, Collider, Rigidbody, IsTrigger, Animator Parameter와 Input Action 연결 추가는 없다.
- UI 표시를 변경하는 작업이며 전투·스폰·보상·저장 데이터 규칙을 변경하지 않는다.
