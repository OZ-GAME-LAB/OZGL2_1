# 스킬 아이콘 및 슬롯 색상 규칙

## 목적과 범위

스킬 아이콘의 실루엣은 흰색으로 읽히게 유지하고, 외곽선과 옅은 슬롯 배경만으로 딜/버프/디버프를 구분한다. 저채도 다크판타지의 고풍스러운 금속 장식과 픽셀 느낌을 유지한다.

현재 적용 대상은 `UI_Lobby_MutedPreview`에서 사용하는 `Canvas_SkillSettings.prefab`의 보유 목록, 장착 슬롯 3개, 상세 아이콘이다. 특성/메뉴/다른 Scene에는 자동 적용하지 않는다. 실제 전투 스킬 효과·수치·해금·게임 저장과는 별도의 UI 표시 규칙이다.

## 공통 아트 규칙

| 요소 | 색상 / 규칙 |
| --- | --- |
| 모든 아이콘 본체 | 따뜻한 흰색 `#F8F2EB`, 불투명도 100% |
| 딜 외곽선 | 짙은 적색 `#A5403F` |
| 버프 외곽선 | 고금색 `#BB994B` |
| 디버프 외곽선 | 보라색 `#83519A` |
| 슬롯 내부 | 해당 분류색, 기본 불투명도 14%, 가장자리로 갈수록 투명 |
| 장착 슬롯 프레임 | 딜 짙은 적색 / 버프 고금색 / 디버프 보라색 전용 Sprite |
| 빈 장착 슬롯 프레임 | 기존 형태를 유지한 무채색 회색, 내부 분류색 없음 |
| 목록 미선택 호버 프레임 | 기존 주황/황동색 상태 유지 |
| 목록 선택 프레임 | 기존 안쪽으로 두꺼운 금색 상태 유지 |

- 분류색은 기존 `Equip_Red.png`, `Card_Selected_Gold.png`, `Equip_Purple.png`에서 채취했다.
- 아이콘 본체 전체를 분류색으로 칠하지 않는다. 텍스트나 배경 풍경도 분류색으로 물들이지 않는다.
- **분류 표시와 선택 상태를 분리한다.** 버프의 금색 아이콘 외곽선은 분류이고, 카드의 두꺼운 금색 프레임은 선택 상태이다.
- 아이콘 외곽선/슬롯 내부/장착 프레임 모두 `IsArcane`이나 아이콘의 원래 색이 아닌 `Category`로 결정한다. 오른쪽 슬롯만 보라색으로 고정하지 않는다.
- 네온색, 강한 발광, 과한 블러, 고채도 원색은 사용하지 않는다.
- 이미지에 카테고리/버튼 문구를 넣지 않는다. 글자는 별도의 TMP로 표시한다.
- 아이콘 원본 PNG는 덮어쓰지 않는다. 아이콘/슬롯 내부는 공통 UI Shader로 표시 색을 변경하고, 장착 프레임은 별도 생성한 `Equip_{Damage,Buff,Debuff}_Muted.png`로 교체한다.

## Unity 설정

설정 폴더: `Assets/06.UI/LobbyMutedPreview/Skills_v1/Styles`

- `SkillCategoryStyle.asset`: `UISkillCategoryStyleSO`. 분류별 Material, 장착 프레임 Sprite 3개, 슬롯 Material, `Slot Opacity`를 공유한다.
- `SkillIcon_Damage.mat` / `SkillIcon_Buff.mat` / `SkillIcon_Debuff.mat`: `Category Outline`으로 분류색을 조절한다. `Ivory White Face`는 세 Material 모두 `#F8F2EB`로 유지한다.
- `SkillSlot_Tint.mat`: 같은 Shader의 `Soft Slot Tint Only` 모드. 도형 안쪽에서만 색이 보이고 가장자리로 사라진다.
- `SkillCategoryUI.shader`: 원본 실루엣을 흰색으로 표시하고 분류색 외곽선을 만든다. 팔레트는 sRGB 기준이며 Linear 렌더링에서 변환한다.
- 외곽선 기본 폭: 원본 텍스처 기준 `Outline Width = 2` texel. 화면 픽셀 수가 아니라 원본 해상도 기준이다.
- 어두운 배경 잔무늬 제거: `Ignore Dark Source Noise = 0.15`. 아이콘 핵심 선이 사라지는 소스라면 값을 낮추고 실제 크기로 확인한다.
- 목록/장착 슬롯: 각 `SkillCard_XX` 또는 `EquippedSlot_X`의 `CategorySlotTint`가 `Icon` 아래에 배치된다. 상세창은 `DetailIconFrame/CategorySlotTint`이다.
- `CategorySlotTint`와 `Icon`의 `Raycast Target`은 꺼 둔다. 프레임 버튼이 입력을 처리한다.
- 검은 전체 배경 60%와 하단 그라데이션은 이 분류색 설정과 별개이며 그대로 유지한다.

## 새 스킬 적용 절차

1. 현재 미리보기에서는 `SkillPreviewCatalog.asset`의 해당 Entry에 `Category`와 투명 아이콘 Sprite를 설정한다.
2. `UISkillLoadoutPreview`는 목록·상세 선택·장착 갱신 시 `UISkillCategoryStyleSO`의 동일 분류 Material/슬롯 색을 적용한다. Material을 런타임에 복제하거나 공용 Material 값을 변경하지 않는다.
3. 새 UI 카드 오브젝트를 늘리는 경우 `_cards`, `_cardIcons`, `_cardSlotTints`와 클릭 인덱스도 연결해야 한다. 카탈로그 항목 추가만으로 카드가 자동 생성되는 구조는 아니다.
4. 기존 카드 연결 후 `Tools/OZGL2/Lobby/Apply Skill Category Visual Style`로 슬롯 색 레이어와 표시 참조를 연결한다. 대상 Prefab Mode에서 Undo를 지원한다. 새 Material/SO 에셋 생성 자체는 Undo 삭제 대상이 아니다.
5. 기존 화면의 분류색 농도만 바꾸려면 Play Mode를 종료하고 `SkillCategoryStyle.asset` 또는 해당 Material을 수정한다. 적용 메뉴 재실행은 이미 존재하는 공유 설정을 덮어쓰지 않는다.
6. 다른 스킬 화면에서는 같은 설정 SO의 `GetIconMaterial(category)`, `GetSlotColor(category)`, `SlotTintMaterial`, `GetEquippedFrame(category)`를 각 Image에 연결한다. 미해금/선택/장착/저장 데이터는 이 SO에 넣지 않는다.

## 장착 프레임 후속 규칙

- `Sprites/Equip_Damage_Muted.png`: 저채도 적갈색 금속. `Equip_Buff_Muted.png`: 고금색. `Equip_Debuff_Muted.png`: 어두운 보라 금속.
- 원본 `Equip_Red.png` / `Equip_Purple.png`는 보존한다. 빈 슬롯은 기존 `Red Frame` 형태에 `SkillFrame_Empty.mat`을 적용해 무채색으로 표시한다. 별도 PNG 변형은 만들지 않으며 명암/투명도/정렬을 보존한다.
- `SkillCategoryStyle.asset`의 `Empty Frame Material`이 무채색 표시 설정이다. `SKILL_EMPTY_FRAME` 모드는 Button tint를 포함해 RGB 채도를 제거한다. 장착하면 해당 Material을 해제해 분류 프레임의 원래 색으로 돌아간다. 호버 중에도 빈 프레임에 분류색이 묻지 않는다.
- 세 슬롯은 위치와 관계없이 같은 분류 규칙을 적용한다. 분류가 다른 스킬로 교체하면 즉시 새 Sprite로 갱신한다.
- 슬롯 루트는 사용자가 지정한 크기와 위치를 유지하고 입력과 아이콘의 기준으로 사용한다. 프레임만 `EquippedSlot_X/FrameArt` 자식 Image에 표시한다. 렌더 순서는 FrameArt → CategorySlotTint → Icon이다.
- `SkillCategoryStyle.asset`의 분류별 `Frame Layout`은 `(가로 배율, 세로 배율, 가로 위치 비율, 세로 위치 비율)`이다. `LobbySkillFrameAlignmentBuilder`가 256px 원본의 장식 중심을 측정한 기준으로 연결한다. 배율과 이동량은 현재 슬롯 크기에 비례하므로 슬롯을 리사이즈해도 아이콘/클릭 영역을 움직이지 않는다.
- 버프/디버프는 회색 기본 프레임(`Equip_Red`)의 좌/우/상/하 보석 **내부 중심 4개**에 맞춘다. Pivot은 모두 중앙이지만 이미지 속 도형은 중앙과 다를 수 있다. 전체 알파 영역, 반짝임, 임의의 `(128,128)` 중심에 맞추지 않는다. 공격 프레임은 승인된 기존 보정을 유지한다.
- 보정 기준은 alpha > 127, 명도 `0.2126R + 0.7152G + 0.0722B >= 120`인 보석 내부의 4방향 연결 성분이다. 텍셀 중심 좌표(index + 0.5)를 측정하고 네 점의 X/Y를 각각 최소제곱 정렬했다. 버프 Layout은 `(0.998246923, 1.006273469, -0.003746004, -0.000718412)`, 디버프는 `(0.976629704, 0.977774973, -0.011985075, 0.010483412)`이다.
- 보정만 재적용할 때는 Edit Mode의 `Tools/OZGL2/Lobby/Align Buff And Debuff Frames (Style Only)`를 사용한다. Undo 지원, 위 SO만 저장하며 Scene/Prefab/원본 PNG와 공격 프레임/색/불투명도는 변경하지 않는다. 화면은 다음 스킬창 열기 또는 장착 갱신 때 반영된다.
- FrameArt는 Preserve Aspect와 Raycast Target을 끈다. 크기/위치는 스킬 갱신 시 공유 레이아웃에서 적용되므로 지속적인 조정은 개별 FrameArt가 아니라 공유 설정에서 한다. 빈 슬롯은 기존 원본 Sprite와 기본 배치에 무채색 Material을 적용한다.
- 프레임 자체에 아이콘/문구를 합치지 않는다. 기존 흰색 아이콘과 옅은 내부색은 별도 레이어이다.
- `IsArcane` 필드는 기존 카탈로그 호환을 위해 남아 있지만 프레임 표시에는 사용하지 않는다.
- 생성 원본/실제 프롬프트/후처리 기록: `Tools/Art/Sources/SkillEquippedFrames_v4/GENERATION.md`.

### 실제 스킬 시스템 연동 시

- 현재 카탈로그는 12개 아트 미리보기용이다. 이번 작업은 첨부 표의 기존 6개 스킬 구성이나 수치를 대체/적용하지 않는다.
- 실제 `SkillData`의 분류를 UI 분류 `DAMAGE/BUFF/DEBUFF`로 명시적으로 매핑한다. 서로 다른 enum을 숫자로 단순 캐스팅하지 않는다.
- `Ultimate`나 복합 효과 스킬은 임의로 색을 추정하지 않는다. 대표 분류를 기획에서 정한 뒤 매핑한다.
- 실제 저장/전투 장착 데이터와의 연결은 별도 구현 사항이다.

## 새 아이콘 원본 규격

- 투명 배경, 명확한 밝은 실루엣, 아이콘 내부 구멍도 실제 투명도 사용. 검은 사각 배경이나 체크무늬를 구워 넣지 않는다.
- 권장: 기존 아이콘과 비슷한 해상도, 테두리 확장을 위한 사방 6px 이상의 투명 여백.
- Import: Sprite / Single, Full Rect, Point, Compression None, Mip Maps Off, Wrap Clamp.
- 현재 Shader는 독립 텍스처의 이웃 픽셀을 샘플링한다. 이 아이콘들을 SpriteAtlas에 패킹하지 않는다. Atlas 지원은 Sprite UV 경계 처리를 별도 구현한 후 사용한다.
- 픽셀 외곽선이 잘리거나 얇은 핵심 선이 사라지지 않는지 목록 크기와 상세 크기 양쪽에서 확인한다.

## 확인 항목

- 전체/딜/버프/디버프 전환, 각 분류의 상세 아이콘과 슬롯 색 일치
- 장착·해제·빈 슬롯·다른 분류 장착 시 이전 색 잔류 없음
- 호버 주황, 클릭 선택 금색, 재클릭/마우스 이탈 시 선택 유지
- 기존 검은 배경과 그라데이션 유지, UI 입력 가로채기 없음
- 원본 Sprite/카탈로그 수치/실제 게임 저장 불변
- Unity Console 및 Shader 컴파일 오류 없음
- 비 16:9, UI 마스크 내부, 다른 그래픽 API와 플레이어 빌드는 별도 검증 필요

검증 메뉴: `Tools/OZGL2/Lobby/Validate Skill Settings Preview`, `Tools/OZGL2/Lobby/Validate Skill Category Visual Style`.

### 적용 검증 기록 — 2026-09-21

- Unity Play Mode: 기존 스킬 UI 42개 + 분류색 76개 검사 통과. 카테고리 탭 Raycast 정상.
- 목록 12개/장착 3개/상세 1개에 색 레이어 연결. 빈 슬롯은 색 레이어를 숨긴다.
- 실제 Game View에서 흰색 본체/분류 외곽선/슬롯 색/기존 호버·선택 표시를 확인했다.
- Console 오류·경고 0개, Missing Script/끊어진 Prefab 참조 0개, Shader 오류 없음.
- Scene 파일과 원본 PNG, 미리보기 카탈로그, 실제 저장 데이터는 그대로 유지했다.
- 결과 화면: `Tools/Art/Previews/SkillSettings_v3_CategoryColors.png`.
- 공유용 규칙은 이 문서와 스킬 README에 기록한다. 루트 `AGENTS.md`에도 참조를 추가했지만 해당 파일은 기존 `.git/info/exclude` 설정에 따라 로컬 전용이다. 제외 설정은 변경하지 않았다.

### 장착 프레임 미세 정렬 검증 — 2026-09-21

- 네 Sprite의 Pivot은 모두 `(128,128)`로 동일했다. 차이는 이미지 내부 보석 중심과 기존 버프/디버프 보정값에 있었다.
- 현재 321.08 크기 슬롯에서 네 보석 중심의 RMS 오차는 버프 약 3.56 → 0.41, 디버프 약 2.75 → 0.07 UI 단위로 감소했다. 화면 픽셀 정렬/Point 샘플링에 따른 차이는 별도이다.
- `LobbySkillSaveFlowValidation.ValidateAlignmentAndHover()`의 Play Mode 150개 검사 통과: 3분류 × 3슬롯, 네 보석의 원본 256 기준 오차 ≤ 1px, 슬롯/아이콘 Transform 불변, 확인창 호버, 검증 후 저장 구성 복원.
- Game View에서 빈 회색 슬롯과 딜/버프/디버프 장착 화면을 비교했다. 컴파일 및 Console 오류·경고 0개.
- 이번 수정은 공유 SO의 버프/디버프 Layout과 Editor 보정/검증 코드에 한정했다. Scene/Prefab 파일 해시는 작업 전후 동일하며 원본 PNG, 공격 보정, 슬롯 농도, 실제 저장 데이터는 보존했다.
- Inspector 추가 연결은 없다. 이후 원본 프레임 아트를 교체한다면 이 문서의 측정 기준과 검증용 보석 좌표도 함께 갱신한다.
