# 결과창 아트 원본 생성 기록

2026-09-26 · built-in image_gen · 자산별 1회, 총 8회. 원본은 `Raw/`에 바이트 변경 없이 복사했습니다.

## 기준

- `../../Revision02/UnifiedResult_Defeat.png`, `../../Revision02/UnifiedResult_Victory.png`, `../../Revision02/DesignPlan.md`.
- 동적 글자·수치·버튼 문구는 PNG에 넣지 않습니다.
- 생성 요청에는 true alpha를 명시했지만 도구 결과가 RGB 체크 배경을 포함했습니다. `Raw`는 생성 원본이며 바로 Unity에 쓰는 최종 스프라이트가 아닙니다.
- 승인 범위의 후처리는 배경 투명화, 동일 여백·크기·대칭 정리만 수행합니다. 색상·디자인·원본은 변경하지 않습니다.

## Result_RecordPanel

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-f1a5e4e4-1250-47b3-8504-07b0e204322f.png`
- 보존: `Raw/Result_RecordPanel.png`
- 시각 확인: 기록판 형태, 빈 내부, 장식 및 문자 제거 확인. 외곽에 체크무늬가 보이므로 실제 알파 여부 후처리에서 검증 필요. 측면 끝 장식 여백이 적어 후처리에서 안전 여백 추가 필요.

```text
Use case: stylized-concept
Asset type: production Unity UI PNG sprite, Result_RecordPanel, one isolated asset.
Input images: Image 1 and Image 2 are STYLE AND SHAPE references only: the accepted defeat and victory result mockups. Recreate ONLY the wide LOWER RECORD BOARD shown in both images, identical neutral shared frame. Do not recreate the whole screen.
Primary request: a front-facing perfectly symmetric horizontal rectangular gothic game UI board with a full opaque near-black neutral obsidian textured body and the thin aged ivory-silver double metal outline, small deep red diamond gems at the top center, bottom center and side midpoints, restrained pointed spike ornaments at all four corners and side midpoint ends. Match the exact restrained profile and dark diamond-grain body of the referenced lower record panel.
Composition: single complete board centered on a transparent landscape canvas; visible ornament bounds approximately 3.6:1 wide, large empty interior for later text and data. The full panel including corner spikes must be visible with 6 percent clear transparent outer padding. Flat orthographic UI, straight horizontal/vertical edges. Fill the ENTIRE rectangular interior with near-black body; outside the board genuinely transparent alpha.
Style: crisp controlled gothic pixel art matching the reference, ivory/silver and small red gems only; limited stepped pixel highlights, moderate pixel texture, no photographic or 3D render detail, no excessive ornate curls. Full reference identity recognizable.
Constraints: NO letters, NO words, NO numbers, NO icons, NO internal dividers, NO experience bar, NO button or button protrusion, NO flags, NO candles, NO crown, NO background scene. Board only. No glow, no bloom, no cast shadow beyond the silhouette. Actual transparent PNG background; do not draw a checkerboard or flat background color.
```

## Result_CrestFrame

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-c88470f2-4ed7-42fc-9de3-2320d9636e14.png`
- 보존: `Raw/Result_CrestFrame.png`
- 시각 확인: 빈 마름모 중심, 실버 이중 테두리, 붉은 보석 4개, 왕관과 룬 제거 확인. 체커 배경이 보여 알파 검증 필요. 끝 장식 여백 추가 필요.

```text
Use case: stylized-concept
Asset type: production Unity UI sprite Result_CrestFrame, exactly one isolated hollow frame.
Input images: Image 1 defeat and Image 2 victory are STYLE AND SHAPE references. Take ONLY the top central diamond crest frame surrounding the crown, remove the crown and circular rune ring completely.
Primary request: an empty square-diamond heraldic frame, diamond vertices at top/right/bottom/left, formed by two restrained thin aged ivory-silver metal rims. Four small deep-red diamond gems at the four vertices, understated thin outward compass-point ornaments extending a little past each vertex. The entire center is a large hollow transparent opening, and the exterior is genuinely transparent too. Preserve reference scale and understated sharp geometry, perfectly left-right and top-bottom symmetric.
Composition: single complete frame centered in a square canvas. Equal clear padding of 9 percent on all sides beyond every tip; all spikes inside image, orthographic straight-on UI. Thin border, large empty diamond center with no dark backing.
Style: crisp controlled gothic pixel art from the accepted result mockup, limited ivory metal pixels, muted charcoal crevices, deep red gems. No excessive ornaments, no painterly photorealistic detail, no perspective.
Constraints: NO crown, NO runic circle, NO letter or word or number, NO scene, NO filled center, NO glow or bloom, NO cast shadow outside subject. Genuinely transparent PNG alpha outside and inside frame; do not paint a checkerboard or flat-colored backdrop.
```

## Result_RuneRing

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-a8cd3bf0-6d0e-462e-921c-14296d01b916.png`
- 보존: `Raw/Result_RuneRing.png`
- 시각 확인: 빈 중심, 원형 룬/방사선, 중립 흰색 계열 확인. 체커 배경이 보여 알파 검증 필요. 끝 장식 여백 추가 필요.

```text
Use case: stylized-concept
Asset type: production Unity UI PNG sprite Result_RuneRing, a single isolated tintable rune ring.
Input images: Image 1 defeat and Image 2 victory show the accepted visual style. Extract the design language ONLY of the thin circular ring and tiny abstract runes behind the top diamond crest. Do not include the crest diamond, crown or any other UI.
Primary request: a very thin circular occult-gothic ornament in neutral desaturated off-white and gray, with restrained concentric arc fragments, small radial marks and a few simple abstract runic strokes around the circumference. The middle 72 percent of the circle diameter is entirely empty transparent negative space. A few short thin compass-like radial accents at diagonals; balanced circular silhouette, elegant restraint.
Composition: one complete ring centered on a square canvas with 10 percent fully transparent outer safety margin. Front-facing, no perspective. Use alpha to make empty center and exterior genuinely transparent.
Style: crisp controlled pixel-art game UI at moderate pixel density, reference-matched runic altar theme. Neutral near-white/grays ONLY so Unity can tint it gold or red later. Thin clearly separated strokes, no dark opaque background behind symbols, no white disc, no tiny photorealistic texture.
Constraints: no real writing, no legible alphabet, no numbers, no crown, no diamond frame, no scene, no figures, no glowing aura, no bloom, no haze, no shadow. Actual transparent PNG alpha with clean edges. Do not paint a checkerboard.
```

## Result_Crown_Victory

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-2feb5a0a-cc3d-448e-864a-73e4f8e1b665.png`
- 보존: `Raw/Result_Crown_Victory.png`
- 시각 확인: 온전한 금색 왕관, 프레임/문자 제거 확인. 중심 정렬. 체커 배경이 보여 내부 구멍을 포함한 알파 검증 필요. 동일 캔버스 맞춤은 후처리에서 수행.

```text
Use case: stylized-concept
Asset type: production Unity UI PNG sprite Result_Crown_Victory, exactly one isolated victory crown.
Input images: the accepted defeat and victory result mockups are shape and pixel-art references. Use ONLY the intact antique gold crown in the top diamond of the VICTORY mockup (Image 2). The crown is the full asset; do not include the frame or runes.
Primary request: a dignified low-width antique-gold medieval open crown, front view, highest middle tip with small diamond ornament, two restrained tall side tips and lower intervening tips, gently curved solid metal base band, small dark burgundy gems and carved negative spaces matching the reference crown. No royal velvet cap, no head, no cushion. Keep the reference crown's restrained pointed silhouette.
Composition: a single complete crown centered on a square canvas, visible crown occupies about 62 percent of canvas width and 44 percent of canvas height, base band horizontal at 68 percent canvas height, tallest tip at 24 percent. Wide transparent safety margin around the crown; pivot at canvas center. Symmetric primary silhouette suitable for pairing with the defeat variant.
Style: crisp controlled gothic game pixel art matching reference, muted antique gold and ivory highlights, charcoal crevices, small deep red gem details. Moderate pixel blocks; not photorealistic, no 3D render, no excessive tiny filigree.
Constraints: no diamond frame, no rune ring, no words/letters/numbers, no extra object, no scene, no glowing aura, no bloom or cast shadow beyond the crown. Genuinely transparent PNG alpha background and transparent open spaces between spikes; do not draw a checkerboard.
```

## Result_Crown_Defeat

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-6cf101ef-0765-43b1-92c7-790c9f8da66e.png`
- 보존: `Raw/Result_Crown_Defeat.png`
- 시각 확인: 변색된 실버와 균열/이 빠진 모서리 확인. 체커 RGB 배경 제거 필요. 생성기가 왕관 크기를 약간 키워 후처리에서 승리 왕관과 가시 크기/기준점 맞춤 필요.

```text
Use case: precise-object-edit
Asset type: production Unity UI PNG sprite Result_Crown_Defeat, one crown only.
Input images: Image 1 is the edit target, the generated intact victory crown sprite; Image 2 is accepted defeat mockup for tarnished mood only.
Primary request: turn ONLY the crown from Image 1 into its defeated version by changing its gold metal to tarnished desaturated antique silver, dulling the burgundy gems, and adding several controlled visible fractures and chipped sections across the metal. A central diagonal crack and small missing edge chips convey defeat; keep the crown recognizable, dignified and readable.
Invariants: preserve Image 1 EXACT square canvas, overall crown dimensions, location, center, base-band y position, five main tips, gemstone positions, central point and transparent outer padding. Do not enlarge, crop, reposition, rotate, rebuild, or change the footprint. Keep all metal as a single crown with no detached debris. No frame or runes. Keep the controlled gothic pixel-art language.
Background: replace the displayed checkerboard in Image 1 with genuine transparent alpha. Both exterior and crown holes must be actually transparent; DO NOT draw a checkerboard or any backdrop.
Style: tarnished ivory-silver and charcoal, restrained dark burgundy gems; crisp stepped pixels, same pixel density and silhouette as victory.
Constraints: only one crown, no words/letters/numbers, no emblem frame, no rune ring, no scenery, no glow/bloom/haze, no ground shadow. True transparent PNG.
```

## Result_Banner_Victory

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-ab98cbb3-e988-4890-8e4a-c50a7a3dc029.png`
- 보존: `Raw/Result_Banner_Victory.png`
- 시각 확인: 온전한 버건디 천, 금색 문양, 깃대/가로대/작은 체인/촛대 확인. 단일 세로 자산이며 반전 사용 가능. 체커 RGB 배경 제거 필요.

```text
Use case: stylized-concept
Asset type: production Unity UI PNG sprite Result_Banner_Victory, exactly one vertical banner assembly, designed to be mirrored for the opposite side.
Input images: the accepted defeat and victory result mockups are style references. Recreate ONLY ONE intact hanging banner assembly beside the VICTORY title from Image 2, in the same restrained gothic pixel-art style.
Primary request: one tall slim aged ivory-silver metal stand with sharp restrained gothic finial and foot, a short horizontal crossbar extending to the right from near its top, a single intact deep burgundy hanging pennant with a pointed bottom suspended from the crossbar, thin worn old-gold trim and small simple old-gold abstract cross-star emblem in its middle. Include a small three-candle cluster integrated at the foot of the stand to the left, tiny pale wax candles with restrained red-orange flame pixels and NO glow halo. One short chain with tiny pendant at the far crossbar tip is acceptable. The assembly has a rigid vertical mounting pole at about 37 percent canvas width and cloth hanging to its right. Keep pole, crossbar, candle base and cloth compact as one grouped sprite.
Composition: isolated full assembly on transparent portrait canvas, frontal UI orthographic view. Full tip and base visible, 8 percent clear padding on every edge. Canvas portrait about 1:2 width:height. The upper cloth attachment and lower stand foot provide shared anchors for the defeat counterpart. Banner lower point well above stand foot.
Style: controlled crisp gothic pixel art, limited silver/ivory metal, rich dark burgundy velvet, restrained antique-gold emblem. Moderate clean pixel detail matching reference, no photorealism, no full scene, no heavy 3D rendering.
Constraints: exactly one banner and one stand, no duplicate/right counterpart, no floor or platform, no wall, no words/numbers/letters, no UI panel, no crown, no frame, no title, no glow/bloom/fog/shadows outside silhouette. Actual transparent alpha PNG outside the subject; do not paint a checkerboard.
```

## Result_Banner_Defeat

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-f0fb628b-f690-486a-8e9b-4a99fffd3ddb.png`
- 보존: `Raw/Result_Banner_Defeat.png`
- 시각 확인: 깃대/가로대/촛대 기준 위치 보존, 천의 해짐과 구멍 확인. 체커 RGB 배경과 천 구멍 제거 필요.

```text
Use case: precise-object-edit
Asset type: production Unity UI PNG sprite Result_Banner_Defeat, one torn banner assembly.
Input images: Image 1 is the edit target, the generated victory banner assembly. Image 2 is the accepted defeat mockup for cloth damage style.
Primary request: change ONLY the hanging burgundy FABRIC of Image 1 into its defeated state: make the cloth frayed and torn with a few long ragged tears along the lower edge, irregular missing fabric toward the bottom, small holes and cracked worn old-gold emblem. Keep a visible dark burgundy body and recognizable banner silhouette.
Invariants: preserve EXACT portrait canvas dimensions, sprite placement, pole x position, full stand top and bottom y positions, crossbar position and widths, top fabric attachment points, metal finial, right horn, chain and pendant, candle cluster and flames, every non-fabric object. Do not move, resize, redraw or rotate the stand. The lower ragged cloth remains within the original intact pennant's outer bounds. The state switch must not move the mount or base. Keep the same frontal view and pixel density.
Background: remove the painted checkerboard from the target and give it actual transparent alpha outside the sprite, in every hole, between the pole and fabric, inside chain loops. Do not paint a checkerboard or background color.
Style: crisp controlled gothic pixel art matching original; deep burgundy fabric, ivory-silver metal and gold embroidery. No glow or haze.
Constraints: one banner only, no new objects, no detached cloth or floating debris, no scene, no typography, no letters or numbers, no UI board, no frame. True transparent PNG.
```

## Result_TitleAccent

- 원본: `C:/Users/sudea/.codex/generated_images/01a0dc2a-4baf-77c2-82e6-77c52c9b5340/exec-3d2a3327-4431-4b1e-a7bb-96e30020432d.png`
- 보존: `Raw/Result_TitleAccent.png`
- 시각 확인: 중립 회색, 좌우 대칭 저대비 장식, 문자 없음 확인. 체커 RGB 배경 제거 필요. 빈 중심은 대체로 확보됨.

```text
Use case: stylized-concept
Asset type: production Unity UI PNG sprite Result_TitleAccent, one isolated neutral tintable background accent for a result title.
Input images: the accepted defeat and victory result mockups are style references. Recreate ONLY the subtle abstract chipped-ink silhouette directly BEHIND the large result title words. Do not draw any actual words, title letters, board, crest, crown or flags.
Primary request: a restrained low wide symmetrical gothic ink-and-engraved-rune flourish silhouette, like two rising small sprays of chipped stone ink flakes emerging from the lower left and lower right toward the middle. A few broken angular scratch accents, tiny diamond chips and rune-like fragments. Leave the central upper 50 percent very sparse and empty so a title can be overlaid. Main density sits low along the baseline, with gently rising side shapes, balanced left/right. This is a subtle reusable mask, not a big bold heraldic emblem.
Color: neutral medium gray only, varying alpha opacity subtly; monochrome tintable in Unity. No red, gold, blue, white highlights or black rectangular backing.
Composition: one complete wide flourish centered on a wide canvas about 3:1; visible ornament centered with 10 percent transparent clear margin on all edges, maximum visible height about 60 percent canvas height. Crisp controlled pixel-art silhouette, matching the reference UI at modest detail. Absolutely no glowing smoke or painterly splashes.
Constraints: no legible letters, no words/numbers, no title text, no skull/crown/cross badge, no scene, no glow/bloom/haze/shadow, no frame or panel. Transparent PNG alpha everywhere around and through the separated small strokes and chips. Do NOT draw a checkerboard or any solid background.
```


