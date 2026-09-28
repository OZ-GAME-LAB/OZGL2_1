# 난이도 기록 프레임 예시 v1

- 요청: 난이도 이름·설명을 유지하면서 최고 클리어 웨이브와 클리어 시간을 표시할 새 프레임 구상.
- 생성 방식: 내장 image_gen, 기존 로비 스크린샷을 바탕으로 한 UI 편집 예시.
- 결과: `Docs/UI/References/LobbyDifficulty/difficulty_record_frame_v1.png`
- 입력: `Temp/LobbyDifficulty/Difficulty_Final_HeroSet.png`
- 생성 원본: `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-68a38b2e-033b-454c-90ce-4f879bc04b89.png`
- 예시 수치: 25 웨이브 / 08:42. 실제 저장 기록을 조회한 값이 아니다.
- 이번 작업은 디자인 예시만 생성했으며 Scene, Prefab, 런타임 코드, 전투 데이터는 변경하지 않았다.

## 구성 의도

- 상단: 난이도 이름과 한 줄 설명.
- 하단: 트로피 + 최고 클리어 웨이브 / 모래시계 + 클리어 시간, 2열 구성.
- 적용 시에는 외곽 프레임, 내부 배경, 구분선, 아이콘을 분리하고 모든 텍스트·수치는 TMP로 표시한다.
- 현재 난이도 전환의 페이드 방식은 유지하고 난이도별 기록을 함께 갱신한다.
- 생성 예시는 공간 확보를 위해 중앙 카드와 하단 버튼 간격을 일부 재구성했다. 실제 적용 시에는 기존 RectTransform 기준으로 겹침을 다시 검증해야 한다.
- 클리어 시간의 집계 기준(전체 난이도 클리어 시간 또는 표시 웨이브의 기록)은 실제 데이터 연결 전에 별도로 확정한다.

## 최종 프롬프트

```text
Use case: precise-object-edit, ui-mockup.
Asset type: high fidelity Korean pixel-art Unity lobby UI design preview, full screen 16:9.
Input image 1 is the edit target: the currently implemented actual lobby screenshot. Preserve the existing scene and all other UI, only redesign the SELECTED CENTER difficulty information plaque to accommodate two record statistics. This is an approval mockup, not a sprite sheet.
Keep exactly: dark gothic ruined castle, crimson eclipse, black-red charcoal backdrop, three diamond-shaped human army portrait cards with easy left, selected normal center in crimson, hard right in ivory; existing left/right arrow buttons, top-left level HUD, top-right menu, bottom four navigation icons and labels. Preserve each card's portrait art, visual style, relative position and frame style. Maintain the bottom-center battle preparation button design and wording; if essential shift it down a few pixels only, without overlapping the bottom navigation. No new windows, no new controls.
Primary change: replace the cramped center horizontal 2-line plaque below the normal portrait with a thoughtfully designed WIDER and TALLER gothic metal RECORD PLAQUE, about 620 pixels wide and 166 pixels tall relative to a 1920x1080 screen, centered around x=960 and y=640. Its top may overlap the lowest tip of the center diamond, but must not cover the knight's face or chest. Retain clear separation from the battle preparation button underneath. Side difficulty name plaques stay simple and unchanged.
NEW PLAQUE DESIGN: crisp intentional pixel-art, near-black burgundy opaque inner panel so scenery cannot compete with text, symmetrical weathered crimson outer metal edge, fine aged brass/ivory inner keyline, tasteful small diamond corner jewels, restrained antique luxurious dark-fantasy ornament. Avoid bulky spikes and oversized flourishes. The plaque silhouette has beveled/chamfered ends and a tiny central red diamond crest that ties it to the selected card. No bright orange, neon, glossy modern rendering, blur, excessive bevels or smooth gradient.
HIERARCHY inside new plaque: two stacked zones.
Upper zone: centered difficulty title exactly "보통" in prominent warm ivory pixel Korean text, then the smaller single-line description exactly "중무장 기사단과의 전투". Narrow restrained ornamental horizontal divider below description.
Lower zone: two equal side-by-side stat cells separated by a thin vertical antique metal rule. In left cell a small ivory trophy icon, label exactly "최고 클리어 웨이브", then prominently "25 웨이브" below. In right cell small ivory hourglass icon, label exactly "클리어 시간", then prominently "08:42" below. Titles muted warm gray, values brighter warm ivory, no colored pill containers. Generous text padding, clear row/column alignment, no overlap. These are sample values.
Keep all original Korean labels precisely: "쉬움", "어려움", "전투 준비", "업적", "특성", "스킬 세팅", "도감", HUD "LV.20".
This should look like the same game's production UI with a carefully expanded center information frame, not a different game. Preserve pixel grid, crisp borders, actual screenshot layout and all unrelated UI. Full screen output, no montage, no annotations, no design-sheet title, no watermark.
```

