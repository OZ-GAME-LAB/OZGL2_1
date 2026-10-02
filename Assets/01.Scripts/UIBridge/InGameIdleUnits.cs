using System.Collections.Generic;
using System.Reflection;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.InGame;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 배치 단계의 마왕군 유닛이 가만히 서 있던 것을 대기(idle) 애니메이션으로 움직이게 한다.
    /// 배치 화면의 유닛은 전투 스크립트가 없는 "스프라이트만 복사한 그림"이라 애니메이션이 없다. 그 그림을 숨기고 같은 자리에
    /// 유닛 프리팹의 그림 구조(SPUM)를 그대로 복제해 idle을 재생한다.
    /// 복제본은 비활성 부모 아래에서 만들고 전투 스크립트(UnitBase 등)를 떼어낸 뒤 켜므로, 전투에 등록되거나 동작하지 않는다.
    /// 팀 파일(GridWorldPreparationView, UnitBase)은 수정하지 않는다.
    /// </summary>
    public sealed class InGameIdleUnits : MonoBehaviour
    {
        private const string VisualName = "IdleVisual";
        private const string ActorPrefix = "Preview_";

        private InGamePrototypeBootstrap _bootstrap;
        private GridPrototypeRunner _runner;
        private Transform _holder;
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.15f;

            var root = GameObject.Find("PreparationVisuals");
            if (root == null) return; // 배치 화면이 보이는 동안에만 있다
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_runner == null) _runner = FindFirstObjectByType<GridPrototypeRunner>(FindObjectsInactive.Include);
            var manager = _runner != null ? _runner.Manager : null;
            var army = _bootstrap != null && _bootstrap.Config != null ? _bootstrap.Config.DemonArmyCatalog : null;
            if (manager == null || army == null) return;

            foreach (Transform actor in root.transform)
            {
                if (!actor.name.StartsWith(ActorPrefix) || actor.Find(VisualName) != null) continue;
                string instanceId = actor.name.Substring(ActorPrefix.Length);
                var unit = manager.FindUnit(instanceId);
                if (unit == null) continue;
                var prefab = army.FindPrefab(unit.Definition.Id, unit.StarLevel);
                if (prefab == null) continue;
                AttachIdle(actor, prefab);
            }
        }

        private void AttachIdle(Transform actor, UnitBase prefab)
        {
            if (_holder == null)
            {
                var h = new GameObject("IdleUnitHolder");
                h.transform.SetParent(transform, false);
                h.SetActive(false); // 이 아래에서 만든 복제본은 켜기 전까지 Awake/OnEnable이 돌지 않는다
                _holder = h.transform;
            }

            var visual = Instantiate(prefab.gameObject, _holder);
            visual.name = VisualName;

            // 전투·물리 관련 컴포넌트를 모두 떼어낸다(SPUM 그림·애니메이터만 남긴다)
            for (int pass = 0; pass < 2; pass++) // 1차: UnitBase가 아닌 것 · 2차: UnitBase 계열(다른 컴포넌트가 의존하므로 나중에)
                foreach (var mb in visual.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName.StartsWith("SPUM") || typeName == "SpritePos") continue;
                    if ((pass == 0) == (mb is UnitBase)) continue;
                    DestroyImmediate(mb);
                }
            foreach (var rb in visual.GetComponentsInChildren<Rigidbody2D>(true)) DestroyImmediate(rb);
            foreach (var c in visual.GetComponentsInChildren<Collider2D>(true)) DestroyImmediate(c);

            // 복사 그림이 이미 원본의 배율을 갖고 있으므로 같은 자리·같은 크기로 둔다
            Transform t = visual.transform;
            t.SetParent(actor, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            // 정적인 복사 그림은 숨긴다(별 배지 글자는 별도 컴포넌트라 그대로 둔다)
            foreach (var sr in actor.GetComponentsInChildren<SpriteRenderer>(true))
                if (!sr.transform.IsChildOf(t)) sr.enabled = false;

            visual.SetActive(true);
            var spum = visual.GetComponentInChildren<SPUM_Prefabs>(true);
            if (spum != null)
            {
                if (!spum.allListsHaveItemsExist()) spum.PopulateAnimationLists();
                spum.OverrideControllerInit();
                if (spum.StateAnimationPairs.TryGetValue(PlayerState.IDLE.ToString(), out var idle) && idle.Count > 0)
                    spum.PlayAnimation(PlayerState.IDLE, 0);
            }
        }
    }
}
