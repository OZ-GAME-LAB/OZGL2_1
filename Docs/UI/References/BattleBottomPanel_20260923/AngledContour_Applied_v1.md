# 사선형 하단 배경 — 적용 레퍼런스

- 이미지: `AngledContour_Applied_v1.png`
- 내장 image_gen 사용. 사용자 첨부 빨간 안내선의 형태를 기준으로 만든 검토용 합성 시안이다.
- 왼쪽 큰 봉우리·작은 봉우리, 낮은 중앙 직선, 오른쪽 큰 봉우리의 비대칭 윤곽.
- 안내선을 제거하고 석재/흑철 배경을 입혔으며 카드·버튼은 전면에 배치했다.
- 첨부된 구형 전투 화면을 시안의 바탕으로 사용했다. 최신 Unity 배치나 기능을 되돌린 것이 아니며 Scene/Prefab/코드는 변경하지 않았다.
- 본 이미지는 화면 레퍼런스다. 실제 적용 시 배경과 테두리 부품만 추출하고 숫자·텍스트·카드는 별도 UI로 유지해야 한다.

## 생성 프롬프트

```text
Use case: precise-object-edit.
Asset type: one full-screen applied dark-fantasy pixel-art battle HUD reference mockup, same 16:9 framing as the supplied image.

Image 1 is BOTH the edit target and a precise SHAPE ANNOTATION. The thick bright-red hand-drawn polyline is a guide for the TOP EDGE of a new bottom HUD backing panel; it must NOT remain in the final result. This is NOT a request for a rectangular U panel.

PRIMARY EDIT: replace the hand-drawn guide with a refined continuous pixel-art black-iron/charcoal-stone backing panel occupying the area BELOW that polyline down to the bottom screen edge. Its top silhouette must faithfully follow the guide, straight angular diagonals, not wavy strokes:
Approximate points in normalized screen coordinates (x%, y% measured downward from top):
(0,76.5) -> (8.8,61.5) -> (17.2,72.7) -> (21.0,68.0) -> (30.5,84.2) -> (78.1,84.2) -> (89.8,61.5) -> (100,74.7).
Connect them as straight line segments. These are silhouette vertices, not text to display.
Important silhouette: a BIG triangular peak behind the LEFT Cost diamond; then a valley; a SMALL separate triangular peak behind the reroll diamond; a long diagonal down to a LOW HORIZONTAL CENTRAL BRIDGE; the central bridge passes behind the three cards; then a diagonal up to a BIG triangular peak behind the RIGHT battle-start diamond, then down to the right screen edge. LEFT TWO PEAKS versus RIGHT ONE PEAK is intentional. Keep this asymmetry. NO vertical tower sides or flat raised shoulders. NO symmetrical double-U substitutions. NO huge triangle erected over the board.

MATERIALS: restrained low-contrast charcoal stone fragments and matte black-iron plates, crisp 2D pixel clusters, narrow worn bone-silver edging plus a hairline muted garnet-red inset trim following the exact angular silhouette. Small unobtrusive joints at silhouette bends are acceptable. Dark, heavy, integrated with the existing crimson/ivory HUD, NOT a bright red filled panel. The black stone surface is quietly visible behind the controls, not a dense decoration competing with text. Straight flat bottom edge at screen bottom.

LAYER ORDER IS CRITICAL: new panel BEHIND all Cost/reroll/buttons/cards and existing foreground UI. The new rim should disappear/occlude behind cards wherever the guide crosses card art. Keep the three card images, frames, Korean names, descriptions, cost numbers and stats readable and unchanged. Do not paint a border through card faces. Preserve the left flame Cost diamond, reroll button+price, right crossed-swords battle-start diamond at EXACT original positions and sizes.

INVARIANTS: keep original top HUD, game board, left stacked skill icons, right incoming-enemies window, texts, numbers, all button/card artwork and layout as they are. Repair only pixels obscured by the red annotation where needed. Remove every trace of the neon red freehand marker. Do not move the board, do not redesign the old screenshot into another HUD layout. No text added, no title labels, no watermark, no comparison grid. Return ONE complete applied screenshot, not an isolated panel asset.
```

