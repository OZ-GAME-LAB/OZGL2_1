using System.Linq;
using OZGL2.Progression;
using OZGL2.Skill;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 업적 화면(UILobbyCollectionState)에 실제 진행도를 넣어 준다.
    /// 전투에서 쌓은 기록(AchievementStore)에, 로비에서 정해지는 값(특성·스킬 투자 단계, 스킬 장착 슬롯)을 더해
    /// 업적 id 마다 진행도를 맞춘다. 카탈로그의 목표치는 업적 화면이 알아서 비교하므로 여기서는 값만 넣는다.
    /// </summary>
    public sealed class AchievementLobbySync : MonoBehaviour
    {
        [SerializeField] private UIAchievementCatalogSO _catalog;
        [SerializeField] private UILobbyCollectionState _state;
        [SerializeField, Min(0.1f), Tooltip("로비에서 바뀌는 값(특성·스킬)을 다시 읽는 간격")] private float _interval = 0.5f;

        private float _next;
        private SkillData[] _skills;
        private TraitData[] _traits;

        private void OnEnable() => _next = 0f;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _interval;
            if (_state == null) _state = FindFirstObjectByType<UILobbyCollectionState>(FindObjectsInactive.Include);
            if (_state == null) return;
            Refresh();
        }

        private void Refresh()
        {
            // 로비에서 정해지는 값은 "지금까지의 최고치"로 저장한다(특성을 되돌려도 달성한 업적은 사라지지 않는다)
            _skills = _skills ?? Resources.LoadAll<SkillData>("Skills");
            _traits = _traits ?? Resources.LoadAll<TraitData>("Traits"); // 매번 다시 읽지 않고 한 번만 불러 둔다
            int invested = new TraitTree(_traits).AllocatedPoints
                           + _skills.Count(s => s != null && SkillTreeStore.IsUnlocked(s.skillId));
            bool changed = AchievementStore.RaiseTo(AchievementStore.Tuning, invested);
            changed |= AchievementStore.RaiseTo(AchievementStore.Slots, SkillTreeStore.GetEquipped().Count);
            if (changed) AchievementStore.Save();

            var entries = _catalog != null ? _catalog.Entries : null;
            if (entries == null) return;
            foreach (var entry in entries)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                    _state.SetAchievementProgress(entry.Id, AchievementStore.ProgressOf(entry.Id));
        }
    }
}
