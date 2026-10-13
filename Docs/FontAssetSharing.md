# UI 폰트 에셋 공유

## 정책

2026-10-13 변경: 기존 `Assets/98.ExternalAssets/00.LocalStaging/01.Font/`를 `Assets/06.UI/01.Font/`로 옮기고, 이 폴더는 SDF/Pixel 폰트 에셋과 원본 폰트, 머티리얼, 셰이더, 라이선스 및 모든 원래 `.meta`를 함께 Git으로 공유합니다. 해당 폴더에 한해 2026-09-18의 Git 제외/Drive 배포 정책을 대체합니다.

`.gitignore`는 `Assets/06.UI/01.Font/`와 폴더 자체의 `.meta`를 추적 가능하게 예외 처리합니다. 이 경로 밖에서는 이름에 SDF가 포함된 `.asset` 및 그 `.meta`, 기존 로비 전용 Fonts 폴더의 `.asset` 및 그 `.meta` 제외 규칙이 유지됩니다. 따라서 이번 변경만으로 새 clone의 모든 UI 폰트 의존성이 해결되는 것은 아닙니다. 특히 `Assets/06.UI/BattleMutedPreview/ResultRefinement_v2/Fonts/`의 결과 화면 SDF는 기존 별도 공유 대상입니다.

## Git 공유 대상

`Assets/06.UI/01.Font/` 전체와 `Assets/06.UI/01.Font.meta`를 함께 커밋 대상으로 확인합니다. 폰트 에셋만 선택하고 원본이나 `.meta`를 빠뜨리지 않습니다. 커밋/push는 별도 승인 절차에 따르며, 이 문서는 업로드 완료 기록이 아닙니다.

1. `Assets/06.UI/01.Font/` 폴더와 해당 폴더의 `.meta`.
   - DOSGothic/DOSMyungjo 원본, Pixel/SDF 에셋, Pixel Outline 머티리얼.
   - 해당 폴더로 함께 이동된 `PixelTMPOutline.shader`와 `.meta`.
   - NotoSansCJKkr, TerrarumSansBitmap, 기존 라이선스/출처 파일.
   - 복구 후 같은 폴더로 이동한 `DOSMyungjo Overlay Pixel.asset`, `LiberationSans SDF.asset`, `LiberationSans SDF - Fallback.asset` 및 각각의 원래 `.meta`.
   - `00.otf_ttf_file/`의 원본 폰트와 `02.SDF_File/`의 생성 폰트, 빛의 계승자와 Paperlogy 로고 폰트를 포함한 현재 하위 폴더 내용.
   - `Licenses/`에 동봉한 Paperlogy, DOS 폰트, Terrarum의 제작자 라이선스 원문과 출처 안내.

`LiberationSans.ttf` 원본은 Git에서 공유하는 기존 `Assets/TextMesh Pro/Fonts/` 경로를 유지합니다. Overlay 전용 `DOSMyungjo Overlay Outline.mat`도 Git에서 공유하는 기존 `Assets/06.UI/LobbyMutedPreview/Overlays/Fonts/` 경로를 유지합니다.

외부 유료 에셋의 폰트는 기존 `98.ExternalAssets` 별도 배포 정책을 따릅니다. Git 제외 여부가 라이선스상 재배포 허용을 뜻하지는 않으므로, 팀 내 사용 범위와 각 폰트 라이선스를 지킵니다.

## 기존 로컬 폰트가 있는 팀원의 이전 순서

1. Unity를 종료합니다. 기존 ignored 폴더는 pull로 자동 이동되거나 정리되지 않습니다.
2. `Assets/98.ExternalAssets/00.LocalStaging/01.Font/`와 `Assets/98.ExternalAssets/00.LocalStaging/01.Font.meta`를 **프로젝트의 Assets 밖**으로 함께 이동해 백업합니다. 하위 에셋의 원래 `.meta`도 모두 보존합니다. Unity가 두 경로를 동시에 임포트하지 않게 이전 위치에는 사본을 남기지 않습니다.
3. 폰트 이동 커밋이 공유된 뒤 pull합니다. 새 `Assets/06.UI/01.Font/`와 폴더/하위 에셋의 `.meta`가 함께 내려왔는지 확인합니다. 기존 백업을 새 경로에 덮어쓰거나, Unity에서 같은 폰트를 새 GUID로 다시 생성하지 않습니다.
4. 개인 작업으로 변경된 폰트가 백업에 있다면 원본과 비교한 뒤 팀과 반영 범위를 정합니다. 같은 GUID의 사본을 Assets 안 여러 경로에 중복 배치하지 않습니다.
5. 새 폴더 밖에 남은 별도 공유 폰트는 합의된 Drive/Unity 패키지에서 기존 경로와 원래 `.meta`를 유지해 복원합니다. 결과 화면 폰트는 [BattleResultReferenceRefinement.md](UI/BattleResultReferenceRefinement.md)를 확인합니다.
6. Unity를 열고 TeamLogo, 로비·메뉴·특성·전투 UI의 글자와 Outline 머티리얼, Missing Font / Missing Material / Missing Shader 및 Console 오류를 확인합니다.

Unity에서 경로를 옮길 때는 Project 창 또는 `AssetDatabase.MoveAsset`을 사용해 폴더와 하위 에셋의 GUID를 유지합니다. Scene/Prefab의 직렬화 참조는 GUID를 따르므로 같은 에셋을 다시 연결할 필요가 없습니다. 코드에 적힌 `AssetDatabase.LoadAssetAtPath` 등의 문자열 경로는 자동으로 바뀌지 않으므로 새 경로로 수정해야 합니다. 필요한 `.meta`를 잃었다면 임의 재생성하지 말고 원래 공유본에서 복원합니다.

## 주의 사항

- 새 clone은 `Assets/06.UI/01.Font/`의 파일을 Git으로 받습니다. 이 폴더 밖의 제외된 폰트는 여전히 별도 공유본이 필요하며, 공유본 버전을 사용하는 커밋과 함께 안내해야 합니다.
- 현재 로비·오버레이 폰트는 Dynamic 방식입니다. 원본 `.ttf`도 반드시 필요합니다.
- `LobbyMutedPreviewFontApplier.cs`와 `LobbyOverlayPreviewBuilder.cs` 등 Editor 도구는 새 `Assets/06.UI/01.Font/` 경로를 사용합니다. 도구 실행 전 Git으로 받은 기존 에셋을 확인합니다. 누락된 에셋을 재생성하면 기존 Scene/Prefab 참조를 복원하는 대신 새로운 GUID가 만들어질 수 있습니다.
- Dynamic Atlas는 글리프 추가에 따라 변경될 수 있습니다. 폰트 변경을 커밋할 때 실제로 필요한 글리프/Atlas 변경인지 Diff를 확인하고, 충돌이 나면 `.meta` 삭제나 폰트 재생성으로 해결하지 않습니다.
- 이번 폴더 이동은 라이선스를 변경하지 않습니다. 포함된 라이선스/출처 안내를 보존하고 팀 내 사용 범위와 각 폰트의 재배포 조건을 지킵니다. 외부 유료 에셋 폴더의 별도 배포 정책도 유지합니다.
