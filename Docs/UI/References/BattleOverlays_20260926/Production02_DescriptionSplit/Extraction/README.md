# 내부 설명판 추출

원본 `Augment_Body_{Silver,Gold,Platinum}.png`의 검정 설명판, 얇은 등급 테두리, 안쪽 코너 장식, 하단 작은 마름모를 별도 PNG로 추출했다. 벨벳, 외곽 프레임, 실장식, 하단 외부 문양과 큰 보석은 설명판에 포함하지 않았다.

## 파일과 좌표

- 최종 파일: `Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment/Separated/DescriptionPanel_{Silver,Gold,Platinum}.png`
- 추출 마스크: 이 폴더의 `Mask_{Silver,Gold,Platinum}.png`. 흰색 alpha 255 영역이 설명판이며 나머지는 실제 투명이다.
- 캔버스는 원본과 같은 600×1100이며 이동·크롭 재배치·리사이즈하지 않았다. 배경과 설명판에 같은 RectTransform과 중앙 Pivot을 적용하면 기존 위치에서 겹친다.
- 좌표는 왼쪽 위가 원점이며 x는 오른쪽, y는 아래 방향이다.

| 등급 | 설명판 불투명 범위 `(x,y,w,h)` | TMP 안전 영역 `(x,y,w,h)` |
| --- | --- | --- |
| Silver | `(95,232,410,745)` | `(120,275,360,510)` |
| Gold | `(106,229,388,718)` | `(130,270,340,505)` |
| Platinum | `(120,169,359,789)` | `(145,205,310,580)` |

TMP 안전 영역은 테두리와 코너 장식을 피한 권장 영역이다. 글자 크기·문단 간격·줄바꿈은 실제 문구로 별도 검증해야 한다. 세 등급의 원래 판 크기는 달랐으며 이를 강제로 통일하지 않았다.

## 원본 보존과 분리 경계

`ExtractDescriptionPanels.ps1`은 수동으로 확인한 다각형 범위에서 원본 픽셀을 그대로 복사하고, 경계와 연결된 벨벳색 잔여 부분만 마스크에서 제외한다. 색·광택·질감을 다시 그리지 않는다. Gold는 붉은 벨벳과 금속의 어두운 금색을 구분하도록 별도 색상 임계값을 사용한다.

- Silver 작은 마름모 내부의 붉은 보석은 보존했다.
- Gold는 작은 마름모 아래의 세로 연결 줄을 y=947부터 제외했다. 배경 합성 시 이 줄과 외부 문양은 배경에 남는다.
- Platinum 작은 마름모 양옆에서 아래로 이어지는 외부 문양은 제외했다.
- 외곽 벨벳을 마스크 밖에서 유지하는 배경 합성은 다른 단계에서 수행한다. 이 스크립트는 배경을 생성하거나 변경하지 않는다.

## 검증

- PNG 저장 후 다시 읽어서 모든 불투명 설명판 픽셀이 해당 원본 위치의 RGBA와 같은지 검사했다. 차이 0개.
- 최종 PNG와 마스크의 alpha가 모든 픽셀에서 일치한다.
- 원본 3개 SHA256 해시가 실행 전후 동일하다. 실제 해시는 `ExtractionMetrics.txt`에 기록했다.
- TMP 안전 영역은 alpha 255, 설명판 중앙 `(300,550)`은 alpha 255이다.
- 캔버스 테두리와 상단 벨벳 중앙·하단 외곽 보석·좌우 외곽 확인점은 alpha 0이다.
- `DescriptionPanels_Dark.png`와 `DescriptionPanels_Light.png`에서 세 등급의 검정판·얇은 림·코너·작은 마름모를 확인했다. 밝은 바탕에서 눈에 띄는 붉은 벨벳 테두리는 없었다.
- `SourceEdges_*.png`는 분리 경계를 확인하기 위한 원본 확대 참조이며 최종 에셋이 아니다.
- Scene, Prefab, `.meta`, ProjectSettings, Packages, Runtime 코드는 변경하지 않았다. Unity Importer·Play Mode 검증은 수행하지 않았다.

## 재현

프로젝트 루트에서 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Docs/UI/References/BattleOverlays_20260926/Production02_DescriptionSplit/Extraction/ExtractDescriptionPanels.ps1`을 실행하면 설명판·마스크·검수 이미지·수치를 다시 만든다. 원본 PNG에는 쓰지 않는다.

Unity에 연결할 때 Sprite Single, Full Rect, 같은 PPU/Pivot, Point, Compression None, Mip Maps Off를 확인한다. 설명판을 늘려야 한다면 원래 오각형과 하단 마름모가 변형되지 않는지 별도 검토한다. 텍스트와 클릭 영역은 분리한다.
