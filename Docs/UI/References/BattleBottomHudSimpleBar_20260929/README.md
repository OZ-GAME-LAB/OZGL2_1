# 전투 하단 HUD 심플 일자형 레퍼런스 4종

## 목적

- 기존 하단 UI의 장식 밀도와 화면 점유율을 낮춘다.
- 배경을 높이가 일정한 하나의 연속 직선 바 형태로 제한한다.
- 기존 정보는 유지한다.
  - 불꽃 자원 `100`
  - 리롤
  - 가넷 화폐와 비용 `100`
  - 중앙 카드 손패 5장
  - 교차검과 `전투 시작`

이번 파일은 디자인 비교용 합성 시안이며 Unity Runtime Sprite나 최종 Text 리소스가 아니다. Scene/Prefab에는 연결하지 않았다.

## 시안 비교

| 번호 | 파일 | 특징 | 추천 용도 |
| --- | --- | --- | --- |
| 01 | `01_ObsidianFlatBar.png` | 무광 흑요석 평판, 얇은 상아색 테두리, 붉은 전투 시작 셀 | 가장 무난한 기본 구현 |
| 02 | `02_GarnetKeylineRail.png` | 가장 얇은 바, 가넷 상단선과 최소 구분점 | 전장 가시성과 심플함 우선 |
| 03 | `03_RecessedCardDock.png` | 중앙 카드 홈과 명확한 기능 구획 | 카드 정렬·클릭 영역 명료화 |
| 04 | `04_TwoToneCommandStrip.png` | 얇은 가넷 상단 띠와 흑요석 하단 면 | 로비의 붉은 포인트를 조금 더 유지 |
| 05 | `05_ThroneCommandRail_BlackWhiteBar.png` | 01 왕좌 지휘 레일 구성 뒤에 검정 단색·흰 테두리 바를 합성 | 기존 버튼·카드 디자인을 유지하는 절충안 |

추천 순서는 **02 → 01 → 03 → 04**다. 실제 제작 난이도만 보면 01과 03이 9-slice 및 기능 영역 분리에 유리하다.

## 05 합성안

- 기준 UI: 이전 레퍼런스 `01_ThroneCommandRail.png`
- 신규 바: 완전 불투명 near-black `#080808`
- 외곽선: warm white `#F2EFE7`, 한 줄만 사용
- 레이어: 전장 배경 → 신규 직선 바 → 기존 HUD 전체
- 기존 자원·리롤·카드·전투 시작의 위치와 디자인은 유지
- 이미지 2의 붉은 상단 띠, 금색 선, 내부 구획 및 장식은 사용하지 않음

디자인 비교용 합성이므로 실제 적용 시에는 검정 Body와 흰색 9-slice Frame을 별도 Image로 분리하고 두 Image의 `Raycast Target`을 끄는 편이 안전하다.

## 프로젝트 내 구현 참고 자산

- 일자형 구도: `Docs/UI/References/BattleHUD_20260923/03_Unified_SlimCommandBar.png`
- 단순 9-slice 프레임: `Assets/06.UI/BattleMutedPreview/Sprites/Frame_Hud.png`
- 로비 검철·은색 레일: `Assets/06.UI/LobbyMutedPreview/LevelHud_v1/Styles/LevelFrame.asset`
- 최소 구분선: `Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/RecordHover/Sprites/Divider.png`
- 로비 전체 색감: `Assets/06.UI/LobbyMutedPreview/Source/Lobby_NoCurrency_Reference.png`

실제 구현에서는 이미지 안의 글자와 숫자를 제거하고 기존 TMP를 사용한다. 카드 Sprite와 카드 데이터도 현재 구현을 그대로 유지한다.

## 생성 방식

- OpenAI 내장 `image_gen` 사용
- 사용자 첨부 하단 HUD: 표시 정보 기준
- 로비 UI 프레임: 픽셀 크기·색상·재질 기준
- 현재 전투 화면: 손패 크기와 안전영역 기준
- 각 시안을 독립된 프롬프트로 생성

## 공통 최종 프롬프트

```text
Use case: ui-mockup
Asset type: comparison-ready minimal game bottom-HUD reference, one design only, not a contact sheet
Input images: Image 1 is only an information/layout reference for the current lower battle HUD. Image 2 and Image 3 are style/material references for the lobby's restrained pixel-Gothic rails, dark panels, ivory lines, and muted garnet accents; IGNORE their green chroma background. Image 4 is only a screen-safe-area and card-scale reference. Generate a NEW design.
Primary request: create a low-detail, simple, perfectly straight horizontal bottom HUD background in the project's lobby pixel-Gothic style.
Mandatory silhouette: ONE continuous rectangular bar of constant height spanning about 94 percent of the screen width at the very bottom. Its top edge and bottom edge must be uninterrupted straight horizontal lines. Tiny 45-degree cuts are allowed only at the two outermost corners. No raised ends, no U shape, no peaks, no wings, no curved center, no detached modules.
Composition/framing: wide 16:9 front-on orthographic UI mockup. The upper 76 percent is a plain dim neutral battlefield placeholder with no characters and no other UI. The lower 24 percent shows the entire bar and five cards, with equal side margins and no cropped elements.
Information that MUST remain visible: on the left, warm-white flame icon with exact number "100"; separate warm-white circular-arrows reroll icon; small garnet currency gem with exact number "100". In the center, five narrow existing-style Gothic cards ordered left-to-right, slightly overlapping in a shallow fan and rising above the straight bar, leaving hover-lift space. On the right, warm-white crossed-swords icon with exact Korean label "전투 시작".
Visual style: crisp deliberate 2D pixel art, consistent pixel scale, matte obsidian/charcoal base, restrained muted garnet, thin warm bone-ivory metal edges. Flat practical Unity UI with separable regions and large readable hit areas.
Typography: readable Korean pixel Gothic style. Render only the required label and numbers.
STRICTLY AVOID: towers, castle architecture, walls, battlements, wings, altars, books, chains, spikes, banners, columns, circular dais, shields, oversized diamonds, crests, heraldic frames, filigree, carved runes, glowing magic, scene decoration, extra labels, logos, watermark, photorealism, 3D perspective, gradients, top HUD, synergy tracker. Decoration density must be very low.
```

## 변형 프롬프트

```text
01 — 옵시디언 플랫 바
A single matte-obsidian bar with only one thin warm-ivory border at top and bottom, two subtle vertical separators, and extremely small square end rivets. The left controls occupy one quiet flat cell. The center behind the cards is uninterrupted black. The right battle action is a simple full-height muted-garnet rectangular cell, flush with the same straight bar, no protrusions. This is the safest and simplest 9-slice concept.

02 — 가넷 키라인 레일
Make the straight bar thinner and visually lighter, using a near-black translucent-looking fill, one single-pixel muted-garnet line along the perfectly straight top edge, and one faint ivory bottom line. No internal boxes. Separate flame 100, reroll, gem 100, cards, and battle action only through generous spacing and three tiny red square separator dots. Indicate the right battle action only with a short garnet underline beneath "전투 시작", not a separate button shape. Lowest possible visual noise.

03 — 리세스드 카드 도크
One continuous straight charcoal bar. The central 48 percent is one slightly darker rectangular recessed card dock, still flush within the same constant-height outer rectangle. Add five very shallow straight card pockets indicated only by thin vertical grooves; the cards rise from them. Left controls are flat inset symbols. Right battle action is a restrained rectangular zone defined only by a thin muted-garnet outline. Thin ivory outer edge, no ornament.

04 — 투톤 커맨드 스트립
One continuous straight bar of constant height with a narrow muted dark-garnet upper band taking 28 percent of the bar height and a larger matte-obsidian lower band taking 72 percent. The cards overlap the upper band and rise upward. Use sparse black vertical dividers, warm-ivory icons and text. The right battle action uses only bolder text and crossed swords, with no additional frame or protrusion. Clean color blocking, no ornaments.

05 — 왕좌 지휘 레일 + 흑백 바 합성
Edit only the first attached image. Add exactly one long flat background bar behind the existing bottom HUD, with bounds near x=28..1644 and y=734..892 on the 1672x941 canvas. Use one fully opaque #080808 fill and one crisp 3–4px #F2EFE7 outline. Keep every existing HUD element above it and unchanged; cards intentionally occlude the bar's top edge. Use the second image only for its black/white treatment. Do not copy its red strip, texture, corner ornaments, or layout. Do not copy the third image's red/gold lines or thin profile. No other edits.
```

## 적용 전 확인

- 카드 호버 1.2배 확대 시 바 또는 Mask에 잘리지 않는지
- 좌측 정보 영역과 첫 카드의 클릭 영역이 겹치지 않는지
- 전투 시작 영역의 최소 클릭 크기가 확보되는지
- 초과 카드 스크롤 영역이 바의 중앙 구간과 일치하는지
- 16:9 외 화면비에서 좌우 여백과 Safe Area가 유지되는지
