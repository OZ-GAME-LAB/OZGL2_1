# Battle Tone Fix — 리롤 생성 기록

- 방식: 내장 `image_gen` 편집. CLI/API fallback 미사용.
- 참조: 사용자 리롤 캡처 `codex-clipboard-e1954c11-0737-49d0-abba-270c9eaac4fd.png`, 기존 `Sprites/Frame_DiamondNeutral.png`, `Sprites/Icon_Reroll.png`.
- 원본: `Frame_Reroll_Original.png`, `Icon_RerollWhite_Original.png` 보존.
- 소비 리소스: `Assets/06.UI/BattleMutedPreview/ToneFix/Frame_Reroll.png` (512×512), `Icon_RerollWhite.png` (128×128).
- 후처리: `Tools/Art/PrepareBattleToneFix.ps1`. 생성 체크 배경/주변 잔여 픽셀 제거, crop, 중심 맞춤, Point 리사이즈만 적용. 아트 색상·형상 재도색 없음.
- 패널 원본 변경 없음. 벨벳 Body/Frame의 밝기는 Unity Image.color에서 각각 0.50/0.62로 조절.

## 프레임 최종 프롬프트

```text
Use case: precise-object-edit. Asset type: Unity pixel-art UI button FRAME ONLY sprite.
Input image 1 is the tiny user-approved reroll button reference, for white/red colors and thin frame. Input image 2 is the current frame edit target, for precise shape and proportions.
Change only the frame colors and subtle red inner trim to match image 1. Keep a thin square diamond outline, centered, geometrically symmetric left/right/top/bottom, small red diamond jewels at four tips, thin chalk-white outer lines and dark crimson red inner accent lines, charcoal fine outlines. Crisp modest pixel-art clusters, restrained dark gothic game palette. NOT gold or cream. Keep frame thin, not thick or bulky, empty hollow interior. NO arrows, NO other symbol, NO text, NO number, NO pedestal. Actual transparent alpha background BOTH outside and inside the diamond. No black filled diamond. No checkerboard painted into image. Square canvas, frame fills ~90%, all tips within bounds. A single isolated frame, frontal orthographic. Do not include the reference screenshot; output only the transparent frame sprite.
```

## 화살표 최종 프롬프트

```text
Use case: precise-object-edit. Asset type: Unity reroll icon sprite.
Input image is existing icon edit target. Preserve the exact two curved opposing circular-arrow silhouettes and layout, replace cream/yellow with neutral bright white, subtle pale-neutral-gray pixel edge shades only. Crisp pixel-art, readable at 66px, no blur or glow, no ornamental changes. Center the pair symmetrically on a square canvas with even transparent margins. Actual transparent alpha background, no checkerboard, no frame, no button, no text, no extra symbols. White two-arrow reroll icon only.
```
