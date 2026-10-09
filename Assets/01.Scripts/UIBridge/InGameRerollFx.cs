using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 보상 카드 리롤 연출. 지금 보이는 카드들이 한 장씩 차례로 옆으로 뒤집혀 사라지고(0.12초), 그 사이에 실제로 카드가 바뀌고,
    /// 새 카드가 한 장씩 뒤집혀 나타난다 — 나타날 때 살짝 튀어 오르고 금빛 빛무리와 반짝이가 퍼진다.
    /// 팀 파일(손패 UIBattleCardHandView/Slot)은 건드리지 않는다. 슬롯의 배치·호버는 슬롯 뿌리(visualRoot)가 맡고,
    /// 이 연출은 그 아래 카드 그림(visualRoot 의 자식)의 가로 크기·기울기만 잠깐 바꿨다가 원래 값으로 되돌린다.
    /// 시간은 배속·일시정지와 상관없이 실제 시간으로 흐른다.
    /// </summary>
    public sealed class InGameRerollFx : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FVisualRoot = typeof(UIBattleCardHandSlot).GetField("_visualRoot", Priv);

        [SerializeField, Min(0.04f)] private float _flipOutSeconds = 0.12f;
        [SerializeField, Min(0.05f)] private float _flipInSeconds = 0.24f;
        [SerializeField, Min(0f)] private float _outStagger = 0.05f;
        [SerializeField, Min(0f)] private float _inStagger = 0.07f;
        [SerializeField] private Color _glowColor = new Color(1f, 0.82f, 0.45f, 0.6f);
        [SerializeField] private Color _sparkColor = new Color(1f, 0.93f, 0.7f, 1f);

        private sealed class Card
        {
            public RectTransform rect;
            public Vector3 baseScale;
            public Quaternion baseRot;
            public float tilt;
            public bool burst;
        }

        private struct Particle
        {
            public RectTransform rect;
            public Image image;
            public Vector2 pos, vel;
            public float life, age, size;
        }

        private bool _running;
        private readonly Dictionary<RectTransform, (Vector3 scale, Quaternion rot)> _bases = new Dictionary<RectTransform, (Vector3, Quaternion)>();
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<(RectTransform rect, Image image, float age)> _glows = new List<(RectTransform, Image, float)>();
        private Canvas _overlay;
        private Sprite _soft;

        public bool Busy => _running;

        /// <summary>연출을 시작한다. swap 은 카드가 가려진 순간에 불리며, 실제로 카드를 바꾸고 성공 여부를 돌려준다.</summary>
        public void Play(Func<bool> swap)
        {
            if (_running || swap == null) return;
            StartCoroutine(Run(swap));
        }

        private IEnumerator Run(Func<bool> swap)
        {
            _running = true;
            _bases.Clear();
            var before = Gather();

            // ① 지금 카드가 한 장씩 옆으로 뒤집혀 사라진다
            float total = _flipOutSeconds + _outStagger * Mathf.Max(0, before.Count - 1);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < before.Count; i++)
                {
                    float k = Mathf.Clamp01((t - i * _outStagger) / _flipOutSeconds);
                    Apply(before[i], 1f - k * k, 1f + 0.05f * k, before[i].tilt * k);
                }
                yield return null;
            }
            for (int i = 0; i < before.Count; i++) Apply(before[i], 0f, 1f, 0f);

            // ② 카드가 완전히 가려진 순간에 실제로 바꾼다(같은 프레임 안이라 새 카드가 풀 크기로 한 프레임 보이는 일이 없다)
            bool ok;
            try { ok = swap(); }
            catch (Exception exception) { Debug.LogException(exception, this); ok = false; }

            if (!ok)
            {
                foreach (var card in before) Restore(card);
                _running = false;
                yield break;
            }

            var after = Gather();
            foreach (var card in after) Apply(card, 0f, 1f, 0f);

            // ③ 새 카드가 한 장씩 뒤집혀 나타난다(살짝 튀어 오르며 빛무리·반짝이)
            total = _flipInSeconds + _inStagger * Mathf.Max(0, after.Count - 1);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < after.Count; i++)
                {
                    if (after[i].rect == null) continue;
                    float k = Mathf.Clamp01((t - i * _inStagger) / _flipInSeconds);
                    if (k <= 0f) { Apply(after[i], 0f, 1f, 0f); continue; }
                    if (!after[i].burst) { after[i].burst = true; Burst(after[i].rect); }
                    float sx = EaseOutBack(k);
                    float pop = 1f + 0.10f * Mathf.Sin(k * Mathf.PI);          // 뒤집히는 동안 위아래로 살짝 커졌다 돌아온다
                    float tilt = after[i].tilt * (1f - k) * Mathf.Cos(k * Mathf.PI * 1.5f);
                    Apply(after[i], Mathf.Max(0.001f, sx), pop, tilt);
                }
                yield return null;
            }
            foreach (var card in after) Restore(card);
            _running = false;
        }

        // ───────────── 카드 찾기·적용

        private List<Card> Gather()
        {
            var list = new List<Card>();
            var view = FindFirstObjectByType<UIBattleCardHandView>(FindObjectsInactive.Exclude);
            if (view == null || FVisualRoot == null) return list;
            int n = 0;
            foreach (var slot in view.Slots)
            {
                if (slot == null || !slot.isActiveAndEnabled) continue;
                var visualRoot = FVisualRoot.GetValue(slot) as RectTransform;
                if (visualRoot == null) continue;
                foreach (Transform child in visualRoot)
                {
                    var rect = child as RectTransform;
                    if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                    if (!_bases.TryGetValue(rect, out var saved)) { saved = (rect.localScale, rect.localRotation); _bases[rect] = saved; }
                    list.Add(new Card { rect = rect, baseScale = saved.scale, baseRot = saved.rot, tilt = (n++ % 2 == 0 ? -1f : 1f) * 7f });
                }
            }
            return list;
        }

        private static void Apply(Card card, float scaleX, float pop, float tilt)
        {
            if (card.rect == null) return;
            card.rect.localScale = new Vector3(card.baseScale.x * scaleX, card.baseScale.y * pop, card.baseScale.z);
            card.rect.localRotation = card.baseRot * Quaternion.Euler(0f, 0f, tilt);
        }

        private static void Restore(Card card)
        {
            if (card.rect == null) return;
            card.rect.localScale = card.baseScale;
            card.rect.localRotation = card.baseRot;
        }

        private static float EaseOutBack(float k)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            float p = k - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        // ───────────── 빛무리·반짝이

        private void EnsureOverlay()
        {
            if (_overlay != null) return;
            var go = new GameObject("RerollFxCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _overlay = go.AddComponent<Canvas>();
            _overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlay.sortingOrder = 20; // 손패(10~11)·드롭 존(15) 위, 배속 막대(28) 아래
            if (_soft == null) _soft = MakeSoftSprite();
        }

        private static Sprite MakeSoftSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f)) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void Burst(RectTransform card)
        {
            EnsureOverlay();
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            var canvas = card.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            Vector2 center = (a + b) * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));

            // 카드 뒤쪽에서 번지는 금빛 빛무리
            var glow = NewImage("Glow", _glowColor);
            glow.rectTransform.sizeDelta = size * 1.5f;
            glow.rectTransform.anchoredPosition = center;
            _glows.Add((glow.rectTransform, glow, 0f));

            // 가장자리에서 퍼져 나가는 반짝이
            int count = 9;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var img = NewImage("Spark", _sparkColor);
                float sz = UnityEngine.Random.Range(12f, 26f) * s;
                img.rectTransform.sizeDelta = new Vector2(sz, sz);
                Vector2 start = center + Vector2.Scale(dir, size * 0.42f);
                _particles.Add(new Particle
                {
                    rect = img.rectTransform, image = img, pos = start,
                    vel = dir * UnityEngine.Random.Range(140f, 360f) * s + Vector2.up * 60f * s,
                    life = UnityEngine.Random.Range(0.45f, 0.8f), age = 0f, size = sz,
                });
            }
        }

        private Image NewImage(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_overlay.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = _soft;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _glows.Count - 1; i >= 0; i--)
            {
                var (rect, image, age) = _glows[i];
                age += dt;
                float k = age / 0.4f;
                if (rect == null || k >= 1f) { if (rect != null) Destroy(rect.gameObject); _glows.RemoveAt(i); continue; }
                var c = _glowColor; c.a *= (1f - k) * (1f - k);
                image.color = c;
                rect.localScale = Vector3.one * (1f + 0.25f * k);
                _glows[i] = (rect, image, age);
            }
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.age += dt;
                if (p.rect == null || p.age >= p.life) { if (p.rect != null) Destroy(p.rect.gameObject); _particles.RemoveAt(i); continue; }
                p.vel *= Mathf.Pow(0.06f, dt);          // 퍼지다가 서서히 멈춘다
                p.pos += p.vel * dt;
                float k = p.age / p.life;
                var c = _sparkColor; c.a = 1f - k * k;
                p.image.color = c;
                p.rect.anchoredPosition = p.pos;
                p.rect.localScale = Vector3.one * (1f - 0.6f * k);
                _particles[i] = p;
            }
        }
    }
}
