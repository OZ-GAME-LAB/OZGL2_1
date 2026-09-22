# 로비 난이도 레퍼런스 생성 기록

- 방식: 기본 내장 `image_gen` 사용. CLI/API 대체 경로 미사용.
- 용도: 4개 구도 비교 시안. 각 시안은 쉬움 / 보통 / 어려움 3개 카드로 구성한다. Unity에 바로 사용하는 최종 Sprite 시트가 아니다.
- 입력 1(스타일 참조): `D:/GitHub/OZGL2_1/Temp/LevelHud/LevelHud_Slider_Play.png`
- 입력 2(프레임 언어 참조): `D:/GitHub/OZGL2_1/Assets/06.UI/LobbyMutedPreview/StageLayers_v1/Sprites/StageFrame_Neutral.png`
- 기존 씬/이미지를 편집하는 작업이 아니라 위 입력을 참고해 새 비교판을 생성한다.
- 최종 적용 시 선택된 안을 기준으로 테두리/글자 없는 난이도별 Artwork 3개를 별도로 생성하고 기존 마름모 Mask 안에 배치한다. 비교판의 프레임과 글자는 실제 Unity 리소스에 포함하지 않는다.
- 최종 보관 위치: `Docs/UI/References/LobbyDifficulty/` (씬에 미연결).

## 생성 결과 (2026-09-22)

| 비교판 | 프로젝트 보관 파일 | 보존한 원본 파일명 |
| --- | --- | --- |
| 01 | `Docs/UI/References/LobbyDifficulty/01_castle_siege_v1.png` | `exec-d512f7d5-cfcd-45bd-b0e5-0a621c1598f4.png` |
| 02 | `Docs/UI/References/LobbyDifficulty/02_hero_threat_v1.png` | `exec-ce7f1a4b-6ffe-4402-acdc-231e3731e73e.png` |
| 03 | `Docs/UI/References/LobbyDifficulty/03_faction_clash_v1.png` | `exec-29295269-0a34-4591-ab5a-1e8867de0097.png` |
| 04 | `Docs/UI/References/LobbyDifficulty/04_gate_march_v1.png` | `exec-8af13120-403d-433d-858e-afa60e4a099f.png` |

원본 폴더: `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/`.
생성 결과를 변형 없이 복사했다. 4개 비교판의 난이도 순서와 구도를 검토했으며 실제 Unity 마스크 연결/동작 검증은 아직 하지 않았다.

## 01. 마왕성 공성전

파일: `01_castle_siege_v1.png`

```text
Use case: stylized-concept.
Asset type: dark fantasy pixel-art difficulty selection concept comparison sheet for an existing Unity game; PREVIEW ONLY, not final packed sprites.
Create one finished wide landscape comparison board with THREE equally sized diamond framed illustrations side by side, each diamond upright and identical aspect ratio 1:1. Very dark charcoal blank board background, generous clear gutters. Each art scene fills the complete diamond interior and is naturally composed specifically for this diamond mask: main focal subject within the central 55 percent, secondary scenery reaches all four tips, no important faces, weapon ends or focal towers cut by the inner frame. Use the supplied lobby screenshot only as STYLE reference and supplied neutral frame only as FRAME LANGUAGE reference, not an edit target; do not reproduce the whole lobby. Keep the same subtly worn antique ivory-metal diamond border with small geometric corner ornaments across all three difficulty cards. Difficulty is communicated by the illustration, NOT by changing frames.
Style: refined gothic dark fantasy game pixel art matching the reference, clearly defined deliberate pixel clusters, restrained rich detail, crisp silhouettes, deep charcoal stone, dark crimson highlights, tarnished bone/ivory metal, desaturated warm accents. NOT photorealistic, NOT smooth digital painting, NOT vector, NOT cartoon, NOT candy colors, no glossy mobile-game styling. Ensure thumbnail readability.
Put a neat small concept title at top of board, and exactly three crisp Korean labels centered below diamonds, left to right: "쉬움", "보통", "어려움". Text outside the artwork only, no wording/numbers embedded inside the art. No lock icons, no progression bars, no interface buttons, no extra cards, no watermark.
The player defends the demon king's castle from invading human heroes: harder difficulty means a more threatening enemy invasion, not a stronger player castle. Same atmosphere and visual family across all three.
Architecture-led composition. Repeat the SAME recognizable black gothic demon citadel, stable central castle silhouette and stone causeway, viewed from a medium distant elevated front angle, reaching the upper diamond tip. Easy: a small scouting troop approaching far below, a quiet slate sky with faint ember windows, spacious misty causeway and modest threat. Normal: an organized column of armored knights and several spears advancing toward the gate, muted red clouds, a few torchlights. Hard: dense elite human siege army filling the causeway toward the same citadel, visible heavy siege engine silhouettes, a crimson eclipse and controlled firelight around the outer walls. The CASTLE remains readable in all panels, threat increases through enemy density and distance. Keep roughly half the image legible stone and sky, avoid meaningless tiny noise. Title verbatim "01. 마왕성 공성전".
```

## 02. 용사군의 위협

파일: `02_hero_threat_v1.png`

```text
Use case: stylized-concept.
Asset type: dark fantasy pixel-art difficulty selection concept comparison sheet for an existing Unity game; PREVIEW ONLY, not final packed sprites.
Create one finished wide landscape comparison board with THREE equally sized diamond framed illustrations side by side, each diamond upright and identical aspect ratio 1:1. Very dark charcoal blank board background, generous clear gutters. Each art scene fills the complete diamond interior and is naturally composed specifically for this diamond mask: main focal subject within the central 55 percent, secondary scenery reaches all four tips, no important faces, weapon ends or focal towers cut by the inner frame. Use the supplied lobby screenshot only as STYLE reference and supplied neutral frame only as FRAME LANGUAGE reference, not an edit target; do not reproduce the whole lobby. Keep the same subtly worn antique ivory-metal diamond border with small geometric corner ornaments across all three difficulty cards. Difficulty is communicated by the illustration, NOT by changing frames.
Style: refined gothic dark fantasy game pixel art matching the reference, clearly defined deliberate pixel clusters, restrained rich detail, crisp silhouettes, deep charcoal stone, dark crimson highlights, tarnished bone/ivory metal, desaturated warm accents. NOT photorealistic, NOT smooth digital painting, NOT vector, NOT cartoon, NOT candy colors, no glossy mobile-game styling. Ensure thumbnail readability.
Put a neat small concept title at top of board, and exactly three crisp Korean labels centered below diamonds, left to right: "쉬움", "보통", "어려움". Text outside the artwork only, no wording/numbers embedded inside the art. No lock icons, no progression bars, no interface buttons, no extra cards, no watermark.
The player defends the demon king's castle from invading human heroes: harder difficulty means a more threatening enemy invasion, not a stronger player castle. Same atmosphere and visual family across all three.
Character-led compositions of the INVADING HUMAN enemies, without demon horns. In all panels a central upright human adversary from mid-thigh up, head near upper inner quadrant, weapon held diagonally contained inside the diamond, dim silhouettes of their troop behind. Easy: lightly equipped human foot soldier with simple dull steel helmet, worn mail/leather armor, small round shield and plain short sword; 2 distant scouts, low threat. Normal: a veteran armored knight in a closed steel helmet, disciplined plate armor, long sword and shield, 6 coherent spear silhouettes behind. Hard: a imposing elite human paladin commander with ornate aged-ivory plate armor, a narrow tarnished-gold luminous halo-like sacred sigil behind the helm and a greatsword pointed down, a dense regiment of elite knights behind. Dark medieval seriousness, no anime facial styling, no horns on human heroes, no modern weapons. Enemy armor silhouette and troop formation should escalate naturally, not just color. Title verbatim "02. 용사군의 위협".
```

## 03. 양 진영의 격돌

파일: `03_faction_clash_v1.png`

```text
Use case: stylized-concept.
Asset type: dark fantasy pixel-art difficulty selection concept comparison sheet for an existing Unity game; PREVIEW ONLY, not final packed sprites.
Create one finished wide landscape comparison board with THREE equally sized diamond framed illustrations side by side, each diamond upright and identical aspect ratio 1:1. Very dark charcoal blank board background, generous clear gutters. Each art scene fills the complete diamond interior and is naturally composed specifically for this diamond mask: main focal subject within the central 55 percent, secondary scenery reaches all four tips, no important faces, weapon ends or focal towers cut by the inner frame. Use the supplied lobby screenshot only as STYLE reference and supplied neutral frame only as FRAME LANGUAGE reference, not an edit target; do not reproduce the whole lobby. Keep the same subtly worn antique ivory-metal diamond border with small geometric corner ornaments across all three difficulty cards. Difficulty is communicated by the illustration, NOT by changing frames.
Style: refined gothic dark fantasy game pixel art matching the reference, clearly defined deliberate pixel clusters, restrained rich detail, crisp silhouettes, deep charcoal stone, dark crimson highlights, tarnished bone/ivory metal, desaturated warm accents. NOT photorealistic, NOT smooth digital painting, NOT vector, NOT cartoon, NOT candy colors, no glossy mobile-game styling. Ensure thumbnail readability.
Put a neat small concept title at top of board, and exactly three crisp Korean labels centered below diamonds, left to right: "쉬움", "보통", "어려움". Text outside the artwork only, no wording/numbers embedded inside the art. No lock icons, no progression bars, no interface buttons, no extra cards, no watermark.
The player defends the demon king's castle from invading human heroes: harder difficulty means a more threatening enemy invasion, not a stronger player castle. Same atmosphere and visual family across all three.
Action-led symmetrical opposing diagonal composition, human heroes approaching from right, demon defenders from left, their sword/shield contact focal point dead center within the safe interior. Black castle gate recedes at the upper diamond tip and ruined courtyard falls toward lower tip. Easy: a demon foot soldier defending against one lightly armed human swordsman, clear two readable silhouettes, restrained amber sparks, some breathing room. Normal: a horned dark-armored demon guard crossing blades with a veteran human knight, a few skirmishing silhouettes behind and stronger central orange sparks, balanced threat. Hard: a demon guard bracing defensively against a towering HUMAN paladin with greatsword and holy light, pressing ranks of knights behind him, broad ivory-vs-crimson central impact light and intense siege atmosphere, but crisp readable silhouettes. More enemy pressure rather than bigger player army. No gore or dismemberment. Keep all principal heads and contact points well inside diamond edges. Title verbatim "03. 양 진영의 격돌".
```

## 04. 성문을 향한 진군

파일: `04_gate_march_v1.png`

```text
Use case: stylized-concept.
Asset type: dark fantasy pixel-art difficulty selection concept comparison sheet for an existing Unity game; PREVIEW ONLY, not final packed sprites.
Create one finished wide landscape comparison board with THREE equally sized diamond framed illustrations side by side, each diamond upright and identical aspect ratio 1:1. Very dark charcoal blank board background, generous clear gutters. Each art scene fills the complete diamond interior and is naturally composed specifically for this diamond mask: main focal subject within the central 55 percent, secondary scenery reaches all four tips, no important faces, weapon ends or focal towers cut by the inner frame. Use the supplied lobby screenshot only as STYLE reference and supplied neutral frame only as FRAME LANGUAGE reference, not an edit target; do not reproduce the whole lobby. Keep the same subtly worn antique ivory-metal diamond border with small geometric corner ornaments across all three difficulty cards. Difficulty is communicated by the illustration, NOT by changing frames.
Style: refined gothic dark fantasy game pixel art matching the reference, clearly defined deliberate pixel clusters, restrained rich detail, crisp silhouettes, deep charcoal stone, dark crimson highlights, tarnished bone/ivory metal, desaturated warm accents. NOT photorealistic, NOT smooth digital painting, NOT vector, NOT cartoon, NOT candy colors, no glossy mobile-game styling. Ensure thumbnail readability.
Put a neat small concept title at top of board, and exactly three crisp Korean labels centered below diamonds, left to right: "쉬움", "보통", "어려움". Text outside the artwork only, no wording/numbers embedded inside the art. No lock icons, no progression bars, no interface buttons, no extra cards, no watermark.
The player defends the demon king's castle from invading human heroes: harder difficulty means a more threatening enemy invasion, not a stronger player castle. Same atmosphere and visual family across all three.
Over-the-shoulder defensive viewpoint FROM INSIDE the demon castle gateway looking outward; two looming dark gothic gate piers in the upper diagonals and the road converging on a vanishing point near the upper-middle of the diamond. Focus is the invading human formation coming directly TOWARD the viewer. Easy: three isolated human scouts on a quiet foggy road outside the gate, open spaces and narrow dull-amber torches. Normal: a tightly ordered shield wall approaching, rows of human spears and restrained banners, dusty warm-gray light, mounting siege tension. Hard: an overwhelming elite knight formation headed by a readable heavily armored human paladin, larger armored silhouettes tightly filling the central road, a distant siege tower and red smoke; ominous pale-gold sacred light threatening the dark gate. Keep a central simple visual hierarchy; no player avatar needed. Distinguish this from castle panorama: the castle is a close framing doorway, NOT a remote building. Same camera for all three stages. Title verbatim "04. 성문을 향한 진군".
```
