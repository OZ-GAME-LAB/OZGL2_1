# 전투 UI 팀원 사용 안내

기준일: 2026-09-28. 대상은 `UI_Battle_MutedPreview`이며 원본 `UI_Battle`과 구분한다.

팀 노션: [전투 UI — 팀원 사용·연동 가이드 (MutedPreview)](https://app.notion.com/p/3e926fb07273815db837f6a0f421bdfb).

이 문서는 현재 저장된 코드·프리팹·관련 문서와 Unity MCP 점검을 바탕으로 정리한 사용 안내다. 이번 인수 점검에서는 Play Mode에서 각 항목을 한 번씩 버튼 이벤트/API 호출로 확인했다. **물리 마우스 클릭 검증은 아니며, 실제 게임 로직 연동·빌드 검증과도 구분한다.** 아래 이전 검증 기록과 이번 확인 범위, 팀원이 추가로 확인할 항목을 구분한다.

## 1. 현재 구현 범위

현재 전투 UI는 재사용 가능한 **표시·페이지 전환·팝업 입력 구조**다. 실제 전투 시스템에 연결된 완성 게임 로직으로 간주하지 않는다.

| 사용 가능한 UI | 아직 외부 시스템 연결이 필요한 부분 |
| --- | --- |
| 웨이브·레벨·시간·Cost·적 예고·시너지 기준값 표시 | 전투 상태 구독, 자동 시간 감소, 배치 유닛 집계·시너지 버프 계산 |
| 준비/전투 화면 전환 | 실제 전투 시작·종료·일시정지 |
| 카드 이름·설명·성급·능력치·그림·점유 형태 표시 | 카드 추첨·구매·리롤·비용 차감·배치 판정·성급 합성 |
| 스킬 아이콘·분류 프레임·남은 쿨다운 표시 | 스킬 발동·조준·자동 쿨다운 진행·로비 저장 장착 결과 연동 |
| 승리/패배 기록 표시·로비 이동 | 승패 판정·기록 집계·경험치/보상 지급·저장 |
| 증강 후보 표시·선택 이벤트 | 후보 추첨·실제 증강 효과 적용·런 데이터 변경 |

실제 데이터와 UI 사이에 연결 컴포넌트를 두고, 데이터가 바뀔 때 아래 표시 API를 호출하는 방향으로 사용한다. UI가 표시한 숫자를 전투 데이터의 원본으로 사용하지 않는다.

## 2. 빠른 실행

1. 팀 공유 폰트와 기존 `.meta`를 먼저 복원한다. 결과 전용 폰트 묶음은 [폰트 공유](#8-폰트와-아트-공유)를 참고한다.
2. `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`를 연다.
3. 현재 저장 상태는 **Combat 활성 / Preparation 비활성**이다. 그대로 Play Mode를 실행하면 전투 UI부터 표시된다. `UIPageGroup`에는 Awake/Start에서 시작 화면을 정하는 코드가 없다.
4. 준비 화면을 확인하려면 Play Mode에서 연결 코드로 `UIPageGroup.ShowPage(Canvas_Preparation의 GameObject)`를 호출한다. 또는 Edit Mode에서 `Canvas_Preparation`을 켜고 `Canvas_Combat`을 끈 뒤 실행한다. 후자는 씬 변경이므로 저장 여부를 의도적으로 결정한다. 준비 화면의 **전투 시작** 버튼은 준비 화면을 숨기고 Combat을 표시한다.
5. `UI_BattleScreens/Canvas_Combat/PreviewTriggers`의 버튼으로 결과/증강 화면을 확인한다.

   | 버튼 | 표시 대상 |
   | --- | --- |
   | `ClearButton` | `Canvas_Popups/Canvas_BattleVictory` |
   | `DefeatButton` | `Canvas_Popups/Canvas_BattleDefeat` |
   | `AugmentButton` | `Canvas_Popups/Canvas_AugmentSelection` |

6. 결과창의 **로비로** 버튼은 `UI_Lobby_MutedPreview`로 이동한다. 증강창은 카드 하나를 선택하면 선택 이벤트를 전달한 뒤 닫힌다.
7. Play Mode를 종료한다. 실행 중 Inspector에서 바꾼 미리보기 값은 일반적인 Unity Play Mode 편집과 마찬가지로 저장되지 않는다.

승리·패배 및 증강 선택은 임의로 넘기는 화면이 아니므로 `Can Dismiss`가 꺼져 있다. **ESC로 닫히지 않는 것이 현재 설정이다.** 일반 메뉴 팝업의 ESC 동작과 구분한다.

### 승리·패배 캡처

이전 구현 검증에서 저장한 2026-09-28 Play Mode 캡처다. 이번 문서 작성 시 새로 촬영한 화면은 아니다.

- [승리 화면](../../Tools/Art/Previews/ResultRefinement_Victory_Play-1.png)
- [패배 화면](../../Tools/Art/Previews/ResultRefinement_Defeat_Play.png)
- 폴더: `Tools/Art/Previews/`

## 3. 화면 구조와 프리팹 수정 위치

```text
UI_Root
  UIPopupController / UISceneNavigator

UI_BattleScreens
  UIPageGroup / UIBattleMutedPreviewView / CanvasGroup
  Canvas_GetReady       공통 HUD·적 예고·시너지
  Canvas_Preparation    Cost·리롤·카드·전투 시작
  Canvas_Combat         전투 스킬 슬롯·미리보기 버튼

Canvas_Popups
  Canvas_BattleVictory
  Canvas_BattleDefeat
  Canvas_AugmentSelection
  기존 메뉴·시너지 등 팝업
```

위 표기는 역할을 설명하는 구조도다. `UI_Root`, `UI_BattleScreens`, `Canvas_Popups`의 실제 부모/형제 관계와 참조는 씬에서 확인한다.

### 이미 프리팹으로 분리된 대상

| 수정 목적 | 프리팹 경로 |
| --- | --- |
| 공통 HUD·적 예고·시너지 화면 | `Assets/02.Prefabs/UI/UI_Panel/Canvas_GetReady.prefab` |
| 상단 웨이브·레벨·시간·경험치 표시 | `Assets/06.UI/BattleMutedPreview/Prefabs/BattleHud.prefab` |
| 이번 웨이브 적 예고 패널 | `Assets/06.UI/BattleMutedPreview/Prefabs/WavePreviewPanel.prefab` |
| 준비 화면 | `Assets/02.Prefabs/UI/UI_Panel/Canvas_Preparation.prefab` |
| 전투 진행 화면 | `Assets/02.Prefabs/UI/UI_Panel/Canvas_Combat.prefab` |
| 시너지 공통 행 | `Assets/06.UI/BattleMutedPreview/Prefabs/SynergyTracker.prefab` |
| 전투 스킬 공통 슬롯 | `Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Prefabs/CombatSkillSlot.prefab` |
| 유닛 카드 | `Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCard_Unit.prefab` |
| 땅 슬롯 카드 | `Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCard_LandSlot.prefab` |
| 기물 카드 | `Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCard_Relic.prefab` |
| 승리·패배 공통 배치 | `Assets/06.UI/BattleMutedPreview/Overlays_v1/Prefabs/Canvas_BattleResultBase.prefab` |
| 승리 상태 Variant | 같은 폴더의 `Canvas_BattleVictory.prefab` |
| 패배 상태 Variant | 같은 폴더의 `Canvas_BattleDefeat.prefab` |
| 증강 선택 화면 | 같은 폴더의 `Canvas_AugmentSelection.prefab` |
| 증강 공통 카드 | 같은 폴더의 `AugmentChoiceCard.prefab` |

### 수정 원칙

- 공통 배치·크기·폰트·테두리는 원본 프리팹에서 수정한다. 승리/패배 공통 변경은 `Canvas_BattleResultBase`가 기준이다.
- 종류별 아이콘·색·표시 데이터와 씬 연결은 개별 인스턴스 또는 Variant에서 유지한다. 개별 설정을 공통 원본에 `Apply All` 하지 않는다.
- 원본 수정이 특정 인스턴스에 반영되지 않으면 해당 속성의 Override부터 확인한다. 전체 Revert로 다른 작업을 지우지 않는다.
- 프리팹을 복제하면 이후 원본 변경이 서로 공유되지 않는다. 공통 디자인을 공유할 때는 같은 프리팹의 인스턴스를 배치한다.
- Scene/Prefab을 텍스트로 직접 편집하지 않는다. Unity Editor에서 수정하고 `.meta`를 보존한다.
- 기존 `Legacy_` 및 이전 팝업은 비활성 보존 대상이다. 새 UI와 동시에 켜지 않도록 한다.

### HUD·적 예고 중첩 프리팹 (2026-09-28)

`Canvas_GetReady`의 기존 `BattleHUD`와 `WavePreview`를 위 두 프리팹의 중첩 인스턴스로 분리했다. 씬에서 개별 복사본을 만든 것이 아니므로 원본의 공통 디자인을 수정하면 부모 Canvas와 씬 인스턴스에 반영된다. 에셋 루트 이름은 `BattleHud` / `WavePreviewPanel`이며, 부모 Canvas와 씬의 중첩 인스턴스 이름은 기존 `BattleHUD` / `WavePreview`를 유지한다.

- `BattleHud`: 웨이브·레벨·시간·경험치 바·눈금. 메뉴 버튼은 포함하지 않는다.
- `WavePreviewPanel`: 제목·프레임·적 아이콘/이름/수량 3칸.
- 둘 다 표시 전용이며 Canvas나 별도 Runtime View를 새로 추가하지 않았다. 새 씬에서는 상위 Canvas 아래에 배치하고 `UIBattleMutedPreviewView`에 참조를 연결한다.
- 기존 View의 HUD 4개 및 적 이름/수량 6개 참조, 위치·크기·Sprite·Text·활성 상태·형제 순서, 기존 버튼 연결을 보존했다. 시너지·메뉴·준비/전투 Canvas는 분리 대상이 아니다.
- 이 구조 변경은 실제 웨이브 예고 계산이나 전투 로직을 추가하지 않는다.

분리 도구는 `Assets/Editor/BattleSharedPanelPrefabExtractor.cs`, 메뉴는 `Tools > OZGL2 > Battle > Extract Shared HUD And Wave Prefabs`다. 이미 분리가 끝났으므로 팀원이 사용하기 위해 다시 실행할 필요가 없으며, 대상 프리팹이 있으면 재실행을 차단한다. 씬 참조 수정은 Undo 대상이나 프리팹 에셋 저장은 Undo만으로 되돌릴 수 없다. 실행 전 사본은 `Tools/Art/Backups/BattleSharedPanels_20260928_115948/`에 있다.

준비 카드 3종은 초기 프리팹 제작 이후 `UI_BattleScreens/Canvas_Preparation/ChoiceCards/CardPrefabInstances` 아래에 배치되었다. 상세 편집 항목은 [BattlePreparationCards.md](BattlePreparationCards.md)를 확인한다. 카드의 상단 `CategoryText`와 `SetCategory` API도 제거된 상태다.

## 4. 다른 씬에 배치할 때 Inspector 연결

**Canvas 프리팹을 드래그하는 것만으로 씬 외부 버튼 연결까지 완성되지는 않는다.** 현재 원본 Canvas 프리팹에는 씬 참조를 저장할 수 없는 버튼 이벤트 자리가 있으며, `UI_Battle_MutedPreview`의 인스턴스 Override에서 연결한다.

| 대상 | 연결할 값 |
| --- | --- |
| `UIPopupController._popupRoot` | 팝업들을 담는 `Canvas_Popups` Transform |
| `UIPopupController._screenGroup` | 배경 화면 입력을 막을 `UI_BattleScreens` CanvasGroup |
| `UIPageGroup._pages` | 준비 Canvas GameObject와 전투 Canvas GameObject |
| `StartCombatButton` On Click | `UIPageGroup.ShowPage`에 전투 Canvas GameObject 전달 |
| 결과/증강 미리보기 버튼 On Click | `UIPopupController.OpenPopup`에 각 대상 `UIPopupPanel` 전달 |
| 메뉴/시너지 버튼 On Click | `UIPopupController.OpenPopup`에 해당 팝업 전달 |
| 각 팝업 `UIPopupPanel` | CanvasGroup, 필요 시 `First Selected`, 화면에 맞는 `Can Dismiss` |
| `UIBattleMutedPreviewView` | 웨이브·레벨·시간·Cost·리롤 Text, 경험치 Fill, 적/시너지 Text 배열 |
| 결과 `UIBattleResultView` | TMP·Image·Slider·로비 Button·Popup·Navigator·로비 경로 |
| `UIAugmentSelectionView` | Panel, 카드 View 배열, Visual Catalog, Preview Choices |
| 전투 `UICombatSkillSlotView` | Category Style, Icon/Frame/Tint, Cooldown Track/Fill/Ticks/TMP, Preview Catalog/Entry Id |

- `UIPopupController.OpenPopup`은 대상이 `_popupRoot`의 자식이 아니면 열지 않는다.
- 팝업은 Controller로 열고 닫는다. 단순 `SetActive`만 사용하면 팝업 스택·배경 입력·선택 복원이 맞지 않을 수 있다.
- 준비 카드에는 자체 Canvas나 Button이 없다. 별도의 카드 입력/구매 컴포넌트를 연결해야 실사용 입력이 생긴다.
- 새 씬에는 EventSystem, InputSystemUIInputModule, Canvas의 GraphicRaycaster가 필요하다. 별도 LayerMask/Collider/Rigidbody/Animator 설정은 현재 UI 자체에 필요하지 않다.
- 로비 복귀 경로는 `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`다.
- Editor에서는 `UIFlowPreviewSceneLoader`가 UI_Flow 경로의 씬 이동을 보조한다. 빌드에서는 이동 대상 씬을 Build Profiles의 Scene List에 등록해야 한다. Editor 이동 성공만으로 빌드 이동까지 검증된 것은 아니다.

## 5. 스크립트와 표시 API

아래 예시는 **기존 API를 사용하는 연결 예시**이며 프로젝트에 새 게임 연동 코드를 추가했다는 뜻이 아니다. 변수는 연결 담당 컴포넌트의 `[SerializeField]` 참조로 준비한다. 반복적인 `GameObject.Find`로 UI를 찾지 않는다.

공통 namespace는 `OZGL2.UIFlow`다. 증강 데이터만 `OZGL2.Augment.AugmentData`를 사용한다.

### 5.1 HUD·적 예고·시너지

파일: `Assets/00.Project/01.Scripts/UI/Battle/UIBattleMutedPreviewView.cs`

```csharp
using OZGL2.UIFlow;

// UIBattleMutedPreviewView hud에 값을 전달한다.
hud.SetWave(3, 10);
hud.SetLevelExperience(20, 0.6f);
hud.SetRemainingTime(42f);
hud.SetCost(100, 100);
hud.SetEnemy(0, "근접 용사", 12);
hud.SetSynergy(0, "마법 결속", 3, 5);
```

| 메서드 | 입력 의미 |
| --- | --- |
| `SetWave(int wave, int totalWaves)` | 현재/전체 웨이브 |
| `SetLevelExperience(int level, float normalizedExperience)` | 레벨과 0~1 경험치 비율 |
| `SetRemainingTime(float seconds)` | 외부에서 계산한 남은 초 |
| `SetCost(int cost, int rerollCost)` | 보유 Cost와 리롤 비용 표시 |
| `SetEnemy(int index, string name, int count)` | 현재 3개 칸의 이름/수량, index 0~2 |
| `SetSynergy(int index, string name, int firstThreshold, int nextThreshold)` | 현재 4개 행의 이름/기준값, index 0~3 |
| `RefreshView()` | 현재 보유한 표시값 다시 적용 |

`3 > 5`는 **현재 배치 수가 아니라 시너지 발동 기준값**이다. 색상도 첫 기준/다음 기준의 표시색이며 실제 버프 발동 상태가 아니다. 시너지 이름과 기준값은 Text를 직접 고치지 않고 View의 Preview Synergies 또는 `SetSynergy`로 바꾼다. 적 이름·수량도 동일하다.

이 View에는 자동 카운트다운이 없다. 현재 API는 View 내부 미리보기 필드를 갱신하며 실제 SO나 저장 데이터에는 쓰지 않는다. 실제 게임 연결 시 데이터 소유자는 별도로 유지한다.

### 5.2 준비 카드

파일: `Assets/00.Project/01.Scripts/UI/Battle/UIBattlePreparationCardView.cs`

```csharp
using UnityEngine;

card.SetTitle("그림자 검사");
card.SetRank(1);
card.SetTrait("특성", "3성 달성 시\n30초마다 마법검 발사");
card.SetSkill("보유 스킬", "스킬 없음");
card.SetStats("123", "45%", "678");
card.SetArtwork(unitSprite);
card.SetTypeIcon(typeSprite);
card.SetFootprint(new[]
{
    new Vector2Int(2, 1),
    new Vector2Int(2, 2),
    new Vector2Int(3, 2)
});

// 땅 슬롯 카드의 별도 설명 항목이다.
landCard.SetAreaDescription("추가 배치 영역", "배치 가능 영역 +4칸");
```

정확한 API는 `SetTitle(string)`, `SetRank(int)`, `SetTrait(string, string)`, `SetSkill(string, string)`, `SetStats(string, string, string)`, `SetAreaDescription(string, string)`, `SetArtwork(Sprite)`, `SetTypeIcon(Sprite)`, `SetFootprint(Vector2Int[])`다.

- 문구는 `UnityEngine.UI.Text`다. View는 OnEnable/Update로 문구를 덮어쓰지 않는다.
- 능력치는 문자열 그대로 표시한다. 단위 변환·수치 계산·성급 제한은 호출하는 게임 시스템의 책임이다.
- 기본 점유 격자는 6×5, 좌상단이 `(0,0)`, y가 아래로 증가한다. 셀 배열 순서는 `y * width + x`다.
- `SetFootprint`는 기존 점유 표시를 지우고 유효한 좌표만 켠다. null/빈 배열은 모두 비우고, 중복/범위 밖 좌표는 안전하게 무시한다.
- Sprite에 null을 전달하면 해당 Image를 숨긴다. 종류에 없는 선택적 필드는 비워 두어도 된다.
- 문구를 바꿔도 실제 마법검·피해 증가·배치 영역 확장 효과가 생기지 않는다.

### 5.3 전투 스킬 슬롯과 쿨다운

파일: `Assets/00.Project/01.Scripts/UI/UICombatSkillSlotView.cs`

```csharp
// entry는 UISkillPreviewCatalogSO.Entries에서 Id로 찾은 항목이다.
slot.ShowSkill(entry);
slot.SetCooldown(8f, 40f); // 남은 시간 8초, Fill 비율 0.2

slot.ShowSkill(null);     // 빈 슬롯 표시
slot.UsePreviewValues(); // Inspector 미리보기로 복귀
```

정확한 메서드는 `ShowSkill(UISkillPreviewCatalogSO.Entry entry)`, `SetCooldown(float remainingSeconds, float duration)`, `UsePreviewValues()`다.

- ShowSkill로 새 스킬을 지정하면 이전 스킬의 쿨다운 표시를 초기화한다.
- 1은 쿨다운 전체, 0은 사용 가능이다. 초는 올림하여 TMP에 표시하고 0이면 숫자를 숨긴다.
- 자체 타이머나 실제 시전 기능은 없다. 런타임 스킬이 제공하는 남은 시간/전체 시간을 계속 전달해야 한다.
- 실제 `SkillCategory`와 `eSkillPreviewCategory`의 BUFF/DEBUFF 순서가 다르므로 숫자 캐스팅하지 않는다. 이름별 명시 매핑을 사용한다.
- Inspector의 Preview Catalog / Preview Entry Id / Preview Remaining Seconds / Preview Duration으로 정지된 표시를 확인할 수 있다.
- 현재 슬롯 Button은 비활성이고 시전 클릭 이벤트가 없다. 표시 API 호출만으로 클릭 기능이 생기지 않는다.

### 5.4 승리·패배 결과

파일: `Assets/00.Project/01.Scripts/UI/Battle/UIBattleResultView.cs`

```csharp
result.ConfigureNavigation(
    navigator,
    "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity");

result.SetState(eBattleResultState.VICTORY);
result.SetData(new BattleResultDisplayData(
    difficulty: "보통",
    seconds: 522f,
    kills: 128,
    deployments: 12,
    earnedXp: 2400,
    level: 20,
    normalizedXp: 0.6f,
    didLevelUp: true));

popups.OpenPopup(result.Popup);
```

패배는 `eBattleResultState.DEFEAT`를 사용한다. 기존 승리/패배 Variant는 상태가 설정되어 있으므로 각 View에 데이터만 전달해도 된다.

API: `SetData(BattleResultDisplayData data)`, `SetState(eBattleResultState state)`, `ConfigureNavigation(UISceneNavigator navigator, string lobbyScenePath)`, `RefreshView()`, `ReturnToLobby()`.

- `SetData(null)`은 Inspector Preview Data로 복귀한다.
- 경험치 Slider는 표시 전용 0~1이다. Slider 값을 직접 고치면 다음 갱신에서 데이터의 `NormalizedXp`가 우선한다.
- 결과창은 `08분 42초`, `2,400 exp`, `LV.20`, `LV UP!` 형식으로 표시한다.
- 결과 제목색은 `Title Color`, 난이도 강조는 `Difficulty Accent`, 룬/제목 배경 강조는 Victory/Defeat Accent로 조절한다. TMP 색만 고치면 Refresh에서 다시 적용될 수 있다.
- 로비 버튼은 View가 실행 중 이벤트를 구독한다. Inspector에 같은 `ReturnToLobby` 이벤트를 중복 추가하지 않는다.
- 결과 View는 데이터 표시만 한다. 경험치 계산·지급·저장은 호출 전에 담당 시스템에서 처리한다.

### 5.5 증강 후보와 선택

파일:

- `Assets/00.Project/01.Scripts/UI/Battle/UIAugmentSelectionView.cs`
- `Assets/00.Project/01.Scripts/UI/Battle/UIAugmentCardView.cs`
- `Assets/00.Project/01.Scripts/UI/Battle/UIAugmentVisualCatalogSO.cs`

후보 전달 예시:

```csharp
// choices: IReadOnlyList<OZGL2.Augment.AugmentData>
augmentView.SetChoices(choices);
popups.OpenPopup(augmentPanel);

// 정지된 Inspector 후보로 돌아갈 때만 호출한다.
augmentView.UsePreviewChoices();
```

외부 연결 컴포넌트의 이벤트 구독 예시:

```csharp
private void OnEnable()
{
    _augmentView.AugmentSelected += HandleAugmentSelected;
}

private void OnDisable()
{
    if (_augmentView != null)
        _augmentView.AugmentSelected -= HandleAugmentSelected;
}

private void HandleAugmentSelected(OZGL2.Augment.AugmentData selected)
{
    // 여기서 팀의 실제 런 시스템에 선택을 전달한다.
    // 현재 UI는 AugmentRun.Draw3/Pick 또는 효과 적용을 대신 호출하지 않는다.
}
```

이 예시는 `_augmentView`가 Inspector에서 필수 연결된 경우다. 연결 컴포넌트의 초기화 단계에서도 누락 참조를 검사한다.

- 후보 API: `SetChoices(IReadOnlyList<AugmentData> choices)`, `UsePreviewChoices()`, `RefreshChoices()`.
- 선택 이벤트: `event Action<AugmentData> AugmentSelected`.
- 최대 3개, 동일 등급 후보를 전달한다. 앞 3개에 다른 등급이 섞이면 경고 후 새 입력을 적용하지 않는다.
- 비활성 상태에서 SetChoices 후 열어도 전달한 후보를 미리보기 값으로 덮어쓰지 않는다.
- 선택하면 중복 입력을 막고 이벤트를 전달한 뒤, 해당 창이 여전히 최상위일 때만 닫는다.
- 이름·설명·등급은 기존 `AugmentData` SO에서 편집한다. `UIAugmentVisualCatalog.asset`은 효과 수치가 아니라 등급별 벨벳/설명판/문장/색/아이콘만 관리한다.
- Tier 1/2/3은 실버/골드/플래티넘이다. 아이콘 매핑의 Augment Id는 `AugmentData.augmentId`와 같아야 한다.
- 독립 카드 API는 `Bind(AugmentData, UIAugmentVisualCatalogSO)`, `SetSelectionState(bool isSelected, bool canSelect)`, `RefreshVisuals()`다. 일반적인 화면 연결은 카드별 이벤트보다 Selection View의 `AugmentSelected`를 사용한다.

## 6. 공통 페이지·팝업·씬 이동 사용법

| 클래스 / 파일 | 핵심 API와 주의 사항 |
| --- | --- |
| `UIPageGroup` / `Assets/00.Project/01.Scripts/UI/UIPageGroup.cs` | `ShowPage(GameObject target)`. `_pages`에 등록된 대상만 허용. 활성 상태만 전환하며 게임 상태/Time.timeScale은 변경하지 않음 |
| `UIPopupController` / 같은 UI 폴더 | `OpenPopup(UIPopupPanel)`, `CloseTopPopup()`, `CloseConfirmedPopup()`, `IsTopPopup(UIPopupPanel)`. 배경 입력과 팝업 스택/선택 복원을 관리 |
| `UIPopupPanel` / 같은 UI 폴더 | `CanDismiss`, `FirstSelected`, `SetDismissGuard(Func<bool>)`, `SetInteractable(bool)`. 팝업 닫기 정책과 CanvasGroup 입력 제어 |
| `UISceneNavigator` / 같은 UI 폴더 | `LoadScene(string scenePath)`. 빌드 등록 씬 로드 또는 Editor 미리보기 이동 |

`CloseTopPopup()`은 닫기 허용 여부를 검사하며 ESC도 이 메서드를 사용한다. `CloseConfirmedPopup()`은 증강 선택 완료처럼 닫기 정책을 통과한 확정 처리용이다. 일반 뒤로가기 동작에서 무조건 Confirmed를 호출해 필수 선택 정책을 우회하지 않는다.

## 7. 디자인을 바꿀 때 확인할 위치

| 항목 | 수정 위치 |
| --- | --- |
| 준비 하단 벨벳 밝기 | `Canvas_Preparation/BottomNobleBackground > Image > Color` |
| 전투 하단 벨벳 | `Canvas_Combat/BottomSkillBackground > Image` |
| 스킬 공통 크기·링·눈금 | `CombatSkillSlot.prefab`의 FrameArt / CooldownTrack / CooldownFill / CooldownTicks |
| 시너지 공통 테두리·배치 | `SynergyTracker.prefab`의 Frame / Icon / Nameplate / Name / Thresholds |
| 시너지별 색·아이콘 | 각 인스턴스의 Frame Color / Icon Source Image |
| 카드 몸체·종류 아이콘 | 각 `BattleCard_*`의 Shell / TypeBadge / Artwork |
| 결과 어두운 배경 | 결과 Base의 `BackgroundShade > Image > Color` |
| 결과 통계·경험치 | 결과 Base의 `Content/RecordPanel` |
| 결과 하단 장식 | 결과 Base의 BannerTailLeft / BannerTailRight 및 View의 상태별 Sprite |
| 증강 등급 배경 | `UIAugmentVisualCatalog.asset`의 Tiers |
| 증강 카드 공통 배치 | `AugmentChoiceCard.prefab` |

시너지의 현재 이름 판 외곽선은 `Nameplate/ReferenceTop`, `ReferenceBottom`, `ReferenceLeft`, `ReferenceRight`다. 비활성 `Nameplate/Border`는 이전 이미지다. 새 테두리로 바꿀 때 활성 상태를 함께 확인한다.

## 8. 폰트와 아트 공유

**Git clone만으로 모든 폰트 참조가 완성되지 않는다.** 기존 폰트 공유 정책은 [FontAssetSharing.md](../FontAssetSharing.md)를 따른다.

- 결과 전용 SDF 배포 묶음: [BattleResultFonts_20260928.unitypackage](../../Tools/Art/Exports/BattleResultFonts_20260928.unitypackage).
- 결과 제목: HS봄바람체 기반 `Assets/06.UI/BattleMutedPreview/ResultRefinement_v2/Fonts/BattleResultTitle SDF.asset`.
- 결과 본문/숫자: KMU Sungkok Serif 기반 같은 폴더의 `BattleResultBody SDF.asset`.
- 두 결과 폰트는 Static SDF다. 새 한글 문구가 필요하면 `Assets/Editor/BattleResultTypography.cs`의 `BODY_CHARACTERS` 등 해당 문자 목록을 확장하고 기존 GUID를 유지해 재생성해야 한다.
- 결과 폰트 패키지에는 원본 TTF/OTF를 재배포하지 않았다. 재생성할 때는 팀이 가진 원본과 라이선스 조건을 확인한다.
- 기존 HUD/카드의 DOSGothic·NotoSansCJKkr, 증강의 BattleOverlay Pixel/Symbols 등은 별도 공유 의존성이 남는다. 결과 폰트 패키지 하나가 모든 UI 폰트를 대신하지 않는다.
- 원래 경로와 `.meta` GUID를 함께 복원한다. 같은 GUID의 사본을 여러 경로에 중복 배치하지 않는다.
- 새로운 이미지/폰트로 교체할 때 기존 외부 원본을 덮어쓰지 않는다. 공유 범위와 재배포 가능 여부를 확인한다.

## 9. Editor 도구 사용 주의

완성된 디자인의 일상 편집은 **Prefab Mode와 Inspector**에서 한다.

- `Create Muted Preview Scene`, `Create Card Prefabs`, `Create And Apply Overlay Prefabs`는 최초 생성 도구다. 팀원이 UI를 사용하기 위해 다시 실행할 메뉴가 아니다.
- 생성 도구는 기존 에셋이 있으면 중단할 수 있으며 현재 완성본을 복구/갱신하는 용도가 아니다.
- `Refine Result Reference Layout`, HUD/배치 Polish 메뉴는 지정한 디자인 수치를 다시 적용한다. 실행하면 사용자가 미세 조정한 값을 바꿀 수 있으므로 의도적으로 사용할 때만 실행한다.
- 검증 메뉴와 생성/보정 메뉴를 구분한다. `Validate Card Prefabs`, `Validate Result Reference Prefabs`는 격리된 복제본을 검사하는 도구다.
- 프리뷰 렌더는 독립 프리팹 화면이며 실제 Play Mode 검증과 다르다. 같은 출력 PNG 경로를 덮어쓸 수 있다.
- 씬 Undo와 프리팹/폰트/이미지 에셋 저장의 복구 범위는 다르다. 작업 전 Git 상태와 백업을 확인한다.

## 10. 검증 기록과 팀 인수 체크리스트

### 이전 구현에서 기록된 검증

- 2026-09-28 결과 UI: 컴파일/Console 오류·경고 0개, 필수 참조·SDF 글리프·상태별 아트·표시 데이터·XP 범위 검사, Play Mode 승리/패배 표시 및 로비 이동 확인. 근거: [BattleResultReferenceRefinement.md](BattleResultReferenceRefinement.md).
- 2026-09-26 스킬 HUD: 표시 API/빈 슬롯/쿨다운 비율, 공통 프리팹 참조와 Play 화면 확인. 실제 시전·자동 쿨다운은 미연결. 근거: [CombatSkillHud.md](CombatSkillHud.md).
- 시너지·증강·카드의 이전 검증은 [BattleMutedPreview.md](BattleMutedPreview.md), [BattleOverlayPrefabs.md](BattleOverlayPrefabs.md), [BattlePreparationCards.md](BattlePreparationCards.md)를 참고한다.

### 이번 인수 점검에서 확인한 범위

- 저장된 Runtime 메서드 서명과 데이터/이벤트 흐름을 읽었다.
- 기존 프리팹 파일과 씬 외부 참조 연결 방식, 결과/증강 Can Dismiss 설정을 확인했다.
- Unity MCP 읽기전용 점검에서 Edit Mode/씬 저장 상태, Combat 활성/Preparation 비활성, HUD View의 Text/Image 참조, StartCombatButton의 Combat 페이지 연결, 결과/증강 미리보기 3개 버튼 및 메뉴→Popup_Settings 연결을 확인했다. 실제 버튼 실행 테스트와는 구분한다.
- 기존 승리/패배 캡처와 결과 폰트 패키지의 파일 존재를 확인했다.

Play Mode를 한 번 실행하여 다음 항목을 각각 한 번씩 확인했다. 버튼은 `Button.onClick.Invoke()`로 호출했으며 표시값은 해당 View API로 전달했다.

| 확인 항목 | 이번 결과 |
| --- | --- |
| `ShowPage(Preparation)` 후 전투 시작 버튼 이벤트 | Preparation→Combat 전환 확인 |
| Clear/Defeat/Augment 버튼 이벤트 | 각각 새 승리/패배/증강 팝업 열기 확인 |
| 증강 첫 카드 버튼 이벤트 | 선택 이벤트 1회 발생 및 팝업 닫기 확인 |
| HUD 표시 API | 웨이브 4/10, 시간 01:05, 경험치 0.25, 적 수량 9 반영 확인 |
| 메뉴 열기 후 `CloseTopPopup()` | 팝업 닫기 및 배경 입력 복원 확인 |
| 스킬 `SetCooldown(8f, 40f)` | Fill 0.2, TMP 숫자 8 확인 |
| 검증 종료 | Play Mode 종료, 최종 Console Error 0 / Warning 0 확인 |

물리 마우스 히트 영역, 다른 해상도/화면비, 실제 전투·보상·증강 효과 연동 및 빌드는 이번에 검증하지 않았다. **로비로 실제 씬 이동한 결과는 위의 이전 구현 검증 기록이며 이번 인수 점검에서 재실행한 항목은 아니다.**

### HUD·적 예고 분리 후 추가 확인

- Unity 컴파일 후 두 새 중첩 원본 연결, 10개 View 참조, 씬의 Missing Script/누락 직렬화 참조 0개 확인.
- 분리 전후 두 패널의 배치·아트·문구·활성 상태·형제 순서 및 씬 전체 버튼의 이벤트 대상/인수 동일 확인.
- Play Mode 한 번에서 HUD 7/10·LV.22·01:23·경험치 0.75와 적 3개 이름/수량 갱신을 확인했다.
- 전투 시작, 메뉴 및 시너지 4행 버튼 이벤트를 각각 한 번 호출하여 전환과 열기/닫기를 확인했다. 물리 마우스 검증은 아니다.
- Play Mode 종료 후 Console Error 0 / Warning 0. 신규 Runtime 코드, ProjectSettings, Packages 변경 없음.
- 씬은 저장했지만 추출 직전 백업과 파일 내용이 동일하다. 이번 구조 변경은 부모 `Canvas_GetReady.prefab`과 새 프리팹 두 개에 직렬화되었고, Git에 이미 보이는 씬 변경은 앞선 작업 내용이다.
- 충돌 검토 대상은 `UI_Battle_MutedPreview.unity`, `Canvas_GetReady.prefab`, 신규 프리팹 2개와 Unity 생성 `.meta`, Editor 도구 및 이 문서다. 앞선 결과 UI 변경과 사용자 Canvas 프리팹화는 별도 기존 변경으로 보존한다.

### 팀원 적용 후 확인

- [ ] 공유 폰트와 `.meta`를 복원하고 Missing Font/Material/Shader가 없다.
- [ ] Unity 컴파일 및 Console 오류·경고, NullReferenceException/MissingReferenceException을 확인했다.
- [ ] 새 씬에서 버튼의 Controller/PageGroup 대상과 팝업 인수가 연결되어 있다.
- [ ] 준비→전투 전환과 메뉴/시너지 열기·닫기, 팝업 뒤 UI 입력 차단을 확인했다.
- [ ] 승리/패배 데이터·장식·경험치 비율과 로비 이동을 확인했다.
- [ ] 증강 후보가 동일 등급이며 선택 이벤트 1회/닫기 동작이 맞다.
- [ ] 카드의 긴 이름·설명과 실제 화면 크기의 가독성을 확인했다.
- [ ] 스킬 0/중간/완료 쿨다운과 빈 슬롯을 확인했다.
- [ ] 다른 해상도/화면비에서 잘림·앵커·입력 영역을 확인했다.
- [ ] 빌드가 필요하면 Scene List와 실제 빌드의 씬 이동을 별도 확인했다.
- [ ] 기능 연결 시 표시값과 실제 게임 데이터/저장값의 소유자를 분리했다.
- [ ] Scene/Prefab/Meta 변경, 인스턴스 Override, 폰트 배포 버전, 미커밋 사용자 변경을 Git Diff에서 구분했다.
- [ ] 기능 완료 처리 시 PM/WBS 상태를 팀과 함께 갱신했다.

## 관련 문서

- [전투 HUD·적·시너지 및 하단 구성](BattleMutedPreview.md)
- [준비 카드 상세 편집](BattlePreparationCards.md)
- [전투 스킬 HUD](CombatSkillHud.md)
- [결과·증강 프리팹](BattleOverlayPrefabs.md)
- [최신 승리·패배 디자인](BattleResultReferenceRefinement.md)
- [폰트 공유 정책](../FontAssetSharing.md)
