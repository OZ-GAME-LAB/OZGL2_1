# 특성 설명/버튼 아트 v2

기존 공용 Button_Frame 및 중앙 프레임 원본을 덮어쓰지 않는 특성 화면 전용 리소스다.

| 파일 | 픽셀 크기 | 사용 UI 크기 |
| --- | --- | --- |
| Button_Footer_310x75.png | 620 × 150 | 초기화/중앙으로 310 × 75 |
| Button_Dialog_244x72.png | 488 × 144 | 취소/확정 244 × 72 |
| Button_Back_268x85.png | 536 × 170 | 뒤로 268 × 85 |
| Button_Step_142x72.png | 284 × 144 | 레벨 +/- 142 × 72 |
| Divider_CurrentNext.png | 640 × 40 | 현재/다음 구분선 320 × 20 |
| Frame_DemonKing_Symmetric.png | 256 × 256 | 중앙 마왕 프레임 210 × 210 |
| Background_DemonKing_Black.png | 256 × 256 | 중앙 마왕 검은 배경 210 × 210 |

- 생성: `imagegen` 스킬의 **built-in image_gen** 모드, 파일별 1회 생성. CLI/API 모드는 사용하지 않았다.
- 전체 프롬프트/참고 이미지: `Tools/Art/BuildTraitDetailAssets.inputs.json`.
- 생성 원본 보존: `Tools/Art/Sources/TraitDetailRefresh/`.
- 재현 후처리: `Tools/Art/BuildTraitDetailAssets.cjs` (Node.js + sharp).
- 사용자 승인 범위인 외부 배경 제거, 비율 맞춤, 마왕 프레임 대칭 보정만 후처리했다. 긴 버튼은 빈 중앙부와 직선 레일 일부를 잘라 연결하고 **등비율**로 축소했다. 모서리를 가로/세로로 찌그러뜨리지 않았다.
- 마왕 프레임은 두 축으로 반사해 좌우/상하 RGBA 차이 0을 검사했다. 별도 마왕 내부 아이콘은 변경하지 않았다.
- Unity 연결: `Image.Type.Simple`, `Preserve Aspect=true`. 각 버튼 Rect 비율과 PNG 캔버스 비율이 같다. Sprite Single, Full Rect, PPU 100, Point, Clamp, Mipmap 없음, 비압축.
- 적용 메뉴: `Tools/OZGL2/Lobby/Apply Trait Detail Art And Effect Comparison`. TraitsScreen 한 Undo 그룹, 대상 Prefab만 저장한다. 새 텍스처 Import 설정은 별도 에셋 설정이며 해당 Undo 그룹에 포함되지 않는다.
- 상세 픽셀/배경/크기 검증: `Validation.json`.

## 데몬킹 불투명 배경

- 프레임의 외곽 좌우 경계 사이를 검은색/알파 255로 채운 **별도 단색 마스크**다. 중앙뿐 아니라 이중 레일 사이까지 가리고 외부는 알파 0이다. 프레임 PNG 자체는 수정하지 않는다.
- 이 추가 단색 스프라이트에는 이미지 생성 모델을 사용하지 않는다. 재현 메뉴는 `Tools/OZGL2/Lobby/Apply Demon King Black Backing` (`LobbyTraitTreeBuilder.cs`).
- 프리팹의 `Nodes` 안 순서는 `DemonKingBackground` → `DemonKingFrame` → `DemonKingIcon`. Nodes보다 먼저 그려지는 Connections/MagicCircle만 가린다.
- 배경 Image: `Simple`, `Preserve Aspect`, `Raycast Target=false`, 프레임과 같은 Rect/크기. 기존 프레임·아이콘·연결선은 그대로다.
- 마스크 검증: 256×256, 불투명 검정 28,100px, 중앙 RGBA=(0,0,0,255), 모서리 RGBA=(0,0,0,0), 좌우/상하 RGBA 차이 0.
- Prefab 배경 추가/순서 변경은 한 Undo 그룹이다. 새 PNG와 Import 설정은 자산 생성이므로 Undo 대상이 아니다. 메인 씬은 저장하지 않는다.
