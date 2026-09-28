# 03 문장 + 04 벨벳 단순 결합

- 생성 방식: built-in image_gen, 두 원본을 사용한 편집 1회.
- 결과: [Rarity_03Crest_04Velvet.png](Rarity_03Crest_04Velvet.png).
- 04의 카드 몸체·절제된 벨벳·등급별 외곽선을 기준으로, 03의 상단 문장 장식과 등급 표식만 결합했다.
- 앞선 Revision03의 과도한 자수안은 사용하지 않았다. 기존 파일은 모두 보존했다.
- 시각 확인 1회: 03 상단 문장/04 카드 바탕, 새 대형 자수 없음, 세 카드 및 글자 정렬 확인.
- 등급/수치는 비교용. Unity/Figma/게임 데이터는 변경하지 않았다.

## 프롬프트

```text
Use case: compositing.
Make a minimal combination of TWO existing UI designs. Do not invent ANY additional decoration.
Image 1 (04) is the BASE: preserve its entire battle background, dimming, three velvet card bodies, quiet near-black burgundy woven patterns, slim silver/gold/platinum outer edges, header, white sword icons, text content, text positions, card sizes, and spacing.
Image 2 (03) supplies ONLY the top HERALDIC BADGE assemblies: left silver angular sword frame; middle gold sword diamond with metal laurel; right platinum sword diamond with crown and angular wings. Transfer these three top badge assemblies, with their small grade name plaques, onto the corresponding three cards of Image 1. Keep all three cards equal size and aligned. Maintain text baseline spacing below the transferred grade plaques. Use the refined text and icon scale from the original images.
CRITICAL: the velvet fabric and tiny pattern intensity must remain EXACTLY as quiet and dark as Image 1. Do not embroider new large shapes. No extra vines, no extra crown motifs on the cloth, no chains or gems across the fabric, no bright silver/gold stitched outlines, no increased pattern density, no newly drawn border ornaments, no additional glow. The upper metal crest from Image 2 is the focal decoration; the fabric from Image 1 stays subdued. It should look like a simple clean combination, not a richer embellished redesign.
Preserve verbatim all Korean text and demo values: 레벨 업, 증강 하나를 선택하세요, 실버, 골드, 플래티넘, 공격 강화, 아군의 공격력이 증가합니다., 공격력 +10%, 등급 비교용. Preserve the white swords. Keep near-black text fields clear. No extra buttons or current level.
Crisp existing pixel-art style, same full-screen 16:9 composition. Return one combined complete UI screenshot, not a comparison grid.
```

