# 결과·재도전 검증 — 2026-09-25

## 변경

- 정상 도전 종료 후 자동 로비 복귀를 결과 선택 대기로 교체.
- 읽기 전용 승패/라운드/종료 레벨/현재 레벨 XP와 RunId 기반 선택 잠금 추가.
- 재도전·StageChoice·Lobby 이동 및 이동 요청 실패 복구.
- 새 도전 초기화 전에 설정/선택/카메라 설정 검증.
- Config의 StageChoice 경로는 Unity SerializedObject API로 설정. 씬·프리팹 원문 수정 없음.

## 통과한 검사

- RunResultVerification: 승리/패배 스냅샷, 미정산 거부, 오래된/중복 선택 차단, 이동 실패 후 재시도.
- InGameVerification: 전체 설정 라운드(더미 전투), 패배, 첫 자동 배치, 보관함 초과·취소·버리고 받기, 확장 강제, 종료 라운드 보상 생략.
- GeneralRewardVerification: 가중치·해금 필터·후보 부족, 최대 확장 대체, 동시 지급 및 중복/취소 차단.
- StageSelectionVerification: 일반30/하드50, 데이터 참조·선택 요청 소유권.
- CoreLoopFailureVerification(Play): 초기 오류 재시도, 정리 실패 재시도 차단, 승패 저장, 준비·종료 타임아웃.

## 실제 Play 확인

- StageChoice → 실제 InGame, 1라운드 승리 → 보상 선택 → 2라운드 준비 확인.
- 2라운드에서 테스트용 치명 피해로 패배 유도. 결과 FAILED, 도달2/클리어1, 종료 레벨3·현재 XP15 표시. 결과 상태에서 Host0/Unit0, 그리드 종료 완료, Error 없음.
- 잘못된 StageChoice 경로를 런타임 Config 복제본에 설정: 이동 거부, 결과 유지, 재선택 가능. 원본 SO 변경 없음.
- 재도전 직후 레벨1/XP0, LP3 유지. 새 RunId, 첫 라운드 기본 유닛1기, 중복 재도전·이전 결과 이동 차단.
- 원본 SO를 변경하지 않은 1라운드 StageDefinition으로 실제 전투 최종 승리 확인. 결과 → StageChoice 이동, 중복 이동 거부.
- 새로운 도전 패배 → Lobby 실제 씬 이동 확인.
- Game View 결과 화면 캡처로 텍스트와 세 버튼 표시 확인.

테스트용 MawangLevel(false)를 사용하여 사용자 레벨·XP·LP 저장을 변경하지 않았다. Play 종료 후 원래 StageChoice 편집 상태, playModeStartScene 및 runInBackground를 복구했다.

## 범위와 한계

실제 전투 30/50라운드 완주, Player 실행 빌드, 최종 UI는 이번에 검증하지 않았다. 정산은 기존 더미 영수증 방식이며 실제 계정 보상/최초 클리어/해금 지급 기능은 추가하지 않았다. 최종 승리 실행 검증에는 단축한 테스트 도전을 사용했다.
