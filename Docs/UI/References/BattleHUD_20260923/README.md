# 전투 UI 레퍼런스 4안 (2026-09-23)

## 범위

- 이미지 생성 내장 도구로 만든 비교용 시안이다. Unity 씬·프리팹·코드 및 실제 게임 수치는 변경하지 않았다.
- 원본 레퍼런스의 Cost·리롤·카드·전투 시작과 중앙 배치 판을 유지하는 방향이다.
- 이번 웨이브 적은 좌측 상단 WAVE 영역, 시너지 정보는 우측으로 배치했다.
- 비교 조건을 맞추기 위해 모두 준비 화면을 사용했다. 전투 중 하단 스킬 UI는 이번 시안 범위에 포함하지 않았다.
- 텍스트·적 수량·체력·시간·카드 효과는 시각 비교용 예시이며 실제 밸런스 또는 데이터 명세가 아니다.

## 시안

- **01 분리형 · 가로 정찰 트레이**: [이미지](01_Segmented_EnemyTray.png)
- **02 분리형 · 상세 목록 패널**: [이미지](02_Segmented_EnemyRoster.png)
- **03 일체형 · 슬림 커맨드 바**: [이미지](03_Unified_SlimCommandBar.png)
- **04 일체형 · 고딕 지휘 패널**: [이미지](04_Unified_GothicCommandPanel.png)

## 확인한 구현 맥락

- `Assets/00.Scenes/UI_Flow/UI_Battle.unity`를 Unity MCP로 읽기 전용 확인했다.
- Canvas_GetReady의 BattleHUD, SynergySummary와 Canvas_Preparation의 리롤·카드, Canvas_Combat의 스킬 슬롯은 구분되어 있다.
- 직업 시너지는 전사·방패병·궁수·마법사·도적·힐러이며 3/5명 단계가 존재한다. 시안은 전사·궁수·마법사만 대표 표시했다.
- 웨이브 적 표시를 연결할 때는 RoundDefinition.Spawns의 적 ID·수량을 표시용 카탈로그와 연결해야 한다. 이번에는 연결 구현하지 않았다.

## 1회 시각 검토

- 4개 모두 좌측 상단 웨이브 적 표시, 우측 시너지, 하단 Cost·리롤 및 카드 영역을 확인했다.
- 01·02는 분리형 HUD, 03·04는 연속된 단일 외곽 프레임 HUD로 생성되었다.
- 생성 이미지 속 작은 수치·문구·눈금은 실제 적용 시 TMP와 개별 UI 요소로 다시 구성해야 한다.

## 생성 프롬프트

생성 방식: 내장 image_gen. 입력 이미지 1은 준비 화면, 입력 이미지 2는 전투 화면 스타일 참조로 사용했다.

### 01 분리형 · 가로 정찰 트레이

```text
Use case: ui-mockup
Asset type: full-screen 16:9 dark fantasy pixel-art strategy game battle PREPARATION screen, polished visual design reference, 2048x1152 landscape.
Input images: Image 1 is the primary layout/style reference for the central board, lower Cost/reroll controls and card offers. Image 2 is the supporting style reference for the HUD, wall health and right-side synergy presentation. Generate a new alternative UI, not a comparison collage.
Style: preserve the supplied game's restrained gothic pixel art: bone-white fine metal borders, tiny diamond rivets, worn oxblood red accents, near-black charcoal panels, crisp deliberate pixel clusters. No smooth vector UI, no plastic, no 3D mockup, no neon, no bloom, no thick blurry lines. Legible Korean labels in clean pixel-style typography. Flat front-facing game screenshot, edge to edge, no monitor surround.
Composition invariants: central 4 by 3 rectangular placement grid with two slim rune slabs at sides and only three small temporary friendly unit sprites (hooded swordsman, mage, archer), largely unobstructed. Grid approximately x=22–77%, y=33–67%. Near-black battle background with barely visible stone texture. Bottom area must preserve the reference's large oxblood diamond with white flame and 100 at lower left; smaller adjacent diamond circular-arrows reroll button with small cost 100 plaque below. Preserve three detailed card offers along the lower middle labeled "그림자 검사", "가시 지대", "마력 증폭기", with short descriptions/art and rank 1 badges; large crossed-swords "전투 시작" diamond at lower right. No need to copy tiny card paragraphs. Do NOT put equipped-skill buttons on top of the card offers.
Required information: top WAVE 3/10, LV.20 with muted red XP fill, wall/tower icon "성벽 82%" with small fill, hourglass "00:42", menu icon. The current wave enemy roster MUST be in UPPER LEFT directly attached to or under WAVE, never on right: sword-and-shield knight x12, archer x6, elite axe knight x2, heading "이번 웨이브". Right side MUST be a synergy display, with real job categories "전사", "궁수", "마법사": icon, count and five diamond pips, threshold marker at 3 and 5. Example current counts 3/5, 2/5, 1/5; subtly muted rows not yet active; distinguish sword, bow, staff silhouettes. These are design sample values, not combat effects. Use "시너지" heading. Never duplicate the enemy roster elsewhere.
All text and numbers must align precisely with visual centers and consistent padding; maintain clean quiet space between UI regions. Do not obscure bottom controls or central battlefield with panels. No title banner describing the concept, no watermark.
Variant 01: CLASSIC SEGMENTED HUD + HORIZONTAL RECONNAISSANCE TRAY.
Top HUD consists of three visibly separate familiar framed plates: WAVE on left, a wide center plate for LV + XP + wall HP, a separate time plate near right and separate menu diamond. Use the thin bone-white, fine-rivet borders of reference 1, with breathing gaps.
Directly beneath the WAVE plate at upper left, attach a low compact horizontal dark tray about 30% of screen width: the heading "이번 웨이브" at its top, then 3 enemy bust icons side by side with x12, x6, x2 immediately below. Small role labels "근접", "원거리", "정예". The tray stops well above the grid and feels attached to the wave module.
Right margin around x=82–97%, y=27–61%: three separate lightweight horizontal synergy rows rather than a large heavy panel. Each has a small job diamond emblem left, name+3/5 etc right, five tiny milestone diamonds beneath. Red muted activated warrior row, subdued others. This is the most faithful restrained evolution of the existing reference.
```

### 02 분리형 · 상세 목록 패널

```text
Use case: ui-mockup
Asset type: full-screen 16:9 dark fantasy pixel-art strategy game battle PREPARATION screen, polished visual design reference, 2048x1152 landscape.
Input images: Image 1 is the primary layout/style reference for the central board, lower Cost/reroll controls and card offers. Image 2 is the supporting style reference for the HUD, wall health and right-side synergy presentation. Generate a new alternative UI, not a comparison collage.
Style: preserve the supplied game's restrained gothic pixel art: bone-white fine metal borders, tiny diamond rivets, worn oxblood red accents, near-black charcoal panels, crisp deliberate pixel clusters. No smooth vector UI, no plastic, no 3D mockup, no neon, no bloom, no thick blurry lines. Legible Korean labels in clean pixel-style typography. Flat front-facing game screenshot, edge to edge, no monitor surround.
Composition invariants: central 4 by 3 rectangular placement grid with two slim rune slabs at sides and only three small temporary friendly unit sprites (hooded swordsman, mage, archer), largely unobstructed. Grid approximately x=22–77%, y=33–67%. Near-black battle background with barely visible stone texture. Bottom area must preserve the reference's large oxblood diamond with white flame and 100 at lower left; smaller adjacent diamond circular-arrows reroll button with small cost 100 plaque below. Preserve three detailed card offers along the lower middle labeled "그림자 검사", "가시 지대", "마력 증폭기", with short descriptions/art and rank 1 badges; large crossed-swords "전투 시작" diamond at lower right. No need to copy tiny card paragraphs. Do NOT put equipped-skill buttons on top of the card offers.
Required information: top WAVE 3/10, LV.20 with muted red XP fill, wall/tower icon "성벽 82%" with small fill, hourglass "00:42", menu icon. The current wave enemy roster MUST be in UPPER LEFT directly attached to or under WAVE, never on right: sword-and-shield knight x12, archer x6, elite axe knight x2, heading "이번 웨이브". Right side MUST be a synergy display, with real job categories "전사", "궁수", "마법사": icon, count and five diamond pips, threshold marker at 3 and 5. Example current counts 3/5, 2/5, 1/5; subtly muted rows not yet active; distinguish sword, bow, staff silhouettes. These are design sample values, not combat effects. Use "시너지" heading. Never duplicate the enemy roster elsewhere.
All text and numbers must align precisely with visual centers and consistent padding; maintain clean quiet space between UI regions. Do not obscure bottom controls or central battlefield with panels. No title banner describing the concept, no watermark.
Variant 02: SEPARATE CRESTED HUD + VERTICAL ENEMY LIST AT TOP LEFT + DETAILED RIGHT PANEL.
Use a more compact segmented HUD: a skull-crested WAVE plate occupying upper-left 20%, central distinct LV/XP plate, separate wall-health plate, separate time plate/menu. Their frame corners are clipped gothic metal, aged ivory/oxblood, not generic boxes.
Extend the WAVE module down into a compact vertical enemy roster at x=2–19%, y=14–42%, three aligned rows: small knight portrait "근접 용사 ×12", archer "원거리 용사 ×6", axe knight "정예 용사 ×2". Heading "이번 웨이브". Icons and text equally spaced. Roster narrow enough not to cover central grid and well above Cost controls.
Right x=82–97%, y=26–63%: ONE framed "시너지" information panel with three stacked tidy rows. Rows show larger job emblem, "전사 3/5", "궁수 2/5", "마법사 1/5", five milestone diamonds, and tiny threshold labels "3 / 5". One active row has subtle muted red underlay. Strong bilateral balance, more informative than variant 1 while the central grid remains clear. Fine decoration, not heavy oversized armor.
```

### 03 일체형 · 슬림 커맨드 바

```text
Use case: ui-mockup
Asset type: full-screen 16:9 dark fantasy pixel-art strategy game battle PREPARATION screen, polished visual design reference, 2048x1152 landscape.
Input images: Image 1 is the primary layout/style reference for the central board, lower Cost/reroll controls and card offers. Image 2 is the supporting style reference for the HUD, wall health and right-side synergy presentation. Generate a new alternative UI, not a comparison collage.
Style: preserve the supplied game's restrained gothic pixel art: bone-white fine metal borders, tiny diamond rivets, worn oxblood red accents, near-black charcoal panels, crisp deliberate pixel clusters. No smooth vector UI, no plastic, no 3D mockup, no neon, no bloom, no thick blurry lines. Legible Korean labels in clean pixel-style typography. Flat front-facing game screenshot, edge to edge, no monitor surround.
Composition invariants: central 4 by 3 rectangular placement grid with two slim rune slabs at sides and only three small temporary friendly unit sprites (hooded swordsman, mage, archer), largely unobstructed. Grid approximately x=22–77%, y=33–67%. Near-black battle background with barely visible stone texture. Bottom area must preserve the reference's large oxblood diamond with white flame and 100 at lower left; smaller adjacent diamond circular-arrows reroll button with small cost 100 plaque below. Preserve three detailed card offers along the lower middle labeled "그림자 검사", "가시 지대", "마력 증폭기", with short descriptions/art and rank 1 badges; large crossed-swords "전투 시작" diamond at lower right. No need to copy tiny card paragraphs. Do NOT put equipped-skill buttons on top of the card offers.
Required information: top WAVE 3/10, LV.20 with muted red XP fill, wall/tower icon "성벽 82%" with small fill, hourglass "00:42", menu icon. The current wave enemy roster MUST be in UPPER LEFT directly attached to or under WAVE, never on right: sword-and-shield knight x12, archer x6, elite axe knight x2, heading "이번 웨이브". Right side MUST be a synergy display, with real job categories "전사", "궁수", "마법사": icon, count and five diamond pips, threshold marker at 3 and 5. Example current counts 3/5, 2/5, 1/5; subtly muted rows not yet active; distinguish sword, bow, staff silhouettes. These are design sample values, not combat effects. Use "시너지" heading. Never duplicate the enemy roster elsewhere.
All text and numbers must align precisely with visual centers and consistent padding; maintain clean quiet space between UI regions. Do not obscure bottom controls or central battlefield with panels. No title banner describing the concept, no watermark.
Variant 03: ONE UNBROKEN SLIM COMMAND BAR + COMPACT ENEMY STRIP + RIGHT SYNERGY RAIL.
Top must read unmistakably as a SINGLE continuous horizontal metal-framed bar spanning x=3–97%, y=3–10%. One shared dark background, one uninterrupted bone-white outer edge, no individually framed boxes for wave, level or time. Fine vertical separators only. Inside left-to-right: skull WAVE 3/10, LV.20 and long red XP fill, tower 성벽82%, hourglass00:42, menu. Align all on a common baseline. Tiny crimson studs at outer ends; strongest clarity and least decoration.
Enemy preview emerges as a compact low wing from the underside of the LEFT WAVE segment (x=3–31%,y=10–23%): "이번 웨이브", three compact knight/archer/elite icons horizontally with x12/x6/x2. Thin bottom line connects this wing visually to the parent bar. Keep all enemies in this upper-left wing.
Right x=83–97%, y=29–59%: a slim open-backed vertical synergy rail headed "시너지", three clean rows with emblem, name and current 3/5,2/5,1/5, five pips each. No big opaque box; crisp row dividers and subtle dark backplates only. Battlefield has maximum negative space. Preserve lower controls faithful to reference1. Overall modern information hierarchy inside the established pixel gothic style.
```

### 04 일체형 · 고딕 지휘 패널

```text
Use case: ui-mockup
Asset type: full-screen 16:9 dark fantasy pixel-art strategy game battle PREPARATION screen, polished visual design reference, 2048x1152 landscape.
Input images: Image 1 is the primary layout/style reference for the central board, lower Cost/reroll controls and card offers. Image 2 is the supporting style reference for the HUD, wall health and right-side synergy presentation. Generate a new alternative UI, not a comparison collage.
Style: preserve the supplied game's restrained gothic pixel art: bone-white fine metal borders, tiny diamond rivets, worn oxblood red accents, near-black charcoal panels, crisp deliberate pixel clusters. No smooth vector UI, no plastic, no 3D mockup, no neon, no bloom, no thick blurry lines. Legible Korean labels in clean pixel-style typography. Flat front-facing game screenshot, edge to edge, no monitor surround.
Composition invariants: central 4 by 3 rectangular placement grid with two slim rune slabs at sides and only three small temporary friendly unit sprites (hooded swordsman, mage, archer), largely unobstructed. Grid approximately x=22–77%, y=33–67%. Near-black battle background with barely visible stone texture. Bottom area must preserve the reference's large oxblood diamond with white flame and 100 at lower left; smaller adjacent diamond circular-arrows reroll button with small cost 100 plaque below. Preserve three detailed card offers along the lower middle labeled "그림자 검사", "가시 지대", "마력 증폭기", with short descriptions/art and rank 1 badges; large crossed-swords "전투 시작" diamond at lower right. No need to copy tiny card paragraphs. Do NOT put equipped-skill buttons on top of the card offers.
Required information: top WAVE 3/10, LV.20 with muted red XP fill, wall/tower icon "성벽 82%" with small fill, hourglass "00:42", menu icon. The current wave enemy roster MUST be in UPPER LEFT directly attached to or under WAVE, never on right: sword-and-shield knight x12, archer x6, elite axe knight x2, heading "이번 웨이브". Right side MUST be a synergy display, with real job categories "전사", "궁수", "마법사": icon, count and five diamond pips, threshold marker at 3 and 5. Example current counts 3/5, 2/5, 1/5; subtly muted rows not yet active; distinguish sword, bow, staff silhouettes. These are design sample values, not combat effects. Use "시너지" heading. Never duplicate the enemy roster elsewhere.
All text and numbers must align precisely with visual centers and consistent padding; maintain clean quiet space between UI regions. Do not obscure bottom controls or central battlefield with panels. No title banner describing the concept, no watermark.
Variant 04: ONE CONTINUOUS ORNATE GOTHIC COMMAND PANEL WITH ATTACHED ENEMY WING.
Top HUD has ONE shared wider dark metal frame spanning x=3–97%,y=3–13%, a single connected silhouette with pointed outer tips, tiny crimson diamond joints, thin bone-white highlight. Do NOT divide into detached boxes. Same continuous background connects every data field. Inside, generous single horizontal information line left-to-right skull WAVE3/10, LV.20, castle 성벽82%, hourglass00:42 and menu. A long slender recessed red XP trough sits directly below the LV region INSIDE the same shared frame; wall HP has its own small trough under its label. Compact hierarchy: numbers bold, labels secondary.
The left WAVE area has an attached shallow DOWNWARD wing at x=3–35%, y=13–27%, connected to the same frame. Wing heading "이번 웨이브", three enemy portraits in a horizontal row, each with role name and x12/x6/x2. Knights rendered in small crisp pixel busts. The wing is balanced with the right content and stays clear of the central grid.
Right side x=82–97%,y=27–63%: a gothic-framed "시너지" panel with three stacked rows whose medallions overlap the left edge slightly; sword/warrior red, bow/archer aged gold, staff/mage muted purple icon outlines, white icon centers. Names 전사/궁수/마법사, 3/5,2/5,1/5 and five progress pips. The active row restrained warm illumination, others dark. This variant feels a little more ceremonial and richly framed than the slim unified bar, but decoration never impairs hierarchy and alignment. Lower cost/reroll/cards/start stay faithful.
```

