# 전투 준비 카드 Prefab

## 범위와 파일

유닛·땅 슬롯·기물 카드를 각각 독립 Prefab으로 관리한다. 이번 산출물은 표시용이며 `UI_Battle_MutedPreview`의 기존 `ChoiceCards`를 교체하거나 Scene에 배치하지 않는다. 실제 전투 데이터, 카드 선택, 구매, 비용 차감, 리롤, 배치 판정, 성급 합성은 연결하지 않는다.

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

Project 창에서 해당 Prefab을 열어 아래 자식의 컴포넌트를 편집하고 Prefab을 저장한다. 모든 문구와 숫자는 `UnityEngine.UI.Text`이며 이미지에 합쳐져 있지 않다. Runtime View는 `OnEnable`, `OnValidate`, `Update`로 문구를 다시 쓰지 않으므로 Inspector에서 편집한 값을 자동으로 덮어쓰지 않는다.

| 편집 항목 | Prefab 내부 경로 | View 참조 필드 | 적용 카드 |
| --- | --- | --- | --- |
| 종류 문구 | `CategoryText` | `_categoryText` | 공통 |
| 카드 이름 | `TitleText` | `_titleText` | 공통 |
| 성급 숫자 | `RankText` | `_rankText` | 공통 |
| 특성 제목 / 설명 | `TraitTitleText`, `TraitDescriptionText` | `_traitTitleText`, `_traitDescriptionText` | 유닛·기물 |
| 보유 스킬 제목 / 설명 | `SkillTitleText`, `SkillDescriptionText` | `_skillTitleText`, `_skillDescriptionText` | 유닛·기물 |
| 공격력 이름 / 값 | `Stats/Attack/LabelText`, `Stats/Attack/ValueText` | `_attackLabelText`, `_attackValueText` | 유닛 |
| 방어력 이름 / 값 | `Stats/Defense/LabelText`, `Stats/Defense/ValueText` | `_defenseLabelText`, `_defenseValueText` | 유닛 |
| 체력 이름 / 값 | `Stats/Health/LabelText`, `Stats/Health/ValueText` | `_healthLabelText`, `_healthValueText` | 유닛 |
| 영역 제목 / 설명 | `AreaTitleText`, `AreaDescriptionText` | `_areaTitleText`, `_areaDescriptionText` | 땅 슬롯 |

카드 종류에 없는 항목의 View 참조는 비워 둔다. 예를 들어 땅 슬롯에는 특성·스킬·능력치·삽화 참조가 없고, 기물에는 능력치 참조가 없다. 이러한 선택적 참조는 Runtime에서 null 안전하게 처리한다.

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

## 6×5 점유 칸 미리보기

- `FootprintGrid`는 공통 6열×5행 격자다. 바탕 격자선과 점유 칸은 별도 오브젝트다.
- `ColumnLine_*`, `RowLine_*`: 바탕 격자선 Image.
- `Cell_<x>_<y>`: 점유 칸의 옅은 채움 Image. 자식 `TopBorder`, `BottomBorder`, `LeftBorder`, `RightBorder`가 테두리를 표시한다.
- Inspector에서 `Cell_<x>_<y>` GameObject를 켜고 끄면 저장되는 점유 형태가 바뀐다. Image의 Enabled만 끄는 방식은 자식 테두리가 남을 수 있으므로 사용하지 않는다.
- View의 `_footprintGridSize`는 `(6, 5)`, `_footprintCells`는 30개 Image 참조다. 배열 순서는 `y * width + x`이며 좌상단을 시작으로 오른쪽, 다음 행 순서다. y는 위에서 아래로 증가한다.
- 격자 크기 값만 바꿔도 새 칸이 생성되지는 않는다. 실제 계층과 참조 배열을 함께 구성해야 한다.
- 격자는 UI 미리보기다. 실제 `GridManager`나 `FootprintDefinition`의 배치 가능 여부를 계산하지 않는다.

## 표시 API

외부 시스템이 값이 바뀌는 시점에 View 메서드를 호출한다. View는 전달된 값을 표시할 뿐 원본 전투 데이터나 저장 데이터를 보관하지 않는다.

| 메서드 | 동작 |
| --- | --- |
| `SetCategory(string)` | 종류 문구 교체 |
| `SetTitle(string)` | 카드 이름 교체 |
| `SetRank(int)` | 입력 정수를 그대로 성급 Text에 표시; 성급 제한이나 능력치 계산 없음 |
| `SetTrait(string title, string description)` | 특성 제목·설명 교체 |
| `SetSkill(string title, string description)` | 보유 스킬 제목·설명 교체 |
| `SetStats(string attack, string defense, string health)` | 공격·방어·체력 값 문구 교체; 단위 변환이나 수치 해석 없음 |
| `SetAreaDescription(string title, string description)` | 영역 제목·설명 교체 |
| `SetArtwork(Sprite)` | 삽화 교체 |
| `SetTypeIcon(Sprite)` | 종류 아이콘 교체 |
| `SetFootprint(Vector2Int[] cells)` | 기존 점유 표시를 모두 해제한 뒤 유효한 칸만 활성화 |

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

- 2026-09-23 Editor 확인: 스크립트 컴파일 후 Console Error/Warning 0건, 독립 Prefab 3종의 참조·표시 API·null·점유 칸 검사 통과. 샘플 Text 높이와 독립 렌더를 확인했다.
- 작업 전후 `UI_Battle_MutedPreview` 씬 파일 SHA-256 동일, 기존 UI 213개 오브젝트의 RectTransform/Image/Button/활성 상태 동일. 임시 생성 객체는 제거했고 실제 Scene은 저장하지 않았다.
- Play Mode: **미실행**. 실제 카드 선택·전투·구매·배치 기능은 이번 작업의 검증 대상이 아니다.
- Inspector: 해당 카드에 필요한 Text/Image/셀 참조, 공유 폰트, 긴 문구의 잘림, 이미지 비율과 알파, 점유 칸 활성 상태를 확인한다.
- Console: 자동 검증 메뉴 실행 결과와 컴파일 오류, `NullReferenceException`, `MissingReferenceException` 유무를 확인한다.
- 추가 LayerMask / Tag / Collider / Rigidbody / IsTrigger / Animator Parameter / Animation Event / Input Action 연결은 필요하지 않다.
- Scene에 연결하는 후속 작업에서는 실제 표시 크기·Canvas 배율·다른 화면비의 가독성과 기존 버튼 입력을 별도로 검사한다. 현재 `ChoiceCards` 교체는 수행하지 않는다.
- Git Diff: 신규 Runtime·Editor 코드, 카드 Prefab 3종, 신규 아트와 Unity 생성 `.meta`, 이 문서 및 미리보기 결과를 검토한다. Scene·ProjectSettings·Packages와 기존 재사용 아트에 의도치 않은 변경이 없어야 한다. 작업 전부터 있던 변경과 구분한다.
