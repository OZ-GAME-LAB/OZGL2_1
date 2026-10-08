# 증강 선택 Heraldry UI

## 적용 구조

- 공용 `Overlays_v1/Prefabs/AugmentChoiceCard.prefab`: 600×900 카드. 새 셸에 설명 영역과 구분선을 통합하고, 기존 마름모 배경·중복 설명 패널·구분선 오브젝트를 제거했다.
- 공용 `Overlays_v1/Prefabs/Canvas_AugmentSelection.prefab`: 공통 헤더, 카드 3개(배율 0.7), 전장 보기/선택창 보기 전용 300×72 버튼.
- `Overlays_v1/UIAugmentVisualCatalog.asset`: 실버/골드/플래티넘 셸·문장, 등급색, GradeLabelY=-133, 기존 Heraldry 아이콘 30개 표시 매핑.
- 기존 `UIAugmentSelectionView`, `UIAugmentCardView`, `UIPopupPanel`, `UIPopupController`를 그대로 사용한다. C# 및 증강 추첨·효과·보상·저장 데이터는 변경하지 않았다.
- 런 한정 증강/즉시 효과 라벨은 표시하지 않는다. `_effectText=null`을 유지한다. 설명 패널 통합으로 `_descriptionPanel=null`이며 기존 코드의 null 처리로 안전하다.

## 아트와 글꼴

- `Textures`: 등급별 CardShell/Crest 6종, 공통 Header, 전용 BattlefieldButton. 이미지 생성 도구로 제작했으며 최종 프롬프트는 `GenerationRecords.json`에 기록했다.
- `Sprites`: 본체/헤더/버튼은 투명 여백을 제거한 Sprite rect, Crest는 등급별 아이콘 위치를 맞추기 위해 동일한 1254×1254 전체 rect를 사용한다. 원본 PNG 자체는 자르지 않았다.
- 내부 알파 직접 보정은 사용자 승인 후 수행했다. RGB·외곽 투명 영역·가장자리 안티앨리어싱을 보존하고 어두운 내부의 알파만 255로 올렸다.
- `Fonts`: 현재 30개 증강의 모든 이름/설명과 화면 문구를 포함한 Static TMP atlas. Title/Body는 별도 Symbols fallback으로 U+2212(−)를 표시한다. 실행 시 Git 제외 원본 폰트에 의존하지 않는다.
- **새 증강 문구에 현재 atlas에 없는 문자가 추가되면 글꼴 atlas도 갱신해야 한다.** 현재 문구 검증 범위 밖의 문자는 자동 추가되지 않는다.

## 검증 결과 (2026-10-08)

- 기존 순수 코어 Edit Mode 검증: PASS (추첨·중복/오래된 입력·확정 1회·취소·오류 처리).
- 게임 Bootstrap/RealSynergySync 없는 별도 Play 씬에서 저장된 Prefab을 다시 로드하여 검증했다.
- 각 등급의 같은 등급 후보 3개, 코어가 거부한 선택 유지, 전장 보기/복귀, 후보 재추첨 없음, 숨긴 카드 입력 차단, 첫 카드 포커스 복귀, 필수 선택창 닫기 제한, 확정 1회와 추가 선택 차단: PASS.
- 렌더링 후 실제 GraphicRaycaster의 최상위 적중 대상이 AugmentCard_1임을 확인했다. PointerClick 이벤트 경로로 버튼을 검증했다.
- 현재 증강 30종의 제목·등급·설명에 빈 메시/잘림 없음. 증강 SO 원본 불변 확인.
- 1920×1080 Game View에서 실버/골드/플래티넘과 전장 보기 화면 캡처 확인. Console Error/Warning 0개, Missing Script/누락 참조 없음.
- 전체 전투 진행 및 효과 수치 재검증, 다른 화면비의 물리 마우스 플레이는 이번 검증에 포함하지 않았다.
- 기존 InGame_UIIntegration의 미저장 상태를 백업·보존했다. Scene 원본과 ProjectSettings/Packages는 저장·변경하지 않았다.

## 공유 영향과 주의점

- 공용 Prefab 사용 씬: InGame, InGame_2, InGame_UIIntegration, UI_Battle_MutedPreview, JOB_SEJIN_BossTest. Scene 파일 변경 없이 새 외형이 반영된다.
- **기존 BattleOverlayPrefabBuilder 전체 재실행 금지:** 현재 생성기는 구형 600×1100 카드·EffectPlate·런 한정 증강 문구를 재생성한다. 생성기 현대화는 이번 작업에 포함하지 않았다.
- Scene 전용 연결이나 팀원 코드에서 별도 오버라이드를 적용한 경우 개별 확인이 필요하다.
- Unity가 생성한 YAML 빈 value 필드 2곳은 Git whitespace 검사에 표시된다. 직접 텍스트 편집으로 수정하지 않았다.

## Inspector/재확인 항목

- Selection View의 Cards 3개, Catalog, Popup Panel 및 첫 카드 포커스 연결 유지.
- 전장 보기/선택창 보기의 기존 영구 이벤트 4개씩과 호출 순서 유지.
- Grade는 176×56, 글자 30, 여백 0. 이름·설명·아이콘은 데이터에서 바인딩된다.
- 테스트 후 Play Mode 종료, 선택창 시작 비활성 및 이전 Play Mode 시작 Scene 설정 복원.
- 다음 검토에서는 Prefab 2개와 UI 표시 Catalog만 충돌 주의 대상으로 보고, 기존 C#·게임 SO·Scene 변경이 없는지 확인한다.
