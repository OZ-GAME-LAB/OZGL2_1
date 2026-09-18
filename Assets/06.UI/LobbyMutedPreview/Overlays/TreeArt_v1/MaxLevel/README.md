# 만렙 특성 프레임

일반/특화 각각 4계열, 총 8개의 만렙 전용 프레임 Sprite입니다.

- 기존 기본 프레임은 그대로 보존합니다. 만렙에 도달한 노드의 `Frame.Image.sprite`만 `_maxLevelFrameSprite`로 교체합니다.
- 레벨을 내리면 `_defaultFrameSprite`로 돌아갑니다. 아이콘, 이름, 클릭 영역과 배치는 변하지 않습니다.
- 고금색 외곽선은 기존 금속의 외곽에 밀착된 이미지의 일부입니다. 별도의 큰 다이아몬드/얇은 외곽선 오브젝트는 사용하지 않습니다.
- 내부는 계열별 저채도 녹색/보라/파랑/고동색으로 채워져 있고, 아이콘을 합성하지 않은 상태입니다.
- 256×256 / Sprite Single / 중앙 pivot / Point / 압축 없음 / mipmap 없음 / 외부 투명 alpha.
- `Validation.json`에 실제 알파와 중심 색상, 원본/결과의 그림 영역 검사 결과를 기록합니다.

## 생성과 재현

그림은 내장 `image_gen`으로 각 프레임을 개별 생성했습니다. 승인된 고금색/녹색 레퍼런스를 스타일 기준으로, 기존 일반/특화 프레임을 형태 기준으로 사용했습니다.

- 생성 원본/승인 레퍼런스: `Tools/Art/Sources/TraitMaxFrames/` (Unity에 불필요한 원본 텍스처를 임포트하지 않도록 Assets 바깥 보관)
- 최종 프롬프트 전체: `Tools/Art/BuildTraitMaxFrames.inputs.json`
- 승인된 후처리: `Tools/Art/BuildTraitMaxFrames.cjs` (Node.js + sharp 필요)
- 후처리는 생성 원본의 체크무늬/분리 배경 제거와 최근접 크기 정렬만 합니다. 색을 재도색하거나 기존 프레임 PNG를 덮어쓰지 않습니다.
- API/CLI로 생성하지 않았고 API 키도 사용하지 않았습니다.

## Unity 연결

`Tools/OZGL2/Lobby/Apply Max Level Trait Frame Sprites`를 실행하면 `Canvas_LobbyOverlays.prefab`의 특성 40개에 기본/만렙 Sprite를 연결하고 기존 `MaxLevelBorder` 자식을 제거합니다. Prefab Mode에서 하나의 Undo 그룹을 지원합니다. 메인 씬의 미저장 내용은 저장하거나 되돌리지 않습니다.

현재 프리팹은 적용·저장되어 있으므로 재실행하지 않아도 됩니다. 다른 계열 상징/중앙 마왕에는 만렙 프레임을 적용하지 않습니다.
