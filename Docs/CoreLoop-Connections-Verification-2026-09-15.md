# 코어루프 연결 검증 — 2026-09-15

작업 브랜치: `feat/grid-asset-fusion/23`. 이 작업에서 SO/팀원 스킬 내부/전투 스탯 계산/투사체 내부/씬 파일은 수정하지 않았다. 기존 에셋·합성 작업 변경은 보존했다.

## 변경 파일

- `Assets/01.Scripts/Stage/StageContracts.cs`: 선택적 전투 생명주기 인터페이스.
- `Assets/01.Scripts/Stage/PooledStageBattle.cs`: 실제 배치 후 준비 연결, 외부 정리 후 풀 반환, 동시 전멸 패배 처리.
- `Assets/01.Scripts/Combat/RealStageBattleFactory.cs`: 연결부 선택 주입. 기존 호출 호환.
- `Assets/_Project/Scripts/Core/InGame/InGameCombatConnection.cs`: 외부 참여자 준비·허용·정리 순서 및 실패 수집.
- `Assets/_Project/Scripts/Core/InGame/InGameSynergyConnection.cs`: 표시와 독립적인 배치 집계.
- `Assets/_Project/Scripts/Core/InGame/InGameGridPresentation.cs`: 표시 책임만 유지.
- `Assets/_Project/Scripts/Core/InGame/InGamePrototypeBootstrap.cs`: 연결 조립, 실제 증강 제공자 선택, 기존 증강 ResetRun 호출, 정리 실패 수집.
- `Assets/_Project/Scripts/Core/InGame/Editor/CoreLoopConnectionVerification.cs`: 전용 검증.
- 팀 가이드 3종 및 이 검증 기록.

## 실행 결과

| 검사 | 결과 |
|---|---|
| InGameVerification | PASS: 전체 더미 전투 흐름, 패배, 최초 자동 배치, 보상 공간 부족·취소·폐기, 확장 강제, 오래된 요청, 재도전 |
| GridFusionVerification | PASS: 합성 원자성, 성급 유지, 보관 제한, 배치 스냅샷, 재진입, 단계 제한, 발판 유지 |
| CoreLoopConnectionVerification — 임시 빈 씬 Play | PASS: 모든 준비 완료 후 허용, 준비 실패 정리, 중복 참여자/정리 방지, 정리 실패 후 나머지 진행 |
| 실제 PooledStageBattle + 검증용 참여자 | PASS: 동시 전멸 패배 반환, 취소 시 정리, 외부 정리 시점의 활성 대여 유지, 정리 실패 후 풀 반환 |
| 실제 RealSynergySync + GridRunSession | PASS: 표시 컴포넌트 없이 2기 집계, 합성 후 1기, 연결 해제 시 0, 이후 이벤트 미수신. 검사 후 기존 집계 복원 |
| Unity 최종 Console | 오류 0 |
| InGame 씬 복귀 | Edit 모드, 저장되지 않은 변경 없음, Missing Script 0 |
| 변경 C#/문서 whitespace | git diff --check 통과 |

마지막 두 방어 수정(누락된 전투 문맥 거부, 종료 오류 보존)은 컴파일 확인했으며 해당 예외 경로를 별도로 재실행하지 않았다.

## 남은 검증/외부 연결

실제 스킬 입력 차단·장판 취소·투사체 회수·마왕 발사 위치 반영은 팀원 어댑터가 없어 미연결이다. 검증용 참여자로 호출 순서와 실패 처리를 확인했으며 실제 효과 정리가 완료된 것으로 간주하지 않는다.

실제 증강 UI 선택은 아직 더미이며, Bootstrap을 통한 실제 증강 선택/초기화 왕복 플레이 검증은 하지 않았다. 현재 초기화 대상은 RealSynergySync.Augments이다. 별도 저장 객체를 사용한다면 연결 규격을 맞춰야 한다.

SO 직업 오설정, 진영별 강화 적용, 회복 체력 계산은 변경하지 않았다. 전체 실제 전투와 스킬 조합의 무오류를 보장하는 검사는 아니다.
