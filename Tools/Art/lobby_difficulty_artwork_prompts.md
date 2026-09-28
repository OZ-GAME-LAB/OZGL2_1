# 로비 난이도 개별 Artwork 생성 기록

- 내장 `image_gen` 사용. 01 마왕성 / 02 용사군, 각 쉬움·보통·어려움 3장씩 총 6장.
- 기본 적용: 02 용사군. 01 마왕성은 교체용 Artwork Set SO로 보관.
- 비교판은 스타일/구도 참조이며, 실제 출력에는 프레임·문자·마름모 테두리를 포함하지 않는다.
- 생성 원본 보존, 별도 이미지 필터/그림 후처리 없음. Unity의 기존 마름모 Mask에서 표시.
- 출력 폴더: `Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/Artwork/`.

## 생성 결과 및 원본 보관

- 아래 6장 모두 정사각 전체 그림으로 생성되어 테두리/TMP와 분리했다. 원본은 보존하고 출력 폴더로 복사했다.
- 원본 폴더: `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/`.

| 출력 파일 | 보존한 원본 파일 |
| --- | --- |
| Castle_Easy.png | exec-9c472df9-703e-4d7a-a951-8d3515fc742d.png |
| Castle_Normal.png | exec-6cc94a32-04bc-401b-9174-36f4f0efe6af.png |
| Castle_Hard.png | exec-11774ae2-33dc-404a-9d54-da0d1869e5ca.png |
| Hero_Easy.png | exec-935be562-9fc7-4ee2-ab19-9217385171b1.png |
| Hero_Normal.png | exec-e42af6a0-3c8b-4487-aa1f-a2b267c2e032.png |
| Hero_Hard.png | exec-188b65f4-3784-414d-9f2d-eec4ab74a306.png |

- Unity Import: Sprite Single / Point / No Mip Maps / Uncompressed / NPOT None / Full Rect / Clamp. 픽셀 자체를 코드로 재작성하거나 필터링하지 않았다.
- 원본 스타일/프레임 없는 구도를 시각 확인했으며 Play Mode에서 02의 세 난이도와 01 교체 세트의 마름모 표시를 확인했다.

## Castle_Easy

참조: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/01_castle_siege_pixel_v2.png`

```text
Use case: stylized-concept.
Asset type: final Unity UI difficulty artwork, one separate square sprite image. Input image is a STYLE AND COMPOSITION REFERENCE only, not a canvas to reproduce.
Create ONE 1:1 square full-bleed illustration for the specified panel. NO FRAME, no diamond border, no ornament, NO TEXT, no labels, no UI, no black presentation margin, no panel divisions, no transparency/checkerboard. Extend the illustration into all four corners: it will be clipped by an existing Unity diamond Mask later. Do NOT draw a diamond shape.
Preserve the selected reference panel's focal subject, story and visual family. Plan for a diamond crop connecting the midpoints of this square: keep the main helmet/face or central castle and important weapon contact within the central 55%, head at x=50%, y=34-39%; sky and expendable scenery in the cropped corners. Camera upright, no tilted artwork.
Authentic low-resolution dark fantasy PIXEL ART, as if carefully hand drawn at 128x128 logical pixels and enlarged with nearest-neighbor. Clearly visible square pixel clusters, sharp stepped diagonals, limited palette around 24-32 colors, solid grouped shadow shapes and 2-4 shade bands, simplified readable silhouettes. Strong art direction, not a pixel filter over a painting. NO antialiasing, smooth gradients, fine noise, photorealistic texture, painterly strokes, blur, bloom, vector art or 3D rendering. Keep mature proportions and gothic mood. Charcoal/blue-black, aged ivory steel, muted oxblood, tarnished gold. The player defends demon castle against HUMAN enemies; no demon horns on human knights.
Selected scene:
Use ONLY the LEFT (easy) panel of concept 01. Architecture-led view of the same recognizable towering black gothic demon castle with central spire and small red windows, broad stone causeway from foreground to the gate. Quiet gray/slate stepped clouds, scattered few HUMAN scouts seen from behind advancing towards the castle, sparse muted torches, no siege engines. The castle upper spire remains fully inside the central safe diamond. Castle dominant, invaders tiny but distinct sprite silhouettes.
```

## Castle_Normal

참조: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/01_castle_siege_pixel_v2.png`

```text
Use case: stylized-concept.
Asset type: final Unity UI difficulty artwork, one separate square sprite image. Input image is a STYLE AND COMPOSITION REFERENCE only, not a canvas to reproduce.
Create ONE 1:1 square full-bleed illustration for the specified panel. NO FRAME, no diamond border, no ornament, NO TEXT, no labels, no UI, no black presentation margin, no panel divisions, no transparency/checkerboard. Extend the illustration into all four corners: it will be clipped by an existing Unity diamond Mask later. Do NOT draw a diamond shape.
Preserve the selected reference panel's focal subject, story and visual family. Plan for a diamond crop connecting the midpoints of this square: keep the main helmet/face or central castle and important weapon contact within the central 55%, head at x=50%, y=34-39%; sky and expendable scenery in the cropped corners. Camera upright, no tilted artwork.
Authentic low-resolution dark fantasy PIXEL ART, as if carefully hand drawn at 128x128 logical pixels and enlarged with nearest-neighbor. Clearly visible square pixel clusters, sharp stepped diagonals, limited palette around 24-32 colors, solid grouped shadow shapes and 2-4 shade bands, simplified readable silhouettes. Strong art direction, not a pixel filter over a painting. NO antialiasing, smooth gradients, fine noise, photorealistic texture, painterly strokes, blur, bloom, vector art or 3D rendering. Keep mature proportions and gothic mood. Charcoal/blue-black, aged ivory steel, muted oxblood, tarnished gold. The player defends demon castle against HUMAN enemies; no demon horns on human knights.
Selected scene:
Use ONLY the CENTER (normal) panel of concept 01. Same black gothic demon castle silhouette and centered causeway camera as easy, small red windows. An organized HUMAN knight column seen from behind, spear silhouettes and several banners approaching gate, muted crimson stepped clouds and restrained torches. More pressure than easy but not full siege. Castle dominant, enemy column readable. Preserve matching architectural silhouette with other difficulties.
```

## Castle_Hard

참조: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/01_castle_siege_pixel_v2.png`

```text
Use case: stylized-concept.
Asset type: final Unity UI difficulty artwork, one separate square sprite image. Input image is a STYLE AND COMPOSITION REFERENCE only, not a canvas to reproduce.
Create ONE 1:1 square full-bleed illustration for the specified panel. NO FRAME, no diamond border, no ornament, NO TEXT, no labels, no UI, no black presentation margin, no panel divisions, no transparency/checkerboard. Extend the illustration into all four corners: it will be clipped by an existing Unity diamond Mask later. Do NOT draw a diamond shape.
Preserve the selected reference panel's focal subject, story and visual family. Plan for a diamond crop connecting the midpoints of this square: keep the main helmet/face or central castle and important weapon contact within the central 55%, head at x=50%, y=34-39%; sky and expendable scenery in the cropped corners. Camera upright, no tilted artwork.
Authentic low-resolution dark fantasy PIXEL ART, as if carefully hand drawn at 128x128 logical pixels and enlarged with nearest-neighbor. Clearly visible square pixel clusters, sharp stepped diagonals, limited palette around 24-32 colors, solid grouped shadow shapes and 2-4 shade bands, simplified readable silhouettes. Strong art direction, not a pixel filter over a painting. NO antialiasing, smooth gradients, fine noise, photorealistic texture, painterly strokes, blur, bloom, vector art or 3D rendering. Keep mature proportions and gothic mood. Charcoal/blue-black, aged ivory steel, muted oxblood, tarnished gold. The player defends demon castle against HUMAN enemies; no demon horns on human knights.
Selected scene:
Use ONLY the RIGHT (hard) panel of concept 01. Same black gothic demon castle silhouette and centered causeway camera. Dense elite HUMAN siege army seen from behind fills foreground causeway, two blocky wooden siege towers on the sides, dark crimson eclipse in sky and controlled firelight. Keep the castle as the player's defended citadel, danger comes from invaders. Castle not transformed into a stronger castle. Central tower and main siege formation readable under diamond crop.
```

## Hero_Easy

참조: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/02_hero_threat_pixel_v2.png`

```text
Use case: stylized-concept.
Asset type: final Unity UI difficulty artwork, one separate square sprite image. Input image is a STYLE AND COMPOSITION REFERENCE only, not a canvas to reproduce.
Create ONE 1:1 square full-bleed illustration for the specified panel. NO FRAME, no diamond border, no ornament, NO TEXT, no labels, no UI, no black presentation margin, no panel divisions, no transparency/checkerboard. Extend the illustration into all four corners: it will be clipped by an existing Unity diamond Mask later. Do NOT draw a diamond shape.
Preserve the selected reference panel's focal subject, story and visual family. Plan for a diamond crop connecting the midpoints of this square: keep the main helmet/face or central castle and important weapon contact within the central 55%, head at x=50%, y=34-39%; sky and expendable scenery in the cropped corners. Camera upright, no tilted artwork.
Authentic low-resolution dark fantasy PIXEL ART, as if carefully hand drawn at 128x128 logical pixels and enlarged with nearest-neighbor. Clearly visible square pixel clusters, sharp stepped diagonals, limited palette around 24-32 colors, solid grouped shadow shapes and 2-4 shade bands, simplified readable silhouettes. Strong art direction, not a pixel filter over a painting. NO antialiasing, smooth gradients, fine noise, photorealistic texture, painterly strokes, blur, bloom, vector art or 3D rendering. Keep mature proportions and gothic mood. Charcoal/blue-black, aged ivory steel, muted oxblood, tarnished gold. The player defends demon castle against HUMAN enemies; no demon horns on human knights.
Selected scene:
Use ONLY the LEFT (easy) panel of concept 02. Center a modest human footsoldier from waist/thigh up: simple iron helmet with narrow dark face opening, short dark scarf, worn leather/mail tunic with pale medieval emblem, small round wooden shield at viewer right, plain short sword at viewer left angled into safe center. Two tiny scouts behind, dim gothic skyline and muted red eclipse sky. Helmet center at about x50%,y36%, torso centered. Do not crop top of helmet under diamond; simplify armor, lower threat than heavy knight.
```

## Hero_Normal

참조: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/02_hero_threat_pixel_v2.png`

```text
Use case: stylized-concept.
Asset type: final Unity UI difficulty artwork, one separate square sprite image. Input image is a STYLE AND COMPOSITION REFERENCE only, not a canvas to reproduce.
Create ONE 1:1 square full-bleed illustration for the specified panel. NO FRAME, no diamond border, no ornament, NO TEXT, no labels, no UI, no black presentation margin, no panel divisions, no transparency/checkerboard. Extend the illustration into all four corners: it will be clipped by an existing Unity diamond Mask later. Do NOT draw a diamond shape.
Preserve the selected reference panel's focal subject, story and visual family. Plan for a diamond crop connecting the midpoints of this square: keep the main helmet/face or central castle and important weapon contact within the central 55%, head at x=50%, y=34-39%; sky and expendable scenery in the cropped corners. Camera upright, no tilted artwork.
Authentic low-resolution dark fantasy PIXEL ART, as if carefully hand drawn at 128x128 logical pixels and enlarged with nearest-neighbor. Clearly visible square pixel clusters, sharp stepped diagonals, limited palette around 24-32 colors, solid grouped shadow shapes and 2-4 shade bands, simplified readable silhouettes. Strong art direction, not a pixel filter over a painting. NO antialiasing, smooth gradients, fine noise, photorealistic texture, painterly strokes, blur, bloom, vector art or 3D rendering. Keep mature proportions and gothic mood. Charcoal/blue-black, aged ivory steel, muted oxblood, tarnished gold. The player defends demon castle against HUMAN enemies; no demon horns on human knights.
Selected scene:
Use ONLY the CENTER (normal) panel of concept 02. Center a veteran HUMAN armored knight from waist/thigh up: closed steel helmet with dark cross-shaped visor slit, heavy but plain dark plate armor, dark scarf, pale emblem on chest, angular large shield at viewer right, steel sword angled low across center. Several disciplined spear/helmet silhouettes behind, dim castle skyline and muted crimson eclipse sky. Helmet center about x50%,y36%, broad shoulders no wider than middle 65%; diamond-safe central composition. Chunky plate highlights, no fine chainmail noise.
```

## Hero_Hard

참조: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/02_hero_threat_pixel_v2.png`

```text
Use case: stylized-concept.
Asset type: final Unity UI difficulty artwork, one separate square sprite image. Input image is a STYLE AND COMPOSITION REFERENCE only, not a canvas to reproduce.
Create ONE 1:1 square full-bleed illustration for the specified panel. NO FRAME, no diamond border, no ornament, NO TEXT, no labels, no UI, no black presentation margin, no panel divisions, no transparency/checkerboard. Extend the illustration into all four corners: it will be clipped by an existing Unity diamond Mask later. Do NOT draw a diamond shape.
Preserve the selected reference panel's focal subject, story and visual family. Plan for a diamond crop connecting the midpoints of this square: keep the main helmet/face or central castle and important weapon contact within the central 55%, head at x=50%, y=34-39%; sky and expendable scenery in the cropped corners. Camera upright, no tilted artwork.
Authentic low-resolution dark fantasy PIXEL ART, as if carefully hand drawn at 128x128 logical pixels and enlarged with nearest-neighbor. Clearly visible square pixel clusters, sharp stepped diagonals, limited palette around 24-32 colors, solid grouped shadow shapes and 2-4 shade bands, simplified readable silhouettes. Strong art direction, not a pixel filter over a painting. NO antialiasing, smooth gradients, fine noise, photorealistic texture, painterly strokes, blur, bloom, vector art or 3D rendering. Keep mature proportions and gothic mood. Charcoal/blue-black, aged ivory steel, muted oxblood, tarnished gold. The player defends demon castle against HUMAN enemies; no demon horns on human knights.
Selected scene:
Use ONLY the RIGHT (hard) panel of concept 02. Center imposing HUMAN paladin commander from thigh up: ornate aged-ivory heavy plate armor with sparse tarnished gold borders, closed ivory/gold helm, long pale tabard, both hands resting on a downward-pointing greatsword perfectly centered. Narrow geometric pixel sun/halo sigil behind helm, subdued elite knight ranks behind and muted crimson sky. Helmet center x50%,y36%, halo fits in middle 55%; plate silhouette clearly stronger than normal. No crown spikes/demon horns; preserve solemn human paladin identity.
```
