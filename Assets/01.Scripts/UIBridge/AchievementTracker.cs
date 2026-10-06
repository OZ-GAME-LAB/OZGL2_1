using System.Collections.Generic;
using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.Progression;
using OZGL2.Stage;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 씬에서 업적에 쓰이는 기록을 쌓는다 — 용사 처치(보스급 따로), 끝낸 전투·승리·관문(보스 웨이브) 돌파, 합성 성장.
    /// 값은 AchievementStore 에 바로 저장되고, 로비의 업적 화면이 그 값을 읽는다. 전투 코드는 건드리지 않고
    /// UnitBase.OnHeroKilled · StageManager.Progress · GridManager.Changed 를 읽기만 한다.
    /// </summary>
    public sealed class AchievementTracker : MonoBehaviour
    {
        private const string BossIdPrefix = "H_BOSS";

        private InGamePrototypeBootstrap _bootstrap;
        private string _runId;
        private int _recordedRounds;
        private GridManager _grid;
        private readonly Dictionary<string, int> _stars = new Dictionary<string, int>();
        private bool _dirty;
        private float _nextSave;

        private void OnEnable() => UnitBase.OnHeroKilled += OnHeroKilled;

        private void OnDisable()
        {
            UnitBase.OnHeroKilled -= OnHeroKilled;
            UnbindGrid();
            Flush();
        }

        private void OnApplicationQuit() => Flush();

        private void OnHeroKilled(UnitBase hero, int exp)
        {
            AchievementStore.Add(AchievementStore.Kills);
            var id = hero != null && hero.statData != null ? hero.statData.unitId : null;
            if (!string.IsNullOrEmpty(id) && id.StartsWith(BossIdPrefix)) AchievementStore.Add(AchievementStore.Giants);
            _dirty = true;
        }

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_bootstrap == null) return;
            TrackRounds();
            TrackGrid();
            if (_dirty && Time.unscaledTime >= _nextSave) { _nextSave = Time.unscaledTime + 2f; Flush(); }
        }

        /// <summary>웨이브 결과가 확정될 때마다 끝낸 전투·승리·관문 돌파를 센다(같은 웨이브는 한 번만).</summary>
        private void TrackRounds()
        {
            var progress = _bootstrap.Stage != null ? _bootstrap.Stage.Progress : null;
            if (progress == null) return;
            // 저장된 판을 이어한 경우, 이미 깬 것으로 건너뛴 웨이브는 다시 세지 않는다
            if (progress.RunId != _runId) { _runId = progress.RunId; _recordedRounds = RunResume.FastForwarded; }
            var rounds = progress.Rounds;
            for (; _recordedRounds < rounds.Count; _recordedRounds++)
            {
                var round = rounds[_recordedRounds];
                AchievementStore.Add(AchievementStore.Battles);
                if (round.Outcome != eBattleResult.VICTORY) continue;
                AchievementStore.Add(AchievementStore.Wins);
                if (IsBossRound(progress.StageId, round.RoundNumber)) AchievementStore.Add(AchievementStore.Gates);
                _dirty = true;
            }
        }

        private bool IsBossRound(string stageId, int roundNumber)
        {
            var catalog = _bootstrap.Config != null ? _bootstrap.Config.StageCatalog : null;
            if (catalog == null || string.IsNullOrEmpty(stageId)) return false;
            try
            {
                var definition = catalog.Resolve(stageId);
                return definition != null && roundNumber >= 1 && roundNumber <= definition.Rounds.Count && definition.Rounds[roundNumber - 1].IsBossRound;
            }
            catch { return false; }
        }

        // ───────────── 합성 성장: 유닛의 성급이 올라간 것을 센다

        private void TrackGrid()
        {
            var session = _bootstrap.GridSession;
            var grid = session != null ? session.Grid : null;
            if (grid == _grid) return;
            UnbindGrid();
            _grid = grid;
            if (_grid == null) return;
            Snapshot();
            _grid.Changed += OnGridChanged;
        }

        private void UnbindGrid()
        {
            if (_grid != null) _grid.Changed -= OnGridChanged;
            _grid = null;
            _stars.Clear();
        }

        private void Snapshot()
        {
            _stars.Clear();
            foreach (var unit in _grid.Units) _stars[unit.InstanceId] = unit.StarLevel;
        }

        private void OnGridChanged()
        {
            if (_grid == null) return;
            int grown = 0;
            foreach (var unit in _grid.Units)
                if (_stars.TryGetValue(unit.InstanceId, out int before) && unit.StarLevel > before) grown += unit.StarLevel - before;
            if (grown > 0) { AchievementStore.Add(AchievementStore.Growth, grown); _dirty = true; }
            Snapshot();
        }

        private void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            AchievementStore.Save();
        }
    }
}
