# Skill settings v1 — 생성 리소스 기록

## 도구 / 보존 정책

내장 `image_gen` 도구를 사용했다. CLI/API 키 방식은 사용하지 않았다. 승인된 스킬 세팅 시안을 스타일/배치 참고 이미지로 사용하고, Unity에 들어가는 텍스트 없는 분리 리소스를 생성했다.

이 폴더의 PNG는 생성 원본이다. 최초 체크 배경 버전 `Icons.png`도 보존한다. 최종 아이콘은 `Icons_Chroma.png`에서 추출했다. 프로젝트에 사용되는 최종 PNG는 `Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites`에 모두 보관한다.

후속 사용자 요청에 따라 생성 배경은 실제 화면에서 사용하지 않는다. 기존 `Lobby_Background.png`와 검은색 Alpha 0.30 오버레이로 교체했으며, 생성 배경 원본/추출본은 보존한다.

## 최종 프롬프트 구성

아래는 리소스별 재생성용 최종 요청 사양이다. 모든 요청에 승인 시안의 다크 판타지 픽셀 아트, 오래된 아이보리 금속 장식, 저채도 가넷/먹색/보라, 기존 형태 보존, 글자·숫자·워터마크 제외를 공통 적용했다.

### 1. Background.png

Use case: stylized-concept. Generate only the background environment of the approved skill-settings reference: a dark gothic castle courtyard, stepped stone foreground, distant black spires, dark red eclipse sky and restrained cyan flames. Preserve the wide front-facing composition and pixel-art treatment. Remove all UI, frames, labels, icons, numbers, bars, panels and character portraits. Keep the center and sides unobtrusive for later Unity UI overlays. No text, no watermark.

### 2. Buttons.png

Use case: ui-mockup / production sprite sheet. Create text-free dark gothic pixel-art button families matching the reference, arranged as three columns and five rows. Columns: compact category tab, medium equip/unequip action button, wide ornate garnet save button. Rows: normal, hover, pressed, selected, disabled. Preserve family silhouettes and scale across states. Ivory aged metal outlines, dark charcoal interiors, muted red selected accent, subdued gray disabled state. Flat orthographic 2D sprites, separated by generous empty margins. Transparent background. No letters, words, numbers, captions, UI mockup or watermark.

### 3. Chrome.png

Use case: ui-mockup / production sprite sheet. Generate isolated text-free gothic pixel UI components: normal square skill card, purple selected square card, two-row stat frame with separator, red diamond equipped frame, purple diamond equipped frame, crowned title plaque, thin circular/triangular ornamental connector arrangement, heading divider and ivory back arrow. Match the approved reference's aged ivory/garnet/black palette and clean pixel edges. Keep dark card/frame interiors opaque and the area outside each component transparent. Separate components; no text or numbers, no character portrait, no full-screen mockup.

### 4. Icons.png → Icons_Chroma.png

Use case: stylized-concept / production icon sheet. Create twelve isolated pixel-art skill symbols in a three-column, four-row grid matching the reference. Reading order: red fire, ivory focus reticle, red lightning strike; ivory sword, purple abyss spiral, chained ivory crown; ivory crescent, garnet curse seal, ivory trident; purple vortex, ivory crosshair, red portal. Icons only, no card frames, labels, numerals or background scene. Center every icon in its cell with ample separation. Preserve dark gothic material and crisp pixels.

Targeted final edit: keep all twelve icons, their positions, silhouettes and colors unchanged; replace only the baked checker background with a uniform pure chroma green `#00FF00` so the empty areas and icon holes can be removed cleanly. Do not add green inside the icon artwork or change the arrangement.

## 승인된 후처리

- `Tools/Art/BuildSkillSettingsAssets.inputs.json`: 생성 파일명, 셀 분할 좌표, 출력 크기 기록.
- `Tools/Art/BuildSkillSettingsAssets.cjs`: 원본 보존, 셀 분할, 외부 체크/초록 배경 투명 처리, 비율 유지 nearest-neighbor 축소, 중앙 정렬.
- 마스크 외곽 정리 이외 새 그림/문양을 코드로 그리거나 색상을 다시 칠하지 않았다.
- 최초 투명 요청 결과에 체크가 실제 픽셀로 포함되어 있어 사용자 승인 범위에서 정리했다. 아이콘의 갇힌 체크 흔적은 생성 도구로 배경만 재편집한 뒤 제거했다.
- Unity import: Sprite/Single, Point, mipmap off, Uncompressed, Full Rect.
- `ArtValidation.json`: 스프라이트별 원본/분할/출력 크기 기록.

## 재현

Node.js와 `sharp`가 설치된 환경에서 저장소 루트 기준 `node Tools/Art/BuildSkillSettingsAssets.cjs`를 실행한다. 저장소에 보관한 Sources 파일을 우선 사용하므로 일반 재실행에는 개인 generated_images 경로가 필요하지 않다. PNG 재생성 뒤 Unity에서 기존 .meta를 유지해 임포트한다.
