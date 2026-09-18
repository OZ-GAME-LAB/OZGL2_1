using UnityEngine;

namespace OZGL2.UIFlow
{
    // 전투 준비/진행 패널이나 탭처럼 Scene 안에 존재하는 페이지의 활성 상태만 전환한다.
    public sealed class UIPageGroup : MonoBehaviour
    {
        [SerializeField] private GameObject[] _pages;

        public void ShowPage(GameObject target)
        {
            if (_pages == null || target == null || System.Array.IndexOf(_pages, target) < 0) return;
            foreach (GameObject page in _pages)
                if (page != null) page.SetActive(page == target);
        }
    }
}
