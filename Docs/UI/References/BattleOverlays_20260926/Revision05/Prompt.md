# 실버 장식 간소화

- 생성 방식: built-in image_gen, 표적 이미지 편집 1회.
- 기준: ../Revision04/Rarity_03Crest_04Velvet.png.
- 결과: [Rarity_SilverSimplified.png](Rarity_SilverSimplified.png).
- 실버 카드 양옆의 끈·술 장식 제거, 벨벳 문양과 코너/하단 장식 간소화.
- 골드·플래티넘 디자인, 세 카드 크기·배치·문구 유지.
- 출력에서 양옆 술 제거, 수수한 실버, 다른 등급 유지 및 가독성을 1회 시각 확인.
- 원본 보존. Unity/Figma/씬/프리팹/게임 데이터 미변경.

## 프롬프트

```text
Use case: precise-object-edit.
Input image 1 is the exact EDIT TARGET. Make a tightly localized refinement of ONLY THE LEFTMOST SILVER CARD in this three-rarity Korean pixel-art level-up screen.
1. Remove BOTH hanging tassels completely from the upper LEFT and RIGHT edges of the silver velvet pennant: remove the dangling thread bundles, hanging cords and their small connectors. Restore the dim battle background in those spaces. Leave a clean simple end to the pennant's horizontal top rod; nothing hangs down either side of the silver card.
2. Make the silver velvet pattern modest and quiet: almost plain dark burgundy velvet, just a very faint sparse weave visible on close inspection, no elaborate damask or conspicuous embroidery. Simplify the silver pennant's small cloth-corner/lower-hem floral flourishes into a clean thin silver outline with a tiny bottom diamond accent. Do NOT add any decoration.
3. Preserve the silver top sword diamond, white sword icon, grade-name plaque "실버", text content, exact text positions, card body shape and size. Its top icon keeps the existing simple angular silver design, without gold laurel or platinum wings.
STRICT INVARIANTS: The MIDDLE GOLD and RIGHT PLATINUM cards are completely unchanged, including all their hanging tassels, fabric patterns, gold laurel and platinum crown/wing crest. Preserve the scene background, dim overlay, title, every label and number, all spacing and card alignment. Do not brighten any portion. Do not change the font or add buttons. Do not remove tassels from the gold or platinum cards.
The hierarchy should now read: silver visibly plain and tassel-free; gold retains its existing moderate ornament; platinum retains its existing crown/wing ornament. No global restyle. Same crisp dark gothic pixel art. Output ONE full 16:9 edited screen, not a collage or cropped card.
```

