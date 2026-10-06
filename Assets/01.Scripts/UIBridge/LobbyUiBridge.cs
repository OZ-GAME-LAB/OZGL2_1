using System.Collections.Generic;
using System.Reflection;
using OZGL2.Progression;
using OZGL2.Skill;
using OZGL2.Synergy;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 UI(희수 파트: 레벨 HUD·특성 트리·스킬 세팅)를 실제 계정 시스템(MawangLevel·TraitTree·SkillTreeStore)에 잇는다.
    /// 씬이 로드될 때 UI 컴포넌트를 찾아 같은 오브젝트에 계정 바인더를 붙인다.
    /// 바인더의 OnEnable이 UI 컴포넌트의 OnEnable보다 늦게 돌기 때문에,
    /// 화면을 열 때마다 UI가 만드는 임시 모델을 실제 모델로 다시 갈아 끼운다.
    /// </summary>
    public static class LobbyUiBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            foreach (var hud in UnityEngine.Object.FindObjectsByType<UILobbyLevelHudView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ensure<LobbyLevelHudBinder>(hud.gameObject);
            foreach (var traits in UnityEngine.Object.FindObjectsByType<UITraitProgressionController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ensure<LobbyTraitBinder>(traits.gameObject);
            foreach (var loadout in UnityEngine.Object.FindObjectsByType<UISkillLoadoutPreview>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ensure<LobbySkillLoadoutBinder>(loadout.gameObject);

            // 로비 UI가 있는 씬에서만, 에디터·개발 빌드에서만 연동 확인용 디버그 패널(F8)을 띄운다.
            bool hasLobbyUi = UnityEngine.Object.FindFirstObjectByType<LobbyLevelHudBinder>(FindObjectsInactive.Include) != null
                              || UnityEngine.Object.FindFirstObjectByType<LobbyTraitBinder>(FindObjectsInactive.Include) != null
                              || UnityEngine.Object.FindFirstObjectByType<LobbySkillLoadoutBinder>(FindObjectsInactive.Include) != null;
            // 타이틀 씬에서도 쓸 수 있게 한다(마왕 튜토리얼 초기화·SP·해금 확인용)
            bool isTitle = SceneManager.GetActiveScene().name.StartsWith("Title");
            if ((hasLobbyUi || isTitle) && (Application.isEditor || Debug.isDebugBuild)
                && UnityEngine.Object.FindFirstObjectByType<LobbyDebugPanel>() == null)
                new GameObject("LobbyUiDebug").AddComponent<LobbyDebugPanel>();
        }

        private static void Ensure<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() == null) target.AddComponent<T>();
        }
    }

    /// <summary>레벨 HUD ← MawangLevel. 레벨·현재 레벨 내 XP·다음 레벨 필요 XP를 그대로 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyLevelHudBinder : MonoBehaviour
    {
        private UILobbyLevelHudView _hud;
        private MawangLevel _mawang;

        private void OnEnable()
        {
            _hud = GetComponent<UILobbyLevelHudView>();
            _mawang = MawangXpBridge.Mawang;
            if (_hud == null || _mawang == null) return;
            // 전투 시스템이 없는 로비에서도 저장된 특성(레벨업 필요 XP 감소 등)이 HUD 수치에 반영되게 한다.
            TraitMawangSettings.ApplySaved(_mawang);
            _mawang.XpChanged += Push;
            _mawang.LeveledUp += OnLeveledUp;
            Push();
        }

        // 특성으로 필요 XP만 바뀌는 경우엔 MawangLevel 이벤트가 없어서 값 변화를 직접 확인한다.
        private void Update()
        {
            if (_mawang == null || _hud == null) return;
            if (_mawang.Level != _shownLevel || _mawang.Xp != _shownXp || _mawang.XpToNext != _shownNeed) Push();
        }

        private void OnDisable()
        {
            if (_mawang == null) return;
            _mawang.XpChanged -= Push;
            _mawang.LeveledUp -= OnLeveledUp;
            _mawang = null;
        }

        private void OnLeveledUp(int level) => Push();

        private int _shownLevel = -1, _shownXp = -1, _shownNeed = -1;

        private void Push()
        {
            _shownLevel = _mawang.Level; _shownXp = _mawang.Xp; _shownNeed = _mawang.XpToNext;
            _hud.SetProgress(_shownLevel, _shownXp, _shownNeed);
        }
    }

    /// <summary>
    /// 특성 트리 ← 실제 TraitTree·MawangLevel. UITraitProgressionController는 화면을 열 때마다 자체 모델을 새로 만들기 때문에
    /// 그대로 두면 LP 소비가 게임 중인 MawangLevel(MawangXpBridge)에 반영되지 않고, 다음 XP 저장 때 옛 LP로 덮어써진다.
    /// 전투 시스템(RealSynergySync)이 이미 떠 있으면 그 TraitTree를 공유해서 로비에서 찍은 특성이 바로 전투에 적용된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyTraitBinder : MonoBehaviour
    {
        private TraitTree _tree;

        private void OnEnable()
        {
            var controller = GetComponent<UITraitProgressionController>();
            var mawang = MawangXpBridge.Mawang;
            if (controller == null || mawang == null) return;
            var sync = FindFirstObjectByType<RealSynergySync>();
            var tree = sync != null && sync.Traits != null ? sync.Traits : controller.Tree;
            if (tree == null) return;
            controller.Bind(tree, mawang);
            _tree = tree;
            _tree.Changed += ApplyMawangRules;
            ApplyMawangRules();
        }

        private void OnDisable()
        {
            if (_tree != null) _tree.Changed -= ApplyMawangRules;
            _tree = null;
        }

        // 로비에서 특성을 찍고 빼는 즉시 레벨업 필요 XP·보너스 LP 규칙을 갱신한다(전투 시스템이 없어도).
        private void ApplyMawangRules() => TraitMawangSettings.Apply(_tree.BuildModifiers(), MawangXpBridge.Mawang);
    }

    /// <summary>
    /// 스킬 세팅 ← SkillTreeStore. 해금 표시는 저장된 봉인 해제 상태, 장착 슬롯은 저장된 장착 목록으로 맞추고
    /// 저장 버튼을 누르면 장착 목록을 SkillTreeStore에 기록한다. UI 카탈로그 id(ui_preview_*)는 표시 이름으로 실제 스킬과 대응시킨다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbySkillLoadoutBinder : MonoBehaviour
    {
        private const int UiSlotCount = 3; // UISkillLoadoutPreview.SLOT_COUNT
        private const string StarterSkillName = "화염구";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private UISkillLoadoutPreview _preview;
        private UISkillPreviewCatalogSO _catalog;
        private UILobbyCollectionState _unlockState;
        private readonly Dictionary<string, string> _realIdByEntryId = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _indexByEntryId = new Dictionary<string, int>();
        private readonly Dictionary<string, SkillData> _realDataByEntryId = new Dictionary<string, SkillData>();
        private readonly Dictionary<string, int> _unlockCosts = new Dictionary<string, int>();
        private TraitTree _capacityTraits;
        private bool _applying;
        private bool _isUnlocking;

        private void OnEnable()
        {
            _preview = GetComponent<UISkillLoadoutPreview>();
            if (_preview == null) return;
            // 기존 카탈로그·해금 상태의 직렬화 계약을 유지하며 리플렉션으로 읽는다.
            _catalog = typeof(UISkillLoadoutPreview).GetField("_catalog", Private)?.GetValue(_preview) as UISkillPreviewCatalogSO;
            _unlockState = typeof(UISkillLoadoutPreview).GetField("_unlockState", Private)?.GetValue(_preview) as UILobbyCollectionState;
            if (_catalog == null || _unlockState == null)
            {
                Debug.LogWarning("스킬 세팅 연동 실패: UISkillLoadoutPreview의 _catalog/_unlockState 연결을 찾지 못했습니다.", this);
                return;
            }
            BuildMap();
            if (_preview.UsesHeraldryLayout)
            {
                var sync = FindFirstObjectByType<RealSynergySync>();
                var traits = FindFirstObjectByType<UITraitProgressionController>(FindObjectsInactive.Include);
                _capacityTraits = sync != null && sync.Traits != null ? sync.Traits : traits != null && traits.Tree != null
                    ? traits.Tree : new TraitTree(Resources.LoadAll<TraitData>("Traits"));
                _capacityTraits.Changed += PushAccountState;
                SkillTreeStore.Changed += OnAccountChanged;
                _preview.UnlockRequested += OnUnlockRequested;
                PushAccountState();
            }
            SyncUnlocks();
            ApplyStoredLoadout();
            _preview.SaveRequested += OnSaveRequested;
        }

        private void OnDisable()
        {
            if (_preview != null)
            {
                _preview.SaveRequested -= OnSaveRequested;
                _preview.UnlockRequested -= OnUnlockRequested;
            }
            SkillTreeStore.Changed -= OnAccountChanged;
            if (_capacityTraits != null) _capacityTraits.Changed -= PushAccountState;
            _capacityTraits = null;
            _isUnlocking = false;
        }

        /// <summary>스킬창 준비 여부 — 화면이 한 번도 열리지 않았으면 카탈로그를 아직 못 읽었다.</summary>
        public bool IsReady => _catalog != null && _unlockState != null;

        /// <summary>저장된 해금 상태를 화면에 다시 반영한다(디버그 패널에서 해금을 바꾼 뒤 호출).</summary>
        public void ResyncUnlocks()
        {
            if (IsReady) { PushAccountState(); SyncUnlocks(); }
        }

        /// <summary>지금 화면 슬롯에 놓인 스킬의 실제 id 목록(아직 저장 전 편집 상태 포함).</summary>
        public List<string> ScreenEquippedRealIds()
        {
            var ids = new List<string>();
            if (!IsReady || _preview == null) return ids;
            foreach (var entryId in _preview.GetEquippedIds())
                if (_realIdByEntryId.TryGetValue(entryId, out var realId)) ids.Add(realId);
            return ids;
        }

        private void BuildMap()
        {
            _realIdByEntryId.Clear();
            _indexByEntryId.Clear();
            _realDataByEntryId.Clear();
            _unlockCosts.Clear();
            var realByName = new Dictionary<string, SkillData>();
            foreach (var data in Resources.LoadAll<SkillData>("Skills"))
                if (data != null && !string.IsNullOrWhiteSpace(data.displayName) && !string.IsNullOrWhiteSpace(data.skillId)) realByName[data.displayName] = data;
            for (int i = 0; i < _catalog.Entries.Count; i++)
            {
                var entry = _catalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                _indexByEntryId[entry.Id] = i;
                if (realByName.TryGetValue(entry.DisplayName, out var realData))
                {
                    _realIdByEntryId[entry.Id] = realData.skillId;
                    _realDataByEntryId[entry.Id] = realData;
                    _unlockCosts[entry.Id] = new SkillRuntime(realData).UnlockCost;
                }
                else Debug.LogWarning("스킬 세팅 연동: '" + entry.DisplayName + "'에 대응하는 SkillData를 찾지 못했습니다.", this);
            }
        }

        private bool IsUnlocked(string entryId, string displayName) =>
            _realIdByEntryId.TryGetValue(entryId, out var realId) &&
            (displayName == StarterSkillName || SkillTreeStore.IsUnlocked(realId));

        private void SyncUnlocks()
        {
            foreach (var entry in _catalog.Entries)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                    _unlockState.SetUnlocked(entry.Id, IsUnlocked(entry.Id, entry.DisplayName));
        }

        /// <summary>저장된 장착 목록(+ 항상 자동 장착되는 화염구)을 화면 슬롯에 채운다. UI의 공개 조작 API만 쓴다.</summary>
        private void ApplyStoredLoadout()
        {
            var desired = new List<string>(); // 카탈로그 entry id
            void Add(string entryId)
            {
                int capacity = _preview.UsesHeraldryLayout ? _preview.SlotCapacity : UiSlotCount;
                if (desired.Count < capacity && !desired.Contains(entryId)) desired.Add(entryId);
            }

            // 저장한 적이 없는 신규 계정에게만 기본 스킬(화염구)을 보여 준다. 저장한 뒤에는 저장된 목록 그대로.
            if (!SkillTreeStore.HasSavedEquipment)
                foreach (var entry in _catalog.Entries)
                    if (entry != null && entry.DisplayName == StarterSkillName) Add(entry.Id);
            foreach (var realId in SkillTreeStore.GetEquipped())
                foreach (var pair in _realIdByEntryId)
                    if (pair.Value == realId && IsUnlocked(pair.Key, _catalog.Entries[_indexByEntryId[pair.Key]].DisplayName)) Add(pair.Key);

            _applying = true;
            try
            {
                foreach (var current in _preview.GetEquippedIds())
                {
                    if (desired.Contains(current) || !_indexByEntryId.TryGetValue(current, out var index)) continue;
                    _preview.SelectSkill(index);
                    _preview.UnequipSelected();
                }
                var alreadyEquipped = new HashSet<string>(_preview.GetEquippedIds());
                foreach (var entryId in desired)
                {
                    if (alreadyEquipped.Contains(entryId) || !_indexByEntryId.TryGetValue(entryId, out var index)) continue;
                    _preview.SelectSkill(index);
                    _preview.EquipSelected();
                }
                _preview.SavePreview(); // 화면 초기 상태를 저장 상태로 확정 — 나가기 확인창이 뜨지 않게
            }
            finally { _applying = false; }
        }

        private void OnSaveRequested(IReadOnlyList<string> entryIds)
        {
            if (_applying) return;
            var realIds = new List<string>();
            foreach (var entryId in entryIds)
                if (_realIdByEntryId.TryGetValue(entryId, out var realId)) realIds.Add(realId);
            SkillTreeStore.SetEquipped(realIds);
        }

        private void PushAccountState()
        {
            if (_preview == null || !_preview.UsesHeraldryLayout) return;
            int capacity = 3 + (_capacityTraits != null ? _capacityTraits.BuildModifiers().ExtraSkillSlots : 0);
            _preview.SetAccountState(SkillTreeStore.SkillPoints, capacity, _unlockCosts);
        }

        private void OnAccountChanged()
        {
            if (!isActiveAndEnabled || !IsReady || _preview == null || !_preview.UsesHeraldryLayout) return;
            PushAccountState();
            SyncUnlocks(); // 장착 draft/committed에는 저장 구성을 다시 덮어쓰지 않는다.
        }

        private void OnUnlockRequested(string entryId)
        {
            if (_isUnlocking || !isActiveAndEnabled || _preview == null || !_preview.UsesHeraldryLayout) return;
            _isUnlocking = true;
            bool succeeded = false;
            string message = "현재 해금할 수 없는 스킬입니다.";
            try
            {
                if (!string.IsNullOrWhiteSpace(entryId) && _realDataByEntryId.TryGetValue(entryId, out var data))
                    succeeded = SkillTreeStore.TryUnlock(data, out message);
                OnAccountChanged();
            }
            finally
            {
                _isUnlocking = false;
                _preview.CompleteUnlock(succeeded, succeeded ? "스킬 잠금이 해제되었습니다." : message);
            }
        }
    }
}
