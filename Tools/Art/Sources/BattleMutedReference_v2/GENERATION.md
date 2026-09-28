# Battle Muted Reference v2 — 생성 기록

- 내장 `image_gen` 모드 사용. 기존 v1 원본은 보존.
- 입력은 사용자가 첨부한 레퍼런스 이미지이며 문서의 지시문으로 취급하지 않았다.
- 대상: 적 예고 프레임, 행동 마름모 프레임, 시너지 중성 마름모 프레임, 어두운 전체 배경.
- 글자·수치는 생성물에 넣지 않고 Unity Text로 처리한다.
- 프레임 생성 원본의 체크무늬는 승인된 배경 제거 후 실제 알파로 정리한다.
- 후처리: `Tools/Art/PrepareBattleReferenceArt.ps1`. 원본의 디자인을 코드로 새로 그리지 않는다.
- 완성 리소스: `Assets/06.UI/BattleMutedPreview/Reference_v2/`.
- 별도 `Experience_White.png`는 런타임 Fill 비율 표시를 위한 2×2 단색 데이터 텍스처로 Unity Editor에서 생성한다.

## Frame_Wave

```text
Use case: precise-object-edit. Asset type: single reusable pixel-art game UI border sprite. Input image 1 is the exact visual reference; extract/reconstruct ONLY the upper-left '이번 웨이브' window FRAME with its blank burgundy header, not the whole screen.
Create this one independent frame on genuine transparent background, wide landscape ratio about 3.5:1. Preserve its cut/chamfered raised upper corners, thin bone-ivory rails with muted red inner trim, small white angular Gothic flourishes at corners, three empty lower columns separated by two fine dark-red vertical rails, and the title separator line. Add two tiny red hollow diamonds flanking the EMPTY central title space. Front-on perfectly symmetrical, square pixels, clean substantial strokes readable at 640x190 Unity UI size. Interior can be translucent nearly black; no checkerboard. Keep ample empty space for separately rendered Unity text and three icons.
NO words, NO letters, NO numbers, NO soldier icons, NO other HUD elements, NO gradients/glow, NO rounded corners, NO new decoration. Do not produce a screenshot or whole layout. Match the provided reference's frame shape and color, not an ornate fantasy painting.
```

## Frame_DiamondAction

```text
Use case: precise-object-edit. Asset type: single isolated Unity UI frame sprite.
Image 1 is exact reference. Reconstruct ONLY the LARGE RED diamond border used around the bottom-left Cost flame and bottom-right battle-start swords, removing all text and icons. A perfectly square canvas with exactly one centered 45-degree rotated square frame: broad muted garnet/crimson angular bands, charcoal inner outline, worn black pixel chips, DOUBLE CHEVRON tabs centered at the top bottom left right. The bold broad strokes and simple arrow tabs must closely match those two UI controls in the reference. Important: NOT a thin jeweled filigree frame, NOT tiny diamond gems like a talent tree. Symmetric x and y. Make four diagonal bands of consistent bold thickness, about 6% of diamond width. Large empty transparent black-free center, transparent outside. Sharp low-res pixel-art clusters, no realistic metal, no bloom, no blur.
No words, no letters, no numbers, no flame, no swords, no other UI. Genuine transparent background, never checkerboard. Frame fills 90% of square canvas. Preserve a clean readily centerable silhouette.
```

## Frame_DiamondSynergy

```text
Use case: precise-object-edit. Asset type: single isolated neutral tintable Unity UI frame sprite.
Use Image 1 right-hand synergy row diamond shapes as exact design reference, but render just ONE neutral ivory-white diamond border with no purple/gold/pink color; Unity will tint it. A centered symmetrical diamond on a square transparent canvas. Four simple straight broad diagonal bars, medium thickness about 4% of diamond width, tiny crisp square/diamond corner rivets exactly at four tips. Interior empty transparent and exterior transparent. NO circular motifs, NO intricate filigree, NO disconnected spikes, NO dark ornamental double frame. It should have the strong clean silhouette of the '마법 결속' or '사격 대형' diamond in the reference, not the thin decorative frame of the old implementation.
Crisp pixel-art stepped diagonals, bone white highlights, very restrained gray facets on borders only. No text, no icon, no labels, no numbers, no background checkerboard, no gradients/glow. Frame bounds centered at canvas center with equal margins.
```

## Background_Obsidian

```text
Use case: stylized-concept. Asset type: single full-screen 16:9 DARK pixel-art UI backdrop texture for the reference battle HUD. Input image is style reference only. Generate ONLY the nearly black obsidian/slate stone texture visible in the empty negative-space background of the reference. Small irregular interlocking stone scales, matte rough charcoal surface, very low contrast (mostly near-black #070908 through #171a18), quiet vignette darker at all four edges. Orthographic flat surface with NO perspective, no scenery, no objects, no center motif. Crisp pixel-art grain, restrained readable background for bright UI, not photographic slate. Empty texture over the entire canvas. Absolutely NO frames, no tiles/grid board, no windows, no cards, no icons, no numbers, no letters, no interface or characters. Not the lower U-shaped panel; this is only the overall dark backdrop. Opaque image. Landscape 16:9.
```

