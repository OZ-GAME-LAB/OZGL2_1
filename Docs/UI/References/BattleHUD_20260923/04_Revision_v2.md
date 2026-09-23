# 전투 UI 04번 수정 시안 v2

- 생성 방식: 내장 image_gen 이미지 편집.
- 기준: 04_Unified_GothicCommandPanel.png (원본 보존).
- 결과: [04_Unified_GothicCommandPanel_v2.png](04_Unified_GothicCommandPanel_v2.png)
- 범위: 레퍼런스 이미지 수정만 수행. Unity 씬·프리팹·코드·실제 시너지 데이터는 변경하지 않음.

## 변경 내용

- 상단 성벽 아이콘·체력·게이지 삭제. 웨이브, 레벨/경험치, 남은 시간, 메뉴 유지.
- 상단 바 양끝 돌출 장식과 메뉴·해골의 큰 마름모 장식 삭제, 내부 구분은 얇은 세로선으로 교체.
- 시너지를 마름모 아이콘 + 이름이 있는 가로 영역 + `3 > 5` 발동 임계치로 변경.
- 이름은 사용자 자료의 마법 결속, 사격 대형, 불굴의 뼈, 저주 의식 적용.
- 시너지 위치는 앞선 사용자 요청과 선택한 04번의 오른쪽 배치를 유지. 자료 설명문의 좌측 배치는 적용하지 않음.
- 하단 Cost·리롤·카드·전투 시작 전체 뒤에 어두운 공통 석재/금속 배경 바 추가.
- 자료 3의 개별 버튼 구성·문구·색상은 복사하지 않음.
- 중앙 배치 판, 임시 유닛, 웨이브 적 예고와 하단 기존 조작부 유지.

## 시각 확인

최종 이미지를 1회 확인: 성벽 표시 제거, 단순한 상단 경계/구분선, 시너지 4개 명칭 및 `3 > 5`, 하단 전체 공통 배경을 확인했다.
시너지 임계치 색상과 전투 수치·시간은 표현용 예시다. 기능 구현이나 실제 배치 수 검증 결과를 뜻하지 않는다.

## 편집 프롬프트

```text
Use case: precise-object-edit
Asset type: revised high-fidelity 16:9 dark gothic pixel-art game battle preparation UI, single full-screen image, landscape 2048x1152.
Input images:
- Image 1 is the EDIT TARGET: previously selected concept 04. Keep its central board, rune slabs, temporary unit placement, three lower cards, Cost flame diamond, reroll controls, battle start diamond, upper-left enemy roster and overall restrained gothic pixel-art aesthetic.
- Image 2 is a STRUCTURAL reference only for synergy rows: a diamond icon overlapping the left end of a horizontal ribbon, then threshold text. Its example "5 > 7" must be replaced by "3 > 5". Do NOT copy its flat placeholder colors or the words 특성 이미지.
- Image 3 is DATA reference only. Use the four exact synergy names from its table. Do NOT copy its document/page layout or the prose instruction about left-side placement; the user wants to keep synergies on the RIGHT.
- Image 4 is a CONCEPT reference ONLY for a shared background strip behind the bottom controls. Ignore all of its detailed arrangement, placeholders, extra buttons, typography and colors.

Make exactly these changes to image 1:
1. TOP BAR: remove castle/wall icon, "성벽 82%" and its health fill entirely. Leave ONLY skull + "WAVE 3/10", "LV. 20" + existing red XP gauge, hourglass + "남은 시간" + "00:42", and menu. Redistribute space neatly so removal creates no awkward gap. One continuous quiet dark metal bar with a fine bone-white outline. Remove ALL side finials, side filigree, protruding decorations, giant diamond surrounds around skull/menu and decorative danglers on this top bar. Keep skull and hamburger as simple small white symbols INSIDE the bar. Corners restrained and squared/slightly clipped. Replace the ornate diamond/cross separators inside the bar with thin short vertical engraved ivory lines, evenly spaced, no diamonds or gems on the top bar. Maintain clean pixel-metal style, NOT a generic web navbar.
2. RIGHT SYNERGY: replace the old three rows, generic warrior/archer/mage names and five-diamond pips. Show FOUR carefully aligned horizontal ribbon trackers under the existing heading "시너지", all inside the right-side region and above the bottom action dock. Each row: a moderate diamond emblem overlapping left edge of a dark horizontal nameplate, name on upper line, EXACTLY "3 > 5" on lower line. The number 3 and 5 indicate activation milestones, NOT a fraction: never render 3/5, never 5>7, no filled-diamond pip sequences, no per-row stat paragraphs.
Four row names, spelled exactly and in this order:
"마법 결속" — white staff / arcane star emblem, muted purple accents.
"사격 대형" — white bow / arrows emblem, aged gold accents.
"불굴의 뼈" — white bony shield / sturdy rib-shield emblem, bone ivory accents.
"저주 의식" — white cursed eye / ritual rune emblem, dark plum accents.
Keep icon centers white, only frames and restrained backing tinted. "3 > 5" large enough to read; active first milestone may be warm ivory and unreached 5 muted gray. Elegant compact consistent size, exact vertical spacing. Synergy names must replace, not accompany, old profession names. No sword/전사 row. This uses 3-unit and 5-unit thresholds; no need to print effects.
3. BOTTOM BACKGROUND DOCK: add ONE full-width continuous charcoal-gray stone/metal background BAR behind the entire lower Cost + reroll + three cards + battle-start area, from just beneath the battlefield down to the bottom edge. It must visibly unify every lower control into one shared anchored HUD region, as the gray strip does in reference4. Choose dark graphite gray around #252528, clearly distinguishable from the nearly black battlefield, with restrained pixel-grain texture, a thin subtle worn metal top lip and very subtle dark red undertone. It is not a luminous new panel, not a gray flat prototype, and NOT four or five separate boxes. Original ornate cards and diamond controls remain ON TOP and readable, at the same positions and sizes; no cropped bottoms. Do not add synergy/probability buttons from reference4.

Preserve everything else: central 4x3 board geometry, side rune plaques, units, upper-left "이번 웨이브" enemy tray with knight/archer/elite and x12/x6/x2, bottom card names/art, lower currency100/reroll100, battle start text "전투 시작". Keep original typography and controlled oxblood/bone/gold/purple palette, crisp pixel art and legible Korean. The battlefield stays uncluttered. No extra header, no side-by-side before/after, no watermark, no UI editor chrome, no information not requested. Final is one cohesive refined image.
```

