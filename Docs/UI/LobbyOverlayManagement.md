# 로비 Overlay 관리 규칙

## 적용 범위

- 현재 디자인 기준 Scene은 `Assets/00.Scenes/Builds/Lobby.unity`이다. 아래 2026-09월 기록의 `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity`는 당시 검증 대상이며, 다른 Scene을 임의로 저장하지 않는다. 공용 Prefab 원본을 수정하면 이를 사용하는 다른 Scene에도 해당 외형·계층 변경이 반영된다.
- 로비의 새 하위 화면은 `Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab` 아래에서 관리한다.
- 메뉴·설정·특성·스킬 세팅·도감·업적을 이 루트의 자식 화면으로 관리한다. 도감/업적 데이터와 해금은 [LobbyCollectionsPreview.md](LobbyCollectionsPreview.md)를 따른다.
- 별도 원본 Prefab을 사용하는 도감/업적은 공용 루트 안에서 중첩 Prefab 연결을 유지한다. 현재 스킬 화면은 부모 Prefab의 일반 자식으로 관리한다. 기존 구조를 임의로 Unpack하거나 화면 전체를 복제하지 않는다.

## 관리 구조

```text
Canvas_LobbyOverlays                 # 공용 루트 / CanvasScaler / UILobbyOverlayView
├─ TraitsScreen                     # 기존 특성
├─ MenuPopup                        # 기존 메뉴
├─ SettingsPopup                    # 기존 설정
├─ Canvas_SkillSettings              # 부모 Prefab에 포함된 일반 자식 화면, 19종 v2
│  └─ ExitConfirmation              # 미저장 확인창
├─ Canvas_UnitCodex                  # 유닛 도감 중첩 Prefab
└─ Canvas_Achievements               # 업적 중첩 Prefab
```

`Assets/06.UI/LobbyMutedPreview/Skills_v1/Prefabs/Canvas_SkillSettings.prefab`은 이전 시안의 참고용 원본으로 보존한다. 현재 부모 안의 스킬 화면은 이 에셋의 중첩 인스턴스가 아니므로 수정 내용을 옛 원본에 Apply하지 않는다. 공통 분류 스타일은 `Skills_v1/Styles`를 재사용하고, 현재 19종 카탈로그/아이콘/궁극기 장식은 `Skills_v2`에 둔다. [SkillRoster_v2.md](SkillRoster_v2.md)의 데이터와 적용 절차를 따른다. `Canvas_Popups`의 옛 팝업들은 보존하며 새 스킬 화면의 중복 인스턴스는 만들지 않는다.

## 화면 전환과 입력

- 로비 `SkillsButton` → `Canvas_LobbyOverlays/UILobbyOverlayView.OpenSkills`.
- 스킬 `Back` → 같은 루트의 `UILobbyOverlayView.CloseTop`.
- 루트 `UIPopupController`가 스킬과 확인창의 스택을 관리한다. `Popup Root`는 자기 Transform, `Screen Group`은 Scene 인스턴스에서 `Canvas_Lobby/CanvasGroup`에 연결한다.
- 도감/업적은 루트 직계 자식에 `UIPopupPanel`을 두고 `UILobbyOverlayView.OpenOverlayPopup(panel)`으로 연다. 로비 `CodexButton`/`ReservedButton`을 각각 연결했다. 메뉴·설정·특성의 기존 처리 방식은 유지한다.
- 새 화면과 기존 화면을 동시에 열지 않는다. 열린 동안 로비 입력을 차단하고 닫을 때 이전 선택을 복원한다.
- 스킬이 열려 있으면 ESC는 Overlay의 Controller만 처리한다. 같은 ESC로 스킬을 닫고 메뉴를 다시 열면 안 된다.
- 일반 뒤로/ESC는 `CloseTopPopup`을 거친다. `CloseConfirmedPopup`으로 미저장 확인을 우회하지 않는다. 강제 정리는 루트 종료 때만 사용한다.
- 스킬 미저장 확인은 기존 `UISkillLoadoutPreview`가 맡는다. 공용 루트는 스킬 장착/포인트/도감/업적 데이터를 소유하지 않는다.
- 2026-09 당시 미리보기 저장과 현재 `Builds/Lobby`의 실제 저장을 구분한다. 현재는 `LobbySkillLoadoutBinder`가 저장 요청을 `SkillTreeStore.SetEquipped`로 연결하며, 해금도 실제 SP/저장 상태와 연동한다. 아래 정리 회귀 검사에서는 실제 저장·해금 확정을 실행하지 않았다.
- `UILobbyCollectionState`는 같은 루트에 별도 컴포넌트로 두며, 도감·스킬의 해금 및 업적 진행도만 공유한다. 전환 담당 `UILobbyOverlayView`에 데이터 책임을 넣지 않는다.

## Canvas / Prefab 연결

- 루트 기준 해상도 1920×1080, Sorting Order 200. 화면마다 중복 EventSystem/CanvasScaler를 만들지 않는다.
- 스킬 화면의 Canvas는 210, 확인창은 211이다. `Override Sorting`과 각 Canvas의 `GraphicRaycaster`를 유지한다.
- 새 전체 화면도 같은 계층 규칙으로 동시에 하나만 열고, 정렬 값을 무작정 높이지 않는다.
- 스킬 Back/Controller 및 공유 상태 참조는 **부모 Prefab 내부의 일반 자식 참조**로 저장한다. 참고용 `Skills_v1` 원본에는 Scene 객체를 연결하지 않는다. 도감/업적의 부모 연결은 각 중첩 Prefab의 override로 유지한다.
- `Screen Group`, 기존 로비/Legacy Controller/Raycaster 참조는 **Scene 인스턴스 override**이다. 부모 전체 Apply로 다른 Scene 참조나 수동 조정을 밀어 넣지 않는다.
- 시작 시 화면/확인창은 비활성, 공용 루트는 활성이다.
- 아트·TMP·그라데이션·분류색·저장 상태 규칙은 유지한다. [SkillCategoryVisualRules.md](SkillCategoryVisualRules.md)를 함께 따른다.

## Editor 도구

### 공통 뒤로 버튼

- 디자인 기준은 `TraitsScreen/Back`이다. 스킬·도감·업적의 `Back`은 같은 위치 `(765, 451)`, 크기 `268×85`, `Button_Back_268x85` 이미지를 사용한다.
- `Label`은 특성과 동일한 중앙 정렬, 32 크기의 `뒤로` 문구·폰트·머티리얼·색상을 사용한다. 별도 `Arrow`는 삭제하지 않고 비활성화한다.
- 시각 설정만 부모 `Canvas_LobbyOverlays.prefab`에 저장한다. 도감/업적은 중첩 인스턴스 override로 유지하며 원본 Prefab을 변경하지 않는다.
- 기존 Button 컴포넌트와 클릭 연결은 유지한다. 특성은 `CloseTraits`, 나머지 세 탭은 `CloseTop`으로 닫고, 스킬 미저장 확인을 우회하지 않는다.
- 2026-09-22: 세 탭의 렌더 후 EventSystem Raycast/포인터 클릭으로 닫힘 확인. 스킬 미저장 뒤로 → 확인창 → 취소 시 편집 유지 → 폐기 시 닫힘/저장 상태 복구 확인. 스킬 회귀 검사 791항목, 도감/업적 통합 검사 226항목 통과, Console 오류·경고 0개.
- 저장 전후 세 Back 바깥의 2,341개 컴포넌트는 동일했다. 작업 시작 시 존재하던 Scene 미저장 변경과 override 159개는 보존하고 Scene을 별도로 저장하지 않았다.

### 조립 및 검증 메뉴

- 아래 메뉴는 2026-09 당시 v2 조립/검증 도구이다. Scene 경로 `UI_Lobby_MutedPreview`와 구형 3슬롯 등 당시 전제를 사용하므로, 현행 `Builds/Lobby`의 Heraldry 화면에서 재생성·자동 검증 용도로 실행하지 않는다. 도구 갱신은 이번 정리 범위에서 제외한다.
- 당시 스킬 조립: `Tools/OZGL2/Lobby/Build Skill Roster V2 (19 Skills)`. 해당 도구가 허용하는 Scene의 Edit Mode 또는 부모 `Canvas_LobbyOverlays`의 Prefab Mode에서 실행한다. Scene에서 시작하면 부모 Prefab Mode를 연다.
- 기존 일반 자식 `Canvas_SkillSettings` 안에서 19개 카드/3열 스크롤/설명/메타데이터/궁극기 장식을 갱신한다. 다른 Overlay와 Scene 인스턴스의 수동 override는 전체 Apply하지 않는다.
- Prefab Mode의 오브젝트/참조 변경은 Unity Undo를 지원한다. 결과 확인 후 부모 Prefab은 수동 저장하며 Scene은 자동 저장하지 않는다. 새 카탈로그/폴더 생성 자체는 Undo 삭제 대상이 아니고 카탈로그 값은 에셋에 저장하므로, 복구 시 Git Diff도 함께 확인한다.
- 이전 `Integrate Skill Settings Into Lobby Overlays` 및 12종 스킬 생성 도구는 초기 통합용 기록이다. 현재 일반 자식/v2 화면을 재생성하는 용도로 사용하지 않는다. 참고용 `Skills_v1` 원본은 덮어쓰지 않는다.
- 당시 스킬 검증: `Tools/OZGL2/Lobby/Validate Skill Roster V2`, `Tools/OZGL2/Lobby/Validate Skill Roster V2 Text Layout`. 도감/업적의 기존 검사 방법은 [LobbyCollectionsPreview.md](LobbyCollectionsPreview.md)에 기록되어 있으며, 현재 화면과 경로/레이아웃 전제가 일치하는지 먼저 확인한다.

## 확인 목록

1. 부모 Prefab과 Scene의 부모 인스턴스에 스킬 화면이 하나씩 있고, 부모 내부에서 일반 자식으로 유지되는지 확인한다. 도감/업적은 각각 중첩 Prefab 연결을 유지한다.
2. SkillsButton/Back, Popup Root/Screen Group, 공유 `UILobbyCollectionState` 참조의 누락을 확인한다.
3. 열기 → 저장 활성/비활성 → 미저장 뒤로/ESC → 취소/로비로 → 재열기를 확인한다.
4. 메뉴·설정·특성, 기존 도감·업적 팝업과 로비 입력 복원을 확인한다.
5. 실제 Raycast, 선택 포커스, Canvas 순서, 화면 비율을 확인한다.
6. Console 오류, Missing Script/Reference, 실제 저장 데이터 불변을 확인한다.
7. 스킬 19개/필터별 9·4·6개, 세로 스크롤 끝의 심판 선택, 티어·발동·해금 SP 설명, 궁극기 목록/상세/장착 장식과 기존 흰색 분류 표시를 확인한다.

Scene/부모 Prefab은 충돌 주의 파일이다. `.unity`/`.prefab`/`.meta`는 텍스트로 수정하지 않는다. ProjectSettings/Packages/입력/물리 설정은 이 통합 범위가 아니다.

## 미사용 구형 UI 정리 기록 — 2026-10-06

### 정리 기준과 변경 범위

- 비활성 상태만으로 미사용 UI라고 판단하지 않는다. Runtime 직렬화 참조, Button 이벤트, 이름·경로 조회, Editor 조립/검증 도구의 의존성과 다른 Scene의 사용 여부를 함께 확인한다.
- `Canvas_LobbyOverlays`, `Canvas_UnitCodex`, `Canvas_Achievements`, `AchievementCard` 원본 Prefab 4개에서 참조 없는 구형 장식만 제거했다. 삭제 후보 루트 127개, 자식 포함 GameObject 141개이다. 생존 Runtime 컴포넌트 102개와 중첩 Prefab 연결 3개를 보존했다.
- `Builds/Lobby`, `Builds/Lobby_2`, `UI_Flow/UI_Lobby_MutedPreview`의 세 소비 Scene에서 삭제 대상에 대한 외부 연결이 없음을 확인했다. 저장한 Scene은 `Builds/Lobby.unity` 하나뿐이다.
- Lobby의 삭제 대상에 남아 있던 무효 override 137개와, 별도 승인을 받은 기존 무효 비활성 설정 1개를 Unity 공식 `PrefabUtility.RemoveUnusedOverrides`로 정리했다. 같은 기존 orphan 대상의 `m_RemovedGameObjects` 기록도 제거됐다. 유효한 override 4,496개의 순서·대상·속성·참조를 보존했다. API 직후에는 값과 컴포넌트 비교 5,276개도 모두 동일했다. 최종 저장 파일에서는 `TraitsScreen/PanHint`의 Y 좌표 하나만 Unity의 float 재직렬화로 `24.8 → 24.800049`가 됐고, 나머지 값 4,495개는 동일하다.
- C#, 아트·폰트·카탈로그·게임 데이터, `.meta`, ProjectSettings, Packages, 전투 Scene은 수정하지 않았다. `.unity`/`.prefab`을 텍스트로 편집하지 않았으며, 네이티브 PrefabInstance 내부 배열 직접 편집은 재사용하지 않는다.

### 삭제한 구형 장식

| 원본 | 삭제 대상 | GameObject 수 |
| --- | --- | ---: |
| Canvas_LobbyOverlays | TraitsScreen의 옛 하단 그라데이션·좌우 Fade·MagicCircle·DemonKingBackground·Link/Joint 각 52개·분류 Header의 옛 Icon 4개, 스킬 화면의 옛 하단 그라데이션·AccountStatus·StatFrame, 메뉴의 옛 Crest·Divider·DividerDiamond·아이콘·PreviewNote | 137 |
| Canvas_UnitCodex | BackgroundBottomGradient, 구형 CollectionPanel 이미지 | 2 |
| Canvas_Achievements | BackgroundBottomGradient | 1 |
| AchievementCard | 구형 IconFrame | 1 |

스킬에서 제거한 `AccountStatus`는 참조 없는 옛 미리보기이며, 사용 중인 `HeraldryAccountStatus`와는 다른 오브젝트이다. 새 장식과 아이콘은 유지했다.

### 비활성이어도 보존한 UI

- 메뉴·설정·특성·스킬·도감·업적 6개 화면은 시작 시 비활성, `Canvas_LobbyOverlays` 루트는 활성 상태로 유지한다.
- 특성 상세창·초기화 확인창·노드 Selection 40개, 스킬 미저장/해금 확인창·잠금/궁극기/필터/장착 상태 표시와 5개 슬롯을 보존한다. 기본 3슬롯과 특성 확장 슬롯 4·5의 구분도 유지한다.
- 도감/업적 카드 Template, 성급·외형 표시와 좌우 화살표, 미발견/빈 목록/진행도 상태, 난이도별 유닛 그룹은 조건에 따라 사용하는 UI이므로 제거하지 않는다.
- `Canvas_Popups` 전체는 기존 Controller·Raycaster·입력 복원 및 다른 Scene/도구의 의존성이 있어 보존한다. 새 화면과 구형 화면이 동시에 열리지 않는지는 별도로 확인한다.

### 추가 코드·도구 정리 없이는 삭제하지 않는 항목

- 특성 `AccountStatus`, 스킬 `HeraldryAccountStatus`: 숨긴 레벨바라도 HUD/Inspector 연결이 남아 있다.
- 메뉴 `BGMToggle`, `SFXToggle`, `State`: `UILobbyOverlayView` 배열과 전투 메뉴 조립 도구가 참조한다.
- 특성 노드/분류 Header의 `NameShade`, `DemonKingIcon`, 상세창 `Divider`: 기존 특성 조립·정렬 도구의 경로 조회가 필요하다.
- 업적 카드 `ProgressRail`, 구형 Back/Arrow·Segment 및 도감 진영 Emblem: 기존 검증/관리 규칙이나 이벤트 연결을 먼저 정리해야 한다.
- 구형 12종 스킬 생성·검증 도구의 `BackgroundBottomGradient` 존재 검사 등은 이번 정리 후 현행 19종 화면에 적용하지 않는다. 오래된 도구를 재실행해 삭제 장식을 되살리거나 현행 디자인을 덮어쓰지 않는다.

### 검증과 복구

- Unity Play Mode의 실제 Button 콜백과 기존 API로 63항목 통과: 메뉴/특성 15, 스킬 22, 도감/업적 26. 열기·뒤로가기·입력 복원, 특성 40개 선택, 확인창 취소, 스킬 필터/잠금/미저장 폐기/4·5슬롯, 도감 외형 전환과 재열기 시 중복 생성 방지를 확인했다.
- 슬롯·포인트·진행도 조건 검사는 임시 표시 상태와 분리된 테스트 카드로 수행하고 복원했다. 실제 스킬 저장·해금 확정·특성 초기화/성장 확정은 실행하지 않았다.
- 계정 PlayerPrefs 27개를 대조했다. SP·장착·해금·레벨·XP·LP 등 실제 값은 유지됐고, 기존 자동 저장의 `OZGL2.Save.SavedAt` 시각만 갱신됐다.
- 최종 Console 오류·경고 0개, Overlay 하위 Missing Script/끊어진 참조 0개. 임시 검증 오브젝트는 제거했다. 종료 상태는 Edit Mode, Lobby 저장 완료이다.
- 실제 마우스/키보드·ESC 하드웨어 입력, 비 16:9 해상도, 플레이어 빌드는 미확인이다.
- 재시작 후 안정 백업은 Git 제외 `Assets/98.ExternalAssets/00.LocalStaging/UI_Cleanup_Backups/20261006_Resume_01`에 `.backup.txt`로 보관한다. 이 백업은 정리된 Prefab과 override 정리 전 Lobby를 포함한다. Prefab 정리 전 원본은 Git 기준 커밋 `8fc8176`에서 복구 가능하다. 재시작 시 삭제된 Temp 백업에 의존하지 않는다.
- 충돌 주의 파일은 Lobby Scene과 원본 Prefab 4개이다. 커밋/푸시는 수행하지 않았다.

## 스킬 원형 슬롯·잠금 표시 동기화 — 2026-10-06

### 현재 표시와 기능

- `Builds/Lobby`와 부모 `Canvas_LobbyOverlays.prefab`의 `Canvas_SkillSettings`에 장착 슬롯 5개를 항상 표시한다. Edit Mode에도 `Heraldry_Skills_v1`의 원형 프레임을 저장해, 화면을 켰을 때 구형 마름모 프레임이 보이지 않게 했다. 슬롯 루트의 기존 크기·위치는 유지한다.
- 기본 1~3번은 장착 가능하고, 4·5번은 특성 `Sk_C3` / ‘추가 지령’의 0·1·2단계에 따라 잠김 2개 → 1개 → 0개로 전환한다. 잠긴 슬롯은 회색 원형·아이보리 자물쇠·‘잠긴 슬롯’으로 표시하고 클릭과 직접 선택 호출을 차단한다. 열렸지만 스킬이 없으면 ‘빈 슬롯’이다.
- `UISkillLoadoutPreview.Refresh`의 표시 개수만 5개로 고정했다. 실제 `_draft`/`_committed` 길이와 장착 한도는 기존 `SlotCapacity` 3·4·5를 따른다. 특성 효과·SP/해금·저장 계산과 `LobbySkillLoadoutBinder`는 변경하지 않았다. 특성 초기화로 용량이 줄면 기존 배열 축소 규칙을 따라 표시를 지우고 다시 잠근다. 기존 영구 장착 저장을 자동으로 삭제하는 동작은 추가하지 않았다.
- Inspector의 `_equippedSlotButtons`는 기존 Button 5개, `_equippedLockIcons`는 처음 3개 null / 마지막 2개 `SlotLock` Image로 연결한다. 자물쇠의 Raycast Target은 끈다. 해금된 스킬의 흰색 본체·분류색·궁극기 장식 정책은 유지한다.
- Prefab의 초기 예시 스킬·정적 문구는 계정 저장이 아니다. 화면을 열면 기존 바인더가 실제 장착·해금·남은 SP를 반영한다. 계정 수치가 아직 적용되지 않은 Edit Mode의 SP/보유 수는 ‘—’로 표시한다. 스킬 화면 자체는 로비 시작 시 비활성이다.

### 별도 승인한 설정창 복원

- 작업 전 Lobby 인스턴스에 있던 `SettingsPopup` 삭제 override를 공식 `PrefabUtility.RevertRemovedGameObject`로 되돌렸다. `_settingsPanel`, `_settingsFirst`, BGM/SFX 두 토글과 두 상태 라벨의 null override 6개는 원본 연결을 상속하도록 복원했다. 설정 디자인·코드·다른 Scene은 수정하지 않았다.
- 삭제 기록에 남아 있던 `SettingsPopup`의 활성 override도 복원되어 로비 시작 시 다른 화면 열기를 막는 것을 발견했다. 이 GameObject만 시작 비활성으로 저장하고 새 Play 세션에서 검증했다. 스킬과 설정을 포함한 하위 화면은 모두 시작 시 닫힘이다.

### 검증·변경 범위

- Unity 컴파일 완료. Play Mode의 실제 Button 콜백 및 기존 API로 스킬 118항목, 메뉴·설정·특성 15항목, 도감·업적 열기/닫기 8항목 통과. 새 Play 세션에서도 시작 시 모든 화면 닫힘과 스킬 118 / 메뉴·설정·특성 15항목을 다시 통과했다.
- 스킬 검사는 분리된 비영구 `TraitTree`와 임시 해금 표시를 연결해 3→4→5, 5→4→3, 특성 초기화, 잠긴 슬롯 선택·장착 차단, 필터·미저장 확인 취소·재열기를 확인하고 원래 트리/장착 상태로 복원했다. 실제 스킬 저장·해금 확정·계정 특성 초기화는 실행하지 않았다.
- 실제 Trait 40개·계정 8개·실제 SkillData 해금 19개와 이전 미리보기 해금 키 19개, 총 PlayerPrefs 86키를 검사 직전/직후 대조해 모두 유지됨을 확인했다. 최초 Play 진입 전 비교에서는 기존 자동 저장의 시각만 갱신됐으며 실제 SP·LP·XP·레벨·장착·특성 값은 유지됐다.
- 최종 Edit Mode의 Scene/부모 Prefab에서 Missing Script·끊어진 참조 0개, Console 오류·경고 0개. 실제 마우스/키보드 입력·ESC, 비 16:9 배치, 플레이어 빌드는 미확인이다. Game View 캡처나 화면 제어는 사용하지 않았다.
- 이번 추가 변경은 `UISkillLoadoutPreview.cs`, `Lobby.unity`, 부모 `Canvas_LobbyOverlays.prefab`, 이 문서이다. 앞선 정리의 도감·업적·업적 카드 Prefab 변경은 보존했다. 새 C#·PNG·`.meta`·ProjectSettings·Packages·팀원 코드는 추가/수정하지 않았다.
- Scene 자동 저장으로 생긴 기존 목록 Grid/Scroll Rect 배치 및 TMP 캐시 override는 작업 전 값·상속 상태로 정리했다. 기존 유효 override 4,516개의 대상·속성·참조·순서를 보존하고, 값은 승인한 스킬/설정 시작 활성 2곳만 `1 → 0`으로 변경했다.
- `git diff --check`에서 부모 Prefab의 Unity 자동 직렬화 빈 필드/TMP 공백 9줄이 보고된다. Scene/Prefab 텍스트 직접 편집 금지 규칙에 따라 손으로 지우지 않았으며 기능 오류와 구분한다.
- 작업 전 미저장 Scene 복사와 Prefab/C#/문서 백업은 Git 제외 `Assets/98.ExternalAssets/00.LocalStaging/UI_Cleanup_Backups/20261006_SkillSlotLocks_01`에 보관한다. 충돌 주의 파일은 위 C#·Lobby Scene·부모 Prefab이며 커밋/푸시는 수행하지 않았다.

## 스킬 슬롯 리볼버 배치 — 2026-10-06

- 후속 승인에 따라 `Canvas_SkillSettings`의 슬롯 5개를 모두 `220×220`으로 통일했다. 위 원형 슬롯·잠금 표시 작업에서 보존했던 서로 다른 크기와 삼각형 배치를 이번 작업에서 변경했다.
- 슬롯 인덱스 0~4는 상단부터 시계방향 `90°, 18°, -54°, -126°, 162°`에 배치한다. 중심 `(0,-155)`, 반지름 `235`, 간격 `72°`이다. 클릭 영역과 좌우 패널·상단 장착 개수 문구가 겹치지 않도록 간격을 확보했다. 슬롯 자체는 회전하지 않고 아이콘과 이름을 똑바로 표시한다.
- 기존 `EquippedConnector` 체인 Sprite는 그대로 사용하고 `470×470`, 같은 중심으로 맞췄다. 아이콘/잠금 `99×99`, Tint `121×121`, 이름판 `171.6×43`, 이름 `145.2×34`와 궁극기 장식도 슬롯별 동일 규격으로 맞췄다. 이름의 기존 폰트·23 크기는 유지한다.
- 변경 대상은 Lobby Scene과 공용 부모 `Canvas_LobbyOverlays.prefab`의 슬롯·자식·체인 RectTransform이다. 슬롯 배열 순서·Button 연결·Sprite·Material·색·잠금·특성 확장·장착·저장 기능은 유지한다. 이번 후속 작업에서는 C#·게임 데이터·새 아트·다른 Scene 파일을 변경하지 않았다.
- Scene의 옛 슬롯 크기 override를 함께 갱신했다. 프리팹 갱신 후 이름판 등이 씬에서 두 번 비례 축소된 신규 override 30개는 공식 `RevertPropertyOverride`로 제거해 원본 규격을 상속하도록 정리했다. 부모 전체 Apply, Scene/Prefab 텍스트 편집, 내부 PrefabInstance 배열 편집은 사용하지 않았다.
- 독립 디스크 대조: 원본 변경은 대상 RectTransform 38개뿐이며 추가/삭제 오브젝트와 대상 밖 변경은 없다. Scene 유효 override 4,516개의 대상·순서·참조를 보존하고, 승인된 RectTransform 위치·크기 39개 값만 변경했다. 자동 Grid/TMP 기록과 끊어진 참조는 추가되지 않았다. 기존 정리/잠금 기능 변경은 보존했다.
- Edit Mode와 Play Mode 각각 배치 38항목 통과: 동일 크기·72도·동심 체인·자식 규격·클릭 영역 및 패널/문구 비겹침. Play Mode에서 기존 스킬 기능 118항목과 메뉴/설정/특성 15항목을 다시 통과했고, 스킬명 19개가 이름표 안에 들어가는지 TMP 선호 크기로 확인했다.
- 테스트 직전/직후 PlayerPrefs 86키는 모두 동일했다. 실제 저장·해금 확정·계정 특성 초기화는 실행하지 않았다. Console 게임/컴파일 오류 0개이며, Play 진입 때 MCP WebSocket 초기화 경고 1개가 있었으나 이후 도구 호출과 검증은 정상 완료했다.
- 실제 마우스/키보드 입력·Game View 시각 캡처·비 16:9 화면·플레이어 빌드는 미확인이다. 종료 상태는 Edit Mode이며 두 파일을 저장했다. 작업 전 백업은 Git 제외 `Assets/98.ExternalAssets/00.LocalStaging/UI_Cleanup_Backups/20261006_SkillRevolver_01`에 보관한다. 커밋/푸시는 수행하지 않았다.

## 확장 슬롯 하단 배치·이름표 크기 통일 — 2026-10-06

- 후속 요청에 따라 기본 슬롯 1~3을 위·오른쪽 위·왼쪽 위에, 특성 확장 슬롯 4·5를 왼쪽 아래·오른쪽 아래에 배치했다. 슬롯 3과 5의 위치만 교환했으며 배열 순서와 Button/장착 연결은 유지한다. 특성 ‘추가 지령’ 1단계는 왼쪽 아래, 2단계는 오른쪽 아래를 연다.
- 부모 `Canvas_LobbyOverlays.prefab`과 `Builds/Lobby`에 반영했다. 다섯 이름표의 폰트 크기를 모두 23으로 통일해 ‘빈 슬롯’과 ‘잠긴 슬롯’도 동일하게 표시한다. 220×220 슬롯·반경 235·체인·이름판 규격·기존 색상은 유지했다.
- 백업 대비 원본 변경은 슬롯 RectTransform 2개와 이름표 TMP 2개뿐이다. Scene의 유효 override 4,516개 대상·순서·참조를 유지했으며 위치 2개 값만 변경되고 나머지는 원본을 상속한다. C#·게임 데이터·아트·다른 Scene·추가/삭제 오브젝트 변경은 없다.
- Edit/Play Mode 배치 검사 각 46항목, Play Mode 잠금·특성 확장·장착·재잠금·뒤로가기/재열기 검사 156항목 통과. 19개 스킬명과 빈/잠긴 문구 2개의 TMP 선호 크기가 이름표 내부에 들어가는지 확인했다. 테스트 직전/직후 PlayerPrefs 86키는 동일했다.
- 최종 Console 오류·경고 및 Missing Script/끊어진 참조 0개. Edit Mode로 종료하고 Scene/원본을 저장했으며 스킬·설정창은 시작 시 닫힘이다. 실제 마우스/키보드·Game View 캡처·비 16:9·플레이어 빌드는 미확인이다. 기존 Unity 직렬화 빈 필드의 Git trailing-whitespace 경고는 `.prefab` 텍스트를 수정하지 않고 유지한다.
- 작업 전 백업은 Git 제외 `Assets/98.ExternalAssets/00.LocalStaging/UI_Cleanup_Backups/20261006_SkillBottomLocks_01`에 보관한다. 기존 정리·기능 수정은 보존했고 커밋/푸시는 수행하지 않았다.

## 잠긴 장착 슬롯의 알파 유지·색상 처리 — 2026-10-06

- 잠긴 4·5번 슬롯의 Button `disabledColor.a = 0.5019608`이 `FrameArt` 렌더링에 추가로 곱해져 배경이 비치던 원인을 수정했다. 두 Button의 비활성 색은 `Color.white`로 바꿔 추가 알파 감쇠를 제거하고, 프레임 `Image.color`로만 잠금 상태를 어둡게 표시한다.
- `UISkillLoadoutPreview`에 Inspector 조정용 `_lockedEquippedFrameColor`를 추가했다. 기본 RGB는 0.5이며 Heraldry 슬롯의 잠금 시에만 적용하고, 열리면 `Color.white`로 복원한다. 프레임 알파는 양쪽 상태 모두 1로 유지한다. 잠금/빈 프레임의 기존 무채색 Material과 Button 호버/선택 전환은 유지한다.
- 공용 부모 Prefab의 두 Button·두 FrameArt와 표시 컴포넌트의 새 기본 필드, 총 5개 컴포넌트만 변경했다. Lobby는 원본을 상속하며 디스크 내용과 기존 override 4,516개가 백업과 동일하다. C# 변경은 표시 색상뿐이며 배치·문구·프레임 크기·특성·장착·SP·해금·저장 계산은 변경하지 않았다.
- Play Mode 검사 240항목과 배치 검사 46항목 통과. 3→4→5 확장 및 감소/초기화 시 색상 복원·재잠금, Image/CanvasRenderer 알파 1, 입력 차단·장착·재열기를 확인했다. 실제 스킬 버튼으로 재열었을 때 잠긴 두 슬롯은 Image `(0.5,0.5,0.5,1)`, CanvasRenderer `(1,1,1,1)`, 자물쇠 표시 상태였다. 테스트 전후 PlayerPrefs 86키는 동일했다.
- 원본 PNG는 승인 범위대로 수정하지 않았다. 프레임 PNG 내부 원본 알파 252~254/255는 그대로이며, 이번 작업에서 제거한 것은 Button이 추가로 적용하던 약 50% 투명도이다. 전체 이미지 픽셀의 완전 불투명 보정은 별도 작업이다.
- 백업은 Git 제외 `Assets/98.ExternalAssets/00.LocalStaging/UI_Cleanup_Backups/20261006_SkillLockedOpacity_01`에 보관한다. Scene/Prefab 텍스트 직접 편집·부모 전체 Apply·새 C#·팀원 코드·아트·ProjectSettings·Packages·다른 Scene 변경 및 커밋/푸시는 수행하지 않았다. 실제 화면 캡처·하드웨어 입력·비 16:9·플레이어 빌드는 미확인이다.

## 프레임 PNG 내부 알파 보정 — 2026-10-06

- 버튼/이미지 알파 처리 후에도 사슬이 미세하게 비치는 원인은 `Heraldry_Skills_v1/Sprites/skill_frame_damage.png` 자체의 내부 알파 252~254/255였다. 사슬은 슬롯보다 뒤에 있으며 Image·CanvasRenderer·Button·CanvasGroup 알파는 모두 1임을 확인했다.
- 별도 사용자 승인으로 PNG 한 개의 안전한 닫힌 내부 알파만 255로 직접 보정했다. 기존 `Tools/Art/Sources/CombatRoundHud_v1/NormalizeInternalAlpha.ps1`를 대상·임시 출력 경로만 메모리에서 바꿔 재사용했다. threshold 200, 외곽 보호 erosion 3px, 보정 723,626픽셀이다. 재생성·RGB/해상도 변경·새 스크립트 생성은 하지 않았다.
- 독립 검증: 전체 RGB 변경 0, 보호된 외곽/안티앨리어싱 알파 변경 0, 완전투명 824,239픽셀 RGBA 동일, 전후 1254×1254. 중심 `(627,640)` 반경 350의 384,765픽셀은 모두 알파 255이다. Unity의 임포트된 RGBA32 텍스처를 GPU에서 읽어도 같은 내부 영역 알파 255를 확인했다.
- 기존 Sprite 참조로 다시 임포트해 잠긴 슬롯뿐 아니라 같은 PNG를 사용하는 공격 슬롯·상세 프레임에도 반영된다. C#·Scene·Prefab·Sprite asset·`.meta`·Shader·게임 데이터·다른 PNG 변경은 없다. 기존 작업 트리의 변경은 보존했다.
- Play Mode 표시/해금/재잠금/장착/뒤로가기·재열기 검사 240항목 통과, 테스트 전후 PlayerPrefs 86키 동일. 최종 Edit Mode에서 Missing Script/끊어진 참조 및 Console 오류·경고 0개, Scene clean/스킬창 시작 닫힘이다. Game View 캡처·실제 하드웨어 입력·비 16:9·플레이어 빌드는 미확인이다.
- 변경 전 PNG와 검증 임시 이미지는 Git 제외 `Assets/98.ExternalAssets/00.LocalStaging/UI_Cleanup_Backups/20261006_SkillFrameAlpha_01`에 보관한다. 원본 SHA256 `4B28EBDAB0F47176221C1AFE1447DD3D15C643C901DD06E8FD23586FAB16D06C`, 최종 `0C3AD52EBB07CA7B449ADCC5E115C464C6DE80B32AE99EACD01B270E86CC26E1`. 커밋/푸시는 수행하지 않았다.

## 적용·검증 기록 — 2026-09-21

아래는 최초 12종 시안 통합 당시의 기록이다. 당시의 중첩 스킬 구조와 검사 개수는 현재 일반 자식으로 관리하는 19종 v2 화면의 구조 또는 검증 결과를 의미하지 않는다.

- Unity MCP로 대상 Scene과 부모 Prefab을 저장했다. 스킬 원본 Prefab은 변경하지 않고 중첩 인스턴스 한 개를 추가했으며, 이전 `Canvas_Popups` 아래에는 새 스킬 화면이 남아 있지 않다.
- 작업 시작 때의 미저장 Scene 변경을 승인 범위에 따라 보존해 저장했다. 통합 전후 기존 부모 인스턴스 override 290개가 모두 유지됐다. 메뉴/특성 조정값을 부모 에셋 전체에 Apply하지 않았다.
- `UILobbyOverlayView`: 스킬/향후 하위 화면 진입, 중복 열기 차단, 닫기 위임만 추가했다. 공용 `UIPopupController`, 스킬 Runtime 데이터, 원본 아트/폰트는 변경하지 않았다.
- 새 Editor 클래스는 통합 조립과 검증 책임을 각각 분리했다. 기존 스킬 생성/검증 도구도 새 부모 Controller를 사용한다. Runtime 코드에 UnityEditor 의존성은 추가하지 않았다.
- Play Mode 자동 검사 **227개 통과**: 통합 33 + 기존 스킬 42 + 분류 표시 76 + 저장/프레임 48 + 정렬/호버 28.
- 가상 Keyboard 이벤트와 실행 순서에 따른 컴포넌트별 Update 호출로 ESC 경로 **6개 통과**: 저장 상태 닫기, 미저장 확인, 확인창 취소, 변경 폐기 후 복귀, 로비 메뉴 열기, 메뉴 닫기. 실제 하드웨어 입력은 별도 확인 항목이다.
- 실제 Game View 렌더 후 카테고리 클릭, 확인창 취소/로비로 클릭, 뒤쪽 UI 차단을 확인했다. 렌더 전에는 Graphic depth가 -1이라 Raycast 검사가 실패할 수 있어 렌더 완료 후 검사했다.
- 비활성 Canvas의 정렬은 직렬화된 `m_OverrideSorting`/`m_SortingOrder`로 설정해 저장을 확인했다. Play Mode에서 스킬 210 / 확인창 211을 검증했다.
- 기존 메뉴/설정/특성, 보존된 구형 팝업 4개의 열기/닫기와 로비 입력 복원 통과. 실제 스킬 PlayerPrefs 값 불변.
- 컴파일 성공, 최종 Console 오류·경고 0개. Overlay 하위 Missing Script/끊어진 참조/TMP 폰트 누락 각각 0개.
- 통합 도구 재실행 시 중복 생성이나 Scene dirty 변경 없음. 종료 상태는 편집 모드, Scene 저장 완료이다.
- 실제 마우스/키보드 수동 조작, 비 16:9 해상도, 플레이어 빌드는 미확인이다. 테스트 캡처는 Git 제외 `Temp/LobbyOverlayChecks`에 보관했다.
- 변경 파일: 대상 Scene, 부모 Prefab, `UILobbyOverlayView`, Editor 통합/검증 도구와 기존 스킬 도구, 두 README, 이 문서 및 Unity가 생성한 신규 Editor `.meta`. 다른 Scene/ProjectSettings/Packages 변경 없음.
- 로컬 `AGENTS.md`에도 이 문서를 참조하도록 기록했다. 해당 파일은 기존 Git 제외 상태를 유지하며, 팀 공유 기준은 이 문서와 두 README이다. 커밋/푸시는 수행하지 않았다.
