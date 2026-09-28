# Battle Noble Bottom Panel — 생성 기록

- 생성 방식: 내장 image_gen 이미지 편집.
- 승인 레퍼런스: `Docs/UI/References/BattleBottomPanel_20260923/AngledContour_Noble_v2.png`.
- 사용자 추가 수정: 패널 내부의 검은 띠/벨벳 이중 구획을 제거하고 전체를 와인색 벨벳으로 통일.
- 원본: `Panel_TwoTone_Original.png` (중간안 보존), `Panel_Velvet_Original.png` (최종 생성 원본).
- 후처리: `Tools/Art/PrepareBattleNoblePanel.ps1`. 외부 배경 투명화, Body/Frame 분리, 중심과 표시 크기 정렬만 수행. 재도색 없음.
- 최종 Unity 리소스: `Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Body.png`, `Panel_Frame.png`.
- 원본은 글자·숫자·버튼·아이콘이 없는 아트다. 기존 UI와 동적 Text를 이미지로 대체하지 않는다.

## 패널 추출/생성 프롬프트

```text
Use case: precise-object-edit / background-extraction.
Input image: approved game UI mockup. Extract and complete ONLY its bottom aristocratic background panel as a production sprite on a GENUINELY TRANSPARENT background. Not a mockup anymore.
REMOVE ALL foreground buttons, cards, Cost diamond, fire icon, reroll diamond, swords/start diamond, price plaque, all icons, all text/numbers, upper HUD, grid, enemy panel, black backdrop. Fill the places previously occluded by these controls with continuous matching dark burgundy damask and black lacquer. Nothing else may remain.
PRESERVE THE APPROVED PANEL ART: black enamel/ebony lacquer upper inset, very dark wine-red velvet subtle damask lower inset, fine champagne-antique-gold continuous double beveled trim and thin crimson accent, delicate restrained golden filigree and tiny gold diamond corner ornaments. No stone, rubble, soil or rough terrain textures. Sharp pixel-art game asset, front-on flat orthographic.
KEEP THE EXACT DISTINCTIVE OUTLINE visible at the very bottom of the input:
Left outer side rises diagonally to a large triangular primary peak, then descends to a valley; a smaller extra peak over the reroll rises next, then descends to the low straight long central bridge; at the right the low bridge rises to ONE large primary triangular peak, then descends to the right outer edge. Lower edge is one straight horizontal line joining everything. All is ONE continuous filled panel, including the central bridge previously hidden by the three cards.
Large peaks around 8.7% and 91.3% of width must be horizontal mirrors and EXACTLY equal height; outer left/right corners and ornaments match. Left smaller peak at20.6% is intentional, do not duplicate it on the right.
Within the PANEL'S tight rectangular bounds, use this top contour (x%, y% from top): (0,35), (8.7,0), (16.8,35), (20.6,19), (28,58), (78.5,58), (91.3,0), (100,35). Bottom edge at y100%. Fine corner diamond ornaments can project a little. Panel's overall aspect ratio approximately 4.55:1, very wide and shallow; only side wings rise tall, central bridge has about42% of full panel height. Do not turn it into a simple rectangle or symmetrical U trough.
Position this complete long panel centered horizontally in a wide canvas with transparent empty space above and below, no cropping. The ENTIRE outside of the contour is transparent alpha, never black or checkerboard. The entire interior is dark opaque material; no holes. No words, no numbers, no cards, no controls, no large emblems. One sprite only.
```

## 벨벳 전체 채움 수정 프롬프트

```text
Use case: precise-object-edit. Input is the production bottom HUD panel to edit. Make ONE MATERIAL CHANGE ONLY:
Replace ALL the broad BLACK ENAMEL/LACQUER INTERIOR BANDS along the slanted upper edges of BOTH large peaks and the smaller left peak with exactly the SAME DARK BURGUNDY VELVET DAMASK found in the lower interior. The interior must now be ONE CONTINUOUS UNIFORM DARK WINE-RED VELVET SURFACE extending all the way up to the thin gold/crimson border. No two-tone split, no black roof-like band, no inset black area, no internal diagonal seam, no separate black zone, no marble/stone texture. The damask pattern continues naturally across where the black bands used to be. Keep the velvet dark and dignified, not bright red.
PRESERVE the exact panel silhouette, canvas and positions: left large triangular peak, left smaller reroll peak, low long straight central bridge, right single large triangular peak, equal-height left/right main peaks and aligned outer corners. Preserve all thin champagne-antique-gold border bevels, tiny corner diamonds, thin crimson border hairline and delicate golden filigree ornaments exactly. A hairline dark outline around the metal is okay, but no broad black fill inside the panel.
Only the panel, with no text, numbers, icons, cards or buttons. The outside of the panel contour must be genuinely TRANSPARENT ALPHA, not a gray checkerboard painted into the image. Keep the artwork fully opaque inside, crisp pixel-art style, no shadows outside. Deliver one full panel asset at the same aspect ratio with no cropping.
```

