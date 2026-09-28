# Gothic Metal 레벨 HUD 생성 기록

- 모드: built-in image_gen. PNG는 생성 결과를 무수정 복사하고 Unity Sprite Rect로 투명 여백만 제외한다.
- 승인 디자인: 01 GOTHIC METAL.
- 디자인 참조: C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-ee52f545-02e7-4172-9e61-9ac054edc754.png
- 최종 저장: Assets/06.UI/LobbyMutedPreview/LevelHud_v1/Sprites/LevelFrame.png, LevelTrack.png, LevelFill.png, LevelTick.png, LevelFillCap.png
- Unity 표시용 Sprite: 같은 LevelHud_v1/Styles/의 동명 .asset. 원본 PNG/.meta 직접 편집 없음.
- 첫 생성의 가짜 체크무늬는 픽셀 알파 검사에서 전부 불투명으로 확인되어 배경 추출 재생성했다.
- 최종 실제 alpha=0 픽셀 수: Frame 1,342,110; Fill 1,411,347; Track 1,389,575; Tick 1,498,852; FillCap 1,489,301. 프레임 중앙 구멍의 alpha=0 확인.
- 프레임 Sprite Rect: (38,251,2096,244), 9-slice border L572/B76/R282/T76.
- Fill Rect (31,425,1619,95); Track Rect (92,413,1496,109); Tick Rect (458,352,338,531); FillCap Rect (509,307,230,641).
- 임포트: Point, Uncompressed, Mipmaps OFF, Clamp, NPOT None, max4096, 입력 알파, Full Rect.

## LevelFrame

```text
Use case: precise-object-edit. Input image is the approved 01 GOTHIC METAL level HUD DESIGN REFERENCE. Create one separated production-ready Unity UI sprite, not a mockup/presentation/sheet. Match its restrained dark fantasy pixel art, aged ivory-silver on black iron bevels and garnet crimson details. Crisp coherent pixel clusters, no blur/bloom, no text/letters/numbers, no background scene, no header. Actual transparent PNG alpha outside the requested object, no painted gray checkerboard. All tips inside canvas, centered. Extract/recreate only the long empty BODY FRAME shown in the middle of the reference. Keep symmetric large pointed diamond garnet end ornaments, aged ivory-silver double top and bottom rails, black bevel, the smaller empty LV label plaque integrated on the LEFT taking about18% of inner width and its divider, and the long open rectangular XP aperture to its right. Preserve reference proportions (~10:1 overall visible bounding box). Crucially REMOVE ALL of the small downward hanging repeated bottom tick/pendant ornaments (will be separate sprites). REMOVE all fill and text. Central XP aperture is genuinely transparent alpha0. Left label plaque interior also transparent alpha0. Only metal rails, plaque rim, divider and end ornaments remain. Wide landscape output with enough transparent margin, isolated ONE empty frame.
```

## LevelFill

```text
Use case: precise-object-edit. Input image is the approved 01 GOTHIC METAL level HUD DESIGN REFERENCE. Create one separated production-ready Unity UI sprite, not a mockup/presentation/sheet. Match its restrained dark fantasy pixel art, aged ivory-silver on black iron bevels and garnet crimson details. Crisp coherent pixel clusters, no blur/bloom, no text/letters/numbers, no background scene, no header. Actual transparent PNG alpha outside the requested object, no painted gray checkerboard. All tips inside canvas, centered. Create ONLY the long FULL continuous CRIMSON XP FILL strip from the bottom of the reference, but remove its silver end cap ornaments (separate sprite). Rich muted oxblood enamel, thin warm-salmon top bevel, dark red lower bevel, tiny evenly spaced inset four-point diamond red metallic engravings along its middle. Exactly horizontal straight rectangle with squared vertical left/right ends, all pixels inside red strip opaque. About18:1 visible strip ratio, slight crisp dimensional shading within the strip, seamless-looking central repeated decoration. No outer metal frame, no plaque, no dividing ticks, no silver end caps. Full100% red strip, no empty/partial region. Transparent exterior only. This will be clipped horizontally by Unity Filled Image.
```

## LevelTrack

```text
Use case: precise-object-edit. Input image is the approved 01 GOTHIC METAL level HUD DESIGN REFERENCE. Create one separated production-ready Unity UI sprite, not a mockup/presentation/sheet. Match its restrained dark fantasy pixel art, aged ivory-silver on black iron bevels and garnet crimson details. Crisp coherent pixel clusters, no blur/bloom, no text/letters/numbers, no background scene, no header. Actual transparent PNG alpha outside the requested object, no painted gray checkerboard. All tips inside canvas, centered. Create ONLY the long EMPTY RECESSED XP TRACK/BACKING that sits behind the red fill. Near-black charcoal iron continuous rectangular strip, subtle worn iron surface with extremely faint oxblood incised small diamond motifs repeated across middle, faint charcoal top inset edge and dark lower bevel. No silver frame, no red filled region, no text, no tick marks. Exactly horizontal18:1 visible strip ratio, squared vertical left/right ends, opaque inside and true alpha0 outside. Keep dark enough to contrast clearly against a crimson fill. ONE standalone strip, not the whole HUD.
```

## LevelTick

```text
Use case: precise-object-edit. Input image is the approved 01 GOTHIC METAL level HUD DESIGN REFERENCE. Create one separated production-ready Unity UI sprite, not a mockup/presentation/sheet. Match its restrained dark fantasy pixel art, aged ivory-silver on black iron bevels and garnet crimson details. Crisp coherent pixel clusters, no blur/bloom, no text/letters/numbers, no background scene, no header. Actual transparent PNG alpha outside the requested object, no painted gray checkerboard. All tips inside canvas, centered. Create ONLY ONE of the SMALL DOWNWARD HANGING TICK ORNAMENTS seen under the main frame in the reference. A slim vertical antique ivory-silver metal spike with a tiny hollow diamond-shaped black inset/garnet center, short horizontal shoulders and long downward pointed tip. Bilaterally symmetric, elegant extremely simple readable silhouette as an approximately7px-wide by15px-high in-game ornament. Display enlarged crisply on an otherwise completely transparent square canvas with generous margins. Single isolated centered tick marker, no horizontal bar, no frame, no repeated marks, no background. Preserve the understated reference ornament rather than creating an elaborate large jewel.
```

## LevelFillCap

```text
Use case: precise-object-edit. Input image is the approved 01 GOTHIC METAL level HUD DESIGN REFERENCE. Create one separated production-ready Unity UI sprite, not a mockup/presentation/sheet. Match its restrained dark fantasy pixel art, aged ivory-silver on black iron bevels and garnet crimson details. Crisp coherent pixel clusters, no blur/bloom, no text/letters/numbers, no background scene, no header. Actual transparent PNG alpha outside the requested object, no painted gray checkerboard. All tips inside canvas, centered. Create ONLY the SMALL SILVER END-CAP at the moving right edge of the red fill in the reference. A slim vertical silver/ivory gothic bracket with dark iron outline, pointed top and bottom, small central diamond-like shoulder pointing slightly LEFT. About1:4 width:height, bilaterally balanced top/bottom, readable restrained pixel art. No red fill attached, no rail, no entire bar, no text, no other objects. ONE small isolated centered metal cap on genuinely transparent square canvas. This independent sprite will travel with the fill boundary; keep ornament minimal.
```
## LevelFrame — 실제 투명 배경 재추출

입력: exec-9118e0cc-9b80-4002-8975-54ce7ab74788.png

```text
Use case: background-extraction. Remove ALL gray checkerboard backdrop outside AND INSIDE both frame openings. Keep only the antique metal frame and its dark bevel outlines. Preserve exact shape, dimensions, position and colors. The two inner openings must be fully transparent too. Actual transparent PNG alpha channel, NOT painted checkerboard. Fully transparent background. No added objects or text.
```

## LevelFill — 실제 투명 배경 재추출

입력: exec-0c025f64-260c-417b-bf5c-39de002a3eca.png

```text
Use case: background-extraction. Remove ALL gray checkerboard outside the long red rectangle. Keep ONLY the red horizontal bar unchanged, preserving the rectangular edges, texture and highlights. No background or shadow outside it. Actual transparent PNG alpha channel, NOT painted checkerboard. Fully transparent background. No added objects or text.
```

## LevelTrack — 실제 투명 배경 재추출

입력: exec-f26122c3-fc5d-4a3b-9b44-aa6c89cd010c.png

```text
Use case: background-extraction. Remove ALL checkerboard outside the long black rectangle. Keep ONLY the black horizontal bar unchanged, preserving its dark texture and rectangular edges. No background or shadow outside it. Actual transparent PNG alpha channel, NOT painted checkerboard. Fully transparent background. No added objects or text.
```

## LevelTick — 실제 투명 배경 재추출

입력: exec-28a2dd58-dd48-4969-b4ef-4696f941ace4.png

```text
Use case: background-extraction. Remove ALL white and gray checkerboard background. Keep ONLY the single small silver and red tick ornament and its black outlines exactly unchanged. Actual transparent PNG alpha channel, NOT painted checkerboard. Fully transparent background. No added objects or text.
```

## LevelFillCap — 실제 투명 배경 재추출

입력: exec-40944ab0-0866-484d-8f73-25f95de7b26c.png

```text
Use case: background-extraction. Remove ALL white and gray checkerboard background. Keep ONLY the single narrow antique silver bracket with black outlines exactly unchanged. Actual transparent PNG alpha channel, NOT painted checkerboard. Fully transparent background. No added objects or text.
```

