# 로비 특성 트리 구현 / 검증 기록

## 적용 범위

- 대상 화면: `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`의 특성 탭.
- 실제 저장 대상: `Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab`의 `TraitsScreen`.
- 씬 파일 자체는 변경하지 않고 기존 프리팹 연결을 사용한다. 메뉴/설정 팝업의 기존 오브젝트와 기능은 유지한다.
- 특성 수치와 선행 관계는 기존 `Resources/Traits`의 `TraitData` 40개를 그대로 사용한다. 전투 효과 전체를 새로 구현한 작업은 아니다.
- 레벨업 비용은 공통 특성 시스템에서 모든 단계/일반/특화에 **1 LP**로 통일했다. 이 비용 규칙을 공유하는 샌드박스에도 적용되며, UI 배치는 위 프리팹의 특성 영역만 변경한다.

## 동작

- 중앙 마왕을 기준으로 위 마왕군 조련(녹색), 오른쪽 인간계 저주(보라), 아래 주문 연구(파랑), 왼쪽 지배의 지혜(금색)를 배치한다.
- 계열 제목은 테두리 없는 상징, 일반 특성 36개는 일반 프레임, 최종 특성 4개는 특화 프레임을 사용한다. 아이콘/프레임/마법진은 기존 분리 아트를 재사용한다.
- 상하좌우 드래그로 탐색한다. 마우스 휠은 커서 위치를 기준으로 25%~160% 확대·축소하며 수직 스크롤은 하지 않는다. 최소 배율에서 전체 트리 범위를 볼 수 있다. 설명창/고정 HUD는 확대하지 않는다.
- `중앙으로`는 현재 배율을 유지한 채 이동 관성을 멈추고 중앙으로 복귀하며 설명을 닫는다.
- 잠긴 특성도 클릭하여 설명을 볼 수 있다. 다른 특성을 클릭하면 설명을 갱신한다. 설명 내부 클릭은 유지하고 바깥 빈 영역 클릭은 닫는다. 별도 닫기 버튼은 없다.
- 현재/최대 레벨은 **설명창의 이름 아래에만** 표시한다. 트리 노드에는 이름만 남기고 레벨/MAX/잠금 문구를 제거한다. 잠긴 상태는 아이콘/프레임의 어두운 색으로 구분한다.
- 최대 레벨이면 프레임 Sprite를 만렙 전용 이미지로 교체한다. 프레임에 밀착한 고금색 외곽선과 계열색의 어두운 내부 채움으로 구분하며, 레벨을 내리면 기본 이미지로 돌아간다. 기존 별도 얇은 외곽선 오브젝트는 제거했다. 클릭 선택 표시는 별도 유지한다.
- 특성명은 만렙 시 계열색(녹색/보라/파랑/금색), 중간 레벨 시 기존 밝은색, 선행 조건 미충족 시 어두운색이다. 해금된 0레벨은 선택 가능한 상태이므로 기존 밝은색이다. 설명창 텍스트 색은 변경하지 않는다.
- 특성 아이콘 40개는 PNG 알파 영역 중심을 기준으로 다이아몬드 내부에 배치한다. 원본 PNG는 수정하지 않는다. 중앙 마왕도 같은 방식으로 정렬하며, 테두리 없는 계열 상징은 유지한다.
- 설명 하단에는 `-` / `+`, 환급/소모 LP, 사용 불가 이유를 표시한다.
- 선행 특성은 **모두 최대 레벨**이어야 다음 특성이 열린다. 최대 레벨, LP 부족, 선행 조건 미충족 시 `+`를 비활성화한다.
- 레벨 0이거나 이미 배운 후속 특성이 의존하는 경우 `-`를 비활성화한다. 후속 특성부터 내려야 한다.
- 레벨 다운은 단계당 1 LP를 반환한다. 해당 레벨과 LP는 기존 PlayerPrefs 저장 경로를 사용한다.
- 하단은 동일 크기 310×75의 `[특성 초기화] [중앙으로]`를 x=-167/+167, y=-470에 배치해 중앙선 기준으로 정렬했다. 기존 버튼 아트를 재사용한다.
- `특성 초기화`는 투자량이 있을 때만 활성화한다. 확인창에 환급 LP를 표시하고, 확정 시 전체 특성을 0레벨로 돌려 투자 LP 전액을 반환한다. 취소/ESC는 데이터를 바꾸지 않는다. 확인창은 뒤쪽 UI 입력을 차단하며 마왕 레벨/경험치는 초기화하지 않는다.
- 비활성 버튼은 어두운 저채도 색으로 표시하며 클릭 처리를 막는다.

## 변경 파일과 책임

| 파일 | 역할 / 변경 이유 |
| --- | --- |
| `Assets/00.Project/01.Scripts/UI/UITraitFrameView.cs` | TraitData 참조, 만렙/진행/미해금 이름 색상, 기본/만렙 Sprite 교체, 선택 이벤트 |
| `Assets/00.Project/01.Scripts/UI/UITraitTreeZoom.cs` | 휠 확대·축소와 커서 기준 위치 보정. 드래그는 기존 ScrollRect에 위임 |
| `Assets/00.Project/01.Scripts/UI/UITraitOverlayView.cs` | 설명 표시, 선택 전환, 레벨 버튼 상태와 요청, 중앙 복귀, 초기화 확인창 |
| `Assets/00.Project/01.Scripts/UI/UILobbyOverlayView.cs` | 특성 초기화 확인창이 열렸을 때 ESC가 확인창부터 취소하도록 처리. 메뉴/설정 동작 유지 |
| `Assets/00.Project/01.Scripts/UI/UITraitProgressionController.cs` | UI와 계정/특성 모델 연결. UI가 저장/효과 계산을 직접 소유하지 않도록 분리 |
| `Assets/00.Project/01.Scripts/UI/UITraitDismissSurface.cs` | 설명 외부 클릭 처리. 드래그와 클릭 구분 |
| `Assets/01.Scripts/Progression/TraitData.cs` | 모든 유효 단계의 비용을 1 LP로 통일. 예전 rankCosts 직렬화는 테스트 데이터 정산용으로 보존하되 신규 비용 계산에서는 제외 |
| `Assets/01.Scripts/Progression/TraitTree.cs` | 레벨 다운 가능 판정, 1 LP 환급, 전체 투자량/초기화 및 환급, 검증용 비영속 모델 옵션 |
| `Assets/01.Scripts/Progression/MawangLevel.cs` | 실제 계정에 영향을 주지 않는 검증용 비영속 모델 옵션. 기존 생성자는 그대로 유지 |
| `Assets/00.Project/Editor/LobbyTraitTreeBuilder.cs` | 특성 영역만 조립하는 Editor 전용 도구 |
| `Assets/00.Project/Editor/LobbyTraitTreeValidation.cs` | 실제 모델과 UI 이벤트 경로를 검사하는 Editor 전용 검증 도구 |
| `Assets/06.UI/LobbyMutedPreview/Overlays/Fonts/TraitPixelTMPOutline.shader` | TMP 대체 글꼴이 필요로 하는 `_CullMode`를 지원하는 특성 전용 픽셀 테두리 셰이더 |
| `Assets/06.UI/LobbyMutedPreview/Overlays/Fonts/DOSMyungjo Trait Outline.mat` | 특성 텍스트 전용 머티리얼. 메뉴/설정 및 원본 외부 폰트는 변경하지 않음 |
| `Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab` | 특성 노드/연결선/설명창 배치 및 참조 연결 |
| `Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/MaxLevel/` | 일반 4종·특화 4종 만렙 프레임, 투명도/정렬 검사 기록 |
| `Tools/Art/BuildTraitMaxFrames.cjs` | 승인된 생성 이미지의 배경 제거와 크기 정렬만 수행하는 후처리 도구 |
| `Tools/Art/BuildTraitMaxFrames.inputs.json`, `Tools/Art/Sources/TraitMaxFrames/` | 개별 생성 프롬프트와 원본 보존. 내장 image_gen 사용, API/CLI 생성 아님 |

새 스크립트/머티리얼/셰이더의 `.meta`는 Unity가 생성했다. 기존 `.meta`를 직접 편집하지 않았다.
이전에 추가했던 `UITraitMaxLevelOutline.cs`는 참조가 없어졌음을 확인한 후 Unity로 제거했다. 원래 일반/특화 프레임 아트는 수정하지 않았다.

## Editor 메뉴와 Undo

- `Tools/OZGL2/Lobby/Build Interactive Trait Tree`: 저장된 대상 씬의 편집 모드에서 실행한다. Prefab Mode에서 특성 영역만 조립하며 하나의 Undo 그룹을 지원한다.
- 생성된 트리가 이미 있으면 재생성을 거부한다. 현재 결과는 저장되어 있으므로 다시 실행할 필요가 없다.
- `Tools/OZGL2/Lobby/Polish Trait Nodes And Wheel Zoom`: 기존 노드의 레벨 문구를 제거하고 만렙 프레임 연결/아이콘 정렬/휠 줌을 적용한다. 기존 노드 데이터와 메뉴/설정창은 유지하고 한 Undo 그룹을 지원한다. 현재 결과는 이미 적용·저장되어 있다.
- `Tools/OZGL2/Lobby/Apply Max Level Trait Frame Sprites`: 특성 40개에 기본/만렙 Sprite를 연결하고 `MaxLevelBorder` 자식만 제거한다. Prefab Mode에서 하나의 Undo 그룹을 지원하며 메인 씬의 미저장 내용은 저장하거나 되돌리지 않는다. 현재 프리팹은 적용·저장되어 있다.
- `Tools/OZGL2/Lobby/Apply Trait Reset And Name States`: 특성명 상태 색상, 초기화 버튼, 하단 좌우 정렬, 확인창과 Inspector 참조를 구성한다. 한 Undo 그룹을 지원하며 메인 씬은 저장하지 않는다. 현재 적용·저장 완료. 사용자가 승인한 범위에서 Scene Change Plan / Unity Editor Tool 절차로 작업했다.
- 도구가 직접 저장을 호출하지 않더라도 **Prefab Mode의 Auto Save가 켜져 있으면 Unity가 저장할 수 있다**. 수동 확인 후 저장하려면 Auto Save를 끈 상태에서 작업한다.
- `Tools/OZGL2/Lobby/Validate Trait Progression Rules`: 비영속 모델로 선행 조건/레벨/차감/환급 규칙을 검사한다.
- `Tools/OZGL2/Lobby/Validate Trait UI In Play Mode`: 대상 씬을 재생한 뒤 실행한다. 프레임 단위로 검사하므로 Unity를 활성 창으로 유지한다. 완료되면 원래 계정 모델을 다시 연결한다.
- `Tools/OZGL2/Lobby/Validate Trait Icon Alignment`: 저장된 프리팹의 특성 아이콘 40개가 중앙 정렬되고 프레임 내부에 있는지 PNG 알파 영역으로 확인한다.

## 확인 결과

Unity 6000.3.22f1에서 확인했다.

- 특성 모델 검증: **1,474개 단언 통과**, 특성 40개 전체 포함. 모든 단계 1 LP, 전체/빈 배분 초기화, 중복 환급 방지, 초기화 후 재투자 및 선행 잠금 복구 포함.
- Play Mode UI 검증: **853개 단언 통과**. 기존 레이캐스트/설명/레벨/환급/드래그/줌 검증에 3상태 특성명 색상, 버튼 중앙 정렬, 초기화 확인/취소/ESC, 뒤쪽 클릭 차단, 환급량 표시, 재진입 후 이벤트 단일 실행을 추가했다.
- 만렙 Sprite 8종이 40개 노드에 연결되어 있고, `MaxLevelBorder` 자식은 0개임을 확인했다. Sprite 교체 시 아이콘과 프레임 RectTransform 크기가 바뀌지 않는지도 검사했다.
- 아이콘 알파 영역 검증: 특성 아이콘 **40개 중앙 정렬 / 40개 다이아몬드 내부 범위 통과**.
- 검증은 저장하지 않는 별도 모델을 사용하며 실행 전후 실제 계정 PlayerPrefs가 같음을 검사한다. 테스트 LP를 실제 계정에 지급하지 않는다.
- 설명 텍스트 넘침 및 누락 참조를 확인했다. 수정 후 실제 화면을 캡처하여 레벨/포인트/설명/버튼 표시를 확인했다.
- 최종 Play Mode 검증 시 Unity Console 오류 0개 / 경고 0개. 특성 화면의 Missing Script 및 폰트/아이콘/데이터 누락 0개.
- 검증 중 활성화 직후 같은 프레임에 다음 버튼을 누르던 테스트는 프레임 대기를 추가했다. 재생 전환 도중 테스트가 도메인 재로드로 중단된 실행은 결과에서 제외하고 재실행했다.
- 메뉴 팝업 99개, 설정 팝업 89개 직렬화 문서는 변경 전과 동일함을 비교했다.

## Inspector / 팀원 확인 목록

- `TraitsScreen`의 `UITraitProgressionController`: View, Account Level, Nodes 40개 연결.
- 각 `UITraitFrameView`: 서로 다른 TraitData, 프레임/아이콘, 이름 Label, Default Frame Sprite/Max Level Frame Sprite 연결. Rank/RankShade/MaxLevelBorder 자식은 없다.
- 각 노드의 Default/Locked/Max Level Name Color 연결. 만렙 색은 가지별로 다르며 기본/미해금 색은 공통이다.
- `UITraitOverlayView`: ScrollRect, 설명 텍스트/아이콘, 포인트, +/- 버튼 및 Label 연결.
- `UITraitOverlayView`: Reset Button/Label, Reset Confirmation/Message, Confirm/Cancel Reset Button 연결. 확인창은 시작 시 비활성이며 배분 0이면 초기화 버튼이 비활성이다.
- `TraitTreeViewport`: 가로/세로 스크롤, RectMask2D, Content, 외부 클릭 처리, UITraitTreeZoom 연결. Scroll Sensitivity=0, Min Zoom=0.25, Max Zoom=1.6.
- `SelectedTraitDetail`: 내부 클릭을 막는 배경 Graphic 유지. 시작 시 비활성.
- 폰트는 `01.Font`의 공유 파일과 **원래 `.meta`를 함께** 복원해야 한다. SDF Git 제외/Drive 공유 정책은 유지한다. 자세한 복원 방법은 `FontAssetSharing.md` 참고.
- LayerMask/Tag, Collider/Rigidbody/IsTrigger, Animator, Input System Action 추가 설정은 없다. 기존 EventSystem을 사용한다.

## 남겨둔 수동 플레이 확인

- 팀원 PC에서도 폰트 공유 파일을 복원한 뒤 한글/테두리와 설명창을 확인한다.
- 실제 마우스로 긴 드래그 후 노드 클릭, 여러 해상도에서 설명 가독성을 확인한다.
- 만렙 직전→만렙→레벨 다운 시 일반/특화 모두 금테와 내부 채움이 나타났다가 기본 프레임으로 복귀하는지 확인한다.
- 1 LP 차감/환급, 만렙 계열색↔중간 밝은색↔미해금 어두운색, 초기화 취소/확정 후 LP/잠금/버튼 상태를 확인한다.
- 실제 계정으로 레벨을 변경하면 영구 저장된다. 직접 시험할 때는 계정 데이터를 보존할지 먼저 결정한다.
- 로비에서 변경한 특성이 전투에 미치는 각 효과의 최종 반영은 기존 전투 시스템과 별도로 통합 확인한다.

## 테스트 계정 전환 (2026-09-18)

- 사용자가 배포 전 테스트 버전이므로 기존 특성 배분 초기화를 승인했다. 자동 마이그레이션/차액 환급 코드는 추가하지 않았다.
- 현재 로컬 계정의 `Hero_B1` 3레벨만 배분되어 있음을 확인하고 초기화했다. 예전 비용 1+2+3=6 LP를 반환해 보유 LP 9→15, 특성 배분 0으로 전환했다. 마왕 레벨 14 / XP 150 유지.
- PlayerPrefs를 다시 읽고 Play Mode로 재진입하여 반영 여부를 확인했다. 앞으로의 초기화는 새 1 LP 기준으로 투자량을 환급한다.
- 다른 팀원의 기존 테스트 저장 데이터까지 자동 초기화하지 않는다. 이미 높은 비용으로 투자한 별도 테스트 계정은 같은 방식으로 별도 정산/초기화가 필요하다.
- 상태 비교 캡처는 저장하지 않는 임시 모델을 사용했다. 화면의 비교용 LP/랭크가 실제 테스트 계정에 저장되지는 않는다.

## 설명 비교 / 전용 버튼 / 대칭 프레임 (2026-09-18)

### 완료 범위

- 사용자 승인 범위인 `UI_Lobby_MutedPreview`에서 사용하는 `Canvas_LobbyOverlays.prefab/TraitsScreen`만 변경했다. 메뉴 팝업, 설정 팝업, 별도 마왕 내부 아이콘은 적용 전후 직렬화 값이 같음을 검사했다.
- 설명창은 `(현재) 1p : 용사 방어력 -2%` / 구분선 / `(다음 레벨) 2p : 용사 방어력 -4%`로 누적 효과를 보여준다. 좁은 패널 안에서 머리말/수치 문장을 두 줄로 배치했다. 다음 효과 색상은 선택 노드의 `LockedNameColor`를 그대로 사용한다.
- 0p는 `적용된 효과 없음`, 만렙의 다음 영역은 `최대 레벨입니다.`로 표시한다. p는 누적 투자량이며 신규 강화 비용은 계속 1 LP다.
- 하단/확인창/뒤로/레벨 조절 버튼용 Sprite 4종, 효과 구분선, 중앙 마왕 프레임을 새로 생성했다. 기존 공용 Sprite와 원본은 보존했다.
- 버튼의 실제 Rect와 PNG 캔버스 비율을 일치시켰다. `Simple + Preserve Aspect`로 표시하므로 각 축을 따로 늘리지 않는다. 마왕 프레임은 좌우/상하 픽셀 대칭을 검증했다.

### 변경 파일과 책임

- `Assets/00.Project/01.Scripts/UI/UITraitEffectTextFormatter.cs`: 한국어 효과 표기만 별도 클래스로 분리했다. UI 컨트롤러에 효과별 문구 분기를 넣지 않고 계정/저장 상태도 소유하지 않는다.
- `Assets/01.Scripts/Progression/TraitTree.cs`: `PreviewModifiers` 읽기 전용 API 추가. 기존 `ApplyEffect`를 재사용해 현재/다음 랭크를 계산하며 기존 API/전투 효과/밸런스는 변경하지 않는다.
- `UITraitOverlayView.cs`: 다음 효과 텍스트 참조와 비교 출력. `UITraitFrameView.cs`: 미해금 이름 색상 읽기 프로퍼티. `UITraitProgressionController.cs`: 선택/랭크 변경 때 비교 문구 갱신, 환급 안내의 명시적 줄바꿈.
- `Assets/00.Project/Editor/LobbyTraitTreeBuilder.cs`: 대상 프리팹만 연결하는 메뉴 `Tools/OZGL2/Lobby/Apply Trait Detail Art And Effect Comparison`. TraitsScreen 변경은 한 Undo 그룹이며 메인 씬을 저장하지 않는다. 이미지 Import 설정은 별도의 에셋 설정으로 Undo 그룹 밖이다.
- `Assets/00.Project/Editor/LobbyTraitTreeValidation.cs`: 모든 특성/랭크의 실제 효과 비교, 설명 텍스트 넘침, 다음 효과 색, 전용 Sprite 비율 검증 추가.
- `Assets/06.UI/LobbyMutedPreview/Overlays/DetailArt_v2/`: PNG 6개, Unity가 생성한 Meta, 검증 JSON, 리소스 README.
- `Tools/Art/BuildTraitDetailAssets.inputs.json`, `BuildTraitDetailAssets.cjs`, `Sources/TraitDetailRefresh/`: built-in image_gen 프롬프트, 승인된 투명 배경/빈 중앙 길이 맞춤/등비율 축소/대칭 보정, 생성 원본 보관.

### 최종 검증

- Unity 6000.3.22f1 컴파일 완료. 최종 Play Mode Console **오류 0 / 경고 0**.
- 모델 **1,474개**, 모든 40개 특성의 전 랭크 효과 **4,714개**, Play Mode UI **3,073개** 단언 통과. 기존 LP/초기화/선행 조건/줌/드래그도 다시 확인했다.
- 현재/다음 효과 및 특화 장문을 모든 랭크에서 TMP 넘침 없이 표시함을 확인했다. 원래 특성 아이콘 40개 중앙/안쪽 배치 검사도 통과했다.
- 검증은 비영속 모델을 사용하며 실제 PlayerPrefs 불변을 검사한다. 화면 캡처용 LP와 랭크도 별도 비영속 모델이고 끝난 뒤 원래 계정 모델을 다시 연결했다. 실제 계정 초기화/재배분은 하지 않았다.
- 캡처 도구에서 한 차례 `ScreenshotUtility.cs`의 재귀 PlayerLoop 오류가 발생했다. 프로젝트/패키지를 수정하지 않고 새 Play Mode에서 Unity 기본 `ScreenCapture`로 다시 캡처했다. 이 최종 실행의 오류/경고는 0개다.
- 확인 캡처: `Temp/TraitTreeValidation/traits-detail-comparison-final.png`, `traits-detail-cap-final.png`, `traits-reset-dialog-final.png`. Temp 증빙은 커밋 대상이 아니다.

### Inspector / 팀원 확인

- `SelectedTraitDetail/NextDescription` → `UITraitOverlayView._detailNextDescription` 연결 완료. 기존 Description, Divider, +/- 참조 및 클릭 동작 유지.
- 뒤로/하단 2개/확인창 2개/+/-의 전용 Sprite, `Simple`, `Preserve Aspect`를 확인한다. PNG 크기와 버튼 비율은 검증 완료다.
- 다른 화면/씬, Input System, LayerMask/Tag, Collider/Rigidbody, Animator, ProjectSettings, Packages 변경 없음. 추가 수동 연결은 필요 없다.
- 공유 폰트 복원 후 팀원 PC와 다른 해상도에서 가독성을 확인한다. 특성 0→1→2→만렙과 다운 시 효과/색상/버튼 갱신, 초기화 확인·취소를 수동 플레이로도 확인한다.
- 코드/문서 `git diff --check` 통과. Prefab의 Unity 자동 직렬화 빈 문자열 뒤 공백은 남겨 두었으며 YAML을 직접 편집하지 않았다.
- 충돌 주의 대상은 공유 `Canvas_LobbyOverlays.prefab`. 기존 폰트 복구/이전 특성 작업과 구분해 커밋 범위를 검토한다. Scene 디스크 변경 없음, Prefab 변경 있음, 신규 Meta 있음. 원본 아트 변경 없음. 새 원본 6종 합계 약 4.2MB, 단일 50MB 초과 파일 없음.
- `imagegen` 스킬은 내장 이미지 생성과 원본 보존/프로젝트 안 최종 저장에 사용했다. Scene Change Plan / Unity Editor Tool / AI Review Log 절차에 따라 승인 범위와 Undo, 검증 내역을 기록했다. 커밋/push는 실행하지 않았다.

## 데몬킹 검은 배경 보완 (2026-09-18)

- 원인: 대칭 프레임 Sprite의 중앙이 투명하여 앞서 배치한 연결선/마법진이 마왕 아이콘 뒤로 비쳤다.
- 추가 리소스: `Assets/06.UI/LobbyMutedPreview/Overlays/DetailArt_v2/Background_DemonKing_Black.png` (256×256) 및 Unity 생성 Meta. 프레임 외곽 경계 사이를 채운 단색 검정 마스크다. 중앙과 이중 레일의 투명 틈까지 가리며 외부는 투명하다. 첫 중앙 영역 마스크는 레일 틈에 선이 남아 최종본에서는 외곽 범위로 보정했다.
- `Canvas_LobbyOverlays.prefab/TraitsScreen/TraitTreeViewport/TraitTreeContent/Nodes/DemonKingBackground` Image 하나 추가. 연결선 위 / 프레임·마왕 아이콘 아래, 프레임과 동일한 210×210 Rect, `Raycast Target=false`로 입력 유지.
- `LobbyTraitTreeBuilder.cs`에 `Tools/OZGL2/Lobby/Apply Demon King Black Backing` 메뉴 추가. 스프라이트 생성/정합과 프리팹 연결을 재현한다. 프리팹 변경 한 Undo 그룹 지원. PNG 생성/Import 설정은 Undo 그룹 밖이다.
- 단색 마스크 작업이므로 이미지 생성 모델을 사용하지 않았다. 원본 프레임 PNG 및 별도 마왕 아이콘은 그대로 보존했다.
- 검증: 중앙 완전 불투명, 외부 모서리 투명, 좌우/상하 픽셀 차이 0. 메뉴·설정·연결선·프레임·마왕 아이콘의 적용 전후 직렬화 내용 동일.
- Play Mode UI 검증 3,073개 통과(클릭/닫기/LP/초기화/줌/드래그), 계정 PlayerPrefs 불변 확인. 배경이 Graphic Raycast를 받지 않고 올바른 그리기 순서인지 검사했다.
- 프리팹만 저장, 메인 씬의 미저장 변경 보존. Runtime 코드/Scene 파일/ProjectSettings/Packages 변경 없음. Inspector 수동 연결 불필요, 추가 Layer/Tag/Physics/Input/Animator 설정 없음.
- 캡처: `Temp/TraitTreeValidation/demon-king-black-backing-final.png`. 팀원은 플레이 화면에서 프레임 안 연결선 가림과 확대/축소 시 테두리 틈을 확인한다.
- 최종 Play Mode Console 오류 0개 / 경고 0개. 플레이를 종료하고 원래 편집 상태로 돌아왔다.
- 중간 캡처 세션에는 대상 화면이 비활성화되어 캡처 호출만 실패했고, 출처/스택 없는 `Unknown Behaviour` Missing Script 경고 10개가 기록되었다. 현재 씬/대상 프리팹/로드된 모든 GameObject를 검사한 결과 Missing Script는 0개였다. 패키지나 다른 에셋을 수정하지 않고 새 Play Mode에서 3,073개 검증을 재실행했으며 오류/경고 0개로 재현되지 않았다. 해당 일시 경고의 발생 원인은 확정하지 않았다.
- 공유 프리팹 충돌에 주의한다. 이 보완에서 커밋/push는 하지 않았다.

## 하트 시각 중심 / 설명창 상단 정렬 (2026-09-18)

### 적용 범위와 원인

- 사용자 승인 범위인 `Canvas_LobbyOverlays.prefab/TraitsScreen`의 `Mon_B2`(불굴의 살) 아이콘과 `SelectedTraitDetail` 배치만 변경했다.
- 하트는 Rect와 알파 외곽 경계의 중앙은 맞았지만 윗부분 면적이 넓어 밝은 실루엣의 시각 중심이 위에 있었다. 원본 PNG/Pivot을 수정하지 않고 하트에만 밝은 픽셀의 세로 무게중심 보정을 적용했다.
- 하트 Rect의 Y는 약 -5.23, 크기는 약 146.29×146.29로 안쪽 마름모 범위에 맞췄다. 나머지 39개 특성 아이콘은 기존 배치와 크기를 유지한다.
- 설명창 상단은 `Icon → Type → Name → Level` 순서다. 아이콘 영역은 66×66에서 128×128로 확대하고 비율을 유지했다. 현재/다음 설명, 안내, 비용, +/- 버튼 간격은 확대된 헤더와 겹치지 않도록 조정했다. 내용과 특성 기능은 그대로다.

### 변경 파일 / 재현 방법

- `Assets/00.Project/Editor/LobbyTraitTreeBuilder.cs`: `Tools/OZGL2/Lobby/Align Trait Heart And Enlarge Detail Header` 메뉴. 대상 프리팹의 특성 영역을 하나의 Undo 그룹으로 변경한다. 기존 설명창 아트 적용 메뉴도 같은 헤더 배치를 재사용한다. Runtime 의존성은 추가하지 않았다.
- `Assets/00.Project/Editor/LobbyTraitTreeValidation.cs`: 하트의 시각 중심, 다른 아이콘의 기존 중앙, 전체 40개의 프레임 내부 배치, 설명창 상단 순서·겹침·아이콘 크기 검증.
- `Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab`: Unity Prefab Mode에서 위 배치를 적용하고 저장. YAML 직접 편집 없음.
- 이 문서: 변경 이유, 검증 결과, 보존 범위 기록. 새 이미지/Meta, Runtime 코드, ProjectSettings, Packages 변경 없음.

### 검증 / 보존 범위

- Unity 컴파일 완료. Play Mode UI **3,078개 단언 통과**. 현재/다음 설명과 특화 장문 넘침, 기존 LP/선행 조건/초기화/클릭/줌/드래그 동작을 함께 검사했다.
- 아이콘 40개 모두 프레임 안쪽 범위 검사 통과. 39개는 기존 바운딩 중앙, 하트 1개는 시각 중심 검사 통과.
- 메뉴·설정창과 다른 특성 39개의 적용 전후 직렬화 해시 비교 **41개 모두 동일**. 기존 Inspector 참조 유지, 별도 수동 연결 필요 없음. Layer/Tag/Physics/Animator/Input 설정 변경 없음.
- 이번 작업 시작 시 이미 사용자가 저장한 씬 조정(마왕 크기, 설명 정렬 등)은 그대로 보존했다. 씬을 저장하거나 되돌리지 않았으며 작업 전후 파일 SHA256은 `0253C683B03FB14BFC6E1CAF7846AB46C822CD9DB80304BB46FE8191AA1213F3`로 동일하다.
- 자동 검증은 비영속 모델을 사용하고 실제 PlayerPrefs 불변을 검사했다. 시각 확인은 실제 계정에서 특성 선택/트리 이동만 수행했으며 LP/레벨은 변경하지 않았다.
- 첫 Play Mode에는 출처/스택 없는 `Unknown Behaviour` 스크립트 누락 경고 8개가 기록됐으나 로드된 모든 GameObject 검사에서 누락은 0개였다. 새 Play Mode에서 3,078개 검증을 재실행한 최종 결과는 **오류 0 / 경고 0 / 누락 스크립트 0**. 일시 경고의 원인은 확정하지 않았으며 다른 에셋은 수정하지 않았다.
- 실제 화면 캡처: `Temp/TraitTreeValidation/trait-heart-header-final.png` (커밋 대상 제외). 팀원은 불굴의 살 중앙 정렬, 선택 전환 시 확대 아이콘과 등급/이름/레벨 순서, 작은 Game View에서 가독성을 확인한다.
- 공유 프리팹 충돌에 주의한다. Scene Change Plan / Unity Editor Tool / AI Review Log 절차에 따라 승인된 배치만 수정했다. 기존 작업 변경은 보존하고 커밋/push는 하지 않았다.

## 이전 작업 Git 검토 포인트

- 충돌 위험: `Canvas_LobbyOverlays.prefab`의 노드 추가로 직렬화 diff가 크다. YAML 직접 수정/충돌 해결 대신 Unity에서 검토한다.
- Scene 변경 없음, Prefab 변경 있음, 신규 Meta 있음, ProjectSettings/Packages 변경 없음.
- 만렙 프레임 작업 중 발견한 메인 씬의 미저장 상태는 그대로 보존했다. 프리팹만 저장했으며 메인 씬 저장/되돌리기를 수행하지 않았다.
- 런타임 코드에 UnityEditor 의존성을 추가하지 않았다. 검증/생성 코드는 Editor 폴더에 분리했다.
- 기존 작업의 `LobbyMutedPreviewFontApplier.cs`, `LobbyOverlayPreviewBuilder.cs`, `Docs/FontAssetSharing.md` 폰트 경로 복구 변경도 작업 트리에 남아 있다. 이번 특성 구현과 구분하여 커밋 범위를 검토한다.
- 이 작업에서 커밋 또는 push는 수행하지 않았다.
