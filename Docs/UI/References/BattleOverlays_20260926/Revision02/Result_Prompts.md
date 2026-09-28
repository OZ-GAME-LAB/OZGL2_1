# 전투 결과 통일 레퍼런스 Revision02

- 작성일: 2026-09-26
- 모드: imagegen 기본 built-in image_gen, 순차 2회 표적 편집. CLI/API fallback 미사용.
- 범위: 이미지 레퍼런스만 제작. Unity Scene/Prefab/C# 및 Figma 파일 변경 없음.
- 기반: 기존 Defeat_03_RunicAltar.png만 레이아웃 기준으로 사용. 기존 Victory_03_RunicAltar.png는 참고하지 않음.
- 생성 흐름: 패배 03 최소 편집 → UnifiedResult_Defeat.png → 해당 완성본만 입력하여 승리 상태 표적 편집 → UnifiedResult_Victory.png.
- 생성 원본은 Codex generated_images에 보존하고 프로젝트에는 사본 저장.
- 이 PNG들은 concept-only 래스터 목업이다. 실제 구현용 공통 부품/상태 스프라이트/TMP가 분리된 리소스가 아니며, 구현 단계에서 공통 RectTransform과 분리 아트로 고정해야 한다.

## 공통 구조

상단 마름모 문장과 룬 → 난이도/결과 제목 → 폭넓은 3열 통계판 → 한 줄 경험치 영역 → 중앙 로비로 버튼. 공통 프레임은 양쪽 모두 앤틱 실버/아이보리이고, 전투 배경 및 암도, 통계 아이콘 홀더, 모래시계/해골/배치 아이콘, 패널 분할선, 경험치 바와 버튼 배치가 유지된다.

## 상태 차이 및 예시값

| 항목 | 패배 | 승리 |
|---|---|---|
| 문장 내부 | 균열이 있는 어두운 왕관 | 온전한 왕관, 은은한 고금색 |
| 룬 | 낮은 적색 | 낮은 고금색 |
| 깃발 | 해진 짙은 와인색 | 동일 슬롯의 온전한 와인색 |
| 제목 | 패배 | 승리 |
| 시간 | 플레이 시간 / 05분 18초 | 클리어 시간 / 08분 42초 |
| 처치 용사 수 | 73명 | 128명 |
| 유닛 배치 수 | 9개 | 12개 |
| 얻은 경험치 | 860 exp | 2,400 exp |

LV 20, LV UP!, 경험치 바 채움과 모든 숫자는 레퍼런스 비교용 예시이며 실제 전투 기록이나 패배 시 레벨업 보장 규칙이 아니다.

## 시각 확인 (각 1회)

- 두 출력은 동일 통계판, 동일 3열, 동일 가로 경험치 줄, 동일 버튼과 후면 배틀 화면을 유지한다.
- 승리 전용 계단·기둥·프레임 추가 없음. 두 화면을 서로 전환해도 중심 문장/통계판/버튼 위치가 눈에 띄게 바뀌지 않는다.
- 시간 아이콘은 패배/승리 모두 모래시계로 수정됨.
- 글자 폭과 깃발 끝 모양, 제목 뒤 장식의 색 등 생성형 편집의 미세 차이는 남는다. 픽셀 단위 동일성이나 실제 공통 프리팹 연결은 검증 대상이 아니며, 해당 레이아웃을 구현할 때 공통 오브젝트를 재사용한다.
- PNG의 레벨 표기는 생성 결과에서 LV.20으로 보이는 부분이 있다. 구현 시 TMP 문구를 LV 20으로 통일한다.

## 패배 기준본 프롬프트

```text
Use case: precise-object-edit.
Asset type: existing dark gothic pixel-art battle result UI mockup, single full-screen 16:9 landscape. Image 1 is the ONLY edit target.
Primary request: minimally revise this existing DEFEAT screen into the master layout shared by defeat AND victory. Keep the original composition and actual positions, dimensions, center axes, spacing, borders, cloth banner mounting points, font sizes, button and all underlying battle-screen pixels unchanged as far as possible. This is NOT a redesign and NOT a new fantasy illustration.
Make ONLY these requested changes:
1. In the existing upper-center diamond emblem replace the horned skull inside the diamond with a solemn fractured dark demon crown, centered in the exact same inner footprint. Crown has an obvious narrow crack and slightly broken upper points. Keep the diamond frame, faint muted red circular rune halo, and outer geometry exactly as they are.
2. Heading retains "보통 난이도". Large result text changes from "패배.." to exactly "패배" (no dots). Preserve its existing typeface, ivory color, size and centered baseline.
3. In the leftmost statistics diamond replace the crossed swords with a simple clearly recognizable ivory PIXEL HOURGLASS icon, well-centered. Keep the diamond holder and size.
4. Standardize the labels without moving them: left column "플레이 시간" and "05분 18초"; middle "처치 용사 수" and "73명"; right "유닛 배치 수" and "9개".
5. Bottom experience row remains aligned exactly in its original single horizontal row: "얻은 경험치", "860 exp", same segmented crimson progress bar at same fill, "LV 20", "LV UP!".
6. Keep the sole "로비로" button exactly in its current position/size/design and keep every common panel edge antique silver/ivory, not golden. Keep original dark wine torn flags in the same shared banner slots.
7. Remove only the small presentation number "03" from the extreme top-left corner, revealing the unchanged dark background there.
Constraints: do not add columns, stairs, pedestals, additional frames, additional ornaments, separate headers, new buttons, portrait, new illustration or currencies. Do not move, enlarge, brighten or crop the UI. Especially preserve the broad stats box, 3 equal columns, thin dividers, the horizontal experience row and centered lobby button beneath. Existing flags/candles can remain, but no new ones. Crisp visible stepped pixel style, dark noble atmosphere and existing background dimming unchanged. Output the full edited game screen, no sheets or annotations.
```

## 승리 상태 표적 편집 프롬프트

```text
Use case: precise-object-edit.
Asset type: a VICTORY STATE of the exact same shared game result overlay, full-screen 16:9 image.
Input image: Image 1 is the ONLY edit target and the canonical pixel-for-pixel layout. Do NOT redesign it. Treat this as swapping a few state sprites and label strings in the same Unity prefab. The result MUST be visibly the same screen, not a different victory layout.
Change ONLY the following small state-specific details:
1. Replace the fractured dark crown INSIDE THE EXISTING TOP DIAMOND with an intact dignified dark demon crown of the same silhouette footprint, same position/size, subtle antique-gold highlights. Do not alter the surrounding silver diamond frame at all.
2. Recolor ONLY the existing circular runes behind that diamond from low crimson to subtle muted antique-gold. Keep identical circle diameter, glyph positions and low intensity; no extra glow or circle.
3. Keep both existing side flag mounting posts and all banner slots EXACTLY fixed. Replace only the torn wine-red flag cloth with intact wine-red cloth with a finished hem, same cloth bounds and same tiny gold trim. Likewise the two little cloth tails below stats retain the same bounds but intact hems. Do NOT add flags, poles, pedestals, stairs, statues, walls or props.
4. Same typography, centered baseline and two-character heading size: replace "패배" with exactly "승리". Above it keep "보통 난이도" unchanged.
5. Same three statistics columns with same icons (left HOURGLASS, middle skull, right pawn) and same x/y baselines. Left label changes to "클리어 시간" and value "08분 42초". Middle "처치 용사 수" value "128명". Right "유닛 배치 수" value "12개".
6. Same SINGLE HORIZONTAL experience row: keep "얻은 경험치"; change "860 exp" to "2,400 exp" in its existing allotted value area. Keep the exact segmented bar geometry, original fill amount and red fill color, and keep "LV 20"/"LV UP!" in their identical slots. Values are illustrative.
7. The sole bottom button still reads "로비로" and stays perfectly unchanged.
Absolute invariants: every common border stays identical antique silver/ivory (DO NOT make the stats frame gold); all panel corners, dimensions, lower frame protrusions, vertical divider positions, typography family, all icon holders, button design, screen layout, dimmed battle background, exposure, brightness and vignette unchanged. Do not shift, rescale, crop or embellish any of these. Preserve original dark noble pixel art. No new effects beyond the subdued rune/crown accents. This must look like one shared prefab with only state artwork and data text swapped. Output just the edited full game screen without labels, annotations or sheets.
```

