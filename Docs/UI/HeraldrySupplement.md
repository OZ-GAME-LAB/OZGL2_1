# 보조 UI Heraldry 테마

## 구성

2026-10-13, `dev_2`에서 분기한 `feat/UI_redesign/7` 작업이다. 보관함 초과 정리, 스킬 설명, 증강 설명, 업적 알림, 스토리 대화, 자동 튜토리얼, 피해·회복 숫자를 기존 검정·금색·버건디 UI에 맞춘다.

각 화면의 기존 표시 코드가 `UIHeraldrySupplement`를 통해 하나의 `UIHeraldrySupplementStyleSO`를 읽는다. 설정은 이미지·폰트·배경색·테두리 배율만 보관한다. 선택, 보상, 업적 기록, 대사 진행, 튜토리얼 조건, 전투 수치는 각 기존 코드에 남긴다.

보관함·설명창·스토리는 배경과 9분할 프레임을 분리한다. 업적·해금 알림과 튜토리얼은 모서리 밖에 사각 배경이 드러나지 않도록 배경까지 포함한 일체형 Sprite를 쓴다. 가로·세로가 늘어날 때 빈 직선 구간만 늘어나고 모서리·문장·말풍선 꼬리는 비율을 유지한다. 글자는 이미지에 굽지 않는다.

## 에셋 위치

- 아트 루트: `Assets/06.UI/SharedUI/Heraldry_Supplement_v1`
- 원본 PNG: `Textures/panel_frame.png`, `header_crest.png`, `speaker_plate.png`, `achievement_medallion.png`, `bubble_tail.png`
- 마감 개선 PNG: `panel_complete.png`, `dialogue_complete_left.png`, `result_panel_clean.png`, `result_track_clean.png`, `result_fill_clean.png` (같은 `Textures` 폴더)
- 웨이브 클리어 왕관: `Textures/wave_clear_crown.png` — 같은 화풍의 부드러운 금색·버건디 왕관. 생성 프롬프트는 `Docs/UI/WaveClearCrownArtPrompt.json`에 보관한다.
- Unity Sprite: `Sprites/*.asset`
- 런타임 설정: `Resources/UI/HeraldrySupplementStyle.asset`
- 생성 프롬프트와 출처: `Docs/UI/HeraldrySupplementArtPrompts.json` — 기본 `image_gen`, 투명 배경 생성. 원본 픽셀 후처리 없이 복사했다.
- 마감 개선 프롬프트: `Docs/UI/HeraldryPolishArtPrompts.json` — 기본 `image_gen`으로 기존 그림을 참조해 생성한 별도 PNG. 기존 결과창·로비 공용 원본은 유지한다.

제목은 기존 `HeirofLightBold_SDF`, 본문은 `NEXON_Lv2_Gothic_SDF`, 전투 숫자는 `Paperlogy-9Black SDF`를 참조한다. 확정·취소 버튼은 기존 SharedMenu의 dark/red Sprite를 재사용한다. 폰트나 기존 아트 원본을 복제하거나 수정하지 않는다.

## 화면별 적용

| 화면 | 표시 구성 | 유지하는 기능 |
|---|---|---|
| 보관함 정리 | 금색 프레임·문장, 4열 카드, 선택 강조, 넘치는 후보 스크롤 | 후보·필요 선택 수·확정·취소 |
| 스킬 설명 | 금색 프레임·문장, 제목/메타/본문의 동적 높이 | 실제 피해·쿨타임 표기, 호버·조준 숨김 |
| 증강 설명 | 공통 프레임, 제목·등급·본문 분리 | 보유 목록·개수·접기·등급 |
| 업적 알림 | 배경 포함 금색 패널·메달·좌측 알림 배치 | 알림 큐·기록·기존 효과음 |
| 스토리 | 넓은 프레임, 버건디 화자 이름판, 문장 | 이미지·캐릭터·타이핑·넘김·건너뛰기 |
| 튜토리얼 | 꼬리 포함 일체형 말풍선·화자 이름판·문장 | 목표 추적·자동 도킹·입력 게이트·단계 |
| 전투 숫자 | 기존 SDF와 공유 외곽선 재질 | 피해 합산·색 의미·강조 기준·수명·풀링 |
| 새 마왕군 해금 | 일체형 패널·메달·실제 1성 초상·유닛 이름 | 해금 판정·기록·등장/유지/퇴장 시간 |
| 웨이브 시작/클리어 | 밝은 글자 면·얇은 외곽선·Bold 한국어 문구 | 웨이브 진행·기존 배너 연출 |
| 최종 결과 | 전용 패널/경험치 바 Sprite·정상 숫자 여백 | 집계·경험치·승리/패배·로비 이동 |

숫자 폰트는 `FontUnifyIgnore`로 다른 UI 글꼴 통일 대상에서 제외한다. 숫자 SDF 설정이 없거나 필수 숫자·부호가 누락되면 기존 TextMesh 표시로 돌아간다. 공통 스타일을 찾지 못하더라도 각 기존 UI의 단순 프레임 표시 경로를 유지한다.

새 테마의 튜토리얼은 하단 HUD 위에 최대 250px, 상단 HUD 아래에 최대 130px의 여백을 확보한다(1080p 기준). 자동 도킹의 겹침 판정과 실제 배치에 같은 여백을 적용한다. 화면 높이·문구 길이에 따라 가용 공간까지만 이동한다.

## 편집과 재생성

`Tools > OZGL2 > Shared UI > Build Heraldry Supplement Art`는 PNG 11장을 임포트하고 Sprite와 설정 SO를 연결한다. 기존 Sprite의 GUID와 설정의 배경색·프레임 배율을 보존한다. 기존 Sprite/SO 값 갱신은 Undo를 지원하며, 새 파일·폴더 생성과 Texture Import 설정 변경은 Undo 대상이 아니다. 지정된 새 Sprite/SO만 개별 저장하며 Scene/Prefab과 무관한 폰트 캐시는 저장하지 않는다.

`Tools > OZGL2 > Shared UI > Build Wave Clear Crown`은 왕관만 다시 임포트하고 `Assets/Resources/BattleBanner/BattleBannerArt.asset`의 `crown`에 연결한다. 기존 배너 재생 코드를 그대로 사용하며 원본 도트 왕관과 Scene/Prefab은 수정하지 않는다. 기존 Sprite와 배너 SO 참조 갱신은 Undo를 지원한다.

Inspector에서 스타일 SO의 이미지·폰트 참조, `Background Color`, `Frame Multiplier`를 조절한다. 프레임 기본 배율은 4이다. 이름판 Sprite의 9분할 경계는 큰 원본 모서리가 늘어나지 않도록 160/150/160/150으로 잡았다. PNG의 알파 경계를 분석해 Sprite Rect만 정하며 원본 PNG를 자르지 않는다.

새 Scene 연결이나 기존 Prefab override는 필요하지 않다. 모든 새 `.meta`는 Unity가 생성하므로 PNG·Sprite·SO·스크립트와 함께 공유해야 한다.

## 초기 보조 UI 검증

메뉴: `Tools > OZGL2 > Shared UI > Validate Heraldry Supplement In Temporary Scene`

`HeraldrySupplementPreviewValidator`가 실제 UI 표시 메서드를 임시 Edit Mode 씬에서 호출하고 PNG와 JSON을 `Builds/HeraldrySupplement_20261013/Preview`에 만든다. 현재 Game View의 크기로 배치한 뒤 1920×1080, 1280×720 PNG를 출력한다. 출력 이미지 크기와 실제 배치를 계산한 화면 크기는 JSON에서 구분한다. 기존 게임 촬영 파일이 있으면 배경으로 사용하고, 없으면 단색으로 검증한다. 원래 씬과 Random 상태를 복원하며 Scene/Prefab을 저장하지 않는다.

2026-10-13 / Unity 6000.3.22f1 확인:

- Game View 1920×1080, 1366×768 두 실제 배치에서 각각 전체 통과. 종료 후 1920×1080으로 복원했다.
- 각 배치에서 PNG 30장 생성: 7종 대표 화면, 긴 문구, 보관함 목록·선택 상태. 스토리 실제 대사 6개 전부 넘침 없음.
- 폰트·Sprite·9분할 참조 누락, 장식의 클릭 차단, TMP 문구 넘침 각각 0건.
- 보관함 12·20개 후보의 표시 전용 데이터로 목록 클리핑·스크롤 이동·선택 전후 확정 버튼 상태·버튼과 스크롤 영역 분리 통과. 원래 게임의 보관함 용량은 변경하지 않았다.
- 전투 숫자 53/153!/+12/-24가 Paperlogy world TMP로 표시되는 것 확인. 기존 TextMesh와 새 TMP의 같은 64 크기 표본 `153!` 높이는 7.15/7.54로 약 5% 차이였다.
- 컴파일 성공, 최종 Console 오류 0개. 원래 TeamLogo 씬으로 복원했으며 미저장 변경 없음.
- Scene/Prefab/ProjectSettings/Packages/기존 폰트 파일 변경 없음. 신규 코드·아트·Sprite·설정 SO와 Unity 생성 `.meta`만 추가했다. 기존 Runtime 표시 스크립트 8개를 수정했다.

저장 데이터를 바꾸는 업적 Begin, 스토리 Finish, 튜토리얼 Advance와 보상 확정은 호출하지 않았다. 위 결과는 편집 모드의 실제 표시 메서드 검증이다. 실제 Play Mode 전투·마우스 휠/드래그·튜토리얼 목표 추적·플레이어 빌드는 미확인이다.

최종 캡처와 결과는 `Preview_1080p`, `Preview_1366x768`에 별도로 보관했다. `Builds`는 Git 제외이므로 팀 공유용 변경에는 코드·아트·설정·이 문서만 포함한다. 커밋/푸시는 별도 요청 후 진행한다.

## 사용자 피드백 마감 수정 (2026-10-13)

### 원인과 변경 책임

- `TutorialDirector` / `AchievementToast`: 투명 모서리가 있는 테두리 뒤의 단색 사각형이 돌출되었다. 업적은 `UIHeraldrySupplement.ApplyPanel`로 배경과 테두리를 한 Sprite에 적용한다. 튜토리얼은 기존 사각 배경과 분리된 꼬리를 숨기고 `UIHeraldryDialogueImage` 하나로 그린다.
- `UIHeraldryDialogueImage`: 그림의 모서리와 가운데 꼬리 구간을 보존하는 3열·5행 메시만 담당한다. 같은 일체형 그림을 좌우 UV 반전하고, 꼬리 폭을 본문 사각형 밖으로 빼서 양쪽 안쪽 여백을 유지한다. 게임 진행과 입력은 기존 Director 책임이다.
- `InGameBattleFx`: 큰 SDF 여백에 0.30~0.34 외곽선을 적용해 밝은 획 내부까지 검게 덮었다. 배너 외곽선을 0.045~0.06으로 줄이고 면 색상을 명시했다. 한국어 문구도 기존 HeirofLightBold의 전용 SDF를 사용한다. 생성한 배너 재질은 owner 종료 때 정리한다.
- `UIBattleResultView`: 결과 Prefab TMP의 `extraPadding=true`가 확보한 atlas 여백 8px을 넘어 10.25px까지 읽어 옆 글자 조각을 표시했다. 실제 같은 Prefab 전후 렌더로 재현했으며, Play Mode 활성화 때 추가 여백을 끄고 메시 여백을 갱신한다. 숫자 값과 연출은 유지한다.
- 결과창과 경험치 바: 정리한 결과 전용 Sprite를 Play Mode에 적용한다. 로비 업적과 공유하던 원본 PNG/Prefab은 유지한다. 패널은 `Simple/useSpriteMesh`, 바는 높이에 맞춘 9분할로 표시한다.
- `HeraldrySpriteSilhouette`: Editor에서 패널의 가장 큰 불투명 외곽선만 삼각분할해 Sprite 메시를 만든다. PNG 픽셀은 그대로 두고 바깥의 분리된 잔여 조각이 그려지지 않도록 한다. Unity 6000의 외곽선 API를 사용할 수 없거나 분할에 실패하면 명시적으로 중단한다. Edit Mode에서 무시되는 `OverrideGeometry` 대신 `SpriteDataAccessExtensions`로 위치·UV·인덱스를 기록한다. 저장 후 재임포트해 꼭짓점 480개·인덱스 1,434개가 유지되고 원래 Sprite 영역을 벗어나지 않는 것을 확인했다. Texture의 Read/Write는 끈 상태를 유지한다.
- `InGameCurrencyHud`: `BuildUnitUnlockBanner`에 표시 구성을 분리하고 같은 패널·메달·제목/본문 글꼴을 적용했다. 기존 즉석 렌더 초상에서 일부 파츠만 보이는 경우가 있어 실제 보상 카드 카탈로그의 완성된 1성 초상을 우선 사용한다. 해금 판정/저장/배너 시간은 유지한다.

### Inspector 및 팀 검토

- `HeraldrySupplementStyle.asset`의 기존 참조에 `Panel Sprite`, `Dialogue Sprite`, `Result Panel Sprite`, `Result Track Sprite`, `Result Fill Sprite`가 추가된다. 빌더로 연결하며 Scene/Prefab 연결 작업은 필요 없다.
- 신규 PNG·Sprite·스크립트와 Unity 생성 `.meta`를 함께 공유한다. 외부 원본 폰트·공용 원본 이미지·ProjectSettings·Packages는 변경하지 않는다.
- 변경 충돌 확인 대상: `TutorialDirector.cs`, `AchievementToast.cs`, `InGameBattleFx.cs`, `InGameCurrencyHud.cs`, `UIBattleResultView.cs`와 공통 스타일/Editor 도구. 기존 미커밋 UI 변경은 유지한다.
- LayerMask/Tag/Collider/Rigidbody/Animator/Input System 연결 변경은 없다.

### Play Mode 표시 검증

실제 InGame Game View에서 `ScreenCapture`로 촬영했다. 기존 저장 상태는 `GENERAL_REWARD`이며 실제 보상 카드 화면 위에 표시용 UI 상태를 구성했다. 튜토리얼 좌/우·짧은/긴 문구 4개, 업적/해금 2개, Wave/Clear/Boss 3개, 승리/패배 각각 집계 중간 3개와 완료 화면 8개로 총 17개다. 보상·해금·업적 완료 기록을 직접 확정하는 메서드는 호출하지 않는다.

해상도별 캡처와 글자/Sprite 검사 기록은 `Builds/UIVisualPolish_20261013/Live_1080p` 및 `Live_1366x768`에 둔다. 자연 발생 조건·실제 마우스 클릭·플레이어 빌드는 별도 플레이테스트 항목이다. 검증 전 저장 파일과 PlayerPrefs를 백업하고 종료 후 비교·복원하며 원래 TeamLogo 씬으로 돌아간다.

일체형 패널 도입 후 Edit Mode 검사 도구도 갱신했다. 30개 캡처 검사에서 폰트·Sprite 참조 누락, 9분할 오류, 장식의 클릭 차단, 글자 넘침은 모두 0건이었다. 외곽 메시 진단 중 발생한 `Not allowed to override geometry` 경고는 위 버퍼 API 적용으로 해결했으며, 기존 Console 기록은 지우지 않았다.

최종 결과: 1920×1080과 1366×768 각각 17/17 표시 검사 통과. 글리프/atlas 참조 누락, 글자 넘침 0건이며 Console Error 0개. 저장 파일 155개 SHA256과 PlayerPrefs 42개가 복원 후 일치했다. Game View는 원래 1920×1080, Scene은 미저장 변경 없는 TeamLogo로 복원했다. 주요 수정 후 화면 8장은 `Builds/UIVisualPolish_20261013/UI_Polish_After_8.zip`에 묶었다.

## 카드 보상 중 시너지 확인 (2026-10-13)

- `UICardRewardPresentation`은 카드 보상의 표시·입력만 담당한다. 기존 `InGameCardSizing`이 실행 중 생성·연결하므로 Scene/Prefab 연결은 필요 없다. 보상 후보·지급·Stage 변경은 기존 시스템이 담당한다.
- 보상이 열려 있을 때 시너지 목록은 계속 보이고, 카드·리롤·`전장 보기`만 조작할 수 있다. 투명한 전체 화면 Raycast 차단막이 카드와 리롤 영역만 통과시킨다. 키보드 선택과 직접 마우스 상태를 읽는 카메라·유닛 선택·웨이브/스킬 설명도 같은 보상 상태를 확인한다.
- `전장 보기`를 누르면 카드가 0.22초 동안 아래로 내려가며 사라진다. 이때 시너지 호버 설명과 `보상 보기`만 허용하고 카드 선택·리롤·전투 시작·배경 조작은 막는다. 다시 열어도 보상 요청과 후보는 유지된다.
- 보상 완료 또는 보관함 정리·확장 배치로 넘어가면 차단막을 해제하고 기존 입력 흐름으로 돌아간다. 카드 위치·크기·CanvasGroup과 리롤 Navigation 설정은 원래 상태로 복원한다.
- 보상 카드는 기존처럼 화면 가로 중앙에 정렬한다. 보상 중 손패 Canvas의 독립 정렬을 켜 시너지 목록보다 앞에 표시하고, 종료 시 원래 설정을 복원한다. 호버 카드는 현재 손패 정렬보다 한 단계 앞에 표시한다. 시너지와 겹쳐도 카드 선택·리롤·접기/펼치기 기능은 유지한다.
- 튜토리얼 보상 안내도 `전장 보기` 사용법으로 바꿨다. 튜토리얼 목표·완료 조건은 유지한다.

Inspector에서 `BattleBannerArt.asset`의 `crown` 참조와 신규 Sprite/PNG가 함께 들어왔는지 확인한다. LayerMask/Tag/Collider/Rigidbody/Animator/Input System 에셋 연결 변경은 없다. 충돌 검토 대상은 `InGameCardSizing`, `InGameSynergyHudBridge`, `InGameCurrencyHud`, `UIGridStorageHandAdapter`와 `BattleBannerArt.asset`이다.

### 카드 보상 최종 검증

- Unity 6000.3.22f1 실제 InGame Play Mode에서 저장된 준비 상태의 웨이브를 기존 디버그 클리어 기능으로 완료하고 정상 `GENERAL_REWARD`로 진입했다. 후보나 보상 단계를 강제 주입하지 않았다.
- 1920×1080·1366×768 각각 73개 검사 통과: 시너지 4행 표시, 카드 접기/복귀, 접힌 상태의 호버 설명, 카드/리롤 Raycast, 배경 포커스·Submit·웨이브 이동·카메라 입력 차단, 보상 요청/후보/재화 불변을 확인했다.
- 별도 실제 입력 검사 18개 통과: 접힌 상태의 리롤/카드 클릭 차단과 전투 시작 거절, 펼친 뒤 리롤 1회(160→127, 비용 33), 클릭한 유닛과 발판 각 1회 지급, 7웨이브 `PREPARATION` 복귀, 차단막 해제를 확인했다.
- 검증용 Mouse/Keyboard 이벤트는 실제 `InputSystemUIInputModule`·Raycast·Button/카드 이벤트 경로를 거쳤다. 초기 자동 입력은 Editor 포커스에 영향을 받아 누락되었으므로 검증 전용 장치와 메모리 한정 입력 설정으로 분리했다. 종료 후 장치·이벤트 구독·입력 설정을 복원했다.
- 새 왕관 Sprite/Texture 참조와 투명 배경 임포트를 확인했다. 배너 캡처는 보상 화면 위에서 기존 배너 표시 루틴만 재생한 이미지다.
- 컴파일 성공, 최종 Console Error 0개. Missing Script 5건과 손패 Viewport 경고 1건은 별도 정리 대상으로 남아 있다. 실제 로드된 Scene 오브젝트의 Missing Script는 0개였다.
- 저장 파일 157개 SHA256·PlayerPrefs 44개를 비교·복원했다. 원래 TeamLogo 씬(미저장 변경 없음), 1920×1080 Game View로 복원했다. Scene/Prefab/ProjectSettings/Packages 변경은 없다.

최종 화면/검사 기록: `Builds/RewardInspection_20261013/Final_1080p`, `Final_1366x768`, `Actions_Diagnostic`. `Builds`는 Git 제외다. 보관함 초과·확장 발판 선택·보스 특성 전환은 코드 경로 검토만 수행했고 실제 재생은 미확인이다. 플레이어 빌드 실행도 별도 확인 항목이다.

### 카드 중앙 정렬 복원 검증

- 1920×1080 실제 보상 화면에서 중심 X=960, 시너지 정렬 2 < 카드 10 < 호버 카드 11을 확인했다. 접기·복귀·시너지 설명·입력 차단 등 기존 검사 72개도 통과했다.
- 정렬 전환 직전의 호버 상태를 초기화하도록 보완했다. 보상 진입·종료 시 정렬 복원과 다시 호버했을 때의 순서를 추가 검사했다.
- 추가 검사 중 Editor 스크립트 재로딩으로 실행 상태가 초기화되어, 검증 동안만 재로딩을 잠그고 재확인했다. 최종 실행 Console Error 0개이며 잠금을 해제했다. 저장 파일 182개·PlayerPrefs 44개와 원래 TeamLogo 씬을 복원했다.
- 이번 수정은 `InGameCardSizing`, `UIBattleCardHandSlot`과 이 문서에 한정한다. Scene/Prefab/ProjectSettings/Packages 변경은 없다. 캡처와 검사 기록은 `Builds/RewardCentering_20261013`에 보관한다.
