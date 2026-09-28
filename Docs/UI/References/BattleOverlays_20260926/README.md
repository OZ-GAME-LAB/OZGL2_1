# 전투 오버레이 레퍼런스 12종

2026-09-26 · 레벨업 / 전투 승리 / 전투 패배 각 4종

## 비교

같은 번호는 같은 디자인 계열이다. 각 이미지는 독립된 전체 화면 PNG이며 레퍼런스 전용이다.

| 번호 | 방향 | 레벨업 | 승리 | 패배 |
| --- | --- | --- | --- | --- |
| 01 | Gothic Metal — 기존 고딕 금속 테두리와의 연결감 | [이미지](LevelUp_01_GothicMetal.png) | [이미지](Victory_01_GothicMetal.png) | [이미지](Defeat_01_GothicMetal.png) |
| 02 | Noble Velvet — 짙은 벨벳과 고금색 장식 | [이미지](LevelUp_02_NobleVelvet.png) | [이미지](Victory_02_NobleVelvet.png) | [이미지](Defeat_02_NobleVelvet.png) |
| 03 | Runic Altar — 마법진과 문장 중심 연출 | [이미지](LevelUp_03_RunicAltar.png) | [이미지](Victory_03_RunicAltar.png) | [이미지](Defeat_03_RunicAltar.png) |
| 04 | Minimal Obsidian — 장식을 절제한 패널과 정보 위계 | [이미지](LevelUp_04_MinimalObsidian.png) | [이미지](Victory_04_MinimalObsidian.png) | [이미지](Defeat_04_MinimalObsidian.png) |

## 참고한 원본과 범위

- [Figma 레벨업 특성 선택](https://www.figma.com/design/Kz3TNcpjEhLXT7lGoCWinf?node-id=95-300): 세 개 선택지, 마름모 아이콘, 이름·설명·보너스·현재 레벨.
- [Figma 난이도 클리어](https://www.figma.com/design/Kz3TNcpjEhLXT7lGoCWinf?node-id=95-889): 난이도, 클리어 시간·처치 용사 수·유닛 배치 수, 경험치·레벨·경험치 바, 로비로.
- [Figma 난이도 패배](https://www.figma.com/design/Kz3TNcpjEhLXT7lGoCWinf?node-id=95-900): 노드 구조는 확인했으나 Figma Starter MCP 한도로 스크린샷을 받지 못했다. 승리와 공유하는 결과 컴포넌트 및 Unity의 Popup_Defeat 구성으로 보완했다.
- 전투 스타일: `../BattleHUD_20260923/04_Unified_GothicCommandPanel_v3.png`.
- Unity의 `UI_Battle`, `UI_Battle_MutedPreview` 내 증강·승리·패배 팝업의 정보 항목을 읽기 전용으로 대조했다.
- 레벨업 카드 이름·설명·수치, 결과 통계와 경험치 수치는 디자인 비교용 예시다. 실제 스킬·증강·전투 밸런스 값이 아니다.
- 모든 화면을 기존 전투 UI 위 어두운 오버레이로 표현했다. 승패의 유일한 동작 버튼은 `로비로`이며 새 기능을 추가하지 않았다.
- 이번 작업은 레퍼런스 생성과 보관만 수행했다. Unity Scene / Prefab / Meta / 코드 / ProjectSettings / Packages 및 Figma 원본은 수정하지 않았다.

## 생성 및 확인

- built-in `image_gen` 사용. 각 화면·방향마다 별도의 프롬프트로 생성했다.
- 원본은 생성 위치에 보존하고 결과 PNG를 이 폴더에 복사했다.
- [레벨업 프롬프트](LevelUp_Prompts.md), [승리 프롬프트](Victory_Prompts.md), [패배 프롬프트](Defeat_Prompts.md)에 전체 프롬프트와 확인 내역을 기록했다.
- 각 안의 선택지/통계 항목, 글자 배치, 주요 프레임 및 배경 대비를 시각 확인했다. 게임 실행 및 상호작용 테스트는 이번 이미지 작업에 해당하지 않는다.
- 이미지 속 글자·아이콘은 레퍼런스용 래스터다. 구현 시 변경되는 문구·수치는 Unity Text/TMP로 분리하고, 시간 등 의미가 정해진 아이콘은 전용 스프라이트를 사용해야 한다.
