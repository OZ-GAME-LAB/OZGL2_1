# 전투 패배 레퍼런스 생성 기록

- 날짜: 2026-09-26
- 모드: built-in image_gen, ui-mockup 4종 생성 + 02안 숫자 누락 보완용 precise-object-edit 1회.
- 용도: 디자인 비교용 이미지. Unity Scene/Prefab 및 Figma 디자인은 변경하지 않았다.
- Figma 출처: https://www.figma.com/design/Kz3TNcpjEhLXT7lGoCWinf/?node-id=0-1
- 확인 범위: Figma 메타데이터의 패배 결과 구조와 Unity의 실제 패배 표시 항목을 기반으로 했다. Figma 패배 스크린샷은 도구 조회 제한으로 직접 얻지 못해, 공통 Group79 구조의 승리 화면 스크린샷을 정보 위계 참고로 사용했다.
- 이미지 입력 1: Sources/Figma_Victory.png — 회색 와이어프레임은 정보 위계 참고이며 색상/승리 문구는 복제하지 않음.
- 이미지 입력 2: ../BattleHUD_20260923/04_Unified_GothicCommandPanel_v3.png — 게임의 고딕 도트 톤과 어둡게 깔린 전투 배경 참고.
- 모든 숫자는 디자인 비교용 예시값: 플레이 시간 05분 18초, 처치 용사수 73명, 유닛 배치 수 9개, 얻은 경험치 860 exp, LV.20. LV UP! 역시 레벨업 발생 예시 표시이다.
- 확정된 게임 수치, 보상, 저장 규칙 변경은 아니다. 실제 구현 시 모든 문자/숫자는 교체 가능한 Unity Text/TMP로 분리해야 한다.

## 생성 결과

| 번호 | 파일 | 디자인 방향 |
|---|---|---|
| 01 | Defeat_01_GothicMetal.png | 중앙 세로형 금속 프레임과 부서진 왕관 |
| 02 | Defeat_02_NobleVelvet.png | 짙은 벨벳의 좌우 분할, 파손된 마왕 문장과 통계 |
| 03 | Defeat_03_RunicAltar.png | 꺼진 룬 문장과 제단형 통계판 |
| 04 | Defeat_04_MinimalObsidian.png | 장식을 절제한 가로형 통계 패널 |

## 검토

- 각 결과는 생성 직후 1회 육안 확인했다.
- 01/03/04는 지정한 경험치와 통계, 로비로 버튼이 표시되었다.
- 02 초안의 경험치 숫자만 누락되어 같은 이미지에 860 exp만 추가하는 단일 표적 편집을 수행했다. 수정 부분 1회 확인 완료.
- 패배 02 원본은 Sources/Defeat_02_NobleVelvet_Original.png에 보존했다.
- 생성 원본은 기본 생성 폴더에 유지하고 결과만 프로젝트 문서 폴더로 복사했다.

## 프롬프트

### 01 GothicMetal

```text
Use case: ui-mockup.
Asset type: polished PC dark-fantasy defense game BATTLE DEFEAT overlay design reference, one complete 16:9 landscape screenshot, 2048x1152 if possible.
Input images: Image 1 is a GRAYSCALE FIGMA WIREFRAME of the shared battle-results information hierarchy only, NOT its color/style and NOT its victory message. Image 2 is the actual game's visual STYLE reference and underlying battle-screen backdrop. Do not copy white/gray wireframe panels or cartoon character.
Primary request: Design a luxurious noble Gothic PIXEL-ART defeat overlay, with crisp deliberate square pixels, readable Korean pixel lettering, refined fine metal edges and tasteful tiny diamond ornaments. Very dark matte obsidian, charcoal, muted wine burgundy, ash ivory lettering, cold aged silver trim, faint subdued dark red accents. Feeling of defeat and loss but readable, restrained and elegant; not muddy dirt or rough earth.
Scene/backdrop: the second image's battle interface remains subtly visible under a strong black translucent fullscreen dim layer (about 80% dark). Overlay is clearly dominant. Do NOT brighten the scene, do not blur the foreground. Flat orthographic game UI, no perspective.
Required EXACT foreground copy, no extra labels or extra data: title "패배.." and subheading "보통 난이도"; stat labels/values "플레이 시간" / "05분 18초", "처치 용사수" / "73명", "유닛 배치 수" / "9개"; separate experience section "얻은 경험치" / "860 exp"; one angular segmented experience progress bar with "LV.20" and small subtle "LV UP!" marking a sample level-up; exactly ONE button labeled "로비로". Every label appears exactly once. Lettering ivory with clear hierarchy, title largest, numerical values medium and labels smaller, ample whitespace. Pixel icon accents may accompany the three stats without replacing the labels.
Constraints: match source game's somber pixel Gothic design, polished alignment and equal margins, all foreground text legible, no real-world logos, no watermark. No modern rounded rectangles, no 3D, no photorealism, no painterly smooth illustration, no bloom, no bright neon, no victory trophy, no retry button, no stars/rating, no gold reward, no SP/LP reward, no new highest-wave statistic, no extra currency, no English DEFEAT subtitle. Single image not a collage.
Variant 01 GOTHIC METAL: A medium-wide tall central vertical result panel, about 45% screen width, fully fits within screen height with a generous top and bottom margin. Cold finely detailed aged-silver Gothic frame, almost-black velvet inner surface and very restrained oxblood corner inlays. A small broken demon crown emblem sits at the top center, a few clean symbolic fractures (not damage to readable text). Subheading below emblem then large "패배..". Three aligned label-left/value-right stat rows in the middle. Fine diamond divider, experience section, bar, then centered single "로비로" button at bottom. Symmetrical and noble. Add tiny "01" in upper-left screen corner, outside the overlay.
```

### 02 NobleVelvet

```text
Use case: ui-mockup.
Asset type: polished PC dark-fantasy defense game BATTLE DEFEAT overlay design reference, one complete 16:9 landscape screenshot, 2048x1152 if possible.
Input images: Image 1 is a GRAYSCALE FIGMA WIREFRAME of the shared battle-results information hierarchy only, NOT its color/style and NOT its victory message. Image 2 is the actual game's visual STYLE reference and underlying battle-screen backdrop. Do not copy white/gray wireframe panels or cartoon character.
Primary request: Design a luxurious noble Gothic PIXEL-ART defeat overlay, with crisp deliberate square pixels, readable Korean pixel lettering, refined fine metal edges and tasteful tiny diamond ornaments. Very dark matte obsidian, charcoal, muted wine burgundy, ash ivory lettering, cold aged silver trim, faint subdued dark red accents. Feeling of defeat and loss but readable, restrained and elegant; not muddy dirt or rough earth.
Scene/backdrop: the second image's battle interface remains subtly visible under a strong black translucent fullscreen dim layer (about 80% dark). Overlay is clearly dominant. Do NOT brighten the scene, do not blur the foreground. Flat orthographic game UI, no perspective.
Required EXACT foreground copy, no extra labels or extra data: title "패배.." and subheading "보통 난이도"; stat labels/values "플레이 시간" / "05분 18초", "처치 용사수" / "73명", "유닛 배치 수" / "9개"; separate experience section "얻은 경험치" / "860 exp"; one angular segmented experience progress bar with "LV.20" and small subtle "LV UP!" marking a sample level-up; exactly ONE button labeled "로비로". Every label appears exactly once. Lettering ivory with clear hierarchy, title largest, numerical values medium and labels smaller, ample whitespace. Pixel icon accents may accompany the three stats without replacing the labels.
Constraints: match source game's somber pixel Gothic design, polished alignment and equal margins, all foreground text legible, no real-world logos, no watermark. No modern rounded rectangles, no 3D, no photorealism, no painterly smooth illustration, no bloom, no bright neon, no victory trophy, no retry button, no stars/rating, no gold reward, no SP/LP reward, no new highest-wave statistic, no extra currency, no English DEFEAT subtitle. Single image not a collage.
Variant 02 NOBLE VELVET: Distinct wide horizontally oriented result panel, about 67% screen width and 68% screen height. Two-column layout: left 35% contains a beautifully crisp pixel-art fallen dark demon banner with a fractured crown insignia, a muted solemn symbolic illustration; right 65% has subheading then "패배.." and three elegantly aligned stat rows. Panel interior entirely rich very dark wine velvet, not bright red, with thin aged-silver and desaturated antique-gold noble filigree only on corners. Fine bottom full-width strip contains experience section and bar; single "로비로" centered below inside frame. Distinct from a tall popup, composed refined noble result screen. Add tiny "02" in upper-left screen corner, outside the overlay.
```

### 03 RunicAltar

```text
Use case: ui-mockup.
Asset type: polished PC dark-fantasy defense game BATTLE DEFEAT overlay design reference, one complete 16:9 landscape screenshot, 2048x1152 if possible.
Input images: Image 1 is a GRAYSCALE FIGMA WIREFRAME of the shared battle-results information hierarchy only, NOT its color/style and NOT its victory message. Image 2 is the actual game's visual STYLE reference and underlying battle-screen backdrop. Do not copy white/gray wireframe panels or cartoon character.
Primary request: Design a luxurious noble Gothic PIXEL-ART defeat overlay, with crisp deliberate square pixels, readable Korean pixel lettering, refined fine metal edges and tasteful tiny diamond ornaments. Very dark matte obsidian, charcoal, muted wine burgundy, ash ivory lettering, cold aged silver trim, faint subdued dark red accents. Feeling of defeat and loss but readable, restrained and elegant; not muddy dirt or rough earth.
Scene/backdrop: the second image's battle interface remains subtly visible under a strong black translucent fullscreen dim layer (about 80% dark). Overlay is clearly dominant. Do NOT brighten the scene, do not blur the foreground. Flat orthographic game UI, no perspective.
Required EXACT foreground copy, no extra labels or extra data: title "패배.." and subheading "보통 난이도"; stat labels/values "플레이 시간" / "05분 18초", "처치 용사수" / "73명", "유닛 배치 수" / "9개"; separate experience section "얻은 경험치" / "860 exp"; one angular segmented experience progress bar with "LV.20" and small subtle "LV UP!" marking a sample level-up; exactly ONE button labeled "로비로". Every label appears exactly once. Lettering ivory with clear hierarchy, title largest, numerical values medium and labels smaller, ample whitespace. Pixel icon accents may accompany the three stats without replacing the labels.
Constraints: match source game's somber pixel Gothic design, polished alignment and equal margins, all foreground text legible, no real-world logos, no watermark. No modern rounded rectangles, no 3D, no photorealism, no painterly smooth illustration, no bloom, no bright neon, no victory trophy, no retry button, no stars/rating, no gold reward, no SP/LP reward, no new highest-wave statistic, no extra currency, no English DEFEAT subtitle. Single image not a collage.
Variant 03 RUNIC ALTAR: Distinct ritual-shaped overlay without a generic rectangular tall panel. Upper central cracked diamond demon emblem, surrounded by a thin incomplete dim extinguishing runic halo (very restrained, dark desaturated red, not bright magical glow). Under it centered subheading "보통 난이도" then largest "패배..". Directly beneath, a wide angular altar-like black metal information slab, about 57% screen width, holds three equal clear columns for the required stats (label above value). Under a fine line, experience earned and angular bar, and single centered "로비로" button. All text sits on opaque enough dark flat backing for absolute readability. Sharply cut silver edges, subtle ash dust-like pixel specks restricted to emblem, generous symmetry. Add tiny "03" in upper-left screen corner, outside the overlay.
```

### 04 MinimalObsidian

```text
Use case: ui-mockup.
Asset type: polished PC dark-fantasy defense game BATTLE DEFEAT overlay design reference, one complete 16:9 landscape screenshot, 2048x1152 if possible.
Input images: Image 1 is a GRAYSCALE FIGMA WIREFRAME of the shared battle-results information hierarchy only, NOT its color/style and NOT its victory message. Image 2 is the actual game's visual STYLE reference and underlying battle-screen backdrop. Do not copy white/gray wireframe panels or cartoon character.
Primary request: Design a luxurious noble Gothic PIXEL-ART defeat overlay, with crisp deliberate square pixels, readable Korean pixel lettering, refined fine metal edges and tasteful tiny diamond ornaments. Very dark matte obsidian, charcoal, muted wine burgundy, ash ivory lettering, cold aged silver trim, faint subdued dark red accents. Feeling of defeat and loss but readable, restrained and elegant; not muddy dirt or rough earth.
Scene/backdrop: the second image's battle interface remains subtly visible under a strong black translucent fullscreen dim layer (about 80% dark). Overlay is clearly dominant. Do NOT brighten the scene, do not blur the foreground. Flat orthographic game UI, no perspective.
Required EXACT foreground copy, no extra labels or extra data: title "패배.." and subheading "보통 난이도"; stat labels/values "플레이 시간" / "05분 18초", "처치 용사수" / "73명", "유닛 배치 수" / "9개"; separate experience section "얻은 경험치" / "860 exp"; one angular segmented experience progress bar with "LV.20" and small subtle "LV UP!" marking a sample level-up; exactly ONE button labeled "로비로". Every label appears exactly once. Lettering ivory with clear hierarchy, title largest, numerical values medium and labels smaller, ample whitespace. Pixel icon accents may accompany the three stats without replacing the labels.
Constraints: match source game's somber pixel Gothic design, polished alignment and equal margins, all foreground text legible, no real-world logos, no watermark. No modern rounded rectangles, no 3D, no photorealism, no painterly smooth illustration, no bloom, no bright neon, no victory trophy, no retry button, no stars/rating, no gold reward, no SP/LP reward, no new highest-wave statistic, no extra currency, no English DEFEAT subtitle. Single image not a collage.
Variant 04 MINIMAL OBSIDIAN: Distinct restrained horizontally wide command-summary overlay about 70% screen width and only 60% screen height. Thin clean ivory/aged silver metal frame with tiny corner cuts, almost no ornament. Header row: small cracked demon crown medallion on left, subheading "보통 난이도" and main large "패배.." next to it, visually balanced. Middle has three equal side-by-side statistic cells with extremely fine dividers and clean value emphasis. Bottom experience row with "얻은 경험치", "860 exp", long segmented experience bar and "LV.20" plus subtle "LV UP!". A single compact angular burgundy "로비로" button centered below. Luxurious due to proportions, typography, and dark restrained materials; no busy embellishment. Add tiny "04" in upper-left screen corner, outside the overlay.
```

### 02 숫자 보완 편집

```text
Use case: precise-object-edit / text correction of this existing game UI mockup.
Edit only the supplied image. The bottom experience strip currently shows an icon and "얻은 경험치", then a segmented bar, "LV.20" and "LV UP!", but the earned-experience numeric value is missing.
Add the exact text "860 exp" in readable warm ivory pixel lettering, associated with "얻은 경험치" in that same bottom experience strip, preferably on a small second line below the label, with enough padding so no overlap. Match existing lettering and visual hierarchy.
Absolute invariants: preserve the entire source design, composition, dimensions, background, art, broken banner illustration, all existing words and numbers, all frames, colors, existing experience bar, title, and button. Do not regenerate or restyle anything else, do not brighten the image. Only add "860 exp" once.
```

## 생성 파일 원본 경로

- 01: `C:\Users\sudea\.codex\generated_images\01a0dbd3-2c4b-7fd1-9c60-1f947c5342c2\exec-60c1bca9-cdbe-4989-9407-5835ae9456eb.png`
- 02: `C:\Users\sudea\.codex\generated_images\01a0dbd3-2c4b-7fd1-9c60-1f947c5342c2\exec-9d5853cf-748b-4a79-9256-fdf431533540.png`
- 03: `C:\Users\sudea\.codex\generated_images\01a0dbd3-2c4b-7fd1-9c60-1f947c5342c2\exec-d7d8161a-5422-418b-ba44-f498be4d31a7.png`
- 04: `C:\Users\sudea\.codex\generated_images\01a0dbd3-2c4b-7fd1-9c60-1f947c5342c2\exec-3abba1aa-1ebb-4674-b20a-3ba56cf9bca8.png`
- 02-original: `C:\Users\sudea\.codex\generated_images\01a0dbd3-2c4b-7fd1-9c60-1f947c5342c2\exec-70fb168b-8760-4830-8abd-c68fa4b06a72.png`

