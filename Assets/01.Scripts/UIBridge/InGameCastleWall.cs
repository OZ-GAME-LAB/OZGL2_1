using OZGL2.InGame;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕 뒤쪽(그리드 아래)에 마왕성 성벽을 세운다 — 마왕이 성벽 앞에서 성문을 등지고 용사들을 막는 모습.
    /// 돌 성벽 한 줄을 가로로 깔고, 양 끝에 돌탑, 마왕 바로 뒤에 성문, 문 양옆에 붉은 깃발을 건다.
    /// 그림은 RF Castle 의 스프라이트 시트(mainlevbuild·decorative)에서 필요한 부분만 잘라 쓰므로 새 이미지가 필요 없다.
    /// 성벽은 배치 가능 영역(맨 아래 칸의 아래쪽 끝) 밑에만 놓여 칸을 가리지 않고, 마왕·유닛·타일보다 아래에 그려진다.
    /// </summary>
    public sealed class InGameCastleWall : MonoBehaviour
    {
        [SerializeField, Tooltip("RF Castle/mainlevbuild.png")] private Texture2D _buildSheet;
        [SerializeField, Tooltip("RF Castle/decorative.png")] private Texture2D _decorSheet;
        [SerializeField, Min(8f), Tooltip("성벽 가로 길이(칸). 화면 양쪽 끝을 넘도록 넉넉히 길게 잡는다")] private float _wallWidth = 90f;
        [SerializeField, Min(2f), Tooltip("성벽이 아래로 이어지는 길이(칸). 화면을 아래로 옮겨도 잘리지 않게 넉넉히 잡는다")] private float _wallDepth = 30f;
        [SerializeField, Min(2f), Tooltip("마왕 가운데에서 양쪽 돌탑까지의 거리(칸)")] private float _towerOffset = 7.6f;
        [SerializeField, Tooltip("성벽 윗면 높이 — 마왕이 선 칸 위쪽 끝 기준(칸 단위). 맨 아래 칸의 아래 끝(-0.5)보다 아래여야 칸을 안 가린다")]
        private float _wallTop = -1.55f;
        [SerializeField, Range(0.3f, 1f)] private float _gateScale = 0.55f;
        [SerializeField, Range(0.3f, 1f)] private float _bannerScale = 0.55f;
        [SerializeField] private int _sortingOrder = -188;

        private const float Ppu = 16f;
        private bool _built;
        private float _next;

        private void Update()
        {
            if (_built || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.4f;
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var config = bootstrap != null ? bootstrap.Config : null;
            if (config == null || config.Catalog == null || _buildSheet == null || _decorSheet == null) return;
            Build(config);
            _built = true;
        }

        private void Build(InGamePrototypeConfigSO config)
        {
            var size = config.Catalog.CreateDefinition().MaximumSize;
            float cell = config.CellWorldSize;
            Vector3 origin = config.GridWorldOrigin;
            float kingX = origin.x + (size.x - 1) * 0.5f * cell;
            float top = origin.y + _wallTop * cell; // _wallTop 은 칸 좌표(맨 아래 칸 중심이 0)

            var root = new GameObject("CastleWall").transform;
            root.SetParent(transform, false);

            // 성벽은 좌우로 화면 밖까지, 아래로도 화면 밖까지 이어 붙여서 카메라를 옮기거나 확대·축소해도 잘리지 않게 한다.
            // 윗면 띠(돌턱) 한 줄 + 그 아래 벽돌 몸통(벽돌 사이에 돌기둥이 끼는 48x48 조각을 반복)
            float tileW = 48f / Ppu;
            float width = Mathf.Max(3f, Mathf.Round(_wallWidth / tileW)) * tileW;
            float bandH = 8f / Ppu;

            var band = Cut(_buildSheet, 159, 119, 48, 8, new Vector2(0.5f, 1f));
            var bandR = Renderer(root, "WallTop", band, new Vector3(kingX, top, 0f), _sortingOrder + 1);
            bandR.drawMode = SpriteDrawMode.Tiled; bandR.tileMode = SpriteTileMode.Continuous;
            bandR.size = new Vector2(width, bandH);

            var body = Cut(_buildSheet, 159, 127, 48, 48, new Vector2(0.5f, 1f));
            var bodyR = Renderer(root, "WallBody", body, new Vector3(kingX, top - bandH, 0f), _sortingOrder);
            bodyR.drawMode = SpriteDrawMode.Tiled; bodyR.tileMode = SpriteTileMode.Continuous;
            bodyR.size = new Vector2(width, _wallDepth);

            // 양쪽 돌탑(성벽보다 높다 — 그리드 가로 범위 밖이라 칸을 가리지 않는다)
            var tower = Cut(_buildSheet, 368, 112, 32, 80, new Vector2(0.5f, 0f));
            float towerX = _towerOffset;
            for (int side = -1; side <= 1; side += 2)
                Renderer(root, "Tower", tower, new Vector3(kingX + side * towerX, top - 3.4f, 0f), _sortingOrder + 2);

            // 마왕 바로 뒤 성문(성벽 윗면에서 아래로 걸린다)
            var gate = Cut(_buildSheet, 416, 209, 80, 95, new Vector2(0.5f, 1f));
            var gateR = Renderer(root, "Gate", gate, new Vector3(kingX, top - 0.2f, 0f), _sortingOrder + 2);
            gateR.transform.localScale = Vector3.one * _gateScale;

            // 문 양옆 붉은 깃발(성벽 윗면 바로 아래에 걸린다)
            var banner = Cut(_decorSheet, 16, 16, 48, 47, new Vector2(0.5f, 1f));
            float gateHalf = 80f / Ppu * _gateScale * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                var b = Renderer(root, "Banner", banner, new Vector3(kingX + side * (gateHalf + 1.7f), top - 0.15f, 0f), _sortingOrder + 3);
                b.transform.localScale = Vector3.one * _bannerScale;
            }
        }

        private static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>시트에서 사각형 하나를 스프라이트로 자른다. 좌표는 이미지 왼쪽 위 기준 픽셀(그림판 좌표).</summary>
        private static Sprite Cut(Texture2D sheet, int x, int yFromTop, int w, int h, Vector2 pivot)
        {
            var rect = new Rect(x, sheet.height - yFromTop - h, w, h);
            return Sprite.Create(sheet, rect, pivot, Ppu, 0, SpriteMeshType.FullRect);
        }
    }
}
