# 카드 마름모 종류 아이콘 v2

- 방식: 내장 ImageGen으로 아이콘 3종을 각각 생성.
- 적용: 카드 Prefab 3종의 `TypeBadge/TypeIcon`만 교체. 프레임·문구·능력치 아이콘·씬은 유지.
- 원본은 이 폴더의 `*_Original.png`, 참조 화면은 `Reference_Cards_v1.png`로 보존.
- 출력: `Assets/06.UI/BattleMutedPreview/Cards_v1/Sprites/Icon_Type_{Unit,Land,Relic}_Diamond_v2.png`.
- 후처리: `Tools/Art/PrepareBattleCardArt.ps1 -DiamondIcons`. 생성물에 포함된 체크 배경을 제거하고 균등 축소·중심 정렬만 수행. 아이콘 본체를 코드로 그리거나 변형하지 않음.
- 256×256 텍스처 중심 기준 |dx|+|dy|≤112px, 중심 Pivot, Point, 무압축. 실제 배치는 112×112, 중심 (80,-80).
- 기존 `Icon_Sword.png`는 Stats/Attack/Icon에도 연결되어 있으므로 변경하지 않음.

## 최종 생성 프롬프트

### Icon_Type_Unit_Diamond_v2

```text
Use case: stylized-concept.
Asset type: single isolated pixel-art game UI pictogram for a 160x160 diamond badge.
Image 1 is STYLE/CONTEXT REFERENCE ONLY: the dark fantasy cards and their upper-left badges. DO NOT recreate cards or text. Generate only the new icon, no frame.
A genuinely transparent RGBA background, no visible checker pattern, no opaque rectangle. Centered on a square canvas. Restrained noble gothic pixel-art, crisp stepped edges and broad clean strokes, ivory-white silhouette (#f6f0e2 to white) with transparent cutouts, no colored glow, no cast shadow, no blur. Tiny pixel highlights only; not photorealism, not vector hairlines. Readable at 64px.
Composition: COMPACT DIAMOND-FITTING MASS, approximately equal overall width and height. Taper towards top and bottom points; greatest width around vertical middle. All visible parts must fit well INSIDE an imagined rhombus; generous 15% margin; visual mass centered with balanced top/bottom and left/right. NO drawn diamond border, NO plaque, NO background fill, NO extra disconnected sparkles. One standalone symbol only. No text, no number.
Subject: ONE upright ceremonial BROADSWORD heraldic emblem. Thick broad faceted spear-diamond-shaped blade points UP, substantial short grip and tapered pommel point DOWN, sweeping broad symmetrical crossguard expands LEFT and RIGHT around the center like the horizontal tips of the imaginary diamond. A short, stout, visually balanced emblem, NOT a thin long vertical sword. Keep clear sword identity; no shield, no additional weapons.
```

### Icon_Type_Land_Diamond_v2

```text
Use case: stylized-concept.
Asset type: single isolated pixel-art game UI pictogram for a 160x160 diamond badge.
Image 1 is STYLE/CONTEXT REFERENCE ONLY: the dark fantasy cards and their upper-left badges. DO NOT recreate cards or text. Generate only the new icon, no frame.
A genuinely transparent RGBA background, no visible checker pattern, no opaque rectangle. Centered on a square canvas. Restrained noble gothic pixel-art, crisp stepped edges and broad clean strokes, ivory-white silhouette (#f6f0e2 to white) with transparent cutouts, no colored glow, no cast shadow, no blur. Tiny pixel highlights only; not photorealism, not vector hairlines. Readable at 64px.
Composition: COMPACT DIAMOND-FITTING MASS, approximately equal overall width and height. Taper towards top and bottom points; greatest width around vertical middle. All visible parts must fit well INSIDE an imagined rhombus; generous 15% margin; visual mass centered with balanced top/bottom and left/right. NO drawn diamond border, NO plaque, NO background fill, NO extra disconnected sparkles. One standalone symbol only. No text, no number.
Subject: ONE compact stylized TERRAIN emblem: a steep central mountain peak rising above a small diamond-shaped land tile, two modest lower slopes widening near the middle, angular earth layers tapering into a downward point. White silhouette and a few transparent facet cuts distinguish the mountain and soil. Four cardinal tips make its overall mass a rhombus, not a wide flat mountain strip. No separate floating rocks, no trees, no buildings.
```

### Icon_Type_Relic_Diamond_v2

```text
Use case: stylized-concept.
Asset type: single isolated pixel-art game UI pictogram for a 160x160 diamond badge.
Image 1 is STYLE/CONTEXT REFERENCE ONLY: the dark fantasy cards and their upper-left badges. DO NOT recreate cards or text. Generate only the new icon, no frame.
A genuinely transparent RGBA background, no visible checker pattern, no opaque rectangle. Centered on a square canvas. Restrained noble gothic pixel-art, crisp stepped edges and broad clean strokes, ivory-white silhouette (#f6f0e2 to white) with transparent cutouts, no colored glow, no cast shadow, no blur. Tiny pixel highlights only; not photorealism, not vector hairlines. Readable at 64px.
Composition: COMPACT DIAMOND-FITTING MASS, approximately equal overall width and height. Taper towards top and bottom points; greatest width around vertical middle. All visible parts must fit well INSIDE an imagined rhombus; generous 15% margin; visual mass centered with balanced top/bottom and left/right. NO drawn diamond border, NO plaque, NO background fill, NO extra disconnected sparkles. One standalone symbol only. No text, no number.
Subject: ONE compact ARCANE POWER emblem: a luminous ivory faceted diamond core with four bold curved energy arms spiraling tightly around it, expanding near the middle and tapering at cardinal top/bottom/left/right tips. Looks like condensed magical power for a mana-amplifying relic, not a wand or staff. Thick legible strokes and clean transparent gaps. Entire outer silhouette fits an imaginary diamond; no actual outer ring/border, no dangling staff, no detached sparks.
```

