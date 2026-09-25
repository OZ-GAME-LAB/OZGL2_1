# PR #54 통합 검증

대상: feat/core-loop-rewards/49 → dev_2. 기준 dev_2 커밋 `7bcac6d` (PR #50, #52, #53 포함).

## 통합 내용

- 일반 보상 기능 커밋 `929f39e`를 보존하고 최신 dev_2를 `19c0a11`로 통합했다. Git 충돌 없음.
- 용사 모델링 변형에 따른 중복 hero ID 허용과 일반 보상 설정 검증을 함께 유지했다.
- 보상 UnitDefinition 생성은 원본 GridUnitDataSO의 2·3성 점유 모양을 보존한다. 지급 성급/발판은 1성/1×1을 유지한다.
- 기존 합성 검증은 작은 테스트 발판에 3성 유닛을 배치할 수 있다고 가정해 실패했다. 제품 합성 규칙을 변경하지 않고 테스트 바닥·유효 위치를 갱신하고 배치 오류 차단/복구 검사를 추가했다.

## 결과

- Unity 6000.3.22f1 컴파일 성공, 최종 Console 오류/경고 0.
- GeneralRewardVerification PASS: 가중치/해금 필터, 최대 영역의 세 번째 유닛, 보관함 취소·동시 지급, 후보 유지, 요청 취소·중복 차단.
- InGameVerification PASS: 새 보상 제공자를 사용한 전체 단계 진행, 패배, 최종 보상 생략, 확장 강제, 보관함, 취소/새 실행. 전투는 수동 더미.
- GridFusionVerification PASS: 성급별 점유, 잘못된 배치의 전투/스킵 차단, 회전 고스트, 재배치 복구, 성급별 배치 스냅샷.
- GridFusionPlayVerification PASS: 실제 포인터 이벤트의 보드/보관함 합성, 잘못된 드롭, 3성 모델과 위치의 전투 반영, 원본 SO 보존.
- 실제 보상 SO 6종을 각각 3성까지 합성하여 유효한 위치/회전으로 재배치하고 전투 확정본에 성급별 ShapeId가 전달되는지 추가 확인: 전부 PASS.
- InGameAugmentPlayVerification PASS: 최신 실제 증강 제공자와 일반 보상 → 증강 → 다음 준비/전투 연결, 중복 적용 차단·종료/취소.
- 실제 StageChoice에서 일반 난이도 선택 → InGame 1라운드 실제 승리 → 후보 3개 → M_HEL_01 선택 → 유닛/발판 2개 지급 → 2라운드 준비 확인. Bootstrap.Error 없음, Missing Script 0.
- Play 검증은 MawangXpBridge의 성장 객체를 테스트 중에만 비영속 인스턴스로 교체하여 계정 레벨/XP/LP 저장값을 변경하지 않았다. 제품 코드 변경 없음. 테스트 후 Play 종료 및 임시 에디터 설정 복원.

## 범위와 별도 확인사항

- GitHub Actions 검사는 PR 조회 시 등록된 항목이 없었다. 위 Unity 로컬 검증을 수행했다.
- 실제 30/50라운드 전투 완주, Player 빌드, 모든 증강 효과의 개별 밸런스 검증은 수행하지 않았다.
- 계정 유닛 해금 저장소 연결과 최종 보상 가중치는 후속 작업이다.
- 기존 dev_2에는 `RealSynergySync.BeginRun` → `MawangLevel.ResetForNewRun`으로 레벨/XP를 초기화하고 LP만 유지하는 변경이 포함되어 있다. 이전 XP 영구 유지 합의와 달라 팀 확인이 필요하다. PR #54가 도입한 변경은 아니며 이번 보상 PR에서는 성장 규칙을 수정하지 않았다.
