# Battle Flat Reference v1

## 목적

- 선택된 `04_LevelBar_ChevronStepChain.png`의 전투 HUD를 Unity용 개별 Sprite로 분리한다.
- 기존 UI의 텍스처·금속 얼룩·벨벳 문양·과도한 꼭짓점 장식을 제거한다.
- Runtime 데이터와 Button 이벤트는 그대로 유지하고 시각 리소스만 교체할 수 있게 한다.

## 생성 방식

- OpenAI 내장 `image_gen` 사용
- `SourceKit.png`: 투명 배경의 UI 리소스 보드
- `Tools/Art/PrepareBattleFlatReferenceV1.ps1`: 영역 분리, 정사각 여백 정리, 팔레트 평탄화, 고립된 점 노이즈 제거 및 공통 장식 출력

## 기준 팔레트

- Charcoal: `#181819`
- Dark Outline: `#080809`
- Warm Ivory: `#E8D9C4`
- Muted Burgundy: `#7A2E2E`

## 최종 프롬프트 요약

선택된 전투 HUD 레퍼런스를 기준으로 투명 UI 리소스 보드를 내장 image_gen으로 생성했다. 각 프레임은 검은 배경과 아이보리 테두리를 한 장으로 붙이고, Currency/Reroll/Start Combat은 버건디 액션 프레임으로 구분한다. 웨이브 창은 단순한 모서리 잘림, 시너지 정보창은 우측 마름모 없는 직사각형, 레벨 바는 7단 셰브런으로 요청했다. 상단 검·모래시계·메뉴는 간결한 흰색 형태로 요청했고, 텍스트·숫자·금속 질감·노이즈·스크래치·리벳·문양·글로우를 금지했다. 생성 보드는 분리 후 단색 팔레트로 평탄화하여 남은 그라데이션과 작은 잡티를 제거했다. 하단 화염·리롤·전투 시작 아이콘은 생성 보드에서 채택하지 않고 기존 Sprite를 그대로 유지한다.

## 입력 레퍼런스

- `Assets/98.ExternalAssets/00.LocalStaging/게임랩2기1팀_사용한 리소스 팩/02_생성한 레퍼런스 이미지/2026-10-01_InGameLevelBar_4Variants/04_LevelBar_ChevronStepChain.png`
- 로비 마름모 메뉴 레퍼런스: `exec-e1b621cc-4252-4c72-94a8-f38e973fe969.png`

## 2026-10-02 실제 적용 및 검증

### 변경 대상

- `Assets/00.Scenes/Builds/InGame.unity`: 하단 프레임, 내부 아이콘/숫자/제목 정렬, 리롤 가격 줄바꿈 방지
- `Assets/02.Prefabs/UI/UI_Panel/Canvas_GetReady.prefab`: 상단 HUD/메뉴 중심선, 웨이브 마스크, 시너지 열 위치
- `Assets/06.UI/BattleMutedPreview/Prefabs/BattleHud.prefab`: 통합 정보 바, 정사각 끝 장식, 간결한 상단 아이콘, 레벨 바 안쪽 여백
- `Assets/06.UI/BattleMutedPreview/Prefabs/WavePreviewPanel.prefab`: 제목 좌우 장식, 모서리 장식, 가로/세로 구분선 복구
- `Assets/06.UI/BattleMutedPreview/Prefabs/SynergyTracker.prefab`: 흰 아이콘/색 테두리 마름모 유지, 우측 작은 마름모 제거, 검은 통합 정보창
- `Assets/Editor/BattleFlatReferenceV1Applier.cs`: 위 리소스/배치 재적용 및 원본 아이콘/버튼 불변 조건 검사. 새 Unity C# 파일/런타임 컴포넌트는 생성하지 않았다.
- `Assets/_Project/Scripts/Grid/Prototype/GridPrototypeRunner.cs`: 안내문의 시각 위치만 110px 내려 정보 바와 겹치지 않게 처리. 보드 레이아웃/게임 데이터/입력 계산은 변경하지 않았다.
- `Tools/Art/PrepareBattleFlatReferenceV1.ps1`, `SourceKit.png`, `FlatReference_v1/Sprites/*.png`: 생성 보드 및 Unity용 출력. Sprite Import 설정과 .meta는 Unity API로 처리했다.

### 적용 기준

- 1920×1080 기준 HUD `1695×82`, 메뉴 `112×112`, 웨이브 `640×180`, 시너지 행 `312×124`
- Currency/Reroll 액션 마름모는 각각 `270×270` / `180×180`. 화염/리롤/전투 시작 원본 Sprite·색상·Importer 유지
- 테두리+배경은 통합 PNG. 장식/구분선/상단 아이콘만 별도 PNG
- 레벨 Track `570×36`, Filled/Horizontal 게이지 `562×28`. 안쪽 여백으로 외곽선 덮임을 방지하며 별도 테두리 Sprite는 사용하지 않음
- 시너지 왼쪽 색 마름모 및 기존 흰색 아이콘은 유지. 오른쪽 끝 작은 마름모 없음
- 기존 Button OnClick 대상/메서드/인자/호출 상태, targetGraphic/interactable/transition/raycastTarget 전후 비교 통과

### 검증 결과

- Unity 재컴파일 및 적용 도구 실행 완료, 필수 Sprite/Prefab 연결 검증 통과
- 최종 Play Mode Console 기록 0개(오류/경고/예외 없음)
- 웨이브 버튼 클릭: 펼침 → 접힘 → 펼침 복원 확인
- 메뉴 버튼 클릭: 메뉴 열림 및 Resume으로 닫힘 확인
- 레벨 게이지 0%/60%/100% 렌더링과 외곽선 보존 확인. 게이지 수치만 Play Mode에서 임시 변경했으며 실제 경험치/레벨 데이터나 Scene 기본값에 저장하지 않음
- QA 캡처: `Tools/Art/QA/FlatReferenceV1/InGame_FlatReferenceV1_Verified0.png`, `Tools/Art/QA/FlatReferenceV1/InGame_FlatReferenceV1_Verified60.png`, `Tools/Art/QA/FlatReferenceV1/InGame_FlatReferenceV1_Verified100.png`
- 리롤의 실제 재화 소모, 전투 시작/라운드 종료, 경험치 획득 및 여러 해상도 플레이는 미확인. 팀 Play Mode 확인 필요
- 기존 원본 외부 에셋, ProjectSettings, Packages 변경 없음. 기존 .meta GUID 유지

### Inspector / Git 검토

- InGame의 Currency/Reroll/StartCombat 프레임과 원본 아이콘 연결 확인
- Canvas_GetReady의 중첩 Prefab, 웨이브 마스크/CanvasGroup, 메뉴 클릭 대상 및 시너지 아이콘 연결 확인
- Scene/공용 UI Prefab은 팀 충돌 위험 파일. Unity가 저장한 배치/장식 변경을 검토할 것
- LayerMask/Tag/Collider/Rigidbody/Animator/Input System 연결 변경 없음
- 메뉴: `Tools/OZGL2/Battle/Apply Flat Reference V1`, 검증: `Tools/OZGL2/Battle/Validate Flat Reference V1`
- Scene 하단 변경은 Unity Undo 그룹 지원. 공용 Prefab 저장은 별도 에셋 작업이므로 Git Diff를 함께 검토할 것
