# 로비 스테이지 봉인 표시

## 구조와 적용 범위

- `Assets/00.Scenes/Builds/Lobby.unity`의 `CurrentStage`, `PreviousStage`, `NextStage_Locked` 세 슬롯에 적용한다.
- `LobbyDifficultyLock`은 기존 `StageClearStore`의 보통 클리어 → 어려움 / 어려움 클리어 → 지옥 판정을 유지한다. 카드 제목 문자열과 기존 전투 시작 버튼 잠금도 유지한다.
- `UILobbyStageCardView`는 표시만 맡는다. 1/3/5명 용사 Image의 원래 색을 직렬화하고 잠금 때 실루엣으로 바꾸며, 해금과 슬롯 재바인딩 때 복구한다. 원본 유닛 Sprite와 해금 저장 데이터는 수정하지 않는다.
- 기존 합성 `StageLock_Chains.png` 연결은 로비 세 카드에서 새 분리 아트로 교체한다. 구형 파일은 다른 씬 호환을 위해 보존한다.
- 중앙은 `difficulty_selected.asset`, 이전/이후 슬롯은 `difficulty_card.asset`을 사용한다. `difficulty_card`의 검정 내부와 하단 외형을 유지하면서 상단 장식은 선택 프레임에 맞춘다. 카드별 기존 크기와 Preserve Aspect 설정은 유지한다.
- `UILobbyDifficultyRecordPanel._stageCard`는 중앙 카드에 연결한다. 잠금 이벤트에서 진행 Tween을 정리하고 기록/음영을 숨긴다. 잠긴 동안 호버 대신 해금 조건을 표시한다.
- `LockStateChanged`, `SetLockCondition(string)`, `SetLockTransitionAlpha(float)`은 새 표시 API다. 기존 호출 API는 유지한다. 전환 알파는 Bridge가 기존 Frame CanvasGroup에서 받아 카드로 전달한다.

## Inspector 편집 위치

```text
Canvas_Lobby/StageSelection/{세 카드}/VisualLayers
├─ ArtworkMask/ArtworkRoot/ArtworkMotion
│  └─ EasyPortraits / NormalPortraits / HardPortraits
├─ LockOverlay                         # 기존 CanvasGroup, 카드 전체 Stretch
│  ├─ Chains                          # 새 사슬 / 고정 장식, Raycast 꺼짐
│  ├─ Lock                            # 회색 원본에 옅은 금색 Color 틴트
│  ├─ SealRibbon
│  │  └─ SealLabel                    # 명조, 봉인됨
│  ├─ ConditionHeader                 # 고딕, 해금 조건
│  └─ ConditionValue                  # 고딕, 이전 난이도 클리어 안내
└─ HeraldryCaption/Description         # 기존 설명, 잠금 시 CanvasGroup alpha0
```

용사 묶음의 실제 원래 경로는 기존 계층을 따른다. 세 슬롯마다 `_silhouetteImages`와 `_unlockedPortraitColors`가 같은 순서로 9개 연결돼야 한다. 새 용사 Image를 추가하거나 기본 tint를 편집하면 해당 배열도 함께 조정한다. 잠긴 상태의 색을 원래색 배열에 복사하지 않는다.

중앙 카드 기준 사슬398×282, 자물쇠82×108이다. 리본은 Play Mode의 최신 수동 조정값 Y=-104.4를 기본 씬에 반영했다. 크기166.1×68.3, Image.Simple / PreserveAspect=false는 유지한다. SealLabel은 세 카드 모두 Y=3.66으로 통일한다(이 값에는 카드 비율을 곱하지 않는다). 중앙 글자는96.7×34, FontSize26, 자동 크기=false이다. 좌우 리본의 기존 위치는 이번에 변경하지 않는다(리본134.7604×55.4132/Y=-90.8679, 글자78.4547×27.5849/Y=3.66/FontSize21.0943). 좌우의 크기·폰트는 기존430/530 비율이다. 카드·기존 용사·제목·기록의 RectTransform과 Transform scale은 유지한다. 리본 글자는 TMP로 분리돼 있다.

`Lock`의 Image Color는 `(1, 0.89, 0.69, 1)` / `#FFE3B0`이다. 회색 PNG와 알파·Sprite 연결을 유지하고 표시 색만 사슬과 어울리는 옅은 금색으로 조절한다. 이 Color는 기존 Runtime 갱신에서 덮어쓰지 않는다.

## 아트와 도구

- 생성 아트: `Assets/06.UI/LobbyMutedPreview/Heraldry_v1/StageLocks_v1/Sprites`의 PNG3개와 native Sprite3개. 생성 도구/원본/프롬프트는 `LobbyStageLockArtPrompts.json`에 기록한다.
- 사슬/자물쇠 생성 PNG의 RGB와 알파는 그대로 사용한다. 리본은 금색 부분만 이미지 편집 도구의 저채도 금색 결과로 교체하며, 붉은 면·외곽·알파는 원본을 유지한다. Unity Sprite rect만 투명 여백을 제외한다. 중앙 선택 프레임/버튼/유닛 아트와 공용 Prefab 원본은 유지한다.
- 프레임 추가 편집은 `DifficultyFrameArtEdits.json`에 기록한다. `difficulty_card.png`의 상단만 이미지 편집 도구 결과로 교체하고 PNG상단300행 바깥과 검정 본문은 원본 픽셀을 유지한다. `difficulty_selected.png`와 두 native Sprite의 GUID/영역은 유지한다.
- 직선 봉인 리본 편집과 Play Mode의 값 보존, 후속 저채도 금색·공통 글자 Y3.66 수정은 `SealRibbonArtEdits.json`에 기록한다. 직선화할 때 native Sprite 영역을 갱신했고 후속 금색 편집 때는 영역도 그대로 유지했다. 금색 RGB151200픽셀만 변경됐으며 붉은 면을 포함한 비금색 RGB와 전체 알파 차이는0픽셀이다. 기존 GUID/씬 연결을 유지했다.
- `Tools > UI > Lobby Stage Locks > Apply Reference Layout`: Builds/Lobby 세 슬롯을 한 번 구성한다. 오브젝트 Undo 지원, 자동 씬 저장 없음. 생성 Sprite 에셋은 Undo 후에도 남는다. 이미 구성된 카드는 중복 생성하지 않으므로 Inspector에서 편집한다.
- `Tools > UI > Lobby Stage Lock > Validate And Capture`: 기존 기록 검증의 격리 복제·렌더 경로를 재사용한다. 계정 API와 Play Mode를 사용하지 않는다.
- `Tools > UI > Lobby Stage Locks > Refresh Seal Ribbon Sprite`: PNG 교체 후 native Sprite crop을 갱신한다. 삭제/재생성 없이 내용을 복사해 GUID를 유지한다. Sprite 내용 Undo 지원이며 PNG 원본은 별도 백업한다.

## 검증 결과 및 남은 확인

- 3슬롯×3난이도×잠금/해금18상태: 원래색 복구, 해금 안내, 재바인딩, 호버 중 잠금/강제 확장 차단, 0/0.5/1 전환 알파, Sprite 연결과 표시 Raycast 비활성 검증 통과.
- 기존 `LobbyInlineRecordValidation.Run(false)`도 통과했다. 수동 두 줄 제목 배치를 유지해 해당 모드의 글자 영역 검사는 기존 정책대로 제외한다.
- 직선 리본 Sprite 연결, 보존한 Image/RectTransform 설정, 중앙/좌우 비례 크기·공통 글자 Y3.66 및 봉인 글자3개의 실제 렌더 영역 검사도 통과했다. Unity 컴파일 완료와 Console Error0개를 확인했다.
- 검증 전후 실제 StageSelection 직렬화와 씬 dirty 상태는 동일하다. 복제본의 일반 MonoBehaviour OnEnable/OnDisable을 명시적으로 호출해 구독/해제도 검사한다.
- 실제 마우스 호버, 실시간 난이도 Tween, 진행 기록에 따른 해금, 실제 전투 시작 버튼 동작은 Play Mode에서 확인해야 한다.
- `Lobby.unity`와 Runtime 표시 코드3개가 충돌 주의 파일이다. Prefab/ProjectSettings/Packages 변경은 없다.
