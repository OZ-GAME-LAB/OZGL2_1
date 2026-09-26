# 문장 장식 + 등급별 벨벳 문양

- 2026-09-26, built-in image_gen 이미지 편집 1회.
- 기준: ../Revision02/Rarity_03_HeraldicCrest.png.
- 결과: [Rarity_HeraldicVelvet.png](Rarity_HeraldicVelvet.png).
- 기존 문장/아이콘/텍스트/배치 유지, 카드 안의 직물 부분에 등급별 자수 추가.
- 실버: 성긴 은색 기하 자수. 골드: 고금색 덩굴 다마스크. 플래티넘: 백금색 왕관·마름모 자수.
- 문양은 어깨·양옆·하단 중심으로 배치하고 본문 뒤는 낮은 대비로 유지.
- 1회 시각 확인: 세 등급 문장과 텍스트 유지, 세 문양 차이, 본문 대비 및 카드 정렬 확인.
- 비교용으로 세 등급에 같은 아이콘/효과 예시를 사용. 기존 동일 등급 3택1 규칙/게임 수치/런타임은 변경하지 않음.
- Unity/Figma/Scene/Prefab/Meta 수정 없음. 생성 원본과 앞선 레퍼런스 모두 보존.

## 후속 제작 시 레이어

공통 카드 벨벳 바탕 / 등급 문양 3종 / 등급 문장 장식 3종 / 흰색 증강 아이콘 / Text 또는 TMP / 독립 호버·선택 외곽선으로 분리한다. 문양과 문장은 같은 등급 프리셋이 선택하지만 강도는 별도로 조정 가능하게 한다. 같은 캔버스 크기와 pivot을 사용하며 자수는 클릭 영역을 바꾸지 않는다. 글자 뒤 자수는 흐리게, 밝은 자수는 가장자리에 집중한다. 아직 구현하지 않은 설계 제안이다.

## 생성 프롬프트

```text
Use case: precise-object-edit.
Input image 1 is the exact EDIT TARGET: the approved heraldic rarity level-up screen.
Primary request: combine its existing heraldic rarity crests with distinct, visible, premium VELVET EMBROIDERY PATTERNS for each grade. Create ONE edited full-screen 16:9 reference showing all three complete cards.
LOCK AND PRESERVE: exact three-card layout, card dimensions, positions, header, dark battle background, overlay darkness, all white sword icons, silver/gold/platinum crests (plain silver frame; gold laurel; platinum crown and metallic wings), title/name/description/value typography and baselines, all Korean text, all frames and pennant silhouettes. Do not change card count. Keep each grade label: "실버", "골드", "플래티넘". Keep "공격 강화", "아군의 공격력이 증가합니다.", "공격력 +10%", "레벨 업", "증강 하나를 선택하세요", "등급 비교용". No new labels/buttons or current-level footer.
EDIT ONLY THE FABRIC/BACKGROUND AREAS INSIDE EACH PENNANT:
LEFT SILVER — rich dark charcoal-wine velvet with a sparse, precise, symmetrical SILVER THREAD geometric weave: slim angular diamonds and short straight-line embroidery. Reserved and refined. Its pattern is visibly simpler than the other grades.
MIDDLE GOLD — deep oxblood burgundy velvet with clearly visible MUTED ANTIQUE-GOLD THREAD damask pattern: refined curling acanthus/vine and small fleur motifs, richer than silver but not busy.
RIGHT PLATINUM — very dark black-wine velvet with PEARL-WHITE PLATINUM THREAD geometric brocade, tiny restrained icy highlights: repeated angular crown/diamond/star facets, symmetrical and royal. NOT real feathers, not purple neon, not blue glowing fabric.
Make these THREE textile patterns genuinely distinct at screenshot size, not a barely visible tint swap. Most of the visible pattern should occupy the cloth shoulders around the diamonds, wide left/right cloth margins, and the pointed lower hem. Use the same weave motif at very low contrast behind the text so it remains a continuous velvet surface, not a separate flat black panel glued on top. No bright embroidery directly crossing letterforms. Body text must remain very legible.
Crisp deliberate pixel-art woven clusters, noble gothic game design. Restrained metallic thread glints, matte deep velvet, no photorealism, no smooth shiny 3D, no overall brightening, no glow halos around text. Matching rarity metal/embroidery hue but white skill icon remains white. Do not add or move the existing metal ornament or change its silhouette. Keep the same outer card/hover hit regions.
Output one complete edited game screen, not isolated assets, not a collage, no watermark.
```

