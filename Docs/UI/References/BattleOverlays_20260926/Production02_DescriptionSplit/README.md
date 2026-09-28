# 검정 설명 패널 / 벨벳 배경 분리

## 완료 범위

사용자가 지정한 대상은 큰 세로 카드 안의 **검정 설명 패널(얇은 테두리·코너·작은 하단 마름모 포함)**과 그 뒤의 **벨벳 배경**입니다. 바깥 금속 테두리·실장식·문양은 벨벳 쪽에 유지합니다. `Crest_*`의 마름모 문장/등급 명패는 수정하지 않았습니다.

- 최종 폴더: `Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment/Separated/`
- 실버·골드·플래티넘 각각 2장, 총 6장.
- 원래 `Augment_Body_*.png`는 덮어쓰지 않았습니다.
- 씬/프리팹/기능/TextureImporter/.meta는 변경하지 않았습니다.

## 파일

| 등급 | 뒤 레이어 | 앞 레이어 |
| --- | --- | --- |
| 실버 | VelvetBackground_Silver.png | DescriptionPanel_Silver.png |
| 골드 | VelvetBackground_Gold.png | DescriptionPanel_Gold.png |
| 플래티넘 | VelvetBackground_Platinum.png | DescriptionPanel_Platinum.png |

6장 모두 **600×1100 RGBA PNG**입니다. 설명판은 기존 픽셀·좌표를 그대로 추출했습니다. 벨벳 배경은 검정판에 가려졌던 부분을 built-in **imagegen**으로 복원한 뒤 외곽 여백을 기존과 동일한 `(30,30,540,1040)`으로 맞췄습니다.

복원 배경의 천 무늬·미세 장식은 생성본입니다. 원본과 픽셀 단위로 동일하다는 의미가 아닙니다. 설명판 부분만 새 천으로 채우는 초기 조합에서 남은 그림자/문양 경계가 보여, 최종 배경은 **전체 벨벳 복원본**을 사용합니다. 설명판은 원본 픽셀 그대로 유지합니다.

## Unity 배치

- 기존 몸체 이미지 대신 `VelvetBackground_등급`을 아래에 배치합니다.
- 위에 `DescriptionPanel_등급`을 같은 크기·앵커·중심 pivot·위치로 겹칩니다.
- 그 위에 특성명·설명·효과 Text/TMP를 배치합니다. 텍스트는 이미지에 넣지 않았습니다.
- 분리된 설명판의 위치/크기를 변경해도 뒤에 구멍이 보이지 않도록 벨벳을 채웠습니다.
- Image Type Simple, Mesh Full Rect, Filter Point, Compression None, Mip Maps off를 우선 검토합니다. Importer 연결은 아직 수행하지 않았습니다.
- 개별 Tight Crop은 하지 않았습니다. 캔버스 기준 좌표를 유지해야 기존 배치를 재사용할 수 있습니다.

### 설명판 영역

좌상단 원점 픽셀 좌표입니다.

| 등급 | 패널 영역 `(x,y,w,h)` | TMP 안전 영역 `(x,y,w,h)` |
| --- | --- | --- |
| Silver | (95,232,410,745) | (120,275,360,510) |
| Gold | (106,229,388,718) | (130,270,340,505) |
| Platinum | (120,169,359,789) | (145,205,310,580) |

## 검증

- 설명판 원본 대응 픽셀 차이: 3종 모두 **0개**.
- 원본 SHA256: 변경 없음.
- 설명판 캔버스 가장자리 alpha: 0.
- 배경에서 설명판 아래 영역의 alpha 빈 구멍: 3종 모두 **0개**.
- 어두운 배경/밝은 배경에서 설명판 테두리와 외곽 투명도를 확인했습니다.
- 복원 배경의 패널 자국 제거와 분리 결과를 확인했습니다.
- Unity 컴파일/Play Mode/씬 배치: **미확인**, 이번 작업에 포함하지 않았습니다.

## 기록

- [생성 프롬프트](Prompts.md): built-in imagegen, 가려진 벨벳 복원.
- [설명판 추출 방법·수치](Extraction/README.md)
- [배경 정리/배치 수치](Validation.txt)
- `Extraction/ExtractDescriptionPanels.ps1`: 원본에서 검정판과 얇은 테두리만 추출.
- `PrepareBackgrounds.ps1`: 생성본 배경 정리·동일 캔버스/여백 정렬.
- `Raw/`: 생성 원본 보존.
- `Verification/Recomposed_*.png`: 복원 배경과 기존 설명판을 겹친 검수용 이미지.
- [분리 결과 미리보기](Separated_Preview.png)

커밋/푸시는 수행하지 않았습니다.

