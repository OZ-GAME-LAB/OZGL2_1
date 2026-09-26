# 전투 승리 오버레이 레퍼런스 4종

- 생성일: 2026-09-26
- 생성 방식: imagegen 기본 built-in image_gen, 별도 4회 호출. CLI/API fallback 미사용.
- 목적: Figma 전투 승리 정보 구조를 현재 게임 고딕 도트 스타일로 비교하는 이미지 레퍼런스. 실제 Unity 및 Figma 파일은 변경하지 않음.
- 입력 1: Sources/Figma_Victory.png (정보 구성)
- 입력 2: ../BattleHUD_20260923/04_Unified_GothicCommandPanel_v3.png (화풍 및 흐리게 보이는 전투 배경)
- 원본 생성 이미지는 Codex generated_images 위치에 그대로 보존하고 PNG 사본만 저장함.
- 08분 42초, 128명, 12개, 2,400 exp, LV 20, LV UP!은 화면 비교용 예시값/상태이며 실제 전투 기록이 아님.
- 유지 정보: 난이도, 클리어!, 클리어 시간, 처치 용사 수, 유닛 배치 수, 얻은 경험치, 레벨/경험치 바/LV UP!, 로비로 단일 버튼.

## 1회 시각 확인

4종 모두 통계 항목, 경험치 바, 레벨업 표시와 로비로 단일 버튼이 있으며, 후면 전투 화면은 어둡게 유지됨. 03은 제단형 장식이 풍성한 방향, 04는 가장 절제된 방향이다. 03과 04의 클리어 시간 옆 장식 아이콘은 시간 전용 모래시계가 아닌 문양으로 생성되어 실제 구현 채택 시 아이콘을 별도 교체하는 것이 좋다. 텍스트와 아이콘은 레퍼런스에 포함된 래스터이며 구현용 분리 아트가 아니다.

## 최종 프롬프트

### Victory_01_GothicMetal.png

```text
Use case: ui-mockup.
Asset type: polished in-game battle victory overlay concept, ONE full-screen 16:9 landscape image, 2048x1152 composition.
Input images: Image 1 is the Figma source for required information and interaction only. Image 2 is the exact game's visual style and underlying battle scene reference; retain this battle HUD and board faintly behind an approximately 80% black dimming overlay. Do not copy the gray wireframe look of Image 1.
Primary request: create an elegant noble gothic PIXEL ART victory result screen for this Korean dark fantasy defense game. Pixel precision, fine ivory and antique-gold metal framing, dark black and very deep wine velvet surfaces, restrained muted crimson jewels, symmetrical, aligned, readable and polished. Victory dignified rather than carnival. Use crisp intentional stepped pixel edges, not smooth 3D, not blurry illustration.
Information content, exact Korean text: heading "보통 난이도", then much larger "클리어!". Statistics labels and values "클리어 시간" / "08분 42초", "처치 용사 수" / "128명", "유닛 배치 수" / "12개". Earned experience text "얻은 경험치" / "2,400 exp". Below show "LV 20", a partially filled crimson/gold segmented experience bar, and a clearly visible "LV UP!". Exactly one primary action button labeled "로비로". Korean typography must be legible pixel-game font with deliberate size/weight hierarchy. All information is UI, not floating callout annotations.
Constraints: exact listed information and sole button. No retry button, no next stage, no reward currency, no coins, no character portrait cards, no extra gameplay function. No watermark, no Figma editor, no presentation sheet, no modern rounded app buttons. Full screen with margins; main result overlay dominates while battle visible faintly along edges.
Variant 01 GOTHIC METAL: Tall vertically stacked central metal-framed result panel, about 42% screen width and 78% height. Above its top frame place a compact gold crowned demon crest. Header centered. Three statistics arranged as neat left labels and right values in three horizontal rows. Experience area below, single wide lobby button at bottom. Strong original Figma hierarchy but elevate finish using segmented ivory perimeter, small gold corner diamonds, deep crimson interior top banner. Add tiny neutral "01" top left of screen.
```

### Victory_02_NobleVelvet.png

```text
Use case: ui-mockup.
Asset type: polished in-game battle victory overlay concept, ONE full-screen 16:9 landscape image, 2048x1152 composition.
Input images: Image 1 is the Figma source for required information and interaction only. Image 2 is the exact game's visual style and underlying battle scene reference; retain this battle HUD and board faintly behind an approximately 80% black dimming overlay. Do not copy the gray wireframe look of Image 1.
Primary request: create an elegant noble gothic PIXEL ART victory result screen for this Korean dark fantasy defense game. Pixel precision, fine ivory and antique-gold metal framing, dark black and very deep wine velvet surfaces, restrained muted crimson jewels, symmetrical, aligned, readable and polished. Victory dignified rather than carnival. Use crisp intentional stepped pixel edges, not smooth 3D, not blurry illustration.
Information content, exact Korean text: heading "보통 난이도", then much larger "클리어!". Statistics labels and values "클리어 시간" / "08분 42초", "처치 용사 수" / "128명", "유닛 배치 수" / "12개". Earned experience text "얻은 경험치" / "2,400 exp". Below show "LV 20", a partially filled crimson/gold segmented experience bar, and a clearly visible "LV UP!". Exactly one primary action button labeled "로비로". Korean typography must be legible pixel-game font with deliberate size/weight hierarchy. All information is UI, not floating callout annotations.
Constraints: exact listed information and sole button. No retry button, no next stage, no reward currency, no coins, no character portrait cards, no extra gameplay function. No watermark, no Figma editor, no presentation sheet, no modern rounded app buttons. Full screen with margins; main result overlay dominates while battle visible faintly along edges.
Variant 02 NOBLE VELVET: Broad landscape result panel, 72% screen width and 64% height, distinctly split composition. Left third is an elegant long dark wine velvet victory banner with gold embroidered crowned demon heraldry, then centered "보통 난이도" and "클리어!". Right two thirds contain the three very clean left-label/right-value stat rows, with generous breathing space. Along the bottom across the wide panel, experience line, segmented bar, LV20/LV UP, then one centered lobby button. Finely beveled antique-gold border and small crimson diamond corners, wealthy aristocratic mood, very dark. Add tiny neutral "02" top left of screen.
```

### Victory_03_RunicAltar.png

```text
Use case: ui-mockup.
Asset type: polished in-game battle victory overlay concept, ONE full-screen 16:9 landscape image, 2048x1152 composition.
Input images: Image 1 is the Figma source for required information and interaction only. Image 2 is the exact game's visual style and underlying battle scene reference; retain this battle HUD and board faintly behind an approximately 80% black dimming overlay. Do not copy the gray wireframe look of Image 1.
Primary request: create an elegant noble gothic PIXEL ART victory result screen for this Korean dark fantasy defense game. Pixel precision, fine ivory and antique-gold metal framing, dark black and very deep wine velvet surfaces, restrained muted crimson jewels, symmetrical, aligned, readable and polished. Victory dignified rather than carnival. Use crisp intentional stepped pixel edges, not smooth 3D, not blurry illustration.
Information content, exact Korean text: heading "보통 난이도", then much larger "클리어!". Statistics labels and values "클리어 시간" / "08분 42초", "처치 용사 수" / "128명", "유닛 배치 수" / "12개". Earned experience text "얻은 경험치" / "2,400 exp". Below show "LV 20", a partially filled crimson/gold segmented experience bar, and a clearly visible "LV UP!". Exactly one primary action button labeled "로비로". Korean typography must be legible pixel-game font with deliberate size/weight hierarchy. All information is UI, not floating callout annotations.
Constraints: exact listed information and sole button. No retry button, no next stage, no reward currency, no coins, no character portrait cards, no extra gameplay function. No watermark, no Figma editor, no presentation sheet, no modern rounded app buttons. Full screen with margins; main result overlay dominates while battle visible faintly along edges.
Variant 03 RUNIC ALTAR: Central victory ritual composition. Upper portion features restrained circular antique-gold pixel rune halo behind a floating compact demon crown, not a character. Under crown a title ribbon "보통 난이도" and large "클리어!". Lower portion is a wide low black metal statistics altar/tablet with three evenly sized statistic columns, then earned experience and segmented progress bar, and single lobby button below. Overall unified central overlay should fill 65% screen width and 80% screen height. Few red rune embers and diamond joinery; crisp pixel not blurry glow. This is a practical readable game result layout not a fantasy poster. Add tiny neutral "03" top left of screen.
```

### Victory_04_MinimalObsidian.png

```text
Use case: ui-mockup.
Asset type: polished in-game battle victory overlay concept, ONE full-screen 16:9 landscape image, 2048x1152 composition.
Input images: Image 1 is the Figma source for required information and interaction only. Image 2 is the exact game's visual style and underlying battle scene reference; retain this battle HUD and board faintly behind an approximately 80% black dimming overlay. Do not copy the gray wireframe look of Image 1.
Primary request: create an elegant noble gothic PIXEL ART victory result screen for this Korean dark fantasy defense game. Pixel precision, fine ivory and antique-gold metal framing, dark black and very deep wine velvet surfaces, restrained muted crimson jewels, symmetrical, aligned, readable and polished. Victory dignified rather than carnival. Use crisp intentional stepped pixel edges, not smooth 3D, not blurry illustration.
Information content, exact Korean text: heading "보통 난이도", then much larger "클리어!". Statistics labels and values "클리어 시간" / "08분 42초", "처치 용사 수" / "128명", "유닛 배치 수" / "12개". Earned experience text "얻은 경험치" / "2,400 exp". Below show "LV 20", a partially filled crimson/gold segmented experience bar, and a clearly visible "LV UP!". Exactly one primary action button labeled "로비로". Korean typography must be legible pixel-game font with deliberate size/weight hierarchy. All information is UI, not floating callout annotations.
Constraints: exact listed information and sole button. No retry button, no next stage, no reward currency, no coins, no character portrait cards, no extra gameplay function. No watermark, no Figma editor, no presentation sheet, no modern rounded app buttons. Full screen with margins; main result overlay dominates while battle visible faintly along edges.
Variant 04 MINIMAL OBSIDIAN: A wide restrained obsidian-black command panel, 76% screen width and 62% height, thin straight antique-gold/ivory metal lines and only tiny diamond connectors. No huge artwork or extravagant crest. Compact small crown above top-centered "보통 난이도", then prominent "클리어!". Below, a balanced horizontal row of three statistic columns with subtle dividers. Lower row contains earned exp at left and LV20 + segmented experience bar + LV UP at right. Sole centered lobby button at bottom, generous padding and impeccable baseline alignment. Understated luxurious pixel-art design, sharp flat surfaces and very restrained crimson accents. Add tiny neutral "04" top left of screen.
```

