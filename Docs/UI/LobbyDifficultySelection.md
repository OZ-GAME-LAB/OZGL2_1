# 로비 난이도 선택

## 적용 범위

- Scene: `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`.
- Hierarchy: `Canvas_Lobby > StageSelection`.
- 쉬움 / 보통 / 어려움, 기본 선택 보통. 모두 선택 가능하며 처음/끝은 순환하지 않는다.
- 기본 그림은 02 용사군. 01 마왕성은 교체용으로 함께 준비했다. 기존 03·04 비교 시안은 참고 문서에만 남기고 게임 리소스로 추가하지 않았다.
- 이번 구현은 UI 선택값과 이벤트 제공까지다. 실제 전투 StageDataSO 연결, 쉬움 밸런스, 난이도별 기록/해금/저장 기능은 포함하지 않는다.
- 기존 전투 준비 버튼의 위치·크기·`UISceneNavigator.LoadScene` 콜백을 유지했다. 선택값은 전투 Scene에 자동 전달되지 않는다.
- 기존 `StageRecord` 판의 TMP는 난이도 이름/짧은 설명으로 재사용한다. 목업 `최고 도달 40`을 난이도별 실제 기록으로 표시하지 않는다.

## Inspector 편집

`Canvas_Lobby > StageSelection > UILobbyDifficultySelector`에서 수정한다.

| 항목 | 편집 내용 |
| --- | --- |
| Artwork Set | `DifficultyArtwork_Hero`(02, 기본) 또는 `DifficultyArtwork_Castle`(01)로 교체 |
| Catalog | 쉬움/보통/어려움 이름과 설명. 순서는 EASY / NORMAL / HARD 유지 |
| Initial Difficulty | 씬 진입 기본값. 현재 NORMAL |
| Fade Out Duration / Fade In Duration | 기본 0.14초 / 0.21초 |
| Artwork Slide Ratio | 그림 폭 대비 이동량. 기본 0.06, 0이면 페이드만 사용 |
| Fade Out Ease / Fade In Ease | 기본 OutQuad / OutCubic |

- SO 위치: `Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/Data/`.
- 그림 위치: `Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/Artwork/` (6개 PNG).
- 세트 안의 Easy/Normal/Hard Sprite를 교체할 수 있다. 그림에는 프레임·글자·자물쇠가 없다.
- Catalog/Artwork Set 내부 값을 수정한 후 제어부 Inspector의 **난이도 표시 갱신** 버튼으로 적용한다. 표시 변경은 Undo를 지원한다.
- Play Mode에서 바꾼 선택은 런타임 임시 상태이며 SO/PlayerPrefs에 저장하지 않는다. Play Mode에서 SO 자체를 편집하면 Unity 특성상 에셋 편집이므로 값이 남을 수 있다.
- 프레임 Sprite는 각 카드의 기존 `UILobbyStageCardView` 설정을 유지한다. 중앙은 붉은 프레임, 양옆은 중립 프레임이다.

## 화면 갱신과 레이어

```text
StageSelection [UILobbyDifficultySelector]
├─ PreviousStage / CurrentStage / NextStage_Locked
│  ├─ UILobbyStageCardView + UILobbyDifficultySlotView
│  ├─ VisualLayers [CanvasGroup]
│  │  ├─ ArtworkMask [기존 45도 Mask, 고정]
│  │  │  └─ ArtworkRoot [기존 -45도 역회전, 고정]
│  │  │     ├─ Background [고정 검정 바탕]
│  │  │     ├─ ArtworkMotion [CanvasGroup, 이 부모만 좌우 이동]
│  │  │     │  └─ Artwork [Image + AspectRatioFitter]
│  │  │     └─ LockedShade [해제]
│  │  ├─ Frame [CanvasGroup, 위치/그림 유지, 투명도만 전환]
│  │  └─ LockOverlay [해제]
│  ├─ 기존 캡션 판 [CanvasGroup + TMP, 위치 고정]
│  └─ StartButton [CurrentStage에만 있음, 기존 위치/콜백 유지]
├─ PreviousArrow
└─ NextArrow
```

1. 좌우 버튼 입력 시 현재 프레임·캡션·그림을 페이드아웃한다. 그림만 입력 반대 방향으로 짧게 이동한다.
2. 투명도 0에서 새 난이도 그림과 TMP를 갱신한다. 자리별 Frame Sprite는 그대로다.
3. 새 그림이 반대편 오프셋에서 제자리로 돌아오며 프레임/글자와 함께 페이드인한다.
4. 완료 후 선택값을 확정하고 `SelectionChanged`를 한 번 호출한다.

- 전환 중 좌우/전투 준비 입력을 차단한다. 전투 준비가 원래 비활성이었다면 완료 후에도 비활성을 유지한다.
- 쉬움의 왼쪽 / 어려움의 오른쪽은 비활성·어두운 화살표로 표시한다. 없는 인접 카드는 숨기며 중복 카드나 순환을 만들지 않는다.
- 소유 Sequence만 종료하며 전역 Kill을 사용하지 않는다. 비활성화/파괴/외부 Tween 종료 시 마지막 확정 상태로 복구한다.
- `SetUpdate(true)`로 게임 시간 배율 0에서도 UI 전환을 실행한다.
- 새 표시용 CanvasGroup과 Image는 Raycast를 가리지 않는다.
- 초기화 시 Button OnClick 런타임 리스너를 등록하고 OnDisable/OnDestroy에서 해제한다. Inspector OnClick에 동일 함수를 중복 연결할 필요가 없다.

## 책임과 외부 API

- `LobbyDifficultyCatalogSO`: 난이도 이름/설명. 전투 수치 없음.
- `LobbyDifficultyArtworkSetSO`: 난이도별 Sprite 세 장. 테마 교체를 난이도 데이터와 분리한다.
- `UILobbyDifficultySlotView`: 카드 한 자리의 표시/투명도/내부 그림 오프셋.
- `UILobbyDifficultySelector`: 선택값, 경계 조건, 입력 구독, 전환과 중단 처리.
- 기존 `UILobbyStageCardView`는 수정하지 않고 재사용한다.

```csharp
eLobbyDifficulty difficulty = selector.SelectedDifficulty;
LobbyDifficultyCatalogSO.Entry entry = selector.SelectedEntry;
selector.SelectionChanged += OnDifficultyChanged; // 소유 객체 종료 시 반드시 -= 해제
selector.TrySelect(eLobbyDifficulty.HARD);         // 전환 중/잘못된 값이면 false
selector.SetArtworkSet(castleArtworkSet);          // 선택값을 유지하며 그림 세트만 교체
```

## Editor 도구

- 구성 메뉴: `Tools > OZGL2 > Lobby > Configure Difficulty Selection (Current Lobby Only)`.
- 구성 코드는 지정된 로비 Scene/Edit Mode에서만 동작한다. 이미 구성된 경우 표시만 갱신하며 사용자의 세트/시간/배치를 초기화하지 않는다.
- Scene 변경은 Undo 지원. 새 PNG/SO 파일은 독립 에셋이므로 Scene Undo 뒤에도 보존된다. 자동으로 다른 씬/dirty 에셋을 저장하지 않는다.
- 검증 메뉴: `Tools > OZGL2 > Lobby > Validate Difficulty Selection (Play Mode)`.
- 검증은 임시 선택/그림 세트를 원복하며 전투 이동이나 저장 데이터 쓰기를 수행하지 않는다.
- Scene/Prefab/.meta 텍스트 직접 편집 대신 Unity Editor API로 Scene과 SO 참조를 연결했다. .meta는 Unity에서 생성했다.

## 검증 기록 (2026-09-22)

- 컴파일 완료. Play Mode 자동 검증 162개 통과, 최종 Console Error/Warning 0개, Scene Missing Script 0개, 필수 Inspector 참조 누락 0개.
- 1·2번 두 세트의 모든 난이도/슬롯, 이름/그림, 잠금 해제, 끝 경계, 전환 투명도/방향, 중복 입력, 이벤트 횟수, 종료/재활성화, 버튼 Raycast, SO 무변경을 검사한다.
- 별도 실시간 확인: Time.timeScale=0에서 NORMAL→HARD 전환 완료. 자동화 중 Editor가 비활성일 때 프레임이 정지하는 기존 Run In Background=false 설정 때문에, 검사 동안만 Application.runInBackground=true로 두었으며 원래 false와 timeScale=1로 복구했다. ProjectSettings는 수정하지 않았다.
- Hero 세트의 쉬움·보통·어려움 화면과 Castle 세트의 보통 선택 화면을 Game View에서 확인했다.
- 적용 전후 StageSelection 밖 기존 컴포넌트 5,620개의 직렬화 값이 동일했다. 전투 준비 RectTransform/Button 직렬화 값도 동일했다.
- 실제 전투 Scene 이동/밸런스/저장 연동은 이번 범위 밖이며 미검증이다.
- Play Mode 종료 후 02 용사군 / 보통 기본 표시로 복구했다. 플레이어 빌드는 수행하지 않았다.
- 캡처: `Temp/LobbyDifficulty/Difficulty_Final_HeroSet.png`, `Difficulty_Play_Easy.png`, `Difficulty_Play_Hard.png`, `Difficulty_Play_CastleSet.png`.

## Git/충돌 주의

- Scene 변경: `UI_Lobby_MutedPreview.unity`의 StageSelection.
- 새 Runtime 코드 4개, Editor 코드 3개, Artwork 6개, SO 3개와 Unity 생성 메타.
- 기존 `Canvas_UnitCodex.prefab`의 사용자 변경은 보존했다. 다른 Prefab, Packages, ProjectSettings, 원본 StageLayers 이미지는 변경하지 않았다.
- 충돌 검토 대상: Scene의 슬롯 참조/ArtworkMotion 부모 변경/CanvasGroup, 새 코드와 .meta 짝, SO의 Sprite 참조.
- `git diff --check`는 Unity가 저장한 빈 `m_Name: ` 필드 4개의 후행 공백을 보고했다. Scene 텍스트 직접 편집 금지 규칙에 따라 Unity 직렬화 결과를 유지했다.
- 생성 방식과 전체 프롬프트: `Tools/Art/lobby_difficulty_artwork_prompts.md` (내장 image_gen, 원본 보존).
