# 난이도 기록 프레임 리소스 생성·후처리

> 아래는 교체 전 사각형 시안의 생성 기록이다. 승인 레퍼런스와 실루엣이 달라 프레임 5종을 재생성했다. 현재 적용본과 프롬프트는 [레퍼런스 일치 수정](lobby_difficulty_record_frame_correction_prompt.md)을 따른다. 아래 구형 후처리를 실행하면 수정 전 디자인으로 돌아가므로 현재 리소스 재생성에는 사용하지 않는다.

- 생성 방식: 내장 image_gen. 승인된 호버 확장 v2 시안을 스타일 참조로 사용.
- 생성 원본: Tools/Art/Sources/DifficultyRecordHover/Frame_Generated_Original.png
- 생성 도구 원본 경로: C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-29ce3210-e746-4d7c-8a1c-080b4d0ba057.png
- 원본은 1774x887 RGB이며 체크무늬가 실제 픽셀로 포함되어 있었다. 사용자 승인 후 외곽에서 연결된 밝은 무채색 배경만 제거했다.
- 후처리 도구: Tools/Art/PrepareDifficultyRecordHoverArt.ps1. PowerShell/System.Drawing, 원본 보존, 배경 제거·분할·NearestNeighbor 크기 정렬만 수행.
- 외곽 연결 배경 제거 후 유효 영역: x61,y73,w1652,h740. 내부 밝은 금속 장식은 연결되지 않아 보존된다.
- 출력 폴더: Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/RecordHover/Sprites/
- 출력 8종: Frame_Top, Frame_Bottom, Frame_LeftRail, Frame_RightRail, Frame_Body, Divider, Icon_Trophy, Icon_Hourglass.
- 트로피는 Collections_v1/Sprites/CompletedTrophy_v2.png, 모래시계는 Overlays/TreeArt_v1/Sprites/Icons/Spells/Icon_Spells_Acceleration.png, 구분선은 Overlays/DetailArt_v2/Divider_CurrentNext.png를 재사용했다. 기존 파일은 변경하지 않고 알파 여백/크기만 정렬한 사본이다.
- Unity 임포트: Sprite/Single, FullRect, Point, Mipmap Off, Uncompressed, Clamp. .meta는 Unity Importer가 생성한다.
- 프레임 상하 캡의 종횡비와 좌우 레일 두께는 확장 중 고정한다. 레일은 타일 표시, 몸체는 별도 Image.
- 모든 문구와 숫자는 TMP이며 이미지에 포함되지 않는다.

## 최종 생성 프롬프트

```text
Use case: stylized-concept.
Asset type: ONE production game UI frame sprite, empty, no text, on genuinely transparent alpha background. The provided reference is style only, specifically the red expandable information plaque in its right half.
Generate a standalone symmetrical wide rectangular dark-fantasy PIXEL ART frame that can be sliced into top cap, bottom cap, left/right vertical rail, and separate dark interior. Nothing else in the image.
Canvas landscape approximately 2:1. Frame occupies centered 94% canvas width, 82% canvas height. Straight horizontal top/bottom and straight uninterrupted vertical left/right sides; tiny 45-degree clipped corners. Exact bilateral symmetry. The vertical rails must stay perfectly straight from top corner to bottom corner with NO side wings or middle ornament. One small engraved crimson diamond at top center and one at bottom center, modest corner rivets. A slim layered metal profile: dark oxblood edge, muted crimson metallic highlight, very fine aged champagne-brass inset line. The reference has bright red shapes, make these more restrained and antique without losing contrast against black. Sharply defined intentional game pixel clusters; no painterly noise, no blur, no glow.
Interior is an opaque near-black burgundy fill, very subtle minimal texture, nearly flat, EMPTY. All surrounding pixels outside the frame are actual transparent alpha. No checker pattern painted into image. No floor, no shadow, no background color field. The top and bottom ornaments must fit fully inside the canvas with breathing room. NO letters, symbols other than tiny decorative diamonds, numbers, trophy, hourglass, text, labels, UI mockup, characters, or extra sprites.
Practical intended game display width about 510 units, border about 5 units; restrained decoration appropriate to small text. The center rectangular section will be lengthened in UI, so straight clean sides and simple interior are essential.
```
