# Wave frame filled background

- Mode: built-in image_gen / precise-object-edit / transparent_background=true.
- Edit target: Assets/06.UI/BattleMutedPreview/Reference_v2/Frame_Wave.png.
- Selected generated original: Frame_Wave_Filled_Generated.png (2168 x 725).
- Preserve existing source and Prefab connection. This request produces a separate replacement PNG only.
- Selected generated interior alpha before postprocessing: 253-254/255 in the inspected main content rectangle; exterior corner alpha 0.
- User approved internal alpha correction. `Tools/Art/MakeWaveFrameInteriorOpaque.ps1` raises the enclosed area to alpha 255 while preserving RGB, dimensions, exterior transparency and an 8px outer edge band.
- Final Sprite: `Assets/06.UI/BattleMutedPreview/Reference_v2/Frame_Wave_Filled_v1.png` (2168 x 725; Point filter, center pivot, uncompressed, no mipmaps).
- Validation: inspected main content rectangle (190,210)-(1949,594) alpha is 255 throughout; exterior corner alpha remains 0. The source Frame_Wave.png and existing Prefab references are unchanged.

## Initial prompt

Use case: precise-object-edit
Asset type: Unity gothic pixel-art WavePreviewPanel frame sprite, wide horizontal panel.
Input image 1 is the EDIT TARGET, the exact current Frame_Wave.png sprite. Preserve this existing design, not a redesign.
Primary request: Fill ALL transparent areas INSIDE the enclosed outer frame with fully opaque nearly-black charcoal (#100e10, very slight warm burgundy undertone), so gameplay cannot show through the three main content cells. Keep the already filled dark-burgundy header intact. Fill to the inner edges continuously: no transparent holes in the panel interior. The fill should be quiet and dark, almost uniform with at most extremely subtle fine grain, suitable behind separately-rendered white labels and unit sprites.
Invariants: preserve the outer silhouette, the wide approximately 3.05:1 aspect ratio, thin ivory segmented outer rails, red inner outline, existing ornamental corner motifs, both small header diamonds, horizontal header divider and both vertical cell separators in exactly the same relative positions and thickness. Keep the original restrained black/red/ivory pixel-art palette and crisp pixel edges. Do not brighten or enlarge the frame. Do not create new embellishments.
Transparency: ONLY pixels OUTSIDE the outer decorative frame silhouette are genuinely transparent alpha=0; all enclosed panel background is opaque alpha=255. No checkerboard drawn into the art.
Composition: a single complete wide panel, front view, same framing as the input, tightly framed with minimal outer transparent padding, no cropped tips. No text, no numerals, no unit icons, no UI mockup, no backdrop or cast shadow.

## Final correction prompt

Use case: precise-object-edit. Edit this single supplied gothic frame sprite only to FIX INTERIOR OPACITY. Preserve the entire existing red/ivory border, header diamonds, dividers, proportions and positions. Every enclosed surface inside the border must be a SOLID OPAQUE sheet, including the left, middle and right cells. Fill the three cells with uniform dark warm charcoal-burgundy RGB(27,18,20) at alpha 255. There is currently an unwanted transparent irregular patch low in the left cell: remove that patch completely by filling it with exactly the same opaque charcoal-burgundy as the rest of all three cells. No transparency, no translucent haze, no holes anywhere INSIDE the enclosing frame. ONLY the area OUTSIDE the outermost decorative frame silhouette must retain transparent alpha zero. Treat the dark background inside as a physical opaque material, NOT something to remove. No added textures, icons, lettering, shapes or decoration. Keep this very wide game panel in the same framing and aspect ratio.
