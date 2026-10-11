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
