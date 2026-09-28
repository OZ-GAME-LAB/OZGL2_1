# 난이도 기록 프레임 호버 확장 v2 — 생성 기록

- 생성 방식: 내장 image_gen, 기본/확장 2상태 비교 목업.
- 입력: Temp/LobbyDifficulty/Difficulty_Final_HeroSet.png (현재 로비 참고).
- 생성 원본: C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-8f076a65-0048-411d-b400-a6cb2cbd3d5e.png
- 결과: Docs/UI/References/LobbyDifficulty/difficulty_record_frame_hover_v2.png
- 용도: 검토용 예시, 실제 UI 에셋이나 Scene에 연결하지 않음.
- 육안 확인: 기본/확장 패널의 하단 기준선·이름/설명·전투 준비 위치가 정렬되며, 확장부는 위쪽에 작은 기록 2열을 표시한다. 25 웨이브 / 08:42는 예시다.
- 상단 비교 제목과 커서는 예시 설명용이다. 실제 아트는 텍스트/TMP, 아이콘, 프레임 캡, 레일을 분리해야 한다.

## 최종 프롬프트

```text
Use case: ui-mockup.
Asset type: polished two-state comparison board for a Korean gothic pixel-art game's HOVER-EXPANDING difficulty information plaque.
Input image 1: the actual implemented lobby screenshot, authoritative reference for appearance and existing geometry. Use its CENTRAL normal-difficulty diamond portrait, small name plaque, and battle button.
Create ONE landscape design comparison board, 2 equal side-by-side panels, same exact camera crop, scale, backdrop and alignment in both. Headings outside the game UI at top: left "기본 상태", right "마우스 오버". Each panel is a CLOSE-UP of the central knight diamond, difficulty name plaque below it, and battle preparation button below the plaque. Crop out distant HUD and bottom navigation rather than shrinking all UI. Include subtle edges of neighboring cards for context. No third panel. Pixel-art game mockup, not wireframe.
Most important interaction: SAME plaque in both panels, SAME WIDTH, SAME BOTTOM EDGE, SAME difficulty label baseline and SAME button position. The right plaque extends UPWARD ONLY into the lower part of the portrait. Do not move or shrink the diamond portrait, its center red frame, the underlying art, or the button. Do not put a second floating tooltip above it.
Left/base state: slim two-line blackened-metal plaque occupying the original screenshot's small caption footprint. Text exactly "보통", then smaller "중무장 기사단과의 전투". No record info in default state.
Right/hover state: frame's top edge lifted by roughly the height of the original compact plaque (height changes approx 100 -> 205 relative to 1920x1080 layout). The lower portion is still the SAME "보통" and "중무장 기사단과의 전투" on the SAME baseline as left. In the newly revealed UPPER portion, two quiet equal columns: left a TINY trophy beside the small label "최고 클리어 웨이브", beneath it value "25 웨이브"; right a TINY hourglass beside small label "클리어 시간", beneath it value "08:42". Thin faint antique-brass vertical divider between columns and small horizontal divider above the fixed lower difficulty information. No "기록" extra header. Labels and icons deliberately understated and much smaller than the difficulty title: labels 55% of title text height, trophy/hourglass icon 50% of title height, values 75% of title height. Values readable, but not giant. Add one subtle mouse cursor inside the right plaque near its lower-right interior without obscuring text.
NEW FRAME AESTHETIC BOTH STATES: symmetrical slim tarnished oxblood/crimson metal outline, restrained warm antique-brass hairline inner edge, near-black charcoal-burgundy opaque fill, small angled corner caps, one tiny central diamond ornament on top/bottom edge, clean tailored pixel-art. A premium aged-metal game control, not a bulky decorative monument. No huge side wings, no large corner spikes, no bright red glow, no gold baroque scrolls. Vertical rails repeat cleanly. Top and bottom ornaments maintain identical thickness, size and shape between states: the side walls are lengthened, not the whole sprite stretched. Pixel grid and restrained shading match the reference; avoid smooth vector, painterly or 3D UI.
Visual invariants: the diamond portrait in both panels is identical in size and screen position; expansion overlays ONLY its lower tip/lower torso and NEVER hides the knight face. The button "전투 준비" stays unchanged in design, size and baseline. Both plaque bottom edges align exactly. Do not enlarge the plaque horizontally or move anything downward. Same compact-to-expanded design identity.
Keep original dark castle and eclipse palette. Show a balanced understandable side-by-side comparison with ample but not excessive margins. No extra explanation blocks, no watermark. Exact Korean text.
```

