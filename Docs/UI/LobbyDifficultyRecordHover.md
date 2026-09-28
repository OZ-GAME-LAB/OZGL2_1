# 로비 난이도 기록 호버 프레임

## 적용 범위

- Scene: Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity
- 표시: Canvas_Lobby > StageSelection > CurrentStage > StageRecord > RecordPanelVisual
- 입력/설정: Canvas_Lobby > StageSelection > CurrentStage > RecordHoverHitArea
- 중앙 정보판만 변경했다. 좌우 카드, 난이도 선택, 그림 전환, 전투 준비 버튼은 유지한다. 중앙 이름/설명의 정렬과 폰트는 승인 목업에 맞춰 조정했다.
- 기존 StageRecord Image는 비활성화만 했으며 원본 Sprite 참조와 파일은 보존했다.
- 이번 범위는 호버 표시와 외부 기록 표시 API다. 실제 전투 결과 집계, PlayerPrefs, 파일 저장, 난이도 전달은 연결하지 않았다.

## 동작

1. 기본 상태는 폭 560, 높이 약 136.79의 작은 판이며 이름/설명만 표시한다.
2. 마우스 진입 후 0.08초 대기, 0.22초 동안 높이 약 250.08까지 위쪽으로 확장한다.
3. 바닥·난이도 문구는 고정된다. 기록 영역만 RectMask2D로 드러나고 지연 페이드인한다.
4. 펼쳐진 판 안의 이동은 유지, 이탈 후 0.10초 유예와 0.16초 접힘.
5. 접는 중 재진입하면 현재 높이를 보존하고 반전한다.
6. 난이도 전환 중에는 접고 호버 입력을 잠근다. 기존 전환의 투명 시점에 이름/그림/기록을 함께 교체한다.
7. 난이도 표시 복구 후 호버 잠금을 풀고 실제 포인터를 재검사한다.
8. UI Tween은 unscaled time이며 비활성화/파괴/포커스 이탈 때 자신이 소유한 Tween만 정리한다.
9. 열린 상태나 유예 중에만 0.08초마다 실제 포인터·최상단 UI를 검사한다. 이탈 이벤트 누락이나 팝업 가림에도 접히며, 완전히 닫힌 대기 상태에서는 추가 Raycast를 하지 않는다.

## Inspector 조절

RecordHoverHitArea의 UILobbyDifficultyRecordPanel:

| 항목 | 기본값/역할 |
| --- | --- |
| Collapsed Height / Expanded Height | 약 136.79 / 250.08, 하단 기준 전체 높이 |
| Enter Delay / Exit Delay | 0.08 / 0.10초 |
| Expand Duration / Collapse Duration | 0.22 / 0.16초 |
| Expand Ease / Collapse Ease | OutCubic / OutQuad |
| Record Fade Start | 0.25, 펼침 진행률 중 기록이 나타나기 시작하는 지점 |
| Panel Rect / Reveal Rect | 프레임 전체와 위쪽 기록 마스크 |
| Record Group | 기록 내용만의 투명도, 부모의 난이도 전환 투명도와 분리 |
| Wave Value Label / Time Value Label | 각각 기록값 TMP |

확장 높이를 변경할 때는 RecordPanelVisual > RecordReveal > RecordContent의 콘텐츠 높이/여백도 함께 확인한다.
이름·설명은 StageRecord의 기존 RecordText/ClearTimeText에서 편집한다.
기록 라벨·값·아이콘은 RecordContent > HighestWave/ClearTime에 분리되어 있다.

## 아트/폰트

- 프레임/구분선은 `RecordHover/Sprites/Ref_*.png` 7종이다. 승인 목업의 실제 장식 픽셀에서 배경 제거/부품 분리만 수행했다. 트로피/모래시계는 추출본의 작은 해상도와 불균일한 여백 문제로 별도 생성한 `Icon_Trophy_Centered_v2.png`, `Icon_Hourglass_Centered_v2.png`로 교체했다.
- `Tools/Art/ExtractApprovedDifficultyRecordArt.ps1`이 재현 가능한 추출 스크립트다. 원본은 `Docs/UI/References/LobbyDifficulty/difficulty_record_frame_hover_v2.png`로 보존한다. 이전 생성 아트와 스크립트는 이력이며 현재 프레임으로 적용하지 않는다.
- 상단/하단은 동일 종횡비의 고정 캡, 레일은 길이만 바뀐다. 닫힘 상태에서 장식이 만나는 부분은 하단 캡이 위에 그려진다.
- `Interior`의 `UIRecordPanelBackground`는 이음선 없는 어두운 팔각형 UI 면만 그린다. 장식은 그리지 않으며, 높이에 따라 좌우 레일을 0 이상으로 유지한다. 반복 붉은 체크 질감은 사용하지 않는다.
- 기록 아이콘, 가로/세로 고금색 구분선, 이름 아래 붉은 구분선은 각각 투명 PNG다. 문자는 모두 TMP로 분리한다.
- 아이콘 v2는 실제 투명 알파의 생성 원본을 보존한 뒤 불투명 영역 기준으로 트리밍/중앙 정렬했다. 두 PNG 모두 256×256, Sprite pivot (128,128), UI RectTransform은 32×32/pivot (0.5,0.5)다. 그림의 긴 변은 224px로 통일하며 가로/세로 비율은 유지한다. Point/압축 없음/Mipmap 없음/PreserveAspect로 표시한다.
- 아이콘 2개만 교체할 때: `Tools > OZGL2 > Lobby > Replace Difficulty Record Icons Only (Current Lobby Only)`. 각 Icon의 가로 중심은 유지하고 해당 Label과 중심 높이를 맞춘다. Undo 지원, 자동 저장 없음. 전체 프레임 적용 메뉴는 수동 배치를 바꿀 수 있으므로 아이콘 수정에는 사용하지 않는다.
- 아이콘 생성 프롬프트: `Tools/Art/lobby_record_icons_v2_prompts.md`, 재현 가능한 크기 정렬: `Tools/Art/NormalizeDifficultyRecordIcons.ps1`. 이전 추출 PNG는 원본 보존용이며 더 이상 두 Icon에 쓰지 않는다.
- 작은 라벨/설명: NotoSansCJKkr 원본으로 만든 전용 `LobbyRecordText SDF.asset`. 90pt, padding 9, 2048 atlas, SDFAA, 필요한 문자를 사전 등록한 Static SDF다.
- 난이도명/기록 수치: DOSMyungjo 원본을 별도 `LobbyRecordEmphasis Pixel.asset`으로 생성했다. 90pt, padding 2, 1024 atlas, RASTER/Point/Static이다. 공유 Dynamic 명조 폰트의 atlas는 수정하지 않는다. 낮은 해상도 DOS 원본에 SDF를 적용할 때 생기는 획 아래 아티팩트를 피하고 원래 도트 획을 보존한다.
- 기본 TMP Mobile 재질을 사용하며 공유 폰트/재질은 바꾸지 않는다. 기록 라벨 17, 값 24, 이름 26, 설명 19에 아트 공통 배율 `560/524`를 곱한다.
- 전용 폰트 2종은 `Assets/98.ExternalAssets/00.LocalStaging/01.Font`에 저장한다. 기존 Git 제외 정책을 유지하며 두 에셋과 원래 `.meta`를 함께 Drive로 공유해야 한다. 팀원이 새 GUID로 생성하면 씬 폰트 참조가 끊어진다.
- 카탈로그 설명에 새 문자를 추가한 경우 아래 적용 메뉴가 기존 GUID를 유지하면서 필요한 글리프를 추가하고 Static으로 되돌린다.

## 기록 API

UILobbyDifficultyRecordPanel은 외부가 전달한 표시용 스냅샷만 보관한다. SO나 저장 데이터에 쓰지 않는다.

```csharp
// 예시 주입. 실제 저장 API가 아니며 실행 세션 동안만 유지된다.
panel.SetRecord(eLobbyDifficulty.NORMAL, 25, 522f); // 25 웨이브 / 08:42
panel.SetRecord(eLobbyDifficulty.EASY, 3);         // 3 웨이브 / --:--
panel.ClearRecord(eLobbyDifficulty.NORMAL);
panel.ClearAllRecords();
```

- 난이도별 미주입 상태는 '기록 없음 / --:--'.
- 음수 웨이브/시간, 잘못된 enum, NaN/Infinity 시간은 거부하며 이전 표시값을 보존한다.
- 시간은 초 단위 float?로 받는다. null은 시간 없음, 0은 유효한 00:00이다.
- 소수초는 버리고 총 분:초로 표시한다. 3723.9초는 62:03이다.
- 최고 웨이브와 시간의 집계 기준은 호출자가 책임진다. 최고 웨이브 달성 플레이의 시간인지, 완주 최단 시간인지 결정하기 전에는 시간 null 사용을 권장한다.
- 예시 수치는 테스트에서만 주입하며 저장 씬에는 실제 기록처럼 넣지 않는다.

## 코드 책임과 변경

- UILobbyDifficultyRecordPanel.cs 신규: 호버·레이아웃·기록 스냅샷 표시. 저장과 선택 책임 없음.
- UILobbyDifficultySlotView.cs: 중앙 슬롯의 선택적 _recordPanel을 투명 시점 Bind와 함께 갱신.
- UILobbyDifficultySelector.cs: 표시 전환의 TransitionStateChanged(bool) 추가. 복구 후 잠금 해제.
- LobbyDifficultyRecordPanelBuilder.cs 신규: 지정 로비 Edit Mode에만 구성하고 Undo 지원.
- LobbyDifficultyRecordPanelValidation.cs 신규: Play Mode 자동 검증과 원래 상태 복구.
- UIRecordPanelBackground.cs: 단순 안쪽 면과 레일 길이 표시 책임을 호버/저장 로직과 분리.
- LobbyDifficultyRecordReferencePolisher.cs: 승인 목업 비율/정렬/폰트를 중앙 기록판에만 적용.
- LobbyDifficultyRecordTypography.cs: 전용 SDF 준비, 글리프 사전 검사, 기존 GUID 보존.

## Editor 메뉴 / Undo

- Tools > OZGL2 > Lobby > Configure Difficulty Record Hover (Current Lobby Only)
- Tools > OZGL2 > Lobby > Apply Approved Difficulty Record Frame (Current Lobby Only)
- Tools > OZGL2 > Lobby > Validate Difficulty Record Hover (Play Mode)
- 구성 메뉴는 다른 Scene/Play Mode에서 실행하지 않는다. 이미 구성된 경우 사용자 설정을 덮어쓰지 않는다.
- Scene 오브젝트 구성은 Undo 지원. 원본 PNG와 새 PNG/Importer 에셋은 별도로 남는다.
- 구성 메뉴는 Scene을 dirty로 표시할 뿐 자동 저장하지 않는다.
- 프레임 교체 메뉴는 최신 ReferencePolisher로 연결되며 지정 로비 중앙 기록판의 프레임/폰트/배치를 적용한다. 기록값/전환 코드/버튼 기능은 유지한다. PNG/Importer/SDF 에셋은 Scene Undo와 별개로 보존된다.

## 검증

### 기록 아이콘 v2 교체

- 기존 Sprite/UI pivot 자체는 이미 Center였지만 추출된 34×36/30×38 이미지 내 그림 여백과 표시 크기가 달랐다.
- 교체 후 두 Image 모두 32×32, normalized pivot (0.5,0.5), 해당 Label과 중심 높이 차이 0, Raycast Target off로 확인했다.
- 교체 직전 기준으로 두 Icon 외부 5,846개 컴포넌트 직렬화 값은 동일했다. 기존 미저장 씬 변경은 보존한 채 저장했다.
- Runtime/폰트/프레임/호버 기능은 이번 교체에서 변경하지 않았다.
- Play Mode에서 실제 마우스 진입으로 확장한 표시를 1회 확인했다. 최종 Console Error/Warning 0, 임시 포인터 및 실행 설정을 복원하고 Play Mode를 종료했다. 화면: `Temp/LobbyDifficulty/RecordHover/IconRevision/Icons_InGame.png`.

### 2026-09-22 승인 픽셀/정렬/전용 폰트 적용

- 최초 비교에서 얇은 프레임, 짧은 대각 모서리, 반복 붉은 바탕, 작은 아이콘, 넓은 기록-난이도 간격을 확인해 수정했다.
- 실제 기본/펼침 화면과 마우스 진입 확장/이탈 접힘을 항목별로 한 번 확인했다. 이전 237개 회귀 검사는 반복하지 않았다.
- 화면 확인 중 `Interior`에 CanvasRenderer가 누락된 것을 발견해 RequireComponent/Editor 연결을 수정했다. 이 부분은 수정 후 표시만 추가 확인했으며 호버 검사를 다시 반복하지 않았다.
- 컴파일 중 글리프 조회 API 접근 오류 1건을 공개 `TryGetGlyphWithUnicodeValue`로 수정했다.
- 필수 Inspector 참조 정상, Missing Script 0. 전용 폰트 2개는 각각 58문자/Static/default TMP shader로 확인했다.
- 변경 범위 밖 5,774개 컴포넌트의 직렬화 값은 동일했다. 초상화, 전투 준비, 다른 메뉴는 변경하지 않았다.
- 실제 화면: `Temp/LobbyDifficulty/RecordHover/ApprovedPixelPolish/Closed.png`, `Expanded.png`. 펼침 이미지의 25 웨이브/08:42는 저장되지 않는 표시 예시다.
- 수정 후 최종 Console Error/Warning 0. Play Mode 종료, 캡처용 runInBackground 복원, 임시 기록/확장 상태는 저장하지 않았다. 씬은 기본 접힘 상태로 저장했다.
- 다른 해상도/플레이어 빌드/실제 전투 저장 연동은 이번 확인 범위가 아니다.

### 이전 120→220 프레임 검증 이력 (시각 디자인은 이후 교체됨)

- 프레임 5종 교체, 상단 4조각 및 Editor 조립/높이만 수정했다. Runtime 스크립트는 이번 수정에서 바꾸지 않았다.
- 최종 120→220 프레임으로 난이도 162개 + 기록 호버 75개, 총 237개 자동 검사를 다시 통과했다.
- 별도 Play 재실행에서 timeScale=0 실제 Input System 경로로 진입 확장 / 확장 영역 유지 / 호버 중 어려움 전환 후 재확장 / 바깥 이탈 접힘 4단계를 통과했다.
- 변경 전 비교 대상 5,788개 컴포넌트 중 프레임 바깥에서는 RecordHoverHitArea의 RectTransform/Controller 높이 설정 2개만 바뀌었다. 기존 난이도 글자·전투 준비·화살표·다른 UI는 동일했다.
- 최종 실제 Game View: `Temp/LobbyDifficulty/RecordHover/ReferenceCorrection/Frame_Closed_Final.png`, `Frame_Expanded_Final-1.png`.
- 캡처 도구 `MCPForUnity.Runtime.Helpers.ScreenshotUtility.CaptureCompositedAfterFrame` 197행에서 PlayerLoop 재귀 Error가 1회 발생했다. 전체 stack을 확인한 뒤 기록을 남기고 Console을 비워, **캡처 없이** Play 재실행/위 4단계를 재검증했다. 재실행에서는 Error/Warning 0개였다. 게임 코드를 수정하거나 Packages를 변경해 숨기지 않았다.
- 프로브 종료, 기본 보통/접힘/기록 없음, timeScale=1, runInBackground=false로 복구했다. 플레이어 빌드는 미실행이다.
- Scene 저장 시 Unity가 생성하는 빈 `m_Name: `의 trailing whitespace 경고는 유지했다. Scene YAML을 직접 고치지 않았다.

### 최초 기능 구현 검증 이력

- 기존 난이도 선택 자동 검사 162개 통과.
- 기록 호버 자동 검사 75개 통과.
- 필수 Inspector 참조/Scene Missing Script 검사 정상.
- Input System 위치 이벤트를 지속 주입하는 Editor 전용 프로브로 실제 UI 입력 경로를 확인했다. OS 커서 이동·클릭이나 입력 설정 변경은 하지 않았다.
- timeScale=0 상태에서 진입 확장, 확장 영역 안 이동 유지, 이탈 접힘, 재진입, 다른 UI 가림 시 접힘, 가림 제거 후 재확장, 호버 중 난이도 변경 후 재확장 7단계를 통과했다. 검사 중 실제 프레임 2,415개가 경과했다.
- PreviousArrow/NextArrow/StartButton 중앙 Raycast가 각각 해당 버튼으로 도달했다.
- StageSelection 밖 기존 컴포넌트 5,620개의 직렬화 값이 동일했고 전투 준비 Button/RectTransform도 동일했다.
- 최종 Console 오류/경고 0개, Missing Script 0개, 필수 참조 정상. 테스트용 포인터 프로브·가림 오브젝트·예시 기록은 모두 정리했다.
- Play Mode를 종료하고 기본 보통/접힘/기록 없음 상태로 복구했다. 검사를 위해 임시 변경한 runInBackground와 timeScale도 false/1로 복구했다.
- Game View 캡처: Temp/LobbyDifficulty/RecordHover/Record_Hover_NoRecord_Final.png, Record_Hover_Example_Final.png. 두 번째는 검증용 25 웨이브/08:42이며 저장된 기록이 아니다.
- 플레이어 빌드와 실제 전투 결과 저장 연동은 수행하지 않았다. 다른 해상도·터치·게임패드에서의 호버 대체 UX는 별도 검토 대상이다.

### Editor 포인터 검사 보조 API

LobbyDifficultyRecordPanelValidation.BeginPointerProbe(screenPosition), MovePointerProbe(screenPosition), EndPointerProbe(), GetPointerProbeStatus()를 제공한다. 대상 로비 Play Mode에서만 사용하며 60초 후 자동 종료, Play 종료/재컴파일 시에도 정리한다. 기존 Mouse 장치를 제거하거나 OS 커서를 이동하지 않는다. 현재 입력 위치가 바뀌므로 사용자 조작과 동시에 실행하지 않는다.

## Git / 리뷰

- Scene 변경 Yes: 위 로비 Scene의 중앙 난이도 정보 영역.
- Prefab/ProjectSettings/Packages 변경 없음.
- Meta는 Unity가 생성/임포트한 새 코드·아트 관련 파일.
- 검토 포인트: 투명 시점 기록 바인딩, 전환 종료 알림 순서, 캡/레이아웃 참조, HitArea가 버튼을 덮지 않는지, 새 파일과 meta 짝.
- 레이어는 기존 UI를 재사용한다. Tag/Collider/Rigidbody/Animator/Input Action 에셋 변경은 없다.
