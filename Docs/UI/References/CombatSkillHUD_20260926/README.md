# 전투 스킬 테두리·하단 배경 레퍼런스

## 범위

- 내장 imagegen으로 생성한 비교용 시안 4종. Unity 씬·프리팹·코드는 변경하지 않았다.
- Unity MCP로 `UI_Battle_MutedPreview > UI_BattleScreens > Canvas_Combat`를 읽기 전용 확인했다. 현재 슬롯은 3개이며 `BottomStrip`은 1920×130, 슬롯은 회전된 162.63 정사각형이다.
- 실제 하단 벨벳 패널과 기존 전투 레퍼런스를 스타일 참고로 사용했다. 준비 화면의 카드·Cost·리롤·전투 시작은 새 Combat 시안에 중복하지 않았다.
- 위쪽은 하단 UI 적용 예시, 아래쪽은 빈 테두리·쿨다운 링·배경의 분리 제작 구상이다. 아래쪽은 추가 스킬 슬롯이 아니다.
- PNG는 디자인 참고용이다. 실제 투명 Sprite나 분리된 UI 리소스가 아니며, 이미지의 수치·링 진행도는 시각 예시다. 선택 후 원본 테두리/아이콘/쿨다운 Fill/TMP를 나누어 제작해야 한다.
- 아이콘은 따뜻한 흰색, 딜/버프/디버프 분류색은 각각 적색/고금색/보라색을 사용한다. 시안의 슬롯 순서는 비교 편의를 위한 예시이며 실제 카테고리를 위치에 고정하지 않는다.

## 시안

| 번호 | 파일 | 방향 |
| --- | --- | --- |
| 01 | [노블 마름모](01_NobleDiamond.png) | 기존 마름모 형태와 통일, 가운데가 살짝 높아지는 벨벳 받침 |
| 02 | [슬림 옥타곤](02_SlimOctagon.png) | 모서리를 깎은 슬롯과 낮은 직선형 바, 간결한 구성 |
| 03 | [마력 인장](03_ArcaneSeal.png) | 원형 프레임과 별도 쿨다운 링, 완만한 곡선 배경 |
| 04 | [문장 받침대](04_CrestDock.png) | 육각 문장과 3개 봉우리형 배경, 상대적으로 장식적인 구성 |

## 검토와 남은 작업

- 각 시안의 주 슬롯 3개, 아이콘 중심, 전체 실루엣과 좌우 균형을 한 번 확인했다.
- 03 최초 출력의 아래쪽 부품 예시에 가로 끊김/중복 오류가 있어, 상단 적용 예시는 유지하고 하단만 도구로 한 번 수정했다. 본 폴더의 03은 수정본이다.
- 프레임의 픽셀 단위 대칭·알파·pivot·Unity 실제 표시 크기는 생산 리소스 제작 단계에서 정렬해야 한다. 현재 이미지로 구현 완료를 의미하지 않는다.
- 01은 기존 게임 형태와의 연속성, 03은 원형 쿨다운의 가독성을 우선할 때 적합하다.

## 참고 이미지

- `Docs/UI/References/BattleHUD_20260923/04_Unified_GothicCommandPanel_v3.png`
- `Assets/06.UI/BattleMutedPreview/LowerLeftReference/Panel_Clean.png`

## 실제 생성 프롬프트

실행 방식: 내장 imagegen. 각 안을 별도 호출했으며 다음 공통 프롬프트와 안별 프롬프트를 합쳐 사용했다.

### 공통

```text
Use case: ui-mockup. Create one professional 16:9 landscape design-reference board for this existing dark-fantasy pixel-art game's COMBAT skill frames and bottom UI background, high-resolution crisp deliberate pixel clusters. Reference image 1 establishes the existing game's thin aged ivory borders and dark visual language. Reference image 2 establishes the actual rich dark burgundy velvet textile and restrained aged-gold noble trim. They are STYLE REFERENCES, not literal layouts to copy. The requested art must look aristocratic, quiet, refined, and handmade pixel-art, NOT ground, dirt, rocky terrain, bright red plastic, neon, blurry gradients, chunky cartoon UI or photorealistic 3D.
Compose a carefully aligned reference sheet on near-black. Upper 65% is a close-up IN-CONTEXT horizontal view of the bottom edge of a dim combat battlefield: only a very faint tile-grid fragment above, exactly THREE large equidistant skill buttons centered on a wide low bottom UI background. Show three warm WHITE icon silhouettes (flame, shield, hourglass). Same structural frame geometry, same center and size for all three; only category metal colors differ: muted oxblood #A5403F, antique gold #BB994B, subdued purple #83519A. Icon faces #F8F2EB, category outlines and very faint internal tint only. A clearly separate thin cooldown arc and translucent dark radial sector on third slot with tiny '8' can illustrate cooldown without changing its category border. First and second are ready. Avoid adding key bindings.
Lower 35% separated by a quiet thin rule: three horizontally arranged isolated production-planning specimens clearly spaced apart: an EMPTY neutral frame without icon, a separate thin white cooldown ring on black, and a scaled full empty background panel with no buttons or letters. This row is for showing separable parts, NOT a fourth playable skill. Make it unmistakable that main playable row has three slots.
All background panel interiors are uniformly dark velvet with subtle coherent damask fabric, no black patch carved inside another velvet patch. Slim metal border follows panel silhouette exactly; horizontal lower edge, symmetric left and right ends. Decorations at small selected corners only, not everywhere. No large hanging diamonds. Keep equal gutters; no element clipping. Three skill slots all fit fully inside canvas with room above and below. Background supports rather than overwhelms them; combat field remains visible.
Do not copy preparation cards, cost, reroll, battle-start control, enemy panel, top HUD, title banners, or existing screenshot text. No card shop. This image focuses only on combat lower UI and reusable frame/background specimens. Use no readable labels except a small variant number at the top-left. No watermark. This is an art reference, not a functional game screenshot or final sliced transparent resource.
```

### 01 노블 마름모

```text
Variant 01 (top-left text '01'): Noble Diamond. Three beautiful precisely symmetric 45-degree DIAMOND metal frames, thin double-step beveled outline with small inset ivory corner joints and a narrow dark inner stroke. Circular cooldown track tucked just INSIDE each diamond without projecting outside its silhouette. Background is one low full-width velvet command rail with a shallow raised central platform under the three diamonds; gently stepped diagonal shoulders at both sides parallel to diamond slopes, narrow quiet lateral wings. Oxblood piping and aged gold exterior rim. Modest filigree only on left/right panel tips. A faithful polished evolution of the existing game; quiet not extravagant.
```

### 02 슬림 옥타곤

```text
Variant 02 (top-left text '02'): Slim Octagon. Three squared skill slots with substantial beveled clipped corners, a restrained octagonal profile (not round, not diamond). A fine inner copper rim plus aged ivory outer bevel. Short corner notches and inset metal grooves, no jewels. Background is a long slim continuous rectangular velvet strip with clipped corners at both ends, perfectly symmetric, fine twin-line aged-gold piping, low visual height. Slots sit partly above the strip and feel securely mounted, never cropped. Elegant minimal commanding design with maximum negative space, no decorative panel peaks. Small cooldown inset as an unobtrusive ring inside each octagon.
```

### 03 마력 인장

```text
Variant 03 (top-left text '03'): Arcane Seal. Three circular medallion skill frames with crisp pixel-circle edges, each framed by a very thin open DIAMOND skeleton sharing four cardinal metal joints, the category-colored circle is the dominant recognizable shape. External rotating-compatible cooldown ring separated by a tiny dark gap. Tiny etched arcane ticks (abstract marks not readable runes), no huge spikes. Background a low smooth-edged velvet ceremonial bridge, shallow symmetrical curved/scalloped top below the three medallions with a straight lower edge, dark antique silver-gold outline, fine filigree at only the two far ends. Visually light yet noble. White icon centers very clear; circular progression is the main functional emphasis.
```

### 04 문장 받침대

```text
Variant 04 (top-left text '04'): Heraldic Crest Dock. Three compact heraldic shield/hexagon skill frames: a short horizontal top, sloping upper shoulders, straight sides and shallow pointed base, exactly matching shapes and centers. Tiny central top crest tab, fine antiqued metal engravings, no crowns and no giant wings. Inner dark beveled border separates white icon and category-color metal. Background a unified very dark velvet dock whose top edge has three subtle equally sized angular raised bays aligned behind the three shields, then descends symmetrically to short lateral wings; straight bottom edge. Dignified Gothic heraldry, oxblood piping and restrained antique gold. The strongest ornate option but still modest, compact and readable, never a bulky throne.
```

### 03 하단 부품 예시 수정

```text
Use case: precise-object-edit. This is the 03 Arcane Seal combat UI reference sheet. Keep the entire TOP HALF and its three skill medallions, white icons, category colors, velvet combat dock, faint battlefield and small '03' marker exactly unchanged. Repair ONLY the lower component-specimen row below the horizontal divider: it currently has a bad duplicated horizontal black smear/split cutting through the empty round frame and cooldown ring, with ghost duplicate contours above the empty velvet strip. Remove that rendering error and all duplicate ghosts. Reconstruct THREE complete clean isolated specimens, each drawn ONCE, aligned in one row centered vertically: left one single complete circular neutral frame with cardinal metal joints and no icon; middle one single complete thin circular cooldown ring with one ivory quarter arc, no number; right one complete single wide low burgundy-velvet background strip with delicate symmetric scalloped upper edge, flat bottom, modest gold rim. No black bands through the art, no doubled lower half, no cropping, no echoes. Preserve same dark noble pixel-art style and lower row bounding boxes. No added text, no new elements.
```

