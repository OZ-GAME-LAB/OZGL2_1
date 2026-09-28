# 로비 스테이지 카드 레이어

## 적용 범위

- Scene: `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`
- Hierarchy: `Canvas_Lobby > StageSelection > PreviousStage / CurrentStage / NextStage_Locked`
- `Canvas_LobbyOverlays` 및 다른 Scene/Prefab은 이번 작업 대상이 아니다.
- 카드 위치·크기·캡션·기록·화살표·전투 준비 버튼과 기존 참조는 유지한다.

## 구조

세 카드 모두 아래 구조를 가지며 기존 캡션/버튼은 그대로 남아 있다.

```text
Stage card [UILobbyStageCardView, 기존 Image 비활성]
├─ VisualLayers
│  ├─ ArtworkMask [Image, Mask / Show Mask Graphic OFF]
│  │  └─ ArtworkRoot
│  │     ├─ Background [검정]
│  │     ├─ Artwork [Image, AspectRatioFitter]
│  │     └─ LockedShade [잠금 시 검정 음영]
│  ├─ Frame [Image]
│  └─ LockOverlay [CanvasGroup]
│     └─ Lock [Image]
└─ 기존 캡션·기록·버튼
```

`VisualLayers`는 첫 번째 자식으로 배치하여 기존 캡션/버튼보다 먼저 그린다. 새 Image의 Raycast Target은 모두 OFF이므로 입력을 가리지 않는다.

## Inspector에서 수정

각 카드 루트의 `UILobbyStageCardView`에서 다음 항목을 조절한다.

| 항목 | 용도 |
| --- | --- |
| Artwork Sprite | 마름모 안에 표시할 그림 |
| Frame Sprite | 독립된 테두리 PNG |
| Lock Sprite | 자물쇠+사슬 PNG |
| Is Locked | 잠금 오버레이 및 음영 표시 여부 |
| Locked Shade Color | 잠금 상태의 음영 색상/불투명도 (기본 적용 20%) |

- `표시 연결` 참조 6개는 연결된 상태를 유지한다.
- `Artwork`의 Image Sprite를 직접 바꾸지 말고 루트의 **Artwork Sprite**를 바꾼다. 루트 값이 표시의 기준이다.
- `Frame` Image의 Color는 카드별로 유지된다. 현재 카드는 붉은 프레임, 이전 카드는 중립 프레임, 잠긴 카드는 중립 프레임에 회색 틴트 0.66을 사용한다.
- `LockOverlay` RectTransform으로 잠금 문양 크기를 변경한다. 현재 카드 폭/높이의 50%이다.
- 기존 카드 루트 Image와 합성 PNG 참조는 복구 및 기존 도구 호환 목적으로 보존하되 비활성화한다. 다시 켜면 이미지가 중복 표시된다.

## 마스크와 비율

- `ArtworkMask`: 가운데 정렬, 카드의 약 56.5685% 크기인 정사각형을 45도 회전한다. 마름모의 전체 가로/세로 범위는 카드의 80%다.
- `ArtworkRoot`: -45도 역회전, 마스크 정사각형의 √2배 크기로 확장한다. 따라서 그림은 기울어지지 않으며 마름모 꼭짓점까지 채운다.
- `Artwork`: `AspectRatioFitter.EnvelopeParent`로 원본 비율을 유지하며 영역을 채운다. 가로/세로 이미지의 초과 영역은 중앙 기준으로 잘린다.
- 프레임 안쪽 검정 경사면 아래까지 그림이 약간 겹쳐 빈틈이 생기지 않게 했다. 완전히 다른 프레임을 적용하면 ArtworkMask 크기를 함께 확인한다.
- `Background`는 그림이 없을 때 흰 사각형 대신 검정 마름모가 남게 한다.
- 프레임·사슬 PNG는 실제 알파를 사용한다. 그림 PNG에는 테두리·자물쇠가 없다.

## 코드 연결과 책임

`UILobbyStageCardView`는 표시만 담당한다. 실제 스테이지 선택, 해금 판정, 저장 데이터는 외부 시스템의 책임이다.

```csharp
card.SetArtwork(stageSprite);
card.SetFrame(frameSprite);
card.SetLockSprite(lockSprite);
card.SetLocked(!isUnlocked);
```

`SetLocked`는 캡션 문구나 버튼 활성화를 변경하지 않는다. 실제 스테이지 시스템을 연결할 때 외부 제어부에서 함께 처리해야 한다. 기존 좌우 화살표는 비활성 상태를 그대로 유지했다. 전투 준비 버튼의 기존 Scene 이동 연결도 유지했다.

새 컴포넌트를 분리한 이유는 그림 교체와 잠금 표현을 한곳에서 관리하되, 진행/해금/저장 로직이 UI 표시 코드에 섞이지 않게 하기 위해서다. Runtime 코드에 UnityEditor 의존성은 없다.

## 편집 안전

- Unity MCP를 통해 Scene 오브젝트를 생성하고 저장했다. Scene/Prefab/.meta 텍스트를 직접 수정하지 않았다.
- 생성과 참조 연결은 Unity Undo 그룹 `Split lobby stage card visual layers`에 등록했다.
- 이전 `LobbyMutedPreviewBuilder`의 전체 레이아웃 재적용은 현재 수동 배치까지 초기화할 수 있으므로 이 작업의 갱신 수단으로 사용하지 않는다.
- 원본 합성 PNG와 다른 프리팹은 변경하지 않았다.

## 검증

- 새 코드 컴파일 완료, Play Mode Console Error/Warning 0개 확인.
- Play Mode 자동 점검 78개 통과: 표시 참조, 원본 Image 비활성, 그림 정방향, 레이어 입력 차단 없음, 잠금 전환/복구, null 그림 처리, 독립 그림 교체, 세로/가로 그림 비율 및 영역 채움, 전투 준비 버튼의 클릭 대상과 기존 콜백, 화살표 비활성 유지.
- Game View에서 마름모 경계·레이어·잠금 표시를 시각 확인했다.
- 기존 컴포넌트 5,665개 직렬화 값과 카드 3개의 RectTransform 기하 정보가 변경되지 않음을 저장 전 비교했다. 예외는 승인된 카드 루트 Image 비활성/새 자식 추가다.
- 전투 준비 버튼의 실제 Scene 이동은 실행하지 않았다. 전투 Scene 변경을 피하고 Raycast 도달 및 기존 콜백 연결만 확인했다.
- Collider / Rigidbody / Tag / LayerMask / Animator / Input System 설정 변경 없음.
- Play Mode 종료 후 저장된 초기 상태(이전/현재 잠금 OFF, 다음 잠금 ON), 참조 누락 0개, Scene dirty=false를 확인했다.
- 작업 직전 저장본과 비교하면 기존 YAML 문서 9개(카드 GameObject/RectTransform/Image 각 3개)만 바뀌고 102개 문서가 추가되었다. 기존 문서 삭제는 0개다. 이전 작업의 다른 변경사항은 보존했다.
- `git diff --check`에는 Unity가 저장한 빈 `m_Name: ` 등의 후행 공백 경고가 있다. Scene/Prefab 텍스트 직접 편집 금지 규칙에 따라 임의 정리하지 않았다.

## 리소스와 생성 기록

- `Assets/06.UI/LobbyMutedPreview/StageLayers_v1/Sprites/`: 새 PNG 6개 및 Unity 생성 메타.
- `Tools/Art/lobby_stage_layers_prompts.md`: built-in image_gen 생성/수정 프롬프트 전문.
