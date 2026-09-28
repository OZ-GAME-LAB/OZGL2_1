# 벨벳 레벨업 등급 표현 4안

- 2026-09-26, built-in image_gen. 각 안별 독립 이미지 편집, 원본 보존.
- 입력: `../LevelUp_02_NobleVelvet.png`.
- 각 이미지에 실버/골드/플래티넘을 나란히 표시한 것은 등급 비교용이며, 현재 코드의 동일 등급 3택1 규칙을 바꾸는 안이 아니다.
- 세 카드에 같은 아이콘/증강명/10% 값을 사용해 등급 표현만 비교했다. 실제 밸런스 수치가 아니다.
- 런 한정 유니크 증강을 기준으로 기존 예시의 `현재 레벨 0`은 제거했다. 로비 특성 레벨을 수정하지 않았다.
- 각 이미지 1회 시각 확인: 세 등급명, 동일 본문, 하단 레벨 제거, 어두운 배경, 세 카드 정렬 확인.
- 평가: 01은 프레임 재질 중심, 02는 등급 리본의 가독성, 03은 장식 실루엣 차이가 가장 분명하다. 04의 문양 차이는 작아 보조 표현으로 적합하다. 래스터 생성 특성상 장식/padding 미세 차이는 실제 구현 때 공통 RectTransform/동일 캔버스 스프라이트로 고정한다.
- Unity/Figma 미적용.

## 01_MetalFrame · 금속 프레임형

출력: `Rarity_01_MetalFrame.png`

```text
Use case: precise-object-edit / ui-mockup.
Input image 1 is the EDIT TARGET: the previously approved Noble Velvet level-up overlay. Preserve its overall dark battle background, black dimming overlay, title plate, three equally sized hanging burgundy velvet pennants, spacing, typography scale, framed text zones, tapered hems and aristocratic gothic PIXEL ART style.
Primary request: create ONE 16:9 complete level-up screen demonstrating THREE RARITY grades, as a design reference. There must still be exactly THREE cards. This is NOT damage/buff/debuff classification.
All three cards now show the SAME white upright sword icon, SAME augment name "공격 강화", SAME description "아군의 공격력이 증가합니다.", SAME bonus "공격력 +10%", No current-level footer: REMOVE the old "현재 레벨 0" field because these are unique run augments, not persistent levelled traits. Retain a dignified pointed lower hem, with no replacement functional control. These are controlled comparison placeholder values; do not invent strength progression, costs or extra game mechanics.
LEFT rarity "실버": neutral brushed antique silver with clear white highlights, no cyan.
MIDDLE rarity "골드": warm muted antique gold, not saturated yellow.
RIGHT rarity "플래티넘": pearl-white platinum with subtle icy-cyan glints and etched facets, not purple and not neon rainbow.
Keep exact common top title "레벨 업", subtitle "증강 하나를 선택하세요". Add a clearly readable small grade plaque "실버" / "골드" / "플래티넘" within the top portion of each card, below the diamond icon and above augment name. Make room by slightly adjusting internal spacing for ALL THREE cards together, never scaling one card differently. All three cards have identical outer dimensions and their icons, grade labels, augment titles, descriptions, bonus values and lower ends align horizontally. All rarity decoration stays within the same reserved frame bounds. Use both name and visual motif so color alone is never required. No stars/numerical rank markers that could be confused with current level.
Respect the current dark aristocratic game tone, black wine-red velvet, crisp stepped pixel edges, detailed but disciplined metal ornament. Content icons remain white. No selected/highlighted card, no cursor, no emphasis resembling hover on the highest grade. Do not use previous red/gold/purple CATEGORY frame coding. No new currency/button/reroll/skip/close control. No extra scene props, no glossy 3D, no watermark. Readable Korean with pixel font title and restrained body size. Return a single edited screenshot, not collage.
VARIANT 01 — Rarity metal frame. Rarity is carried by the ENTIRE diamond border and thin pennant outline, not by recoloring the velvet. Silver has one clean beveled rim; gold has a restrained engraved double rim; platinum has a faceted split rim and small inner diamond inlays, ALL within the same bounding size. Keep identical burgundy velvet on all three, subdued background. Grade plaque is a small matching metal plate. Ornament difference should be visible even in grayscale. Small corner identifier '01' replaces old '02'.
Put a tiny unobtrusive '등급 비교용' note at the bottom outside the cards. This side-by-side rarity comparison does NOT mean the game offers mixed rarities in one draw. Make the three entire cards clearly visible and evenly spaced. The '공격력 +10%' is a controlled placeholder across all three, not an assertion of actual tier balancing.
```

## 02_RankRibbon · 등급 리본형

출력: `Rarity_02_RankRibbon.png`

```text
Use case: precise-object-edit / ui-mockup.
Input image 1 is the EDIT TARGET: the previously approved Noble Velvet level-up overlay. Preserve its overall dark battle background, black dimming overlay, title plate, three equally sized hanging burgundy velvet pennants, spacing, typography scale, framed text zones, tapered hems and aristocratic gothic PIXEL ART style.
Primary request: create ONE 16:9 complete level-up screen demonstrating THREE RARITY grades, as a design reference. There must still be exactly THREE cards. This is NOT damage/buff/debuff classification.
All three cards now show the SAME white upright sword icon, SAME augment name "공격 강화", SAME description "아군의 공격력이 증가합니다.", SAME bonus "공격력 +10%", No current-level footer: REMOVE the old "현재 레벨 0" field because these are unique run augments, not persistent levelled traits. Retain a dignified pointed lower hem, with no replacement functional control. These are controlled comparison placeholder values; do not invent strength progression, costs or extra game mechanics.
LEFT rarity "실버": neutral brushed antique silver with clear white highlights, no cyan.
MIDDLE rarity "골드": warm muted antique gold, not saturated yellow.
RIGHT rarity "플래티넘": pearl-white platinum with subtle icy-cyan glints and etched facets, not purple and not neon rainbow.
Keep exact common top title "레벨 업", subtitle "증강 하나를 선택하세요". Add a clearly readable small grade plaque "실버" / "골드" / "플래티넘" within the top portion of each card, below the diamond icon and above augment name. Make room by slightly adjusting internal spacing for ALL THREE cards together, never scaling one card differently. All three cards have identical outer dimensions and their icons, grade labels, augment titles, descriptions, bonus values and lower ends align horizontally. All rarity decoration stays within the same reserved frame bounds. Use both name and visual motif so color alone is never required. No stars/numerical rank markers that could be confused with current level.
Respect the current dark aristocratic game tone, black wine-red velvet, crisp stepped pixel edges, detailed but disciplined metal ornament. Content icons remain white. No selected/highlighted card, no cursor, no emphasis resembling hover on the highest grade. Do not use previous red/gold/purple CATEGORY frame coding. No new currency/button/reroll/skip/close control. No extra scene props, no glossy 3D, no watermark. Readable Korean with pixel font title and restrained body size. Return a single edited screenshot, not collage.
VARIANT 02 — Dedicated rank ribbon. Keep ALL THREE pennant body outlines and diamond frames in the same original antique-gold/ivory treatment; do NOT recolor their entire frames. The rarity lives mainly in a short wide cloth-and-metal ribbon immediately below each sword diamond, spanning about 70% of card width, containing exact name 실버 / 골드 / 플래티넘. Silver ribbon has a plain edge; gold a subtle double-line braided edge; platinum a delicate geometric filigree edge with pearl accents. The three ribbons share exact size, text size, baseline. White swords and burgundy velvet remain identical, letting compact reusable rarity ribbons do the work. Small corner identifier '02'.
Put a tiny unobtrusive '등급 비교용' note at the bottom outside the cards. This side-by-side rarity comparison does NOT mean the game offers mixed rarities in one draw. Make the three entire cards clearly visible and evenly spaced. The '공격력 +10%' is a controlled placeholder across all three, not an assertion of actual tier balancing.
```

## 03_HeraldicCrest · 문장 장식형

출력: `Rarity_03_HeraldicCrest.png`

```text
Use case: precise-object-edit / ui-mockup.
Input image 1 is the EDIT TARGET: the previously approved Noble Velvet level-up overlay. Preserve its overall dark battle background, black dimming overlay, title plate, three equally sized hanging burgundy velvet pennants, spacing, typography scale, framed text zones, tapered hems and aristocratic gothic PIXEL ART style.
Primary request: create ONE 16:9 complete level-up screen demonstrating THREE RARITY grades, as a design reference. There must still be exactly THREE cards. This is NOT damage/buff/debuff classification.
All three cards now show the SAME white upright sword icon, SAME augment name "공격 강화", SAME description "아군의 공격력이 증가합니다.", SAME bonus "공격력 +10%", No current-level footer: REMOVE the old "현재 레벨 0" field because these are unique run augments, not persistent levelled traits. Retain a dignified pointed lower hem, with no replacement functional control. These are controlled comparison placeholder values; do not invent strength progression, costs or extra game mechanics.
LEFT rarity "실버": neutral brushed antique silver with clear white highlights, no cyan.
MIDDLE rarity "골드": warm muted antique gold, not saturated yellow.
RIGHT rarity "플래티넘": pearl-white platinum with subtle icy-cyan glints and etched facets, not purple and not neon rainbow.
Keep exact common top title "레벨 업", subtitle "증강 하나를 선택하세요". Add a clearly readable small grade plaque "실버" / "골드" / "플래티넘" within the top portion of each card, below the diamond icon and above augment name. Make room by slightly adjusting internal spacing for ALL THREE cards together, never scaling one card differently. All three cards have identical outer dimensions and their icons, grade labels, augment titles, descriptions, bonus values and lower ends align horizontally. All rarity decoration stays within the same reserved frame bounds. Use both name and visual motif so color alone is never required. No stars/numerical rank markers that could be confused with current level.
Respect the current dark aristocratic game tone, black wine-red velvet, crisp stepped pixel edges, detailed but disciplined metal ornament. Content icons remain white. No selected/highlighted card, no cursor, no emphasis resembling hover on the highest grade. Do not use previous red/gold/purple CATEGORY frame coding. No new currency/button/reroll/skip/close control. No extra scene props, no glossy 3D, no watermark. Readable Korean with pixel font title and restrained body size. Return a single edited screenshot, not collage.
VARIANT 03 — Heraldic crest silhouette. Keep all card body frames restrained and common antique-gold, identical burgundy velvet. Around each SAME-SIZE sword diamond create a grade-specific small heraldic attachment inside the same fixed badge region: silver plain angular crest cap and sparse corner brackets, gold balanced slim laurel-like gothic metal flourishes, platinum a symmetrical three-point crown cap and elegant faceted wing-like metal flourishes. Grades use their silver/gold/pearl materials, not green real leaves or huge wings. Ornament density increases clearly but badge outer region stays constant and icons never shrink. A slim readable grade text plaque below each diamond, no full width ribbon. Small corner identifier '03'.
Put a tiny unobtrusive '등급 비교용' note at the bottom outside the cards. This side-by-side rarity comparison does NOT mean the game offers mixed rarities in one draw. Make the three entire cards clearly visible and evenly spaced. The '공격력 +10%' is a controlled placeholder across all three, not an assertion of actual tier balancing.
```

## 04_VelvetWeave · 벨벳 문양형

출력: `Rarity_04_VelvetWeave.png`

```text
Use case: precise-object-edit / ui-mockup.
Input image 1 is the EDIT TARGET: the previously approved Noble Velvet level-up overlay. Preserve its overall dark battle background, black dimming overlay, title plate, three equally sized hanging burgundy velvet pennants, spacing, typography scale, framed text zones, tapered hems and aristocratic gothic PIXEL ART style.
Primary request: create ONE 16:9 complete level-up screen demonstrating THREE RARITY grades, as a design reference. There must still be exactly THREE cards. This is NOT damage/buff/debuff classification.
All three cards now show the SAME white upright sword icon, SAME augment name "공격 강화", SAME description "아군의 공격력이 증가합니다.", SAME bonus "공격력 +10%", No current-level footer: REMOVE the old "현재 레벨 0" field because these are unique run augments, not persistent levelled traits. Retain a dignified pointed lower hem, with no replacement functional control. These are controlled comparison placeholder values; do not invent strength progression, costs or extra game mechanics.
LEFT rarity "실버": neutral brushed antique silver with clear white highlights, no cyan.
MIDDLE rarity "골드": warm muted antique gold, not saturated yellow.
RIGHT rarity "플래티넘": pearl-white platinum with subtle icy-cyan glints and etched facets, not purple and not neon rainbow.
Keep exact common top title "레벨 업", subtitle "증강 하나를 선택하세요". Add a clearly readable small grade plaque "실버" / "골드" / "플래티넘" within the top portion of each card, below the diamond icon and above augment name. Make room by slightly adjusting internal spacing for ALL THREE cards together, never scaling one card differently. All three cards have identical outer dimensions and their icons, grade labels, augment titles, descriptions, bonus values and lower ends align horizontally. All rarity decoration stays within the same reserved frame bounds. Use both name and visual motif so color alone is never required. No stars/numerical rank markers that could be confused with current level.
Respect the current dark aristocratic game tone, black wine-red velvet, crisp stepped pixel edges, detailed but disciplined metal ornament. Content icons remain white. No selected/highlighted card, no cursor, no emphasis resembling hover on the highest grade. Do not use previous red/gold/purple CATEGORY frame coding. No new currency/button/reroll/skip/close control. No extra scene props, no glossy 3D, no watermark. Readable Korean with pixel font title and restrained body size. Return a single edited screenshot, not collage.
VARIANT 04 — Velvet weave / engraved interior. Keep common slim ivory-gold outline frames on all three; rarity distinction comes primarily from the upper velvet cloth panel around the icon and slim inner textile seams, remaining dark and subtle. Silver: black-wine cloth with muted silver straight woven seams. Gold: deep wine cloth with antique-gold damask vine embroidery. Platinum: near-black wine cloth with pearl-silver angular diamond jacquard embroidery and tiny cool highlights, NOT glowing blue cloth. Same white sword diamond has a modest silver/gold/platinum inner lip. Keep all text in clean solid near-black fields. This should look woven and noble, not a noisy pattern across text. Grade name is a clear small plaque; no large crown or wing decorations. Small corner identifier '04'.
Put a tiny unobtrusive '등급 비교용' note at the bottom outside the cards. This side-by-side rarity comparison does NOT mean the game offers mixed rarities in one draw. Make the three entire cards clearly visible and evenly spaced. The '공격력 +10%' is a controlled placeholder across all three, not an assertion of actual tier balancing.
```

