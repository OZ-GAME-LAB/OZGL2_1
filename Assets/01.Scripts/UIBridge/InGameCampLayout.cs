using OZGL2.InGame;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 용사가 천막에서 나오게 한다. 천막 수와 위치는 건의 배경 설정(OutdoorBattlefieldConfigSO)이 스테이지별로 정하고
    /// (보통 1개 · 어려움 3개 · 지옥 5개, 화면에 그리는 것도 건의 OutdoorBattlefieldPresentation), 이 컴포넌트는 같은 설정에서
    /// 천막 자리를 읽어 용사 스폰 위치를 각 천막 입구로 바꿔 줄 뿐이다. 천막이 여러 개면 입구를 차례로 돌며 나온다.
    /// 선택한 스테이지는 부트스트랩이 가져가기 전(Awake)에 StageLaunchRuntime 에서 미리 읽는다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class InGameCampLayout : MonoBehaviour
    {
        [SerializeField, Tooltip("천막 수·위치를 정하는 건의 배경 설정(OutdoorBattlefieldConfig)")] private OutdoorBattlefieldConfigSO _battlefield;
        [SerializeField, Tooltip("천막 아랫면에서 입구까지 올려 잡는 높이(칸)")] private float _doorLift = 0.3f;
        [SerializeField, Tooltip("천막이 하나일 때 입구 좌우로 나눠 서는 간격(칸)")] private float _singleSpread = 1f;

        private void Awake()
        {
            var pending = StageLaunchRuntime.Session != null ? StageLaunchRuntime.Session.Pending : null;
            string stageId = pending != null ? pending.StageId : string.Empty;

            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var config = bootstrap != null ? bootstrap.Config : null;
            if (config == null || _battlefield == null || _battlefield.CampTent == null || _battlefield.CampTent.Sprite == null)
            {
                InGamePrototypeConfigSO.HeroSpawnOverride = null;
                return;
            }

            Vector3 origin = config.GridWorldOrigin;
            float cell = config.CellWorldSize;
            int tents = _battlefield.GetTentCount(stageId);
            float halfHeight = _battlefield.CampTent.Sprite.bounds.size.y * _battlefield.CampTent.Scale * 0.5f;

            Vector3[] doors;
            if (tents == 1)
            {
                Vector2 c = _battlefield.GetTentCell(0, 1);
                float y = c.y - halfHeight + _doorLift;
                doors = new[]
                {
                    origin + new Vector3(c.x - _singleSpread, y, 0f) * cell,
                    origin + new Vector3(c.x, y, 0f) * cell,
                    origin + new Vector3(c.x + _singleSpread, y, 0f) * cell,
                };
            }
            else
            {
                doors = new Vector3[tents];
                for (int i = 0; i < tents; i++)
                {
                    Vector2 c = _battlefield.GetTentCell(i, tents);
                    doors[i] = origin + new Vector3(c.x, c.y - halfHeight + _doorLift, 0f) * cell;
                }
            }
            InGamePrototypeConfigSO.HeroSpawnOverride = () => doors;
        }

        private void OnDestroy() => InGamePrototypeConfigSO.HeroSpawnOverride = null;
    }
}
