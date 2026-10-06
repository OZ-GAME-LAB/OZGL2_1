using System.Collections.Generic;
using OZGL2.InGame;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 난이도마다 용사 진영(움막) 수를 바꾼다: 보통 1개, 어려움 가로로 3개, 지옥 가로로 5개.
    /// 용사는 움막 입구에서 차례로 나온다(움막 수만큼 스폰 위치를 돌려 가며 쓴다).
    /// 가운데 움막은 건의 배경 설정(OutdoorBattlefieldConfig)에 이미 있으므로 나머지 움막만 이 컴포넌트가 더 놓는다.
    /// 선택한 스테이지는 부트스트랩이 가져가기 전(Awake)에 StageLaunchRuntime 에서 미리 읽는다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class InGameCampLayout : MonoBehaviour
    {
        [SerializeField] private Sprite _tentSprite;
        [SerializeField] private Sprite _cratesSprite;
        [SerializeField] private Sprite _barrelSprite;
        [SerializeField, Tooltip("움막 중심의 높이(칸). 건의 배경 설정에서 옮겨 둔 가운데 움막과 같아야 한다.")] private float _tentY = 11.6f;
        [SerializeField, Tooltip("용사가 나오는 높이(칸) — 움막 입구 바로 아래")] private float _doorY = 10.5f;
        [SerializeField, Min(2f), Tooltip("움막 사이 가로 간격(칸)")] private float _spacing = 4.5f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Awake()
        {
            var pending = StageLaunchRuntime.Session != null ? StageLaunchRuntime.Session.Pending : null;
            string stageId = pending != null ? pending.StageId : string.Empty;
            int tents = stageId.Contains("hell") ? 5 : stageId.Contains("hard") ? 3 : 1;

            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var config = bootstrap != null ? bootstrap.Config : null;
            if (config == null) { InGamePrototypeConfigSO.HeroSpawnOverride = null; return; }

            Vector3 origin = config.GridWorldOrigin;
            float cell = config.CellWorldSize;
            float centerX = (config.Catalog.CreateDefinition().MaximumSize.x - 1) * 0.5f;

            var xs = new float[tents];
            for (int i = 0; i < tents; i++) xs[i] = centerX + (i - (tents - 1) * 0.5f) * _spacing;

            // 움막이 하나뿐이면 원래 스폰 방식(세 곳)을 그대로 쓴다
            if (tents <= 1) InGamePrototypeConfigSO.HeroSpawnOverride = null;
            else
            {
                var doors = new Vector3[tents];
                for (int i = 0; i < tents; i++) doors[i] = origin + new Vector3(xs[i], _doorY, 0f) * cell;
                InGamePrototypeConfigSO.HeroSpawnOverride = () => doors;
            }

            // 가운데(원래 있는 움막)를 뺀 나머지 움막과 상자·통나무
            for (int i = 0; i < tents; i++)
            {
                if (Mathf.Abs(xs[i] - centerX) < 0.01f) continue;
                Spawn("Tent_" + i, _tentSprite, origin, cell, xs[i], _tentY);
                Spawn("Crates_" + i, _cratesSprite, origin, cell, xs[i] - 2.3f, _tentY - 0.75f);
                Spawn("Barrel_" + i, _barrelSprite, origin, cell, xs[i] + 2.4f, _tentY - 0.95f);
            }
        }

        private void Spawn(string objectName, Sprite sprite, Vector3 origin, float cell, float x, float y)
        {
            if (sprite == null) return;
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.position = origin + new Vector3(x, y, 0f) * cell;
            go.transform.localScale = Vector3.one * cell;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -190; // 건의 장식과 같은 층(바닥 위, 그리드 아래)
            _spawned.Add(go);
        }

        private void OnDestroy()
        {
            InGamePrototypeConfigSO.HeroSpawnOverride = null;
            foreach (var go in _spawned) if (go != null) Destroy(go);
        }
    }
}
