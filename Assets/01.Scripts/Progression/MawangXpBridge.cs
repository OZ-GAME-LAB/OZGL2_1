using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 실제 게임용 XP 연동 — UnitBase.OnHeroKilled(세진이 이미 만들어둔 static 훅)를 구독해서
    /// 계정 영구 MawangLevel에 경험치를 쌓는다. UnitBase 코드는 전혀 건드리지 않는다.
    /// 씬 배치 없이 게임 시작과 동시에 자동으로 붙는다.
    /// </summary>
    public static class MawangXpBridge
    {
        public static MawangLevel Mawang { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (Mawang != null) return;

            Mawang = new MawangLevel();
            UnitBase.OnHeroKilled += OnHeroKilled;
        }

        private static void OnHeroKilled(UnitBase hero, int expReward)
        {
            Mawang.AddXp(expReward);
        }
    }
}
