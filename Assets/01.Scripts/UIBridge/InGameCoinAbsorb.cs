using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 용사가 죽으면 그 자리에서 금화가 튀어 올라 화면 왼쪽 아래 재화 표시로 날아가 들어가는 연출(영혼이 경험치바로 들어가는 것과 짝).
    /// 금화는 빙글 돌며(옆면이 보이게) 날아가고, 도착하면 재화 표시가 톡 커지며 금빛 파동이 퍼진다.
    /// 재화 숫자는 금화가 도착한 만큼씩 올라간다(InGameCurrencyHud 가 날아가는 중인 양을 빼고 보여 준다).
    /// 재화 자체는 기존 시스템이 그대로 쌓고, 이 컴포넌트는 연출만 얹는다. 용사가 많이 죽어도 화면이 가득 차지 않게 동시에 날 수 있는 금화 수를 제한한다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InGameCoinAbsorb : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _riseSeconds = 0.22f;
        [SerializeField, Min(0.2f)] private float _flySeconds = 0.62f;
        [SerializeField, Min(8f)] private float _coinSize = 34f;
        [SerializeField, Range(1, 10)] private int _maxCoinsPerKill = 6;
        [SerializeField, Min(1)] private int _gainPerCoin = 4;
        [SerializeField, Min(10)] private int _maxActive = 80;

        private sealed class Coin
        {
            public RectTransform root;
            public Image image;
            public Vector2 start, rise, control;
            public float delay, t, size, spin;
            public bool flying;
            public int amount;
        }

        private readonly List<Coin> _active = new List<Coin>();
        private readonly Stack<Coin> _pool = new Stack<Coin>();
        private readonly List<(RectTransform rect, Image img, float t)> _flashes = new List<(RectTransform, Image, float)>();
        private Canvas _canvas;
        private Sprite _coin, _ring;

        private void OnEnable() => InGameCurrencyHud.Gained += OnGained;

        private void OnDisable()
        {
            InGameCurrencyHud.Gained -= OnGained;
            // 날아가던 금화가 남아 있으면 숫자가 모자란 채로 멈추므로 전부 도착한 것으로 처리한다
            foreach (var coin in _active) InGameCurrencyHud.CoinArrived(coin.amount);
            _active.Clear();
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ───────────── 처치 → 금화 생성

        private void OnGained(Vector2 start, int gain)
        {
            if (gain <= 0) return;
            EnsureCanvas();
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            int count = Mathf.Clamp(Mathf.CeilToInt(gain / (float)_gainPerCoin), 1, _maxCoinsPerKill);
            int share = gain / count, remainder = gain - share * count;
            for (int i = 0; i < count; i++)
            {
                int amount = share + (i == count - 1 ? remainder : 0);
                if (_active.Count >= _maxActive) { InGameCurrencyHud.CoinArrived(amount); continue; }
                var coin = _pool.Count > 0 ? _pool.Pop() : CreateCoin();
                coin.amount = amount;
                coin.t = 0f; coin.flying = false;
                coin.delay = i * 0.06f;
                coin.size = _coinSize * s;
                coin.spin = Random.Range(0f, Mathf.PI * 2f);
                coin.start = start;
                float spread = count > 1 ? Random.Range(-60f, 60f) : Random.Range(-14f, 14f);
                coin.rise = start + new Vector2(spread, Random.Range(34f, 66f) * s);
                coin.root.gameObject.SetActive(true);
                coin.root.sizeDelta = new Vector2(coin.size, coin.size);
                coin.root.localScale = Vector3.zero;
                coin.root.anchoredPosition = start;
                _active.Add(coin);
            }
        }

        // ───────────── 매 프레임

        private void Update()
        {
            Vector2 target = Target();
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var coin = _active[i];
                if (coin.delay > 0f) { coin.delay -= Time.unscaledDeltaTime; continue; }
                coin.t += Time.unscaledDeltaTime;
                coin.spin += Time.unscaledDeltaTime * 14f;
                float flip = Mathf.Max(0.18f, Mathf.Abs(Mathf.Cos(coin.spin)));  // 빙글 돌며 옆면이 보인다
                float shade = 0.72f + 0.28f * flip;
                coin.image.color = new Color(shade, shade, shade * 0.92f, 1f);

                if (!coin.flying)
                {
                    float k = Mathf.Clamp01(coin.t / _riseSeconds);
                    float ease = 1f - (1f - k) * (1f - k);
                    coin.root.anchoredPosition = Vector2.Lerp(coin.start, coin.rise, ease);
                    float pop = k < 0.6f ? Mathf.Lerp(0f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
                    coin.root.localScale = new Vector3(pop * flip, pop, 1f);
                    if (k >= 1f)
                    {
                        coin.flying = true; coin.t = 0f;
                        Vector2 mid = (coin.rise + target) * 0.5f;
                        coin.control = mid + new Vector2(Random.Range(-80f, 80f), Random.Range(30f, 110f));
                        coin.start = coin.rise;
                    }
                    continue;
                }

                float f = Mathf.Clamp01(coin.t / _flySeconds);
                float e = f * f * (3f - 2f * f) * 0.5f + f * f * 0.5f; // 처음엔 느리게, 끝으로 갈수록 빨라진다
                Vector2 a = Vector2.Lerp(coin.start, coin.control, e), b = Vector2.Lerp(coin.control, target, e);
                coin.root.anchoredPosition = Vector2.Lerp(a, b, e);
                float shrink = Mathf.Lerp(1f, 0.7f, f);
                coin.root.localScale = new Vector3(shrink * flip, shrink, 1f);
                if (f >= 1f)
                {
                    Arrive(coin, target);
                    _active.RemoveAt(i);
                }
            }

            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var (rect, img, t) = _flashes[i];
                t += Time.unscaledDeltaTime;
                float k = t / 0.35f;
                if (k >= 1f) { Destroy(rect.gameObject); _flashes.RemoveAt(i); continue; }
                rect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.7f, k);
                img.color = new Color(1f, 0.85f, 0.4f, (1f - k) * 0.85f);
                _flashes[i] = (rect, img, t);
            }
        }

        private void Arrive(Coin coin, Vector2 target)
        {
            coin.root.gameObject.SetActive(false);
            _pool.Push(coin);
            InGameCurrencyHud.CoinArrived(coin.amount);
            Sfx.Play(SfxId.CoinGain); // 짧은 시간에 여러 개가 도착해도 소리는 슬롯의 최소 간격으로 솎아 낸다
            // 금빛 파동
            var go = new GameObject("CoinFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            float size = coin.size * 2.4f;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = target;
            var img = go.GetComponent<Image>();
            img.sprite = _ring; img.raycastTarget = false;
            img.color = new Color(1f, 0.85f, 0.4f, 0.85f);
            _flashes.Add((rect, img, 0f));
        }

        private static Vector2 Target()
        {
            if (InGameCurrencyHud.TryGetCurrencyScreenPos(out var pos)) return pos;
            return new Vector2(Screen.width * 0.07f, Screen.height * 0.12f);
        }

        // ───────────── 그림

        private Coin CreateCoin()
        {
            var coin = new Coin();
            var go = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            coin.root = (RectTransform)go.transform;
            coin.root.anchorMin = coin.root.anchorMax = Vector2.zero;
            coin.root.pivot = new Vector2(0.5f, 0.5f);
            coin.image = go.GetComponent<Image>();
            coin.image.sprite = _coin; coin.image.raycastTarget = false;
            return coin;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            _coin = MakeCoin(64);
            _ring = MakeRing(64);
            var go = new GameObject("CoinAbsorb", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 27; // 재화 숫자(25)보다 위, 팝업(100)보다 아래
        }

        /// <summary>금화: 금빛 원판 + 짙은 가장자리 + 안쪽 둥근 홈 + 왼쪽 위 반사광.</summary>
        private static Sprite MakeCoin(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float alpha = Mathf.Clamp01((1f - d) * r * 0.6f);
                    Color c;
                    if (d > 0.86f) c = new Color(0.62f, 0.38f, 0.06f);                       // 가장자리
                    else if (d > 0.74f) c = new Color(1f, 0.9f, 0.45f);                      // 안쪽 밝은 테
                    else
                    {
                        float shade = Mathf.Lerp(1f, 0.8f, d);
                        c = new Color(1f * shade, 0.78f * shade, 0.22f * shade);           // 면
                        float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-r * 0.3f, r * 0.32f)) / (r * 0.45f));
                        c = Color.Lerp(c, new Color(1f, 0.98f, 0.8f), hl * 0.7f);
                    }
                    px[y * n + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(alpha * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRing(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - n * 0.5f) * (x + 0.5f - n * 0.5f) + (y + 0.5f - n * 0.5f) * (y + 0.5f - n * 0.5f)) / r;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.14f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
