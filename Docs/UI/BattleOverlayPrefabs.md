# 전투 결과·증강 선택 프리팹

## 적용 범위

- 적용 씬: `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`
- 기존 생성 아트로 승리·패배·증강 선택 Canvas를 구성하고, 기존 준비 카드 프리팹 3종을 배치했다.
- 원본 `UI_Battle.unity`, 실제 전투·증강 추첨·효과 적용·보상 지급·저장 규칙은 변경하지 않았다.
- 모든 이름·설명·숫자는 이미지에 넣지 않고 Text 또는 TMP로 유지한다.
- 팝업은 기존 `UIPopupController`를 사용한다. 새 패널도 `Canvas_Popups` 하위에 있어야 정상적으로 열린다.

## 편집할 위치

새 프리팹·카탈로그·폰트의 루트는 `Assets/06.UI/BattleMutedPreview/Overlays_v1/`다.

| 대상 | 편집 파일 / 씬 위치 |
| --- | --- |
| 승리·패배 공통 배치 | `Prefabs/Canvas_BattleResultBase.prefab` |
| 승리 표시 상태 | `Prefabs/Canvas_BattleVictory.prefab` |
| 패배 표시 상태 | `Prefabs/Canvas_BattleDefeat.prefab` |
| 증강 선택 화면 | `Prefabs/Canvas_AugmentSelection.prefab` |
| 증강 카드 공통 디자인 | `Prefabs/AugmentChoiceCard.prefab` |
| 등급별 아트·아이콘 매핑 | `UIAugmentVisualCatalog.asset` |
| 실제 씬의 새 팝업 3개 | `Canvas_Popups/Canvas_BattleVictory`, `Canvas_BattleDefeat`, `Canvas_AugmentSelection` |
| 준비 카드 인스턴스 | `UI_BattleScreens/Canvas_Preparation/ChoiceCards/CardPrefabInstances` |

승리·패배는 별도 복사본이 아니라 `Canvas_BattleResultBase`의 **Prefab Variant**다. 공통 위치·프레임·글자 배치는 Base에서 수정하고, 상태에 따른 차이는 Variant의 `UIBattleResultView`에서 관리한다. 원치 않는 인스턴스 Override가 있으면 Base 수정이 해당 항목에 반영되지 않을 수 있으므로 Inspector의 Overrides를 함께 확인한다.

각 팝업의 `BackgroundShade > Image > Color`에서 검은 배경의 불투명도를 바꿀 수 있다. 기본값은 78%이며 별도 배경 그림 대신 기존 전투 화면 위를 어둡게 덮는다.

## 승리·패배 표시

`UIBattleResultView`는 같은 구조에서 다음 항목만 상태별로 바꾼다.

- 왕관과 좌우 깃발 Sprite
- 결과 제목과 강조색
- 시간 항목 이름: 승리 `클리어 시간`, 패배 `플레이 시간`

기록 패널·아이콘 위치·경험치 바·로비 버튼은 공통이다. 좌우 깃발은 같은 Sprite를 반전하여 사용한다.

### 값 수정과 경험치 Slider

- Inspector 미리보기: `UIBattleResultView > 전투 / 보상 시스템에 연결되지 않은 미리보기 값 > Preview Data` (`_previewData`)
- 갱신 API: `SetData(BattleResultDisplayData data)`
- 상태 변경 API: `SetState(eBattleResultState state)`
- 데이터 항목: 난이도, 시간, 처치 수, 배치 수, 획득 경험치, 레벨, 경험치 비율, 레벨업 여부

경험치의 기준값은 `BattleResultDisplayData.NormalizedXp`다. 0~1 범위로 제한한 값을 `Content/RecordPanel/Experience`의 Slider에 전달한다. Slider는 표시 전용이며 조작할 수 없다. Inspector에서 Slider만 바꾸면 View 갱신 때 데이터 값으로 다시 표시되므로 `_previewData`의 비율을 수정해야 한다.

`SetData`로 전달한 값은 런타임 표시용으로 보유하며 미리보기 설정 원본에 쓰지 않는다. `SetData(null)`은 미리보기 설정으로 돌아간다. 경험치 증가 계산이나 실제 지급은 이 View가 수행하지 않는다.

### 로비 이동

- 씬 인스턴스에는 기존 `UI_Root`의 `UISceneNavigator`를 연결했다.
- 이동 경로는 `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`다. 새 승리·패배창의 씬 인스턴스와 비활성 보존한 이전 결과창 2개의 로비 버튼까지 총 4곳을 동일하게 연결했다.
- 생성 도구도 같은 프리뷰 로비 경로를 사용하며, 대상 SceneAsset이 존재하는지 먼저 확인한다. 공용 Navigator와 원본 씬의 이동 경로는 변경하지 않는다.
- 결과 프리팹을 다른 씬에서 사용할 때는 `ConfigureNavigation(navigator, lobbyScenePath)` 또는 Inspector 연결이 필요하다.
- 이동 실패 시 결과창이 먼저 사라지지 않도록 버튼 처리에서 팝업을 선행해서 닫지 않는다.

## 증강 선택

### 정적 데이터와 표시 리소스

실제 증강의 이름·설명·등급은 기존 `OZGL2.Augment.AugmentData` SO를 읽는다. 새 `UIAugmentVisualCatalogSO`는 효과나 진행 상태가 아닌 표시 리소스만 관리한다.

카탈로그의 `Tiers` 항목:

| Tier | 표시 이름 | 분리 배경 | 문장 라벨 Y |
| --- | --- | --- | --- |
| 1 | 실버 | Silver | -122 |
| 2 | 골드 | Gold | -128 |
| 3 | 플래티넘 | Platinum | -110 |

- Tier 값으로 항목을 찾으므로 배열 인덱스 자체가 등급 판정 기준은 아니다.
- 각 항목에서 `Velvet Background`, `Description Panel`, `Crest`, `Grade Color`, `Grade Label Y`를 편집한다.
- `Icons`의 `Augment Id`는 `AugmentData.augmentId`와 정확하게 일치시킨다. 일치하는 유효 Sprite가 없으면 `Default Icon`을 사용한다.
- 기본 아이콘은 기존 카드의 굵은 흰색 검 `Cards_v1/Sprites/Icon_Type_Unit_Diamond_v2.png`를 재사용한다.
- `mon_hp`는 기존 `Sprites/Icon_BoneShield.png`에 별도 매핑했다. 아이콘 색상은 흰색을 유지한다.

### 분리된 카드 레이어

`AugmentChoiceCard`의 기본 크기는 600×1100이다.

- `VelvetBackground`: 벨벳과 바깥 금속 장식
- `DescriptionPanel`: 검정 설명 배경과 내부 테두리
- 두 Image는 동일한 600×1100 영역·중심에 겹쳐 놓는다. 결합 이미지 `Augment_Body_*`는 보존하지만 이 프리팹에서는 사용하지 않는다.
- `Crest`: 별도 문장 프레임. 기준 원본은 512×512이며 아이콘과 등급 문구는 직접 자식이다.
- `Crest/Icon`: 원본 중심 기준 Y +36. 문장의 현재 높이에 비례하여 정렬한다.
- `Crest/Grade`: 카탈로그의 등급별 `Grade Label Y`를 같은 방식으로 적용한다.
- `Name`, `Description`, `Effect`: TMP. Bind는 이 세 오브젝트의 위치나 글꼴 크기를 강제로 바꾸지 않는다.
- `HoverHighlight`: 등급 아트를 덮어쓰지 않는 별도 입력 강조 레이어. 호버·키보드 포커스·선택 표시만 담당한다.

화면에는 카드 원본을 균등 배율 0.60으로 배치한다. 카드 배율을 바꿀 때 X/Y를 함께 조절하여 마름모와 금속 장식을 찌그러뜨리지 않는다.

### 후보와 선택 API

- Inspector: `Canvas_AugmentSelection > UIAugmentSelectionView > Preview Choices`
- 외부 후보 전달: `SetChoices(IReadOnlyList<AugmentData> choices)`
- 선택 알림: `AugmentSelected` 이벤트
- 미리보기로 복귀: `UsePreviewChoices()`
- 표시 다시 적용: `RefreshChoices()`

최대 3개 후보를 표시하며 같은 등급만 허용한다. 다른 등급이 섞인 입력은 경고 후 적용하지 않는다. 비활성 상태에서 `SetChoices`로 전달한 후보를 다시 열 때 미리보기 배열로 덮어쓰지 않는다. 카드를 선택하면 입력 중복을 막고 선택 이벤트를 전달한 다음, 자신이 최상위 팝업일 때만 확인 닫기를 수행한다. ESC로 선택을 건너뛰는 동작은 허용하지 않는다.

초기 미리보기는 기존 실버 SO 3개다.

| ID | 이름 | 기존 SO 설명 |
| --- | --- | --- |
| `skill_damage` | 화력 강화 | 스킬 피해 +8% |
| `mon_hp` | 불굴의 대열 | 몬스터 체력 +8% |
| `mon_speed` | 재빠른 발놀림 | 몬스터 공격속도 +6% |

설명은 `AugmentData.description`을 그대로 사용한다. 별도 효과 라벨은 `isInstant`에 따라 `즉시 효과` 또는 `런 한정 증강`만 표시하며 수치를 재계산하지 않는다.

이번 구현은 UI 범위다. `AugmentRun.Draw3`, `Pick`, 실제 보상 지급을 호출하지 않는다. 실제 전투 연결 시 기존 시스템이 후보를 공급하고 `AugmentSelected`를 받아 적용해야 한다. 기존 추첨 규칙인 마일스톤별 같은 등급 3개 제시는 바꾸지 않는다.

## 준비 카드와 기존 오브젝트 보존

- `Cards_v1/Prefabs/BattleCard_Unit`, `BattleCard_LandSlot`, `BattleCard_Relic`의 기존 프리팹을 인스턴스로 배치했다.
- 기존 `ChoiceCards/Card_1~3`은 삭제하지 않고 비활성화했다. 기존 팝업 `Popup_StageClear`, `Popup_Defeat`, `Popup_AugmentSelection`도 비활성 보존했다.
- 준비 카드 원본의 600×1100 비율을 유지하여 현재 칸에서는 174×319로 표시된다. 폭을 늘려 찌그러뜨리지 않았다.
- 이 표시 크기에서는 카드 본문이 작게 보인다. 원본 Text·디자인은 유지했으며, 더 큰 가독성이 필요하면 이후 준비 카드 영역 확대 또는 축약 표시 설계를 별도로 진행한다.
- 준비 카드 배치는 선택·구매·리롤·전투 배치 기능을 추가한 것이 아니다.

미리보기 경로는 `UI_BattleScreens/Canvas_Combat/PreviewTriggers`다. `ClearButton`, `DefeatButton`, `AugmentButton`의 기존 팝업 열기 이벤트에서 대상 패널만 새 프리팹으로 바꿨다. 기존 메뉴·시너지 등 다른 팝업 연결은 유지한다.

## 폰트와 아트 Import

- 전용 프로젝트 폰트: `Overlays_v1/Fonts/BattleOverlay Pixel.asset`
- 기존 DOSMyungjo 원본으로 생성한 TMP 도트 폰트이며 Atlas는 Point 필터를 사용한다. 공유 폰트나 기존 폰트 에셋을 덮어쓰지 않는다.
- 원본에 없는 `−`, `×`, `·`는 NotoSansCJKkr 기반 프로젝트 전용 `Fonts/BattleOverlay Symbols SDF.asset`를 생성하여 보완했다. 두 폰트의 Atlas/Material도 에셋 내부에 보관한다.
- 동적 Atlas이므로 앞으로 새 글자를 추가하거나 폰트를 재생성할 환경에는 기존 공유 원본 폰트가 필요하다. 원본 폰트 위치는 `Assets/98.ExternalAssets/00.LocalStaging/01.Font/`다.
- 새 Sprite는 Single / FullRect / 중심 Pivot / Point / 무압축 / Mipmap 없음 / 원본 Alpha를 사용한다. Importer는 Unity에서 설정하고 `.meta`를 직접 수정하지 않는다.

## 생성 도구와 Undo

- 도구: `Assets/Editor/BattleOverlayPrefabBuilder.cs`
- 카드 배치 보조: `Assets/Editor/BattleOverlayCardPlacement.cs`
- 메뉴: `Tools > OZGL2 > Battle > Create And Apply Overlay Prefabs`

이 메뉴는 최초 생성·적용 도구다. 지정 씬의 저장된 Edit Mode에서만 실행한다. 기존 대상 프리팹·카탈로그·인스턴스가 있으면 중단하므로 **현재 완성본을 다시 만들거나 사용자 편집을 갱신하는 메뉴가 아니다**. 디자인 변경은 해당 프리팹과 카탈로그에서 한다.

- Undo 지원: 씬 안 새 인스턴스 배치, 기존 대상 비활성화, 버튼 대상 변경.
- Undo 제외: 새 Prefab / SO / Font 에셋 파일 생성, 텍스처 Importer 설정. 씬 Undo만으로 새 에셋 파일이 삭제되는 것은 아니다.
- 미저장 씬을 자동으로 덮어쓰지 않는다. 실패 시 해당 씬 Undo 그룹을 되돌리도록 처리한다.

## 확인 결과와 남은 확인

구현 작업에서 확인한 항목:

- Unity 컴파일 완료, 최종 확인 시 Console Error / Warning 0개.
- 최종 씬의 Missing Script / 누락된 직렬화 참조 0개, 저장되지 않은 변경 없음.
- 새 팝업 열기와 증강 선택 후 확인 닫기 동작.
- 실버·골드·플래티넘 3개 등급의 표시.
- 결과 경험치 Slider 값 0.6일 때 전체 Track 366 중 Fill 폭 219.6.
- 복귀 경로 변경 후 승리창의 로비 버튼을 클릭하여 `SceneManager.sceneLoaded` 이벤트와 활성 씬 경로가 모두 `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`임을 확인했다. 확인 후 Play Mode를 종료하고 `UI_Battle_MutedPreview`로 복귀했다. 나머지 결과창 3곳도 저장된 목적지 경로가 동일한지 확인했다.

추가 확인이 필요한 항목:

- 다른 해상도·화면비에서 잘림과 준비 카드 본문 가독성.
- 실제 전투 기록·경험치 지급·증강 효과 적용: 이번 범위에서 연결하지 않았으므로 미검증.

Inspector에서는 세 팝업의 `UIPopupPanel`, 첫 선택 버튼, View의 TMP·Image·Slider, 증강 카탈로그, 결과창 Navigator 참조를 확인한다. 추가 LayerMask / Tag / Collider / Rigidbody / Animator / Input Action 설정은 필요 없다.

## 변경 파일과 Git 검토

- Runtime: `UIBattleResultView.cs`, `UIAugmentVisualCatalogSO.cs`, `UIAugmentCardView.cs`, `UIAugmentSelectionView.cs` — 표시와 입력 책임 분리.
- Editor: `BattleOverlayPrefabBuilder.cs`, `BattleOverlayCardPlacement.cs` — 지정 프리뷰 씬만 최초 구성하고 Undo 지원.
- `Overlays_v1/Prefabs/`, `UIAugmentVisualCatalog.asset`, `Fonts/` — 재사용·편집 가능한 UI 자산.
- `UI_Battle_MutedPreview.unity` — 팝업 프리팹·기존 준비 카드 적용, 기존 대상 비활성 보존, 이벤트 대상 갱신.

Scene / Prefab / Meta 변경: Yes. ProjectSettings / Packages 변경: No. Unity가 생성한 Meta와 실제 에셋을 함께 관리한다. 충돌 위험이 큰 파일은 프리뷰 씬과 공통 결과 Base 프리팹이다. 원본 전투 씬·기존 카드 프리팹·증강 SO 수치가 바뀌지 않았는지 Git Diff에서 확인한다.

최종 `git diff --check`에서는 Unity가 직렬화한 씬의 빈 `m_Name` / `value` 줄 5곳에 trailing whitespace 경고가 있었다. Scene 텍스트를 직접 편집하지 않는 규칙에 따라 해당 직렬화 내용을 임의 수정하지 않았다.

이 작업은 커밋·push를 수행하지 않는다. 기능 완료 처리 시 팀의 PM / WBS 상태 갱신 여부를 별도로 확인한다.
