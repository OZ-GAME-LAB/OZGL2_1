# 기록 프레임 레퍼런스 일치 수정

## 최종 상단 원본 생성 프롬프트

```text
Use case: precise-object-edit / background-extraction.
The attached cropped image IS the approved final frame design, not merely a style reference. Produce a clean production sprite by preserving the depicted red frame's exact outline, geometry, proportions, muted red color palette, black outlines, small top crest, beveled shoulder angles, large lower lateral diamond ornaments and bottom center crest.
Remove all typography, trophy/hourglass icons, vertical and horizontal interior divider lines and the mouse cursor, restoring an empty near-black burgundy subtly textured interior. Remove the environment/knight fragments OUTSIDE the frame and replace exterior with actual transparent alpha. Preserve the opaque dark interior.
CRITICAL: do not redesign the frame, do not turn it into a rectangle, do not shorten the diagonal shoulders, do not shrink its lower side diamond wings. Do not brighten the red into scarlet. The top angled shoulders and bottom lateral accents must remain as drawn here. Exact left-right symmetry.
Return ONE empty frame only, fully visible, front-on, no labels, no alternate examples, no UI screenshot, no checkerboard background. Upscale crisply for use as pixel-art game UI with the SAME frame aspect ratio as input.
```

- 방식: 내장 `image_gen`, 승인된 `Docs/UI/References/LobbyDifficulty/difficulty_record_frame_hover_v2.png`를 형상 기준으로 편집 생성.
- 기존 생성 프롬프트의 `style only`, `wide rectangular`, `NO side wings`가 승인안과 다르게 사각형을 유도한 것을 바로잡는다. 붉은 모따기·좌우 마름모·상하 중앙 장식을 유지한다.
- 하단/레일 생성 원본: `Tools/Art/Sources/DifficultyRecordHover/Frame_ApprovedSilhouette_Original.png`.
- 상단 최종 원본: `Tools/Art/Sources/DifficultyRecordHover/Frame_ReferenceMatched_Original.png`. 승인 목업에서 프레임만 잘라 입력하여 대각 어깨를 복원했다.
- 도구 원본: `C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-c8749636-b863-476a-9c44-d04b8cea353a.png`.
- 원본 1922×818 RGB에는 실제 체크무늬가 있어 승인받은 배경 정리·분할·크기 정렬만 수행했다. 기존 생성 원본도 보존한다.
- 후처리: `Tools/Art/PrepareDifficultyRecordReferenceArt.ps1`. 외곽 연결 무채색만 제거하고 내부 색/모양은 코드로 그리거나 재색칠하지 않는다.
- 유효 영역 x26,y76,w1870,h628. 상단 y245 / 하단 y494에서 분할. 공통 전체 폭 512에 맞춘 캡은 512×46 / 512×57이며, 외곽 날개와 가운데 장식을 포함한다.
- 레일은 전체 폭의 가장자리가 아닌 좌우 16px 안쪽, 두께 16px. 몸체는 32px 안쪽. 510 폭으로 조립할 때 모든 값에 510/512 적용.
- 채택 아트 5종은 기존 `RecordHover/Sprites/Frame_*.png`를 교체해 기존 참조를 유지한다. 아이콘·구분선·TMP는 유지한다. Meta는 직접 편집하지 않는다.
- 최초 수정 생성본은 상하 장식이 커서 104 높이로 접을 수 없었으므로 두 번째 편집에서 장식 크기와 모따기 높이만 줄였다.
- 두 번째 결과도 상단 모따기가 짧아 최종적으로 상단을 4개로 나눴다. `Frame_TopLeftShoulder`, `Frame_TopRightShoulder`, `Frame_TopStrip`, `Frame_TopCrest`. 장식은 60/292 비율을 공통 적용하고 직선 중앙부만 가로 길이를 조정한다. 기존 `Frame_Top`은 참조를 보존한 비활성 원본이며 실제 상단은 이 4개 자식 이미지다.
- 최종 표시 높이 120→220. 하단과 난이도명/설명/버튼 위치는 유지하며 기록 영역만 16px 위로 이동한다. 상단은 60px, 하단은 약56.78px라 닫혀도 장식이 겹치지 않는다.

## 1차 수정 생성 프롬프트

```text
Use case: precise-object-edit.
Asset type: Unity dark-fantasy pixel-art UI record-panel sprite, production asset.
Input image 1 is the APPROVED design. Extract and faithfully recreate ONLY the expanded red record panel from the RIGHT "mouse over" half: the panel immediately above the battle-ready button and overlapping the knight's lower torso (approximately x1000..1508,y496..727 in the 1672x941 reference).
Do NOT design a new rectangle. Match the reference's silhouette and ornament language exactly: chunky worn crimson iron outer frame with angular/chamfered shoulders, diagonal top corner cuts, black edging and restrained red highlights; a tiny centered diamond crest at the top; distinctive large sideways diamond/chevron ornaments near the LOWER left and right edges at the lower nameplate's midline; a centered downward diamond at the bottom; fine warm-dark antique-gold inset line, primarily red not gold. Bilateral symmetry. The lower nameplate looks like the original red long hexagonal banner; the upper records region extends upward above it using narrower vertical sides and beveled top shoulders.
Remove ALL text, values, icons, cursor and divider lines inside the panel; keep an uninterrupted almost-black subtle burgundy textured interior. Do not include knight, diamond portrait border, battle-ready button, environment, comparison labels or other objects.
Composition: ONE front-facing fully visible expanded frame, centered and filling most of a landscape canvas with generous transparent outer padding. Frame overall width to height roughly 2.35:1, matching the right reference panel. Bold readable pixel clusters at the reference game scale, not smooth vector, no gold ball corner decorations, no thin rectangular replacement, no bright glow.
Background: genuinely transparent alpha outside the irregular frame silhouette, opaque dark interior. No baked checkerboard, no backdrop, no shadow outside. Preserve all the angular red side decorations. No typography or watermark.
```

## 최종 비율 보정 프롬프트

```text
Use case: precise-object-edit. Image1 is the current generated frame to correct. Image2 is the approved layout reference, exact design target, especially the right-hand expanded panel.
Keep the red angular gothic pixel-art frame, its near-black burgundy interior, lower flanking open diamonds, red black outlines and symmetric composition. Change only the proportions of its border decorations to match image2 precisely:
1) Shrink the top center red diamond crest by about 60 percent; it is a tiny crest in the reference, NOT a large badge.
2) Make the TOP cap shallow: all top decoration including bevel shoulders ends within the top 22 percent of the total frame height. The top corner diagonals are small chamfers, not huge roof-like slopes.
3) Make the BOTTOM decorative band shallower: all lower protruding side diamonds, diagonal corners and bottom center diamond fit inside the bottom 26 percent of height. Shrink these ornaments consistently, maintaining square diamonds, not squashing them. Leave the middle 52 percent as two straight thin crimson vertical rails with empty dark interior. Those rails are inset about 5 percent from the outermost lower side tips.
4) Main frame body is dark wine-red metal with muted coral highlights like the approved reference, not vivid neon red. Keep visible bold red edging, not gold rectangular edging.
5) Two faint very thin warm old-gold interior edge highlights are acceptable, but no long divider line and no icons or text.
ONE expanded frame only, width:height about 2.4:1, front facing, centered with small exterior padding. Pixel art matching the approved game reference. All irregular exterior must be genuinely transparent alpha, NOT checkerboard or gray. Opaque almost-black interior. No environment, no UI mockup, no cursor, no typography.
```
