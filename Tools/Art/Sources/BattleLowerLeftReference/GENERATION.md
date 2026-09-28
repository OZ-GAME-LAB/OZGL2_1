# Battle lower-left reference recreation

- Date: 2026-09-23
- Mode: built-in image generation (reference-guided edits / individual transparent asset generation).
- Scope approved: Cost / reroll / price visuals and positions; remove only the three circled panel diamonds. Keep dynamic numbers and existing button behaviour.
- Raw outputs: `Tools/Art/Sources/BattleLowerLeftReference/*_Original.png` (unchanged).
- Runtime assets: `Assets/06.UI/BattleMutedPreview/LowerLeftReference/`.
- Cleanup: `Tools/Art/PrepareBattleLowerLeftArt.ps1` removes baked neutral checker backgrounds, retains original visible colours, crops isolated assets and aligns their bounds. No icons painted by code.
- Panel cleanup: `Tools/Art/PrepareBattleUnifiedPanel.ps1` removes exterior checker and registers the seven rim segments to the existing 45-degree panel geometry. Source landmarks: `0.000,310.429;179.128,131.301;351.704,303.006;425.413,231.139;605.183,411.464;1705.893,410.946;1989.906,130.754;2171.000,315.017`; source bottom 591; target bottom 420; output 1920x432. Only background removal and size/position registration after image generation.
- Sprite import: FullRect / Point / no mipmaps / uncompressed / center pivot / actual alpha.
- Editor menu: `Tools/OZGL2/Battle/Apply Lower Left Reference` (Undo supported; saves only the preview scene; requires clean Edit Mode).
- Layout: keep the user's Currency root and BottomNobleBackground transform/tint; reroll 168x168, horizontally under the small panel peak and vertically centered with Currency; price 180x56 with 4px gap; icons and dynamic Unity Text separate. Existing NotoSans CJK KR provides readable white numbers.
- Preserved: source art, original UI_Battle scene, button links, labels' current numeric strings, Runtime data and every unrelated UI.

## Generation prompts

### Frame_Cost

References:
- `C:/Users/sudea/AppData/Local/Temp/codex-clipboard-8e34d20f-f7e2-4ea8-ba5d-4c35e0c5e5d7.png`

```text
Use case: precise-object-edit/background-extraction. Production Unity UI sprite, not a mockup. Reference input is the approved bottom-left game UI. Recreate ONLY the specified element faithfully, pixel-art dark gothic style, sharp at final small scale, front-on no perspective. Output one isolated sprite on actual transparent alpha (not checkerboard), no text or numbers, no background, no other UI. Preserve exact silhouette and proportions of the element in reference, do not redesign. Element: the LARGE muted-crimson diamond outer frame around the fire/100 on the LEFT. Extract ONLY that hollow diamond frame, no flame, no numbers, no black interior fill. Thick muted dusty crimson angular band with distressed black cutouts, outward double-chevron geometric ornaments at top/bottom/left/right, all perfectly centered and rotationally symmetric, 45degree straight sides. No gold jewels. This frame is broad and bold compared to the small reroll frame. Square canvas tightly fitted with equal 3% transparent margin. Match reference's red, not bright scarlet.
```

### Icon_Flame

References:
- `C:/Users/sudea/AppData/Local/Temp/codex-clipboard-8e34d20f-f7e2-4ea8-ba5d-4c35e0c5e5d7.png`

```text
Use case: precise-object-edit/background-extraction. Production Unity UI sprite, not a mockup. Reference input is the approved bottom-left game UI. Recreate ONLY the specified element faithfully, pixel-art dark gothic style, sharp at final small scale, front-on no perspective. Output one isolated sprite on actual transparent alpha (not checkerboard), no text or numbers, no background, no other UI. Preserve exact silhouette and proportions of the element in reference, do not redesign. Element: the WHITE magical flame silhouette above 100 in the LARGE LEFT diamond. Recreate its broad teardrop shape, tapered crooked tip and open dark interior flame holes faithfully. Solid clean neutral white, not yellow/beige. Transparent negative holes and background. The flame should have a generous broad body rather than a narrow vertical streak. Only flame, no frame, no number, no shadow/glow. Square canvas centered with about8% transparent margin; preserve tall flame aspect ratio, do not squeeze horizontally.
```

### Frame_Reroll

References:
- `C:/Users/sudea/AppData/Local/Temp/codex-clipboard-8e34d20f-f7e2-4ea8-ba5d-4c35e0c5e5d7.png`

```text
Use case: precise-object-edit/background-extraction. Production Unity UI sprite, not a mockup. Reference input is the approved bottom-left game UI. Recreate ONLY the specified element faithfully, pixel-art dark gothic style, sharp at final small scale, front-on no perspective. Output one isolated sprite on actual transparent alpha (not checkerboard), no text or numbers, no background, no other UI. Preserve exact silhouette and proportions of the element in reference, do not redesign. Element: ONLY the small diamond FRAME surrounding the reroll arrows on the RIGHT. Preserve thin ivory-white outer border paired with a slightly bolder muted-crimson inner diamond border and tiny sharp ornamental white corner nodes/tips, plus dark distressed accents. The red inner trim must remain visible; not a plain white diamond or oversized jewelry corners. Symmetric square diamond, all sides45degrees. Hollow fully transparent center (remove arrows/background) and outside. Square canvas 3% equal margins. Slim border compared with the big left Cost frame.
```

### Icon_Reroll

References:
- `C:/Users/sudea/AppData/Local/Temp/codex-clipboard-8e34d20f-f7e2-4ea8-ba5d-4c35e0c5e5d7.png`

```text
Use case: precise-object-edit/background-extraction. Production Unity UI sprite, not a mockup. Reference input is the approved bottom-left game UI. Recreate ONLY the specified element faithfully, pixel-art dark gothic style, sharp at final small scale, front-on no perspective. Output one isolated sprite on actual transparent alpha (not checkerboard), no text or numbers, no background, no other UI. Preserve exact silhouette and proportions of the element in reference, do not redesign. Element: ONLY the pair of WHITE curved opposing reroll arrows in the smaller right diamond. Match the reference's two broad gently curving swept arrows, upper arrow points right/down and lower arrow points left/up, distinct arrowheads and two generous gaps. Slightly wider-than-tall shape, no enclosing circle or frame. Clean solid neutral-white pixel-art silhouette, no yellow. Actual transparent background and inner gaps. Center pair in square canvas, do not thicken into a closed ring, no glow or texture.
```

### Frame_Price

References:
- `C:/Users/sudea/AppData/Local/Temp/codex-clipboard-8e34d20f-f7e2-4ea8-ba5d-4c35e0c5e5d7.png`

```text
Use case: precise-object-edit/background-extraction. Production Unity UI sprite, not a mockup. Reference input is the approved bottom-left game UI. Recreate ONLY the specified element faithfully, pixel-art dark gothic style, sharp at final small scale, front-on no perspective. Output one isolated sprite on actual transparent alpha (not checkerboard), no text or numbers, no background, no other UI. Preserve exact silhouette and proportions of the element in reference, do not redesign. Element: ONLY the long narrow RECTANGULAR price plaque border directly under the reroll button. Thin ivory-white double edged straight rectangular border with tiny white cross-like corners, NO big gold/red diamond corner jewels or filigree. Aspect ratio about3.3:1. Long flat horizontal edges, clean small pointed tips at4corners. Remove100 and red diamond currency icon. Hollow transparent center and exterior; black fill supplied separately in Unity. Center long narrow border in wide canvas with equal small transparent margins. No rounded corners, no red banner.
```

### Icon_CostGem

References:
- `C:/Users/sudea/AppData/Local/Temp/codex-clipboard-8e34d20f-f7e2-4ea8-ba5d-4c35e0c5e5d7.png`

```text
Use case: precise-object-edit/background-extraction. Production Unity UI sprite, not a mockup. Reference input is the approved bottom-left game UI. Recreate ONLY the specified element faithfully, pixel-art dark gothic style, sharp at final small scale, front-on no perspective. Output one isolated sprite on actual transparent alpha (not checkerboard), no text or numbers, no background, no other UI. Preserve exact silhouette and proportions of the element in reference, do not redesign. Element: ONLY the flat muted-crimson diamond currency symbol inside the small rectangular price plaque. Red filled square rotated45degrees, thin darker crimson inset double border, tiny near-black outer edge, perfectly centered symmetric. Flat iconic gem, not a 3D gemstone. No white highlights, no gold, no corner ornaments, no text. Square canvas tight with4% margins, outside genuinely transparent.
```

### Panel_Clean

References:
- `D:/GitHub/OZGL2_1/Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Unified.png`
- `C:/Users/sudea/AppData/Local/Temp/codex-clipboard-f890b26c-1389-4a26-84e3-3436deb86a2c.png`

```text
Use case: precise-object-edit. Input1 is the ONE-PIECE production panel sprite edit target; input2 is a screenshot marking exactly three decorations to remove with red circles. Remove ONLY these three small gold diamond ornaments: far LEFT upper outside corner at about x1.5% y38%, the V-valley between the two left peaks at x16.2% y43% (remove its hanging flourish as part of that ornament), far RIGHT upper outside corner at x98.5% y38%. Replace those three spots with the CONTINUOUS existing thin gold/red border and matching velvet, clean straight/corner joins, no new ornaments. Preserve ALL other ornamentation exactly: both large peak diamonds and hanging filigree, smaller left peak diamond/filigree, BOTH central-bridge corner diamond ornaments, BOTH lower corner flourishes. Preserve all geometry, exact45degree slants, silhouette, aspect ratio1920:432, pixel-art texture, gold trim, muted dark red velvet fill and brightness of input1. Do NOT simplify/remove the remaining diamonds; do NOT change shape/layout; do NOT add a black band. Output ONLY the COMPLETE single panel sprite on truly TRANSPARENT ALPHA outside, no checkerboard, no red annotations or handles, no text/numbers/buttons/icons. Keep full asset visible without cropping and preserve the original sprite layout. Same long wide panel proportions.
```

