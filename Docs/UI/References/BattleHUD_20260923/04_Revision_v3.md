# 전투 UI 04번 수정 시안 v3

- 결과: [04_Unified_GothicCommandPanel_v3.png](04_Unified_GothicCommandPanel_v3.png)
- 생성 방식: 내장 image_gen으로 v2를 편집하고 중앙 배경 높이만 한 번 보정.
- 원본 및 v2 보존. Unity 씬·프리팹·코드 변경 없음.

## 반영 내용

- 상단 `남은 시간` 문구 제거, 모래시계 및 `00:42` 유지.
- 전체 시너지 외곽 창·제목·공통 배경 제거. 개별 시너지 행 4개와 각각의 프레임·명칭·아이콘·`3 > 5` 유지.
- 전투 시작 마름모를 코스트 마름모 기준으로 크기·정렬 통일.
- 하단 배경을 양끝이 높고 중앙이 낮은 직선 절개형 U자 디자인으로 변경. 리롤·카드 자체 크기와 위치는 유지.
- 중앙 배경은 기존 전체 높이의 약 절반을 목표로 낮추고, 게임에 맞는 낡은 금속/석재, 붉은 홈과 아이보리 테두리 적용.
- 생성 이미지이므로 실제 UI 요소의 정확한 픽셀 크기·위치는 추후 구현 시 RectTransform 기준으로 맞춰야 함.

## 생성 프롬프트

```text
Use case: precise-object-edit
Asset type: refined full-screen 16:9 dark gothic pixel-art battle preparation UI mockup, single landscape image.
Input image 1 is the edit target, version 2. Preserve its style and nearly all content. Make ONLY the following targeted revisions.

A. TIMER: Delete the Korean words "남은 시간" from the top bar. Keep the hourglass and "00:42", neatly centered together in their existing section. Keep WAVE 3/10, LV.20 / XP, menu and restrained top bar design. No wall HP.

B. SYNERGY: Remove ONLY the LARGE enclosing outer synergy window: its shared outside border, decorative corners, overall dark backing, and its header "시너지" with header plaque. Do not remove or merge the FOUR individual horizontal row panels. Keep four separate floating diamond-icon + dark nameplate rows, all their own fine frames and the exact names "마법 결속", "사격 대형", "불굴의 뼈", "저주 의식", and "3 > 5" inside each row. Keep their purple / old gold / ivory / plum accent styles and white icons. No outer rectangular box around these four rows, no new title. Position the four rows evenly on the right so they remain above the battle-start control with a clear gap; using space freed by the removed header is fine. Do not add or change their game data.

C. MATCH DIAMOND CONTROL SIZES: lower-right crossed-swords "전투 시작" diamond must have the SAME outer width AND height as the existing lower-left flame "100" Cost diamond. Use the Cost diamond as the size reference, do not resize Cost. Both diamonds should have matched scale, padding and visual weight and be on the same horizontal baseline; fit swords and text comfortably inside without distortion. Leave reroll small and leave three card sizes unchanged. Avoid overlap with the lowest synergy row.

D. MOST IMPORTANT — REPLACE THE STRAIGHT BOTTOM BACKGROUND WITH A STEPPED U-SHAPED DOCK:
This is a SHAPE CHANGE TO THE BACKGROUND, NOT a move or shrink of cards/reroll. The current broad rectangular charcoal strip has a flat top at about y=67% of the screen. Remove that flat strip shape and its old straight full-width top border.
Create one connected dark charcoal/blackened-metal HUD foundation, anchored to the bottom, whose TOP CONTOUR forms a deep ANGULAR U.
- The left wing behind Cost only (screen x=0–16%) retains its original full background height: top at y=67%, bottom at y=100%.
- The right wing behind Battle Start only (x=84–100%) has the same high top at y=67%, bottom at y=100%.
- Immediately on the inward side of Cost, the top edge descends with a STRAIGHT near-vertical wall and small clipped/beveled corner to y=83%. Mirror this at the inward side of Battle Start.
- The middle connecting trough under REROLL AND ALL THREE CARDS (about x=18–82%) is only HALF the former background height: its top is y=83%, bottom y=100%. A long LOW horizontal connecting metal edge follows y=83% behind the foreground controls. No high backdrop or high straight border left behind reroll.
- Thus the U-shaped geometry is high on both ends, depressed and low across the very wide middle, with straight cut shoulders rather than round curves, a bowl, wavy scallops, or disconnected panels. The upper halves of cards and reroll now stand free against the original near-black battlefield background. DO NOT shrink or lower the cards/reroll to fit into the low trough.
- Substantial visual game-concept polish: dark iron/rough stone pixel texture, restrained oxblood red inlaid seam, fine worn ivory/antique-metal bevel along the ENTIRE U-shaped top contour, tiny rivets at the shoulder corners and a subtle small gothic diamond accent on the low central edge. Keep trim fine, muted and proportional. This must look like the same game's HUD, not a modern gray placeholder or a huge red decoration.
- The background is always BEHIND foreground Cost/reroll/cards/start; its border must NOT cross on top of card artwork/text.
- Do NOT leave ANY straight original high dock edge spanning the central card/reroll region at y=67%. The lowered center must be visibly open, black, uncluttered above y=83%.

Invariants: keep the 4x3 board, two rune slabs, three placeholder units and health strips, upper-left wave enemy preview panel, bottom card contents and legible names, black battlefield, oxblood/bone gothic pixel style, crisp restrained worn metal line art. No new objects, no new functions, no generic modern UI. One complete polished game screenshot, no before/after panels, no watermark, no text about the revision.
```

## 중앙 배경 높이 보정 프롬프트

```text
Use case: precise-object-edit.
Input image is the already approved-composition battle UI edit. Make ONE geometric correction ONLY to the bottom U-shaped BACKGROUND. All text, controls, board, top bar, 4 synergy rows, artwork and icon positions must remain precisely unchanged.
Currently the central low edge of the U-shaped dark gray dock sits at about y=90% of image height, which is too low. Raise that central background top edge to y=83% of image height (approximately y=781 on this 941px-tall image), across the entire wide middle between the Cost and Battle Start wings. This makes the central background 17% screen-height, roughly HALF of the side wings' 33% screen-height. The straight near-vertical U shoulders therefore become shorter. Keep the far-left and far-right high wing edges at approximately y=67%. Keep the original fine ivory/red metal trim and small corner rivets and the same graphite stone/iron material. The narrow raised trim passes BEHIND the reroll and the cards and must never draw across foreground icons, text or artwork. The central bottom background should fill all space from the new y=83% contour to the bottom; area above remains the black battlefield. Retain the clear angular high-low-high U-shaped silhouette; do not change it to a full-width tall rectangle. Left and right high wing heights should match. Do not move, resize, replace, recolor or edit ANY other content. No "남은 시간" text, no overall synergy container or heading, no wall HP. Output one complete image with the same aspect ratio and style.
```

