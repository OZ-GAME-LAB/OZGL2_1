# 승리·패배 결과 화면 레퍼런스 정렬

## 범위와 원칙

- 대상: `UI_Battle_MutedPreview > Canvas_Popups > Canvas_BattleVictory / Canvas_BattleDefeat`.
- 공통 원본: `Assets/06.UI/BattleMutedPreview/Overlays_v1/Prefabs/Canvas_BattleResultBase.prefab`.
- Victory/Defeat는 Base의 Variant를 유지한다. 실제 전투 집계·보상·경험치 지급·로비 이동 규칙은 변경하지 않는다.
- 기존 왕관·룬·통계 프레임·버튼 원본은 보존한다. 하단의 짧은 깃발과 금속 받침만 별도 이미지로 보완한다.
- 모든 문자와 숫자는 TMP다. 아이콘, 장식, 프레임은 독립 Image로 유지한다.

## 편집 위치

| Base 하위 경로 | 역할 |
| --- | --- |
| `BackgroundShade` | 전투 화면을 덮는 어두운 따뜻한 색조, Alpha 0.94 |
| `Content/RuneRing`, `CrestFrame`, `Crown` | 중앙 문장 및 상태별 왕관 |
| `Content/BannerLeft`, `BannerRight` | 좌우 깃발·깃대·촛불 |
| `Content/TitleAccent`, `Difficulty`, `ResultTitle` | 제목 뒤 따뜻한 장식과 명조 제목 |
| `Content/RecordPanel` | 시간·처치·배치 3열 및 경험치 한 줄 |
| `Content/RecordPanel/InteriorShade` | 원본 프레임 안쪽 질감만 어둡게 눌러 정보 대비 확보 |
| `Content/BannerTailLeft`, `BannerTailRight` | 기록판 아래로 이어지는 깃발·금속 받침 |
| `Content/LobbyButton` | 기존 로비 복귀 버튼 및 TMP |

1920×1080 기준 중앙축은 X=960이다. 왕관/룬은 화면 Y=200~209, 제목은 Y=486, 기록판은 Y=704, 로비 버튼은 Y=907 부근에 배치한다. 룬 하단 끝과 난이도 문구가 겹치지 않도록 간격을 둔다. 원본 PNG의 투명 여백을 포함한 Rect 크기와 실제 그림 크기를 구분한다. 좌우 장식은 한 Sprite를 반전하여 중심과 높이를 공유한다.

`UIBattleResultView`의 `Title Color`는 공통 아이보리다. `Victory Accent` / `Defeat Accent`는 룬·제목 뒤 장식에만 사용한다. 난이도 명칭은 `Difficulty Accent`로 강조한다. 따라서 제목 TMP의 색만 바꾸면 Refresh 시 다시 `Title Color`가 적용된다.

## 전용 폰트

- 제목: `HS봄바람체2.0.ttf` → `ResultRefinement_v2/Fonts/BattleResultTitle SDF.asset`.
- 항목명·수치·버튼: `KMU80SungkokSerif.otf` → `ResultRefinement_v2/Fonts/BattleResultBody SDF.asset`.
- 기존 `BattleOverlay Pixel`, `BattleOverlay Symbols SDF`와 원본 TTF/OTF는 수정하지 않는다.
- 전용 폰트는 Static SDF다. 숫자·영문·기호와 현재 결과 문구를 포함한다. 새로운 한글 문구는 `Assets/Editor/BattleResultTypography.cs`의 `BODY_CHARACTERS`에 추가하고 폰트 준비 함수를 다시 실행한다. 기존 GUID는 유지한다.
- **SDF 에셋과 meta는 저장소의 기존 ignore 정책 대상이다. 팀 공유 폰트 묶음으로 함께 전달해야 한다.** 원본 meta를 유지해야 Prefab 폰트 참조가 끊기지 않는다. ignore 정책은 이 작업에서 변경하지 않는다.
- 배포용 두 SDF/atlas/material/GUID 묶음: `Tools/Art/Exports/BattleResultFonts_20260928.unitypackage`. 원본 TTF/OTF는 이 묶음에 재배포하지 않았다. 원본 폰트가 필요한 재생성 작업은 기존 팀 공유 원본으로 진행한다.

## 표시 API

- `SetData` / `SetState` / `ConfigureNavigation`은 기존과 동일하다.
- 시간은 `08분 42초`, 경험치는 `2,400 exp`, 레벨은 `LV.20`, 레벨업은 `LV UP!`로 표시한다.
- 경험치 Slider는 표시 전용이며 `NormalizedXp`를 사용한다. 새 UI는 경험치를 지급하지 않는다.
- `_bannerTailLeft/Right`와 `_victoryBannerTail/_defeatBannerTail`은 상태별 하단 장식만 교체한다. 미연결 시 null-safe 처리한다.
- 로비 경로는 씬 인스턴스의 `Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity` 연결을 유지한다.

## Editor 적용 도구

- 메뉴: `Tools > OZGL2 > Battle > Refine Result Reference Layout`.
- 코드: `Assets/Editor/BattleResultReferenceRefiner.cs`.
- 대상 씬의 Edit Mode에서만 실행한다. 미저장 변경이 있으면 디스크 버전과 현재 열린 상태의 사본을 모두 보존한 뒤 원본을 저장한다. Prefab Stage에서는 실행하지 않는다. 초기 생성 도구 `Create And Apply Overlay Prefabs`를 다시 실행하지 않는다.
- 전체 Prefab Revert 대신, 승인된 Content 위치·크기·색·폰트 속성만 Base 상속으로 정리한다. 사용자의 데이터·Navigator·루트 활성 상태는 보존한다.
- 씬 변경은 Undo 지원. Prefab 파일 저장·신규 폰트/아트 import는 Undo 대신 `Tools/Art/Backups/BattleResult_*`의 실행 전 사본으로 복구한다.
- 메뉴 재실행은 현재 미세 조정한 디자인 값을 다시 적용하므로 의도적으로 실행한다. 일상 디자인 편집은 Base의 Inspector에서 진행한다.

## 확인 항목

- Victory/Defeat가 동일한 크기·정렬을 상속하는지, 의도하지 않은 시각 Override가 없는지 확인한다.
- 폰트 atlas/material, 왕관·배너·하단 장식, TMP·Slider·Button 참조를 확인한다.
- 승리/패배 제목, 상태별 강조색과 깃발 교체, 경험치 비율 표시, 로비 복귀 연결을 확인한다.
- 새로운 LayerMask/Tag/Collider/Rigidbody/Animator/Input Action 설정은 필요 없다.
- Git에서 Base/Variant/프리뷰 씬의 충돌 가능성을 확인한다. 기존 공유 폰트 변경은 사용자 작업과 구분한다.

## 확인 결과 (2026-09-28)

- Unity 컴파일 및 Console Error / Warning 0개 확인.
- Base·승리·패배 임시 복제본에서 필수 참조, Missing Script/직렬화 참조, SDF 글리프, 상태별 아트, 데이터 표시 형식, Slider 범위·null 복귀 검사 통과.
- Play Mode에서 승리·패배 팝업 열기와 표시 확인. 로비 버튼 이벤트를 통해 `UI_Lobby_MutedPreview` 이동 확인.
- 스크린샷: `Tools/Art/Previews/ResultRefinement_Victory_Play-1.png`, `ResultRefinement_Defeat_Play.png`.
- 독립 Prefab 표시 검토 메뉴: `Tools > OZGL2 > Battle > Validate Result Reference Prefabs`. `BattleResultReferenceValidation.Render`는 실제 씬을 수정하지 않는 별도 프리뷰다.
- 원본 전투 화면은 현재 씬 상태를 그대로 유지한다. 레퍼런스의 중앙 타일/유닛을 새로 배치하지 않았다.
- 실제 전투 집계·보상 지급 및 다른 화면비는 이번 범위에서 검증하지 않았다.

작업 중 함께 발견된 준비/전투 Canvas 프리팹화와 스킬 슬롯 색상 변경은 별도 사용자 변경으로 보존한다. 전체 씬 Git diff에는 이 변경도 포함되므로 결과 UI 변경과 구분해서 검토한다.

`git diff --check`의 대상 프리팹 검사에서 Unity가 저장한 빈 `m_Name`/`value` 뒤 공백 6곳이 보고됐다. Scene/Prefab 텍스트 직접 편집 금지 규칙에 따라 직렬화 파일을 수동 정리하지 않았다. 코드와 문서에는 해당 공백 문제가 없다.

신규 하단 장식은 built-in image_gen으로 생성했다. 원본, 최종 프롬프트 및 알파 경계 크롭 기록은 `Docs/UI/References/BattleResultRefinement_20260928/UnderframePrompts.md`에 보관한다.
