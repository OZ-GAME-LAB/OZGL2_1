# Battle Muted Preview UI

## 전투 스킬 하단 HUD (2026-09-26)

`Canvas_Combat`의 01 Noble Diamond 공통 슬롯·분류 프레임·쿨다운 링·벨벳 배경을 적용했다. 편집 위치, 표시 API, 미리보기/실제 전투 연동 범위, 검증 기록은 [CombatSkillHud.md](CombatSkillHud.md)를 따른다. 준비 화면의 기존 하단 패널은 변경하지 않았다.

## 현재 하단 패널: 단일 이미지·45도 정렬 (2026-09-23)

- `UI_BattleScreens/Canvas_Preparation/BottomNobleBackground` 자체에 Image 1개를 배치한다. 이전 Body/Frame 자식 2개는 Undo를 지원하는 Editor 도구로 제거했으며, 이전 PNG 원본은 보존했다.
- 원인: 이전 이미지 처리에서 X를 구간별로 변환하고 Y를 독립 압축하여 경사각이 변했다. 가로 stretch 또한 비율 왜곡을 허용했다. 작업 시작 시 사용자의 미저장 패널 폭은 1967.33이었으며 승인 후 그 상태를 먼저 저장해 보존했다.
- 새 통 이미지: `Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Unified.png` (1920×432). 새 원본의 체크 배경 제거 후 실제 경사선의 교점을 2D 좌표 정렬했다.
- Cost/리롤/전투 시작의 봉우리 중심 X=159/375/1757. 6개 사선은 45도, 중앙 연결부는 수평이다. 최종 금색 경계 픽셀 표본은 기준선 대비 오차 1px 이내였다.
- RectTransform: Anchor Min/Max=(0,0), Pivot=(0,0), Position=(0,0), Size=1920×432, Scale=(1,1,1). 가로 stretch를 제거하여 마름모와 같은 좌표·배율을 사용한다.
- Image: `Panel_Unified`, Simple, Preserve Aspect On, Raycast Target Off, Color=(0.55,0.55,0.55,1). 전체 벨벳과 어두운 톤 유지. 별도 Canvas sorting order -1 유지.
- 밝기·색상은 이제 `BottomNobleBackground > Image > Color` 한 곳에서 조절한다. 아래 Body/Frame 편집 안내는 이전 버전 기록이다.

### 변경 파일과 도구

- `Assets/Editor/BattleNobleBottomPanelBuilder.cs`: `ApplyUnifiedPanel()`, 메뉴 `Tools > OZGL2 > Battle > Apply Unified Noble Panel`. 지정 씬/Edit Mode/미저장/예상 외 자식 검사, 계층 Undo, 저장 실패 복구 지원.
- 기존 Apply Noble Bottom Panel은 통 이미지가 있으면 통합 경로를 사용한다. Apply Muted Tone Fix도 통합 Image를 지원한다.
- `Tools/Art/PrepareBattleUnifiedPanel.ps1`: 외부 배경 제거, 원본 RGBA 최근접 샘플링으로 상단 경사·하단선 좌표 정렬. 아트 재도색이나 그림 생성은 하지 않는다.
- `Tools/Art/Sources/BattleNobleUnifiedPanel/`: 내장 image_gen 원본 및 `GENERATION.md`의 최종 프롬프트·재현 인자.
- `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`: 배경 루트 정렬, 단일 Image, 이전 자식 제거 저장. 기존 사용자 씬 편집은 사전 저장 후 유지했다.

### 검증 결과

- 적용 전 215개 → 적용 후 213개. 제거된 Body/Frame 2개와 패널 루트의 RectTransform/Image 외에는 활성 상태·배치·Button 직렬화 값 변경 없음.
- 1920×1080 실행 화면 및 패널 Image 1개/자식 0개/Sprite 연결/균등 배율 확인. `Tools/Art/Previews/UI_Battle_MutedPreview_UnifiedPanel.png`.
- Cost/리롤/전투 시작 중심점 raycast 모두 기존 Button과 일치. 전투 시작 후 패널·준비 화면 숨김과 Combat 전환 확인.
- 컴파일 완료, Missing Script 0, Console Error/Warning 0. 각 항목 1회 확인 후 Edit Mode 복귀.
- 원본 UI_Battle SHA256 유지. 이 변경에서 Prefab·ProjectSettings·Packages·Runtime 변경 없음. Preview Scene, Sprite와 Unity 생성 Meta, Editor 도구/문서/아트 처리 파일만 변경했다.
- 다른 화면비에서 전체 UI를 재배치하는 대응은 이번 범위에 포함하지 않았으며 미확인이다. 커밋/push하지 않았다.

## 하단 톤·리롤 색상 보정 (2026-09-23)

- 벨벳 전체 채움 구조는 유지한다. 패널 Body/Frame이 흰색 tint로 표시되어 하단이 밝게 느껴지는 부분만 조정했다. 상단 HUD나 전체 배경 밝기는 바꾸지 않았다.
- `Canvas_Preparation/BottomNobleBackground/Body` Image Color: RGB 0.50 / Alpha 1.
- `Canvas_Preparation/BottomNobleBackground/Frame` Image Color: RGB 0.62 / Alpha 1.
- `Reroll_Placeholder/Frame`, `Icon`은 기존 크림색 아트 대신 적색·흰색 얇은 프레임과 흰색 화살표로 교체했다. 기존 원본은 보존한다.
- `Currency/Body`, `Reroll_Placeholder/Body`, `StartCombatButton/Body`는 RGB (0.01, 0.01, 0.012), Alpha 1로 변경해 배경색 비침을 제거했다.
- 위 경로는 모두 `UI_BattleScreens/Canvas_Preparation` 아래이며 Inspector의 Image Color에서 조절할 수 있다.

### 파일·도구

- `Assets/06.UI/BattleMutedPreview/ToneFix/Frame_Reroll.png`, `Icon_RerollWhite.png`: 새 분리 Sprite. 중심 pivot, Point/무압축/원본 alpha.
- `Assets/Editor/BattleNobleBottomPanelBuilder.cs`: `ApplyToneFix()`, 메뉴 `Tools > OZGL2 > Battle > Apply Muted Tone Fix`. 지정 Image 7개만 Undo 기록하고 씬 저장. 위치·크기·버튼 연결·raycast 설정은 유지한다.
- 기존 Apply Noble Bottom Panel에도 같은 패널 tint를 반영하여 재적용 시 밝은 톤으로 돌아가지 않게 했다.
- `Tools/Art/PrepareBattleToneFix.ps1`: 생성 배경/잔여 픽셀 정리·crop·크기와 중심 정렬만 수행.
- `Tools/Art/Sources/BattleToneFix/GENERATION.md`: 내장 image_gen 최종 프롬프트, 생성 원본과 리소스 경로 기록.

### 검증

- 적용 전후 215개 오브젝트 비교: 지정 Image 7개만 변경. 모든 RectTransform·Button·활성 상태 동일, 저장 후 dirty false.
- 1920×1080 실행 화면 1회 확인. `Tools/Art/Previews/UI_Battle_MutedPreview_ToneFix.png`.
- Cost·리롤·전투 시작·마지막 시너지의 중심점 raycast 모두 기존 Button과 일치. 전투 시작 이벤트 후 준비/패널 숨김과 Combat 표시 확인.
- Missing Script 0, Console Error/Warning 0. 검증 후 Edit Mode 복귀.
- 실제 리롤 추첨/경제 기능은 기존 placeholder 유지. 다른 화면비와 실제 마우스 경계 클릭은 미확인.
- 원본 `UI_Battle.unity` SHA256 유지. 변경 대상은 Preview Scene과 신규 Sprite/Unity 생성 Meta, Editor 도구/문서/아트 처리 파일이다. Prefab·ProjectSettings·Packages 변경 없음.
- 기존 사용자의 Editor 폴더 이동 보존. 커밋/push하지 않음.

## 추가 적용: 귀족풍 벨벳 하단 패널 (2026-09-23)

- 사용자 승인 후 `UI_Battle_MutedPreview`의 하단 배경을 추가했다. 기존 카드·버튼·Text·상단 HUD·전투 기능은 유지한다.
- 최종 아트는 검은 내부 띠 없이 **전체 와인색 벨벳 문양**과 고금색 테두리/장식으로 구성한다. 왼쪽 큰 봉우리·리롤 작은 봉우리·낮은 중앙 연결부·오른쪽 큰 봉우리를 유지했다.
- 편집 위치: `UI_BattleScreens/Canvas_Preparation/BottomNobleBackground`.
  - `Body`: 벨벳 몸체 Image.
  - `Frame`: 고금색 외곽선·장식 Image.
  - 두 레이어는 같은 1920×432 캔버스와 중심 Sprite pivot으로 정렬하며, 하단 고정/가로 stretch다. 모든 장식 Image의 Raycast Target은 꺼져 있다.
- 패널은 `Canvas_Preparation`의 첫 형제이며, 준비 화면이 닫히면 함께 숨겨진다.
- 최초 화면 확인에서 오른쪽 꼭짓점이 시너지 위에 그려지는 문제를 발견하여 **장식 배경 두 개의 렌더 순서만** 수정했다.
  - 기존 `Canvas_GetReady/Background`: 별도 Canvas, sorting order -2.
  - 새 `BottomNobleBackground`: 별도 Canvas, sorting order -1.
  - 기존 HUD 0, 준비/전투 UI 1, 팝업 100은 유지. 새 GraphicRaycaster는 추가하지 않는다.

### 파일과 수정 이유

- `Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Body.png`, `Panel_Frame.png`: 본체와 테두리를 별도로 교체하기 위한 투명 Sprite.
- `Assets/Editor/BattleNobleBottomPanelBuilder.cs`: 기존 V2 전체 재배치 없이 하단 패널만 추가/재적용하는 Editor 도구.
- `Tools/Art/PrepareBattleNoblePanel.ps1`: 승인된 외부 배경 제거·레이어 분할·크기/중심 정렬. 생성 픽셀의 재색칠이나 신규 도형 그리기는 하지 않는다.
- `Tools/Art/Sources/BattleNobleBottomPanel/`: 내장 image_gen 생성 원본 2개와 최종 프롬프트 기록. 중간 검은 띠 시안도 원본으로 보존한다.
- `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`: 위 두 Image와 배경 렌더 순서를 저장한 유일한 씬 변경 대상.

### Editor / Inspector

- 메뉴: `Tools > OZGL2 > Battle > Apply Noble Bottom Panel`.
- 지정 씬의 Edit Mode에서만 실행한다. 미저장 씬·필수 아트/부모 누락·예상하지 못한 패널 자식/컴포넌트는 적용 전 중단한다.
- 씬 오브젝트 생성·수정과 표시 순서는 Undo 지원. 텍스처 import 설정은 씬 Undo와 별개이며 실패 시 이전 설정 복구를 시도한다.
- 재실행하면 해당 패널의 위치/크기/색상과 배경 정렬을 도구 기준으로 복원하므로 수동 디자인 편집 후에는 의도할 때만 실행한다.
- Sprite는 Single/FullRect, 중심 pivot, Point 필터, 무압축, Mipmap 없음, Clamp, 원본 알파 사용이다. `.meta`는 Unity가 생성/저장한다.
- 추가 Runtime 코드, LayerMask/Tag, Collider/Rigidbody, Animator/Input Action 설정은 필요 없다.

### 검증 결과

- 아트: Body와 Frame 합성 시 원본 정렬 픽셀 기준 overlap 0, missing 0, pixel mismatch 0. 합성 투명 픽셀 375,668개.
- 기존 212개 오브젝트의 활성 상태·RectTransform·Button 직렬화 값을 비교했으며, 부모의 신규 자식 목록을 제외하고 변경 없음. 새 패널 루트/Body/Frame 3개만 추가했다.
- Sprite 참조 두 개 및 중심 pivot 확인, Missing Script 0.
- 1920×1080 화면 확인 중 발견한 렌더 순서 문제만 보정 후 확인했다.
- Play Mode에서 Cost·리롤·전투 시작·마지막 시너지의 중심점 EventSystem Raycast 대상 일치, 새 배경의 입력 가림 없음.
- 전투 시작 PointerClick 이벤트 처리 성공, 준비 화면과 패널 숨김/전투 화면 표시 성공. 실제 리롤 추첨/비용 차감은 기존대로 구현하지 않았다.
- Console Error/Warning 0, 확인 후 Edit Mode로 종료. 다른 화면비는 미확인이다.
- 최종 화면: `Tools/Art/Previews/UI_Battle_MutedPreview_NobleVelvet-1.png`.
- Scene 및 Unity 생성 Meta 변경 있음. Prefab/ProjectSettings/Packages/원본 UI_Battle 변경 없음. 기존 Editor 폴더 이동은 보존했다. 커밋/push하지 않았다.

## 현재 적용본: Reference v2 (2026-09-23)

첫 배치의 프레임 형태·두께·시너지 폭·글꼴·경험치 바와 화면 비율이 사용자 레퍼런스와 달라 후속 승인을 받아 수정했다. 아래 v1 생성 도구/리소스는 보존하되, 현재 씬의 재정렬에는 **Apply Reference V2 Layout**을 사용한다.

- 새 아트: `Assets/06.UI/BattleMutedPreview/Reference_v2/`의 적 예고 프레임, 행동 마름모, 중성 시너지 마름모, 전체 석재 배경.
- `Experience_White.png`는 Unity Filled 표시를 위한 2×2 흰색 데이터 텍스처이며 8칸의 적색 Fill/눈금은 각각 별도 Image다.
- 기존 아이콘 원본은 유지하고 시너지 아이콘은 UI Outline으로 축소 시 선명도만 보강한다.
- 시너지 `3 > 5`는 첫 기준값을 고금색, 다음 기준값과 구분자를 회색으로 표시한다. 색상은 View Inspector에서 편집 가능하며 실제 발동 판정을 추가한 것은 아니다.
- 한글은 기존 NotoSansCJKkr-Regular + Bold, 상단 영문/숫자는 DOSGothic + Bold를 사용한다. 여전히 Unity Text이며 SDF를 생성하지 않았다.
- 카드·배치 영역의 **아트는 그대로**, 위치·크기만 후속 승인에 따라 레퍼런스 비율로 조정했다. U자 하단 배경·중앙 유닛·타일 아트는 계속 제외다.
- 원본의 파란 시너지/확률 보조 버튼은 삭제하지 않고 숨겼다. 오른쪽 시너지 4행의 팝업 연결은 유지한다.

| 영역 | 좌상단 X / Y / 폭 / 높이 (1920×1080 기준) |
| --- | --- |
| 상단 HUD | 53 / 32 / 1815 / 79 |
| 적 예고 | 46 / 122 / 635 / 183 |
| 배치 영역 | 286 / 324 / 1230 / 373 |
| 카드 그룹 | 516 / 723 / 890 / 319 |
| 시너지 | 1544 / 194부터 136 간격 / 334 / 124 |
| Cost / 전투 시작 | X 29 / 1627, Y 766, 260×260 |
| 리롤 | 306 / 838 / 138 / 138 |
| 리롤 가격 | 279 / 973 / 184 / 59 |

Editor 도구: `Assets/Editor/BattleMutedReferenceLayout.cs`

- 메뉴: `Tools > OZGL2 > Battle > Apply Reference V2 Layout`
- 현재 복제 씬의 Edit Mode에서만 실행하며, 미저장 씬이나 필수 리소스 누락 시 중단한다.
- 기존 V1 생성기와 분리한 이유는 원본 생성 과정은 보존하고, 승인된 참조 배치만 재적용하기 위해서다.
- 계층 편집 Undo 및 실패 시 복구 지원. 새 단색 데이터 PNG 생성·import는 Undo 밖이다.
- 기존 Button 이벤트와 View의 Text/Image 참조를 유지한다. 추가 UI 장식의 Raycast Target은 끈다.
- 재적용하면 표의 위치·크기·색상·폰트 기준값으로 복구되므로 수동 디자인 편집 후에는 의도할 때만 실행한다.
- 아트 후처리: `Tools/Art/PrepareBattleReferenceArt.ps1`, 생성 원본/프롬프트: `Tools/Art/Sources/BattleMutedReference_v2/GENERATION.md`.

기존의 `Create Muted Preview Scene`/`Apply Preview Spacing`은 v1 도구다. 현재 v2 씬에 구형 여백 도구를 적용하지 않는다.

### v2 확인 결과

- 컴파일 성공, Play Mode 종료 후 Console Error/Warning 0개.
- 메뉴·시너지 4행·전투 시작의 Rect 중심에서 EventSystem Raycast로 해당 Button이 최상위 입력 대상인지 확인한 뒤 PointerClick 이벤트를 실행했다. 6개 모두 입력 대상 일치/클릭 처리 성공.
- 메뉴/시너지 팝업 열기·닫기 및 Combat 페이지 전환 성공. 실제 마우스로 모든 경계 픽셀을 클릭한 검증은 아니다.
- Missing Script 0개, View의 단일/배열 Text·Image 참조 누락 0개.
- Fill의 실제 Sprite는 Experience_White, fillAmount 0.6. 기준값 표시 HTML 색상은 EBDC99/777777.
- 화면에서 밝은 배경과 Wave 내부 체크 잔재를 발견해 해당 두 부분만 정리했다. 기능 검증은 1회 수행했다.
- 원본 UI_Battle SHA256은 기존 값과 동일하며, 새 프리뷰만 저장하고 Edit Mode로 종료했다.
- 최종 화면: `Tools/Art/Previews/UI_Battle_MutedPreview_Reference_v2_Final.png`.
- Scene/Meta 추가·수정 있음. Prefab/ProjectSettings/Packages 변경 없음. 기존 사용자의 Editor 폴더 이동은 보존했다.
- 커밋/push하지 않았다.

## 목적과 범위

- 대상: `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`
- 원본 `UI_Battle.unity`를 Unity AssetDatabase로 복제한 뒤 표시 레이어만 변경했다.
- 상단 통합 HUD, 왼쪽 적 예고, 오른쪽 시너지 4행, Cost, 리롤, 전투 시작, 메뉴를 분리 리소스로 구성했다.
- 모든 숫자와 문구는 `UnityEngine.UI.Text`이며 이미지에 포함하지 않았다.
- 카드, U자형 하단 배경, 전투 유닛, 타일의 신규 아트는 이번 범위에서 제외했다. 카드와 배치 영역은 원본의 자리표시자를 유지한다.
- 실제 전투 데이터·경제·저장 로직·기존 팝업·로비·다른 씬은 변경하지 않았다.

## 편집 위치

루트 `UI_BattleScreens` 기준:

| 위치 | 내용 |
| --- | --- |
| `Canvas_GetReady/BattleHUD` | 상단 몸체·웨이브·레벨·시간·경험치 |
| `Canvas_GetReady/BattleHUD/ExperienceTrack` | 경험치 바탕 |
| `Canvas_GetReady/BattleHUD/ExperienceFill` | 경험치 비율 표시, Filled/Horizontal |
| `Canvas_GetReady/BattleHUD/ExperienceTicks` | 별도 눈금 오브젝트 |
| `Canvas_GetReady/SettingsButton` | 메뉴 아이콘, 기존 설정 팝업 연결 유지 |
| `Canvas_GetReady/WavePreview` | 이번 웨이브의 적 3종 이름·수량·아이콘 |
| `Canvas_GetReady/SynergyTrackers/Synergy_0..3` | 마법 결속·사격 대형·불굴의 뼈·저주 의식 |
| `Canvas_Preparation/Currency` | Cost 프레임·아이콘·수치 |
| `Canvas_Preparation/Reroll_Placeholder` | 리롤 프레임·아이콘 |
| `Canvas_Preparation/RerollPrice`, `RerollCost` | 리롤 비용 프레임·통화 아이콘·Text |
| `Canvas_Preparation/StartCombatButton` | 전투 시작, 원본 Combat 페이지 전환 유지 |

시너지 전체를 감싸는 외부 프레임은 없고 각 행만 별도 프레임을 가진다. 현재 Cost/전투 시작은 260 × 260이다. 메뉴와 시너지 팝업은 기존 UI 흐름을 재사용한다. 하단 파란 보조 버튼은 v2에서 비활성 보관한다.

교체된 기존 자식은 삭제하지 않고 비활성 `Legacy_` 자식으로 보관한다. 튜토리얼, 기존 증강 상세 자리표시자, 기존 하단 회색 배경은 복제 씬에서만 숨겼다.

## 공통 시너지 행 프리팹 (2026-09-26)

- 원본: `Assets/06.UI/BattleMutedPreview/Prefabs/SynergyTracker.prefab`
- 씬의 `UI_BattleScreens/Canvas_GetReady/SynergyTrackers/Synergy_0..3`은 모두 이 원본의 인스턴스다. 독립적인 프리팹 복사본 4개가 아니다.
- `Synergy_0`의 기존 디자인을 기준으로 만들었으며, 내부 RectTransform 배치에는 개별 Override를 두지 않았다. 공통 내부 레이아웃과 테두리 Sprite는 원본에서 수정한다.
- 각 행의 루트 위치, 클릭 대상, 분류별 테두리 색상·아이콘·이름은 씬에서 유지한다. 루트 RectTransform은 Unity의 기본 인스턴스 Override 대상이므로 행 전체의 위치·크기와 행 간격은 씬에서 확인한다.

| 편집 목적 | 편집 위치 |
| --- | --- |
| 공통 마름모 테두리 이미지 | 프리팹의 `Frame > Image > Source Image` |
| 공통 아이콘 위치·크기 | 프리팹의 `Icon > RectTransform` |
| 개별 시너지 아이콘 | 씬의 각 `Synergy_n/Icon > Image > Source Image` |
| 개별 테두리 색상 | 씬의 각 `Synergy_n/Frame > Image > Color` |
| 이름 판 배경 | 프리팹의 `Nameplate/Body` |
| 이름 판의 현재 외곽선 | 프리팹의 `Nameplate/ReferenceTop`, `ReferenceBottom`, `ReferenceLeft`, `ReferenceRight` |
| 이름·수치 글꼴과 배치 | 프리팹의 `Name`, `Thresholds` |

`Nameplate/Border`는 이전 디자인의 비활성 이미지다. 현재 외곽선은 위의 `Reference*` 네 개로 표시된다. 새 테두리 이미지로 바꾸려면 프리팹에서 기존 선을 숨기고 `Border`를 활성화해 교체할 수 있다.

이름·기준 수치 내용은 기존 `UIBattleMutedPreviewView`가 갱신한다. 문구를 바꿀 때는 `UI_BattleScreens`의 해당 미리보기 값 또는 `SetSynergy`를 사용한다. Text만 직접 바꾸면 다음 갱신에서 데이터 값으로 돌아간다.

프리팹 에셋의 Button에는 씬 객체를 저장하지 않는다. 씬 인스턴스의 `On Click`에 기존 `UIPopupController.OpenPopup(Popup_Synergy)` 연결을 유지했고, View의 이름·수치 참조 8곳을 새 인스턴스로 교체했다. 다른 씬에 새로 배치할 때는 이 연결을 별도로 지정해야 한다. 개별 아이콘·색상을 공통 원본에 `Apply All` 하지 않는다.

교체는 Unity MCP로 수행했으며 Runtime 코드·전투 규칙은 변경하지 않았다. 기존 미저장 씬 변경을 먼저 저장하고 `Temp/SynergyTracker_BeforePrefab_20260926.unity`에 작업 전 사본을 보관했다. 이전 행은 새 인스턴스로 대체했으며 씬 교체는 Unity Undo로 되돌릴 수 있다. 새 프리팹 에셋 생성 자체는 씬 Undo의 삭제 대상이 아니다.

검증: 4개 인스턴스의 공통 원본 연결, 내부 레이아웃 Override 없음, 표시 참조 8곳 및 누락된 참조 0개를 확인했다. Play Mode에서 각 행의 `SetSynergy` 표시 갱신과 클릭 이벤트를 통한 팝업 열기·닫기를 한 번씩 확인했으며 Console 오류·경고는 0개였다. 실제 마우스 입력·다른 화면비 검사는 이번 구조 변경에서 별도로 수행하지 않았다.

## 표시 코드와 데이터 연결

`Assets/00.Project/01.Scripts/UI/Battle/UIBattleMutedPreviewView.cs`

- `UI_BattleScreens`에 연결된 표시 전용 컴포넌트다.
- Inspector의 미리보기 값으로 이름·수량·시간·Cost·경험치 비율을 편집할 수 있다.
- Runtime 갱신은 `SetWave`, `SetLevelExperience`, `SetRemainingTime`, `SetCost`, `SetEnemy`, `SetSynergy`를 사용한다.
- 남은 시간은 표시만 하며 자체 카운트다운은 하지 않는다.
- 경험치는 0~1 범위로 제한하여 Fill에 반영한다.
- 시너지 `3 > 5`는 기준값 표시다. 배치 유닛 집계나 버프 계산을 구현한 것은 아니다.
- Runtime 표시값과 실제 전투/저장 데이터의 책임을 분리했다. ScriptableObject나 저장 데이터는 추가하지 않았다.

Cost와 리롤은 원본부터 동작 없는 비활성 자리표시자다. 이번에도 리롤 추첨·비용 차감은 구현하지 않았고, 비활성 버튼의 색이 아트를 중복으로 어둡게 하지 않도록 표시만 처리했다.

## 아트 리소스

`Assets/06.UI/BattleMutedPreview/Sprites/`: 23개 PNG

- 생성 아트 21개: 직사각 프레임 4종, 마름모 프레임 2종, 세로 구분선 1종, 통화 보석 1종, 독립 아이콘 13종.
- 기존 로비 경험치 이미지에서 투명 여백만 제거한 Track/Fill 2개.
- PNG의 실제 알파, 아이콘 중심 pivot (0.5, 0.5), Point 필터, 무압축, Mipmap 없음.
- 직사각 프레임은 9-slice이며 몸체의 어두운 색상은 별도 Image로 관리한다.
- 프레임·Fill·눈금·아이콘·Text를 각각 교체할 수 있다.
- 생성 원본: `Tools/Art/Sources/BattleMutedPreview_v1/`
- 후처리: `Tools/Art/PrepareBattleMutedSprites.ps1`
- 후처리는 승인된 배경 정리·분할·여백/중심 정렬만 수행한다. 생성 원본과 기존 로비 이미지는 변경하지 않는다.
- 재생성 시 스프라이트 import는 Unity Editor에서 수행하며 .meta를 직접 편집하지 않는다.

글꼴은 기존 `Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSGothic.ttf`를 사용한다. 해당 로컬 공유 폴더가 없는 팀원은 기존 Drive 공유 폰트를 먼저 준비해야 한다. 이번 작업에서 SDF 생성·Git 추적 정책 변경은 하지 않았다.

## Editor 도구

`Assets/Editor/BattleMutedPreviewBuilder.cs`

- `Tools > OZGL2 > Battle > Create Muted Preview Scene`
  - 원본 복제 후 새 씬 생성. 기존 프리뷰가 있으면 덮어쓰지 않는다.
  - Play Mode 또는 미저장 씬이 있으면 중단한다.
- `Tools > OZGL2 > Battle > Apply Preview Spacing`
  - 활성 씬이 새 프리뷰일 때만 여백·프레임 두께·경험치 리소스를 적용하고 저장한다.
  - 사용자 디자인 수정 후 실행하면 해당 배치 항목은 도구의 기준값으로 돌아가므로 의도할 때만 실행한다.
- 씬 오브젝트 편집은 Undo를 지원한다. AssetDatabase 씬 복제와 텍스처 import 설정은 Undo 대상이 아니다.
- Runtime 코드에서 UnityEditor를 참조하지 않는다.

## 검증 결과 (2026-09-23)

- Unity 컴파일 및 Console 확인: Error 0, Warning 0.
- Play Mode에서 메뉴 열기/닫기·팝업 입력 차단·시너지 4행 팝업·전투 페이지 전환 확인.
  - 버튼 연결은 UnityEvent 호출로 검증했다. 실제 마우스 히트 영역을 전부 클릭하는 검증은 별도로 하지 않았다.
- 표시 API를 통한 웨이브·레벨·시간·Cost·적 수량 갱신 확인.
- 경험치 실제 연결 Image의 Filled 타입과 0.6 비율 확인.
- 씬의 Missing Script 0개 확인.
- 표시 컴포넌트의 Text·Fill·배열 참조 누락 0개 확인.
- 최초 화면에서 발견한 프레임/텍스트 겹침과 경험치 원본의 과도한 투명 여백만 보정하고 보정 화면 확인.
- 원본 UI_Battle SHA256 동일:
  `F23160B80702E70610F4899336E7BCD35CE3281C00414BB76FB27C7777881CF4`
- 새 씬 저장 후 Edit Mode로 종료했다.

화면 결과: `Tools/Art/Previews/UI_Battle_MutedPreview_Final.png`

## Inspector / 후속 연결 체크리스트

- `UIBattleMutedPreviewView`의 Text·Fill·3개 적·4개 시너지 배열 연결 유지.
- 동적 레벨/경험치 값은 View API로 전달한다. Fill을 Inspector에서 단독 변경하면 View의 미리보기 값이 우선한다.
- 실제 전투 연동 시 시스템에서 이벤트로 표시 API 호출. UI가 전투 데이터를 소유하지 않게 유지.
- 향후 리롤 기능을 연결할 때만 Button을 활성화하고 비용/추첨 시스템을 별도로 연결.
- 실제 마우스 입력, 다른 해상도/화면비에서 히트 영역과 잘림은 후속 Play 확인 대상.
- 신규 LayerMask/Tag/Collider/Rigidbody/Animator/Input Action 설정은 필요 없다.

## Git / AI 변경 검토

- Scene: 새 프리뷰 1개 추가, 원본 변경 없음.
- Prefab / ProjectSettings / Packages: 이번 작업 변경 없음.
- .meta: 새 Scene·스크립트·아트에 대해 Unity가 생성한 파일.
- 변경 코드: 표시 전용 Runtime 1개, 제한된 씬 생성/정렬 Editor 1개, 승인된 아트 후처리 스크립트 1개.
- 기존 Editor 폴더 이동 등 작업 시작 전에 있던 변경은 보존했고 이번 작업 범위에 포함하지 않았다.
- 커밋·push는 수행하지 않았다.

## 하단 왼쪽 레퍼런스 재적용 (2026-09-23)

- 승인 범위: `Canvas_Preparation`의 Cost·리롤·리롤 가격 표시와 `BottomNobleBackground`의 표시된 마름모 장식 3곳만 수정했다.
- 새 아트 7종: `Assets/06.UI/BattleMutedPreview/LowerLeftReference/`의 Cost/리롤/가격 프레임, 불꽃/순환 화살표/통화 보석, 통합 벨벳 패널.
- 배경의 양쪽 끝 장식과 왼쪽 골의 장식만 생성 단계에서 제거했다. 나머지 봉우리·중앙 다리 장식·하단 장식은 유지했다.
- 현재 사용자가 조정한 Cost 루트 위치와 배경 RectTransform/색상을 먼저 저장한 후 그대로 보존했다. 배경의 어두운 0.55 tint도 유지했다.
- 리롤은 138→168 크기로 확대하고 Cost와 같은 중심 높이, 작은 배경 봉우리의 중심축에 맞췄다. 가격표는 180×56이며 리롤 아래 4 간격이다.
- 숫자는 기존 `UnityEngine.UI.Text`를 유지하고 이미지에 넣지 않았다. 숫자 가독성/굵기 구분에 기존 로컬 `NotoSansCJKkr-Regular.otf`를 사용한다(Cost 56 Bold, 리롤 비용 40 Normal). 해당 폰트 공유 파일이 없는 팀원은 기존 로컬 폰트 준비가 필요하다.
- 모든 새 Sprite는 실제 투명 배경, 중심 pivot, Point, FullRect, 무압축, Mipmap 없음으로 Unity에서 임포트했다. 원본 PNG는 보존했다.

### 다시 적용하는 Editor 도구

- 파일: `Assets/Editor/BattleLowerLeftReferencePolisher.cs`
- 메뉴: `Tools > OZGL2 > Battle > Apply Lower Left Reference`
- 씬 오브젝트 Undo 지원. 텍스처 임포트 설정은 Undo 대상이 아니다.
- 프리뷰 씬/Edit Mode/저장된 상태에서만 적용되며, 동일 이름 중복·누락·Prefab 인스턴스·자동 레이아웃은 중단한다.
- 다른 UI 배치를 건드리지 않도록 기존 전체 빌더와 분리했다. 재실행하면 해당 Cost 자식·리롤·가격표의 수동 디자인 조정은 이 기준값으로 돌아간다.
- 후처리: `Tools/Art/PrepareBattleLowerLeftArt.ps1`, `Tools/Art/PrepareBattleUnifiedPanel.ps1`(새 출력 경로 지정 지원).
- 생성 방식·전체 프롬프트·정렬 좌표: `Tools/Art/Sources/BattleLowerLeftReference/GENERATION.md`.

### 이번 변경 검증

- Unity 컴파일 완료, Console Error 0 / Warning 0, Missing Script 0.
- Play Mode의 1920×1080 화면 1회 확인: `Tools/Art/Previews/UI_Battle_MutedPreview_LowerLeftReference.png`.
- 적용 전후 213개 오브젝트 비교: 오브젝트 수·활성 상태·Button 직렬화 데이터 동일. Cost 루트와 배경 RectTransform 동일. 승인된 영역 밖의 RectTransform/Image 변경 없음.
- Cost/리롤/전투 시작 중앙 Raycast가 각 버튼에 도달함을 확인했다. 전투 시작 PointerClick 실행으로 Preparation 비활성/Combat 활성 전환을 확인했다.
- Cost와 리롤은 원래와 동일하게 비활성 자리표시자(실제 비용 차감/리롤 기능 미구현)다. 기존 기능을 새로 구현하지 않았다.
- 동적 숫자 Text와 새 Sprite 7종의 Inspector 참조·중심 pivot·필터 설정을 확인했다.
- Edit Mode 복귀, 씬 저장 완료. 원본 `UI_Battle.unity` SHA256 동일. 다른 Scene/Prefab/Runtime/ProjectSettings/Packages는 이번 변경에서 수정하지 않았다.
- 신규 LayerMask/Tag/Collider/Rigidbody/Animator/Input Action 연결 없음. 다른 해상도/화면비는 미확인.
- Git 검토 대상: 프리뷰 Scene(충돌 주의), 새 아트와 Unity 생성 `.meta`, Editor 도구, 아트 준비 스크립트, 이 문서와 생성 기록. 기존 사용자 Editor 폴더 이동은 보존했다.
