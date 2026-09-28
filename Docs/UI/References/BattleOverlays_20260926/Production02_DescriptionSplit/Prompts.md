# 검정 설명 패널과 벨벳 배경 분리 — 생성 프롬프트

생성 방식: built-in imagegen, precise-object-edit.
가려진 벨벳을 복원한 생성 원본은 보존한다. 설명판 영역만 교체하면 남는 원본 그림자/문양 경계를 피하기 위해, 최종 배경에는 전체 복원본을 사용하고 원본과 동일한 외곽 여백으로 정렬한다. 검정 설명판은 원본 픽셀 그대로 따로 추출한다.

## Silver

입력: `D:/GitHub/OZGL2_1/Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment/Augment_Body_Silver.png`

생성 원본: `C:\Users\sudea\.codex\generated_images\01a0a4a8-3799-7db1-907e-69bb9af378b8\exec-6f96e5e7-823b-4cce-8f7a-ea725dcbc187.png`

```text
Use case: precise-object-edit. Asset type: Unity UI background layer extracted from an existing augment card. Input Image 1 is the EXACT edit target, not loose inspiration.
Make exactly ONE change: remove the entire inner near-black pentagonal DESCRIPTION PANEL, including its thin metallic rim, the tiny corner details on that INNER rim, and the tiny INNER bottom-point diamond. Replace that whole inner panel area with uninterrupted dark burgundy velvet, extending the surrounding existing fabric texture seamlessly across it. There must be NO black inset, no remaining outline of the inset, and no extra border where it used to be.
LOCK the outer pennant silhouette, OUTER metal border, outer corner filigree, top suspension bar, absence of hanging tassels, plain restrained wine velvet with very faint weave, and large OUTERMOST bottom-point metal ornament. Maintain aged silver material and original dark brightness, pixel-art style, exact full-canvas composition and original spacing. Do not add motifs, titles, text, swords, labels, new jewels, glows, gradients, black panels, or other UI.
The result is just the FULL uninterrupted velvet banner background with original outer decorations. Keep the same 600:1100 portrait aspect and exact normalized object placement, transparent alpha outside its original silhouette. Preserve input's transparency; do not draw checkerboards. Do not crop or enlarge the pennant.
```

## Gold

입력: `D:/GitHub/OZGL2_1/Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment/Augment_Body_Gold.png`

생성 원본: `C:\Users\sudea\.codex\generated_images\01a0a4a8-3799-7db1-907e-69bb9af378b8\exec-017f73ca-09af-4520-8c03-3b53de8d9bb6.png`

```text
Use case: precise-object-edit. Asset type: Unity UI background layer extracted from an existing augment card. Input Image 1 is the EXACT edit target, not loose inspiration.
Make exactly ONE change: remove the entire inner near-black pentagonal DESCRIPTION PANEL, including its thin metallic rim, the tiny corner details on that INNER rim, and the tiny INNER bottom-point diamond. Replace that whole inner panel area with uninterrupted dark burgundy velvet, extending the surrounding existing fabric texture seamlessly across it. There must be NO black inset, no remaining outline of the inset, and no extra border where it used to be.
LOCK the outer pennant silhouette, OUTER metal border, outer corner filigree, top suspension bar, existing hanging tassels, understated tone-on-tone burgundy damask, and large OUTERMOST bottom-point metal ornament. Maintain antique gold material and original dark brightness, pixel-art style, exact full-canvas composition and original spacing. Do not add motifs, titles, text, swords, labels, new jewels, glows, gradients, black panels, or other UI.
The result is just the FULL uninterrupted velvet banner background with original outer decorations. Keep the same 600:1100 portrait aspect and exact normalized object placement, transparent alpha outside its original silhouette. Preserve input's transparency; do not draw checkerboards. Do not crop or enlarge the pennant.
```

## Platinum

입력: `D:/GitHub/OZGL2_1/Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment/Augment_Body_Platinum.png`

생성 원본: `C:\Users\sudea\.codex\generated_images\01a0a4a8-3799-7db1-907e-69bb9af378b8\exec-e0ff81bd-0f5b-4c8f-94d9-4fc5083a5f7a.png`

```text
Use case: precise-object-edit. Asset type: Unity UI background layer extracted from an existing augment card. Input Image 1 is the EXACT edit target, not loose inspiration.
Make exactly ONE change: remove the entire inner near-black pentagonal DESCRIPTION PANEL, including its thin metallic rim, the tiny corner details on that INNER rim, and the tiny INNER bottom-point diamond. Replace that whole inner panel area with uninterrupted dark burgundy velvet, extending the surrounding existing fabric texture seamlessly across it. There must be NO black inset, no remaining outline of the inset, and no extra border where it used to be.
LOCK the outer pennant silhouette, OUTER metal border, outer corner filigree, top suspension bar, existing hanging tassels, understated tone-on-tone burgundy damask, and large OUTERMOST bottom-point metal ornament. Maintain pearl silver/platinum material and original dark brightness, pixel-art style, exact full-canvas composition and original spacing. Do not add motifs, titles, text, swords, labels, new jewels, glows, gradients, black panels, or other UI.
The result is just the FULL uninterrupted velvet banner background with original outer decorations. Keep the same 600:1100 portrait aspect and exact normalized object placement, transparent alpha outside its original silhouette. Preserve input's transparency; do not draw checkerboards. Do not crop or enlarge the pennant.
```

