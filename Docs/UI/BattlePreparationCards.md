# 전투 준비 카드 Prefab

## 2026-10-07 카드 개편 적용 상태

`BattleCard_Unit`, `BattleCard_LandSlot`의 공용 Prefab은 `Heraldry_Cards_v2/Sprites` 셸을 사용한다. 상단 붉은 깃발은 좌우 한 개이며, 하단 금색 장식을 유지한다. v1 원본 이미지는 보존한다. 이후의 최초 제작 설명보다 이 항목을 우선한다.

- 유닛: 격자를 숨기고 성급별 초상화를 표시한다. 첫 칸은 실제 `GetFootprint(starLevel).Cells.Count`에 따른 **점유 칸수**, 둘째는 **보유 스킬 / 3성 기준** 설명이다. 일부 효과는 1·2성부터 작동하므로 3성 해금으로 표현하지 않는다. 하단 공격력·체력은 현재 성급 배율을 적용하며 방어율은 유지한다. 특성·시너지의 전투 보정은 포함하지 않는다.
- 확장: `UIExpansionCardVisualCatalogSO`가 `floor_domino`, `floor_line3`, `floor_corner3`, `floor_square4`, `floor_tee4`, `floor_l4`를 개별 Sprite로 연결한다. `Resources/UIExpansionCardVisualCatalog.asset`을 기본으로 로드한다. 보상 선택 전과 선택 후 카드 모두 같은 카탈로그를 사용한다. 모양별 PNG는 `Heraldry_Cards_v2/ExpansionArt`에 있다.
- 확장 삽화의 칸 구분: 승인한 `18_ExpansionArt_CellBoundaries.png` 시안에 맞춰 위 PNG 6종에 내부 갈색 경계와 가는 밝은 테두리를 추가했다. domino 2칸, line3·corner3 3칸, square4·tee4·l4 4칸이며 연결된 외곽·흙 질감·금색 더하기는 유지한다. PNG를 같은 경로에 교체해 기존 `.meta` 6개와 Sprite GUID/fileID 및 카탈로그 참조를 유지했다. Inspector 재연결은 필요하지 않다. 생성 프롬프트·출처는 `Tools/Art/Sources/BattleCardShells_v2/ExpansionCellArtGeneration.json`, Unity 실제 카드 6종 렌더는 `Tools/Art/Previews/BattleCardRedesign_ExpansionCells.png`다. 투명 알파·칸수·Importer·참조와 실제 카드 크기의 경계를 확인했고 Console 오류·경고는 없었다. 이 PNG 교체에서 코드·Scene·Prefab을 추가로 저장하지 않았으며 Play Mode는 재실행하지 않았다. 실제 손패 확대와 확장 보상 선택 전후 표시 크기는 플레이 중 확인한다.
- 확장 종류 아이콘: 좌상단 `TypeBadge/Frame/TypeIcon`은 중앙 삽화용 `floor_square4`를 재사용하지 않고 `Heraldry_Cards_v2/TypeIcons/Icon_Type_Expansion.png`를 사용한다. 첨부 레퍼런스의 작은 흙 타일·우상단 금색 더하기를 별도 투명 PNG로 제작했다. 저장된 유닛 카드와 동일한 배지 위치·Frame 120×120·아이콘 45×45 중앙 배치를 사용한다. 올리브색 Frame과 중앙 확장 삽화는 유지하며, 재적용 시에도 유닛 카드의 RectTransform을 기준으로 맞춘다.
- 일반 발판: 기존 격자를 사용한다. 미등록 확장이나 이미지 누락도 실제 격자로 대체하며, 다른 확장 그림으로 대신하지 않는다. `SetFootprintVisible(bool)`은 채움뿐 아니라 격자 루트 전체를 제어한다.
- 도감 상세: 공용 유닛 카드를 재사용하므로 첫 칸은 분류 한 줄, 둘째는 선택한 성급의 사거리·공속·효과를 표시한다.
- 폰트: 제목·탭·숫자는 **빛의 계승자 Bold (`HeirofLightBold.ttf`)**, 본문은 **NEXON Lv2 Gothic**을 유지한다. 유닛 12개와 지형 4개 문구는 `TextMeshProUGUI`로 전환했으며 `Heraldry_Cards_v2/Fonts/BattleCardTitle SDF.asset`, `BattleCardBody SDF.asset`을 사용한다. SDFAA·90pt 샘플링·9px padding·2048 atlas·Bilinear·Dynamic/Multi Atlas 설정이다. 현재 문구를 미리 포함하고 추가되는 한글은 같은 원본 폰트에서 확장한다. 본문은 27pt 기준이며 6종 스킬 설명은 최대 네 줄이다. 기물의 기존 Text 6개는 전환하지 않았다.
- 능력치 아이콘: 첨부한 하단 레퍼런스에 맞춘 면 채움 검·안쪽 면과 외곽 테두리가 있는 방패·면 채움 하트를 `Heraldry_Cards_v2/StatIcons/Icon_Stat_{Attack,Defense,Health}.png`로 연결한다. 공용 `Heraldry_Traits_v1` 원본은 보존한다. 아이콘 RectTransform은 기존 31×31, `Preserve Aspect`는 켜고 `Raycast Target`은 끈다. 생성 프롬프트와 출처는 `Tools/Art/Sources/BattleCardShells_v2/StatIconGeneration.json`, 실제 카드 렌더는 `Tools/Art/Previews/BattleCardRedesign_StatIcons.png`에 기록한다.
- 종류 배지: `TypeBadge/Frame`은 유닛에서 `Heraldry_Cards_v2/Badges/Badge_Type_Unit.png`의 어두운 진홍색 배경, 지형에서 `Badge_Type_LandSlot.png`의 어두운 올리브색 배경을 사용한다. 금색 원형 테두리와 상·하단 마름모 형태를 유지한 별도 PNG이며, `Image.color`는 흰색이다. 원본 `Heraldry_Cards_v1/Sprites/Badge_Type.png`는 보존한다. 두 카드의 Frame은 120×120으로 통일하고 각 카드의 전용 아이콘은 유지한다. 생성 기록은 `Tools/Art/Sources/BattleCardShells_v2/TypeBadgeGeneration.json`, 최초 색상 적용 렌더는 `Tools/Art/Previews/BattleCardRedesign_TypeBadges.png`다.

Unity Inspector 연결: 두 카드의 `Shell`, `Artwork`를 유지하고 기존 문구 필드에는 같은 GameObject의 TMP 컴포넌트를 연결한다. 공용 View의 문구 필드는 `Graphic`으로 기존 Text와 TMP를 함께 지원하며 `SetTitle`, `SetSkill` 등 공개 API는 그대로다. 확장 카드에는 `Artwork` Image와 `_artwork` 참조를 추가했다. `BattleCardHand.prefab`에는 성급별 초상화가 있는 `Heraldry_Codex_v1/UnitCatalog.asset`과 Expansion 카탈로그를 연결한다. `UI_Battle_MutedPreview` Presenter의 Grid 카탈로그는 `Assets/03.ScriptableObjects/RealGridCatalog.asset`이다. 드래그 입력과 배치 판정, Collider·Layer·Animator·Input System 설정은 변경하지 않는다.

Editor 메뉴는 `Tools/OZGL2/Battle/Redesign` 아래에 있다. `Import Art and Apply Saved Cards`는 카드 두 개와 손패의 카탈로그 참조, v2 아트·카탈로그를 저장한다. 저장형 배치는 Undo 대상이 아니며 Git으로 검토한다. `Apply Layout to Open Card (Undo)`는 열린 카드의 Prefab Mode에서 Ctrl+Z를 지원하며 저장은 사용자가 수행한다. `Validate Saved Cards`와 `Validate Card Reuse and Star Stats`는 에셋을 저장하지 않는다.

능력치 아이콘만 다시 적용할 때는 `Import and Apply Saved Stat Icons`를 사용한다. 세 PNG를 Sprite로 임포트하고 `BattleCard_Unit`의 하단 Image 세 개만 저장하며, 저장형 적용은 Undo 대상이 아니다. `Apply Stat Icons to Open Unit Card (Undo)`는 이미 임포트된 Sprite를 열린 유닛 Prefab에 연결하고 Ctrl+Z를 지원한다. 전체 레이아웃 적용에서도 같은 아이콘을 유지한다. 이번 아이콘 교체는 Unity 컴파일 오류 없음, Sprite/Importer 참조 검사, 실제 31×31 UI 렌더를 확인했다. Play Mode의 손패 확대·도감 상세 표시 크기는 최종 육안 확인 대상이다.

종류 배지 배경만 다시 적용할 때는 `Import and Apply Saved Type Badges`를 사용한다. 두 PNG를 임포트하고 두 카드의 `TypeBadge/Frame`만 저장하며 저장형 적용은 Undo 대상이 아니다. `Apply Type Badge to Open Card (Undo)`는 열린 유닛/지형 카드에 이미 임포트된 Sprite를 연결하고 Ctrl+Z를 지원한다. 전체 레이아웃 적용에서도 종류별 배경을 유지한다. 두 Prefab의 작업 전후 Diff에서 각각 Frame Sprite 참조 한 곳만 바뀌었으며, 컴파일·필수 참조·투명도·실제 카드 렌더를 확인했다. Scene은 저장하지 않았다. Play Mode 확인 항목은 손패·확장 보상 카드의 배지 색과 아이콘 대비다.

TMP 전환은 `BattleCardTmpMigrationBuilder.cs`가 폰트 생성과 Text 컴포넌트·View 참조 교체만 담당한다. `Migrate Saved Unit and Land Text to TMP`는 두 카드와 전용 폰트를 저장하며, 저장형 적용은 Undo 대상이 아니다. `Migrate Open Card Text to TMP (Undo)`는 열린 대상 카드의 컴포넌트와 참조에 Ctrl+Z를 지원하고 Prefab 저장은 사용자가 수행한다. 최초 폰트 에셋 생성 자체는 Undo 대상이 아니다. `Import Art and Apply Saved Cards` 및 열린 카드 레이아웃 재적용도 TMP를 유지한다. 폰트 재적용은 아직 없는 문자만 추가하며, 신규 글리프 생성 전에 FontEngine을 초기화한다. Play Mode 종료 후 재실행과 새 한글 추가도 확인했다.

TMP 검증: 유닛 12개/지형 4개 SDF, 기물 Text 6개와 기물 Prefab 파일 불변, 준비 Canvas·도감의 중첩 카드 참조, 6종 보유 스킬·마왕군 18개 성급 표본·인간군 6종 설명의 한글 글리프/잘림을 확인했다. Play Mode의 임시 인스턴스에서 TMP 메시·표시 API, 확장 +3칸 읽기, 발판 문구 보정, 3성 별 표시, 손패 호버/1.44배 확대/스크롤/슬롯 재사용을 검증했다. Console 오류·경고는 없었다. 실제 렌더는 `Tools/Art/Previews/BattleCardRedesign_TMP.png`이며 기물을 함께 표시해 전환 범위를 비교한다. 사용자 Scene을 저장하지 않았고 Collider·Tag·Layer·Animator·Input System 설정은 추가로 연결할 항목이 없다. 공용 카드 2종과 View의 참조 변경은 팀 작업 시 충돌 검토 대상이다.

검증: Unity 컴파일, 필수 참조, 6종 스킬 문자 잘림·글리프, 6종 확장 ID 연결, 같은 카드 슬롯의 확장↔발판 전환, 성급별 기본 스탯을 확인했다. Play Mode에서는 호버·확대·스크롤·슬롯 재사용 및 독립 메모리 세션의 확장 보상 선택 전후 Sprite 일치를 확인했다. 실제 전투에서 보상을 얻어 마우스로 회전·배치하는 전체 플레이 흐름은 별도 확인 대상이다. 미리보기 PNG는 `Tools/Art/Previews/BattleCardRedesign_*`에 저장한다.

충돌 검토 대상은 공용 카드 두 개, `BattleCardHand.prefab`, `UI_Battle_MutedPreview.unity`다. `.meta`와 Sprite Importer 설정은 Unity가 생성·저장했고, Scene/Prefab 텍스트를 직접 편집하지 않았다.

확장 종류 아이콘과 배지 배치를 다시 적용할 때는 `Tools/OZGL2/Battle/Redesign/Import and Apply Saved Expansion Type Icon`을 사용한다. Land 카드의 전용 아이콘을 연결하고 저장된 유닛 카드의 TypeBadge·Frame·TypeIcon Anchor/Pivot/크기/위치/회전/Scale을 복사한다. 현재 기준은 TypeBadge (63, -195), Frame 약 (-10.01, 7.50)/120×120, Frame 자식 TypeIcon 중앙 (0, 0)/45×45이다. 유닛 카드의 수동 배치는 전체 재적용 시에도 보존한다. 저장형 적용은 Undo 대상이 아니며 `Apply Expansion Type Icon to Open Land Card (Undo)`는 열린 Land 카드에서 Ctrl+Z를 지원한다. Inspector의 기존 View 참조는 유지한다. 생성 프롬프트·출처는 `Tools/Art/Sources/BattleCardShells_v2/ExpansionTypeIconGeneration.json`, 최초 전용 아이콘 렌더는 `Tools/Art/Previews/BattleCardRedesign_ExpansionTypeIcon.png`다. 통일 후 비교 렌더는 `Tools/Art/Previews/BattleCardRedesign_UnifiedBadges.png`다. Play Mode에서는 손패·확장 보상 카드의 배지 정렬과 작은 아이콘의 가독성을 확인한다.

배지 배치 통일 검증: 저장된 두 Prefab의 TypeBadge·Frame·TypeIcon RectTransform 값이 모두 동일하고, Land 카드의 올리브색 Frame·확장 아이콘 Sprite와 View 참조를 확인했다. 실제 Unity 비교 렌더와 컴파일을 확인했으며 Console 오류·경고는 없었다. 이번 배치 변경 대상은 Land 카드 Prefab과 Editor 재적용 도구이며, 유닛 Prefab·Scene·ProjectSettings·기존 .meta 파일은 추가로 변경하지 않았다. Play Mode는 재실행하지 않았다.

유닛 성급 숫자 스타일: `StarBadge/RankText`에만 TMP `FontStyles.Bold`를 켜고 `Fonts/BattleCardRank_WhiteOutline.mat`를 연결했다. 기존 검은 글자색·크기·중앙 배치를 유지하고 전용 Material의 흰색 Outline Width를 0.18로 설정했다. `Extra Padding`은 테두리 잘림을 방지하도록 켰다. 공용 `BattleCardTitle SDF.asset`과 그 기본 Material의 굵기/테두리는 수정하지 않았으며 다른 문구 11개는 기존 Material과 Normal 스타일을 유지한다.

`Tools/OZGL2/Battle/Redesign/Apply Saved Unit Rank Bold and White Outline`은 유닛 RankText와 전용 Material만 저장한다. 저장형 적용과 최초 Material 생성은 Undo 대상이 아니다. `Apply Unit Rank Style to Open Card (Undo)`는 열린 유닛 카드의 RankText와 이미 생성된 전용 Material에 Ctrl+Z를 지원한다. 전체 레이아웃 재적용도 이 스타일과 RankText의 수동 배치를 유지한다. Inspector 추가 연결은 없으며 View의 `_rankText`로 대상을 찾는다.

RankText 검증: 유닛 Prefab Diff는 RankText Material 참조·Font Style·Extra Padding 세 항목이다. 공용 제목 SDF 파일의 전후 SHA256이 동일하고 전용 Material의 atlas 연결 및 다른 Text와의 분리를 확인했다. 컴파일 오류와 게임 코드 예외는 없었으며 컴파일 중 MCP WebSocket 재연결 경고 1건이 기록됐다. 실제 Unity 비교 렌더 `Tools/Art/Previews/BattleCardRedesign_RankOutline.png`는 왼쪽 기존 숫자 1, 가운데 변경 후 숫자 1, 오른쪽 변경 후 숫자 3이다. 이번 작업에서 Scene/ProjectSettings를 저장하지 않았고 Play Mode는 재실행하지 않았다. 플레이 중에는 손패·도감에서 별 안 숫자의 가독성과 확대 시 테두리 잘림을 확인한다.

이번 변경 파일 묶음:

| 파일 | 변경 이유 |
| --- | --- |
| `BattleCard_Unit.prefab`, `BattleCard_LandSlot.prefab` | 새 셸·본문 배치·삽화 연결·기본 격자 숨김 |
| `BattleCardHand.prefab`, `UI_Battle_MutedPreview.unity` | 실제 성급별 초상화·Grid·확장 카탈로그 연결 |
| `BattleHandCardDisplayData.cs`, `UIBattleCardHandView.cs`, `UIBattlePreparationCardView.cs` | 격자 표시 여부 전달, 카드 재사용 시 이미지·격자 상태 갱신 |
| `UIGridStorageHandAdapter.cs`, `UIBattleCardHandPreviewPresenter.cs` | 실제 점유 칸수·현재 성급 기본 스탯·3성 기준 설명·개별 확장 이미지 공급 |
| `BattleCardSkillDescription.cs`, `UIExpansionCardVisualCatalogSO.cs` | 전투 수치와 분리한 설명 생성, 모양 ID와 Sprite의 표시 전용 연결 |
| `InGameExpansionCard.cs`, `InGameUiPolish.cs` | 선택 후 확장 그림 연결, 유닛 정보 덮어쓰기 방지 |
| `UIUnitCodexDetailView.cs` | 공용 카드의 좁은 첫 줄·넓은 둘째 칸에 도감 정보 재배치 |
| `BattleCardRedesignBuilder.cs`, `BattleCardRedesignValidation.cs` | 반복 적용 메뉴·Undo 편집 경로·참조/문구/재사용 검증 |
| `BattleCardTmpMigrationBuilder.cs`, `Heraldry_Cards_v2/Fonts` | 기존 두 폰트의 카드 전용 SDF 생성·컴포넌트/참조 변환·Undo 지원 |
| `BattlePreparationCardPrefabValidation.cs`, `BattleCardHandValidation.cs` | 여러 실제 카드 렌더, 현재 격자 설정·카탈로그에 맞춘 기존 검증 |
| `Heraldry_Cards_v2`, `Tools/Art/Sources/BattleCardShells_v2`, `Tools/Art/Previews/BattleCardRedesign_*` | 셸 2장·확장 6장·Unity 생성 메타/카탈로그, 생성 기록과 검증 이미지 |

최종 Console은 컴파일 오류가 없으며, 기존 `InGameCardSizing._rewardRestingYOffset` 미사용 필드 경고(CS0414) 1건은 유지된다. 기존 `AchievementCard.prefab` 변경은 이번 작업 대상이 아니다. ProjectSettings·Packages는 변경하지 않았다. Unity가 직렬화한 빈 `m_Text`/`m_Name` 줄의 끝 공백은 직접 수정하지 않는다.

## 범위와 파일

유닛·땅 슬롯·기물 카드를 각각 독립 Prefab으로 관리한다. 최초 제작은 프리팹만 준비했으나, 이후 `UI_Battle_MutedPreview > UI_BattleScreens/Canvas_Preparation/ChoiceCards/CardPrefabInstances`에 3종을 배치했다. 기존 `Card_1~3`은 비활성 보존한다. 실제 전투 데이터, 카드 선택, 구매, 비용 차감, 리롤, 배치 판정, 성급 합성은 연결하지 않는다. 현재 적용 구조와 실행 방법은 [BattleUITeamGuide.md](BattleUITeamGuide.md)를 확인한다.

| 종류 | 색상 | Prefab 경로 |
| --- | --- | --- |
| 유닛 | 저채도 적색 | `Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCard_Unit.prefab` |
| 땅 슬롯 | 고금색 | `Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCard_LandSlot.prefab` |
| 기물 | 저채도 청색 | `Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCard_Relic.prefab` |

관련 코드:

- Runtime: `Assets/00.Project/01.Scripts/UI/Battle/UIBattlePreparationCardView.cs`
- Editor 생성: `Assets/Editor/BattlePreparationCardPrefabBuilder.cs`
- Editor 검증·미리보기: `Assets/Editor/BattlePreparationCardPrefabValidation.cs`

세 카드의 루트는 `RectTransform`과 `OZGL2.UIFlow.UIBattlePreparationCardView`를 가진다. 자체 Canvas나 Button은 없으며, 이후 연결 작업에서 대상 Canvas 아래에 배치할 수 있다. 종류별 프레임 색은 카드 종류를 구분하는 표시이며 실제 스킬의 딜/버프/디버프 분류와 연결되지 않는다.

## Inspector 편집맵

Project 창에서 해당 Prefab을 열어 아래 자식의 컴포넌트를 편집하고 Prefab을 저장한다. 유닛·지형의 문구와 숫자는 `TextMeshProUGUI`, 기물은 `UnityEngine.UI.Text`이며 이미지에 합쳐져 있지 않다. Runtime View는 `OnEnable`, `OnValidate`, `Update`로 문구를 다시 쓰지 않으므로 Inspector에서 편집한 값을 자동으로 덮어쓰지 않는다. TMP의 Font Asset은 위 전용 SDF 두 개를 사용하고 Image 등 텍스트가 아닌 Graphic을 View 문구 필드에 연결하지 않는다.

| 편집 항목 | Prefab 내부 경로 | View 참조 필드 | 적용 카드 |
| --- | --- | --- | --- |
| 카드 이름 | `TitleText` | `_titleText` | 공통 |
| 성급 숫자 | `RankText` | `_rankText` | 공통 |
| 특성 제목 / 설명 | `TraitTitleText`, `TraitDescriptionText` | `_traitTitleText`, `_traitDescriptionText` | 유닛·기물 |
| 보유 스킬 제목 / 설명 | `SkillTitleText`, `SkillDescriptionText` | `_skillTitleText`, `_skillDescriptionText` | 유닛·기물 |
| 공격력 이름 / 값 | `Stats/Attack/LabelText`, `Stats/Attack/ValueText` | `_attackLabelText`, `_attackValueText` | 유닛 |
| 방어력 이름 / 값 | `Stats/Defense/LabelText`, `Stats/Defense/ValueText` | `_defenseLabelText`, `_defenseValueText` | 유닛 |
| 체력 이름 / 값 | `Stats/Health/LabelText`, `Stats/Health/ValueText` | `_healthLabelText`, `_healthValueText` | 유닛 |
| 영역 제목 / 설명 | `AreaTitleText`, `AreaDescriptionText` | `_areaTitleText`, `_areaDescriptionText` | 땅 슬롯 |

카드 종류에 없는 항목의 View 참조는 비워 둔다. 땅 슬롯에는 특성·스킬·능력치 참조가 없고, 기물에는 능력치 참조가 없다. 이러한 선택적 참조는 Runtime에서 null 안전하게 처리한다.

| 그림·장식 | Prefab 내부 경로 | 편집 방법 |
| --- | --- | --- |
| 카드 몸체·외곽선·구획 | `Shell` | Image의 Source Image 교체 |
| 종류 마름모의 검은 내부 | `TypeBadge/BlackDiamond` | Image Color 편집 |
| 종류 마름모 외곽선 | `TypeBadge/Frame` | Source Image / Image Color 편집 |
| 종류 아이콘 | `TypeBadge/TypeIcon` | Source Image 편집, View의 `_typeIcon` 연결 유지 |
| 성급 별 장식 | `StarBadge` | Source Image 편집; 숫자는 별도 `RankText` |
| 유닛·기물 삽화 | `Artwork` | Source Image 편집, View의 `_artwork` 연결 유지 |
| 능력치 아이콘 | `Stats/Attack/Icon`, `Stats/Defense/Icon`, `Stats/Health/Icon` | 각 Image에서 개별 교체 |

장식과 Text의 `Raycast Target`은 꺼 둔다. 원본 아트 파일을 덮어쓰지 않고 필요한 Image의 Sprite를 교체한다. 긴 이름은 `TitleText`의 Best Fit 범위와 줄바꿈·잘림을 확인한다. 현재 위치·크기·폰트 크기는 Prefab을 기준으로 하며 이 문서에서 배치 좌표를 고정하지 않는다.

## 5×5 점유 칸 미리보기

- `FootprintGrid`는 공통 5열×5행 격자다. 바탕 격자선과 점유 칸은 별도 오브젝트다. 개편된 유닛과 확장 카드에서는 기본적으로 숨긴다.
- `ColumnLine_*`, `RowLine_*`: 바탕 격자선 Image.
- `Cell_<x>_<y>`: 점유 칸의 옅은 채움 Image. 자식 `TopBorder`, `BottomBorder`, `LeftBorder`, `RightBorder`가 테두리를 표시한다.
- Inspector에서 `Cell_<x>_<y>` GameObject를 켜고 끄면 저장되는 점유 형태가 바뀐다. Image의 Enabled만 끄는 방식은 자식 테두리가 남을 수 있으므로 사용하지 않는다.
- View의 `_footprintGridSize`는 `(5, 5)`, `_footprintCells`는 25개 Image 참조다. 배열 순서는 `y * width + x`이며 좌상단을 시작으로 오른쪽, 다음 행 순서다. y는 위에서 아래로 증가한다.
- 격자 크기 값만 바꿔도 새 칸이 생성되지는 않는다. 실제 계층과 참조 배열을 함께 구성해야 한다.
- 격자는 UI 미리보기다. 실제 `GridManager`나 `FootprintDefinition`의 배치 가능 여부를 계산하지 않는다.

## 표시 API

외부 시스템이 값이 바뀌는 시점에 View 메서드를 호출한다. View는 전달된 값을 표시할 뿐 원본 전투 데이터나 저장 데이터를 보관하지 않는다.

| 메서드 | 동작 |
| --- | --- |
| `SetTitle(string)` | 카드 이름 교체 |
| `SetRank(int)` | 입력 정수를 그대로 성급 Text에 표시; 성급 제한이나 능력치 계산 없음 |
| `SetTrait(string title, string description)` | 특성 제목·설명 교체 |
| `SetSkill(string title, string description)` | 보유 스킬 제목·설명 교체 |
| `SetStats(string attack, string defense, string health)` | 공격·방어·체력 값 문구 교체; 단위 변환이나 수치 해석 없음 |
| `SetAreaDescription(string title, string description)` | 영역 제목·설명 교체 |
| `SetArtwork(Sprite)` | 삽화 교체 |
| `SetTypeIcon(Sprite)` | 종류 아이콘 교체 |
| `SetFootprint(Vector2Int[] cells)` | 기존 점유 표시를 모두 해제한 뒤 유효한 칸만 활성화 |
| `SetFootprintVisible(bool)` | 격자 루트 전체 표시/숨김 |

문자열에 null을 전달하면 빈 문구가 된다. Sprite에 null을 전달하면 해당 Image가 꺼지고, 유효 Sprite를 전달하면 다시 켜진다. `SetFootprint`는 null 또는 빈 배열이면 점유 표시를 비우며, 중복·범위 밖 좌표와 누락된 셀 참조는 무시한다. 전달받은 좌표 배열을 수정하거나 저장하지 않는다.

예를 들어 `view.SetStats("123", "45%", "678")`는 세 문자열을 그대로 표시한다. 공격력·방어력·체력의 제목은 별도 Text이므로 Inspector에서 편집한다. 실제 전투의 방어율을 카드 숫자로 연결할 때도 단위와 표기 규칙은 호출하는 시스템에서 결정한다.

## 예시 내용과 데이터 연결 범위

| 카드 | 예시 내용 |
| --- | --- |
| 유닛 — 그림자 검사 | 특성: `3성 달성 시 / 30초마다 마법검 발사`, 보유 스킬: `스킬 없음`, 공격력·방어력·체력: 각각 `1000` |
| 땅 슬롯 — 가시 지대 | `추가 배치 영역`, `배치 가능 영역 +4칸` |
| 기물 — 마력 증폭기 | 특성: `3성 달성 시 / 재사용 시간 감소`, 보유 스킬: `주변 마법 피해 +15%` |

표의 `/`는 예시 설명의 줄바꿈을 나타낸다. 성급 숫자, 점유 형태, 능력치, 특성 조건과 효과 문구는 시각 검토용 샘플이다. 이 문구를 추가해도 마법검 발사, 재사용 시간 감소, 피해 증가, 배치 영역 확장이 실행되지 않는다. 실제 `UnitStatData`, 스킬 SO, 저장 데이터의 수치를 변경하지 않는다.

## 아트와 폰트 의존성

신규 아트 폴더: `Assets/06.UI/BattleMutedPreview/Cards_v1/Sprites/`

- 몸체 3종: `CardShell_Unit.png`, `CardShell_LandSlot.png`, `CardShell_Relic.png`
- 별: `Badge_Star.png`
- 종류 아이콘: `Icon_Type_Unit_Diamond_v2.png`, `Icon_Type_Land_Diamond_v2.png`, `Icon_Type_Relic_Diamond_v2.png`
- 능력치 공격 아이콘: `Icon_Sword.png` (새 종류 아이콘과 별도 유지)
- 삽화: `Artwork_ShadowSwordsman.png`, `Artwork_ManaAmplifier.png`

기존 아트 재사용:

- 마름모: `Assets/06.UI/BattleMutedPreview/Reference_v2/Frame_DiamondSynergy.png`
- 방패: `Assets/06.UI/BattleMutedPreview/Sprites/Icon_BoneShield.png`
- 심장: `Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/Sprites/Icons/Legion/Icon_Legion_UndyingFlesh.png`

좌상단 종류 아이콘은 256×256 중심 Pivot Sprite를 공통 112×112 Rect에 표시한다. `TypeBadge` 중심에 맞추고, 원본 모양을 왜곡하지 않은 채 불투명 픽셀의 마름모 경계 반경을 112px 이하로 정렬했다. 프레임 사선과 약 6 UI 단위 이상의 간격을 확보한다. 기존 검·산·마력 아이콘 원본은 보존한다. 생성 기록은 `Tools/Art/Sources/BattleCardDiamondIcons_v2/GENERATION.md`, 후처리는 `PrepareBattleCardArt.ps1 -DiamondIcons`를 사용한다.

Text는 `Assets/98.ExternalAssets/00.LocalStaging/01.Font/NotoSansCJKkr-Regular.otf`를 사용한다. 이 폰트는 로컬 공유 경로에 있으므로 새 clone만으로 참조가 완성되지 않을 수 있다. [폰트 공유 절차](../FontAssetSharing.md)에 따라 원본 폰트와 기존 `.meta`를 동일 경로·GUID로 복원한다. 이번 카드 때문에 TMP SDF 폰트를 새로 생성할 필요는 없다.

## Editor 메뉴

### Create Card Prefabs

메뉴: `Tools > OZGL2 > Battle > Create Card Prefabs`

- 컴파일·임포트가 끝난 Edit Mode에서 실행한다.
- 필수 카드 PNG 10개, 기존 재사용 Sprite, 폰트가 필요하다.
- 대상 Prefab이 하나라도 이미 있으면 중단한다. 기존 Prefab을 수정하거나 덮어쓰는 재적용 도구가 아니다. 완성된 카드 편집은 Prefab Mode에서 수행한다.
- 임시 PreviewScene에 계층을 구성하여 독립 Prefab 3개를 저장한다. 실제 Scene을 열거나 저장하지 않고, 종료 시 실제 Scene 구성·루트·dirty 상태를 검사한다.
- 신규 카드 Sprite만 Single / 중심 Pivot / Full Rect / Point / 무압축 / Mipmap Off / Clamp로 임포트한다. 기존 재사용 아트의 임포터는 수정하지 않는다.
- Prefab·폴더 생성과 Sprite 임포트 설정은 Undo 대상이 아니다. `.meta`는 Unity가 생성·관리하며 직접 편집하지 않는다.
- 생성 중 예외가 나면 이번 호출에서 저장을 시도한 신규 Prefab을 정리하고 카드 Sprite의 기존 임포터 설정 복구를 시도한다. Console 오류가 있으면 생성 결과와 복구 여부를 확인한다.

### Validate Card Prefabs

메뉴: `Tools > OZGL2 > Battle > Validate Card Prefabs`

`LoadPrefabContents`로 격리한 임시 내용을 검사하고 저장 없이 해제한다. 검사 대상은 Missing Script, Text 폰트, 장식 Raycast, 종류별 필수 참조, 30개 셀 참조, 제목·성급 교체, 점유 칸 중복·범위·초기화, null 문구·Sprite 처리다. 특성·스킬·능력치·영역 API 호출도 수행하지만 각 문구의 실제 화면 가독성은 별도 확인이 필요하다.

### Render Card Prefab Preview

메뉴: `Tools > OZGL2 > Battle > Render Card Prefab Preview`

임시 PreviewScene의 복제본으로 3종 카드를 렌더하고 `Tools/Art/Previews/BattlePreparationCards_v1.png`에 저장한다. 같은 경로의 미리보기 PNG는 다시 생성된다. 실제 Scene과 원본 Prefab을 저장하지 않으며, 임시 UI 메시·머티리얼·렌더 자원은 종료 시 정리한다. 이 결과는 독립 카드의 시각 검토용이며 실제 전투 화면이나 Play Mode 검증 결과가 아니다.

## 검증 및 후속 확인

- 2026-09-26: 실사용하지 않는 상단 `CategoryText`를 카드 3종에서 제거했다. `_categoryText`와 검증 코드에서만 사용하던 `SetCategory`도 제거했으며 생성 도구에서 다시 만들지 않는다. 카드 이름·분류 아이콘·성급·설명·능력치와 나머지 배치는 유지한다.
- 제거 후 Editor 검사: 카드 3종의 참조/API/null/점유 칸 검사 통과, 연결된 Scene 카드 3개에서도 해당 문구 없음. 다른 자식 컴포넌트와 루트 배치 불변 확인. 컴파일·Console 오류/경고 0, Scene 파일 SHA256 불변, Play Mode는 이번 단순 표시 제거에서는 미실행. 원본 Prefab은 Git 이력과 로컬 `Temp/BattleCardCategoryRemoval` 백업으로 복구할 수 있다.

- 2026-09-23 Editor 확인: 스크립트 컴파일 후 Console Error/Warning 0건, 독립 Prefab 3종의 참조·표시 API·null·점유 칸 검사 통과. 샘플 Text 높이와 독립 렌더를 확인했다.
- 작업 전후 `UI_Battle_MutedPreview` 씬 파일 SHA-256 동일, 기존 UI 213개 오브젝트의 RectTransform/Image/Button/활성 상태 동일. 임시 생성 객체는 제거했고 실제 Scene은 저장하지 않았다.
- Play Mode: **미실행**. 실제 카드 선택·전투·구매·배치 기능은 이번 작업의 검증 대상이 아니다.
- Inspector: 해당 카드에 필요한 Text/Image/셀 참조, 공유 폰트, 긴 문구의 잘림, 이미지 비율과 알파, 점유 칸 활성 상태를 확인한다.
- Console: 자동 검증 메뉴 실행 결과와 컴파일 오류, `NullReferenceException`, `MissingReferenceException` 유무를 확인한다.
- 추가 LayerMask / Tag / Collider / Rigidbody / IsTrigger / Animator Parameter / Animation Event / Input Action 연결은 필요하지 않다.
- Scene에 연결하는 후속 작업에서는 실제 표시 크기·Canvas 배율·다른 화면비의 가독성과 기존 버튼 입력을 별도로 검사한다. 현재 `ChoiceCards` 교체는 수행하지 않는다.
- Git Diff: 신규 Runtime·Editor 코드, 카드 Prefab 3종, 신규 아트와 Unity 생성 `.meta`, 이 문서 및 미리보기 결과를 검토한다. Scene·ProjectSettings·Packages와 기존 재사용 아트에 의도치 않은 변경이 없어야 한다. 작업 전부터 있던 변경과 구분한다.

## 현재 성급의 보유 스킬 표시 — 2026-10-07

손패·웨이브 보상·카드 미리보기는 `BattleCardSkillDescription.Build(stats, starLevel)`에 현재 카드 성급을 전달한다. 성급별 Prefab이 같은 기초 `UnitStatData`를 공유하므로 SO의 기본 `starLevel`만으로 카드 성급을 판단하지 않는다. 모든 카드에 붙던 `3성 기준` 문구를 제거했다.

사용자 선택에 따라 **성급 상승으로 얻는 특수 효과만** 보유 스킬에 표시한다. 1성은 모두 `스킬 없음`이며, 도발·범위 공격·대상 선택·기본 회복 같은 기본 전투 방식은 이 칸에서 제외한다.

| 직업 | 1성 | 2성 | 3성 |
| --- | --- | --- | --- |
| 전사·방패병·마법사 | 스킬 없음 | 스킬 없음 | 스킬 없음 |
| 궁수 | 스킬 없음 | 잃은 체력에 따른 최대 50% 추가 피해 | 동일 |
| 도적 | 스킬 없음 | 매 3번째 공격마다 1초 기절 | 동일 |
| 힐러 | 스킬 없음 | 최대 2명 동시 회복 | 최대 3명 동시 회복 |

- 효과나 스탯 데이터가 없으면 `스킬 없음`을 반환한다. 기존 `Build(stats)` API는 유지한다.
- 저장된 `BattleCard_Unit.prefab`의 1성 샘플도 설명 텍스트만 동기화했다. 폰트·공유 Material·RankText·배치는 유지했다.
- Editor 검증: 6직업 × 3성급의 문구·SDF 텍스트 잘림 검사 통과.
- Play Mode 검증: 실제 Adapter를 사용한 임시 손패 복제본에서 18개 보관함 표시 데이터와 TMP 문구, 같은 슬롯의 성급 변경·재사용, 6종 1성 보상 카드 문구를 확인했다. 원본 스탯 데이터와 실제 게임 보관함은 변경하지 않았다.
- 컴파일 완료, 최종 Console 오류·경고 및 Missing Script 0개. 신규 Inspector 연결은 없다.
- 지형·기물 카드, Scene, 전투 코드와 밸런스 데이터는 이번 수정 대상이 아니다. 이전 웨이브 패널 변경도 그대로 보존했다.
