using UnityEngine;

/// <summary>
/// Day1 전용 테스트 도구. UnitBase 프리팹을 지정 위치에 스폰하고 목표 지점으로 이동시켜서
/// 상태머신 + 이동이 정상 동작하는지 눈으로 확인하기 위한 용도.
/// 실제 스폰/배치 시스템(그리드, 웨이브)이 붙으면 이 스크립트는 지워도 된다.
/// </summary>
public class UnitTestSpawner : MonoBehaviour
{
    [Header("테스트할 유닛 프리팹 (UnitBase 붙어있는 프리팹)")]
    public UnitBase unitPrefab;

    [Header("스폰 위치 / 목표 위치")]
    public Vector3 spawnPosition = new Vector3(0f, 4f, 0f);
    public Vector3 targetPosition = new Vector3(0f, -4f, 0f);

    private void Start()
    {
        if (unitPrefab == null)
        {
            Debug.LogWarning("[UnitTestSpawner] unitPrefab이 비어있음. 인스펙터에서 할당해줘.", this);
            return;
        }

        UnitBase unit = Instantiate(unitPrefab, spawnPosition, Quaternion.identity);
        unit.SetMoveTarget(targetPosition);
    }

    // 씬 뷰에서 스폰/목표 위치를 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnPosition, 0.3f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetPosition, 0.3f);
        Gizmos.DrawLine(spawnPosition, targetPosition);
    }
}
