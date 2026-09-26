# 결과창 독립 아트 8종 정리 기록

2026-09-26. built-in image_gen으로 각 자산을 1회씩 생성하고 원본을 `Raw/`에 보존했다. 생성 원본과 보존본 8쌍의 SHA-256 일치를 확인했다. 정확한 생성 프롬프트는 `Prompts.md`에 기록했다.

## 최종 파일

최종 경로: `Assets/06.UI/BattleMutedPreview/Overlays_v1/Result/`.

| 파일 | 캔버스 | 가시 영역 | 용도 |
| --- | --- | --- | --- |
| Result_RecordPanel.png | 1200×400 | 1160×290 | 실버 공통 기록판, 검은 몸체 포함 |
| Result_CrestFrame.png | 512×512 | 464×464 | 중앙이 빈 공통 마름모 프레임 |
| Result_RuneRing.png | 512×512 | 463×464 | 중앙이 빈 중립 ivory 룬 링 |
| Result_Crown_Victory.png | 512×512 | 432×312 | 온전한 고금색 왕관 |
| Result_Crown_Defeat.png | 512×512 | 432×315 | 균열·변색 실버 왕관 |
| Result_Banner_Victory.png | 384×768 | 330×719 | 온전한 천·깃대·촛대 |
| Result_Banner_Defeat.png | 384×768 | 330×720 | 해진 천·같은 기준 깃대·촛대 |
| Result_TitleAccent.png | 768×256 | 720×158 | 제목 뒤에 놓는 중립 회색 장식 |

## 후처리 범위

- `PrepareResultArt.ps1`은 기존 `Tools/Art/PrepareBattleReferenceArt.ps1` 및 `PrepareBattleCardArt.ps1`의 연결영역 방식과 비례 리사이즈 방식을 재사용한다.
- 원본은 모두 RGB 체크 배경이었다. 외부와 연결된 중립 회색 배경만 제거했고, 빈 마름모/링 중앙과 왕관 구멍·체인·찢어진 천 안쪽의 체크는 지정한 안전 시드로 제거했다.
- 중립 배경의 밝기 하한 102, RGB 차이 상한 10을 사용해 금속의 따뜻한 ivory/silver 강조를 보존했다. 단색 회색 TitleAccent만 밝기 하한 190으로 밝은 체크 배경을 제거했다.
- 작은 배경 잔여물 중 24픽셀 미만의 분리된 원본 컴포넌트만 제거했다. 색상 변경, 새 장식 추가, 형태 재설계는 수행하지 않았다.
- 비례를 보존한 nearest-neighbor 크기 조정과 투명 여백을 적용했다. 기록판의 실제 생성 비율은 약 4:1이므로 1200×400 캔버스 안에서 그 비율을 유지했다.
- 왕관은 같은 512×512 캔버스, 중심 정렬, 가시 폭 432로 맞췄다. 원본 비율을 유지하여 세로 가시 크기는 3픽셀 차이가 남는다. 두 상태 모두 동일한 432×320 예약 영역 안에 들어간다.
- 깃발은 원본 887×1774 두 장의 합집합 crop과 같은 변환을 사용해 깃대 상단·가로대·장착 기준 위치를 유지했다. 가시 상단은 둘 다 y=24이며, 상태 전환이나 좌우 반전에 공통 캔버스를 사용한다.

## 확인 결과

- `Result_ContactSheet.png`를 어두운 배경에서 확인했다. 기록판 내부는 채워져 있고, 프레임/룬 중심에는 체크무늬가 남지 않았다. 제목 장식의 내부 밝은 체크 잔여물과 약해진 룬 외곽선을 한 번 보정한 뒤 최종 시각 확인했다.
- `AlphaMetrics.txt`: 전부 캔버스 끝 불투명 픽셀 0. 기록판 centerAlpha=255. 마름모·룬 링 centerAlpha=0.
- 글자·수치·경험치 바·내부 구분선은 8종 생성 자산에 들어 있지 않다. 배너의 작은 추상 금색 문양은 정적 장식이다.
- 원본 8개, 최종 8개, 재현용 정리 스크립트, 프롬프트/알파 검증 문서, 1개의 최종 contact sheet를 보존했다.

## Unity 적용 시 확인

- 아직 Sprite importer, Scene, Prefab, `.meta`, Runtime/Editor C# 또는 ProjectSettings는 수정하지 않았다. Unity MCP가 없어 Unity Console/Play Mode 검증은 미확인이다.
- 왕관·문장·룬은 같은 PPU/중심 pivot, 깃발은 같은 PPU/공통 장착 앵커를 사용한다. 이미지 raycastTarget은 false로 설정한다.
- 프레임 장식은 비율을 유지한다. 기록판을 9-slice로 확장하려면 코너와 보석이 늘어나지 않는 border를 Inspector에서 검토한다.
- TitleAccent는 중립 회색, RuneRing은 중립에 가까운 ivory 원색을 유지했다. 승리/패배 tint 강도는 실제 화면에서 확인한다.
- 이 작업은 버튼, 경험치 프레임, 배치 아이콘과 독립적이며, 다른 담당자가 생성하는 해당 파일을 건드리지 않는다.
