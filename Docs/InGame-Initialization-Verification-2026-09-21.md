# InGame 초기화·스킬 수명주기 보강 검증

브랜치: `fix/ingame-initialization/42` / Unity 6000.3.22f1

## 변경 범위

- `InGamePrototypeBootstrap`: 씬 진입 경로와 무관한 필수 연결 생성, 새 실행 초기화, 스킬 참여자 자동 등록, 취소/종료 시 즉시 차단.
- `InGameSkillConnection`: 기존 전투 수명주기와 스킬 공개 API 연결, 이전 실행의 늦은 활성화/정리 차단.
- `InGameSynergyConnection`: 이전 실행의 집계·정리가 새 실행을 변경하지 않도록 소유권 확인.
- `RealCombatBootstrap`, `RealSynergySync`: 중복 없는 생성 API, 새 게임 장착 정보 재조회, 카메라·마왕 위치 갱신, 사용 허용/중단 API.
- `SkillManager`, `SkillExecutor`, `SkillBarUI`: 모든 TryCast 경로의 시전 차단, 지연 공격/장판/VFX 정리, 조준 취소, 생성한 스프라이트·텍스처 해제.
- `GridFusionPlayVerification`: PR #40의 라운드 시작 체력 회복 규칙에 맞춰 검증 기대값 수정.
- `InGameSkillLifecycleVerification`: 경계 조건 재현용 Play Mode 검증 추가.

피해·회복 수치, SO 직업, 씬·프리팹·프로젝트 설정은 변경하지 않았다. 신규 C# 파일의 meta는 Unity가 생성했다.

## 실행 결과

| 검증 | 결과 |
|---|---|
| Unity 컴파일 및 Console | 오류·경고 0개 |
| Builds/Lobby에서 시작 → Builds/InGame 로드 | 로비의 sync 0개에서 InGame의 sync 1개로 생성, 시너지 연결 성공 |
| 실제 1라운드 전투 → 보상 → 준비 → 2라운드 전투 | 보상·준비에서 시전/스킬바 차단, 2라운드에서 재활성화, Bootstrap 오류 없음 |
| 전투 중 취소 | 호출 즉시 시전 차단, 종료 작업 완료 |
| 로비 이동 후 InGame 재진입 | 같은 sync 재사용, 새 SkillManager 생성, 실행기·스킬바 각각 1개 |
| 라운드 간 유지 | 같은 SkillManager 유지, 라운드마다 쿨다운을 초기화하지 않음 |
| 독립 스킬 검증 | 즉발·지정 시전 차단, 차단 중 쿨다운 미소비, 취소된 투사체 추가 피해 없음 |
| 효과 정리 | 반복 취소 안전, 다음 시전 피해 1회, 장판 즉시 정지·제거, 조준 재등장 없음 |
| 이전 실행 소유권 | 이전 연결의 정리 후 새 manager 사용 허용 유지, 이전 연결의 활성화 거절 |
| 설정 누락 실패 | 오류 보고 후에도 스킬 사용 차단 유지 |
| InGameVerification | 전체 더미 스테이지·패배·초기 배치·보상 초과/취소/버리기·오래된 요청·필수 확장·재시작 PASS |
| InGameCameraVerification | 초기 프레이밍·리사이즈 실패 취소·준비 복구·배치·전환 차단·취소 PASS |
| GridFusionPlayVerification | 보드/보관함/교차 합성·유효성·성급·배치 이동/소멸·체력 회복·원본 SO 보존 PASS |
| Git diff 검사 | 공백 오류 없음, 관계없는 파일 변경 없음 |

## 검증 범위와 후속 연동

- 실제 전투는 2라운드 진입 및 취소까지 확인했다. 실제 전투로 30라운드를 모두 완료한 검증은 아니다. 전체 스테이지 종료는 기존 더미 검증으로 확인했다.
- UI 담당자의 장착 저장 연결이 완성되면, 그 저장소를 새 게임에서 다시 읽는다. 미저장 UI 미리보기 선택은 여기서 저장하지 않는다.
- 유닛에 이미 적용된 버프·상태이상의 만료는 기존 유닛 규칙을 유지한다. 라운드마다 강제로 초기화하려면 유닛 담당자의 규칙/API 합의가 필요하다.
- 별도 Inspector 연결은 필요 없다. 스킬 참여자는 자동 등록하며 기존 ProjectileCombatParticipant 등록은 유지한다.
- 실행 검증은 프로토타입 저장 경로를 사용하므로 검증 실행 기록이 로컬에 남을 수 있다.

재실행 메뉴와 호출 규격은 `CoreLoop-Connections-TeamGuide.md` 참고.
