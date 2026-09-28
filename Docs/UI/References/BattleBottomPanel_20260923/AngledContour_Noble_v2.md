# 전투 하단 패널 — 귀족풍 조형 시안 v2

- 제작일: 2026-09-23
- 결과물: `AngledContour_Noble_v2.png`
- 원본 편집 대상: `AngledContour_Applied_v1.png` (보존)
- 생성 방식: 내장 image_gen, 이미지 편집 2회 (재질 변경 후 오른쪽 외곽 정렬 보정)
- 사용 범위: 적용 모습을 보여주는 레퍼런스 이미지. Unity Scene/Prefab/코드 변경 없음.
- 변경 내용: 거친 돌 질감을 흑색 래커·짙은 와인색 문양·고금색 장식으로 교체. 좌우 큰 꼭짓점 높이와 바깥 모서리를 시각적으로 정렬.
- 유지 사항: 왼쪽 리롤 뒤 작은 봉우리, 낮은 중앙 연결부, 앞쪽 카드와 버튼 배치. 전체 좌우 복제가 아닌 주 봉우리와 바깥 모서리의 대칭 보정.
- 검토: 생성 결과에서 재질/조형을 육안 확인. 실제 UI 구현 및 픽셀 단위 대칭 검증은 수행하지 않음.

## 1차 생성 프롬프트

```text
Edit the supplied battle UI mockup into a polished premium aristocratic dark-fantasy PIXEL-ART game HUD reference. This is a targeted material and symmetry revision of ONLY the lower background panel, not a redesign of the UI.

PRESERVE the entire composition, all existing UI controls, icons, labels, numbers, cards, gameplay grid, top HUD and right enemy information exactly in their existing locations. The lower panel stays behind all cards and buttons. Do not introduce extra text.

REPLACE the rough earthy rock/stone lower background with a refined noble material: smooth near-black ebony lacquer / black enamel surfaces, inset very dark burgundy velvet with extremely subtle tone-on-tone damask, restrained fine champagne-antique-gold metal edging with a little muted crimson. Aristocratic, dignified, elegant Gothic workmanship. Crisp controlled pixel-art edges and small ornamental details matching the existing game. The broad surface must be quiet and luxurious, not noisy. Absolutely NO terrain, soil, stone, gravel, cracks, rubble, masonry, rusty metal, huge emblems, neon, or realistic 3D rendering. Preserve foreground crimson diamond buttons.

KEEP THE EXACT ANGULAR CONTOUR: tall primary triangular peak behind the left Cost diamond, a descending valley, a smaller triangular peak behind reroll, then slope down into the low long horizontal bridge behind the card lower halves; rise to the tall primary triangular peak behind the right battle-start diamond, then descend toward the right screen edge. The extra small LEFT reroll peak is deliberate: do not mirror it on the right and do not replace this contour with a rectangular tray.
FIX THE RIGHT-SIDE GEOMETRY: primary LEFT and RIGHT large peaks must have exactly equal height, horizontally mirrored positions, identical slope angles, bevel widths and mirrored corner-cap details. At the 1672x941 source dimensions, align the large left peak around (146,581) and right peak around (1526,581), not the current lower right peak around y602. Align the corresponding outer left/right edge intersections around y701. Right corner joints must meet cleanly with no offset, crooked extra point, double outline or hanging trim. Keep the intentional small reroll peak on the left and keep the central bridge low, around y780. Extend the refined panel to the bottom edge of the screen.
The trim is thin, precise and tasteful: continuous antique-gold hairline with narrow dark-crimson inner accent, tiny symmetric diamond/rivet corner details only. Where the panel boundary runs behind cards, it must be occluded by cards, never drawn across card faces. Preserve readable foreground elements and high contrast. Deliver ONE full-screen applied reference image with the same aspect ratio, no comparison sheet or caption.
```

## 오른쪽 모서리 보정 프롬프트

```text
Use case: precise-object-edit. Edit ONLY the lower-right aristocratic HUD BACKGROUND frame in this supplied image. Preserve the new black lacquer, dark burgundy damask and fine gold design, and preserve every foreground icon, card, label, number, button and all upper UI.

CRITICAL GEOMETRIC CORRECTION: The left main background peak above the Cost diamond is at about x146 y575 in this 1672x941 image. The right main background peak above the battle-start diamond is INCORRECTLY at about x1526 y597. RAISE THE RIGHT BACKGROUND PEAK BY 22 PIXELS to x1526 y575 so the two main peaks sit on EXACTLY the same horizontal level. Its upper tip must align with the left tip when a horizontal ruler is placed across the screen. Leave the left peak fixed.
Mirror the left peak's outer descending bevel/slope, its 45-degree corner treatment, gold trim thickness, crimson inner hairline, and small diamond ornament onto the right primary peak. Left outer screen-edge corner is around x12 y701: right outer corner must be its horizontal mirror around x1660 y701. The right outer slope and inner slope should meet the raised tip cleanly without crooked angles, steps, broken trim or double caps. Reposition its small hanging gold filigree accordingly. DO NOT move the battle-start button itself.
The central low connection remains low and horizontal. KEEP the small extra reroll peak on the LEFT ONLY, unchanged: the entire panel is intentionally asymmetric because of that extra left peak; do not add a second small peak on the right. Foreground cards must occlude the low connecting gold trim.
This edit is narrowly about the RIGHT CORNER / PEAK alignment. No material redesign, no other UI changes, no guides, no ruler or arrows in the output. Return the full-screen applied image, same resolution/aspect ratio.
```

