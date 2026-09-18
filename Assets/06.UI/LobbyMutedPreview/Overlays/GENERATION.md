# 메뉴/특성 Overlay 리소스 생성 기록

- 방식: 내장 image_gen, 에셋별 개별 생성.
- 스타일: 기존 저채도 픽셀 다크판타지. 문구/숫자는 TMP로 별도 표시.
- 투명 요청에서 배경 체크무늬가 이미지로 생성되어, 같은 디자인의 외곽만 크로마 녹색으로 재생성했습니다. Unity Editor에서 녹색을 알파로 추출하며 원본은 Source에 보존합니다.

## Panel_Frame

원본 생성 프롬프트:

A single reusable rectangular gothic modal panel sprite. Square canvas. Front-on, square corners, very thin worn ivory pixel border, small muted garnet diamond finials only at four corners. Almost black subtly mottled opaque interior, broad completely empty content area covering 80 percent. No header ornament attached, no text, no buttons, no icons. Entire outer silhouette surrounded by genuinely transparent alpha background, not a checkerboard drawing. Designed for nine-slice: all corner decoration confined to outer 12 percent; straight undecorated repeatable middle edges. Low saturation antique luxury dark fantasy, crisp chunky pixel art, restrained not ornate baroque.

최종 배경 교체 프롬프트:

Use case: background-extraction / precise-object-edit. This image is the edit target. Keep EXACTLY the same single UI sprite silhouette, geometry, ornaments, empty dark interior, resolution, and pixel style. Replace ONLY the exterior background, including all checkerboard texture and all background specks, with perfectly flat pure chroma green #00FF00 for Unity asset extraction. Do not draw a transparency checkerboard. Preserve every black outline, ivory edge, and garnet pixel of the foreground. The black interior of the frame must stay opaque black. No text, no other changes, no extra objects. Chroma green exists only in the exterior background. 

저장 경로: `Source/Panel_Frame.png` → `Sprites/Panel_Frame.png`

## Button_Frame

원본 생성 프롬프트:

A single very wide horizontal gothic button plate, width to height about 4 to 1, centered. Thin stepped ivory outline with small muted garnet diamond ornaments at left and right tips, black opaque empty interior. No words, no icons, no symbols in center. Genuinely transparent alpha exterior, not checkerboard artwork. Strong crisp pixel art staircase edges, antique dark fantasy UI, subdued garnet and bone ivory. Leave large blank label region. No shine/glow/3D bevel.

최종 배경 교체 프롬프트:

Use case: background-extraction / precise-object-edit. This image is the edit target. Keep EXACTLY the same single UI sprite silhouette, geometry, ornaments, empty dark interior, resolution, and pixel style. Replace ONLY the exterior background, including all checkerboard texture and all background specks, with perfectly flat pure chroma green #00FF00 for Unity asset extraction. Do not draw a transparency checkerboard. Preserve every black outline, ivory edge, and garnet pixel of the foreground. The black interior of the frame must stay opaque black. No text, no other changes, no extra objects. Chroma green exists only in the exterior background. 

저장 경로: `Source/Button_Frame.png` → `Sprites/Button_Frame.png`

## Header_Ornament

원본 생성 프롬프트:

A single wide symmetric gothic UI title ornament sprite, about 3 to 1 width to height. Restrained muted garnet thorn-wing silhouette surrounding a central antique ivory four-point occult star inside a thin garnet ring. Thin horizontal flourishes extend left and right terminating in small diamonds. No skull, no text, no panel, no background scene. Genuinely transparent alpha background. Crisp visible chunky pixel clusters. Noble antique low saturation dark fantasy, perfectly front-facing, understated.

최종 배경 교체 프롬프트:

Use case: background-extraction / precise-object-edit. This image is the edit target. Keep EXACTLY the same single UI sprite silhouette, geometry, ornaments, empty dark interior, resolution, and pixel style. Replace ONLY the exterior background, including all checkerboard texture and all background specks, with perfectly flat pure chroma green #00FF00 for Unity asset extraction. Do not draw a transparency checkerboard. Preserve every black outline, ivory edge, and garnet pixel of the foreground. The open spaces inside the ornamental circle and between branches are also background and must be pure green. No text, no other changes, no extra objects. Chroma green exists only in the exterior background. 

저장 경로: `Source/Header_Ornament.png` → `Sprites/Header_Ornament.png`

## Points_Frame

원본 생성 프롬프트:

A single wide horizontal point-counter frame sprite, about 5 to 1 width to height. Black opaque blank content center. Thin worn ivory straight horizontal edges, muted garnet angular bracket ends and small ruby diamond endpoint accents. Separate UI text and icon will be inserted later, leave the entire inside empty. Genuinely transparent alpha outside, no checkerboard drawing, no text, no digits, no icon. Crisp pixel art, old noble dark fantasy, low saturation, no glow.

최종 배경 교체 프롬프트:

Use case: background-extraction / precise-object-edit. This image is the edit target. Keep EXACTLY the same single UI sprite silhouette, geometry, ornaments, empty dark interior, resolution, and pixel style. Replace ONLY the exterior background, including all checkerboard texture and all background specks, with perfectly flat pure chroma green #00FF00 for Unity asset extraction. Do not draw a transparency checkerboard. Preserve every black outline, ivory edge, and garnet pixel of the foreground. The black interior of the frame must stay opaque black. No text, no other changes, no extra objects. Chroma green exists only in the exterior background. 

저장 경로: `Source/Points_Frame.png` → `Sprites/Points_Frame.png`

## Trait_Frame_Normal

원본 생성 프롬프트:

A single EMPTY standard trait node frame sprite, exact diamond (rotated square), centered symmetrical. Thin double worn bone-ivory pixel edge with charcoal inner line, tiny restrained corner tips at the four cardinal vertices. Matte nearly black opaque empty diamond interior, no icon, no letters, no number, no badge, no other items. Genuinely transparent alpha exterior not checkerboard drawing. Crisp large pixel clusters, elegant low-saturation dark fantasy. Wide enough empty center for a separately replaceable icon. Shape like the simple ivory small nodes in reference, not the huge ornate lobby navigation buttons.

최종 배경 교체 프롬프트:

Use case: background-extraction / precise-object-edit. This image is the edit target. Keep EXACTLY the same single UI sprite silhouette, geometry, ornaments, empty dark interior, resolution, and pixel style. Replace ONLY the exterior background, including all checkerboard texture and all background specks, with perfectly flat pure chroma green #00FF00 for Unity asset extraction. Do not draw a transparency checkerboard. Preserve every black outline, ivory edge, and garnet pixel of the foreground. The black interior of the frame must stay opaque black. No text, no other changes, no extra objects. Chroma green exists only in the exterior background. 

저장 경로: `Source/Trait_Frame_Normal.png` → `Sprites/Trait_Frame_Normal.png`

## Trait_Frame_Specialized

원본 생성 프롬프트:

A single EMPTY specialized trait node frame sprite, exact diamond (rotated square), centered symmetrical. More prominent double antique garnet red pixel border with dark burgundy shadows and restrained ivory corner glints. Small geometric ruby ornaments at four tips, modest extra pointed lower tip; no attached rank badge. Matte nearly black opaque empty diamond interior. No icon, no words, no digits, no ability artwork. Genuinely transparent alpha exterior not checkerboard drawing. Same design family as simple normal trait nodes, slightly stronger hierarchy through border thickness and low-saturation garnet, not neon, no glow, no gold. Crisp chunky pixel art.

최종 배경 교체 프롬프트:

Use case: background-extraction / precise-object-edit. This image is the edit target. Keep EXACTLY the same single UI sprite silhouette, geometry, ornaments, empty dark interior, resolution, and pixel style. Replace ONLY the exterior background, including all checkerboard texture and all background specks, with perfectly flat pure chroma green #00FF00 for Unity asset extraction. Do not draw a transparency checkerboard. Preserve every black outline, ivory edge, and garnet pixel of the foreground. The black interior of the frame must stay opaque black. No text, no other changes, no extra objects. Chroma green exists only in the exterior background. Keep red trim muted antique garnet, low saturation matching the reference.

저장 경로: `Source/Trait_Frame_Specialized.png` → `Sprites/Trait_Frame_Specialized.png`


