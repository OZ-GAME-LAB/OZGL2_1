using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage;

/// <summary>
/// IStageDefenders의 실제 구현. 마왕군은 준비씬에서 이미 배치·합성까지 끝난 UnitRegistry의 유닛들이라
/// 여기서 새로 스폰할 건 없고, 생존 수 보고 + 라운드 시작 시 죽은 유닛 부활(4.3절)만 담당한다.
/// PooledStageBattle(김건·준기 파트)의 RunRoundAsync()가 이 AliveCount로 승패를 판정한다.
/// </summary>
public sealed class RealDefenders : IStageDefenders
{
    public int AliveCount => UnitRegistry.GetAliveCount(UnitSide.DemonArmy);

    public Task PrepareRoundAsync(CancellationToken cancellationToken)
    {
        var units = UnitRegistry.GetUnits(UnitSide.DemonArmy);
        for (int i = 0; i < units.Count; i++)
        {
            UnitBase unit = units[i];
            if (unit != null && unit.currentState == UnitState.Dead)
            {
                unit.Revive();
            }
        }

        return Task.CompletedTask;
    }
}
