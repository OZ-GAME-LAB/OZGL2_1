using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.Grid.Prototype
{
    /// <summary>단독 검증용 실행 소유자. 실제 통합 시 스테이지 소유자가 Session을 화면에 주입한다.</summary>
    public sealed class GridPrototypeHost : MonoBehaviour
    {
        private string _preparationScenePath;
        public GridRunSession Session { get; private set; }
        public GridPrototypeFlow Flow { get; private set; }
        public void Initialize(GridPrototypeCatalogSO catalog, GridPrototypeRunner view)
        {
            if (Session != null) throw new InvalidOperationException("Host is already initialized.");
            Session = new GridRunSession(Guid.NewGuid().ToString("N"), catalog.CreateDefinition());
            var grid = Session.Grid;
            foreach (var block in catalog.CreateBlocks()) grid.AddBlock("block_" + block.Id, block.Id, block);
            foreach (var unit in catalog.CreateUnits()) grid.AddUnit("unit_" + unit.Id, unit);
            Flow = new GridPrototypeFlow(Session, new GridPrototypeRewards(catalog));
            Session.TryAllowPreparation(Session.RunId, 1, false);
            _preparationScenePath = view.gameObject.scene.path;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            view.Bind(Session, Flow);
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Session == null || Session.IsEnded || scene.path != _preparationScenePath) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var view in root.GetComponentsInChildren<GridPrototypeRunner>(true))
                    if (view.Session == null || ReferenceEquals(view.Session, Session)) view.Bind(Session, Flow);
        }
        public void EndRun() { Session?.Dispose(); Destroy(gameObject); }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Session?.Dispose();
        }
    }
}
