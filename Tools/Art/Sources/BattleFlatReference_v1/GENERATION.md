# Battle Flat Reference v1

## 목적

- 선택된 `04_LevelBar_ChevronStepChain.png`의 전투 HUD를 Unity용 개별 Sprite로 분리한다.
- 기존 UI의 텍스처·금속 얼룩·벨벳 문양·과도한 꼭짓점 장식을 제거한다.
- Runtime 데이터와 Button 이벤트는 그대로 유지하고 시각 리소스만 교체할 수 있게 한다.

## 생성 방식

- OpenAI 내장 `image_gen` 사용
- `SourceKit.png`: 투명 배경의 UI 리소스 보드
- `Tools/Art/PrepareBattleFlatReferenceV1.ps1`: 영역 분리, 투명 여백 정리, 3색+투명 평탄화

## 기준 팔레트

- Charcoal: `#111112`
- Warm Ivory: `#E8D9C4`
- Muted Burgundy: `#7A2E2E`

## 최종 프롬프트 요약

선택된 전투 HUD 레퍼런스를 기준으로 상단 HUD, 웨이브 패널, 시너지 명판, 마름모 프레임, 셰브런 경험치 바, 하단 패널, 전투 시작 프레임과 가격 명판을 각각 분리한 투명 UI 리소스 보드를 생성했다. 텍스트·숫자·아이콘은 제외했으며, 무광 차콜·따뜻한 아이보리·저채도 버건디만 사용하고 텍스처, 노이즈, 스크래치, 광택, 그라데이션, 리벳, 문양, 글로우를 금지했다.

## 입력 레퍼런스

- `Assets/98.ExternalAssets/00.LocalStaging/게임랩2기1팀_사용한 리소스 팩/02_생성한 레퍼런스 이미지/2026-10-01_InGameLevelBar_4Variants/04_LevelBar_ChevronStepChain.png`
- 로비 마름모 메뉴 레퍼런스: `exec-e1b621cc-4252-4c72-94a8-f38e973fe969.png`
