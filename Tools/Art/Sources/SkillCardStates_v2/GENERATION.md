# 스킬 카드 호버/선택 프레임 v2

- 도구: 내장 image_gen (CLI 미사용).
- 편집 기준: Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites/Card_Normal.png.
- 원본: Card_Hover_Amber_Source.png / Card_Selected_Gold_Source.png.
- 상태: 2026-09-20 사용자 승인 후 `Canvas_SkillSettings.prefab`의 목록 카드 12개와 상세 프레임에 적용 완료.
- 체크 배경을 투명화하고 원본 카드의 외곽 범위를 넘지 않도록 비율 유지 축소하여 192×192로 정렬했다. 생성 원본과 기존 보라색 프레임은 보존한다.
- 최종 이미지: `Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites/Card_Hover_Amber.png`, `Card_Selected_Gold.png`.
- 재현: 저장소 루트에서 `node Tools/Art/BuildSkillCardStateAssets.cjs` (Node.js/Sharp 필요). 색·문양을 다시 그리지 않고 배경 제거·크기 정렬만 한다. 처리 좌표는 `Processing.json`에 기록한다.
- Unity Import: Single Sprite, Point, mipmap off, Uncompressed, Full Rect. .meta는 Unity가 생성했다.
- 검증: Play Mode 42개 검사 및 Raycast 통과. 호버 진입/이탈, 클릭 선택, 마우스 이탈/재누름 시 선택 유지, 선택 대상 변경, 비활성 우선, 상세 프레임 색 확인. Console 오류·경고 0개.
- 적용 화면: `Tools/Art/Previews/SkillSettings_v2_AmberGold.png` (첫 카드 호버 + 두 번째 행 중앙 카드 선택).

## Hover — 사용한 프롬프트

Use case: precise-object-edit.
Input image 1 is the exact edit target: an existing square dark-fantasy pixel-art skill-card frame for a Unity UI.
Create ONLY the mouse-hover variant of this SAME frame. Preserve its original thin border geometry, dimensions, corner shapes, top-centered small diamond, blank dark charcoal textured interior, and the footprint in the square canvas. Recolor the ivory/red border into a restrained warm burnt-amber/orange aged bronze: warm copper midtones around #AD703F, muted amber highlights around #D69B59, brown-black shadows. The hover should clearly read orange, never purple or lemon yellow, and remain tasteful in a dark gothic game. Keep the border the SAME thin thickness as the reference. No new inner outline in this hover state.
Crisp visibly stepped pixel edges; do not smooth or turn it into glossy vector UI. Single isolated frame, no skill icon or text or numerals or caption, no glow spilling outside the border, no additional ornaments, no perspective. Keep the interior dark and opaque. Outside the frame must be genuinely transparent alpha, no baked checkerboard and no opaque background. Preserve the reference's alignment and margins. Output one square frame only.

## Selected — 사용한 프롬프트

Use case: precise-object-edit.
Input image 1 is the exact edit target: an existing square dark-fantasy pixel-art skill-card frame for a Unity UI.
Create ONLY the clicked/selected variant of this SAME frame. Preserve its original outer footprint, corner shapes, top-centered small diamond, blank dark charcoal textured interior, and the footprint in the square canvas. Replace the ivory/red border accents with regal OLD GOLD / muted yellow: antique gold #BCA261, pale aged golden highlights #E8CE86, dark ochre shadows #6D542B. It must read yellow/gold, NOT purple, NOT orange/copper, NOT neon lemon.
Crucial requested change: add ONE additional clearly visible continuous golden border layer immediately INSIDE the existing border, expanding inward only, approximately doubling the total border thickness compared with the reference while leaving ample central empty icon space. The outside dimensions must NOT get larger. Keep the two concentric layers coherent, with a very narrow dark seam between them and matching angular corners; this is a strong selected state, not a diffuse glow.
Crisp visibly stepped pixel edges; do not smooth or turn it into glossy vector UI. Single isolated frame, no skill icon or text or numerals or caption, no exterior glow or extra ornaments, no perspective. Interior dark and opaque; only outside frame genuinely transparent alpha, no baked checkerboard and no opaque background. Preserve the reference alignment and margins. Output one square frame only.
