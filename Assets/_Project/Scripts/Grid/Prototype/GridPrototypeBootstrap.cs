using UnityEngine;

namespace OZGL2.Grid.Prototype
{
    /// <summary>JOB_KIMGUN 단독 실행 전용. 실제 Stage가 주입하는 화면에는 이 컴포넌트를 사용하지 않는다.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(GridPrototypeRunner))]
    public sealed class GridPrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private GridPrototypeCatalogSO _catalog;
        private void Start()
        {
            var view = GetComponent<GridPrototypeRunner>();
            if (view.Session != null || _catalog == null) return;
            var host = new GameObject(nameof(GridPrototypeHost)).AddComponent<GridPrototypeHost>();
            host.Initialize(_catalog, view);
        }
    }
}
