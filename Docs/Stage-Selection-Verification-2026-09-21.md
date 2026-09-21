# 스테이지 선택 연결 검증

브랜치: `feat/stage-selection/44`, Unity 6000.3.22f1.

## 변경 파일

- 신규 `StageCatalogSO`, `StageLaunchSession`, `StageLaunchRuntime`: ID 조회, 요청의 1회 소비와 취소, 씬 경계 및 Editor 직접 실행 구분.
- 신규 `StageSelectionController`, `StageSelectionDummyView`: 실제 UI가 재사용할 진입점과 교체 가능한 더미 표시.
- `InGamePrototypeBootstrap`, `InGamePrototypeConfigSO`: 선택 데이터 검증·고정, 기존 실행기에 전달, 같은 스테이지 재도전.
- `UISceneNavigator`: 기존 LoadScene을 유지하며 TryLoadScene의 접수 성공/오류 반환 추가.
- `StageSelectionSetup`, `StageSelectionVerification`: Unity API 설정 도구와 계약 검증.
- 신규 카탈로그·StageSelectionPrototype 프리팹, 기존 StageChoice 씬·InGame 설정·빌드 씬 목록 연결.
- 원본 밸런스 SO, 개인 테스트 씬, Lobby·InGame 씬 파일, 외부 에셋은 수정하지 않았다.

## 결과

| 항목 | 결과 |
|---|---|
| 계약 검증 | 보통 30/어려움 50 조회, 용사 풀 호환성, 미등록 용사 거부, 누락/중복/알 수 없는 ID 거부 PASS |
| 요청 수명주기 | 중복 요청 거부, 1회 소비, 오래된 취소 격리, 잘못된 씬 및 선택 초기화 PASS |
| StageChoice → 보통 | 실제 씬 이동 후 `stage_normal_30`, 총 30라운드, 기본 배치 1기, 첫 보상 단계 도달 |
| 재도전 | 같은 StageId, 다른 RunId, 1라운드·기본 유닛 1기·보관함 0개. 중복 재도전 거부 |
| StageChoice → 어려움 | 실제 씬 이동 후 `stage_hard_50`, 총 50라운드, 첫 보상 단계 도달 |
| 저장 데이터 | FileStageProgressStore에서 읽은 StageId가 `stage_hard_50`과 일치 |
| 이동 실패 | 없는 씬 요청 시 false/오류 반환, Pending 없음, 잠금 해제, 다음 정상 요청 성공 |
| 잘못된 요청 직접 전달 | InGame이 오류를 보고하고 Stage 미생성·스킬 차단. 재시작해도 테스트 Stage로 대체하지 않음 |
| 선택 취소 | 더미의 로비 복귀 API 성공, Lobby 도착, Pending 없음 |
| 선택 없이 씬 이동 | 새 Play의 Lobby에서 InGame으로 직접 로드해도 시작 거부 |
| Editor 직접 Play | InGame을 시작 씬으로 Play하면 명시적 허용 설정에 따라 `stage_ingame_prototype_test` 시작 |
| 기존 코어루프 | 전체 더미 스테이지·패배·초기 배치·보상·확장·취소·재시작 검증 PASS |
| 기존 카메라 | 전환·프레이밍·배치·취소·실패 복구 검증 PASS |
| 기존 스킬 수명주기 | 입력 차단·지연 피해/장판/조준 정리·이전 실행 격리·설정 실패 검증 PASS |
| StageChoice 참조 | Missing Script 및 끊어진 직렬화 참조 0개 |
| 컴파일/Console | 오류·경고 0개 |
| 변경 파일 | diff 공백 오류 및 신규 에셋 meta 누락 없음 |

더미 화면은 Game 화면 캡처로 확인했고, 전환 테스트는 더미 버튼이 호출하는 Controller API를 통해 실행했다. 팀원 최종 UI의 실제 버튼 연결을 검증한 것은 아니다.

## 한계와 다음 작업

- 실제 전투는 각 스테이지 첫 보상 진입 및 재도전을 확인했다. 실제 전투 전체 30/50라운드 완주 및 독립 Player 빌드는 미검증이다.
- 기존 빌드 시작 순서는 유지했다. UI 담당자가 Lobby에서 StageChoice로 이동하고 StartStage(ID)를 호출하도록 연결해야 한다.
- 정상 종료 후 자동 로비 복귀는 유지한다. 결과 UI의 재도전 선택, 스테이지 해금, 실제 보상/증강 UI는 이번 범위가 아니다.
- 테스트 중 실제 코어루프가 작성한 실행 기록과 계정 진행 데이터가 로컬에 남을 수 있다.
- 테스트용 임시 객체는 Play 종료로 정리했고, 씬 편집 상태는 Lobby로 복구했다. 커밋/푸시는 수행하지 않았다.

UI 연결 방법: `Stage-Selection-TeamGuide.md`.
