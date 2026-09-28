# 첫 준비 및 상단 분산 스폰 (#60)

## 동작

- 스테이지 진입 및 새 실행은 기본 유닛/발판을 배치한 뒤 1라운드 준비에서 대기한다. 배치 확정본은 전투 시작 승인 시 생성한다.
- 첫 준비에서도 재배치, 보관, 회전/반전이 가능하다. 빈 배치 또는 배치 오류는 전투 시작을 막는다. 첫 준비의 Skip은 기존 규칙대로 비활성이다.
- 이후 라운드의 보상/준비/전투 순서 및 결과 선택 흐름은 유지한다.
- 용사는 최대 그리드 상단의 왼쪽/중앙/오른쪽 위치에서 순환 생성된다. 용사 종류가 바뀌어도 생성 순서를 이어가며 각 라운드에서는 왼쪽부터 다시 시작한다. 따라서 용사 1기는 왼쪽, 2기는 왼쪽과 중앙에서 생성된다.
- 기존 용사 수량, 간격, 스탯 및 적 탐색/추적/공격 로직은 변경하지 않았다. 상단에서 하단 마왕군을 향해 접근하므로 수직 직선 이동을 강제하지 않는다.

## 연결 및 설정

- `StageGridPreparation.PlaceInitial`은 이제 준비 상태까지 초기화한다. 이 메서드 호출만으로 전투가 시작되지 않는다. 테스트/외부 호출자는 필요 시 `GridRunSession.TryBeginBattle`을 명시적으로 호출해야 한다.
- `InGamePhasePresentation`과 `InGameDummyView`의 첫 준비 제외 조건을 제거했다. 기존 시작 요청 API를 그대로 사용한다.
- `InGamePrototypeConfigSO.CreateHeroSpawnPositions`는 그리드 원점/최대 크기/셀 크기에 따라 세 좌표를 계산한다.
- 설정 에셋의 단일 `_heroSpawnPosition`을 `_heroSpawnTopMargin`(기본 2칸), `_heroSpawnHorizontalInset`(기본 0.5칸)으로 교체했다. `HeroSpawnPosition` 프로퍼티는 중앙 좌표를 반환한다.
- `RealStageBattleFactory` / `PooledStageBattle`은 선택적 위치 목록을 전달받는다. 기존 단일 좌표 호출은 호환되며, 전달한 목록은 복사하여 외부 수정의 영향을 차단한다.
- 전투 카메라는 세 좌표를 포함해 화면 범위를 계산한다. 준비 카메라 범위는 유지한다.
- 새 설정값은 Unity SerializedObject로 저장했으며 씬/프리팹은 변경하지 않았다.

## 검증 (Unity 6000.3.22f1)

- InGameVerification: 첫 준비 수동 시작, 더미 전체 스테이지/패배, 보상/보관 초과/확장/취소/새 실행 검사 통과.
- GeneralRewardVerification: 일반 보상 추첨·대체·중복 지급 방지 검사 통과.
- InitialPreparationSpawnVerification: 첫 준비/빈 배치 시작 거부, 실제 풀을 통한 1/2/3/8기 순서 및 종류 간 연속성, 다음 라운드 순서 초기화, 입력 목록 복사, 취소 후 풀 반환 검사 통과.
- InGameCameraVerification: 첫 준비에서 시작 요청, 카메라 전환 잠금, 월드 배치, 카메라 오류 복구/취소 검사 통과.
- 실제 StageChoice → InGame: 1라운드 PREPARATION, 기본 유닛 1기, 활성 용사 0기 및 입력 가능 확인. 시작 요청 후 COMBAT 진입과 실제 용사의 상단 생성/하향 이동 확인.
- 테스트 도중 새 검사 코드의 누락된 namespace를 수정하여 컴파일 오류를 해결했다. 기존 카메라 더미 검사는 첫 전투 lifecycle 알림을 명시적으로 전달하도록 갱신했다.
- 실제 전체 라운드 완주, 모든 해상도 및 플레이어 빌드 검증은 미수행이다.
