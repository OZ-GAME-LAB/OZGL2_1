# WavePreview 펼침 UI

## 적용 범위

- 대상 씬: `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`
- 부모 프리팹: `Assets/02.Prefabs/UI/UI_Panel/Canvas_GetReady.prefab`
- HUD 프리팹: `Assets/06.UI/BattleMutedPreview/Prefabs/BattleHud.prefab`
- 기존 `WavePreviewPanel.prefab`의 유닛 아이콘/문구/프레임은 변경하지 않는다.
- 실제 웨이브 생성, 전투 데이터, 저장 데이터와는 연결하지 않는 UI 펼침 기능이다.

## 오브젝트와 설정

```text
UI_BattleScreens/Canvas_GetReady (UIWavePreviewDisclosure)
├─ BattleHUD
│  ├─ WaveText (Text + Button)
│  └─ WaveToggleButton (Image + Button)
│     └─ Arrow (Image)
└─ WavePreviewViewport (RectMask2D)
   └─ WavePreview (기존 중첩 프리팹 + CanvasGroup)
```

- `Canvas_GetReady > UIWavePreviewDisclosure`에서 `Start Expanded`로 시작 상태를 선택한다. 기본은 열림이다.
- `Duration`: 전체 이동 시간(기본 0.25초). `Ease`: 이동 곡선(기본 OutCubic).
- 마스크 위치/크기는 `WavePreviewViewport`에서 조절한다. 현재 좌상단 `(46, -122)`, `635×183`이다.
- HUD 하단은 y=-111이며, 마스크는 그 아래에 독립적으로 존재한다. 투명한 바의 배경으로 가리는 대신 실제 클리핑으로 비침을 막는다.
- 내부 `WavePreview`는 좌상단 anchor/pivot, 열림 위치 `(0,0)`을 사용한다. 런타임에 Y만 패널 높이만큼 이동한다. 임의로 패널의 anchor/pivot/위치를 바꾸지 않는다.
- 버튼 클릭 영역은 문구 288×60, 화살표 버튼 44×44로 분리한다. 기존 문구의 폰트와 크기는 유지한다.
- 새 화살표 Sprite: `Assets/06.UI/BattleMutedPreview/WaveDisclosure_v1/Icon_WaveChevron.png`.
- 테두리는 기존 `Frame_DiamondNeutral.png`를 재사용하며, 내부 화살표만 회전한다.

## 동작과 API

`UIWavePreviewDisclosure`가 버튼 두 개에 런타임 리스너를 등록한다. Inspector의 OnClick에 같은 Toggle을 중복으로 추가하지 않는다.

```csharp
disclosure.Toggle();
disclosure.SetExpanded(true);        // 애니메이션으로 열기
disclosure.SetExpanded(false, false); // 즉시 접기
bool expanded = disclosure.IsExpanded; // 현재 목표 상태
bool moving = disclosure.IsTransitioning;
```

- DOTween은 unscaled time을 사용한다. Time.timeScale=0에서도 열린다.
- 연속 클릭 시 기존 Tween을 중단하고 현재 위치부터 반대로 움직인다.
- 비활성화 시 Tween과 버튼 리스너를 정리하며, 재활성화하면 마지막 목표 상태를 유지한다.
- 접힌 패널은 alpha=0, blocksRaycasts=false이다. 이동 중에도 패널 자체 입력을 막는다.
- 상위 팝업의 CanvasGroup을 존중한다(`ignoreParentGroups=false`). 팝업이 열린 동안 **사용자 버튼 클릭**을 차단한다. 외부 `SetExpanded` 호출은 별도 UI 제어용이므로 의도적으로 가능하다.
- `UIBattleMutedPreviewView`의 기존 웨이브/적 이름/수량 Text 직접 참조는 유지한다.

## Editor 도구와 복구

- 신규 설치 메뉴: `Tools/OZGL2/Battle/Install Wave Preview Disclosure`.
- 이미 설치한 상태에서는 재설치를 거부한다. 디자인 변경은 Prefab/Inspector에서 한다.
- Runtime과 Editor 코드는 분리되어 있다. 설치 도구는 기존 버튼 이벤트와 표시 참조를 보존하고 저장한다.
- 씬 참조 재연결은 Undo를 지원한다. 저장된 Prefab 파일은 Undo 대상이 아니며 설치 전 백업으로 복구한다.
- 이번 백업: `Tools/Art/Backups/WaveDisclosure_20260928_124806/`.
- `BattleMutedPreviewBuilder`와 `BattleMutedReferenceLayout`은 새 마스크 경로도 조회하도록 보완했다. 후자는 새 버튼이 있으면 문구의 폭과 raycast 설정도 유지한다. 다만 이 구형 전체 배치 메뉴는 기존 디자인까지 재적용하므로 단순 펼침 설정 변경 목적으로 실행하지 않는다.

## 검증 (2026-09-28)

- Unity 컴파일 오류/경고 없음, 새 컴포넌트 필수 참조 6개 연결 및 Missing Script 0개 확인.
- Play Mode에서 EventSystem Raycast로 두 클릭 영역을 확인하고 각각 PointerClick으로 닫기/열기 확인.
- 마스크 이동 중간 상태 캡처에서 HUD 뒤 비침 없음 확인.
- timeScale=0 동작, 빠른 반전의 위치 연속성/완료, 비활성화 중단, 재활성화 단일 리스너, 실제 설정 팝업 중 두 버튼 차단 확인.
- 검증 중에만 runInBackground를 임시 활성화하고 원래 값으로 복구했다. ProjectSettings 변경 없음.
- 물리 마우스 조작, 다른 해상도, 빌드 실행은 미확인.
- LayerMask/Tag/Collider/Rigidbody/Animator/Input Action 변경 및 추가 연결 없음.
- 캡처: `Tools/Art/Previews/WaveDisclosure_Open_20260928.png`, `WaveDisclosure_Closed_20260928.png`, `WaveDisclosure_MaskedHalf_20260928.png`.

## 아트 생성 기록

- built-in image_gen으로 투명 도트 금속 화살표를 생성했다.
- 원본과 최종 프롬프트: `Tools/Art/Sources/WaveDisclosure_v1/`.
- 승인된 알파 여백 크롭·중심 정렬·NearestNeighbor 크기 조정만 적용했다.
- 후처리 도구: `Tools/Art/PrepareWaveChevron.ps1`.

## Git 검토

- 충돌 주의: `Canvas_GetReady.prefab`, `BattleHud.prefab`, `UI_Battle_MutedPreview.unity`.
- Scene/Prefab은 Unity Editor API로만 저장한다. `.meta`는 Unity가 생성하며 직접 수정하지 않는다.
- 다른 UI/프리팹의 기존 미커밋 변경은 이 작업과 별도로 보존한다.
- 이번 변경은 부모/HUD 프리팹을 통해 씬에 반영되었다. 씬 저장 후 파일 SHA-256은 설치 직전 백업과 동일하여 이번 작업으로 추가된 `.unity` 차이는 없다.
- 새 Runtime 1개, 설치 Editor 1개, 기존 Editor 경로 호환 2개, 새 Sprite/원본/문서를 검토한다.
