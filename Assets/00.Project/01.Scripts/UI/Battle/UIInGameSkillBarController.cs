using System;
using System.Collections.Generic;
using OZGL2.InGame;
using OZGL2.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 실제 장착 스킬을 전투 HUD 슬롯에 표시한다.
    /// 장착/쿨다운 데이터는 소유하지 않고 InGamePrototypeBootstrap의 현재 SkillManager만 읽는다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Battle/InGame Skill Bar Controller")]
    public sealed class UIInGameSkillBarController : MonoBehaviour
    {
        private const float EQUIPMENT_REFRESH_INTERVAL = 0.25f;

        [Header("InGame 연결")]
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;

        [Header("Scene 전용 슬롯")]
        [SerializeField] private RectTransform _slotContainer;
        [SerializeField] private UICombatSkillSlotView[] _sceneSlots = Array.Empty<UICombatSkillSlotView>();
        [SerializeField] private UISkillPreviewCatalogSO _catalog;

        [Header("자동 정렬")]
        [SerializeField, Min(1)] private int _maximumSlots = 5;
        [SerializeField] private float _spacing = 19f;
        [SerializeField, Min(0)] private int _bottomPadding = 56;

        private readonly List<SlotBinding> _slots = new List<SlotBinding>(5);
        private readonly List<SkillRuntime> _equipped = new List<SkillRuntime>(5);
        private readonly List<RectTransform> _cancelAreas = new List<RectTransform>(5);
        private readonly HashSet<string> _missingVisualWarnings = new HashSet<string>();
        private SkillManager _manager;
        private float _nextEquipmentRefreshTime;
        private bool _isExternalSkinActive;

        public bool IsConfigured => _bootstrap != null && _slotContainer != null &&
            _sceneSlots != null && _sceneSlots.Length > 0;

        public void Configure(
            InGamePrototypeBootstrap bootstrap,
            RectTransform slotContainer,
            UICombatSkillSlotView[] sceneSlots,
            UISkillPreviewCatalogSO catalog = null)
        {
            if (Application.isPlaying && isActiveAndEnabled && _bootstrap != null)
            {
                _bootstrap.Changed -= HandleBootstrapChanged;
                if (_isExternalSkinActive) _bootstrap.SetExternalSkillUiActive(false);
            }
            _isExternalSkinActive = false;
            _bootstrap = bootstrap;
            _slotContainer = slotContainer;
            _sceneSlots = sceneSlots ?? Array.Empty<UICombatSkillSlotView>();
            _catalog = catalog != null ? catalog : FindCatalog(_sceneSlots);

            if (Application.isPlaying && isActiveAndEnabled)
            {
                if (_bootstrap != null) _bootstrap.Changed += HandleBootstrapChanged;
                ClearBindings();
                InitializeLayout();
                RefreshBinding(true);
                RefreshExternalSkinState();
            }
        }

        private void OnEnable()
        {
            if (_bootstrap != null) _bootstrap.Changed += HandleBootstrapChanged;
            InitializeLayout();
            RefreshBinding(true);
            RefreshExternalSkinState();
        }

        private void OnDisable()
        {
            if (_bootstrap != null) _bootstrap.Changed -= HandleBootstrapChanged;
            _bootstrap?.CancelSkillInput();
            if (_isExternalSkinActive) _bootstrap?.SetExternalSkillUiActive(false);
            _isExternalSkinActive = false;
            ClearBindings();
            _manager = null;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextEquipmentRefreshTime)
            {
                _nextEquipmentRefreshTime = Time.unscaledTime + EQUIPMENT_REFRESH_INTERVAL;
                RefreshBinding(false);
                RefreshExternalSkinState();
            }
            RefreshCooldowns();
        }

        private void HandleBootstrapChanged()
        {
            RefreshBinding(true);
            RefreshExternalSkinState();
        }

        private void InitializeLayout()
        {
            if (_slotContainer == null) return;

            HorizontalLayoutGroup layout = _slotContainer.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = _slotContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = _spacing;
            layout.padding = new RectOffset(0, 0, 0, _bottomPadding);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (_slots.Count == 0) CacheSceneSlots();
        }

        private void CacheSceneSlots()
        {
            if (_slotContainer == null) return;

            var roots = new HashSet<Transform>();
            bool hasConfiguredSlots = _sceneSlots != null && _sceneSlots.Length > 0;
            UICombatSkillSlotView[] candidates = hasConfiguredSlots
                ? _sceneSlots
                : _slotContainer.GetComponentsInChildren<UICombatSkillSlotView>(true);

            foreach (UICombatSkillSlotView view in candidates)
            {
                if (view == null) continue;
                Transform root = FindDirectChild(view.transform, _slotContainer);
                if (root == null || (!hasConfiguredSlots && !root.gameObject.activeSelf) ||
                    root.name.StartsWith("Legacy_", StringComparison.Ordinal) || !roots.Add(root)) continue;

                AddBinding(root.GetComponent<RectTransform>(), view);
                if (_slots.Count >= Mathf.Max(1, _maximumSlots)) break;
            }

            if (_catalog == null) _catalog = FindCatalog(candidates);
        }

        private void EnsureSlotCount(int count)
        {
            int targetCount = Mathf.Min(Mathf.Max(0, count), Mathf.Max(1, _maximumSlots));
            if (_slots.Count == 0) CacheSceneSlots();
            if (_slots.Count == 0 || _slotContainer == null) return;

            SlotBinding template = _slots[0];
            while (_slots.Count < targetCount)
            {
                GameObject clone = Instantiate(template.Root.gameObject, _slotContainer, false);
                clone.name = "SkillSlot_Runtime_" + (_slots.Count + 1);
                UICombatSkillSlotView view = clone.GetComponentInChildren<UICombatSkillSlotView>(true);
                AddBinding(clone.GetComponent<RectTransform>(), view);
            }
        }

        private void AddBinding(RectTransform root, UICombatSkillSlotView view)
        {
            if (root == null || view == null) return;
            Button button = root.GetComponentInChildren<Button>(true);
            if (button == null) return;
            UIInGameSkillInputRelay inputRelay = button != null
                ? button.GetComponent<UIInGameSkillInputRelay>()
                : root.GetComponentInChildren<UIInGameSkillInputRelay>(true);
            if (inputRelay == null && button != null)
                inputRelay = button.gameObject.AddComponent<UIInGameSkillInputRelay>();
            if (inputRelay == null) return;

            var binding = new SlotBinding(root, view, button, inputRelay);
            if (button != null)
            {
                button.enabled = true;
                if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
            }
            inputRelay?.Configure(_ => TryBeginCast(binding));
            _slots.Add(binding);
        }

        private void RefreshBinding(bool force)
        {
            SkillManager manager = _bootstrap != null ? _bootstrap.SkillManager : null;
            if (!force && IsEquipmentCurrent(manager)) return;

            _manager = manager;
            _equipped.Clear();
            if (_manager != null)
            {
                foreach (SkillRuntime skill in _manager.EquippedSkills)
                {
                    if (skill != null) _equipped.Add(skill);
                    if (_equipped.Count >= Mathf.Max(1, _maximumSlots)) break;
                }
            }

            EnsureSlotCount(_equipped.Count);
            for (int i = 0; i < _slots.Count; i++)
            {
                SlotBinding binding = _slots[i];
                SkillRuntime skill = i < _equipped.Count ? _equipped[i] : null;
                binding.Skill = skill;
                binding.Root.gameObject.SetActive(skill != null);
                if (skill == null) continue;

                SkillData data = skill.Data;
                UISkillPreviewCatalogSO.Entry entry = FindVisualEntry(data);
                binding.View.ShowSkill(entry);
                string warningKey = data != null ? data.skillId : "<missing-data-" + i + ">";
                if (entry == null && _missingVisualWarnings.Add(warningKey))
                    Debug.LogWarning("전투 스킬 아이콘 매핑을 찾을 수 없습니다: " +
                        (data != null ? data.displayName : "데이터 없음"), this);
            }
        }

        private void RefreshCooldowns()
        {
            float now = Time.time;
            bool canCast = _manager != null && _manager.IsCastingEnabled &&
                _bootstrap != null && !_bootstrap.IsUiInputBlocked;

            foreach (SlotBinding binding in _slots)
            {
                SkillRuntime skill = binding.Skill;
                if (skill == null || !binding.Root.gameObject.activeSelf) continue;
                binding.View.SetCooldown(skill.RemainingCooldown(now), skill.EffectiveCooldown);
                if (binding.Button != null)
                    binding.Button.interactable = canCast && skill.IsReady(now);
            }
        }

        private void TryBeginCast(SlotBinding binding)
        {
            SkillRuntime skill = binding?.Skill;
            if (skill == null || binding.Button == null || !binding.Button.interactable ||
                _bootstrap == null || _bootstrap.IsUiInputBlocked) return;

            _cancelAreas.Clear();
            foreach (SlotBinding slot in _slots)
                if (slot.Root != null && slot.Root.gameObject.activeInHierarchy)
                    _cancelAreas.Add(slot.Root);
            _bootstrap.TryBeginSkillInput(skill, _cancelAreas);
        }

        private void RefreshExternalSkinState()
        {
            bool shouldUseExternalSkin = isActiveAndEnabled && _bootstrap != null && _slots.Count > 0;
            if (_isExternalSkinActive == shouldUseExternalSkin) return;

            _bootstrap?.SetExternalSkillUiActive(shouldUseExternalSkin);
            _isExternalSkinActive = shouldUseExternalSkin;
        }

        private UISkillPreviewCatalogSO.Entry FindVisualEntry(SkillData data)
        {
            if (data == null || _catalog == null || _catalog.Entries == null) return null;
            foreach (UISkillPreviewCatalogSO.Entry entry in _catalog.Entries)
                if (entry != null && string.Equals(entry.DisplayName, data.displayName, StringComparison.Ordinal))
                    return entry;
            return null;
        }

        private bool IsEquipmentCurrent(SkillManager manager)
        {
            if (!ReferenceEquals(manager, _manager)) return false;
            if (manager == null) return _equipped.Count == 0;

            int index = 0;
            foreach (SkillRuntime skill in manager.EquippedSkills)
            {
                if (skill == null) continue;
                if (index >= Mathf.Max(1, _maximumSlots)) break;
                if (index >= _equipped.Count || !ReferenceEquals(skill, _equipped[index])) return false;
                index++;
            }
            return index == _equipped.Count;
        }

        private static UISkillPreviewCatalogSO FindCatalog(UICombatSkillSlotView[] views)
        {
            if (views == null) return null;
            foreach (UICombatSkillSlotView view in views)
                if (view != null && view.PreviewCatalog != null) return view.PreviewCatalog;
            return null;
        }

        private static Transform FindDirectChild(Transform child, Transform parent)
        {
            if (child == null || parent == null || !child.IsChildOf(parent)) return null;
            Transform current = child;
            while (current.parent != null && current.parent != parent) current = current.parent;
            return current.parent == parent ? current : null;
        }

        private void ClearBindings()
        {
            foreach (SlotBinding binding in _slots)
            {
                binding.InputRelay?.Clear();
                if (binding.Root != null && binding.Root.name.StartsWith("SkillSlot_Runtime_", StringComparison.Ordinal))
                {
                    if (Application.isPlaying) Destroy(binding.Root.gameObject);
                    else DestroyImmediate(binding.Root.gameObject);
                }
                else if (binding.Root != null)
                {
                    if (binding.Button != null)
                    {
                        binding.Button.enabled = false;
                        if (binding.Button.targetGraphic != null)
                            binding.Button.targetGraphic.raycastTarget = false;
                    }
                    binding.Root.gameObject.SetActive(false);
                }
            }
            _slots.Clear();
            _equipped.Clear();
            _cancelAreas.Clear();
        }

        private sealed class SlotBinding
        {
            public RectTransform Root { get; }
            public UICombatSkillSlotView View { get; }
            public Button Button { get; }
            public UIInGameSkillInputRelay InputRelay { get; }
            public SkillRuntime Skill { get; set; }

            public SlotBinding(
                RectTransform root,
                UICombatSkillSlotView view,
                Button button,
                UIInGameSkillInputRelay inputRelay)
            {
                Root = root;
                View = view;
                Button = button;
                InputRelay = inputRelay;
            }
        }
    }
}
