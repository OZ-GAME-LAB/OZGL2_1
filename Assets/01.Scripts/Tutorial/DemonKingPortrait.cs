using UnityEngine;

namespace OZGL2.Tutorial
{
    /// <summary>
    /// 마왕(DemonKing 프리팹)을 화면 밖에서 살아 움직이는 채로 그려 RenderTexture로 내보낸다. UI의 RawImage에 붙이면
    /// 대기(idle) 동작을 하는 마왕이 대화창 옆에 서 있다(쿠키런 설명 캐릭터처럼).
    /// 프리팹의 SPUM 그림·애니메이터만 복제하고 스크립트는 모두 떼어내므로 전투에 등록되거나 동작하지 않는다.
    /// 카메라는 보일 때만 켠다.
    /// </summary>
    public sealed class DemonKingPortrait : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(64)] private int _size = 384;
        [SerializeField] private Vector3 _stage = new Vector3(6000f, 6000f, 0f);

        public RenderTexture Texture { get; private set; }

        private GameObject _root;
        private Camera _camera;
        private SPUM_Prefabs _spum;
        private float _returnToIdleAt = -1f;

        public bool IsReady => Texture != null && _root != null;

        public void Build(GameObject prefab)
        {
            if (IsReady) return;
            if (prefab == null) prefab = _prefab;
            if (prefab == null) return;

            // 비활성 부모 아래에서 만들어 스크립트를 떼어낸 뒤 켠다(켜기 전에는 Awake/OnEnable이 돌지 않는다)
            var holder = new GameObject("DemonKingPortraitHolder");
            holder.transform.SetParent(transform, false);
            holder.SetActive(false);
            var visual = Instantiate(prefab, holder.transform);
            visual.name = "DemonKingVisual";
            for (int pass = 0; pass < 2; pass++)
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

            _root = new GameObject("DemonKingPortraitStage");
            _root.transform.SetParent(transform, false);
            _root.transform.position = _stage;
            visual.transform.SetParent(_root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            Destroy(holder);
            // 전투 설명 중 게임이 멈춰(timeScale 0) 있어도 마왕은 계속 움직이게 한다
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            visual.SetActive(true);

            _spum = visual.GetComponentInChildren<SPUM_Prefabs>(true);
            if (_spum != null)
            {
                if (!_spum.allListsHaveItemsExist()) _spum.PopulateAnimationLists();
                _spum.OverrideControllerInit();
                Play(PlayerState.IDLE);
            }

            // 첫 프레임 모양으로 화면 틀을 잡는다(몸 전체가 들어오게 여유를 둔다)
            Bounds bounds = default;
            bool any = false;
            foreach (var r in visual.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r.sprite == null || r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            if (!any) bounds = new Bounds(_stage, Vector3.one * 2f);

            var camGo = new GameObject("DemonKingPortraitCamera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = ~0;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
            _camera.depth = -50f;
            _camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * 0.5f * 1.2f;
            camGo.transform.position = new Vector3(bounds.center.x, bounds.center.y + bounds.size.y * 0.04f, -10f);
            Texture = new RenderTexture(_size, _size, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _camera.targetTexture = Texture;
            _camera.enabled = false;
            _root.SetActive(false);
        }

        /// <summary>보일 때만 켠다(꺼 두면 그리지 않는다).</summary>
        public void SetShown(bool shown)
        {
            if (!IsReady) return;
            _root.SetActive(shown);
            _camera.enabled = shown;
            if (shown) Play(PlayerState.IDLE);
        }

        public void Play(PlayerState state)
        {
            if (_spum == null) return;
            if (_spum.StateAnimationPairs.TryGetValue(state.ToString(), out var clips) && clips.Count > 0)
                _spum.PlayAnimation(state, 0);
        }

        /// <summary>몸짓(공격 모션)을 한 번 하고 잠시 뒤 대기로 돌아간다.</summary>
        public void Gesture()
        {
            if (_spum == null || !_root.activeInHierarchy) return;
            Play(PlayerState.ATTACK);
            _returnToIdleAt = Time.unscaledTime + 0.9f;
        }

        private void Update()
        {
            if (_returnToIdleAt > 0f && Time.unscaledTime >= _returnToIdleAt)
            {
                _returnToIdleAt = -1f;
                Play(PlayerState.IDLE);
            }
        }

        private void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
