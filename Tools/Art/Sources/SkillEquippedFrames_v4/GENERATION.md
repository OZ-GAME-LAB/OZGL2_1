# 장착 슬롯 분류 프레임 — 2026-09-21

- 생성: 내장 `image_gen` 편집 모드, 분류별 1회씩 총 3회. CLI/API fallback 미사용.
- 입력: `Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites/Equip_Red.png`.
- 원본: 이 폴더의 `Damage_Source.png`, `Buff_Source.png`, `Debuff_Source.png`. 입력/생성 원본은 덮어쓰지 않는다.
- 결과: `Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites/Equip_{Damage,Buff,Debuff}_Muted.png`.
- 후처리: 사용자 승인 범위의 검은 외부 배경/외곽 잡티 알파 정리, 검은 내부 보존, 256×256 중앙 정렬. RGB 재색칠/아이콘 합성 없음. `Tools/Art/BuildSkillEquippedFrames.py`로 재현한다.
- 임포트: Sprite Single / Full Rect / Point / Compression None / Mip Maps Off / Clamp. TMP/아이콘을 굽지 않는다.

## 실제 전달 프롬프트

### Damage

Use case: precise-object-edit. Asset type: transparent 2D pixel-art Unity equipped-skill slot frame. Input image is the existing empty red diamond frame. Edit only its colored metal borders and corner diamond studs into the requested dark refined category color; preserve the SAME diamond silhouette, border thickness, pixelated edges, symmetrical north/east/south/west small diamond studs, flat front-facing composition and opaque nearly black empty inner diamond. No icon, no text, no extra ornaments, no scene, no neon glow, no drop shadow outside. Keep true alpha transparency OUTSIDE the diamond, never a checkerboard painted into the image. One perfectly centered square sprite with matching equal padding, single frame only. Dark antique fantasy game aesthetic, restrained highlights and dark metal shadows. Color palette: muted oxblood red, shadow #753332, restrained aged rosy-red edge highlights #B97B73.

### Buff

Use case: precise-object-edit. Asset type: transparent 2D pixel-art Unity equipped-skill slot frame. Input image is the existing empty red diamond frame. Edit only its colored metal borders and corner diamond studs into the requested dark refined category color; preserve the SAME diamond silhouette, border thickness, pixelated edges, symmetrical north/east/south/west small diamond studs, flat front-facing composition and opaque nearly black empty inner diamond. No icon, no text, no extra ornaments, no scene, no neon glow, no drop shadow outside. Keep true alpha transparency OUTSIDE the diamond, never a checkerboard painted into the image. One perfectly centered square sprite with matching equal padding, single frame only. Dark antique fantasy game aesthetic, restrained highlights and dark metal shadows. Color palette: aged antique gold / tarnished brass, shadow #7A6138, muted old gold edge highlights #B79B65. Not yellow neon, not orange.

### Debuff

Use case: precise-object-edit. Asset type: transparent 2D pixel-art Unity equipped-skill slot frame. Input image is the existing empty red diamond frame. Edit only its colored metal borders and corner diamond studs into the requested dark refined category color; preserve the SAME diamond silhouette, border thickness, pixelated edges, symmetrical north/east/south/west small diamond studs, flat front-facing composition and opaque nearly black empty inner diamond. No icon, no text, no extra ornaments, no scene, no neon glow, no drop shadow outside. Keep true alpha transparency OUTSIDE the diamond, never a checkerboard painted into the image. One perfectly centered square sprite with matching equal padding, single frame only. Dark antique fantasy game aesthetic, restrained highlights and dark metal shadows. Color palette: dark aged violet metal, shadow #533760, restrained dusty amethyst edge highlights #9473A7. No bright magenta.

