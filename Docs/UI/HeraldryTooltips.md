# 라운드 보상·시너지 설명창

## 편집 위치

- 라운드 보상: `Assets/06.UI/BattleMutedPreview/Heraldry_Tooltips_v1/Prefabs/RoundRewardPanel_Heraldry.prefab`
- 시너지 상세: `Assets/06.UI/BattleMutedPreview/Heraldry_Tooltips_v1/Prefabs/SynergyTooltipPanel_Heraldry.prefab`
- 연결된 씬: `Assets/00.Scenes/Builds/InGame.unity`만 변경한다. 다른 씬은 참조가 없으면 기존 표시 방식을 유지한다.

## 구조 및 조정 방법

`Background`, `Frame`, `HeaderOrnament`, `Content`를 분리한다. `Frame`은 내부가 투명한 9분할 이미지이고 검정 내부는 `Background`가 채운다. 마왕 투구·붉은 깃발은 `HeaderOrnament`의 독립 이미지다. Sprite 사각형은 투명 여백을 제외하도록 설정했다. 두 PNG의 금색 RGB는 사용자 승인 후 현재 전투 하단 UI의 금색에 맞춰 직접 보정했으며, 알파·붉은 깃발·해상도·형태와 Sprite 설정은 유지했다.

프리팹 루트의 `UIAdaptiveTooltipPanel`을 편집한다.

- `Base Width`: 1080p 기준 라운드 보상 420, 시너지 600. 같은 종류의 창은 내용 길이에 관계없이 같은 폭을 유지한다.
- `Padding`: 왼쪽/위/오른쪽/아래. 기본값 32/50/32/28.
- `Ornament Size`: 기본값 88×70. 창 높이 변화에 따라 늘어나지 않는다.
- `Frame Pixels Per Unit Multiplier`: 기본값 4. 모서리·테두리 두께를 조정한다.
- `Title Font`: 기존 `HeirofLightBold_SDF`.
- `Body Font`: 기존 `NEXON_Lv2_Gothic_SDF`.

`Content`의 글자는 에디터 미리보기 예시다. 실제 보상·시너지 문구는 기존 HUD가 생성하므로 프리팹의 예시 글자를 바꿔도 게임 문구는 바뀌지 않는다. 기존 `RectTransform`의 높이 역시 실행 시 내용에 맞게 다시 계산한다. 기준 폭과 여백은 위 컴포넌트에서 조정한다.

게임에서는 화면 배율에 맞춘 폭 안에서 TMP가 줄바꿈하고, 그 높이와 아이콘/초상화 높이를 합산해 창의 높이만 늘린다. 해상도가 바뀌면 다시 계산한다. 배경·9분할 프레임만 높이에 따라 늘어나며 장식은 고정 크기를 유지한다.

## 연결 및 편집 도구

- `Tools/OZGL2/Battle Tooltips/Build New Heraldry Prefabs`: 최초 생성 전용. 기존 프리팹이 있으면 중단하여 수동 편집을 덮어쓰지 않는다. 씬을 저장하지 않는다.
- `Tools/OZGL2/Battle Tooltips/Connect To Current InGame`: 현재 InGame의 `InGameCurrencyHud`와 `UIBattleMutedPreviewView`의 `InGameSynergyHudBridge`에 프리팹을 연결한다. Undo를 지원하며 씬 저장은 별도로 해야 한다.
- `Tools/OZGL2/Battle Tooltips/Validate In Temporary UI Scene`: 게임 초기화 없이 임시 Edit Mode 씬에서 실제 표시 메서드를 검사한다. 원본 SO는 수정하지 않는다. 원래 씬을 활성화한 뒤 임시 씬은 닫고, 캡처만 `Temp/HeraldryTooltips_20261012`에 남긴다. 원본 씬의 미저장 표시가 생기면 비교 후 처리하고 무조건 저장하지 않는다.
- `Tools/OZGL2/Battle Tooltips/Validate Wave Reward Attachment`: 실제 웨이브 Viewport와 HUD를 임시 씬에 복제해 접힘에 따른 보상창 위치를 검사한다. 계정·전투·실제 보상은 사용하지 않고 Scene/Prefab도 저장하지 않는다. 원본이 처음에 저장된 상태였고 검증 전후 직렬화 상태가 동일할 때만 다시 열어 미저장 표시를 정리한다.

기존 보상 계산·지급, SP·유닛 해금, 시너지 발동 조건과 효과, 저장 데이터는 변경하지 않는다. `UIAdaptiveTooltipPanel`은 외형 설정만 담당한다. 기존 `_frameSprite`는 다른 안내에도 쓰이므로 변경하지 않는다.

## 검증

2026-10-12, Unity 6000.3.22f1 / Edit Mode / 1920×1080:

- 실제 시너지 6종: 너비 600, 글자 넘침 없음.
- 긴 조합 설명의 복제 데이터: 너비 600 유지, 높이가 약 485→552로 증가, 글자 넘침 없음.
- 보상 2~4행: 너비 420 유지, 아이콘 및 72px 초상화 영역을 포함해 자동 높이 계산.
- `Frame`은 Sliced, 장식 크기는 모든 경우 88×70 유지.
- Unity 컴파일 성공, 검증 후 Console Error 0개.
- 계정 데이터와 실제 지급은 사용하지 않았으며 Play Mode는 실행하지 않았다. 실제 초상화 렌더러 대신 테스트 텍스처로 레이아웃을 확인했다.

Play Mode에서 팀원이 확인할 항목:

- 실제 시너지 행의 호버 진입/이탈, 배치 수 변경, 활성·비활성 표시.
- 웨이브 창을 접으면 보상창이 상단 HUD 아래로 올라오고, 펼치면 보이는 웨이브 창 아래를 따라가는지 확인.
- 웨이브 패널 아래 보상창과 증강 목록의 간격 및 실제 유닛 초상화 표시.
- 화면 해상도 변경 후 창의 폭·글자 높이·화면 가장자리 위치 재계산.
- `NullReferenceException`, `MissingReferenceException` 및 Console Error 확인.

현재 범위에서는 내용에 맞춰 높이를 늘리며 최대 높이/스크롤은 추가하지 않았다. 향후 설명이 크게 늘거나 낮은 화면 높이에서 화면을 초과한다면 별도 스크롤 설계가 필요하다. LayerMask, Tag, Collider, Rigidbody, Animator, Input System, ProjectSettings는 변경하지 않는다.

### 웨이브 접힘 추종 수정 (2026-10-12)

새 이미지 교체 이전의 `55402b9`(2026-10-08)에서 웨이브 창의 가장 낮았던 바닥을 기억해 보상·증강 목록을 고정하는 처리가 추가돼 있었다. 완전히 접히면 보이는 Graphic이 없어 화면 높이의 58% 지점으로 내려가는 기본 위치도 사용됐다.

`InGameCurrencyHud.PlaceSummaryUnderWavePanel`에서 누적 Y 캐시를 제거하고 현재 마스크 안에 보이는 영역을 기준으로 계산한다. Viewport 자체와 부모 마스크를 모두 지원하며, 완전히 접히면 Viewport 상단에서 기존 28×화면배율 간격을 유지한다. 좌측 X는 Viewport 기준으로 고정하고 부모 CanvasGroup의 투명도도 확인한다. `SummaryBottom`을 읽는 기존 증강 목록 역시 같은 위치 기준을 따라간다. Scene/Prefab/이미지 수정과 새로운 Inspector 연결은 필요 없다.

1920×1080 임시 Edit Mode 검증 결과: 펼침 Y 725.2 → 중간 829.6 → 접힘 934 → 재펼침 725.2, X 53.3 유지. 시작부터 접힌 경우, 부모 마스크가 있는 자식 패널 구조 및 8회 반복 전환을 통과했다. 캡처는 `Temp/HeraldryTooltips_20261012/WaveReward_Expanded.png`, `WaveReward_Collapsed.png`에 남긴다. 수정 후 실제 게임의 Tween·클릭을 포함한 Play Mode 재실행은 하지 않았다.

## Git 검토 범위

### 하단 UI 금색 통일 (2026-10-12)

`tooltip_frame_gold_v1.png`, `tooltip_crest_v1.png`의 금색만 `Heraldry_Battle_v5/Source/currency_medallion_opaque.png`의 명암별 RGB 팔레트에 맞췄다. 현재 리롤 링과 하단 가로 테두리도 비교했다. 기존 Image Tint와 sRGB 설정은 모두 같았으므로 Scene/Prefab/C# 설정을 바꾸지 않고 공통 PNG만 갱신했다.

픽셀 검증: 프레임 1254×1254, 장식 1774×887 유지. 알파·금색 외 영역·투명 영역·붉은색 픽셀 변경은 각각 0개다. Sprite GUID와 프레임의 205px 9분할도 유지했다. 임시 Edit Mode에서 시너지 6종·긴 설명·보상 2~4행의 레이아웃을 다시 검증했고 모든 글자 넘침이 없었다. 실제 하단 원본 Sprite와 나란히 렌더한 비교 캡처는 `Temp/TooltipGoldMatch_20261012/GoldMatch_RewardHud.png`, 보정 전 PNG 백업도 같은 폴더에 남긴다. 원본 InGame은 저장하지 않았고 실제 Play Mode는 실행하지 않았다.

비교 캡처 첫 실행에서 임시 Camera의 targetTexture를 해제하기 전에 RenderTexture를 정리해 오류 1건이 발생했다. 임시 검증 코드의 정리 순서를 수정한 재실행에서는 신규 오류 0건을 확인했다. 기존 Console 기록은 삭제하지 않았으며 손패 Viewport 높이 경고는 이번 색상 변경과 별개로 남아 있다. 기존 Scene/C#/Prefab/Sprite 설정/기준 PNG 등 21개 파일은 작업 전후 해시가 같았다.

충돌 주의: `InGame.unity`, `InGameCurrencyHud.cs`, `InGameSynergyHudBridge.cs`. 씬 변경은 신규 프리팹 참조 두 개와 시너지 연결 컴포넌트 추가뿐이다. 신규 이미지·Sprite·프리팹·스크립트의 `.meta`는 Unity가 생성한다. Lobby, Title, InGame_UIIntegration 및 기존 외부 원본 에셋은 수정하지 않는다.

이미지 생성 프롬프트는 `Docs/UI/HeraldryTooltipArtPrompts.json`에 보관한다.

## 시너지 아이콘 리메이크 및 직접 연결 (2026-10-12)

### 에셋과 편집 위치

`Assets/06.UI/BattleMutedPreview/Heraldry_Tooltips_v1/SynergyIcons/`에 시너지 전용 PNG 6종을 추가했다. 기본 제공 이미지 생성 도구로 직업별 도안을 만들고, 사용자 승인 범위에서 RGB·알파·미세 잡티·여백·중앙 정렬을 직접 정리했다. 정사각형 512×512, 크림색 `#E9DFC6`, 가시영역의 최장 변 384px, 본체 불투명·내부와 바깥 투명·가장자리 안티앨리어싱을 공통으로 사용한다. 기존 보상 아이콘과 공용 원본은 바꾸지 않았다. 마법사는 실제 작은 표시에서 읽기 쉽도록 별과 지팡이 머리를 넓힌 생성 편집본을 선택했다.

| 직업 | 파일 | 문양 | Inspector 연결 |
| --- | --- | --- | --- |
| 전사 | `synergy_warrior_v1.png` | 교차 검 | Warrior Icon |
| 방패병 | `synergy_shield_v1.png` | 뼈 방패 | Shield Icon |
| 궁수 | `synergy_archer_v1.png` | 활과 화살 | Archer Icon |
| 마법사 | `synergy_mage_v1.png` | 별과 지팡이 | Mage Icon |
| 도적 | `synergy_rogue_v1.png` | 눈과 단검형 꼬리 | Rogue Icon |
| 힐러 | `synergy_healer_v1.png` | 치유 십자와 날개 | Healer Icon |

연결 위치는 **InGame → UI_BattleScreens → InGameSynergyHudBridge → 직업별 시너지 아이콘**이다. 목록과 호버 설명창이 같은 Sprite를 사용한다. 목록의 Icon은 기존 52×52·흰색 Tint·Preserve Aspect를 유지하고, 상세 아이콘도 기존 `34.32×화면배율` 크기를 유지한다. 깃발의 직업색과 수량/정렬/시너지 효과는 바꾸지 않는다. 고정된 Edit Mode 행의 미리보기 그림은 그대로이며, 게임의 Refresh에서 실제 직업 그림으로 교체된다. Inspector 연결 작업은 Undo를 지원했다.

### 연결 문제와 최소 수정

이전 코드는 로드된 Sprite의 이름을 최초 1회 검색했다. InGame에는 `Icon_CrossedSwords`가 로드되지 않았고, `Icon_Flame`은 서로 다른 크기의 동명 Sprite가 존재했다. 찾지 못한 슬롯의 그림을 지우지 않아 다른 직업의 이전 그림이 남을 수 있었다. 힐러도 재화 불꽃을 임시 사용하고 있었다.

`InGameSynergyHudBridge.cs`에 private SerializeField Sprite 6개만 추가했다. 하나라도 연결된 씬은 직접 연결만 사용하며, 일부 누락을 공용 이름 검색으로 메우지 않는다. 누락 시 기존 그림을 지우고 Image를 숨기며 직업별로 경고를 제한한다. 연결을 복구하면 다시 표시된다. 아이콘 캐시 확인을 목록 내용의 signature 검사보다 먼저 수행해, 배치 수가 같아도 Sprite 참조가 바뀌면 목록과 호버 캐시를 함께 갱신한다. OnEnable/OnValidate는 이 캐시를 초기화한다.

6개가 모두 미연결인 구형 씬만 이름 기반 호환 경로를 사용한다. 최대 1초 간격으로 다시 검색하고, 동명 Sprite가 여러 개면 임의 선택하지 않는다. 완성 캐시도 재확인하므로 뒤늦은 로드·중복·중복 해소를 반영한다. 새 public API나 Runtime 클래스, 패키지, 게임 데이터는 추가하지 않았다.

### 검증 결과와 남은 확인

Unity 6000.3.22f1에서 컴파일 완료, Console Error 0개. 임시 Additive Edit Mode 씬에 실제 행을 복제하고 순수 `SynergyTracker`와 실제 private Refresh/BuildTip 표시 메서드를 호출했다. 게임 초기화·RealSynergySync·계정·실제 보상·Play Mode는 사용하지 않았다.

- 6직업 각각 목록과 상세의 Sprite 일치, 글자 넘침 없음, 기존 흰색 Tint·Preserve Aspect·크기 유지.
- 같은 슬롯의 전사→마법사→힐러→전사 재사용, 배치 수가 같은 상태의 Sprite 교체와 호버 캐시 무효화 통과.
- 일부 직접 연결 누락 시 목록/상세 그림 숨김, 반복 Refresh에도 경고 1회, 참조 복구 후 재표시 통과.
- 기존 최대 4행 및 단계→배치 수→직업 순 정렬 유지.
- 구형 연결의 뒤늦은 로드, 동명 Sprite 중복/해소, 6개 완성 캐시 이후 재검색 통과.
- OnEnable/OnValidate가 공유하는 ResetIconCache 메서드는 검사했다. 전체 활성화/비활성화 흐름과 실제 마우스 진입·이탈은 Play Mode 재확인이 필요하다.
- 현재 Editor 화면배율 0.8에서 상세 너비 480, 높이 약 387.65, 상세 아이콘 약 27.456px. 1920×1080 캡처에 목록 6직업과 실제 마법사/힐러 상세를 배치했다. 캡처 해상도와 표시 메서드가 참조하는 Game View 화면배율은 별개다.
- 최종 검증 실행의 경고 3회는 일부 연결 누락/이름 중복을 의도적으로 만든 테스트 결과다. 기존 `InGameCardSizing._rewardRestingYOffset` 미사용 필드 컴파일 경고(CS0414)는 이번 변경과 무관하며 유지했다. Console 기록을 지우지 않았다.
- 원본 InGame의 직렬화 상태가 검증 전후 같고 미저장 표시도 없었으며, 임시 씬을 닫아 InGame 하나만 남겼다. 저장 시 함께 들어온 Separator_1/2 오버라이드는 사용자 승인 후 현재 커밋 상태로 복원했다.

검증 캡처/상세 결과는 Git에서 제외되는 `Temp/SynergyIcons_20261012/SynergyIcons_ActualUI.png`, `SynergyIcons_Normalized.png`, `ValidationResults.json`에 있다. 생성/편집 프롬프트 전체는 `Docs/UI/HeraldrySynergyIconArtPrompts.json`에 기록한다.

팀원 Play Mode 확인: 실제 유닛 배치/회수 때 직업 아이콘과 호버가 함께 바뀌는지, 화면 크기 변경 및 카드 보상 중 숨김/복귀, NullReferenceException/MissingReferenceException이 없는지 확인한다. Inspector의 6개 Sprite와 기존 Tooltip Panel Prefab 연결을 유지한다. LayerMask/Tag/Collider/Rigidbody/Animator/Input System 설정 변경은 없다.

### Git 변경 범위

- `InGame.unity`: 기존 연결 컴포넌트에 Sprite 참조 6곳만 추가.
- `InGameSynergyHudBridge.cs`: 직접 연결·캐시 갱신·호환 재탐색·누락 숨김/경고.
- `SynergyIcons/`: 새 PNG 6종과 Unity가 생성한 폴더/PNG 메타.
- 이 문서와 `HeraldrySynergyIconArtPrompts.json`: 편집 위치·프롬프트·검증 기록.

Prefab/다른 씬/ProjectSettings/Packages/기존 공용 아이콘/보상 아이콘은 변경하지 않는다. 충돌 주의 파일은 `InGame.unity`, `InGameSynergyHudBridge.cs`이다. 이 작업은 자동 커밋하거나 push하지 않는다.

## 시너지 설명창 레퍼런스 레이아웃 적용 (2026-10-12)

이 후속 작업은 위 아이콘 작업의 상세 표시 크기와 기존 세로 단계 배치를 대체한다. InGame에 이미 연결된 `SynergyTooltipPanel_Heraldry.prefab`만 갱신하므로 씬 참조를 추가로 저장하지 않는다. 보상 프리팹·공통 `UIAdaptiveTooltipPanel`·기존 이미지·게임 판정은 변경하지 않는다.

### 표시 구조와 조정 위치

- `Content/JobHeader`: 112×112 아이콘, 오른쪽 명조 제목 46 및 고딕 배치 수 26. 헤더는 실제 목록 `FlagLeft.Image.color`를 읽어 왼쪽→오른쪽으로 사라지는 그라데이션을 적용한다. 기존 `SideFade.asset`을 재사용하고 알파는 0.28로 낮춰 어두운 테마와 글자 대비를 유지한다.
- `Content/HeaderDivider`: 적색 구분선과 금색 끝장식.
- `Content/TierProgression`: 1단계 → 2단계의 요구 인원·효과를 두 열로 비교한다. 단계 제목 36, 요구 인원과 효과 26이며 두 열 중 더 긴 설명에 맞춰 높이를 맞춘다.
- `Content/ComboHeading`: 모든 직업이 공유하는 금색 그라데이션과 명조 구역 제목 40.
- `Content/ComboEntries`: 개별 조합 이름 30, 조건·효과 26을 각각 별도 줄로 배치한다. 항목 간격 28, 줄 간격 6이다. 활성 단계·조합의 기존 녹색 표시는 유지한다.
- 마지막 효과의 줄바꿈 높이 다음에만 아래 여백 28을 더한다. 기준 폭 600을 유지하고 설명과 조합 개수에 따라 높이가 늘어난다. 실제 Game View가 작으면 시너지 창 전체 배율을 줄여 화면 안에 맞춘다. 스크롤은 추가하지 않는다.

Prefab Mode에서 루트의 **UISynergyTooltipView**로 글자 크기·아이콘 크기·줄/항목/구역 간격·그라데이션 강도를 조정한다. 아래 여백과 기준 너비는 기존 **UIAdaptiveTooltipPanel → Padding.w / Base Width**에서 조정한다. 하위 TMP 크기·RectTransform 위치·헤더 색은 표시 때 계산되므로, 지속적인 조정은 루트 설정으로 한다. 제목/조합 이름은 기존 명조, 배치 수/조건/효과는 기존 고딕 글꼴을 사용한다.

### 코드와 Editor 도구

- `UISynergyTooltipView.cs`: 시너지 표시와 자동 높이만 분리한다. 순수 Tracker/SO를 읽기만 하며 조합 항목을 재사용하고, 부족할 때만 템플릿을 복제한다. 게임 효과·저장·보상 책임은 없다.
- `InGameSynergyHudBridge.cs`: 새 view가 연결된 프리팹에만 새 표시를 사용하고, 직업색은 목록에서 읽는다. 미연결 또는 불완전한 프리팹은 기존 표시로 돌아간다. 기존 아이콘 연결·캐시는 유지한다.
- `SynergyTooltipLayoutBuilder.cs`: 메뉴 `Tools/OZGL2/Battle Tooltips/Apply Synergy Reference Layout`. 지정 프리팹의 Prefab Mode에서만 최초 구조 변환을 한다. Undo 한 묶음을 지원하며 저장은 별도다. 완성본 재실행은 아무것도 바꾸지 않고, 불완전한 기존 연결/알 수 없는 자식은 보존하고 중단한다. 수동 편집한 완성본을 파괴·재생성하지 않는다.
- `HeraldryTooltipValidation.cs`: 사용자 승인 후 기존 검증 메뉴를 새 정적 구조 보존 방식으로 갱신했다. 비활성 owner에 테스트 컴포넌트를 연결해 원본 행 숨김/전투 구독을 막고, 정리 전 임시 Canvas 참조를 분리한다. 이 메뉴의 시너지 색은 중립색이며, 직업색은 별도의 실제 행 복제 검사에서 검증했다. `portraits`는 실제 Portrait 오브젝트만 센다. 웨이브 추종 검증은 변경하지 않았다.

### 실제 검증

Unity 6000.3.22f1, 계정·RealSynergySync·전투 초기화·Play Mode 없이 임시 Additive Edit Mode UI에서 검사했다.

- 6직업의 목록/호버 Sprite 일치, 헤더 RGB와 실제 깃발 RGB 일치, 공통 금색 띠와 20개 표시 참조 정상.
- 1080p 기준 6직업 모두 600×744, 마지막 효과 아래 여백 28. 글자 넘침/겹침 없음. 0.8배에서는 너비 480, 아래 여백 22.4 유지.
- 조합 0개: 높이 376, 제목띠 숨김. 조합 4개: 높이 1032. 항목 재사용·잔여 항목 숨김 정상.
- 복제한 긴 단계/조합 문구: 너비 600 유지, 높이 744→920. 원본 SO는 그대로이며 아래 여백 28 유지.
- 현재 작은 Game View에서 실제 BuildTip의 화면 안 맞춤 통과. 활성 녹색, 아이콘 누락 시 숨김 및 복구 후 재표시 통과.
- 기존 검증 메뉴도 6직업·긴 조합(744→832)·보상 2~4행을 통과했다. 보상 계산/프리팹은 바꾸지 않았다.
- 완성 프리팹에 빌더를 두 번 재실행해 구조와 설정이 그대로임을 확인했다. 초기 실험의 파괴적 재적용 Undo에서 Unity UI 참조 복원 문제가 있어 최종 도구는 최초 변환만 허용한다. 최초 변환 전체 Undo/Redo는 최종 구조에서 재검사하지 않았다.
- 컴파일 완료 및 최종 Console Error 0개. 의도적인 아이콘 누락 검사의 경고 1건은 정상이다. 작업 시작 전에 있던 `RealSynergySync (auto)` 씬 종료 오류는 이번 변경으로 수정하거나 재현 검증하지 않았다.
- 원본 씬과 SO의 직렬화 상태는 검증 전후 동일하다. 컴파일 후 생긴 InGame 미저장 표시도 사용자 승인으로 임시 비교 사본을 만들어 저장 파일과 동일함을 확인했다. 원본을 덮어쓰지 않고 다시 열었으며, 임시 씬은 Unity 에셋 관리로 제거했다.

캡처/상세 결과: `Temp/SynergyPopupApply_20261012/SynergyLayout_ActualUI_1.png`~`3.png`, `ValidationResults.json`. 이 폴더에는 작업 전 백업도 있으며 Git에서 제외된다.

Play Mode 재확인: 실제 마우스 호버 진입/이탈, 배치 수 변경과 단계/조합 활성색 갱신, 낮은 해상도에서 가독성, 카드 보상 중 숨김/복귀. NullReferenceException/MissingReferenceException 및 Console 오류를 확인한다. LayerMask/Tag/Collider/Rigidbody/Animator/Input System/ProjectSettings/Packages 변경은 없다.

이번 추가 변경은 시너지 프리팹·표시 코드·Editor 도구·이 문서이며 새 스크립트 메타 2개는 Unity가 생성했다. InGame 파일은 작업 시작 시의 기존 아이콘 참조 6곳 변경 상태를 그대로 유지한다. 충돌 위험 파일은 `InGameSynergyHudBridge.cs`, `SynergyTooltipPanel_Heraldry.prefab`이며 커밋/push는 하지 않는다.

## 시너지 목록 아이콘·호버창 크기 조정 (2026-10-12)

현재 InGame의 `Synergy_0`~`Synergy_3/Icon`만 RectTransform 크기를 52×52에서 72.8×72.8로 1.4배 키웠다. pivot이 (0, 1)이므로 anchoredPosition도 (50, -28)에서 (39.6, -17.6)으로 보정하여 기존 중심을 유지한다. 행 크기·텍스트·구분선·Transform 배율은 바꾸지 않는다. 증강 목록에서도 쓰는 `SynergyTracker_InGame.prefab` 원본은 52×52 그대로다.

호버창 전체 표시 배율은 **InGame → UI_BattleScreens → InGameSynergyHudBridge → Tooltip Display Scale**에서 조정한다. 현재 씬은 0.7이며, 새 필드의 기본값은 1이므로 미설정 씬에는 기존 크기를 유지한다. 구형 설명창 경로에는 적용하지 않는다. 시너지 표시 프리팹 자체의 기준 폭 600과 수동 편집 내용은 그대로다.

`BuildTip`은 기존 화면 맞춤을 먼저 계산한 뒤 글자·아이콘·장식·테두리·여백을 0.7배로 다시 배치한다. 좁은 화면에서는 너비 제한도 같은 배율로 전달한다. Transform.localScale은 1을 유지하고 실제 sizeDelta를 줄이므로 기존 호버 위치와 화면 경계 보정이 표시 크기와 일치한다. 프리팹을 보는 Edit Mode의 기준 미리보기 크기와 실행 시 InGame의 표시 크기는 구분한다.

이번 후속 변경 파일은 `InGame.unity`, `InGameSynergyHudBridge.cs`, 이 문서다. 기존 미커밋 아이콘/프리팹 변경을 보존하고 공용 프리팹·이미지·다른 씬·ProjectSettings·Packages는 추가 변경하지 않는다. 실제 마우스 호버와 해상도별 가독성은 Play Mode에서 확인한다. 자동 커밋/push는 하지 않는다.

### 크기 조정 검증

Unity 6000.3.22f1 / 임시 Additive Edit Mode UI / 실제 Game View 640×480에서 6직업 모두 기존 약 368×460.5 → 257.6×321.1로 표시했다. 너비는 정확히 70%, 높이는 TMP 높이 반올림으로 약 69.74%다. 긴 단계·조합 설명도 화면 맞춤 이후 너비 70%·높이 약 70.17%를 유지했다. 가상 가용 너비 320→224 검사에서는 패널 너비 304→212.8로 정확히 70%, 높이 약 70.38%였다.

- 글꼴 크기·큰 아이콘·상단 장식·9분할 모서리 배율 정상, 글자 넘침 없음. 고정 1px 선과 작은 끝장식은 기존 굵기를 유지한다.
- 목록 4개 아이콘의 기존 중심 유지와 재활성화 후 크기 누적 없음, 구분선까지 6.6px 여유 확인. 증강도 사용하는 원본 프리팹 52px 유지.
- 표시 참조 20개 누락 없음, 호버 그래픽 raycastTarget false 및 Transform 배율 1 유지. sizeDelta와 실제 WorldCorners 크기 일치.
- 위치는 기존 UpdateTooltip의 경계 수식과 화면 좌표 실측으로 검사해 화면 안에 들어감을 확인했다. 실제 Mouse 호버 및 Play Mode는 실행하지 않았다.
- 원본 씬과 시너지 SO의 직렬화 상태는 검증 전후 동일하다. 컴파일 후의 미저장 표시도 승인된 임시 비교 사본과 저장 파일의 SHA256이 동일함을 확인해 원본을 다시 열었고, 비교 사본은 Unity에서 제거했다.
- 컴파일 완료 및 Console Error 0개. 기존 InGameCardSizing._rewardRestingYOffset 미사용 경고는 유지한다.

캡처와 상세 결과는 Git에서 제외되는 `Temp/SynergySizing_20261012/SynergySizing_ActualUI.png`, `ValidationResults.json`에 남겼다. 해당 캡처는 1920×1080 이미지 위에 실제 640×480 Game View에서 계산한 표시 크기로 배치한 비교 자료이며, 실제 Full HD Play Mode 캡처는 아니다. 작업 전 백업도 같은 폴더에 보존한다.
