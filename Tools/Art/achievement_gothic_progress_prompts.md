# Achievement Gothic Metal Progress — 2026-09-22

## 생성 방식과 범위

- 내장 image_gen 사용. Frame / Fill / Tick을 각각 별도로 생성했다.
- 사용자 지정 `01. Gothic Metal` 참조 이미지:
  `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-0c58a2aa-c7f9-4569-ad23-170ce485d733.png`.
- 처음 생성한 Frame/Fill의 체크무늬가 실제 픽셀로 포함되어 있음을 알파 검사로 확인했다. 내장 이미지 편집으로 배경만 제거한 결과를 다시 검사하고 채택했다.
- 코드로 이미지를 다시 그리거나 PNG를 수정하지 않았다. 최종 PNG의 알파를 보존하고 Unity Sprite 영역으로 투명 여백만 제외했다.
- 원본 생성 파일은 생성 폴더에 보존했다. 프로젝트는 아래 복사본만 참조한다.

## 최종 파일 및 Unity 연결

기준 폴더: `Assets/06.UI/LobbyMutedPreview/Collections_v1`.

| 역할 | 실제 PNG | 표시용 Sprite | 대상 |
| --- | --- | --- | --- |
| 프레임 | `Sprites/GothicProgress_Frame.png` | `Styles/GothicProgress_Frame.asset` | `AchievementCard/ProgressRail` |
| 채움 | `Sprites/GothicProgress_Fill.png` | `Styles/GothicProgress_Fill.asset` | `AchievementCard/ProgressFill` |
| 눈금 한 개 | `Sprites/GothicProgress_Tick.png` | `Styles/GothicProgress_Tick.asset` | `AchievementCard/ProgressFill/Dividers`의 Tick Sprite |

최종 생성 원본:
- Frame: `exec-971719e1-b8b6-48b9-b8cb-f29e14ec5788.png`
- Fill: `exec-23e79d25-ce29-442f-b0df-bcd3cbec59d4.png`
- Tick: `exec-8f2f1394-f13f-4061-849a-19a131f06584.png`
- 모두 위 참조 이미지와 같은 생성 폴더에 있다.

Sprite 영역은 PNG 좌하단 기준, 순서 `x,y,width,height`:
- Frame: `27,310,1828,224`
- Fill: `61,457,1464,76`
- Tick: `591,517,58,236`

임포트: Point / 무압축 / Mipmap Off / NPOT None / Clamp / 원본 알파 / Full Rect.
Frame은 320 × 약 39.21, Track/Fill은 270 × 18, 중심은 기존 (71,-23)이다.
프레임을 채움과 눈금보다 나중에 그려 가장자리를 정리한다.
눈금은 Line Width 2.5, Height Ratio 0.4, Bottom Inset 1.5.
진행 중 눈금은 원본 아이보리, 완료 시 (0.28,0.23,0.18,1) 추가 틴트로 밝은 Fill에 대비된다.
Fill은 중립 회색 원본에 기존 카드의 In Progress Color / Completed Color를 곱한다.
숫자/문자/고정 눈금 배열은 어떤 PNG에도 포함하지 않았다.

## 투명도 검사

- Frame: 1882×836, 완전 투명 1,436,953픽셀. 바깥 모서리/중앙 알파 0, 내부 직사각형 샘플 102,200픽셀 모두 알파 16 이하(중앙 포함 투명).
- Fill: 1586×992, 완전 투명 1,457,556픽셀. 모서리 알파 0, 본체 중앙 알파 253.
- Tick: 1240×1269, 완전 투명 1,559,866픽셀. 바깥 여백 투명, 본체 중앙 알파 253.
- 생성 도구의 본체 알파 253~255를 그대로 보존했다.

## 최초 생성 프롬프트

### Frame

```text
Use case: stylized-concept. Asset type: a SINGLE transparent production sprite, only the outer FRAME of the Gothic Metal progress bar shown in the reference.
Reference image: style and shape reference, NOT an image to reproduce whole. Isolate/redraw just ONE empty horizontal frame from that design, no header, no numbers, no ticks, no fill, no dark inner backing.
Aged bone/ivory thin double-line metal upper and lower rails, chamfered corners, compact garnet-red diamond and tiny cross fittings at left and right, perfectly symmetric in geometry. Crisp restrained pixel art, not blurry painting.
IMPORTANT game-fit proportions: sprite bounding box about 9:1 wide-to-high including end ornaments. The long central transparent rectangular aperture is about 86% of total width and 50% of total height. This will be displayed around 320x36 Unity UI units, so the rails need clear consistent weight; restrained weathering, no noisy tiny details.
Single centered frame on a truly transparent RGBA canvas. Both exterior AND the whole central opening must have alpha zero. No black rectangle or fake checkerboard. No gradients/glow outside metal. No words, numbers, segments, icons, sample states. Landscape canvas; all tips intact.
```

### Fill

```text
Use case: stylized-concept. Asset type: single transparent production FILL strip sprite for the Gothic Metal progress bar.
Reference image only shows the desired pixel-art metallic fill style. Generate ONLY a full-width neutral GRAYSCALE horizontal metal/enamel strip, a long RECTANGULAR slab with straight square ends, ratio about16:1. A thin silver-white highlight on the top edge, softly stepped medium gray middle and darker gray lower edge using crisp pixel clusters and subtle restrained metal texture. It must be colorless (equal red/green/blue) so Unity can tint it oxblood for in-progress and antique gold for complete.
NO frame, NO diamonds, NO ticks, NO text, NO knob, NO rounded caps, NO empty track, NO alternate state. No sample background or card. Only a single slim grayscale full fill strip centered in a transparent RGBA landscape image. Interior of the strip fully opaque, exterior truly alpha-zero. Very clean rectangular silhouette so horizontal masking reveals exact progress without feathered edges. Pixel art, limited shades, not blurred, no drop shadow.
```

### Tick

```text
Use case: stylized-concept. Asset type: ONE small transparent pixel-art tick mark sprite for a Gothic Metal progress gauge.
Reference image is a style guide only. Generate only ONE narrow VERTICAL rectangular notch made of aged ivory metal, about1:4 width:height, flat straight top and bottom, pale ivory highlight along left and a muted warm gray side. Simple crisp squared pixel geometry, readable when displayed at2x6 pixels. No ornate shapes.
It is one free-standing vertical little bar, NOT a horizontal gauge or multiple ticks, NOT a frame, NOT an icon or sword, NOT a panel. It will be repeated dynamically by Unity at calculated boundaries, so do not draw repetitions. Centered on a genuinely transparent RGBA canvas with generous clear margin. No black rectangle, checkerboard, glow, shadow, words, numbers or other marks. Opaque notch, transparent exterior.
```

## 투명 배경 수정 프롬프트

### Frame

편집 대상: `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-b4192f9b-e23a-497e-a505-7f1f352d034e.png`

```text
Use case: background-extraction. Edit target: the attached Gothic Metal progress frame. REMOVE the gray checkerboard and mottled background completely, outside AND inside the frame aperture. Deliver a clean isolated gold-and-garnet UI frame on an actual transparent PNG alpha background. Keep only the metal rails, black metal edge outlines and the two symmetrical red jewel finials. Preserve the existing frame shape, size and position, pixel-art details. The center aperture must be 100% transparent. No checkerboard painted into pixels, no backdrop, no fill, no ticks, no text. Genuine transparent background.
```

### Fill

편집 대상: `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-10bebce7-7897-4a31-bce1-87178e41df74.png`

```text
Use case: background-extraction. Edit target: the attached grayscale horizontal metallic progress fill strip. Remove all the gray checkerboard and mottled surrounding background. Keep only the long rectangular gray enamel strip, unchanged in shape, grayscale texture and position. The strip itself must stay opaque. Output actual transparent PNG alpha around the strip, NOT a depiction of checkerboard transparency. No frame ornaments, no additional objects or text. Genuine transparent background.
```

