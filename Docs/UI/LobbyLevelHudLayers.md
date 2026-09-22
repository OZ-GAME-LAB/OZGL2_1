# Gothic Metal 레벨 HUD

## 현재 상태

- 대상은 `UI_Lobby_MutedPreview.unity > Canvas_Lobby > AccountStatus`뿐이다.
- 리소스 5종과 표시 코드 2종을 연결하고 사용자 범위 승인 후 Unity API로 Scene을 저장했다. Play Mode 확인 후 Edit Mode로 복귀했으며 미저장 Scene 변경은 없다.
- 사용자가 수정 중인 `Canvas_UnitCodex.prefab`은 수정하지 않았다. 로비의 `AccountStatus` 외부 컴포넌트 5,711개는 변경 전후 직렬화 값이 동일하다.

## 리소스 분리

`Assets/06.UI/LobbyMutedPreview/LevelHud_v1/` 아래에 원본 `Sprites/*.png`와 표시 영역을 지정한 `Styles/*.asset`을 둔다. Unity Image에는 Styles의 Sprite를 사용한다.

| 이름 | 역할 |
| --- | --- |
| LevelFrame | 양끝 장식과 레벨 명판을 포함한 빈 몸체/프레임. 실제 투명 구멍, 9-slice 경계 설정 |
| LevelTrack | 아직 채워지지 않은 경험치 영역의 어두운 바탕 |
| LevelFill | 문양이 새겨진 적색 경험치 띠. 프레임/눈금/끝 장식/문구 없음 |
| LevelTick | 하단 눈금 한 개. 개수에 맞춰 독립적으로 반복 |
| LevelFillCap | 진행 경계를 따라 이동하는 끝 장식 한 개 |

레벨 글자는 PNG에 포함하지 않고 기존 TMP를 사용한다. 프레임과 Fill을 늘려 하나로 취급하지 않는다. 원본 PNG를 자르거나 덮어쓰지 않고 Sprite Rect만 지정했으며, .meta와 Sprite 에셋은 Unity API로 생성했다.

## 클래스 책임

- `UILobbyLevelHudView`: 레벨 글자, 가로 Fill, 끝 장식 위치만 갱신한다. Preview Level / Preview Experience / Preview Required Experience는 계정 연결 전 디자인 확인용이다. 외부에서 전달한 값은 별도 비직렬화 필드에 보관하며 Inspector 미리보기 설정을 덮어쓰지 않는다.
- `UIProgressTickGraphic`: 한 개의 Sprite를 등간격 메시로 반복한다. Segment Count가 5이면 20/40/60/80% 경계에 4개를 표시한다. 1~64구간, Tick Height로 크기 변경. 업적/계정 데이터 의존성 없이 다른 막대에도 사용할 수 있다.

성장 데이터와 UI 표시의 책임을 분리하기 위해 두 클래스로 구성했다. PlayerPrefs, 실제 경험치 획득, 레벨업, 특성 포인트, 계정 저장 규칙은 수정하지 않았다.

## 적용된 계층과 Inspector 설정

```text
Canvas_Lobby
└─ AccountStatus                 UILobbyLevelHudView / 기존 Image 비활성
   ├─ ExperienceTrack            LevelTrack Sprite
   ├─ ExperienceFill             LevelFill Sprite / Horizontal Filled
   │  └─ FillCap                 LevelFillCap Sprite
   ├─ LevelPlaqueBackground      레벨 명판의 검은 바탕
   ├─ LevelFrame                 LevelFrame Sprite / Sliced
   ├─ ExperienceTicks            UIProgressTickGraphic / LevelTick Sprite
   └─ Level                      기존 TMP
```

기존 `DemonIcon_Placeholder`와 `XP_Segment_0`~`XP_Segment_7`은 비활성 상태로 보존했다.

| 조절 대상 | Inspector 위치 / 값 |
| --- | --- |
| 미리보기 레벨·경험치 | AccountStatus → UILobbyLevelHudView → Preview Level / Preview Experience / Preview Required Experience. 기본값 20 / 60 / 100 |
| 눈금 개수 | ExperienceTicks → UIProgressTickGraphic → Segment Count. 기본 5구간 = 내부 눈금 4개 |
| 눈금 크기 | ExperienceTicks → Tick Height. 기본 높이 16 |
| 프레임 몸체 | LevelFrame → Image → Source Image. Styles/LevelFrame 사용 |
| Fill·빈 바탕·끝 장식 | ExperienceFill / ExperienceTrack / ExperienceFill/FillCap의 Image |

- 기존 `AccountStatus` 루트의 위치/크기는 유지한다. 원본 합성 Image와 XP_Segment 오브젝트는 삭제 대신 비활성 보존한다.
- 기존 `Level` TMP의 폰트·머티리얼·색상은 유지하고 명판에 맞춰 글자 크기 26, 영역 77×34로 조절했다. View의 Level Text에 연결했다.
- Experience Fill에 Styles/LevelFill Sprite를 지정하고 View의 Experience Fill에 연결했다.
- FillCap은 ExperienceFill의 자식이며 View의 Fill Cap에 연결했다. 부모 영역의 진행 비율 위치에 Anchor를 배치한다.
- 프레임은 Image Sliced, Pixels Per Unit Multiplier = 2096 / 660이며 기본 표시 크기는 660×76.83이다. Track과 Fill은 왼쪽 여백 178, 오른쪽 여백 82, 높이 30으로 같은 영역을 사용한다.
- ExperienceTicks는 Fill과 같은 좌우 여백을 사용한다. 루트 너비를 변경하면 Fill·Track·눈금의 폭이 함께 조절되며 프레임 양끝 장식은 9-slice로 보존된다.
- 모든 활성 장식과 레벨 텍스트는 Raycast Target이 꺼져 있다.
- 다른 Overlay 화면의 AccountStatus 사본은 이번 범위에서 변경하지 않는다.
- Unity Undo 그룹 `Apply Gothic Metal Lobby Level HUD`로 변경을 묶었다. 별도 Editor Script / MenuItem은 추가하지 않았다.

## 외부 계정 시스템 연결 예시

```csharp
// 현재 레벨 안에서 모은 XP / 다음 레벨 필요 XP. 총 누적 XP를 넘기지 않는다.
levelHud.SetProgress(account.Level, account.Xp, account.XpToNext);
```

외부 제어부가 초기 표시와 계정 XP 변경 이벤트에 맞춰 호출한다. 표시 컴포넌트는 계정 객체를 새로 만들거나 저장하지 않는다. 필요한 이벤트의 구독/해제는 외부 제어부에서 함께 구현한다. `UsePreviewValues()`로 디자인 미리보기 값으로 복귀한다.

- 적색 채움 = `현재 레벨 XP / 필요 XP` (0~1 범위).
- 빈 바탕 = `1 - 채움 비율`, 즉 다음 레벨까지 남은 비율.
- 0%에서는 끝 장식을 숨긴다. 잘못된 분모/NaN/Infinity는 0%로 처리한다.
- 눈금은 비율 기준이므로 레벨마다 필요 XP가 달라도 막대 구성이 유지된다.

## 검증

- 새 Runtime 코드 컴파일 및 Console 오류/경고 0개.
- 원본 Scene을 변경하지 않는 임시 Preview Scene에서 206개 점검 통과: 0/1/25/60/99/100% 및 범위 초과·음수·NaN·Infinity, Fill/끝 장식/남은 비율, 0레벨 제한, 미리보기 복구, 눈금 1~64구간과 메시 수·위치, Sprite/참조 누락 처리.
- 실제 로비 Play Mode에서 추가 236개 점검 통과: 진행률/Fill/끝 장식/남은 비율, 잘못된 입력, 미리보기 복구, 눈금 개수·메시·위치, Inspector 참조, 기존 이미지 비활성, 로비 버튼 8개의 Raycast에서 HUD 간섭 없음.
- 실제 Game View 스크린샷에서 프레임·레벨 명판·Fill·끝 장식·눈금의 배치와 투명 영역을 확인했다. `Temp/LevelHud/LevelHud_01_Play.png`에 로컬 확인용 캡처가 있다.
- 너비 540 / 660 / 820에서 Fill·눈금 너비 일치, 좌우 여백 178 / 82 유지, 끝 장식 비율 위치를 확인하고 원래 660×104 루트로 복구했다.
- Play Mode 종료 후 Scene 저장 상태, Level 20 / 60%, 루트 RectTransform 보존, 외부 컴포넌트 5,711개 무변경을 재확인했다. Console Error/Warning 0개.
- LayerMask/Tag/Collider/Rigidbody/Animator/Input System 변경은 없다.
- 생성 방식과 전체 프롬프트: `Tools/Art/lobby_level_hud_prompts.md`.

## 변경 파일과 리뷰 범위

- `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`: AccountStatus의 분리 레이어 배치·참조 연결. 충돌 위험이 있는 Scene 파일이므로 해당 계층의 변경만 리뷰한다.
- `Assets/00.Project/01.Scripts/UI/UILobbyLevelHudView.cs`: 외부 수치를 받는 표시 API와 디자인 미리보기. 실제 계정 시스템 연동은 별도 작업이다.
- `Assets/00.Project/01.Scripts/UI/UIProgressTickGraphic.cs`: 구간 수에 따른 눈금 Sprite 반복 표시.
- `Assets/06.UI/LobbyMutedPreview/LevelHud_v1/`: 투명 PNG 5개, 표시용 Sprite 에셋 5개 및 Unity 생성 .meta.
- 이 문서 및 `Tools/Art/lobby_level_hud_prompts.md`: 편집 방법·검증 결과·원본 생성 기록.
- Prefab / ProjectSettings / Packages는 이번 작업으로 변경하지 않았다. 작업 전부터 수정되어 있던 Canvas_UnitCodex.prefab은 해시가 동일하다.
- `git diff --check`의 이번 Scene 경고는 Unity가 직렬화한 빈 `m_Name: ` 필드의 공백 5개다. Scene 텍스트 직접 수정 금지 규칙에 따라 수동 정리하지 않았다.
