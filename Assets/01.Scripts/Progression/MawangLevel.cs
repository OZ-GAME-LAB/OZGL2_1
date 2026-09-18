using System;
using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 마왕 레벨업 시스템 — 순수 C# (MonoBehaviour 아님). UI 는 희수, 이건 시스템만.
    ///
    ///   용사 처치 / 라운드 클리어 → 경험치(XP)
    ///   누적 XP ≥ 다음 레벨 필요량 → 레벨 +1 → 레벨 포인트(LP) 지급
    ///
    ///  - 다음 레벨 필요 XP: 60 + 40·(L-1) + 8·(L-1)²   (특성 "속성 성장" 으로 XpNeedMult 감소)
    ///  - 레벨업 = LP +1, 레벨 5·10·15·20 도달 시 보너스 +1 (특성 "가르침" 으로 보너스 추가)
    ///  - 레벨·XP·LP 전부 계정 영구(PlayerPrefs). 런 리셋 없음 — 레벨은 계속 누적된다.
    ///  - 한 번에 XP 가 많이 들어오면 여러 레벨 연속 처리.
    /// </summary>
    public class MawangLevel
    {
        private const string KeyLevel = "OZGL2.Mawang.Level";
        private const string KeyXp = "OZGL2.Mawang.Xp";
        private const string KeyPoints = "OZGL2.Mawang.LP";

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int Points { get; private set; }

        /// <summary>특성 "통찰" — XP 획득 배율.</summary>
        public float XpGainMult { get; set; } = 1f;

        /// <summary>특성 "속성 성장" — 레벨업 필요 XP 배율(감소).</summary>
        public float XpNeedMult { get; set; } = 1f;

        /// <summary>특성 "가르침" — 레벨 5·10·15·20 도달 시 추가 보너스 LP.</summary>
        public int MilestoneBonusLp { get; set; } = 0;

        /// <summary>특성 "숙련의 결정" — 5의 배수 레벨 도달 시 즉시 추가 XP.</summary>
        public int SurgeXpOnMilestone { get; set; } = 0;

        public event Action<int> LeveledUp;   // 새 레벨
        public event Action XpChanged;
        public event Action PointsChanged;

        public MawangLevel()
        {
            Level = Mathf.Max(1, PlayerPrefs.GetInt(KeyLevel, 1));
            Xp = PlayerPrefs.GetInt(KeyXp, 0);
            Points = PlayerPrefs.GetInt(KeyPoints, 0);
        }

        /// <summary>현재 레벨에서 다음 레벨까지 필요한 XP.</summary>
        public int XpToNext
        {
            get
            {
                int n = Mathf.Max(0, Level - 1);
                float raw = 60f + 40f * n + 8f * n * n;
                return Mathf.Max(1, Mathf.RoundToInt(raw * Mathf.Max(0.1f, XpNeedMult)));
            }
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;

            Xp += Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Max(0f, XpGainMult)));

            bool leveled = false;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                GrantLevelReward();
                LeveledUp?.Invoke(Level);
                leveled = true;
            }

            Save();
            XpChanged?.Invoke();
            if (leveled) PointsChanged?.Invoke();
        }

        private void GrantLevelReward()
        {
            int lp = 1;
            if (Level % 5 == 0)
            {
                lp += 1 + Mathf.Max(0, MilestoneBonusLp);
                if (SurgeXpOnMilestone > 0) Xp += SurgeXpOnMilestone; // while 루프가 다시 검사
            }
            Points += lp;
        }

        // ─────────── LP 소비 (특성 트리)

        public bool CanSpend(int cost) => cost > 0 && Points >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanSpend(cost)) return false;
            Points -= cost;
            Save();
            PointsChanged?.Invoke();
            return true;
        }

        public void Refund(int amount)
        {
            if (amount <= 0) return;
            Points += amount;
            Save();
            PointsChanged?.Invoke();
        }

        // ─────────── 저장 (전부 계정 영구)

        /// <summary>디버그 — 레벨·XP·LP 전부 초기화.</summary>
        public void ClearSaved()
        {
            PlayerPrefs.DeleteKey(KeyLevel);
            PlayerPrefs.DeleteKey(KeyXp);
            PlayerPrefs.DeleteKey(KeyPoints);
            PlayerPrefs.Save();
            Level = 1;
            Xp = 0;
            Points = 0;
            XpChanged?.Invoke();
            PointsChanged?.Invoke();
        }

        private void Save()
        {
            PlayerPrefs.SetInt(KeyLevel, Level);
            PlayerPrefs.SetInt(KeyXp, Xp);
            PlayerPrefs.SetInt(KeyPoints, Points);
            PlayerPrefs.Save();
        }
    }
}
