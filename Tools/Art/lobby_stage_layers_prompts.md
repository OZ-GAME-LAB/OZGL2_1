# 로비 StageSelection 분리 아트 생성 기록

- 생성 방식: built-in image_gen, 기존 합성 이미지 참조 편집.
- 적용 범위: UI_Lobby_MutedPreview / Canvas_Lobby / StageSelection 카드 3개.
- 원본: Assets/06.UI/LobbyMutedPreview/Sprites/Stage_Previous.png, Stage_Current.png, Stage_Locked.png (보존).
- 최종 저장 폴더: Assets/06.UI/LobbyMutedPreview/StageLayers_v1/Sprites/
- 최종 PNG: StageArtwork_Current.png, StageArtwork_Previous.png, StageArtwork_Next.png, StageFrame_Neutral.png, StageFrame_Current.png, StageLock_Chains.png.
- 1254×1254 PNG. 프레임과 잠금 이미지의 실제 알파를 Unity에서 확인했다. 프레임의 중앙/외곽 알파는 0이다.
- 초기 프레임의 가짜 체크 배경은 이미지 생성 도구로 재추출했다. 코드로 배경 제거/이미지 편집하지 않았다.
- Unity 임포트: Sprite Single / Full Rect / Point / Uncompressed / Mipmap Off / Clamp / 입력 알파 사용.

## Frame_Neutral

참조: `Stage_Previous.png`

```text
Use case: precise-object-edit. Input image: the existing game stage card, edit target. Extract/recreate ONLY its outer ornate diamond frame as one independent Unity UI sprite. Preserve the straight 45 degree diamond rails, chipped antique ivory/silver metal on black bevels, four small hollow diamond corner ornaments and tiny pointed top/bottom finials. Exactly centered, vertically and horizontally symmetric, same old dark-fantasy pixel-art weight and proportions as reference. REMOVE all castle, sky, clouds, magic circle and green backing. Both exterior and entire diamond inner opening must be genuinely transparent alpha zero. One empty diamond ring, NOT a filled diamond. No white/black/checkered/green solid background, no picture, no padlock/chains, no text, no UI sheet. Square RGBA canvas, artwork bounding box centered about92% of canvas. Crisp restrained pixel clusters, not painterly or blurred. All tips included. Preserve reference design instead of inventing elaborate new ornaments.
```

## Artwork_Current

참조: `Stage_Current.png`

```text
Use case: precise-object-edit. Input existing stage card is the composition/style reference. Generate its INTERIOR ILLUSTRATION ONLY, remove the diamond frame, ornaments and green background, extend the painting out to fill a square image edge to edge. Keep ominous central black Gothic castle, tall narrow central tower, red glowing windows, dark crimson storm clouds and the luminous antique arcane circular cross in the upper sky aligned behind the tower. Match restrained pixel-art style of the input, sharp pixel clusters and near-black/oxblood palette. IMPORTANT: upright square FULL-BLEED landscape image with NO diamond border, NO frame, NO chains, NO lock, NO text. The main castle silhouette is contained within the central inscribed diamond so it reads well when Unity crops it with a diamond mask. Opaque square artwork intended for a replaceable masked UI image, not a complete card.
```

## Artwork_Previous

참조: `Stage_Previous.png`

```text
Use case: precise-object-edit. Input existing stage card is the composition/style reference. Recreate its INTERIOR ILLUSTRATION ONLY, remove diamond frame, ornaments and green backing, extend artwork into a full opaque square. Keep weathered Gothic castle with the tall central narrow tower, monochrome charcoal/aged-bone cloudy sky and the pale circular cross magic sigil in the upper sky. Muted desaturated gray ivory palette, a quieter previously-cleared stage. Crisp dark fantasy pixel art of exactly the same coarseness as reference. Upright centered composition, tower and major castle mass inside central inscribed diamond for later Unity diamond clipping. No frame, no border, no lock, no chains, no text, no green. Full-bleed square castle artwork only.
```

## Artwork_Next

참조: `Stage_Locked.png`

```text
Use case: precise-object-edit. Input existing locked-stage card is composition/style reference. Recreate ONLY the dark castle interior artwork UNDERNEATH the lock and chains. REMOVE all padlock, chains, diamond frame, ornaments and green backing. Reconstruct the hidden center of the ominous Gothic castle logically. Tall near-black clustered spires, a few dim ember-red window lights, smoky slate gray clouds, subtle gray magic ring/cross in upper sky. Match low-saturation dark pixel art, crisp blocky clusters. Opaque FULL-BLEED SQUARE artwork, no UI border, no text, no chains/lock symbols. Upright centered castle composed inside central inscribed diamond so it can be masked by Unity. Keep it dark but readable; darkness/lock will also be separate Unity layers.
```

## LockOverlay

참조: `Stage_Locked.png`

```text
Use case: background-extraction. Input locked-stage card is reference. Produce ONLY the padlock and TWO crossing chains as a standalone transparent UI overlay. Antiqued ivory metal padlock at exact center with black keyhole, readable blocky pixel-art silhouette, same scale/proportions and pixel density as input. Four diagonal chain arms form an X from the lock toward upper-left, upper-right, lower-left and lower-right corners. Balanced diagonal chains with linked oval metal loops, no loose other objects. No castle, no sky, no magic circle, no diamond border, no green or other backing. Genuine transparent RGBA background through all chain holes and surrounding the chain/lock assembly. Centered symmetrical square output, X tips inside canvas with clear padding; no text. This is to overlay on a diamond-cropped scene, not a complete card.
```

## Frame_Neutral_Clean

```text
Use case: background-extraction. Edit target is this empty diamond frame. Remove ALL gray checkerboard/mottled background both outside AND inside the frame. Actual transparent PNG alpha, not a painted checkerboard. Keep ONLY the ivory metal rails and their black bevel outlines, four diamond corner ornaments and small top and bottom finials. Preserve their exact geometry, scale, placement, crisp pixel art. Keep black metal outlines but remove gray backdrop completely. No castle, no picture, no text, no extra art. Fully transparent interior hole and fully transparent exterior.
```

## Frame_Current

```text
Use case: precise-object-edit and background-extraction. The attached ivory diamond frame is the exact geometry to preserve. Create its current-stage RED counterpart: change only ivory/silver metal into muted antique crimson/oxblood metal with restrained salmon edge highlights, black bevels stay black. MATCH the game's current red stage frame. Keep identical rail width, corner diamond ornaments, finials, centered size and position. Also remove ALL checkerboard/mottled background inside and outside: actual transparent PNG alpha zero throughout the central hole and exterior. No solid backing, no fake checkerboard, no castle/art, no text, no other objects. A single empty red diamond ring sprite for compositing, exact same geometry as input.
```

## Frame_Current_Clean

```text
Use case: background-extraction. Remove the entire gray checkerboard background from this image. Keep ONLY the red diamond-shaped metal frame and its black outlines, exactly unchanged. Transparent outside the frame AND fully transparent inside its central opening. This needs an actual transparent PNG alpha channel, not checkerboard colored pixels. Clean cutout, preserve all frame shape and colors. Do not add any background or objects.
```

