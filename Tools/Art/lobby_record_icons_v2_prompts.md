# 기록판 아이콘 v2

- 생성: built-in image_gen, 서로 독립된 이미지 2회 생성.
- 목적: 목업의 극소형 아이콘 추출본을 독립 아트로 교체. 원본은 Sources/DifficultyRecordHover/Icon_*_Generated_v2.png에 보존.
- 후처리: NormalizeDifficultyRecordIcons.ps1. 실제 알파 유지, 불투명 영역 기준 트리밍, 종횡비 유지 NN 크기 정렬, 256×256 가운데 배치. 다시 그리거나 색상 변경하지 않음.
- Sprite: Center pivot, Full Rect, Point, Mipmap off, Uncompressed. UI는 두 아이콘 모두 32×32, Preserve Aspect, Raycast Target off.

## Trophy prompt

Use case: stylized-concept. Asset type: production Unity pixel-art UI sprite, one standalone trophy icon for highest cleared wave. Generate ONE small, perfectly front-facing, bilateral-symmetrical trophy: clear cup bowl, two equal open handles, narrow stem and centered stepped base. Restrained antique gold metal with warm ivory highlights (#EED9A5), mid gold (#C7A568), dark brown edges (#594027); 4-5 solid tones only. Design as a crisp 32x32 logical pixel art symbol enlarged with nearest-neighbor to a large square PNG. Icon must remain immediately readable displayed at 28 pixels tall. Clean contiguous pixel clusters, no noise, no scratches, no speckles, no tiny engraving, no perspective, no blur or glow. Full trophy intact and balanced, occupies centered 75% of square width/height, equal transparent margins, both geometric bounding-box center and vertical stem exactly at canvas center. Actual transparent alpha background including holes inside handles, NOT a painted checkerboard, no backdrop, no drop shadow, no ground, no frame, no text, no labels, no comparison sheet. This is a dark medieval fantasy game's refined golden information icon, not a modern emoji.

## Hourglass prompt

Use case: stylized-concept. Asset type: production Unity pixel-art UI sprite, one standalone hourglass icon for clear time. Generate ONE perfectly upright front-facing HOURGLASS, bilateral-symmetrical. Strong equal straight top and bottom antique-gold bars, two short side supports, a clean hourglass X-shaped glass silhouette narrowing in the exact center, visible warm ivory sand triangles. Elegant but extremely simple and legible at 28 pixels tall. Match a dark medieval fantasy trophy icon: warm ivory (#EED9A5), antique mid gold (#C7A568), dark brown edges (#594027), 4-5 solid tones. Design on a crisp 32x32 logical pixel grid enlarged with nearest-neighbor to a large square PNG, large contiguous pixels, no fine engraving/no noise/no speckles. Icon occupies centered 75% height and about 58% width; equal top/bottom padding and mirrored left/right margins, bounding-box center exactly canvas center. Actual transparent alpha background and transparent empty regions, NOT painted checkerboard. No scene, no shadow, no frame, no text, no labels, no duplicate icons. No perspective, tilt, blur, glow, gradients or 3D rendering.
