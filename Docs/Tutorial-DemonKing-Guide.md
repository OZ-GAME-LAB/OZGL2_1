# 마왕 튜토리얼 시스템 가이드

마왕(DemonKing)이 화면 아래에서 직접 말해 주는 튜토리얼·도움말 시스템입니다. (InGame_2 씬 기준)

## 구성
| 파일 | 역할 |
|---|---|
| `Assets/01.Scripts/Tutorial/TutorialLibrary.cs` | **마왕의 대사 모음.** 문장만 고치면 됩니다. |
| `Assets/01.Scripts/Tutorial/TutorialDirector.cs` | 대화창·강조·자동 트리거·도움말(?) 담당. 씬의 `ReleaseMode` 오브젝트에 붙어 있음 |
| `Assets/01.Scripts/Tutorial/DemonKingPortrait.cs` | DemonKing 프리팹을 idle 애니메이션으로 그려 대화창에 보여 줌(전투 스크립트 없음) |
| `Assets/01.Scripts/Tutorial/TutorialStore.cs` | 본 기록 저장(PlayerPrefs). 처음 한 번만 자동 재생 |

## 자동으로 나오는 때
- 첫 배치 단계 → `intro` / 첫 전투 → `battle`(게임 일시정지) / 첫 카드 보상 → `reward` / 첫 증강 → `augment` / 첫 보스 웨이브 → `boss`
- 화면 오른쪽 위 `?` 단추로 언제든 주제를 골라 다시 볼 수 있음. 로비 디버그 패널(F8)의 "튜토리얼 초기화"로 본 기록 삭제.

## 대사/단계 추가 방법
`TutorialLibrary.cs`에 `new TutorialStep("대사", "강조 대상 열쇠", 몸짓여부)`를 추가합니다.
강조 열쇠: `wave` `grid` `hand` `synergy` `start` `skills` `xp` `currency` `reroll` `king` (비우면 강조 없이 말만 함)
새 열쇠가 필요하면 `TutorialDirector.TryGetTarget`에 화면 영역을 찾는 줄을 추가합니다.

## 규칙
- UI 프리팹과 팀 코드는 수정하지 않고, 실행 중에 별도 캔버스(정렬 90)에 그립니다.
