# GridManager 프로토타입 검증 기록

이 문서는 9월 9일의 기물/모양 결합형 구현에 대한 과거 기록입니다. 현재 블록/유닛 분리 구조와 검증 결과는 GridManager-TeamGuide.md 및 GridManager-Verification-2026-09-10.md를 참조하세요.

Unity 6000.3.22f1, feat/grid-manager/5, JOB_KIMGUN에서 검증했습니다.

## 결과

| 검사 | 결과 |
| --- | --- |
| 스크립트 컴파일 | 오류 없음 |
| 최종 Console / 메타 | 오류·경고 0, 신규 에셋·폴더 meta 누락 없음 |
| 6종 × 4방향 | 초기 영역 배치 및 네 번 회전 복귀 통과 |
| 점유 판정 | 바닥 없음·경계 이탈·타 기물 겹침 차단, 자기 기존 점유 허용 통과 |
| 드래그 취소 | 원래 위치·회전 유지, 실제 점유 선반영 없음 통과 |
| 카드 복귀 | 점유만 해제, 바닥 보존 통과 |
| 확정 이벤트 | 미리보기 중 LayoutChanged 미발생, 배치 성공 후 1회 발생 통과 |
| 마왕 영역 | 별도 위치 (3.5,-1), 일반 기물 배치 차단 및 시작 인원 제외 통과 |
| 준비 단계 | 최초 스킵 금지, 일반 기물 1기 필요, 드래그 중 진행 금지 통과 |
| 필수 확장 | 배치 전 시작/스킵 금지, 취소해도 필수 조건 유지 통과 |
| 확장 연결 | 겹침·분리·대각선만 연결 차단 통과 |
| 확장 상한 | 회전한 1×2 조각 14회로 40칸 채우기 통과 |
| 확장 후보 | 만원 및 고립된 빈칸 배치 불가 판정 통과 |
| Play UI 입력 | PointerDown/Move/Up, R/Esc로 카드 배치·회전·무효 드롭·트레이 복귀·확장 통과 |
| 고스트 표시 | 초록/빨강 반투명 색상 검사 및 Game View 캡처 확인 |
| 전투 중 조작 | 기물 드래그 및 확장 차단 통과 |
| 씬/프리팹 | Missing Script 0, 카탈로그·패널 테마 참조 확인 |

규칙 검사: GridVerification.LastResult = PASS.
Play 입력 검사: GridPlayVerification.LastResult = PASS.

## 시각 확인 및 수정

1920×1080 Game View에서 초기/확장된 바닥, 하단 중앙 카드, 독립 마왕 칸, ㄱ자 초록 고스트, 확장 빨강 고스트, 점선 배치 후보를 확인했습니다. L자 4칸 아이콘의 카드 밖 넘침을 수정한 뒤 재검사했습니다.

Editor가 백그라운드일 때 Game View가 초기 프레임에 멈추어 있어 캡처 검증 동안만 Application.runInBackground를 활성화했고, 이후 false로 복원했습니다. ProjectSettings의 해당 설정은 변경하지 않았습니다.

캡처는 Library/GridVerification에 있으며 Git에서 제외됩니다. Unity가 생성한 기본 UI Toolkit 테마는 AssetDatabase.MoveAsset으로 Assets/_Project/UI 아래로 옮겨 GUID와 참조를 유지했습니다.

## 검증 한계 및 남은 연결

UI 테스트는 Unity UI Toolkit 이벤트를 실제 콜백에 전달합니다. OS 마우스/키보드 장치 자체를 조작한 수동 QA나 여러 해상도·빌드 플랫폼 검사는 수행하지 않았습니다.

현재 캐릭터는 기준 칸의 점·이름으로 표시하며 실제 기물 프리팹은 미연결입니다. 전투/보상 버튼은 흐름을 재현하는 더미입니다. StageManager·실제 SO·전투 위치 변환·실제 준비씬 전환·저장 통합은 병합 후 검증해야 합니다. 합성·시너지 효과는 구현하지 않았습니다.

기존 StageManager 브랜치 작업은 변경하지 않았습니다. Notion 수정, 커밋, Push, 병합은 수행하지 않았습니다. Unity가 생성한 ProjectSettings/SceneTemplateSettings.json은 기능 변경 파일에 포함하지 않으며 커밋에서 제외해야 합니다.

git diff --check는 Unity가 직렬화한 씬의 빈 m_Name 뒤 공백 1건을 보고합니다. 씬을 외부 텍스트 편집기로 수정하지 않았습니다.
