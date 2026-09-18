# InGame 준비·전투 공간 및 카메라 전환 (#26)

## 적용
- 기존 월드 타일은 준비/전투 동안 같은 위치에 유지한다.
- 준비 화면의 중앙은 투명한 월드 입력 영역이며 하단 보관함·폐기·시작 UI는 더미로 유지한다.
- 준비 구도는 가까이, 전투 구도는 적 스폰 위치를 포함해 넓게 표시한다. XY 정면 회전과 현재 카메라 투영 방식을 유지한다.
- InGameCameraConfig.asset에서 전환 시간, 여백, 준비/전투 viewport 안전 영역을 조절한다.
- 첫 라운드는 기본 유닛 자동 배치 후 전투 구도로 시작한다. 이후 시작/스킵 요청은 카메라 전환이 끝난 후 배치를 확정한다.
- 전환 중 준비 입력과 중복 시작을 차단한다. 취소/세션 변경/비활성화는 대기 작업을 취소하여 이전 실행이 전투를 시작하지 않게 한다.
- 카메라 오류 시 준비 화면의 Retry camera로 설정을 다시 검증하고 준비 구도를 복구한다. 복구가 완료되어야 오류와 입력 잠금을 해제한다. End current run은 기존 Bootstrap.CancelRun 종료 경로를 사용하며 성공 정산이나 강제 씬 이동을 수행하지 않는다.
- 최초 실행은 Stage 생성 전에 실제 전투 영역의 카메라 구도를 검증한다. 전투/보상 화면의 해상도 변경 중 구도 설정이 실패하면 기존 실행을 취소하고 Bootstrap.Error에 이유를 남긴다. 전투를 준비 상태로 바꾸거나 성공 보상을 지급하지 않는다.

## 연결 지점
- InGamePrototypeBootstrap.TryBeginBattle(skip): UI의 전투 시작 진입점. true는 요청 수락이며 즉시 전투 시작 완료를 뜻하지 않는다.
- InGamePhasePresentation.CanInteract / IsTransitioning / Error: 최종 UI의 입력/버튼/안내 상태.
- InGamePhasePresentation.CanRetryCamera / RetryCamera(): 오류 복구 요청. true는 요청 수락을 뜻하며 완료 여부는 IsTransitioning과 Error로 확인한다. CanExitAfterCameraError / ExitAfterCameraError()는 오류 후 실행 종료에 사용한다.
- Bootstrap.HasExternalCombatParticipants는 카메라 표시를 제외한 외부 전투 어댑터 존재 여부이다. 어댑터 존재만으로 스킬 차단·효과 정리 등 개별 기능의 연결 완료를 보장하지 않는다.
- 실제 UI도 GridRunSession.TryBeginBattle을 직접 호출하지 말고 Bootstrap.TryBeginBattle을 사용한다. 직접 호출은 카메라 전환을 우회한다.
- GridWorldInputSurface: 현재 카메라의 ray와 XY 그리드 평면으로 좌표를 계산한다. 화면 좌표와 UI Toolkit 패널 좌표 변환 제공.
- IGridBoardSurface: 기존 고정 UI 그리드와 월드 입력 영역이 공유하는 입력 표면 계약.
- GridWorldPreparationView: 프리팹의 활성 SpriteRenderer 외형만 복사한다. UnitBase/Animator/Collider/AI는 생성하지 않는다. 준비 애니메이션은 이번 범위 밖이다.
- 준비용 유닛 위에 `★1` 형태로 현재 StarLevel을 표시한다. 합성·재배치·회수에 맞춰 갱신하며 전투 진입 시 준비 미리보기와 함께 숨긴다. 더미 TextMesh 표시로, 전투 프리팹이나 성급 데이터를 수정하지 않는다.
- 준비에서 기존 전투 유닛의 Renderer만 숨기며, 전투 배치 후 IInGameCombatParticipant.SetCombatEnabled(true)에서 준비 외형을 숨기고 원래 렌더 상태를 복원한다.
- 카메라 구도 계산과 게임 진행을 분리했다. 씬에는 InGameCameraTransition / InGamePhasePresentation을 조립하며 Stage/Grid 규칙은 재사용한다.

## 팀원 협의 사항
- 최종 피그마 UI 디자인은 구현하지 않았다. 실제 HUD 공간에 맞춰 viewport와 준비 UI 하단 여백을 조절해야 한다.
- 기존 팀원 스킬바/디버그 패널은 그대로 유지된다. 준비 UI는 현재 스킬바 공간을 피한다.
- 스킬 담당자의 준비 단계 시전 금지, 조준 취소, 효과 정리는 별도 IInGameCombatParticipant 어댑터로 연결해야 한다. 이번 카메라 표시 연결을 스킬 제어 완료로 간주하면 안 된다.
- 현재 SkillBarUI의 XY 정면 좌표 계산과 호환되도록 카메라 회전은 변경하지 않는다. 경사 카메라를 도입하면 팀원의 스킬 좌표 변환도 함께 검토해야 한다.
- 유닛의 원본 SO/전투 로직/외부 에셋을 변경하지 않는다.

## 검증
- InGameCameraVerification: 빈 임시 씬 Play에서 메모리 저장소와 수동 전투 서비스를 사용한다. 계정/실제 진행 저장을 쓰지 않는다.
- 초기 전투 구도, 줌 전후 좌표 일치, 수동 포인터 배치/회수, 준비 표시의 전투 컴포넌트 미생성, 전환 중 시작 차단, 중복 시작, 취소, 최대 보드 화면 비율을 검사한다.
- 카메라 far clip을 테스트 오브젝트에서만 낮춰 전환과 복귀의 연속 실패, 전투 미시작, 종료 가능 상태, 설정 복원 후 재시도 및 중복 재시도 차단을 검사한다. 카메라만 연결한 경우 외부 어댑터로 집계하지 않는지도 검사한다.
- 최초 구도 검증 실패와 전투 중 해상도 재계산 실패도 검사한다. 수동 전투 서비스와 실제 StageRunHost를 조합하여 전투 실행 종료 및 오류 이유 보존을 확인한다.
- 기존 GridVerification / GridConnectivityVerification / GridFusionVerification / InGameVerification / GridFusionPlayVerification으로 회귀 확인한다.
- 테스트 캡처는 Git 제외된 Temp/CameraVerification에만 생성한다.
