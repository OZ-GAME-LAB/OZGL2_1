# 실제 증강 선택 연결

## 구성

Builds/InGame의 기존 Bootstrap에 `InGameAugmentRewards`와 `InGameAugmentDummyView`를 추가하고 `_augmentProvider`를 연결했다. 씬 변경은 Unity Editor API로 저장했다. 기존 `RealAugmentRewards` 및 샌드박스는 변경하지 않았다.

Bootstrap은 BeginRun 뒤에 현재 RealSynergySync를 명시적으로 바인딩하고, 종료 시 선택 요청을 먼저 취소한다. 제공자는 당시 SkillManager를 실행 소유권으로 사용한다. 이전 실행의 선택으로 새 실행의 증강을 변경할 수 없다.

`InGameAugmentSelection`은 요청과 완료만 관리한다. 적용은 팀원의 `AugmentRun.Pick`이 담당하며 기존 Changed 이벤트를 통해 RealSynergySync가 배율을 갱신한다. 원본 SO는 수정하지 않는다.

## UI 연결

- 실제 UI로 교체할 때 `InGameAugmentDummyView`만 제거/비활성화한다. 제공자는 유지한다.
- IsPending이 true일 때 Candidates를 표시하고, 표시 당시 RequestId를 함께 보관한다.
- Unity 메인 스레드에서 `TrySelect(requestId, candidate)`를 호출한다. false이면 완료로 표시하지 않는다.
- 후보 밖 선택, 이전 요청 클릭, 중복 클릭을 거부한다. 적용 성공 후에만 StageManager의 대기가 완료된다.
- 취소·제공자 비활성화·파괴·Bootstrap 종료 시 후보가 사라지고 대기가 취소된다.
- 적용 예외가 나면 같은 실행에서 재시도하지 않는다. 부분 적용 여부를 확정할 수 없으므로 새 실행이 필요하다.
- 후보 1~2개면 있는 수만 표시한다. 0개이면 자동 스킵하지 않고 오류로 중단한다.

## 추첨 규칙

기존 `Draw3(int milestoneStage)`는 유지한다. 새 오버로드는 실버/골드/플래티넘 가중치를 직접 받고, 매 후보마다 등급을 추첨한 뒤 기존 카드 weight로 카드를 뽑는다. 후보 중복과 최대 스택에 도달한 카드는 제외한다. 소진된 등급은 남은 양수 가중치 등급에 재분배하며, 가중치 0인 등급으로 대체하지 않는다.

현재 일반/하드 SO는 단일 등급을 지정한다. SO 수치와 보스 주기는 변경하지 않았다. 최종 라운드는 기존 StageManager 규칙대로 증강을 생략한다.

## 실제 효과 연결 범위

SO의 connStatus 문구 대신 실제 효과 소비 코드를 기준으로 `InGameAugmentAvailability`에서 후보를 제한한다. 현재 30개 중 17개가 대상이다. 스킬 피해/쿨다운/범위/버프 지속/치명타/메아리/피격 감속, 몬스터 체력/공격/공속, 용사 공격 약화, XP 배율 등 연결된 효과를 사용한다.

미연결 후보 13개: cdkill_g, cdkill_p, eco_sp, explode_p, frost_g, hero_vuln, revive_p, shield_s, util_heal, util_reset, util_xp_p, util_xp_s, xpalive_p.

즉시 효과, 그리드 확장, 사망/처치 특수 효과 등은 이 브랜치에서 새로 구현하지 않는다. 담당자가 실제 실행 경로를 연결한 뒤 해당 effect의 허용 여부와 회귀 테스트를 함께 갱신한다. 기존 SO/효과 계산과 샌드박스 후보 목록에는 영향이 없다.

유닛 점유 블록, 최종 UI, 계정 정산 및 밸런스 조정은 범위 밖이다.

## 검증

- `OZGL2/InGame/Verify Augment Selection`: 순수 선택·추첨·취소·적용 실패 계약.
- `OZGL2/InGame/Verify Augment Integration (Empty Play Scene)`: Bootstrap이 없는 Play 씬에서 실제 제공자와 StageManager 연결. 전투 승패는 수동 더미를 사용한다.
- 기존 `Verify Integration`, `Verify Skill Lifecycle (Empty Play Scene)`와 함께 회귀 확인한다.

증강 요청 영수증은 실행 중 메모리 범위다. 강제 종료 후 이어하기/증강 복구 저장은 제공하지 않는다. 스킬·전투 배율 자체의 밸런스 검증은 별도다.
