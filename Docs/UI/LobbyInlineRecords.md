# 로비 난이도 카드 내부 기록 표시

## 적용 범위

`Assets/00.Scenes/Builds/Lobby.unity`의 중앙 난이도 카드에 적용한다.
별도 기록창을 펼치는 대신 기존 Description과 기록을 교차 페이드한다.
카드 RectTransform과 Transform scale은 전환 중 변경하지 않는다.

## 편집 위치

- `Canvas_Lobby/StageSelection/CurrentStage/RecordHoverHitArea`: 카드 전체 입력 영역 및 `UILobbyDifficultyRecordPanel` 연결.
- `CurrentStage/VisualLayers/HeraldryCaption/Description`: 평상시 설명. CanvasGroup은 호버 전환만 담당한다.
- 같은 Caption의 `RecordShadeMask/RecordShade`: `UIRecordCardShade`가 하단이 짙고 상단이 투명한 면을 그린다. Color의 alpha로 농도, Fade Height(현재 240)로 투명한 위쪽부터 최대 농도까지의 길이를 조절한다. 면은 마스크의 좌우·하단까지 이어지며, 페이드가 끝난 아래쪽도 같은 최대 농도로 채운다.
- `RecordShadeMask`: 원본 프레임의 본문 내부 윤곽으로 추출한 `difficulty_inner_mask_v1.png`와 Unity `Mask`를 사용한다. `Show Mask Graphic`은 끄고, 금색 테두리·장식 밖의 그라데이션만 잘라낸다. 기록 글자·아이콘은 이 마스크 밖의 기존 위치에 둔다.
- 같은 Caption의 `InlineRecords/ClearTime`, `InlineRecords/HighestWave`: 각각 흰색 모래시계와 칼, 제목, 기록 값.

`HeraldryCaption` 자체의 alpha는 기존 난이도 전환이 제어한다. 기록과 설명은 자식 CanvasGroup만 바꾸므로 두 애니메이션이 충돌하지 않는다.
`VisualLayers`는 raycast 차단 상태를 유지하며, 같은 카드 범위의 직계 자식 HitArea에서 입력을 받는다. StartButton은 HitArea보다 나중에 그려져 기존 입력 순서를 유지한다.
`StageRecord/ClearTimeText`의 난이도 요약 금색 문구는 계속 표시한다. 이전 `StageRecord/RecordPanelVisual`과 `TutorialBridge/LobbyCardRecordToggle`만 현재 Lobby에서 비활성화한다. 원본 코드와 구형 씬의 확장 모드는 유지한다.

## 동작과 데이터

- 진입 유예 0.08초, 진입 페이드 0.20초, 이탈 유예 0.10초, 복구 0.16초. `Time.timeScale`과 무관하게 동작한다.
- 새 난이도 바인딩·비활성화·포커스 이탈 시 표시를 초기화한다. 재진입은 현재 진행률에서 이어진다.
- 부모 CanvasGroup 입력 차단과 최상위 UI raycast를 검사해 팝업 뒤의 기록 호버를 막는다.
- `LobbyRecordBridge`가 기존 저장 기록을 주입한다. `SetRecord`, `ClearRecord`, `Bind` API와 저장·계산 로직은 유지한다.
- 최대 클리어 웨이브와 최단 전체 클리어 시간은 독립된 기록이다. 같은 플레이의 기록이라는 의미가 아니다.
- 기록이 없으면 `기록 없음` / `--:--`, 부분 진행은 `N 웨이브` / `--:--`를 표시한다.
- 난이도 인덱스는 기존 `EASY=보통`, `NORMAL=어려움`, `HARD=지옥`을 유지한다.

## Editor 도구

`Tools > UI > Lobby Records > Apply Inline Layout`: 현재 Builds/Lobby에서 한 번 구성한다. Undo를 지원하며 씬을 자동 저장하지 않는다. 이미 구성돼 있으면 재생성하지 않는다.

`Tools > UI > Lobby Records > Apply Frame Interior Mask`: 현재 프레임의 Sprite 영역과 본문 색상 연결 영역에서 마스크를 추출한다. 원본 기준 2px 안쪽으로 맞춰 테두리의 금색 면과 필터링을 보호한다. Shade의 위쪽 시작 높이를 유지하고 좌우·하단을 마스크까지 늘린다. 기록 정렬과 연결은 유지하며, 오브젝트 변경은 Undo를 지원한다. 생성 PNG는 에셋으로 남으며 씬 저장은 별도로 수행한다.

`Tools > UI > Lobby Records > Validate And Capture`: 계정 데이터를 사용하지 않는 임시 UI 복제본에서 표시와 레이아웃을 검사한다. 실제 마우스 이벤트를 사용하는 Play Mode 검증은 별도로 수행한다.

`Tools > UI > Lobby Records > Validate Mask And Capture`: 마스크 구조와 스텐실 쓰기·복원, 음영의 하단 전체 채움과 알파 연속성 및 기록 전환을 검사한다. 기존 수동 줄바꿈 배치를 바꾸지 않으며, 이 전용 검사에서는 글자 영역 크기 검사만 명시적으로 생략한다.

## 수동 확인

- 카드 진입·이탈 및 빠른 재진입에서 설명과 기록이 정상 교차 전환되는지.
- 카드 위에 커서를 둔 채 난이도를 변경해 이전 난이도 기록이 남지 않는지.
- 설정/도감/특성 팝업을 열고 닫을 때 뒤쪽 카드가 호버되지 않는지.
- 좌우 화살표 및 전투 준비 버튼 입력과 난이도 잠금이 유지되는지.
- 게임 포커스 이탈·복귀 및 로비 재진입에서 초기 상태가 복원되는지.

씬 충돌 주의 파일: `Lobby.unity`. 공용 프리팹, ProjectSettings, 저장 데이터는 이 작업 범위에 포함하지 않는다.

## 2026-10-12 검증 결과

- Unity 컴파일 완료, Console Error 0개, 로비 Missing Script 0개.
- 임시 복제 UI에서 무기록·부분 기록·완료 기록·123분 표시 및 글자 넘침 검사 통과.
- 페이드 진행률 0/0.5/1에서 RectTransform 불변, Description/음영/기록 알파 교차와 부모 난이도 알파 독립 검사 통과.
- 난이도 바인딩, 부모 입력 차단과 ignoreParentGroups, 포커스 이탈, 비활성화/재활성화 복원 검사 통과.
- 원본 씬과 직렬화 상태를 보존한 채 일반/호버/지옥 긴 시간 캡처 완료. 표시된 08:42·30 웨이브는 검증용 예시이며 실제 계정 기록이 아니다.
- 실제 Play Mode 마우스 이동·실시간 Tween·팝업 왕복은 미확인.
- Lobby 저장 완료, 미저장 표시 없음. 기존 오브젝트 변경은 HitArea RectTransform/패널 연결, Caption 자식 추가, Description CanvasGroup 추가, 구형 RecordPanelVisual 비활성화, 기존 LobbyCardRecordToggle 비활성화이다. StageRecord 부모와 금색 요약 문구는 유지한다.
- C# Git 공백 검사 통과. 씬에는 Unity 직렬화가 생성한 빈 `m_Name` 뒤 공백만 존재하며 텍스트로 수동 수정하지 않았다.

## 곡선 하단 마스크 검증

- 마스크와 Shade만 포함하는 구조, 테두리·기록이 마스크 대상에서 제외되는 것, 스텐실 쓰기/읽기/복원 검사 통과.
- 새 부모 연결 전후 기록 오브젝트의 직렬화 내용과 Shade 월드 모서리가 동일함을 확인했다.
- 일반/호버 캡처에서 마스크 밖 변경 픽셀 0개(래스터 좌표 맞춤 3px 허용), 곡선 하단의 배경·금색 면이 보존됨을 확인했다.
- 컴파일 성공, Console Error 0개. 실제 Play Mode 마우스 왕복은 미확인이다.
- 마스크 검증은 수동 두 줄 제목의 글자 영역 검사를 생략했다. 기본 전체 검증은 해당 검사를 유지한다.

## 마스크 내부 수평 경계 보완

- 기존 검증은 마스크 밖으로 번짐만 확인해 내부에서 먼저 끝나는 음영을 놓쳤다. 기존 412×240 육각형 면의 하단 Y=-260에서 어두운 면이 끊겨 곡선 앞에 밝은 띠가 남았다.
- 음영 면을 카드 좌우·하단까지 채우고 부모 마스크가 외곽 곡선을 결정하도록 수정했다. 페이드 시작 Y=-20, 길이 240, 최대 alpha 0.72는 유지한다.
- 이전 배치에서 새 하단 커버 검사가 실패하고 수정 후 통과함을 확인했다. 페이드 종료 전후 알파 연속성, 하단 좌·중·우 채움, 기존 기록 전환 검사도 통과했다.
- 기록 글자·아이콘 직렬화와 패널 연결은 수정 전후 동일하다. 프레임·버튼·마스크 PNG 해시도 동일하다.
- 임시 UI 비교 캡처에서 마스크 밖 변경 픽셀 0개(3px 허용), 컴파일 성공 및 Console Error 0개를 확인했다. 실제 Play Mode 마우스 호버는 미확인이다.
