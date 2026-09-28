# 전투 결과 · 레벨업 증강 아트 리소스 v1

2026-09-26. 승인된 통일안에서 **개별 PNG 아트만 제작**했습니다. Unity Scene/Prefab 배치, TextureImporter 설정, 게임 기능 연결은 아직 하지 않았습니다.

## 저장 위치

- 최종 PNG 19종: [Overlays_v1](../../../../../Assets/06.UI/BattleMutedPreview/Overlays_v1)
- 생성 원본: 이 폴더의 각 `Raw` 하위 폴더. 생성 이미지 원본은 변경하지 않았습니다.
- 기준 이미지: [승리](../Revision02/UnifiedResult_Victory.png), [패배](../Revision02/UnifiedResult_Defeat.png), [최종 증강 등급](../Revision06/Rarity_PlatinumRefined.png)
- 생성 방식: **built-in imagegen**. API/CLI fallback이나 별도 패키지 설치는 사용하지 않았습니다.
- 승인된 후처리: 실제 체크무늬 배경 제거, 투명 여백, 크기·중심 정렬. System.Drawing 기반 재현 스크립트로 처리했습니다. 색을 바꾸거나 새 문양을 코드로 그리지 않았습니다.

## 증강용 PNG — 8종

후속 작업에서 검정 설명 패널과 벨벳 배경을 별도 PNG로 분리했습니다. 기존 8종은 보존되며, 분리 사용 시 [Production02 설명](../Production02_DescriptionSplit/README.md)을 따릅니다.

최종 경로: `Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment/`

| 파일 | 크기 | 사용 |
| --- | --- | --- |
| Augment_Body_Silver.png | 600×1100 | 수수한 벨벳 몸체. 양쪽 실장식 없음 |
| Augment_Body_Gold.png | 600×1100 | 고금색 외곽·벨벳 문양·실장식 |
| Augment_Body_Platinum.png | 600×1100 | 백금 외곽·작은 보석·가는 보조선·실장식 |
| Crest_Silver.png | 512×512 | 단정한 빈 마름모 + 빈 등급 명패 |
| Crest_Gold.png | 512×512 | 월계 장식 빈 마름모 + 빈 등급 명패 |
| Crest_Platinum.png | 512×512 | 왕관·날개 장식 빈 마름모 + 빈 등급 명패 |
| Augment_TitlePlate.png | 1024×320 | 문구 없는 레벨업 제목판 |
| Augment_EffectPlate.png | 512×144 | 문구 없는 효과 수치판 |

문장과 등급 명패는 원본에서 겹쳐 있어 **한 장으로 유지**했습니다. 몸체, 문장/명패, 실제 증강 아이콘, 효과판, Text/TMP는 독립 레이어로 배치합니다.

- 몸체는 캔버스와 외곽 여백을 통일했습니다. 동일 RectTransform으로 교체할 수 있습니다.
- 문장 3종의 **아이콘 기준점은 (256,220), 투명 중앙 너비는 200px**입니다. 위쪽이 이미지 y=0인 픽셀 좌표입니다. 흰색 증강 아이콘을 별도 Image로 중앙에 삽입합니다.
- 실제 명패 형태와 원본 비율을 보존했으므로 **등급명 텍스트는 각각 안전 영역에 정렬**합니다. [정규화/안전 영역](AugmentCrests/Normalization.md).
- 몸체와 문장을 하나의 이미지로 합치지 않았으며, 아이콘은 추후 증강별로 교체합니다.
- 현재 실제 추첨은 같은 등급 후보 최대 3개입니다. 등급별 리소스 3종은 이 추첨 규칙을 바꾸지 않습니다.
- 호버·선택 표현은 등급 장식을 바꿔 그리지 말고 별도 UI 상태 레이어로 구현합니다.

## 결과 화면용 PNG — 11종

최종 경로: `Assets/06.UI/BattleMutedPreview/Overlays_v1/Result/`

| 파일 | 크기 | 공유/교체 |
| --- | --- | --- |
| Result_RecordPanel.png | 1200×400 | 공통 검은 기록판 + 실버 외곽 |
| Result_CrestFrame.png | 512×512 | 공통 빈 마름모 문장 틀 |
| Result_RuneRing.png | 512×512 | 공통 중립색 룬 링, UI에서 상태별 색 지정 가능 |
| Result_Crown_Victory.png | 512×512 | 승리: 온전한 고금색 왕관 |
| Result_Crown_Defeat.png | 512×512 | 패배: 균열·빛바랜 왕관 |
| Result_Banner_Victory.png | 384×768 | 승리: 온전한 천·깃대·작은 촛대 |
| Result_Banner_Defeat.png | 384×768 | 패배: 해진 천·깃대·작은 촛대 |
| Result_TitleAccent.png | 768×256 | 공통 중립색 제목 뒤 장식 |
| Result_LobbyButton.png | 640×240 | 공통 빈 벨벳 버튼, 기본 상태 |
| Result_ExperienceFrame.png | 768×128 | 공통 경험치 외곽, 중앙 투명 |
| Icon_Deployment.png | 256×256 | 배치 수 표시용 체스 말 |

- 기록판·버튼은 승리/패배 모두 **동일 PNG**를 사용합니다.
- 왕관은 동일 캔버스·중심·가시 폭, 깃발은 동일 부착 기준으로 정렬했습니다. 원본 비율을 보존해 가시 높이에는 수 px 차이가 있습니다.
- 깃발은 깃대·촛대가 포함된 한쪽 묶음입니다. 반대쪽에는 좌우 반전해 사용합니다. 천만 독립 교체하는 레이어는 이번 묶음에 포함하지 않았습니다.
- 룬/제목 장식은 기본 중립색입니다. 구현 단계에서 고금색/어두운 적색을 지정합니다.
- 경험치 **Frame만 PNG**입니다. Track/Fill/눈금은 기존 단색 스프라이트를 별도 Image/Slider로 배치합니다. Fill에 프레임을 같이 넣지 않습니다.
- 버튼 Hover/Pressed는 이번에 별도 PNG로 만들지 않았습니다. 기본판을 유지한 UI 색상/명도 또는 별도 상태 레이어로 연결합니다.

## 재사용할 기존 리소스

새 파일로 복제하거나 원본을 변경하지 않았습니다.

| 용도 | 기존 경로 |
| --- | --- |
| 시간 아이콘 | Assets/06.UI/BattleMutedPreview/Sprites/Icon_Hourglass.png |
| 처치 수 아이콘 | Assets/06.UI/BattleMutedPreview/Sprites/Icon_Skull.png |
| 통계 아이콘 마름모 | Assets/06.UI/BattleMutedPreview/Sprites/Frame_DiamondNeutral.png |
| 통계 구분선 | Assets/06.UI/BattleMutedPreview/Sprites/Divider_Vertical.png |
| 증강 예시 검 아이콘 | Assets/06.UI/BattleMutedPreview/Cards_v1/Sprites/Icon_Sword.png |
| XP Track/Fill/눈금용 단색 | Assets/06.UI/BattleMutedPreview/Reference_v2/Experience_White.png |

## Text/TMP로 유지할 내용

레벨업 제목·선택 안내, 등급명, 증강명·설명·효과량, 승리/패배 제목, 난이도, 시간·처치·배치 수, 경험치·레벨·레벨업 안내, 로비로 버튼 문구를 이미지에 넣지 않았습니다. 이 값들은 실제 데이터와 Text/TMP를 연결해야 합니다. 새로운 게임 데이터·보상·추첨 로직은 만들지 않았습니다.

## Unity 적용 시 확인

아직 설정하거나 검증하지 않은 **차후 작업 체크리스트**입니다.

- Texture Type: Sprite (2D and UI), Sprite Mode: Single.
- Alpha Is Transparency, Mip Maps off, Filter Mode Point, Compression None을 우선 확인.
- 공통 PPU(예:100), Mesh Type Full Rect, Pivot (0.5,0.5). 빈 중앙/투명 여백을 자동 Tight Crop하지 않습니다.
- 원래 PNG 비율을 유지합니다. 전체 문장/왕관/장식을 9-slice로 늘리지 않습니다. 이번에는 Sprite Border를 설정하지 않았습니다.
- 아이콘·장식 Image의 raycastTarget=false. 클릭 대상은 별도 버튼 RectTransform.
- 등급 이름판의 안전 영역과 증강 아이콘 중심은 위 정규화 문서에 따라 연결.
- XP는 표시용 Slider/Filled Image로 분리, 눈금은 별도 반복 Image.
- Scene/Prefab 적용 전 범위 승인, 누락 참조/기존 버튼 동작/Play Mode/Console은 적용 후 확인.

## 생성 기록 · 검수

- [증강 몸체·제목·효과판·버튼 프롬프트](AugmentBodyAndCommon/Prompts.md)
- [경험치 프레임·배치 아이콘 프롬프트](AugmentBodyAndCommon/AdditionalPrompts.md)
- [증강 문장 프롬프트](AugmentCrests/Prompts.md)
- [결과 화면 프롬프트](Result/Prompts.md)
- [몸체·공용 투명도/크기 기록](AugmentBodyAndCommon/Validation.txt)
- [증강 문장 정규화](AugmentCrests/Normalization.md)
- [결과 화면 투명도/크기 기록](Result/AlphaMetrics.txt)

생성 원본과 투명화된 출력은 구분해 보관합니다. 실제 alpha·중앙 구멍·여백을 검사하고 어두운 배경에서 각 자산 묶음을 확인했습니다. 검수 중 발견한 실버 금속/실장식의 배경 제거 손실은 원본에서 다시 처리해 복구했습니다. 스프라이트별 미세한 질감 차이는 유지했습니다.

- [몸체·공용 미리보기](AugmentBodyAndCommon/ContactSheet.png)
- [증강 문장 미리보기](AugmentCrests/Crests_Final_Dark.png)
- [결과 화면 미리보기](Result/Result_ContactSheet.png)

## 변경 범위

- 추가: Overlays_v1 PNG, 이 Production01의 원본·정리 스크립트·프롬프트·검수 문서.
- 기존 코드/Scene/Prefab/ProjectSettings/Packages/기존 아트: 수정하지 않음.
- .meta 직접 작성/수정: 없음. Unity에서 임포트할 때 생성되는 파일은 차후 검토 대상.
- Unity 컴파일/Play Mode/Importer 연결: **미확인**. 이번 작업은 아트 생성 및 PNG 파일 검증 범위.
- Git 커밋/푸시: 하지 않음.
