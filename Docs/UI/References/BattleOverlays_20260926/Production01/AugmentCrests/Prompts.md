# 증강 문장 프레임 생성 프롬프트

- 기준 이미지: `../../Revision06/Rarity_PlatinumRefined.png`
- 생성 방식: built-in `image_gen` (각 스프라이트별 개별 호출)
- 원본 보존: `Raw`에는 생성 결과를 변경 없이 복사합니다.
- 공통 요구: 투명 외부 및 중앙 아이콘 영역, 불투명한 빈 등급 이름판, 좌우 대칭 및 동일 배치 기준.

## 생성 원본 및 검증

- `Raw/Crest_Silver.png`: 원본 `C:/Users/sudea/.codex/generated_images/01a0dc2a-89dd-72e0-87ab-9e1a10f5e766/exec-4732bdbd-b997-4ca9-b2e1-ce0bd655a601.png`
- `Raw/Crest_Gold.png`: 원본 `C:/Users/sudea/.codex/generated_images/01a0dc2a-89dd-72e0-87ab-9e1a10f5e766/exec-05ae3bbb-17e3-452e-a71b-f938fe7beb1f.png`
- `Raw/Crest_Platinum.png`: 원본 `C:/Users/sudea/.codex/generated_images/01a0dc2a-89dd-72e0-87ab-9e1a10f5e766/exec-c04278a6-c598-4983-a937-6a7d40fac591.png`
- 각 결과는 1254×1254 RGB PNG이며, 투명 배경 요청과 달리 체크무늬가 이미지에 포함되었습니다. `Raw`는 수정하지 않았습니다.
- 중앙 검과 모든 글자는 제거되었고 빈 이름판의 어두운 불투명 바탕은 유지되었습니다.
- 후속 정리 완료: `CleanupCrests.ps1`로 실제 alpha 복구 및 공통 아이콘 중심·수평 지름 정규화. 통합 이름판과 모든 장식의 원본 비율은 보존했습니다. 실제 수치와 확인 결과는 `Normalization.md` 참조.

## Crest_Silver

```text
Use case: precise-object-edit
Asset type: one production Unity 2D game UI crest frame sprite, not a card and not a complete UI.
Input images: Image 1 is the exact approved visual reference, a three-card rarity comparison. Use ONLY the requested rarity's diamond emblem and its integrated grade-name plaque.
Primary request: isolate and faithfully reconstruct only the specified EMPTY diamond emblem frame with its blank grade-name plaque immediately beneath it. Delete the white sword icon completely, delete all letters completely, and delete all card/banner/body/scene/background elements. Preserve the approved emblem silhouette, restrained ornament vocabulary, muted gothic pixel-art treatment, dark bevels and small metallic highlights. Do not redesign it.
Scene/backdrop: genuinely transparent RGBA outside the frame AND throughout the central diamond opening. No simulated transparency or painted checkerboard. The name plaque's dark body remains fully opaque for later TextMeshPro lettering.
Composition/framing: one upright front-facing sprite on a square 1024 x 1024 transparent canvas. Common image pivot is canvas center. The main diamond is centered around (512,430) with outer corners approximately (512,138), (804,430), (512,722), (220,430). The empty icon opening inside this rim must stay centered with the same insertion area across all rarity tiers. Keep the blank horizontal grade-name plaque centered beneath the diamond, approximately x195..829 and y745..860, same size and placement in all tiers. These are composition targets. Ornament may expand outside the rim within this common reserved square; keep clear transparent padding and don't crop any ornament.
Style/medium: match the reference's crisp angular gothic pixel game UI, restrained pixel-stepped edges and compact bevel highlights, subdued worn metal. Bilateral symmetry, consistent line weights. No smoothing into glossy 3D jewelry.
Text: absolutely no text, lettering, numbers, symbols that resemble writing, or watermark.
Constraints: a single crest only; empty transparent diamond interior; opaque dark blank name plaque; alpha-transparent exterior; no swords; no background game scene; no rectangular card body; no ribbons or hanging cloth; no tassels; no aura, glow, fog, shadow backdrop, giant additions, or decorative flourish redesign.
Requested rarity: SILVER, the LEFT reference crest. Plain restrained silver diamond frame with dark grey bevels and small cold metallic accents at the diamond points. Keep this tier simple: no laurels, no wings, no crown, no tassels. Its integrated blank name plaque has a dark opaque center, muted silver border, and the small angular side ornaments shown in the reference.
```

## Crest_Gold

```text
Use case: precise-object-edit
Asset type: one production Unity 2D game UI crest frame sprite, not a card and not a complete UI.
Input images: Image 1 is the exact approved visual reference, a three-card rarity comparison. Use ONLY the requested rarity's diamond emblem and its integrated grade-name plaque.
Primary request: isolate and faithfully reconstruct only the specified EMPTY diamond emblem frame with its blank grade-name plaque immediately beneath it. Delete the white sword icon completely, delete all letters completely, and delete all card/banner/body/scene/background elements. Preserve the approved emblem silhouette, restrained ornament vocabulary, muted gothic pixel-art treatment, dark bevels and small metallic highlights. Do not redesign it.
Scene/backdrop: genuinely transparent RGBA outside the frame AND throughout the central diamond opening. No simulated transparency or painted checkerboard. The name plaque's dark body remains fully opaque for later TextMeshPro lettering.
Composition/framing: one upright front-facing sprite on a square 1024 x 1024 transparent canvas. Common image pivot is canvas center. The main diamond is centered around (512,430) with outer corners approximately (512,138), (804,430), (512,722), (220,430). The empty icon opening inside this rim must stay centered with the same insertion area across all rarity tiers. Keep the blank horizontal grade-name plaque centered beneath the diamond, approximately x195..829 and y745..860, same size and placement in all tiers. These are composition targets. Ornament may expand outside the rim within this common reserved square; keep clear transparent padding and don't crop any ornament.
Style/medium: match the reference's crisp angular gothic pixel game UI, restrained pixel-stepped edges and compact bevel highlights, subdued worn metal. Bilateral symmetry, consistent line weights. No smoothing into glossy 3D jewelry.
Text: absolutely no text, lettering, numbers, symbols that resemble writing, or watermark.
Constraints: a single crest only; empty transparent diamond interior; opaque dark blank name plaque; alpha-transparent exterior; no swords; no background game scene; no rectangular card body; no ribbons or hanging cloth; no tassels; no aura, glow, fog, shadow backdrop, giant additions, or decorative flourish redesign.
Requested rarity: GOLD, the MIDDLE reference crest. Antique gold diamond rim and dark bevels, with the existing small symmetric laurel leaves rising along the lower left and lower right sides as in the reference. Small restrained diamond point accents only. No crown, no wings, no tassels. Its integrated blank name plaque has a dark opaque center, antique gold border, and the small angular side ornaments shown in the reference.
```

## Crest_Platinum

```text
Use case: precise-object-edit
Asset type: one production Unity 2D game UI crest frame sprite, not a card and not a complete UI.
Input images: Image 1 is the exact approved visual reference, a three-card rarity comparison. Use ONLY the requested rarity's diamond emblem and its integrated grade-name plaque.
Primary request: isolate and faithfully reconstruct only the specified EMPTY diamond emblem frame with its blank grade-name plaque immediately beneath it. Delete the white sword icon completely, delete all letters completely, and delete all card/banner/body/scene/background elements. Preserve the approved emblem silhouette, restrained ornament vocabulary, muted gothic pixel-art treatment, dark bevels and small metallic highlights. Do not redesign it.
Scene/backdrop: genuinely transparent RGBA outside the frame AND throughout the central diamond opening. No simulated transparency or painted checkerboard. The name plaque's dark body remains fully opaque for later TextMeshPro lettering.
Composition/framing: one upright front-facing sprite on a square 1024 x 1024 transparent canvas. Common image pivot is canvas center. The main diamond is centered around (512,430) with outer corners approximately (512,138), (804,430), (512,722), (220,430). The empty icon opening inside this rim must stay centered with the same insertion area across all rarity tiers. Keep the blank horizontal grade-name plaque centered beneath the diamond, approximately x195..829 and y745..860, same size and placement in all tiers. These are composition targets. Ornament may expand outside the rim within this common reserved square; keep clear transparent padding and don't crop any ornament.
Style/medium: match the reference's crisp angular gothic pixel game UI, restrained pixel-stepped edges and compact bevel highlights, subdued worn metal. Bilateral symmetry, consistent line weights. No smoothing into glossy 3D jewelry.
Text: absolutely no text, lettering, numbers, symbols that resemble writing, or watermark.
Constraints: a single crest only; empty transparent diamond interior; opaque dark blank name plaque; alpha-transparent exterior; no swords; no background game scene; no rectangular card body; no ribbons or hanging cloth; no tassels; no aura, glow, fog, shadow backdrop, giant additions, or decorative flourish redesign.
Requested rarity: PLATINUM, the RIGHT reference crest. Pearl platinum diamond rim with dark bevels, the existing SMALL crown directly on top, the existing angular metallic lower wings at left and right, tiny cold pearl/cyan gem points, and a restrained fine inner edge. Preserve the modest crown scale and compact wings of the approved reference; do not enlarge them or introduce large gems or new ornament. Its integrated blank name plaque has a dark opaque center, pearl platinum border, and the small angular side ornaments shown in the reference. No laurels, no tassels, no glow.
```
