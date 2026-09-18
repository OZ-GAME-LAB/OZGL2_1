using System.Collections.Generic;
using System.Text;
using OZGL2.Progression;
using TMPro;
using UnityEngine;

namespace OZGL2.UIFlow
{
    // 로비 표현과 기존 계정 특성 모델을 연결한다. 전투 효과 계산과 저장 규칙은 TraitTree가 소유한다.
    [DisallowMultipleComponent]
    public sealed class UITraitProgressionController : MonoBehaviour
    {
        [SerializeField] private UITraitOverlayView _view;
        [SerializeField] private UITraitFrameView[] _nodes;
        [SerializeField] private TMP_Text _accountLevel;
        private TraitTree _tree;
        private MawangLevel _account;

        public TraitTree Tree => _tree;
        public MawangLevel Account => _account;

        private void OnEnable()
        {
            if (_view == null || _nodes == null) return;
            var definitions = new List<TraitData>();
            var ids = new HashSet<TraitId>();
            foreach (var node in _nodes)
                if (node != null && node.Definition != null && ids.Add(node.Definition.id))
                    definitions.Add(node.Definition);
            // 화면 재진입 때 전투에서 저장한 최신 LP와 랭크를 다시 읽는다.
            _view.SelectionChanged += RefreshSelection;
            _view.UpgradeRequested += Upgrade;
            _view.DowngradeRequested += Downgrade;
            _view.ResetRequested += ResetTraits;
            Bind(new TraitTree(definitions), new MawangLevel());
        }

        // 외부 계정 서비스가 같은 모델을 공유할 때도 UI와 저장 로직을 분리한다.
        public void Bind(TraitTree tree, MawangLevel account)
        {
            if (tree == null || account == null) throw new System.ArgumentNullException("특성/계정 모델");
            if (_tree != null) _tree.Changed -= Refresh;
            if (_account != null) _account.PointsChanged -= Refresh;
            _tree = tree;
            _account = account;
            if (isActiveAndEnabled)
            {
                _tree.Changed += Refresh;
                _account.PointsChanged += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (_view != null)
            {
                _view.SelectionChanged -= RefreshSelection;
                _view.UpgradeRequested -= Upgrade;
                _view.DowngradeRequested -= Downgrade;
                _view.ResetRequested -= ResetTraits;
            }
            if (_tree != null) _tree.Changed -= Refresh;
            if (_account != null) _account.PointsChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_view == null || _tree == null || _account == null) return;
            _view.SetAvailablePoints(_account.Points);
            _view.ShowResetState(_tree.AllocatedPoints);
            if (_accountLevel != null) _accountLevel.text = "LV. " + _account.Level;
            foreach (var node in _nodes)
                if (node != null && node.Definition != null)
                    node.ShowRank(_tree.RankOf(node.Definition.id), _tree.IsOpen(node.Definition.id));
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_view == null || _view.SelectedFrame == null || _tree == null || _account == null) return;
            var definition = _view.SelectedFrame.Definition;
            if (definition == null) return;
            var id = definition.id;
            int rank = _tree.RankOf(id);
            _view.ShowEffectComparison(rank, definition.maxRank,
                UITraitEffectTextFormatter.Format(definition, rank),
                rank < definition.maxRank ? UITraitEffectTextFormatter.Format(definition, rank + 1) : string.Empty);
            string status;
            if (!_tree.IsOpen(id))
            {
                var names = new StringBuilder("선행 특성 최대 레벨 필요\n");
                foreach (var parent in definition.parents)
                {
                    if (_tree.IsMaxed(parent)) continue;
                    if (names[names.Length - 1] != '\n') names.Append(", ");
                    var parentDef = _tree.Def(parent);
                    names.Append(parentDef != null ? parentDef.displayName : parent.ToString());
                }
                status = names.ToString();
            }
            else if (rank > 0 && !_tree.CanUnrank(id)) status = "후속 특성부터 레벨을 내려주세요.";
            else if (_tree.IsMaxed(id)) status = "최대 레벨에 도달했습니다.";
            else if (!_tree.CanRank(id, _account.Points)) status = "보유 포인트가 부족합니다.";
            else status = "레벨 다운 시\n해당 단계 LP 전액 환급";
            _view.ShowProgression(rank, definition.maxRank, _tree.CanRank(id, _account.Points),
                _tree.CanUnrank(id), _tree.NextCost(id), _tree.RefundCost(id), status);
        }

        private void Upgrade()
        {
            var definition = _view != null && _view.SelectedFrame != null ? _view.SelectedFrame.Definition : null;
            if (definition != null && _tree != null && _account != null)
                _tree.TryRank(definition.id, _account);
        }

        private void Downgrade()
        {
            var definition = _view != null && _view.SelectedFrame != null ? _view.SelectedFrame.Definition : null;
            if (definition != null && _tree != null && _account != null)
                _tree.TryUnrank(definition.id, _account);
        }

        private void ResetTraits()
        {
            if (_tree == null || _account == null || !_tree.TryResetAndRefund(_account)) return;
            if (_view != null) _view.HideDetail();
        }
    }
}
