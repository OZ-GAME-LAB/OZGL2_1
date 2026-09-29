# 전투 하단 HUD 리디자인 레퍼런스 6종

## 목적

- 현재의 넓은 붉은 벨벳 U자 패널을 형태 제약 없이 다시 탐색한다.
- 로비의 픽셀 고딕 스타일을 전투 하단 UI에도 일관되게 적용한다.
- 기존 정보와 조작 위치의 의미는 유지한다.
  - 불꽃 자원과 수치 `100`
  - 리롤 버튼
  - 가넷 화폐 아이콘과 리롤 비용 `100`
  - 중앙 카드 손패
  - 교차검 아이콘과 `전투 시작`

이번 산출물은 디자인 비교용 합성 시안이다. Unity Runtime Sprite, 9-slice 원본 또는 최종 한글 Text 리소스가 아니며 Scene/Prefab에는 연결하지 않았다.

## 공통 스타일 기준

- 기준 이미지: `Assets/06.UI/LobbyMutedPreview/Source/Lobby_NoCurrency_Reference.png`
- 검정·차콜·흑요석 중심, 저채도 가넷 적색 보조
- 따뜻한 상아색 금속 레일과 소량의 낡은 황동
- 마름모 결절, 45도 모서리, 단색의 명확한 기능 아이콘
- 선명한 픽셀 아트와 일관된 픽셀 크기
- 카드 호버 상승 공간과 전장 가시성 확보
- 실제 구현 시 모든 글자와 숫자는 이미지에서 제거하고 TMP로 표시

## 시안

| 번호 | 파일 | 구조 | 적용성 | 주의점 |
| --- | --- | --- | --- | --- |
| 01 | `01_ThroneCommandRail.png` | 왕좌 지휘 레일 | 매우 높음 | 가장 얇고 화면을 적게 가리며 9-slice 분리가 쉬움 |
| 02 | `02_DetachedHeraldry.png` | 분리형 문장패 | 매우 높음 | 기능별 Prefab 분리가 쉬우며 팀 작업 충돌을 줄이기 좋음 |
| 03 | `03_BlackCastleGate.png` | 흑성 관문 | 높음 | 세계관 연결은 강하지만 좌우 성채가 차지하는 면적을 조정해야 함 |
| 04 | `04_BloodMoonRitualAltar.png` | 혈월 의식 제단 | 중간 | 부채꼴 손패와 잘 어울리지만 원형 제단의 화면 점유율이 큼 |
| 05 | `05_DemonStrategyCodex.png` | 마왕의 전술서 | 중간 | 기능 구획이 명확하지만 책 면이 전장을 많이 가릴 수 있음 |
| 06 | `06_BlackWingCommandStand.png` | 흑익 지휘대 | 실험안 | 개성은 가장 강하지만 장식과 카드 실루엣이 경쟁할 수 있음 |

## 추천

1. 기본 구현 후보: **01 왕좌 지휘 레일**
2. 모듈화·팀 협업 우선 후보: **02 분리형 문장패**
3. 세계관 강조 후보: **03 흑성 관문**

실제 적용 단계에서는 먼저 01 또는 02를 선택한 뒤, 다음처럼 Sprite를 분리하는 편이 안전하다.

- 공통 하단 레일 또는 카드 받침
- Cost 문장
- 리롤 문장
- 리롤 비용 명패
- 전투 시작 문장과 명패
- 장식 결절/연결선

## 생성 방식

- OpenAI 내장 `image_gen` 사용
- 사용자 첨부 하단 HUD: 정보·기능 기준
- 현재 전투 전체 화면: 배치와 안전영역 기준
- 로비 기준 화면: 색상·재질·형태 기준
- 여섯 장을 각각 독립 생성했으며, 한 장짜리 콘택트 시트는 사용하지 않았다.

## 공통 최종 프롬프트

```text
Use case: ui-mockup
Asset type: comparison-ready game HUD reference, one design only, not a contact sheet
Input images: Image 1 is the current lower-battle-HUD information reference; Image 2 is the mandatory lobby visual-style reference; Image 3 is the current full battle-screen composition reference. Generate a NEW design, do not edit or copy the reference layout.
Primary request: redesign only the lower battle interface in the same visual language as the lobby: crisp deliberate pixel-art dark fantasy, modular Gothic UI, obsidian black and charcoal, muted garnet red, warm bone-ivory metal rails, tiny aged brass accents, diamond joints and clear warm-white silhouette icons.
Composition/framing: wide 16:9 game UI mockup, front-on orthographic. The upper 65 percent is a simple dim neutral battlefield placeholder with no characters and no UI. The lower 35 percent contains the entire new HUD, fully visible with equal side margins and no cropped corners.
Information that MUST remain clearly visible: left-side flame resource icon with the exact number "100"; a separate circular-arrows reroll control; beneath or beside it a small garnet currency-gem icon with exact number "100"; center card hand with five narrow Gothic cards ordered left-to-right, partially overlapping in a shallow fan and enough open space above for hover lift; right-side crossed-swords primary action with exact Korean label "전투 시작".
Visual hierarchy: center cards must remain the focal interaction area, resource and reroll are secondary, battle-start is the clearest call to action. Keep the battlefield visible; avoid a large opaque slab across the whole width.
Typography: chunky readable Korean pixel Gothic style. Render only the required label and numbers; no extra words.
Constraints: practical shippable Unity UI mockup, strong silhouettes, consistent pixel scale, flat 2D sprite-like rendering, clear separable modules, no logos, no watermark, no photorealism, no 3D perspective, no neon glow, no blurry gradients, no giant skull, no excessive filigree, no card VFX, no targeting arrow, no top HUD, no synergy tracker.
```

## 변형 프롬프트

```text
01 — 왕좌 지휘 레일
Use an extremely slim black-iron double rail hugging the bottom edge. On the left, two compact overlapping heraldic diamonds hold flame resource and reroll/cost. In the center, the five cards rise directly from a low open card rail with almost no opaque background. On the right, a long garnet arrow-ended command nameplate with a crossed-swords diamond forms the battle-start button. Airy, readable, lowest screen occlusion, closest to the lobby's connected diamond rail language.

02 — 분리형 문장패
Remove any continuous bottom panel. Build three independent floating modules: a left flame-resource crest, a smaller reroll-and-cost crest next to it, and a large right battle-start crest with a short label plate. Connect them only with sparse bone-ivory pins and tiny red diamond nodes. The center card hand floats over a nearly invisible thin shelf. Strong modular rhythm, obvious negative space between functions, asymmetrical but balanced.

03 — 흑성 관문
Translate the lobby demon-castle architecture into a low HUD. Use two short pixel-Gothic fortress buttresses at far left and far right, linked by a very low crenellated parapet that supports the cards. Put flame resource and reroll/cost as emblems embedded in the left buttress. Put crossed swords and the battle-start label in a gate-like right buttress. The middle is open and low so card hover space is unobstructed. Dark masonry, restrained garnet banners, ivory metal corners; architectural but not bulky.

04 — 혈월 의식 제단
Create a low open semicircular ritual dais traced by a sparse segmented garnet rune arc. The five cards fan naturally from the arc's center without a large background. Place flame resource and reroll/cost on two ritual nodes at the left end of the arc. Place the crossed-swords battle-start seal as the larger terminal node at the right end. Use small diamond joints, obsidian tiles, and restrained red eclipse motifs. Ceremonial and magical but still flat, practical, and non-glowing.

05 — 마왕의 전술서
Shape the lower interface like a wide open black-leather strategy codex lying flat and front-facing, stylized as pixel UI rather than a realistic book. The left compact page contains flame resource and reroll/cost in stamped diamond emblems. The five physical cards rise from the thin central spine in a shallow fan, leaving hover space. The right compact page corner carries a wax-seal-like garnet battle-start command with crossed swords and label. Keep pages dark and narrow; do not create a huge parchment slab. Tarnished ivory clasps and subtle garnet lining connect it to the lobby.

06 — 흑익 지휘대
Use two abstract low demon-wing silhouettes made of layered black-iron pixel plates, opening outward from the center and leaving the battlefield visible. The left wing joint houses flame resource and reroll/cost as inset diamond controls. The right wing tip forms a broad crossed-swords battle-start command plate. The five cards stand in the open valley between the wings, partially overlapped in a shallow fan. Thin garnet seams and bone-ivory blade edges, dramatic but restrained, no feathers, no creature body, no giant solid panel.
```

## 후속 적용 시 확인할 항목

- 실제 카드 최대 장수와 부채꼴 폭
- 호버 카드 1.2배 확대 시 상단·좌우 잘림 여부
- 16:9 외 화면비의 Safe Area와 좌우 버튼 간격
- 오른쪽 시너지 트래커와 전투 시작 버튼의 겹침 여부
- Cost/리롤이 실제 기능 연결 전 placeholder인지 여부
- Text/TMP, Button, Raycast 영역을 장식 Image와 분리
