# Battle Muted Preview — 생성 원본 기록

- 생성 도구: 내장 image_gen
- 참고: Docs/UI/References/BattleHUD_20260923/04_Unified_GothicCommandPanel_v3.png
- 날짜: 2026-09-23
- 원본은 보존하며, 승인된 투명 배경 정리·분할·중심/여백 정렬만 후처리했다.
- 원본 출력에 체크무늬가 포함되어 있어 PrepareBattleMutedSprites.ps1로 실제 알파를 적용했다.
- Experience_Fill/Track은 새 생성물이 아니라 기존 로비 리소스의 여백만 제거한 파생본이다.

## Chrome_Original.png

```text
Use case: stylized-concept. Asset type: production Unity UI sprite sheet, not a screen mockup. Input image is STYLE REFERENCE ONLY, do not reproduce full screen. Dark gothic pixel art, restrained aged ivory metal edges, chipped pixel texture, tiny muted oxblood rivets, perfectly symmetric and precise geometry. Flat orthographic front view. Sharp deliberate pixel clusters. No letters, no numbers, no captions, no labels, no shadows cast outside objects, no soft glow, no checkerboard painted into image. Background must be REAL alpha transparency. Each object completely isolated, uncropped, generous transparent gutters. Nothing from reference's battle tiles, units, cards or U-shaped backdrop.
Make ONE 1536x1536 sprite sheet of FOUR isolated UI FRAME assets arranged ONE PER ROW in four equal 1536x384 cells. Each asset centered in its cell.
Row1: long thin continuous horizontal status bar frame: rectangular ivory thin weathered metal border, minute clipped corners, NO diamonds, no side finials, no central symbols, hollow transparent interior. Shape width1400 height120 centered at y192.
Row2: wave enemy preview panel frame: rectangular ivory/oxblood restrained gothic frame, subtly clipped corners, tiny red diamond corner details, hollow transparent interior. Width1100 height280 centered at y576. No internal dividers or header text.
Row3: individual synergy nameplate frame: thin aged ivory rectangular border, no outside ornament, hollow transparent interior. Width760 height220 centered at y960. Do not attach an icon or diamond.
Row4: reroll cost small rectangular plaque frame: thin ivory/oxblood border with tiny angular corner metal, hollow transparent interior. Width600 height170 centered at y1344.
Only these four outlined frames; interiors also truly transparent so Unity can layer a separate black body behind them. No black rectangle around sheet, no visible grid.
```

## Shapes_Original.png

```text
Use case: stylized-concept. Asset type: production Unity UI sprite sheet, not a screen mockup. Input image is STYLE REFERENCE ONLY, do not reproduce full screen. Dark gothic pixel art, restrained aged ivory metal edges, chipped pixel texture, tiny muted oxblood rivets, perfectly symmetric and precise geometry. Flat orthographic front view. Sharp deliberate pixel clusters. No letters, no numbers, no captions, no labels, no shadows cast outside objects, no soft glow, no checkerboard painted into image. Background must be REAL alpha transparency. Each object completely isolated, uncropped, generous transparent gutters. Nothing from reference's battle tiles, units, cards or U-shaped backdrop.
Make ONE 1024x1024 sheet with FOUR separate UI components in a strict 2x2 equal512px grid, no grid lines. Each centered with at least64px gutter.
TOP LEFT: robust oxblood-red double-rail diamond frame for cost and start button, 380x380 visualbounds, hollow transparent center, elegant chipped red metal, small pointed accents at four diamond tips; EXACT bilateral and vertical symmetry, no icon, no text. This matches reference's red Cost diamond.
TOP RIGHT: thin ivory double-line diamond frame with tiny ivory diamond rivets at four cardinal tips, 380x380 visualbounds, hollow transparent center. Neutral white frame suitable for color tinting in Unity for synergy/reroll. No purple already baked in. No icon.
BOTTOM LEFT: one small solid dark crimson diamond token, 120x120 bounds, tiny ivory outer hairline and black inset separating outer edge. Centered. No lettering.
BOTTOM RIGHT: one minimal vertical pale-metal divider stroke, 12px wide by240px tall, tiny tapered pointed tips, no cross, no diamonds, no filigree. Centered.
Exactly four objects, no extra marks. All space outside and inside hollow frames alpha transparent.
```

## Icons_Original.png

```text
Use case: stylized-concept. Asset type: production Unity UI sprite sheet, not a screen mockup. Input image is STYLE REFERENCE ONLY, do not reproduce full screen. Dark gothic pixel art, restrained aged ivory metal edges, chipped pixel texture, tiny muted oxblood rivets, perfectly symmetric and precise geometry. Flat orthographic front view. Sharp deliberate pixel clusters. No letters, no numbers, no captions, no labels, no shadows cast outside objects, no soft glow, no checkerboard painted into image. Background must be REAL alpha transparency. Each object completely isolated, uncropped, generous transparent gutters. Nothing from reference's battle tiles, units, cards or U-shaped backdrop.
Make ONE 1536x1536 sprite sheet, STRICT 4 columns by4 rows of square384px cells, no grid drawn. Thirteen stand-alone icons, each centered in its assigned cell, white/bone silhouette with minimal medium gray internal cuts, NO attached frames, no disks, no backplates, no colored outlines. Uniform apparent scale about170px to240px bounds with at least64px padding.
Reading row-major:
row1 col1: front-facing human skull emblem; col2: symmetric hourglass; col3: three simple horizontal menu strokes; col4: stylized white flame.
row2 col1: two circular reroll arrows forming a ring; col2: crossed swords; col3: elegant vertical arcane staff with small star-ring head; col4: bow and arrow.
row3 col1: sturdy shield containing rib-bone motif; col2: cursed eye with diamond pupil; col3: full-body sword-and-shield helmeted human knight silhouette; col4: full-body human archer silhouette aiming bow.
row4 col1: full-body elite human knight carrying axe with small angular helmet horns; col2 EMPTY TRANSPARENT; col3 EMPTY TRANSPARENT; col4 EMPTY TRANSPARENT.
Clearly distinguish the THREE human enemy silhouettes from the four abstract synergy icons. Keep icon sizes consistent with purpose; clean pixel silhouettes and transparent cutouts, no text or numbers anywhere.
```


