# 로비 난이도 도트 스타일 2차 생성 기록

- 요청: 기존 01·02·04 비교판의 구도와 난이도 차이를 유지하고 도트 스타일을 강화한다.
- 방식: 내장 `image_gen`, 각 비교판별 style-transfer 1회. CLI/코드 이미지 필터 미사용.
- Image 1: 각 항목의 기존 v1 비교판(편집 대상).
- Image 2: `D:/GitHub/OZGL2_1/Temp/LevelHud/LevelHud_Slider_Play.png` (도트 스타일 참조만).
- 보관 위치: `Docs/UI/References/LobbyDifficulty/`, 기존 v1 보존.
- 아직 게임용 Sprite나 씬에 적용하지 않는 비교 시안이다.

## 결과 확인

- 01·02·04 각 1개씩 총 3개 비교판을 생성했다. 각 비교판 안은 왼쪽부터 쉬움 / 보통 / 어려움이다.
- v1 대비 구름·갑옷·성벽의 색면과 계단형 윤곽을 단순화했다. 난이도 순서, 주요 피사체, 마름모 구도를 시각 확인했다.
- 실제 Unity Mask 연결 및 Play Mode 검증은 하지 않았다. Scene/Prefab/Runtime 무변경.
- 원본 생성 폴더: `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/`.
- 01 원본: `exec-23e2291d-07f2-44c0-8e98-e3ac68d81fdf.png`.
- 02 원본: `exec-7f88a0a3-69e7-48ac-9d6d-a4474ad2d916.png`.
- 04 원본: `exec-c0f7993a-f49c-4f90-aceb-e8c33a07fb37.png`.
- 결과 3개를 위 보관 위치에 변경 없이 복사했으며, 이전 v1 및 생성 원본은 보존했다.

## 01. 마왕성 공성전

- 입력: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/01_castle_siege_v1.png`
- 출력: `01_castle_siege_pixel_v2.png`

```text
Use case: style-transfer.
Asset type: revised dark fantasy game pixel-art difficulty reference board.
Image 1 is the EDIT TARGET: preserve its three-panel concept, subject identities, camera/framing, left-to-right difficulty progression, and diamond frame layout. Image 2 is ONLY the existing Unity lobby STYLE REFERENCE: match its visibly chunky pixel art, NOT its UI layout. Do not add its HUD or buttons.
Primary request: REDRAW the entire first board as authentic hand-placed LOW-RESOLUTION PIXEL ART. The previous version looks too much like a detailed painted illustration. The new version MUST look unmistakably like native retro game art at normal viewing size: each diamond scene should feel drawn on a roughly 128x128 to 144x144 logical pixel canvas and then enlarged 4x using nearest-neighbor. Clearly visible large square pixel clusters and regular stepped diagonals throughout characters, stone, clouds, fire, ornaments and outlines. Do not simulate pixels with a noise filter over a realistic painting. Simplify shapes and remove micro-detail; use large intentional color clusters, 2-4 flat shadow/highlight bands per material, a limited shared palette of about 24-32 colors, strong silhouette separation, selectively placed 1-logical-pixel highlights. Crisp square pixels, no smoothing, no antialiasing, no soft gradients, no lens effects, no blur, no painterly brushwork, no realistic material grain, no dense dithering.
Keep mature gothic dark fantasy seriousness: charcoal/blue-black stone, muted oxblood and rust red skies, aged ivory metal, restrained tarnished gold. No cute/chibi bodies, no bright candy colors, no plastic 3D, no glossy fantasy illustration. Preserve adult proportions but simplify small faces to clear helmet shapes.
Composition: wide landscape board on solid very dark charcoal, THREE equally sized upright diamond frames in a straight horizontal row, all top and bottom tips aligned, clear gutters and generous outside margins. Same thin antique ivory metal frame design with small geometric ornaments on all cards, now drawn with sharp pixel steps. Artwork fills diamond fully. Important heads, towers and weapons stay safely inside frame, concentrate main subject in central 55%. Keep background detail subordinate and readable at small game thumbnail size.
Text only outside frames, crisp Korean pixel typography. Left label exactly "쉬움", center "보통", right "어려움". Retain concept number/title given below, no extra captions. No watermarks, no locks, no controls. Pixel styling applies to entire board, including frames and lettering.
The player defends the demon castle; increasing difficulty means stronger HUMAN invaders. Do not change this narrative. Change rendering style only; preserve the selected concept.
Concept-specific invariants:
Preserve concept 01's castle/causeway view: SAME recognizable black demon citadel centered in each diamond, central tall tower, bridge leading from bottom tip toward gate. Left: grey overcast sky, few small scouts on bridge, quiet open space. Middle: dark red sky, organized knight column, several banners and torch clusters. Right: deep crimson eclipse sky, crowded elite siege army and two readable siege-engine silhouettes. Use boldly pixelated castle masses, simple red rectangular windows, stepped cloud patches and distinct small sprite-like soldiers rather than many realistic people. Do not increase the player's castle power across difficulties. Board title exactly "01. 마왕성 공성전".
```

## 02. 용사군의 위협

- 입력: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/02_hero_threat_v1.png`
- 출력: `02_hero_threat_pixel_v2.png`

```text
Use case: style-transfer.
Asset type: revised dark fantasy game pixel-art difficulty reference board.
Image 1 is the EDIT TARGET: preserve its three-panel concept, subject identities, camera/framing, left-to-right difficulty progression, and diamond frame layout. Image 2 is ONLY the existing Unity lobby STYLE REFERENCE: match its visibly chunky pixel art, NOT its UI layout. Do not add its HUD or buttons.
Primary request: REDRAW the entire first board as authentic hand-placed LOW-RESOLUTION PIXEL ART. The previous version looks too much like a detailed painted illustration. The new version MUST look unmistakably like native retro game art at normal viewing size: each diamond scene should feel drawn on a roughly 128x128 to 144x144 logical pixel canvas and then enlarged 4x using nearest-neighbor. Clearly visible large square pixel clusters and regular stepped diagonals throughout characters, stone, clouds, fire, ornaments and outlines. Do not simulate pixels with a noise filter over a realistic painting. Simplify shapes and remove micro-detail; use large intentional color clusters, 2-4 flat shadow/highlight bands per material, a limited shared palette of about 24-32 colors, strong silhouette separation, selectively placed 1-logical-pixel highlights. Crisp square pixels, no smoothing, no antialiasing, no soft gradients, no lens effects, no blur, no painterly brushwork, no realistic material grain, no dense dithering.
Keep mature gothic dark fantasy seriousness: charcoal/blue-black stone, muted oxblood and rust red skies, aged ivory metal, restrained tarnished gold. No cute/chibi bodies, no bright candy colors, no plastic 3D, no glossy fantasy illustration. Preserve adult proportions but simplify small faces to clear helmet shapes.
Composition: wide landscape board on solid very dark charcoal, THREE equally sized upright diamond frames in a straight horizontal row, all top and bottom tips aligned, clear gutters and generous outside margins. Same thin antique ivory metal frame design with small geometric ornaments on all cards, now drawn with sharp pixel steps. Artwork fills diamond fully. Important heads, towers and weapons stay safely inside frame, concentrate main subject in central 55%. Keep background detail subordinate and readable at small game thumbnail size.
Text only outside frames, crisp Korean pixel typography. Left label exactly "쉬움", center "보통", right "어려움". Retain concept number/title given below, no extra captions. No watermarks, no locks, no controls. Pixel styling applies to entire board, including frames and lettering.
The player defends the demon castle; increasing difficulty means stronger HUMAN invaders. Do not change this narrative. Change rendering style only; preserve the selected concept.
Concept-specific invariants:
Preserve concept 02's centered human-enemy portraits from mid-thigh upward. Left: modest human footsoldier, simple iron helmet, small round wooden shield, short sword; two tiny scouts behind. Middle: veteran armored knight, heavier plate, larger shield, sword, rows of spears behind. Right: elite human paladin commander, aged ivory plate with restrained gold edging, both hands around a downward-pointing greatsword, geometric pixel sun-sigil behind helm, elite regiment behind. Strongly simplify chainmail into 2-3 connected color masses and armor into readable angular pixel planes, not hundreds of glittering marks. No demon horns, no anime/chibi faces. Keep all heads and weapon focal points inside diamonds. Board title exactly "02. 용사군의 위협".
```

## 04. 성문을 향한 진군

- 입력: `D:/GitHub/OZGL2_1/Docs/UI/References/LobbyDifficulty/04_gate_march_v1.png`
- 출력: `04_gate_march_pixel_v2.png`

```text
Use case: style-transfer.
Asset type: revised dark fantasy game pixel-art difficulty reference board.
Image 1 is the EDIT TARGET: preserve its three-panel concept, subject identities, camera/framing, left-to-right difficulty progression, and diamond frame layout. Image 2 is ONLY the existing Unity lobby STYLE REFERENCE: match its visibly chunky pixel art, NOT its UI layout. Do not add its HUD or buttons.
Primary request: REDRAW the entire first board as authentic hand-placed LOW-RESOLUTION PIXEL ART. The previous version looks too much like a detailed painted illustration. The new version MUST look unmistakably like native retro game art at normal viewing size: each diamond scene should feel drawn on a roughly 128x128 to 144x144 logical pixel canvas and then enlarged 4x using nearest-neighbor. Clearly visible large square pixel clusters and regular stepped diagonals throughout characters, stone, clouds, fire, ornaments and outlines. Do not simulate pixels with a noise filter over a realistic painting. Simplify shapes and remove micro-detail; use large intentional color clusters, 2-4 flat shadow/highlight bands per material, a limited shared palette of about 24-32 colors, strong silhouette separation, selectively placed 1-logical-pixel highlights. Crisp square pixels, no smoothing, no antialiasing, no soft gradients, no lens effects, no blur, no painterly brushwork, no realistic material grain, no dense dithering.
Keep mature gothic dark fantasy seriousness: charcoal/blue-black stone, muted oxblood and rust red skies, aged ivory metal, restrained tarnished gold. No cute/chibi bodies, no bright candy colors, no plastic 3D, no glossy fantasy illustration. Preserve adult proportions but simplify small faces to clear helmet shapes.
Composition: wide landscape board on solid very dark charcoal, THREE equally sized upright diamond frames in a straight horizontal row, all top and bottom tips aligned, clear gutters and generous outside margins. Same thin antique ivory metal frame design with small geometric ornaments on all cards, now drawn with sharp pixel steps. Artwork fills diamond fully. Important heads, towers and weapons stay safely inside frame, concentrate main subject in central 55%. Keep background detail subordinate and readable at small game thumbnail size.
Text only outside frames, crisp Korean pixel typography. Left label exactly "쉬움", center "보통", right "어려움". Retain concept number/title given below, no extra captions. No watermarks, no locks, no controls. Pixel styling applies to entire board, including frames and lettering.
The player defends the demon castle; increasing difficulty means stronger HUMAN invaders. Do not change this narrative. Change rendering style only; preserve the selected concept.
Concept-specific invariants:
Preserve concept 04's viewpoint looking FROM INSIDE demon castle gateway out toward an approaching HUMAN invasion: two dark gate piers and muted red banners framing each diamond, road toward upper-middle vanishing point. Left: three isolated scouts on a quiet grey foggy road. Middle: compact advancing shield wall and spear/banner rows. Right: heavily armored paladin at the front, dense elite knight ranks, distant blocky siege tower and muted crimson smoke. Show obvious threat growth by formation silhouettes. Use chunky block-built stone, sparse deliberate window/torch pixels, stepped fog color bands; simplify distant troops into strong group shapes. Keep same viewpoint across three. Board title exactly "04. 성문을 향한 진군".
```
