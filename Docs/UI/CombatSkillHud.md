# 전투 스킬 HUD — 01 Noble Diamond

## 적용 범위

- Scene: `Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity`
- 계층: `UI_BattleScreens/Canvas_Combat`
- 공통 Prefab: `Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Prefabs/CombatSkillSlot.prefab`
- 아트: `Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Sprites/`
- 01번 레퍼런스: `Docs/UI/References/CombatSkillHUD_20260926/01_NobleDiamond.png`

승인된 스킬 3칸과 하단 배경만 변경했다. 준비 화면, 상단 HUD, 시너지, 카드, 중앙 배치 영역, 벽과 임시 테스트 버튼은 변경하지 않았다. 원본 `UI_Battle` 및 로비 Scene도 유지한다. 기존 `BottomStrip`과 `Legacy_SkillSlot_1~3`은 비활성 상태로 보존한다.

현재 구성은 **UI 미리보기**이다. 실제 스킬 발동, 자동 쿨다운 진행, 로비 저장 장착 정보와는 연결하지 않았다. 기존 슬롯과 마찬가지로 Button은 비활성이고 클릭 이벤트는 없다.

## 편집 위치

| 대상 | 편집 위치 | 역할 |
| --- | --- | --- |
| 공통 레이아웃 | `CombatSkillSlot.prefab` | 원본 수정 시 세 인스턴스에 공통 반영 |
| 어두운 마름모 | `Plate` | 단순 Unity Image, 회전 45도 |
| 분류별 내부색 | `CategorySlotTint` | 기존 공용 SO/Material, 아이콘 원본은 보존 |
| 분류 테두리 | `FrameArt` | 3종 Sprite를 카탈로그 분류에 따라 교체 |
| 원형 바탕 | `CooldownTrack` | 분류색의 어두운 원형 트랙 |
| 남은 비율 | `CooldownFill` | 밝은 금속색, Filled / Radial360 / Top / Clockwise |
| 스킬 그림 | `Icon` | 기존 화염구·강철 피부·시간 정지 아이콘 재사용 |
| 남은 초 | `CooldownSeconds` | TMP, 올림한 초 표시, 준비 완료 시 숨김 |
| 하단 벨벳 | `Canvas_Combat/BottomSkillBackground` | 단일 PNG, Image Color 기본 RGB 0.6 / A 1 |

숫자/스킬 이름은 이미지에 합치지 않는다. 아이콘 본체는 공용 스타일의 흰색을 유지하고 분류색은 외곽선·옅은 내부색으로 구분한다. 프레임·배경·쿨다운 링은 새 리소스이며 기존 스킬 아이콘/공유 Material/SO는 수정하지 않았다.

세 인스턴스의 기본 중심은 1920×1080에서 `(600,-925)`, `(960,-925)`, `(1320,-925)`이며 위쪽 왼쪽 앵커를 기준으로 한다. 공통 Prefab은 256×256, Scene 인스턴스 배율은 1.125이다. 같은 모양을 다른 색으로 교체해도 외곽 알파 크기 240×240과 중심 pivot이 유지된다.

하단 배경은 아래쪽 가로 Stretch, Size Delta `(-32,246)`, Position `(0,8)`이다. 중앙의 평평한 부분이 올라온 벨벳 패널이며 슬롯 위쪽이 패널보다 돌출되도록 배치했다. 장식 Image/TMP의 Raycast Target은 모두 꺼져 있다.

## 표시 코드와 값 조절

`Assets/00.Project/01.Scripts/UI/UICombatSkillSlotView.cs`는 **표시 책임만 분리한 컴포넌트**다. 실제 전투/저장 시스템을 참조하지 않는다.

- Inspector `Preview Catalog` / `Preview Entry Id`: 카탈로그에서 ID로 아이콘과 분류를 읽는다.
- `Preview Remaining Seconds` / `Preview Duration`: 쿨다운 표시 미리보기 값이다.
- `Cooldown Track Opacity`: 트랙 농도.
- `Cooldown Fill Color` / `Cooldown Fill Opacity`: 진행 링 색상/농도.
- 기본 샘플: 화염구 0/12, 강철 피부 0/25, 시간 정지 8/40. 이는 정지된 아트 미리보기 수치다.

```csharp
view.ShowSkill(entry);       // 아이콘과 분류 변경. 새 스킬은 쿨다운 표시 초기화.
view.SetCooldown(8f, 40f);   // 남은 시간 / 전체 시간 = 0.2, TMP에는 8.
view.ShowSkill(null);        // 빈 슬롯: 무채색 프레임, 아이콘/링/숫자 숨김.
view.UsePreviewValues();     // 외부 표시 상태를 해제하고 Inspector 미리보기로 복귀.
```

`Fill Amount` 자체도 Image에서 조절할 수 있으나, View가 갱신되면 전달받은 남은 시간 비율이 우선한다. 미리보기에서는 위 시간 필드를, 실제 전투 연결 후에는 `SetCooldown`을 사용한다. 1은 쿨다운 전체, 0은 사용 가능이다. 음수/NaN/Infinity/0 이하 전체 시간은 안전 처리한다. 런타임 API 값은 비직렬화 필드에만 보관하며 SO/미리보기 필드에 쓰지 않는다.

편집 모드에서도 표시를 확인할 수 있도록 `OnValidate`는 갱신 요청만 남기고 Canvas 렌더 직전에 반영한다. Update 루프는 없으며 Canvas 이벤트 구독은 비활성 시 해제한다.

## 후속 실제 스킬 연결

- 실제 장착 시스템이 확정되면 선택된 스킬을 명시적으로 UI Entry로 변환해 `ShowSkill`을 호출한다. 현재 로비의 편집중 장착 배열을 저장본으로 간주하지 않는다.
- 실제 `SkillRuntime.RemainingCooldown` / `EffectiveCooldown`을 `SetCooldown`에 전달한다. UI 내부에서 임의로 시간을 진행하거나 실제 SO의 수치를 바꾸지 않는다.
- 실제 `SkillCategory`와 `eSkillPreviewCategory`는 BUFF/DEBUFF 순서가 다르므로 enum 숫자 캐스팅을 하지 않는다.
- 클릭/조준/발동 로직을 연결할 때만 Button과 필요한 Raycast를 별도 활성화한다. 이번 작업에서 시전 기능을 추가한 것은 아니다.

## 생성/후처리 기록

- 내장 `image_gen` 사용. CLI/API fallback은 사용하지 않았다.
- 생성 원본 5개와 전체 최종 프롬프트: `Tools/Art/Sources/CombatSkills_v1/generation.json`
- 원본: 같은 폴더의 `*_source.png`. 원본은 변경하지 않는다.
- 후처리: `Tools/Art/PrepareCombatSkillArt.py`
- 정렬/알파 검사: `Tools/Art/Sources/CombatSkills_v1/normalization.json`

생성 결과의 체크무늬 배경을 제거하고 투명 RGB를 정리했다. 여백/크기/중심 정렬과 좌우 대칭 보정만 수행했으며 코드로 문양을 그리거나 재색칠하지 않았다. 패널 1920×256, 프레임/링 256×256. 모든 아트에 실제 알파, 중심 pivot, FullRect, Point, 무압축, Mipmap 없음, Clamp를 적용했다. `.meta`는 Unity에서 생성했다.

## Editor 도구와 Undo

- 파일: `Assets/Editor/CombatSkillHudBuilder.cs`
- 메뉴: `Tools > OZGL2 > Battle > Build Combat Skill HUD (Noble Diamond)`
- 지정 Scene의 Edit Mode에서만 실행한다. 필수 에셋/대상/중복 이름/기존 Prefab 참조를 먼저 검사한다.
- 승인된 현재 Scene 미저장 변경은 먼저 저장한 뒤 적용한다. 대상 계층 수정은 Undo 지원, 새 폴더/Prefab 생성 및 이미지 import는 Undo 제외다.
- 재실행은 기존 Prefab·생성 슬롯·배경을 재사용한다. 사용자 수정 디자인을 덮어쓰지 않으므로 코드 기본값을 바꿔도 기존 객체에는 자동 소급되지 않는다.
- 기존 임시 UI는 비활성 보존한다. 다른 Canvas는 변경하지 않는다.

## 검증 — 2026-09-26

- Unity 컴파일 완료. 최종 Console Error 0 / Warning 0.
- 세 슬롯의 공통 Prefab 연결, 필수 참조 누락 0, Scene Missing Script 0 확인.
- Sprite 5개의 실제 알파/중앙 pivot/Point/Mipmap Off 설정 확인.
- Play Mode에서 기존 전투 시작 이벤트 호출로 준비 화면 비활성/Combat 활성/새 슬롯 3개 표시 확인. 실제 마우스 입력 전체 테스트는 미실행.
- 3개 View 각각 0, 0.5, 1 상한 비율, NaN/Infinity, 카테고리 교체, 빈 슬롯 표시 검사 통과.
- 1920×1080 화면 확인 후 01 레퍼런스와 슬롯 간격/돌출 높이를 맞췄다. 최종 화면: `Tools/Art/Previews/UI_Battle_CombatSkills_v1.png`.
- 기존 Scene 블록 중 변경은 BottomStrip 활성 상태, 기존 슬롯 3개 이름/활성 상태, Canvas_Combat/SkillSlots의 자식 목록만이다.
- 원본 `UI_Battle.unity`와 `UI_Lobby_MutedPreview.unity`의 SHA256은 작업 전후 동일하다.
- Edit Mode 복귀, 저장 완료, dirty false. 다른 화면비·실제 전투·빌드는 미확인.
- LayerMask/Tag/Collider/Rigidbody/Animator/Input Action 추가 설정은 없다.

## Git / 리뷰 범위

- Scene 변경 Yes: 프리뷰 씬 1개(다른 작업과 충돌 주의).
- Prefab 변경 Yes: 새 공통 스킬 슬롯 1개.
- Meta 변경 Yes: 신규 아트/폴더/코드/Prefab의 Unity 생성 메타.
- ProjectSettings/Packages 변경 No. 기존 Runtime/공유 스킬 데이터/원본 아이콘은 유지한다.
- 새 Runtime View, Editor Builder, PNG 5개, 생성 원본·프롬프트·후처리 스크립트·문서가 추가되었다. 커밋/push는 하지 않았다.
- `git diff --check`의 Scene `value: ` / `m_Name: ` 공백은 Unity 직렬화 출력이다. 이를 제거하려고 Scene YAML을 직접 수정하지 않는다.
