# 로비 도감·업적·공유 해금 미리보기

## 범위와 저장 구분

- 대상 Scene: `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`.
- 공용 부모: `Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab`.
- 새 화면: `Collections_v1/Prefabs/Canvas_UnitCodex.prefab`, `Canvas_Achievements.prefab`.
- `Canvas_LobbyOverlays`에 위 두 화면을 중첩 배치한다. 로비 도감/업적 버튼과 Back은 기존 공용 팝업 스택을 사용한다. 구형 팝업·다른 Scene은 유지한다.
- **사용자가 승인한 UI 미리보기**이다. 실제 전투 기록 수집, 계정 저장/불러오기, 업적 보상 지급, 게임의 `SkillTreeStore` 연결은 하지 않았다. Play Mode 종료 시 세션 해금/진행은 사라진다.

## Inspector에서 편집할 곳

아래 `Collections_v1`은 모두 `Assets/06.UI/LobbyMutedPreview/Collections_v1`이다.

| 설정 | 에셋/오브젝트 | 편집 항목 |
| --- | --- | --- |
| 유닛 도감 | `Collections_v1/UnitCatalog.asset` | ID, Display Name, Description, Portrait, Faction, Default Unlocked |
| 업적 정의 | `Collections_v1/AchievementCatalog.asset` | ID, Display Name, Description, Icon, Target, Progress Per Segment |
| 스킬 기본 해금 | `Assets/06.UI/LobbyMutedPreview/Skills_v1/SkillPreviewCatalog.asset` | 각 Entry의 Default Unlocked |
| 미리보기 진행/해금 예외 | `Canvas_LobbyOverlays > UILobbyCollectionState` | Initial Achievement Progress, Initial Unlock Overrides |
| 화면 어둡기 | 각 새 Canvas의 `BackgroundShade > Image > Color Alpha` | 기본 0.60 |
| 하단 그라데이션 | 각 새 Canvas의 `BackgroundBottomGradient` | 로비와 동일한 Image 설정 |
| 도감 카드 | 도감 원본 Prefab의 `UnitCard_Template` | Frame / Portrait / Lock / Name |
| 업적 카드 디자인 | `Collections_v1/Prefabs/AchievementCard.prefab` | Frame / IconFrame / Icon / Name / Description / Progress / ProgressFill / Status |
| 업적 목록 배치 | `Canvas_Achievements.prefab`의 Content > GridLayoutGroup | Cell Size / Spacing / Constraint Count |
| 업적 달성 표시 | `Canvas_Achievements.prefab`의 CompletedBadge / CompletedCount | 프레임·트로피 / TMP 글자 배치 |

ID는 중복 없이 유지한다. 기존 유닛은 `unit.M_WAR_01` 같은 `unit.` + UnitStatData.unitId를 사용한다. 스킬은 기존 `ui_preview_*` ID를 유지한다. 실제 게임의 한글 Skill ID와 다르므로 직접 연결하지 않는다.

카탈로그에는 고정 정의만 저장한다. 현재 해금과 업적 진행은 `UILobbyCollectionState`의 런타임 Dictionary에 보관한다. `Initial Unlock Overrides`는 카탈로그의 Default Unlocked보다 우선하는 미리보기 예외다. 예외가 없으면 기본 해금 설정을 사용한다. Inspector 초기값은 다음 Play 세션 또는 명시적인 `ResetPreviewState()` 때 적용된다.

## 업적 카드 디자인 수정

1. Play Mode를 종료하고 `Assets/06.UI/LobbyMutedPreview/Collections_v1/Prefabs/AchievementCard.prefab`을 더블 클릭한다.
2. Prefab Mode에서 프레임 이미지, 자식 RectTransform의 위치·크기, TMP 폰트·정렬 등을 편집하고 저장한다. 독립 프리팹의 루트는 활성 상태라 직접 편집할 수 있다.
3. `Canvas_Achievements`의 `AchievementCard_Template`는 이 프리팹의 비활성 중첩 인스턴스다. 이름/비활성 상태 외의 디자인은 원본에서 상속한다. 공통 디자인을 바꿀 때는 해당 인스턴스에 별도 override를 쌓지 말고 `AchievementCard.prefab` 원본을 수정한다.
4. 다음 Play Mode에서 업적창을 열면 기존 `UIAchievementView`가 중첩 템플릿을 복제해 모든 카드에 적용한다. 이미 생성된 Play Mode 카드의 실시간 갱신 기능은 추가하지 않았다.

주의 사항:

- 카드 내부 디자인은 위 프리팹에서 수정하지만, 목록에서 카드가 차지하는 크기는 업적창 `Content`의 `GridLayoutGroup`이 결정한다. 현재 Cell Size는 `520×230`이다. 카드 전체 크기·간격·열 수를 바꾸려면 목록 설정도 함께 맞춘다.
- `UIAchievementCardView`의 Icon / Name Text / Description Text / Progress Text / Completed Text / Progress Fill / Frame 참조를 유지한다. 자식 오브젝트를 교체했다면 대응 필드를 다시 연결한다.
- 이름·설명·아이콘·목표치는 `AchievementCatalog.asset`, 현재 진행도는 `UILobbyCollectionState`에서 공급한다. 카드 프리팹의 샘플 문구나 아이콘을 바꿔도 실행 시 업적 데이터가 덮어쓴다.
- 진행 막대·Status·연결된 Frame의 상태색은 `UIAchievementCardView`의 In Progress Color / Completed Color로 설정한다. 해당 Image의 Color만 변경하면 실행 시 상태색이 다시 적용된다. ProgressFill은 진행 비율에 따라 Horizontal Filled로 표시한다.
- 진행 구분선은 `ProgressFill/Dividers`의 `UIAchievementProgressDividers`가 카탈로그의 목표/칸당 횟수에 맞춰 그린다. 카드의 Progress Dividers 참조를 유지한다. 기존 `Segment_1..9`는 비활성 보존하며 다시 켜지 않는다. `_segments`는 별도 칸별 채움 Image용이므로 구분선을 연결하지 않는다.

### 업적별 칸 수와 계산식 편집

`AchievementCatalog.asset > Entries > 해당 업적`에서 다음 두 값을 설정한다. 기존 ScriptableObject에 표시 설정만 추가했으며 현재 진행도는 여전히 별도 런타임 State가 관리한다.

- **Target**: 달성에 필요한 전체 횟수.
- **Progress Per Segment**: **몇 회당 1칸인지**. 1 이상 정수이며 업적마다 다르게 입력할 수 있다.
- 칸 수 = `ceil(Target / Progress Per Segment)`, 구분선 수 = `칸 수 - 1`.
- i번째 경계 위치 = `(i × Progress Per Segment) / Target`, 전체 채움 비율 = `현재 진행도 / Target`.

| Target | Progress Per Segment | 칸 / 구분선 | 표시 예 |
| --- | --- | --- | --- |
| 5 | 1 | 5칸 / 4선 | 3/5 → 3칸 채움 |
| 100 | 10 | 10칸 / 9선 | 73/100 → 7칸 + 다음 칸의 30% |
| 100 | 25 | 4칸 / 3선 | 25회마다 1칸 |
| 500 | 50 | 10칸 / 9선 | 73/500 → 1칸 + 다음 칸의 46% |
| 95 | 10 | 10칸 / 9선 | 마지막 칸은 남은 5회에 해당하는 절반 폭 |
| 1 | 1 | 1칸 / 0선 | 단일 막대 |

목표 이하로 딱 나누어떨어지지 않아도 마지막 나머지 칸을 유지한다. 값이 목표 이상이면 구분선 없는 한 칸이다. 현재 샘플은 100회 업적=10회당 1칸, 500회 업적=50회당 1칸, 나머지=1회당 1칸으로 설정했다.

편집은 Edit Mode에서 하고 다음 Play Mode에 확인한다. 실행 중 State의 Changed 이벤트나 `UIAchievementView.Refresh()` 시에도 카탈로그 값을 다시 읽는다. 자동 Update 폴링은 하지 않는다.

`Dividers`는 채움 이미지의 전체 RectTransform을 따라가는 단일 UI 메시다. 칸마다 GameObject를 만들지 않으며 Raycast를 막지 않는다. 비정상적으로 큰 목표를 입력해도 논리 칸 수/진행 비율은 유지하되 UI 정점 한도를 보호하기 위해 기본 최대 4096개의 실제 경계만 고르게 표시한다. 실제 사용 시 카드 폭에 맞는 칸당 횟수를 지정한다.

### Gothic Metal 진행 막대 — 2026-09-22

- `AchievementCard.prefab`의 진행 막대에 독립 생성한 프레임·Fill·눈금 3종을 연결했다. 카드 이름·아이콘·문구·외곽 카드 디자인은 유지한다.
- `ProgressRail > Image > Source Image`: `Styles/GothicProgress_Frame.asset`. 내부가 투명한 프레임이며 Fill보다 뒤 순서(화면에서는 앞)에 렌더한다.
- `ProgressTrack`: 빈 영역의 어두운 바탕. `ProgressFill`: `Styles/GothicProgress_Fill.asset`, 기존 `현재 진행도 / Target`만큼 가로로 채운다. 상태색은 카드의 In Progress Color / Completed Color로 편집한다.
- `ProgressFill/Dividers > UIAchievementProgressDividers > Tick Sprite`: `Styles/GothicProgress_Tick.asset`. 한 개짜리 눈금을 계산된 경계마다 반복한다. Height Ratio / Bottom Inset / Line Width로 높이·하단 여백·두께를 편집한다. Completed Tint는 밝은 완료 Fill에 대비되는 눈금 색이다.
- 목표/칸당 횟수는 기존 `AchievementCatalog.asset`의 Target / Progress Per Segment로 편집한다. PNG에 눈금 수나 글자를 굽지 않았으므로 3/5=4눈금, 100회=10회당 1칸 등의 계산은 그대로 유지된다. 진행/저장/해금/보상 데이터는 이번에 수정하지 않았다.
- 이미지 생성 스킬에 따라 프레임·Fill의 첫 결과에 포함된 가짜 체크무늬를 검출하고, 내장 이미지 편집으로 실제 투명 PNG를 다시 만들었다. PNG는 무수정 복사하고 Unity Sprite 영역으로 여백만 제외했다. 생성 방식·원본·전체 프롬프트·영역 좌표: `Tools/Art/achievement_gothic_progress_prompts.md`.
- Unity API와 Undo로 원본 카드 프리팹의 진행 막대만 변경·저장했다. 진행 막대 외 49개 컴포넌트 직렬화 값 불변. 새 이미지/Sprite/.meta 생성과 디스크 저장의 복구는 Undo만으로 보장되지 않으므로 Git Diff에서 별도 확인한다.
- 검증: 계산/메시/스프라이트 UV/짧은 눈금/상태 틴트 122항목, 실제 업적 카드 123항목, 기존 Collections 회귀 226항목 통과. 9개 실제 CanvasRenderer의 경계 수·색상, 3/5→5/5 상태 전환과 달성 카운터 갱신도 확인했다.
- 최종 Game View: `Temp/AchievementProgress/gothic-metal-final.png`(Git 제외). 기존 미저장 로비 Scene은 저장하지 않고 보존한다. 다른 Prefab/Scene/ProjectSettings/Packages는 이번에 변경하지 않았다. 최종 카드 Prefab Mode에서 편집을 이어갈 수 있다.
- 추가 수동 확인: 다른 화면비에서 진행 막대·문구 간격, 원하는 막대색/눈금 높이. Layer/Tag/Collider/Rigidbody/Animator/Input System 추가 설정은 없다.

### 상단 달성 배지

- `CompletedBadge`: 기존 로비 `HUD_Level.png` 프레임 재사용.
- `CompletedBadge/Trophy`: 새로 생성한 실제 투명 PNG `Sprites/CompletedTrophy_v2.png`. 이전 `CompletedTrophy.asset`의 사각 영역 재사용은 배경/픽셀 품질 문제로 중단했으며 해당 이전 에셋은 미사용으로 보존한다. 새 PNG는 Point·무압축·Mipmaps Off, Image는 Preserve Aspect 및 Raycast Off로 설정했다.
- `CompletedCount`: 기존 TMP 및 `_summaryText` 참조 유지. `달성 n / 전체`는 런타임에서 갱신된다. 장식은 Raycast 비활성이다.

### 진행 분할·배지 적용 기록 — 2026-09-22

- Runtime 변경: `UIAchievementCatalogSO`에 칸당 횟수 설정, `UIAchievementCardView`에 구분선 연결, 신규 `UIAchievementProgressDividers`에 경계 메시 표시 책임을 분리했다. 저장·보상·실제 전투 집계는 변경하지 않았다.
- 설정/디자인 변경: `AchievementCatalog.asset`의 칸당 횟수, `AchievementCard.prefab`의 구분선 연결, `Canvas_Achievements.prefab`의 상단 배지, 신규 `Sprites/CompletedTrophy.asset`. 부모 Overlay와 Scene은 Git 변경이 없다.
- 사용자가 편집한 카드 위치·크기·글자 설정을 보존했다. 카드의 기존 컴포넌트 57개와 상단 배지 이외 화면 컴포넌트 103개의 직렬화 값이 작업 전후 동일함을 확인했다.
- 신규 Editor 검증 메뉴: `Tools/OZGL2/Lobby/Validate Achievement Progress`. 임시 Preview Scene과 카탈로그 복사본만 사용하며 원본 변경이 없어 Undo 대상이 없다. `LobbyAchievementProgressValidation.RunLive()`는 열린 업적 화면을 읽기만 검사한다.
- Unity 검증: 계산/메시/크기 변경/예외 입력 113개, 실제 업적 카드 105개, 기존 도감·업적·공유 해금 회귀 검사 226개 통과. 실제 CanvasRenderer 메시의 정점 수와 구분선 수, 진행 갱신 시 달성 2→3 및 Back 좌표의 최상위 Raycast/포인터 클릭 닫힘을 별도 확인했다.
- 초기 Play 검증에서 새 구분선의 CanvasRenderer 누락을 발견해 필수 컴포넌트 선언과 프리팹 연결을 보완한 뒤 다시 검증했다. 최종 Unity 컴파일 및 Console 오류/경고 0개, 기존 회귀 검사의 Missing Script/누락 참조/TMP 폰트 검사 통과.
- 종료 상태: Edit Mode, 대상 Scene dirty=false, 부모 인스턴스 override 159개 유지. Scene 저장본은 이전 파일과 동일하다. `.meta`는 Unity가 생성했고 다른 Scene·메뉴·특성·스킬·원본 PNG·ProjectSettings·Packages는 수정하지 않았다. 커밋/푸시하지 않았다.
- Inspector에서 업적마다 Target / Progress Per Segment를 확인하고, 사용자 환경의 화면비·물리 마우스 입력은 추가 확인한다. Layer/Tag/Collider/Rigidbody/Animator/Input System 변경은 없다. 충돌 위험은 두 업적 프리팹과 카탈로그이며 기존 카드 수동 편집분이 Git Diff에 함께 포함된다.
- Game View 캡처: Git 제외 `Temp/AchievementProgress/achievement-progress-final.png`.

### 트로피 품질 수정 — 2026-09-22

- 이전 Sprite 영역 재사용은 배경 제거가 아니었고 픽셀 품질 검수가 부족했다. 내장 이미지 생성으로 독립 트로피를 재생성한 뒤 PNG의 실제 알파와 Game View를 확인해 교체했다.
- 생성 원본의 알파를 그대로 보존했다. 1254×1254 PNG의 완전 투명 픽셀 934,336개, 바깥 모서리와 양쪽 손잡이 구멍의 알파 0을 확인했다. 본체 대표 픽셀은 254/255의 알파다.
- 원본 프리팹의 Trophy Image와 48×48 크기만 변경하고 다른 컴포넌트 111개의 직렬화 값을 보존했다. 사용자의 미저장 Scene 변경, 수정된 프레임·위치는 저장/덮어쓰기하지 않았다.
- Play Mode에서 새 Sprite 참조, Point, 무압축, Mipmap Off, 종횡비 유지, 입력 비차단 확인. 업적 Live 검사 105개 통과, Console 오류/경고 0개. 현재 검수 캡처는 `Temp/AchievementProgress/trophy-v2-in-game.png`이며 이전 캡처를 대체한다.
- 생성 프롬프트/파일/임포트 기록: `Tools/Art/achievement_trophy_v2_prompt.md`. 이번 품질 수정에서는 Runtime 코드·카탈로그·다른 화면 변경 및 커밋을 하지 않았다.

### 분리·검증 기록 — 2026-09-22

- 신규 `AchievementCard.prefab`과 Unity가 생성한 `.meta`, 연결을 저장한 `Canvas_Achievements.prefab` 및 이 문서만 이번 작업 대상으로 변경했다. Runtime 코드는 변경하지 않았다.
- Unity API로 기존 템플릿 자체를 중첩 프리팹으로 연결하여 `_cardTemplate` 참조를 유지했다. 카드 바깥 44개 컴포넌트의 직렬화 값은 동일했다. 원본 카드 내부 누락 참조/Scene 외부 참조 각각 0개.
- Play Mode에서 9개 카드, 달성 2/9, 템플릿 비활성, 생성 카드 디자인 상속 및 Back 닫힘을 확인했다. 도감/업적/공유 해금 회귀 검사 226항목 통과, Console 오류·경고 0개.
- 기존 Scene의 미저장 변경과 부모 override 159개를 보존했고 Scene을 별도로 저장하지 않았다. 다른 화면·카탈로그·진행도·ProjectSettings·Packages는 변경하지 않았다.
- 화면 오브젝트 조작은 Unity Undo 지원 API를 사용했다. 새 프리팹/메타 생성 및 저장된 에셋 파일의 복구는 Undo만으로 보장되지 않으므로 Git Diff에서 함께 확인한다. 커밋/푸시는 하지 않았다.

## 현재 샘플 데이터

- 유닛 12종: 실제 카탈로그의 마왕군 6종 / 인간군 6종. 각 진영 전사·궁수·마법사는 기본 해금, 방패병·힐러·도적은 미해금이다. 실제 플레이어 진행을 의미하지 않는다.
- 스킬 12종은 기존 동작을 보존하기 위해 **모두 기본 해금**이다. 카탈로그에서 체크를 끄거나 런타임 SetUnlocked를 호출하면 실루엣과 장착 제한이 작동한다.
- 업적 9종: 첨부 자료의 명칭/목표/샘플 진행도에 맞춰 시작한다. `1, 5, 7, 73, 73, 0, 8, 3, 2`이며 처음 달성 수는 2/9이다. 조건 설명은 편집 가능한 시안이며 자동 달성 판정이 아니다.

## 표시 규칙

- 미해금 유닛: 원본 RGB를 제거한 어두운 알파 실루엣, 자물쇠, 이름 `미발견`.
- 해금 유닛: 원본 초상 색과 이름 복원. 실루엣 텍스처를 따로 만들지 않는다.
- 미해금 스킬: 카테고리 목록에는 남고 선택할 수 있으나 상세 이름/수치는 숨긴다. 장착 버튼과 직접 EquipSelected 호출을 모두 차단한다. 재해금하면 기존 분류색/아이콘/설명이 복원된다.
- 스킬 강제 재잠금 시 해당 항목만 임시 저장/편집 구성 양쪽에서 제거한다. 다른 미저장 편집은 유지한다. 카탈로그가 일시적으로 null/빈 상태인 경우는 재잠금으로 취급하지 않는다.
- 업적: 진행 막대와 텍스트는 `0..Target` 범위로 표시하되 런타임 원래 누적값은 보존한다. 목표 달성 시 고금색 막대/`달성`으로 구분한다.
- 두 목록은 카탈로그 수에 맞춰 카드를 생성·재사용하고 세로 스크롤한다. 글자는 전부 TMP이다. 픽셀 SDF는 기존 공유 `01.Font` 파일을 재사용한다.

## 이후 실제 시스템 연결 지점

```csharp
// UILobbyCollectionState 참조는 Inspector/명시적인 주입으로 전달한다.
collectionState.SetUnlocked("unit.M_SHD_01", true);
collectionState.SetUnlocked("ui_preview_sword", true);
collectionState.SetAchievementProgress("hero_hunter_1", 80);
```

Changed 이벤트로 활성 화면이 갱신되고, 비활성 화면은 재열기 때 최신 상태를 읽는다. 향후 저장 어댑터가 실제 기록을 읽어 이 API로 전달하도록 확장한다. UI/카탈로그가 직접 전투 통계를 세거나 PlayerPrefs를 작성하지 않도록 유지한다.

## 아트와 생성 기록

- `Collections_v1/Sprites`: 새 도감 카드·업적 카드·진영 2종·잠금 1종. 내장 image_gen으로 각각 생성하고 승인된 알파 여백/크기 정렬만 후처리했다.
- 업적 아이콘 9종은 기존 특성 아트에서 알파 영역을 정렬한 파생본이다. 기존 파일은 수정하지 않았다.
- `Collections_v1/Portraits`: 기존 유닛 모델의 SpriteRenderer 조합을 Unity Preview Scene에 시각 컴포넌트만 복사해 렌더한 PNG 12종. 게임 스크립트/Animator는 실행하지 않는다. 원본 Prefab은 수정하지 않는다.
- 생성 프롬프트: `Tools/Art/collection_art_prompts.md`.
- 원본 보존: `Tools/Art/Sources/LobbyCollections_v1`.
- 후처리 재현: `Tools/Art/prepare_collection_art.py`, `normalize_collection_icons.py`.

## Editor 도구와 Undo

- 초상 생성: `Tools/OZGL2/Lobby/Render Unit Codex Portraits (New Files Only)`.
- 최초 조립: `Tools/OZGL2/Lobby/Build Collections Preview`.
- 검증: `Tools/OZGL2/Lobby/Validate Collections Preview` — 대상 Scene Play Mode, 모든 Overlay가 닫힌 상태에서 실행한다. 검증은 미리보기 State를 초기화하므로 사용자 세션 테스트 도중에는 실행하지 않는다.
- 최초 조립은 기존 화면/원본 Prefab이 있으면 중단한다. 기존 스킬·메뉴·특성 전체를 Apply하지 않고 추가 오브젝트/컴포넌트/해금 참조만 부모에 반영한다.
- Scene 생성/연결은 Undo를 지원한다. 신규 PNG/SO/Material/Prefab의 디스크 생성은 Undo만으로 제거되지 않을 수 있으므로 Git Diff에서 별도 검토한다. Scene 저장은 검증 후 별도 수행한다.

## 검증/리뷰 체크

- Scene/부모 Prefab은 충돌 주의. `.unity`/`.prefab`/`.meta` 텍스트 직접 수정 금지.
- 새 Canvas: Sorting 210, GraphicRaycaster, UIPopupPanel, Back → 부모 CloseTop.
- 루트: UILobbyCollectionState 한 개, 세 화면이 같은 상태 참조. 중복 EventSystem/CanvasScaler 없음.
- TMP/초상/카탈로그/잠금 Material 참조와 로비 입력 복원을 확인한다.
- 도감 양 진영·해금 전환·카드 증가, 업적 진행/만료 목표, 스킬 해금/잠금 및 저장 확인 흐름을 Play Mode에서 검사한다.
- ProjectSettings/Packages, Layer/Tag/물리/Animator 설정 변경 없음.
- 비 16:9 화면비·실제 하드웨어 조작·Player 빌드는 별도 검증이 필요하다.

## 적용·검증 기록 — 2026-09-21

- 새 도감/업적 프리팹과 부모 Overlay를 Unity API로 저장하고, 대상 Scene을 저장했다. 종료 시 Edit Mode, Scene dirty=false.
- 변경 전 부모 인스턴스 override 112개를 유지했다. 기존 메뉴·특성의 전체 Apply는 하지 않았다.
- Unity Play Mode 자동 검사 557개 통과: 신규 도감/업적/공유 해금 214 + 기존 스킬 42 + 분류 표시 76 + 설명 27 + 저장/프레임 48 + 정렬/호버 150.
- 회귀 검사에서 카탈로그 null/빈 상태를 재잠금으로 오인해 장착 구성을 지우던 문제를 발견·수정하고 재검증했다.
- Game View에서 도감의 발견/실루엣, 업적 카드/막대, 스킬 잠금 표시를 캡처해 확인했다. 캡처는 Git 제외 `Temp/LobbyCollections`에 있다.
- 최종 Console 오류/경고 0개, 새 화면 Missing Script/끊어진 참조/TMP 폰트 누락 0개. 실제 스킬 PlayerPrefs와 카탈로그 원본이 검사 전후 동일함을 확인했다.
- `git diff --check`의 경고는 Unity 직렬화 빈 필드 뒤 공백이다. Scene/Prefab을 텍스트로 정리하지 않았다. C# 변경분 공백 오류 없음.
- 새 이미지 50MB 초과 없음. SDF/기존 외부 원본/다른 Scene/ProjectSettings/Packages 변경 없음. 커밋/푸시는 하지 않았다.

## 클릭 입력 수정·재검증 — 2026-09-21

- 로비 `ReservedButton`은 기존 예약 버튼의 `Interactable=false`가 남아 있었다. 대상 Scene에서 활성화하고, 최초 조립 도구도 화면 연결 시 입력을 활성화하도록 보완했다.
- 도감 원본 Prefab의 `Faction_0/Emblem`, `Faction_1/Emblem`에 Button을 추가하고 Image Raycast Target을 켰다. 각각 `UIUnitCodexView.ShowFaction(0/1)`에 연결해 기존 텍스트 탭과 같은 진영 전환을 수행한다. 아트·배치·카탈로그는 유지한다.
- 복구 도구: `Tools/OZGL2/Lobby/Repair Collection Click Targets`. 대상 Scene Edit Mode에서만 실행하며 Undo를 지원한다. 도감 원본에는 추가 Button/클릭 관련 속성만 Apply하고, Scene 저장은 별도 수행한다.
- 이전 자동 검사는 `onClick.Invoke()`로 연결된 동작을 검사했으므로 실제 입력 가능 상태를 검증하지 못했다. 이번에는 해당 누락을 보완했다.
- Unity Play Mode 검증: Collections 구조·동작 226개, 로비 포인터 18개, 도감 포인터 31개 통과. 포인터 검사는 Game View 렌더 후 실제 좌표의 최상위 UI Raycast를 확인하고 pointer down/up/click을 전달한다. 물리 마우스 수동 조작 검사는 별도다.
- 검증 진입점: `LobbyCollectionsPreviewValidation.ValidateLobbyPointer()`는 모든 팝업을 닫고 렌더 후 실행, `ValidateCodexPointer()`는 도감만 열고 렌더 후 실행한다. 도감 아이콘/기존 탭 전환, 업적 재열기, 팝업 뒤 로비 입력 차단까지 검사한다.
- 저장 후 대상 Scene dirty=false, 부모 인스턴스 override 112개 유지. Console 오류/경고 0개. 다른 Scene·메뉴·특성·아트·실제 저장 데이터는 변경하지 않았다.
