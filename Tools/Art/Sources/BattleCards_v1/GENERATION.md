# 전투 준비 카드 생성 기록

- 생성 방식: 내장 ImageGen 이미지 편집/생성 도구 (CLI/API 별도 호출 없음).
- 사용자 승인 범위: 프리팹 3종만 준비, 씬 미배치. 생성 원본 보존, 투명 배경 정리·크기·중심 정렬 후처리 허용.
- 기준: 사용자가 제공한 유닛·땅 슬롯·기물 카드 레퍼런스. 모든 이름·설명·라벨·숫자는 별도 Unity Text로 구성.
- 원본: 이 폴더의 `*_Original.png`. 최종 사용: `Assets/06.UI/BattleMutedPreview/Cards_v1/Sprites/`.
- 후처리: `Tools/Art/PrepareBattleCardArt.ps1`. 기존 알파 우선 사용, 바깥 체크 배경 flood fill 제거, 5픽셀 이하 잡점 제거, 경계 자르기, 중심 정렬, 최근접 크기 조정. 프레임 내부/삽화 내용은 코드로 그리지 않음.
- 기물 프레임 초안의 큰 상단 마름모와 산 아이콘 초안의 발광은 ImageGen으로 수정했고 초안도 `*_Draft.png`로 보존.
- 최종 프리팹과 사용법: `Docs/UI/BattlePreparationCards.md`.

## 생성 요청

### CardShell_Unit

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Production pixel-art game UI texture, reference-guided asset generation, not a mockup. Input image is STYLE and LAYOUT REFERENCE only. Generate ONE isolated portrait CARD SHELL with near-black matte inner panels and ivory thin segmented metal rectangular outer rim, jewel nodes, clipped ornamental corners, and restrained coloured inner accents exactly like the matching reference card. Full straight-on orthographic UI, no perspective, crisp deliberate pixels, no blur, noble dark fantasy. The actual shell is 600 wide x 1000 tall, centered with a tiny transparent outer margin. Maintain straight symmetrical parallel sides. Top title/header zone from 0% to 15% height, empty main artwork panel from 15% to 58% height. Completely EMPTY interior regions for runtime Unity artwork and text; opaque near-black interiors with subtle texture but NO grid. No separate large diamond icon, no star badge, no category caption, NO character, NO object, NO tile shapes. ABSOLUTELY NO TEXT, NO LETTERS, NO NUMBERS, NO text-like marks anywhere. Outer background genuinely transparent alpha, not checkerboard. No drop shadow outside. No background scene. Use LEFT unit card, muted crimson / oxblood accents. Bottom regions: empty trait panel from58% to74%, empty owned-skill panel74% to88%, bottom stats band88% to98% divided into3 identical vertical cells with fine ivory separators. Tiny blank arrow-ended label tabs at upper left of trait and skill sections. All panels empty. Header blank burgundy name strip and blank dark upper narrow strip. Do not draw any symbols in any panel. Match the reference's clean fine frame, not huge chunky stone.
```

### CardShell_LandSlot

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Production pixel-art game UI texture, reference-guided asset generation, not a mockup. Input image is STYLE and LAYOUT REFERENCE only. Generate ONE isolated portrait CARD SHELL with near-black matte inner panels and ivory thin segmented metal rectangular outer rim, jewel nodes, clipped ornamental corners, and restrained coloured inner accents exactly like the matching reference card. Full straight-on orthographic UI, no perspective, crisp deliberate pixels, no blur, noble dark fantasy. The actual shell is 600 wide x 1000 tall, centered with a tiny transparent outer margin. Maintain straight symmetrical parallel sides. Top title/header zone from 0% to 15% height, empty main artwork panel from 15% to 58% height. Completely EMPTY interior regions for runtime Unity artwork and text; opaque near-black interiors with subtle texture but NO grid. No separate large diamond icon, no star badge, no category caption, NO character, NO object, NO tile shapes. ABSOLUTELY NO TEXT, NO LETTERS, NO NUMBERS, NO text-like marks anywhere. Outer background genuinely transparent alpha, not checkerboard. No drop shadow outside. No background scene. Use CENTER terrain card, aged antique copper/gold accents. Main artwork zone extends from15% to68% (different from unit card). Bottom description region68% to98%, one spacious blank black panel, with a fine gold horizontal divider at82% and one tiny center diamond. No trait tabs, no stat cells. Completely blank top header, completely blank bottom panel. Match the reference's restrained thin warm metal, not shiny yellow.
```

### CardShell_Relic

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Production pixel-art game UI texture, reference-guided asset generation, not a mockup. Input image is STYLE and LAYOUT REFERENCE only. Generate ONE isolated portrait CARD SHELL with near-black matte inner panels and ivory thin segmented metal rectangular outer rim, jewel nodes, clipped ornamental corners, and restrained coloured inner accents exactly like the matching reference card. Full straight-on orthographic UI, no perspective, crisp deliberate pixels, no blur, noble dark fantasy. The actual shell is 600 wide x 1000 tall, centered with a tiny transparent outer margin. Maintain straight symmetrical parallel sides. Top title/header zone from 0% to 15% height, empty main artwork panel from 15% to 58% height. Completely EMPTY interior regions for runtime Unity artwork and text; opaque near-black interiors with subtle texture but NO grid. No separate large diamond icon, no star badge, no category caption, NO character, NO object, NO tile shapes. ABSOLUTELY NO TEXT, NO LETTERS, NO NUMBERS, NO text-like marks anywhere. Outer background genuinely transparent alpha, not checkerboard. No drop shadow outside. No background scene. Use RIGHT relic card, muted steel-blue / icy cyan accents. Bottom regions: empty trait panel58% to74%, one larger empty owned-skill panel74% to98%. Tiny blank arrow-ended label tabs at upper left of those two sections. NO stats band and NO3 stat cells. Completely blank dark blue top header. Main artwork region15% to58% completely empty. Match reference restrained blue metal, not neon.
```

### Badge_Star

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Production game UI sprite. Reference image is visual reference only. Create ONE empty five-point gold star badge like the upper-right badges of the cards. Warm parchment gold highlights, muted antique gold beveled lower facets and dark crisp outline, refined pixel art. Straight-on, exactly centered symmetric silhouette, flat front surface left EMPTY for a dynamic Unity Text number. The number must NOT be drawn. No text, no digit, no sign, no frame, no glow, no other shapes. Genuinely transparent alpha background (not checkered). Square canvas, silhouette uses 85% canvas with equal margins. Crisp deliberate pixel steps, not blurry and not 3D rendering.
```

### Icon_Sword

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Game UI transparent pictogram. Reference left card sword only is shape/style reference. Create ONE simple upright medieval longsword, tip up, vertical centered, white ivory silhouette, minimal pale grey pixel shading on bevel, cross guard, short straight hilt. Wide enough to read at 32px. Crisp clean chunky pixel-art linework, exactly symmetrical, no glow. No frame or diamond, no text or digits, no star, no background. Genuinely transparent alpha. Portrait canvas, full sword occupies 85% height.
```

### Icon_Mountain

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Game UI transparent pictogram. Reference middle card mountain inside diamond is shape/style reference. Create ONE white ivory stylized angular double mountain peak pictogram with sharp triangular dark cut-outs and broad solid slopes. Two peaks: tall central-right, smaller left. Simple crisp pixel art reads at48px. Centered, landscape silhouette within square canvas, 75% width. No diamond/frame/star, no ground, no scene, no letters/digits. Genuinely transparent alpha background.
```

### Artwork_ShadowSwordsman

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Production game unit card artwork sprite. Reference left card character is exact subject and style reference. ONE small full-body dark hooded shadow swordsman in a standing three-quarter frontal pose, cute stocky proportions, black face with two small red glowing eyes, black layered leather armor with subtle muted crimson trim. Holds a sharp white/silver long sword diagonally down across front pointing lower right. Hood/head near top, both boots fully visible; strong clear silhouette and deliberate crisp pixel-art clusters at native 200px character height, not painterly, not smooth 3D. Dark fantasy polished game sprite. No grid, no tile, no scene, no UI frame, no lettering, no shadow floor, no additional characters. Genuine transparent alpha. Center the visual bounds with empty safety margin 8%, keep sword fully in canvas.
```

### Artwork_ManaAmplifier

참조: C:/Users/sudea/AppData/Local/Temp/codex-clipboard-9732135a-fede-4f9b-b5ca-4b2d08c91f67.png

```text
Use case: stylized-concept. Production game object card artwork sprite. Reference right card tower is exact subject and style reference. ONE isolated dark obsidian mana-amplifier obelisk: narrow tall angular black stone fragments pointing upward, bright white/cyan crystalline core at upper center, few levitating smaller obsidian shards, restrained blue circular magic ring and tiny sparkle, stepped angular base. Isometric three-quarter front consistent with reference. Cyan thin ground-diamond rune may remain as part of sprite, no checker grid. Crisp high-quality pixel art clusters, readable at200px tall, dark charcoal material and cold blue highlights. No UI frame/star/text/number, no scene or otherobjects. Genuinely transparent alpha, full silhouette incl. runes centered with8%margin.
```

## 기물 프레임 수정 요청

```text
Use case: precise-object-edit. Input is edit target. Change ONLY this: remove the two large blue diamond-shaped frames stuck to the upper-left and upper-right corners of this blank card. Reconstruct the thin continuous straight horizontal ivory and blue top border and its rectangular blank title-strip background where they were. Keep the small centered top crest diamond and all small border nodes, all other borders, proportions, section dividers, colours and textures exactly unchanged. Do NOT add any text, digits, badges, icons or other elements. All interior regions remain completely blank as now. Return same portrait composition with actual transparent background outside the card, no checkerboard.
```

## 산 아이콘 수정 요청

```text
Use case: precise-object-edit. Input is edit target, white mountain game icon. Remove ALL the blurry luminous haze, glow, bloom and soft halo around and inside the mountain icon. Keep the same angular mountain silhouette but its edges must be perfectly hard crisp pixel edges against actual transparent alpha. Flat opaque ivory-white pictogram only; clean negative-space triangular notch. No shading glow, no shadow, no blur, no texture, no frame, no words. Exactly centered. Do not produce another softly glowing icon. Only the solid hard-edged mountain pictogram remains, on truly transparent background.
```

