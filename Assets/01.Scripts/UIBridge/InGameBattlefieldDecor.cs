using System;
using System.Collections.Generic;
using OZGL2.InGame;
using OZGL2.Progression;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 난이도마다 전장 분위기를 다르게 꾸민다 — 건의 배경(풀밭·천막·소품) 위에 얹는 장식 층이다.
    /// - 보통: 싱싱한 초록 풀밭. 들꽃 무더기, 바위, 작은 그루터기, 떠다니는 꽃가루.
    /// - 어려움: 시들어 가는 황무지. 마른 덤불·가지, 큰 그루터기, 뼈와 쓰러진 용사, 횃불, 흩날리는 재.
    /// - 지옥: 불타는 대지. 용암 웅덩이와 갈라진 틈, 타 버린 나무, 뼈 무더기, 붉은 횃불, 피어오르는 불씨, 붉게 가라앉은 하늘빛.
    /// 장식은 천막·용사가 지나는 길·배치 영역·성벽을 피해서 놓고, 난이도마다 같은 자리에 같은 모양이 나오게(고정 시드) 한다.
    /// 그림은 RF Castle 의 소품 시트에서 자른 것과 코드로 만든 도트(꽃·바위·용암·불꽃)를 섞어 쓴다.
    /// </summary>
    public sealed class InGameBattlefieldDecor : MonoBehaviour
    {
        [SerializeField, Tooltip("RF Castle/decorative.png")] private Texture2D _decorSheet;
        [SerializeField, Tooltip("뼈·쓰러진 용사 스프라이트(WitheredGround/Decorations)")] private Sprite[] _bones;
                [SerializeField, Range(0.3f, 2f), Tooltip("장식 개수 배율")] private float _density = 1f;

        private enum Mood { Normal, Hard, Hell }

        private const float SheetPpu = 22f;
        private Mood _mood;
        private string _stageId;
        private bool _built;
        private float _next;
        private Transform _root;
        private System.Random _rng;
        private readonly List<Vector2> _placed = new List<Vector2>();
        private readonly List<Ambient> _ambient = new List<Ambient>();
        private readonly List<Flicker> _flickers = new List<Flicker>();
        private float _cx, _gridTopY, _gridMaxX;
        private Vector3 _origin;
        private float _cell = 1f;
        private Sprite _soft, _dot;

        private struct Ambient { public Transform tf; public SpriteRenderer sr; public Vector2 home; public float speed, sway, phase, size; public Vector2 span; }
        private struct Flicker { public SpriteRenderer glow; public Transform flame; public float baseAlpha, baseScale, phase; }

        private void Awake()
        {
            var pending = StageLaunchRuntime.Session != null ? StageLaunchRuntime.Session.Pending : null;
            _stageId = pending != null ? pending.StageId : string.Empty;
            int index = Array.IndexOf(StageClearStore.OrderedStageIds, _stageId);
            _mood = index == 1 ? Mood.Hard : index >= 2 ? Mood.Hell : Mood.Normal;
        }

        private void Update()
        {
            if (!_built)
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 0.4f;
                var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
                var config = bootstrap != null ? bootstrap.Config : null;
                if (config == null || config.Catalog == null || _decorSheet == null) return;
                Build(config);
                _built = true;
                return;
            }
            Animate(Time.unscaledDeltaTime);
        }

        // ───────────── 만들기

        private void Build(InGamePrototypeConfigSO config)
        {
            var size = config.Catalog.CreateDefinition().MaximumSize;
            _cell = config.CellWorldSize;
            _origin = config.GridWorldOrigin;
            _cx = (size.x - 1) * 0.5f;
            _gridMaxX = size.x;
            _gridTopY = size.y;
            _rng = new System.Random(_stageId.GetHashCode() ^ 0x5F3759DF);
            _root = new GameObject("BattlefieldDecor_" + _mood).transform;
            _root.SetParent(transform, false);
            _soft = MakeSoft();
            _dot = MakeDot();

            AddTint();
            switch (_mood)
            {
                case Mood.Normal: BuildNormal(); break;
                case Mood.Hard: BuildHard(); break;
                default: BuildHell(); break;
            }
        }

        private void AddTint()
        {
            Color c = _mood == Mood.Normal ? new Color(0.45f, 0.80f, 0.30f, 0.12f)
                    : _mood == Mood.Hard ? new Color(0.36f, 0.30f, 0.24f, 0.24f)
                    : new Color(0.55f, 0.07f, 0.04f, 0.36f);
            var go = new GameObject("Tint");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(_origin.x + _cx * _cell, _origin.y + 8f * _cell, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _dot; sr.color = c; sr.sortingOrder = -193;
            go.transform.localScale = new Vector3(400f, 400f, 1f);
        }

        // 보통 — 싱싱한 풀밭: 옛 전투의 흔적(해골·뼈)과 바위만 가볍게
        private void BuildNormal()
        {
            var rocks = new[] { MakeRock(0), MakeRock(1), MakeRock(2) };
            var stump = Cut(164, 132, 26, 20, 0.5f, 0f);
            Scatter(Scaled(12), 2.2f, p => AddProp(rocks[_rng.Next(rocks.Length)], p, 1.5f, true));
            Scatter(Scaled(3), 3.4f, p => AddProp(stump, p, 1f, true));
            BoneField(Scaled(10), 3, 1.0f, Color.white);
        }

        // 어려움 — 오래된 전장: 해골과 뼈가 훨씬 많고, 마른 가지와 그루터기가 섞인다
        private void BuildHard()
        {
            var branch = Cut(199, 28, 22, 22, 0.5f, 0f);
            var stumps = new[] { Cut(214, 131, 34, 26, 0.5f, 0f), Cut(164, 132, 26, 20, 0.5f, 0f), Cut(194, 160, 26, 26, 0.5f, 0f), Cut(280, 131, 38, 27, 0.5f, 0f) };
            Scatter(Scaled(6), 2.6f, p => AddProp(branch, p, 1f, true));
            Scatter(Scaled(7), 3.2f, p => AddProp(stumps[_rng.Next(stumps.Length)], p, 1f, true));
            BoneField(Scaled(22), 4, 1.15f, new Color(0.95f, 0.93f, 0.88f));
        }

        // 지옥 — 불타는 대지: 용암과 갈라진 틈, 타 버린 그루터기, 뼈 무더기가 빽빽하다
        private void BuildHell()
        {
            var stumps = new[] { Cut(214, 131, 34, 26, 0.5f, 0f), Cut(194, 160, 26, 26, 0.5f, 0f) };
            var cracks = new[] { MakeCrack(0), MakeCrack(1), MakeCrack(2) };
            var lavas = new[] { MakeLava(0), MakeLava(1) };

            Scatter(Scaled(26), 1.6f, p => AddGround(cracks[_rng.Next(cracks.Length)], p, -191, new Color(1f, 0.55f, 0.2f, 0.95f), 0f));
            Scatter(Scaled(14), 3.2f, p =>
            {
                AddGlow(p, new Color(1f, 0.45f, 0.1f, 0.40f), 3.6f, -190);
                AddGround(lavas[_rng.Next(lavas.Length)], p, -189, Color.white, 0.5f);
            });
            Scatter(Scaled(4), 3.4f, p => AddProp(stumps[_rng.Next(stumps.Length)], p, 1f, true, new Color(0.5f, 0.32f, 0.3f)));
            BoneField(Scaled(32), 4, 1.25f, new Color(1f, 0.86f, 0.80f));
            for (int i = 0; i < 70; i++) AddMote(new Color(1f, 0.55f, 0.18f), 0.08f, 0.17f, 0.9f, 1.5f, false, true);
        }

        /// <summary>해골·뼈를 무더기로 흩뿌린다. 무더기마다 가운데에서 가까운 자리에 3~N개를 모아, 넓은 땅에 드문드문한 점이 아니라 "전투 흔적"처럼 보이게 한다.</summary>
        private void BoneField(int piles, int perPile, float scale, Color tint)
        {
            var sprites = new List<Sprite>();
            if (_bones != null) sprites.AddRange(_bones);
            for (int i = 0; i < 4; i++) { sprites.Add(MakeSkull(i)); sprites.Add(MakeBoneBits(i)); }
            if (sprites.Count == 0) return;
            Scatter(piles, 3.0f, center =>
            {
                int n = 2 + _rng.Next(perPile);
                for (int k = 0; k < n; k++)
                {
                    var p = center + new Vector2((float)(_rng.NextDouble() * 2 - 1) * 1.1f, (float)(_rng.NextDouble() * 2 - 1) * 0.8f);
                    if (!Allowed(p, 0f)) continue;
                    var sr = AddProp(sprites[_rng.Next(sprites.Count)], p, scale * Mathf.Lerp(0.85f, 1.3f, (float)_rng.NextDouble()), true, tint);
                    sr.transform.rotation = Quaternion.Euler(0f, 0f, (float)(_rng.NextDouble() * 2 - 1) * 28f);
                }
            });
        }

        private int Scaled(int count) => Mathf.Max(1, Mathf.RoundToInt(count * _density));

        // ───────────── 배치

        private bool Allowed(Vector2 p, float spacing)
        {
            // 배치 영역(그리드) 주변
            if (p.x > -2f && p.x < _gridMaxX + 1f && p.y > -1.2f && p.y < _gridTopY + 0.8f) return false;
            // 용사가 천막에서 영역으로 내려오는 길
            if (Mathf.Abs(p.x - _cx) < 3.8f && p.y > _gridTopY - 1f && p.y < 8.8f) return false;
            // 천막 자리
            if (Mathf.Abs(p.x - _cx) < 11.8f && p.y > 8.2f && p.y < 14f) return false;
            foreach (var q in _placed) if ((q - p).sqrMagnitude < spacing * spacing) return false;
            return true;
        }

        private void Scatter(int count, float spacing, Action<Vector2> place)
        {
            int placedCount = 0;
            for (int attempt = 0; attempt < count * 25 && placedCount < count; attempt++)
            {
                var p = new Vector2(_cx + (float)(_rng.NextDouble() * 2 - 1) * 25f, -0.4f + (float)_rng.NextDouble() * 25f);
                if (!Allowed(p, spacing)) continue;
                _placed.Add(p);
                place(p);
                placedCount++;
            }
        }

        private Vector3 World(Vector2 cellPos) => new Vector3(_origin.x + cellPos.x * _cell, _origin.y + cellPos.y * _cell, 0f);

        private static int OrderFor(float y) => -160 - Mathf.Clamp(Mathf.RoundToInt(y), 0, 26);

        private SpriteRenderer AddProp(Sprite sprite, Vector2 cellPos, float scale, bool flipRandom, Color? tint = null)
        {
            var go = new GameObject(sprite != null ? sprite.name : "Prop");
            go.transform.SetParent(_root, false);
            go.transform.position = World(cellPos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = OrderFor(cellPos.y);
            sr.color = tint ?? Color.white;
            sr.flipX = flipRandom && _rng.Next(2) == 0;
            go.transform.localScale = Vector3.one * scale;
            return sr;
        }

        private SpriteRenderer AddGround(Sprite sprite, Vector2 cellPos, int order, Color color, float spin)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(_root, false);
            go.transform.position = World(cellPos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingOrder = order; sr.color = color;
            go.transform.rotation = Quaternion.Euler(0f, 0f, (float)_rng.NextDouble() * 360f * (spin > 0f ? 1f : 0.12f));
            if (spin > 0f) { sr.flipX = _rng.Next(2) == 0; }
            return sr;
        }

        private SpriteRenderer AddGlow(Vector2 cellPos, Color color, float diameter, int order)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(_root, false);
            go.transform.position = World(cellPos);
            go.transform.localScale = Vector3.one * diameter;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _soft; sr.color = color; sr.sortingOrder = order;
            _flickers.Add(new Flicker { glow = sr, flame = null, baseAlpha = color.a, baseScale = diameter, phase = (float)_rng.NextDouble() * 6.28f });
            return sr;
        }

        // ───────────── 떠다니는 알갱이(꽃가루·재·불씨)

        private void AddMote(Color color, float minSize, float maxSize, float minSpeed, float maxSpeed, bool drift, bool rise = false)
        {
            var go = new GameObject("Mote");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _soft; sr.color = color; sr.sortingOrder = 900;
            var a = new Ambient
            {
                tf = go.transform, sr = sr,
                home = new Vector2(_cx + (float)(_rng.NextDouble() * 2 - 1) * 15f, -0.5f + (float)_rng.NextDouble() * 18f),
                speed = Mathf.Lerp(minSpeed, maxSpeed, (float)_rng.NextDouble()) * (rise ? 1f : (drift ? 0.3f : 0.5f)),
                sway = 0.4f + (float)_rng.NextDouble() * 0.9f, phase = (float)_rng.NextDouble() * 6.28f,
                size = Mathf.Lerp(minSize, maxSize, (float)_rng.NextDouble()),
                span = rise ? new Vector2(0f, 18f) : new Vector2(drift ? 3f : 6f, 0f),
            };
            go.transform.localScale = Vector3.one * a.size * (rise ? 3.2f : 2.4f);
            _ambient.Add(a);
        }

        private void Animate(float dt)
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < _ambient.Count; i++)
            {
                var a = _ambient[i];
                if (a.tf == null) continue;
                Vector2 p;
                if (a.span.y > 0f)
                {
                    // 위로 피어오른다(맨 위에서 아래로 다시 시작)
                    float y = Mathf.Repeat(a.home.y - -0.5f + t * a.speed, a.span.y) + -0.5f;
                    p = new Vector2(a.home.x + Mathf.Sin(t * a.sway + a.phase) * 0.7f, y);
                    float k = Mathf.InverseLerp(-0.5f, -0.5f + a.span.y, y);
                    var c = a.sr.color; c.a = Mathf.Sin(k * Mathf.PI) * (0.65f + 0.35f * Mathf.Sin(t * 7f + a.phase)); a.sr.color = c;
                }
                else
                {
                    p = new Vector2(a.home.x + Mathf.Sin(t * a.speed + a.phase) * a.span.x, a.home.y + Mathf.Sin(t * a.sway * 0.7f + a.phase) * 0.8f);
                    var c = a.sr.color; c.a = 0.35f + 0.35f * Mathf.Sin(t * 1.3f + a.phase); a.sr.color = c;
                }
                a.tf.position = World(p);
            }
            for (int i = 0; i < _flickers.Count; i++)
            {
                var f = _flickers[i];
                if (f.glow == null) continue;
                float n = 0.78f + 0.22f * Mathf.Sin(t * 9f + f.phase) * Mathf.Sin(t * 5.3f + f.phase * 1.7f);
                var c = f.glow.color; c.a = f.baseAlpha * n; f.glow.color = c;
                if (f.flame != null) f.flame.localScale = new Vector3(0.16f, 0.26f * (0.85f + 0.3f * n), 1f);
                else f.glow.transform.localScale = Vector3.one * f.baseScale * (0.96f + 0.06f * n);
            }
        }

        // ───────────── 그림 만들기

        private Sprite Cut(int x, int yFromTop, int w, int h, float pivotX, float pivotY)
        {
            var rect = new Rect(x, _decorSheet.height - yFromTop - h, w, h);
            return Sprite.Create(_decorSheet, rect, new Vector2(pivotX, pivotY), SheetPpu, 0, SpriteMeshType.FullRect);
        }

        private static Sprite FromPixels(int w, int h, Func<int, int, Color32> pixel, float ppu = 32f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = pixel(x, y);
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        }

        private static Sprite MakeSoft()
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * 64 + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(Mathf.Clamp01(1f - d), 2f) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        }

        private static Sprite MakeDot()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private Sprite MakeSkull(int seed)
        {
            // 위에서 본 작은 해골(10x9): 하얀 두개골에 어두운 눈구멍과 이빨
            int w = 12, h = 11;
            var rng = new System.Random(seed * 71 + 9);
            var bone = new Color32(228, 222, 204, 255); var shade = new Color32(176, 168, 150, 255); var hole = new Color32(40, 32, 30, 255);
            return FromPixels(w, h, (x, y) =>
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.58f) / (h * 0.45f);
                bool skull = dx * dx + dy * dy < 1f && y >= 3;
                bool jaw = y >= 1 && y < 4 && Mathf.Abs(dx) < 0.55f;
                if (!skull && !jaw) return new Color32(0, 0, 0, 0);
                if (y >= 6 && y <= 7 && (x == 3 || x == 4 || x == 7 || x == 8)) return hole;          // 눈구멍
                if (y == 4 && (x == 5 || x == 6)) return hole;                                          // 코
                if (jaw && y <= 2 && (x + seed) % 2 == 0) return hole;                                  // 이빨 사이
                return (x + y + seed) % 5 == 0 ? shade : bone;
            });
        }

        private Sprite MakeBoneBits(int seed)
        {
            // 흩어진 뼈 조각 몇 개(20x10): 막대 모양 뼈와 갈비 조각
            int w = 22, h = 12;
            var rng = new System.Random(seed * 131 + 21);
            var grid = new Color32[w, h];
            var bone = new Color32(226, 220, 202, 255); var shade = new Color32(168, 160, 142, 255);
            int bones = 2 + rng.Next(2);
            for (int b = 0; b < bones; b++)
            {
                int x0 = rng.Next(1, 8), y0 = rng.Next(1, h - 2), len = rng.Next(7, 12), slope = rng.Next(-1, 2);
                for (int k = 0; k < len; k++)
                {
                    int xx = x0 + k, yy = Mathf.Clamp(y0 + (slope == 0 ? 0 : k / 4 * slope), 0, h - 1);
                    if (xx >= w) break;
                    grid[xx, yy] = bone;
                    if (yy + 1 < h) grid[xx, yy + 1] = shade;
                }
                // 양쪽 끝 마디
                for (int e = 0; e < 2; e++)
                {
                    int ex = e == 0 ? x0 : Mathf.Min(w - 2, x0 + len - 1);
                    int ey = Mathf.Clamp(y0 + (slope == 0 ? 0 : (e == 0 ? 0 : (len - 1) / 4 * slope)), 0, h - 2);
                    grid[ex, ey] = bone; if (ex + 1 < w) grid[ex + 1, ey] = bone;
                    if (ey + 1 < h) { grid[ex, ey + 1] = bone; }
                    if (ey > 0) grid[ex, ey - 1] = bone;
                }
            }
            return FromPixels(w, h, (x, y) => grid[x, y]);
        }

        private Sprite MakeRock(int seed)
        {
            var rng = new System.Random(seed * 131 + 7);
            int w = 18 + seed * 2, h = 11 + seed;
            return FromPixels(w, h, (x, y) =>
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.42f) / (h * 0.5f);
                float d = dx * dx + dy * dy * 1.1f;
                float edge = 1f + 0.12f * Mathf.Sin(x * 1.7f + seed) + 0.08f * Mathf.Sin(y * 2.3f + seed * 2f);
                if (d > edge) return new Color32(0, 0, 0, 0);
                if (d > edge * 0.78f || y < 2) return new Color32(48, 44, 42, 255);     // 어두운 테두리·밑면
                bool light = (x - w * 0.35f) * (x - w * 0.35f) + (y - h * 0.68f) * (y - h * 0.68f) < (h * 0.32f) * (h * 0.32f);
                return light ? new Color32(150, 146, 138, 255) : (d > 0.4f ? new Color32(104, 100, 94, 255) : new Color32(122, 118, 110, 255));
            });
        }

        private Sprite MakeCrack(int seed)
        {
            var rng = new System.Random(seed * 53 + 5);
            int w = 40, h = 14;
            var glow = new float[w, h];
            int x0 = 1, y0 = h / 2;
            while (x0 < w - 1)
            {
                glow[x0, y0] = 1f;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                { int xx = x0 + dx, yy = y0 + dy; if (xx >= 0 && xx < w && yy >= 0 && yy < h) glow[xx, yy] = Mathf.Max(glow[xx, yy], 0.45f); }
                x0 += 1 + rng.Next(2);
                y0 = Mathf.Clamp(y0 + rng.Next(-1, 2), 2, h - 3);
                if (rng.Next(9) == 0) { int by = y0, bx = x0; for (int k = 0; k < 5 && bx < w - 1; k++) { glow[bx, by] = 1f; bx++; by = Mathf.Clamp(by + (rng.Next(2) == 0 ? -1 : 1), 1, h - 2); } }
            }
            return FromPixels(w, h, (x, y) =>
            {
                float g = glow[x, y];
                if (g >= 1f) return new Color32(255, 190, 70, 255);
                if (g > 0f) return new Color32(150, 36, 14, 210);
                return new Color32(0, 0, 0, 0);
            });
        }

        private Sprite MakeLava(int seed)
        {
            var rng = new System.Random(seed * 211 + 3);
            int w = 56, h = 40;
            float ox = rng.Next(100), oy = rng.Next(100);
            return FromPixels(w, h, (x, y) =>
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float wob = 1f + 0.22f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + ox) + 0.12f * Mathf.Sin(Mathf.Atan2(dy, dx) * 5f + oy);
                float d = r / (0.75f * wob);
                if (d > 1f) return new Color32(0, 0, 0, 0);
                if (d > 0.9f) return new Color32(30, 10, 8, 255);        // 식은 가장자리
                if (d > 0.78f) return new Color32(92, 24, 10, 255);      // 굳은 껍질
                float n = (Mathf.Sin(x * 0.33f + ox) + Mathf.Sin(y * 0.41f + oy) + Mathf.Sin((x + y) * 0.23f + ox * 0.37f)) / 6f + 0.5f; // 바둑판 무늬가 되지 않게 사인을 더해 부드러운 얼룩을 만든다
                if (d > 0.5f) return n > 0.55f ? new Color32(255, 120, 24, 255) : new Color32(230, 80, 16, 255);
                return n > 0.5f ? new Color32(255, 215, 90, 255) : new Color32(255, 160, 40, 255);
            });
        }
    }
}
