using System;
using OZGL2.Grid;
using OZGL2.Grid.UI;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>월드 타일 표시만 소유한다. 전투 연결은 Bootstrap에서 관리한다.</summary>
    public sealed class InGameGridPresentation : MonoBehaviour
    {
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private GridBoardThemeSO _theme;
        private GridRunSession _session;
        private GridWorldBoardView _board;
        private void Start()
        {
            _bootstrap.Changed += Bind;
            Bind();
        }
        private void Bind()
        {
            var next = _bootstrap.GridSession;
            if (next == _session && (next == null || !next.IsEnded)) return;
            Unbind();
            if (next == null || next.IsEnded) return;
            _session = next;
            var config = _bootstrap.Config;
            if (_theme != null)
                _board = new GridWorldBoardView(next.Grid, _theme,
                    new GridWorldMapping(config.GridWorldOrigin, Vector3.right * config.CellWorldSize, Vector3.up * config.CellWorldSize),
                    config.CellWorldSize, transform);
        }
        private void Unbind()
        {
            _session = null; _board?.Dispose(); _board = null;
        }
        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.Changed -= Bind;
            Unbind();
        }
    }
}
