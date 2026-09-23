# 하단 U자 UI 배경 레퍼런스 4안

- 내장 `image_gen`으로 제작한 디자인 검토용 시안. Unity 씬·스프라이트·코드는 변경하지 않았다.
- 카드/버튼을 제거한 분리형 미리보기이며 런타임용 투명 리소스는 아니다.
- 공통 형태: 좌우 높은 지지판, 수직 단차, 낮은 중앙 연결부. 패널 위의 숫자·글자는 실제 구현 시 별도 Unity Text로 유지한다.
- 참고 이미지: 사용자 첨부 `codex-clipboard-0c82b9ce-0434-4af5-aaac-fd0bc2a79361.png`.
- 기존 레퍼런스와의 연결감은 01, 화면 정보 가독성 우선은 02를 권장한다.
- 선택한 안의 실제 적용은 별도 요청/승인 후 진행한다.

## 01 · 흑철·석재

파일: `01_BlackIron_Stone.png`

```text
Use case: ui-mockup.
Asset type: one polished pixel-art DARK FANTASY bottom U-shaped HUD BACKGROUND concept, a single option, not a contact sheet.
Input image 1: style and U-shaped silhouette reference only. This is NOT a request to reproduce the entire screen.
Composition: landscape approximately 3:1. Display ONLY the isolated long bottom U-shaped panel at a large useful scale, front-on orthographic, on a flat near-black presentation background. NO gameplay board, NO cards, NO foreground buttons/icons, NO words or numbers. The empty backing panel itself is the subject.
MANDATORY IDENTICAL GEOMETRY for comparison: horizontal bottom edge, two equal tall solid rectangular end-wings occupying the leftmost and rightmost 14% of width. Both wings extend upwards to the SAME height (about 80% of canvas height), to eventually sit behind the Cost and battle-start diamond buttons. The inside edge of each wing descends STRAIGHT DOWN with only small 45-degree corner cuts, then connects to a LOW straight central bridge. The long central bridge spans the middle 72% and is only about ONE THIRD as tall as the end-wings. Thus the entire top middle is an open, large, BLACK, rectangular recess, leaving space for cards and reroll to protrude in front. Clear rectilinear U silhouette, NOT a curved bowl, NOT a straight solid rectangular bar, NOT an arch rising in the center, NOT four separate panels. No decorations crossing the open central recess.
Visual language: crisp deliberate 2D pixel art with coherent pixel size, dark charcoal and muted garnet/crimson, small bone-ivory edge highlights. Flat UI art, not photorealistic or a 3D physical object. Broad quiet dark surfaces with readable trims. Strict left-right structural symmetry; all four options share exactly this geometry. No glow bloom, no blur, no gradients, no enormous skulls, no chains. Entire silhouette visible with small equal margins; no clipped corners.

Variation 01: battle-worn charcoal flagstone interior enclosed by forged black-iron edging. Irregular modest flat stone chips and cracks, a thin paired ivory and dark-red inset rail along the entire U silhouette, a few small square corner rivets, minimal diamond joins at shoulder corners. Closest to the reference image's stone-and-crimson UI backing. Solid grounded weight without over-ornamentation. Panel values clearly but subtly lighter than black presentation backdrop.
```

## 02 · 슬림 철제

파일: `02_Slim_ForgedSteel.png`

```text
Use case: ui-mockup.
Asset type: one polished pixel-art DARK FANTASY bottom U-shaped HUD BACKGROUND concept, a single option, not a contact sheet.
Input image 1: style and U-shaped silhouette reference only. This is NOT a request to reproduce the entire screen.
Composition: landscape approximately 3:1. Display ONLY the isolated long bottom U-shaped panel at a large useful scale, front-on orthographic, on a flat near-black presentation background. NO gameplay board, NO cards, NO foreground buttons/icons, NO words or numbers. The empty backing panel itself is the subject.
MANDATORY IDENTICAL GEOMETRY for comparison: horizontal bottom edge, two equal tall solid rectangular end-wings occupying the leftmost and rightmost 14% of width. Both wings extend upwards to the SAME height (about 80% of canvas height), to eventually sit behind the Cost and battle-start diamond buttons. The inside edge of each wing descends STRAIGHT DOWN with only small 45-degree corner cuts, then connects to a LOW straight central bridge. The long central bridge spans the middle 72% and is only about ONE THIRD as tall as the end-wings. Thus the entire top middle is an open, large, BLACK, rectangular recess, leaving space for cards and reroll to protrude in front. Clear rectilinear U silhouette, NOT a curved bowl, NOT a straight solid rectangular bar, NOT an arch rising in the center, NOT four separate panels. No decorations crossing the open central recess.
Visual language: crisp deliberate 2D pixel art with coherent pixel size, dark charcoal and muted garnet/crimson, small bone-ivory edge highlights. Flat UI art, not photorealistic or a 3D physical object. Broad quiet dark surfaces with readable trims. Strict left-right structural symmetry; all four options share exactly this geometry. No glow bloom, no blur, no gradients, no enormous skulls, no chains. Entire silhouette visible with small equal margins; no clipped corners.

Variation 02: restrained modern-readable Gothic forged steel. Smooth broad matte gunmetal plates with extremely subtle pixel stippling, narrow straight ivory/brushed-steel bevel and a single muted garnet groove along the stepped U perimeter, tiny flush rivets at joints. Large calm nearly-black areas. Slim trim with clean chamfered joints, no carved stone, no glyphs, no relief motifs. Keep the required exact panel geometry, make the construction noticeably lighter and cleaner than the stone option.
```

## 03 · 고딕 성채

파일: `03_Gothic_Bastion.png`

```text
Use case: ui-mockup.
Asset type: one polished pixel-art DARK FANTASY bottom U-shaped HUD BACKGROUND concept, a single option, not a contact sheet.
Input image 1: style and U-shaped silhouette reference only. This is NOT a request to reproduce the entire screen.
Composition: landscape approximately 3:1. Display ONLY the isolated long bottom U-shaped panel at a large useful scale, front-on orthographic, on a flat near-black presentation background. NO gameplay board, NO cards, NO foreground buttons/icons, NO words or numbers. The empty backing panel itself is the subject.
MANDATORY IDENTICAL GEOMETRY for comparison: horizontal bottom edge, two equal tall solid rectangular end-wings occupying the leftmost and rightmost 14% of width. Both wings extend upwards to the SAME height (about 80% of canvas height), to eventually sit behind the Cost and battle-start diamond buttons. The inside edge of each wing descends STRAIGHT DOWN with only small 45-degree corner cuts, then connects to a LOW straight central bridge. The long central bridge spans the middle 72% and is only about ONE THIRD as tall as the end-wings. Thus the entire top middle is an open, large, BLACK, rectangular recess, leaving space for cards and reroll to protrude in front. Clear rectilinear U silhouette, NOT a curved bowl, NOT a straight solid rectangular bar, NOT an arch rising in the center, NOT four separate panels. No decorations crossing the open central recess.
Visual language: crisp deliberate 2D pixel art with coherent pixel size, dark charcoal and muted garnet/crimson, small bone-ivory edge highlights. Flat UI art, not photorealistic or a 3D physical object. Broad quiet dark surfaces with readable trims. Strict left-right structural symmetry; all four options share exactly this geometry. No glow bloom, no blur, no gradients, no enormous skulls, no chains. Entire silhouette visible with small equal margins; no clipped corners.

Variation 03: a Gothic fortress-inspired panel. The SAME end-wing and low-bridge geometry, but the wings have large slate masonry blocks, layered angular iron corner braces and very shallow pointed-arch reliefs carved into their flat surface. Subdued red enamel iron straps, worn bone-silver trim, architectural buttress-like vertical ribs contained entirely inside the wing silhouette. The low central rail has a restrained repeating angular masonry rhythm. No actual towers extending above the defined wings, no extra spikes or buildings. Strong structural character, dark enough to remain behind UI.
```

## 04 · 룬 흑요석

파일: `04_Runic_Obsidian.png`

```text
Use case: ui-mockup.
Asset type: one polished pixel-art DARK FANTASY bottom U-shaped HUD BACKGROUND concept, a single option, not a contact sheet.
Input image 1: style and U-shaped silhouette reference only. This is NOT a request to reproduce the entire screen.
Composition: landscape approximately 3:1. Display ONLY the isolated long bottom U-shaped panel at a large useful scale, front-on orthographic, on a flat near-black presentation background. NO gameplay board, NO cards, NO foreground buttons/icons, NO words or numbers. The empty backing panel itself is the subject.
MANDATORY IDENTICAL GEOMETRY for comparison: horizontal bottom edge, two equal tall solid rectangular end-wings occupying the leftmost and rightmost 14% of width. Both wings extend upwards to the SAME height (about 80% of canvas height), to eventually sit behind the Cost and battle-start diamond buttons. The inside edge of each wing descends STRAIGHT DOWN with only small 45-degree corner cuts, then connects to a LOW straight central bridge. The long central bridge spans the middle 72% and is only about ONE THIRD as tall as the end-wings. Thus the entire top middle is an open, large, BLACK, rectangular recess, leaving space for cards and reroll to protrude in front. Clear rectilinear U silhouette, NOT a curved bowl, NOT a straight solid rectangular bar, NOT an arch rising in the center, NOT four separate panels. No decorations crossing the open central recess.
Visual language: crisp deliberate 2D pixel art with coherent pixel size, dark charcoal and muted garnet/crimson, small bone-ivory edge highlights. Flat UI art, not photorealistic or a 3D physical object. Broad quiet dark surfaces with readable trims. Strict left-right structural symmetry; all four options share exactly this geometry. No glow bloom, no blur, no gradients, no enormous skulls, no chains. Entire silhouette visible with small equal margins; no clipped corners.

Variation 04: cold matte obsidian slabs with sparse sharp faceted cracks and recessed ritual engravings. Dark gunmetal edging with small tarnished warm-silver highlights; dim burgundy-red geometric rune bands carved into the flat side wings and a very fine continuous rune seam on the low bottom bridge. Engravings are subtle, not emitting light. No bright magic circles or floating particles. The SAME symmetrical stepped U geometry; quieter central area, slightly mystical ceremonial feeling while fully respecting practical HUD readability.
```


