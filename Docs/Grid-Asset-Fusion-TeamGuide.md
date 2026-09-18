# RF Castle 배치판과 합성 연동 (#23)

> #25부터 InGame의 기본 테마는 SPUM으로 교체되었다. 현재 연결 배치 규칙과 타일 설정은 [Grid-Connectivity-TeamGuide.md](Grid-Connectivity-TeamGuide.md)를 참고한다. 아래 RF Castle 정보는 기존 테마에 대한 기록이며 합성 계약은 유지된다.

## 적용 범위

- InGame 준비 화면과 전투 월드 배치판에서 RF Castle 스프라이트를 사용한다.
- 미확장 영역은 밝은 갈색, 확보한 빈 영역은 어두운 녹색, 발판 영역은 어두운 보라색 돌바닥으로 구분한다.
- `RFCastleTheme.asset`에서 스프라이트와 색을 교체한다. UI의 밝은 영역은 HDR tint 제한을 보완하는 오버레이를 사용한다.
- 초기 4×3, 최대 8×5, 기존 6종 발판과 1×2 확장 규칙을 유지한다.
- 이번 프로토타입의 유닛 점유는 성급과 관계없이 1×1이다. 발판의 모양과 유닛 점유 모양은 독립적이다.

## 합성 규칙과 입력

- 같은 종류·같은 성급 2기 → 상위 성급 1기, 최대 3성.
- 자동 인접 합성이 아니라 **대상 유닛/보관함 카드 위에 드롭**하여 합성한다. 기존 팀 구현의 드롭 방식에 맞춘다.
- 그리드, 보관함 및 두 영역 간 합성을 지원한다. 결과는 대상 유닛의 인스턴스 ID와 위치를 유지한다.
- 원본 유닛만 소모하고 발판은 유지한다. 빈 칸에 다른 유닛을 재배치할 수 있다.
- 보관함 빈 공간에 드롭하면 보관, 다른 유닛 카드 위에 드롭하면 합성을 시도한다. 조건이 다르면 원위치로 돌아간다.
- 합성은 준비 단계에서만 허용한다. 보관함 공간 확보 요청 처리 중에는 금지한다.
- 별도의 합성 보상을 만들거나 매번 그리드를 초기화하지 않는다.

## 연결 API

- `GridRunSession(runId, definition, GridFusionPolicy.CanFuse)`로 팀 합성 판정을 주입한다. 판정을 생략하면 기존 호출 호환성을 유지하며 합성은 비활성이다.
- `GridManager.CanFuseUnits(sourceId, targetId)`는 읽기 판정, `TryFuseUnits`는 상태 확정이다.
- `CanFusePreview`로 미리보기/안내를 구분한다. `CommitPreview()`는 유닛 드롭 위치에 합성 가능한 대상이 있으면 합성한다.
- `UnitPlacement.StarLevel`이 실행 중 성급의 기준이다. `WithPlacement` 및 `GridDeploymentSnapshot`에서도 보존된다.
- 합성 성공 시 `Changed`와 `LayoutChanged`를 각 1회 통지한다. 실패 시 유닛·발판·보관함 데이터는 그대로 유지한다.
- `FusionRules`의 ID/성급 기반 판정을 기존 `UnitSlot` 경로와 함께 사용한다. Grid에서 `FusionRules.Fuse(UnitSlot, UnitSlot)`를 직접 호출하지 않는다.

## 전투·시너지 담당자

- `RealDefenders.PrepareRoundAsync`는 매 라운드 배치 확정본에 맞춰 신규 생성, 기존 위치 갱신, 소모/회수된 개체 제거, 성급 반영 및 사망 유닛 부활을 수행한다.
- 성급 적용은 기존 `UnitBase.ApplyStarLevel`을 사용하며 원본 SO를 변경하지 않는다.
- 준비 월드 미리보기와 전투 생성은 `DemonArmyCatalog.FindPrefab(id, starLevel)`로 같은 성급 외형을 조회한다. 2·3성 외형이 없으면 카탈로그의 기본 외형 대체 규칙을 따른다.
- 준비 중 합성하면 미리보기와 드래그 외형을 갱신한다. 전투 개체는 다음 전투 배치 확정 때 외형이 달라진 경우에만 교체하며, 같은 외형이면 기존 개체와 체력을 유지한다. 합성 시 체력 회복은 기존 `ApplyStarLevel` 규칙을 따른다.
- 누락된 프리팹/스탯은 전투 시작 전에 오류로 처리한다. 일부 유닛만 생성하고 전투를 진행하지 않는다.
- `RealDefenders`는 IDisposable이며 InGame의 StageRunHost가 정리한다. 다른 조립 루트는 팩토리의 `ownDefenders` 콜백을 통해 수명을 소유해야 한다.
- Bootstrap이 소유하는 `InGameSynergyConnection`이 실제 배치 유닛 수를 직업별로 계산하여 팀의 `RealSynergySync.SetCount`에 전달한다. 보관함 유닛은 제외한다. `InGameGridPresentation`은 표시만 담당한다.
- 합성으로 개체 수가 줄면 시너지가 해제될 수 있다. 종료 시 해당 실행의 시너지 카운트를 0으로 돌린다.

## UI 담당자

- `GridBoardView` / `GridPrototypeRunner`는 교체 가능한 준비 화면이다. 최종 UI에 더미 화면 구현을 복사할 필요는 없다.
- 바닥 표현은 `GridBoardThemeSO.GetSurface`로 조회하고, 유닛 점유는 `GetUnitAt`으로 별도 조회한다.
- 준비 화면의 셀 좌표와 실제 전투 좌표는 `GridWorldMapping`으로 연결한다. 원점은 셀 (0,0)의 중심이다.
- 전투 월드 바닥은 `GridWorldBoardView`가 표시하며 입력 처리를 소유하지 않는다.
- 그리드 규칙은 화면 요소나 전투 GameObject를 직접 참조하지 않는다.

## 에셋과 설정

- 외부 원본: `Assets/98.ExternalAssets/RF Castle/Sliced/mainlevbuild.png`
- 스프라이트: `mainlevbuild_465`(미확장), `mainlevbuild_459`(빈 영역), `mainlevbuild_925`(발판).
- 출처: https://assetstore.unity.com/packages/2d/environments/rogue-fantasy-castle-164725
- 라이선스: Unity Asset Store Standard EULA. 원본의 공개 저장소 재배포는 포함하지 않는다.
- 외부 원본은 팀의 기존 외부 에셋 공유 절차를 따른다. 이 PR에는 스프라이트 참조를 가진 팀 설정만 포함하고 원본 파일은 포함하지 않는다.
- 팀원은 같은 경로와 `.meta` GUID로 에셋을 준비해야 한다. 임포트된 원본을 이동하거나 메타를 재생성하지 않는다.
- InGame 씬에 `InGameGridPresentation`과 준비 화면의 테마 참조가 연결되어 있다.
- `OZGL2 > InGame > Apply Grid Art and Fusion`은 저장된 InGame 씬에만 적용 가능한 설정 도구다. 기존 설정을 다시 적용하므로 사용자 색상 변경 후에는 필요한 경우에만 실행한다.

## 검증 방법

- `OZGL2 > InGame > Verify Grid Fusion Rules`: 합성 원자성, 3성 상한, 회수/재배치 성급 유지, 이전 확정본 불변성, 꽉 찬 보관함 합성, 재진입/단계 차단.
- `OZGL2 > Grid > Verify ...`: 기존 그리드 검증 메뉴로 회귀 확인.
- `GridFusionPlayVerification`: 빈 임시 씬의 Play 모드에서 실행한다. 실제 UI 포인터 이벤트와 실제 마왕군 프리팹으로 합성/다음 전투 반영을 확인한다. 테스트 중 백그라운드 실행을 켜며 `Cleanup()`으로 해제한다.
- 테스트에는 용사 처치나 계정 성장 저장이 필요하지 않다.

## 범위 밖

- 최종 UI 레이아웃·아트 연출, 자동 인접 합성, 합성 확률/비용, 4성 이상은 포함하지 않는다.
- 투사체 풀과 전투 스킬 자체의 구현은 변경하지 않는다.
- 다칸 유닛을 다시 사용하려면 콘텐츠 데이터와 합성 배치 규칙을 별도로 검토해야 한다.
