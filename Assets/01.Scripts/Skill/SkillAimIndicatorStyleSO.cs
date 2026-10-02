using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>
    /// 스킬 조준·사거리 표시의 이미지와 움직임 설정. 이 에셋(Resources/SkillAimIndicatorStyle)에 스프라이트를 끼워 넣으면
    /// 코드로 그린 기본 표시 대신 그 이미지를 쓴다. 비워 둔 칸은 기본 표시를 그대로 쓰므로 하나씩 바꿔 나가도 된다.
    ///
    /// 이미지 규격
    ///  · 원형 범위(fill/ring/dashRing/ticks/cross): 정사각형, 중심이 이미지 중앙. 가장자리까지가 범위의 지름(이미지 폭 = 범위 지름).
    ///  · 방향 표시(shaft/head/chevron): +x(오른쪽)를 향한 모양으로 그린다. 게임에서 방향에 맞춰 돌려 쓴다.
    ///  · corridor: Sprite Editor에서 9분할(Border)을 지정하면 길이·폭에 맞춰 늘어난다.
    ///  · 피벗은 Center. 색을 직접 입힌 이미지라면 아래 'Tint With Skill Color'를 끄면 이미지 색 그대로 나온다.
    /// </summary>
    [CreateAssetMenu(menuName = "OZGL2/Skill/Aim Indicator Style", fileName = "SkillAimIndicatorStyle")]
    public sealed class SkillAimIndicatorStyleSO : ScriptableObject
    {
        [Header("원형 범위 이미지 (비우면 기본 표시)")]
        [Tooltip("범위 안을 채우는 면")] public Sprite fill;
        [Tooltip("범위의 바깥 테두리")] public Sprite ring;
        [Tooltip("안쪽에서 천천히 도는 고리")] public Sprite dashRing;
        [Tooltip("테두리 안쪽 눈금(천천히 반대로 돈다)")] public Sprite ticks;
        [Tooltip("중심 표시")] public Sprite cross;

        [Header("방향형 이미지 (비우면 기본 표시, +x 방향 기준)")]
        [Tooltip("피해가 들어가는 통로. 9분할 권장")] public Sprite corridor;
        [Tooltip("화살표 몸통(오른쪽으로 갈수록 진하게)")] public Sprite shaft;
        [Tooltip("화살촉")] public Sprite head;
        [Tooltip("꼬리에서 머리로 흘러가는 표시")] public Sprite chevron;
        [Tooltip("끝 고리. 비우면 ring 이미지를 쓴다")] public Sprite endRing;

        [Header("색")]
        [Tooltip("켜면 스킬 종류 색(피해 주황·장판 하늘색·힐 초록)을 이미지에 곱한다. 직접 색을 입힌 이미지는 끈다.")]
        public bool tintWithSkillColor = true;

        [Header("움직임")]
        [Tooltip("안쪽 고리가 도는 속도(도/초). 0이면 멈춤")] public float dashSpinSpeed = 16f;
        [Tooltip("눈금이 도는 속도(도/초). 음수는 반대 방향")] public float tickSpinSpeed = -6f;
        [Tooltip("나타날 때 튀어나오는 시간(초). 0이면 바로 나타남")] [Min(0f)] public float popSeconds = 0.18f;
        [Tooltip("방향 표시에서 쉐브론이 흐르는 속도")] [Min(0f)] public float flowSpeed = 1.6f;
        [Tooltip("범위 안 대상 수(×N) 표시")] public bool showTargetCount = false;
    }
}
