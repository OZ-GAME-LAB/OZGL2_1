using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace OZGL2.BalanceTest
{
    /// <summary>밸런스 시트 한 라운드분 — 물량과 용사 스탯 배율(HP·공격·공속·힐량).</summary>
    public readonly struct RoundScaling
    {
        public readonly int Count;
        public readonly float Hp, Atk, Spd, Heal;
        public RoundScaling(int count, float hp, float atk, float spd, float heal)
        { Count = count; Hp = hp; Atk = atk; Spd = spd; Heal = heal; }
    }

    /// <summary>
    /// Resources/Balance/RoundScaling.csv(stage_id,round,count,hp,atk,spd,heal)를 읽어 조회해 준다.
    /// 이 CSV는 밸런스 시트 05·06·07(보통·어려움·헬)의 HP배율·공격배율·공속배율·힐량배율·물량 열에서
    /// 그대로 뽑은 값이라, 시트 수치를 고치면 CSV만 다시 뽑아서 인게임 테스트에 바로 반영할 수 있다.
    /// </summary>
    public static class BalanceRoundScaling
    {
        private static Dictionary<(string, int), RoundScaling> _table;
        private static readonly Dictionary<string, int> _roundCounts = new Dictionary<string, int>();

        public static bool TryGet(string stageId, int round, out RoundScaling row)
        {
            EnsureLoaded();
            return _table.TryGetValue((stageId ?? string.Empty, round), out row);
        }

        public static int RoundCount(string stageId)
        {
            EnsureLoaded();
            return _roundCounts.TryGetValue(stageId ?? string.Empty, out int n) ? n : 0;
        }

        private static void EnsureLoaded()
        {
            if (_table != null) return;
            _table = new Dictionary<(string, int), RoundScaling>();
            var asset = Resources.Load<TextAsset>("Balance/RoundScaling");
            if (asset == null)
            {
                Debug.LogWarning("[BalanceTest] Resources/Balance/RoundScaling.csv 를 못 찾음 — 라운드 배율은 전부 1로 동작.");
                return;
            }

            var inv = CultureInfo.InvariantCulture;
            var lines = asset.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0행은 헤더
            {
                var cells = lines[i].Trim().Split(',');
                if (cells.Length < 7) continue;
                if (!int.TryParse(cells[1], out int round) || !int.TryParse(cells[2], out int count)) continue;
                if (!float.TryParse(cells[3], NumberStyles.Float, inv, out float hp) ||
                    !float.TryParse(cells[4], NumberStyles.Float, inv, out float atk) ||
                    !float.TryParse(cells[5], NumberStyles.Float, inv, out float spd) ||
                    !float.TryParse(cells[6], NumberStyles.Float, inv, out float heal)) continue;
                _table[(cells[0], round)] = new RoundScaling(count, hp, atk, spd, heal);
                _roundCounts[cells[0]] = Mathf.Max(round, _roundCounts.TryGetValue(cells[0], out int prev) ? prev : 0);
            }
        }
    }
}
