# TMP 폰트 에셋 별도 공유

## 정책

2026-09-18 결정: SDF 폰트 에셋과 Dynamic 데이터가 변하는 로비 Pixel 폰트 에셋은 Git에서 제외하고 팀 Drive로 배포합니다. 로컬 파일은 유지하며, 기존 추적 파일은 승인된 커밋에서 Git 추적만 해제합니다. 과거 커밋 기록은 그대로 남습니다.

`.gitignore`는 `Assets` 아래 이름에 SDF가 포함된 `.asset` 및 그 `.meta`, 로비 전용 Fonts 폴더의 `.asset` 및 그 `.meta`를 제외합니다. SDF 셰이더, 셰이더 include, 일반 머티리얼까지 포괄적으로 제외하지 않습니다. 이름에 SDF가 없는 다른 TMP 폰트를 새로 추가하면 제외 범위를 별도로 점검합니다.

## 공유 묶음에 포함할 파일

프로젝트 루트 기준 경로를 보존한 ZIP 또는 GUID를 유지하는 Unity 패키지로 배포합니다. 모든 에셋의 원래 `.meta`를 함께 넣고, 원본 폰트 및 라이선스 안내도 포함합니다.

1. `Assets/98.ExternalAssets/00.LocalStaging/01.Font/` 폴더와 해당 폴더의 `.meta`.
   - DOSGothic/DOSMyungjo 원본, Pixel/SDF 에셋, Pixel Outline 머티리얼.
   - 해당 폴더로 함께 이동된 `PixelTMPOutline.shader`와 `.meta`.
   - NotoSansCJKkr, TerrarumSansBitmap, 기존 라이선스/출처 파일.
2. `Assets/06.UI/LobbyMutedPreview/Overlays/Fonts/DOSMyungjo Overlay Pixel.asset` 및 `.meta`.
3. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` 및 `.meta`.
4. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` 및 `.meta`.

외부 유료 에셋의 폰트는 기존 `98.ExternalAssets` 별도 배포 정책을 따릅니다. Git 제외 여부가 라이선스상 재배포 허용을 뜻하지는 않으므로, 팀 내 사용 범위와 각 폰트 라이선스를 지킵니다.

## 팀원 적용 순서

1. 추적 해제 커밋을 받기 전에 로컬 폰트 및 `.meta`를 백업합니다. 이 커밋을 pull하면 기존 추적 경로의 파일이 제거될 수 있습니다.
2. Unity를 닫고 코드 변경을 받은 뒤, 합의된 Drive 묶음을 동일한 프로젝트 상대 경로에 복원합니다.
3. `.meta`를 누락하거나 새로 생성하지 않습니다. 이전 경로와 새 경로에 같은 GUID의 복사본을 동시에 두지 않습니다.
4. Unity를 열고 `UI_Lobby_MutedPreview`의 로비·메뉴·특성 텍스트와 Outline 머티리얼을 확인합니다.
5. Missing Font / Missing Material / Missing Shader와 Console 오류를 확인합니다.

특히 `01.Font`로 옮긴 로비 폰트의 기존 `Assets/06.UI/LobbyMutedPreview/Fonts/` 사본은 중복 배치하지 않습니다. 필요한 `.meta`를 잃었다면 임의 재생성하지 말고 공유본에서 복원합니다.

## 주의 사항

- 폰트 묶음 없이는 새 clone만으로 UI를 재현할 수 없습니다. 공유본 버전을 사용하는 커밋과 함께 안내해야 합니다.
- 현재 로비·오버레이 폰트는 Dynamic 방식입니다. 원본 `.ttf`도 반드시 필요합니다.
- `LobbyMutedPreviewFontApplier.cs`는 현재 이전 출력 경로인 `Assets/06.UI/LobbyMutedPreview/Fonts`를 사용합니다. 이동 후 이 도구를 실행하면 기존 이동본을 갱신하지 않고 별도 에셋을 만들 수 있으므로, 경로 정리 전에는 재실행하지 않습니다. 이번 Git 제외 작업은 도구 코드를 변경하지 않습니다.
- 이 문서는 공유 절차이며 Drive 업로드 완료 기록이 아닙니다. 업로드 위치와 공유본 버전은 배포자가 별도로 안내해야 합니다.
