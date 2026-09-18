# Trait Tree Art v1 — 생성 기록

- 생성: 내장 `image_gen` (CLI/API 별도 호출 없음).
- 참조: 승인된 4방향 트리 시안. 배경/이름판 없는 계열 상징, 일반/특화 프레임 구분 유지.
- 출력 원본은 Source에 보존. 투명 배경 출력 실패 이력 때문에 이번 세트는 단색 크로마 배경을 생성하고, 사용자 승인에 따라 실제 RGBA PNG로 후처리했습니다.
- `Tools/Art/BuildTraitTreeArt.cjs`가 배경 제거, 경계 잔광 정리, 중심 맞춤, nearest 리샘플링, 개별 PNG/시트/검증 결과를 생성합니다. 신규 리소스만 처리하며 Scene/Prefab/Meta를 직접 쓰지 않습니다.
- 마법진은 단색 가넷 잉크로 경계 혼합색을 정리하고 명도를 알파로 보존했습니다.
- Reference의 전체 트리 PNG는 원래 래스터 이미지를 그대로 누끼한 결과가 아니라, 분리된 신규 아트를 같은 4방향·3경로×3단계 구조로 합성한 이미지입니다. 텍스트는 배치 참고용입니다.

## Icons_Monsters

```text
Use case: stylized-concept. Asset type: production pixel-art game UI ICON-ONLY sprite sheet.
Use the reference solely to match its icon language: flat 2D strong pixel silhouettes, antique warm ivory, subtle dark contour, restrained dark fantasy, very simple at small size. NOT a screenshot. NO castle, no tree layout, no text, no letters, no numerals, NO enclosing borders, diamonds, tiles, panels, circles, nameplates, cards or connector lines. The symbols themselves alone must be separate cutouts. Intrinsic circular symbols like coins or magic spirals are allowed; enclosing icon frames are forbidden.
Arrange EXACTLY TEN distinct isolated symbols in a strict uniform 5-column by 2-row grid, read left to right top row then bottom row. Wide canvas 2560x1024. Each symbol centered inside its equal cell with generous clear margins; no overlaps or touching. Icons occupy about 60% of their cell. Same visual stroke thickness and optical size. No grid lines or index numbers.
Background: entirely flat pure chroma-key green #00FF00, absolutely no gradients, shadows outside icons, texture, checkers, glow or vignette. It is a temporary extraction matte and must not affect icon colors. Icons must not use green. Use muted warm ivory #E3D6BA, charcoal outlines and sparse muted beige shading. Hard crisp pixel edges, no smooth painterly rendering or glossy 3D. Empty gaps within icon silhouettes also pure green. This will be removed to real alpha afterward.
Semantic names below identify glyphs only; DO NOT print any words.
Order row1:
1 Sharpness/예리함: one slim diagonal sword.
2 Onslaught/맹공: two crossed heavier swords, different silhouette from 1.
3 Execution/처형 본능: brutal headsman's axe.
4 Toughness/강건함: compact muscular torso with raised arms.
5 Undying flesh/불굴의 살: large simple solid heart.
Order row2:
6 Iron armor/철갑: upright heraldic shield, this is the symbol itself NOT a surrounding frame.
7 Agility/민첩: a winged boot.
8 Repeated strikes/연격: three diagonal parallel striking blades.
9 Piercing volley/관통 사격: three upright arrows spreading into a volley.
10 Legion advance/마왕군의 진격: dramatic five-point infernal flame/crown silhouette, ultimate trait emblem.
Ten glyphs only, no labels.
```

## Icons_Curses

```text
Use case: stylized-concept. Asset type: production pixel-art game UI ICON-ONLY sprite sheet.
Use the reference solely to match its icon language: flat 2D strong pixel silhouettes, antique warm ivory, subtle dark contour, restrained dark fantasy, very simple at small size. NOT a screenshot. NO castle, no tree layout, no text, no letters, no numerals, NO enclosing borders, diamonds, tiles, panels, circles, nameplates, cards or connector lines. The symbols themselves alone must be separate cutouts. Intrinsic circular symbols like coins or magic spirals are allowed; enclosing icon frames are forbidden.
Arrange EXACTLY TEN distinct isolated symbols in a strict uniform 5-column by 2-row grid, read left to right top row then bottom row. Wide canvas 2560x1024. Each symbol centered inside its equal cell with generous clear margins; no overlaps or touching. Icons occupy about 60% of their cell. Same visual stroke thickness and optical size. No grid lines or index numbers.
Background: entirely flat pure chroma-key green #00FF00, absolutely no gradients, shadows outside icons, texture, checkers, glow or vignette. It is a temporary extraction matte and must not affect icon colors. Icons must not use green. Use muted warm ivory #E3D6BA, charcoal outlines and sparse muted beige shading. Hard crisp pixel edges, no smooth painterly rendering or glossy 3D. Empty gaps within icon silhouettes also pure green. This will be removed to real alpha afterward.
Semantic names below identify glyphs only; DO NOT print any words.
Order row1:
1 Weakness/쇠약: simple human skull.
2 Powerlessness/무력화: three slack chain links.
3 Lethargy/나태: crouched slumped human silhouette.
4 Disarm/무장 해제: shield split by a vertical crack; shield is the icon itself, no outer frame.
5 Shatter/파쇄: downward jagged impact burst with broken armor shards.
Order row2:
6 Vulnerability mark/취약 각인: open eye with vertical slit pupil.
7 Slowness/둔족: heavy weighted boot.
8 Quagmire/수렁: a simple inward mud whirlpool spiral.
9 Mire/진창: low clump of muddy sludge with bubbles.
10 Despair brand/절망의 낙인: imposing curse eye with short outward spines. NOT contained in a surrounding diamond or border.
Ten glyphs only, no labels.
```

## Icons_Spells

```text
Use case: stylized-concept. Asset type: production pixel-art game UI ICON-ONLY sprite sheet.
Use the reference solely to match its icon language: flat 2D strong pixel silhouettes, antique warm ivory, subtle dark contour, restrained dark fantasy, very simple at small size. NOT a screenshot. NO castle, no tree layout, no text, no letters, no numerals, NO enclosing borders, diamonds, tiles, panels, circles, nameplates, cards or connector lines. The symbols themselves alone must be separate cutouts. Intrinsic circular symbols like coins or magic spirals are allowed; enclosing icon frames are forbidden.
Arrange EXACTLY TEN distinct isolated symbols in a strict uniform 5-column by 2-row grid, read left to right top row then bottom row. Wide canvas 2560x1024. Each symbol centered inside its equal cell with generous clear margins; no overlaps or touching. Icons occupy about 60% of their cell. Same visual stroke thickness and optical size. No grid lines or index numbers.
Background: entirely flat pure chroma-key green #00FF00, absolutely no gradients, shadows outside icons, texture, checkers, glow or vignette. It is a temporary extraction matte and must not affect icon colors. Icons must not use green. Use muted warm ivory #E3D6BA, charcoal outlines and sparse muted beige shading. Hard crisp pixel edges, no smooth painterly rendering or glossy 3D. Empty gaps within icon silhouettes also pure green. This will be removed to real alpha afterward.
Semantic names below identify glyphs only; DO NOT print any words.
Order row1:
1 Essence of ruin/파괴의 정수: compact sharp arcane starburst.
2 Precise spell/정밀 주문: a magical targeting crosshair; its circle is intrinsic to the glyph, no enclosing UI frame.
3 Magic penetration/마력 관통: three jagged magic lances piercing upward.
4 Swift spell/신속한 주문: one feather/wing with speed notches.
5 Acceleration/가속: simple hourglass.
Order row2:
6 Time distortion/시간 왜곡: clean winding vortex spiral.
7 Area dominion/광역 지배: three simple pawn/human silhouettes standing together.
8 Legion's shout/군단의 함성: a military standard waving on a pole.
9 Additional command/추가 지령: three upright command spearheads or trident-like formation.
10 Demon King's power/마왕의 권능: bold sophisticated swirling three-arm arcane vortex with small star at center. NO enclosing UI border.
Ten glyphs only, no labels.
```

## Icons_Growth

```text
Use case: stylized-concept. Asset type: production pixel-art game UI ICON-ONLY sprite sheet.
Use the reference solely to match its icon language: flat 2D strong pixel silhouettes, antique warm ivory, subtle dark contour, restrained dark fantasy, very simple at small size. NOT a screenshot. NO castle, no tree layout, no text, no letters, no numerals, NO enclosing borders, diamonds, tiles, panels, circles, nameplates, cards or connector lines. The symbols themselves alone must be separate cutouts. Intrinsic circular symbols like coins or magic spirals are allowed; enclosing icon frames are forbidden.
Arrange EXACTLY TEN distinct isolated symbols in a strict uniform 5-column by 2-row grid, read left to right top row then bottom row. Wide canvas 2560x1024. Each symbol centered inside its equal cell with generous clear margins; no overlaps or touching. Icons occupy about 60% of their cell. Same visual stroke thickness and optical size. No grid lines or index numbers.
Background: entirely flat pure chroma-key green #00FF00, absolutely no gradients, shadows outside icons, texture, checkers, glow or vignette. It is a temporary extraction matte and must not affect icon colors. Icons must not use green. Use muted warm ivory #E3D6BA, charcoal outlines and sparse muted beige shading. Hard crisp pixel edges, no smooth painterly rendering or glossy 3D. Empty gaps within icon silhouettes also pure green. This will be removed to real alpha afterward.
Semantic names below identify glyphs only; DO NOT print any words.
Order row1:
1 Insight/통찰: human head profile containing a small brain/spiral.
2 Deep insight/심층 통찰: open wise eye.
3 Plunder/약탈: a small closed treasure chest.
4 Fast growth/속성 성장: a clean rising inner flame.
5 Experience crystal/숙련의 결정: one faceted upright crystal.
Order row2:
6 Teaching/가르침: ancient parchment scroll with short horizontal marks that are NOT readable text.
7 Expansion/규모 확장: three unfolded banner/wall segments expanding sideways.
8 Efficient formation/효율 편성: four outward directional arrows arranged as a cross, open center.
9 Great army/대군세: three armored soldiers, distinguish from simple pawns by their helmets.
10 Ruler's wisdom/지배자의 지혜: a regal five-point crown, strong capstone silhouette.
Ten glyphs only, no labels.
```

## Icons_Core

```text
Use case: stylized-concept. Produce an ICON-ONLY production sprite sheet of exactly FIVE standalone pixel-art emblems matching the reference tree, NOT a tree screenshot.
Canvas1536x1024, strict grid3columns by2rows. First row3icons, second row2icons with lastcell completely empty. Center each inside its cell with 20%clear margin.
Row1 left: horned Demon King's crowned skull face, front-facing severe angular silhouette, black charcoal inner face and muted garnet crimson outer horns/flames, similar to reference central icon but WITHOUT its diamond.
Row1 middle: army training category, one horned battle helmet with crossed blades behind it, muted moss jade green and ivory.
Row1 right: wisdom/growth category, regal crown above two coin stacks, aged muted brass gold and ivory.
Row2 left: spell research category, open grimoire with small arcane flame rising above it, muted slate blue and ivory.
Row2 middle: curse category, broken human helmet side profile with floating curse eye next to it, muted amethyst violet and ivory.
Row2 right: NOTHING, flat green.
These are FREE-STANDING heraldic silhouettes, not enclosed emblems. NO enclosing diamond, circular medallion, frame, border, shield backing, box, decorative bracket, ribbon, plaque, text or numbers. Preserve actual crown/book/helmet shapes.
Flat crisp 2D pixel art with strong simple contours, restrained antique colors, no glossy 3D, low-detail forms readable at128px.
BACKGROUND is perfectly uniform pure #00FF00 chroma key, including open gaps inside emblems. No gradients, shadows, checkers, vignette or glow. The actual jade armor is DESATURATED gray-green, never chroma-green. Production art for later alpha extraction.
```

## Frames

```text
Use case: stylized-concept. Produce a production EMPTY FRAME sprite sheet, exactly9 empty diamond frames in a strict3column x3row grid on a square1536x1536 canvas.
Reference is style only: crisp pixel-art dark fantasy, antique metals, low-saturation green/gold/blue/purple, charcoal inlays. These frames will receive separate icon images afterward, so their centers must be COMPLETELY EMPTY: NO icons, NO glyphs, NO emblems, NO letters, NO numbers, NO labels.
Row1 left NORMAL muted jade-green frame; middle NORMAL antique brass-gold; right NORMAL slate sapphire-blue.
Row2 left NORMAL muted amethyst-purple; middle SPECIALIZED ornate jade-green; right SPECIALIZED ornate brass-gold.
Row3 left SPECIALIZED ornate slate-blue; middle SPECIALIZED ornate amethyst-purple; right CENTRAL DEMON KING garnet-red ornate diamond.
NORMAL design: thin beveled diamond perimeter, tiny simple pointed corner details, antique ivory edge highlights, empty charcoal black inlay. All4 normal frames IDENTICAL geometry.
SPECIALIZED design: same basic diamond but stronger double perimeter with small gem-diamond ornaments on each of4corners and short outward tips. More ornate than normals but not baroque or bulky. All4 specialized frames IDENTICAL geometry, only color changes. Geometric center aperture kept large to receive icons.
CENTRAL frame: larger-looking layered red diamond,4ivory corner highlights, same geometric diamond language, empty deepblack interior; no demon head inside.
Each occupies65-70%of equal cell, precise centered sameextent and plentiful padding. No connecting lines, no overlap, no extra decorations between cells.
Flat front view, highly visible crisp pixel steps, no perspective, no smooth 3D or gloss.
BACKGROUND pure flat #00FF00 green for extraction; no shadows, no gradients, no checkers. ONLY exterior background green; inside diamond inlays stay black. Green frame metal must be desaturated (#657B62 type), not bright chromagreen. Nine empty frames, nothing else.
```

## MagicCircle

```text
Use case: stylized-concept. Asset type: isolated background UI ornament for the skill tree.
Reconstruct ONLY the faint arcane magic circle that lay behind the central Demon King in the reference. A centered complete symmetrical gothic ritual sigil, thin nested circular rings, interlaced geometric diamonds and subtle radial ticks, restrained small invented rune strokes. Empty center. Full circular outline visible with8%margin, no cropped perimeter. Square1024x1024.
Pixel-art lines with precise stepped edges. Muted dark garnet red (#713238 range), consistent ink. Enough opacity for clean extraction; opacity will be reduced in Unity. Flat2D, no perspective, no lighting, no glow, no blurred haze, no black filled disk or vignette. All gaps and central open area are background, only actual red linework is foreground.
ABSOLUTELY NO icons, skulls, demon faces, trait nodes, tree branches, text labels or numbers; NO scene/castles.
Perfectly uniform pure chroma key GREEN #00FF00 background so red linework can be extracted cleanly to real alpha. No checkers or textures. Single independent magic-circle sprite only.
```

## Frames 최종 수정

두 번째 행 왼쪽의 보라색 일반 프레임만 첫 번째 행의 일반 프레임과 동일한 얇은 단일 테두리와 작은 모서리 장식으로 변경. 다른 8개, 검은 내부, 녹색 배경 유지. 최종 수정본이 Source/Frames_Chroma.png입니다.

