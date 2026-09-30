# 라운드 스폰 수 반영 (2026-09-28)

- 출처: [밸런스 시트 v0.5](https://docs.google.com/spreadsheets/d/1JwulrLMckb8AhYEn_jjKJuD5FCim9zyl/edit?gid=141066876)
- 범위: 보통 30R, 어려움 50R의 물량만 적용. 헬 및 라운드별 스탯 배율 전달은 후속 작업.
- StageSpawnCounts.csv는 시트의 최종 수량 80개를 저장한 명시적 데이터다. 보스 라운드 감소가 포함되어 있어 추가 배율을 곱하지 않는다.
- 기존 StageNormal30/StageHard50 SO의 _count만 변경했다. 기존 직업별 수량 비율을 비례 환산하고 정수 나머지가 큰 순서로 배분했다(동률은 기존 엔트리 순서).
- 유닛 종류/스폰 간격/보상/증강 가중치/보스 플래그/스탯은 유지했다.
- StageSpawnDataSetup은 재생성 시 CSV 수량을 사용한다. 이 메뉴는 여전히 전체 라운드를 재생성하므로 기존 직업 배분과 보상까지 재설정한다. 이번 반영에는 해당 메뉴를 실행하지 않고 SerializedObject로 수량만 수정했다.
- 검증: Unity CreateSnapshot으로 전체 80R 합계를 원본 시트와 대조해 통과. 두 SO의 Git diff에서 _count 이외 변경이 없는지 확인. Unity Console 오류 0, git diff --check 통과.
- 이번 변경 후 실제 전체 라운드 플레이 및 승패 밸런스는 검증하지 않았다. 스탯은 기존 값이다.
- 후속 플레이 피드백에 따라 보통 1라운드만 시트의 10마리에서 5마리로 조정했다(사용자 요청). CSV와 SO에 동일 반영했으며 다른 라운드는 유지한다. 어려움 1라운드는 12마리다. Unity 실행용 데이터에서 보통 R1=5, 해당 SO의 다른 필드 변경 없음 확인.
