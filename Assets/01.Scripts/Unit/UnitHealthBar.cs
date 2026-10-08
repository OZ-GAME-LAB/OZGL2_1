using System;
using UnityEngine;

/// <summary>
/// 유닛 머리 위에 떠 있는 체력바. 별도 스프라이트 에셋 없이 런타임에 만든 작은 흰 텍스처·세로 그라데이션 텍스처로 여러 겹을 쌓는다. UnitBase.Awake()가 자동으로 붙여준다.
///
/// 잘 보이고 보기 좋게 만든 점:
/// - 진영 색: 마왕군은 밝은 보라, 용사는 선명한 빨강. 막대 테두리도 진영마다 다른 밝은 색(보랏빛 은색 / 따뜻한 금색)이라 어떤 바닥 위에서도 구분된다.
/// - 두꺼운 어두운 외곽선 + 밝은 안쪽 테두리 + 어두운 홈 + 위가 밝은 채움 + 윗면 광택 + 아랫면 그늘 + 25% 눈금으로 작아도 입체감이 난다.
/// - 맞으면 하얗게 번쩍이고, 깎인 만큼은 눈에 띄는 노란 잔상이 잠깐 남았다가 따라 줄어든다. 체력 30% 아래에서는 채움과 테두리가 맥박처럼 깜빡인다.
/// - 옆 유닛과 겹치지 않게: 막대 폭은 칸(1칸) 안에 들어오게 제한하고(무기·방패까지 잰 몸 폭을 그대로 쓰면 2칸이 넘게 커졌다),
///   용사는 떼로 몰려오므로 마왕군보다 막대를 조금 작고 얇게 그린다(둘 다 늘 보인다).
///   겹치더라도 화면 아래쪽(앞쪽) 유닛의 막대가 위에 그려진다. 성급은 유닛 머리 위의 별 표시가 이미 있어서 막대에는 넣지 않는다.
/// - 체력 비율은 태어난 순간의 체력(특성·증강으로 늘어난 값 포함)을 기준으로 해서, 최대 체력이 늘어난 유닛도 막대가 정확하다.
/// </summary>
public class UnitHealthBar : MonoBehaviour
{
    private const float BarHeight = 0.18f;
    private const float MaxBarWidth = 0.7f;     // 한 칸(월드 1) 안에 들어오게 — 옆 칸 유닛의 막대와 겹치지 않는다
    private const float MinBarWidth = 0.5f;
    private const float Frame = 0.022f;          // 안쪽 밝은 테두리 두께
    private const float Outline = 0.04f;         // 바깥 어두운 외곽선 두께
    private const float TrailDelay = 0.4f;       // 맞은 뒤 잔상이 줄기 시작하기까지
    private const float TrailSpeed = 1.4f;       // 초당 줄어드는 비율
    private const float FullHpAlpha = 0.9f;      // 마왕군 막대가 가득 찼을 때의 투명도(거의 그대로)
    private const float HeroHeight = 0.14f;      // 용사 막대는 떼로 몰려오므로 마왕군(0.18)보다 얇게
    private const float HeroWidthScale = 0.85f;  // 용사 막대 폭 배율
    private const float LowHpRatio = 0.3f;
    private const int SortStep = 16;             // 막대 한 개가 쓰는 정렬 칸 수(겹겹이 쌓인 층 수보다 크게)

    private static Sprite _white, _gradient;

    private UnitBase _unit;
    private SpriteRenderer _outline, _frame, _background, _trail, _fill, _shine, _shade, _flash;
    private readonly SpriteRenderer[] _ticks = new SpriteRenderer[3];
    private SpriteRenderer[] _all = new SpriteRenderer[0];
    private int[] _baseOrder = new int[0];
    private int _sortBoost = int.MinValue;
    private float[] _baseAlpha = new float[0];
    private Color _fillColor, _frameColor;
    private bool _visible = true, _hero;
    private float _barWidth = 0.8f;
    private float _height = BarHeight;

    private float _maxSeen = 1f;      // 이 유닛이 이번에 가진 가장 큰 체력(분모)
    private float _ratio = 1f;        // 지금 체력 비율
    private float _trailRatio = 1f;   // 잔상 막대 비율
    private float _flashK;            // 맞았을 때 번쩍임(1 → 0)
    private float _lastHitTime = -10f;
    private bool _wasDead;

    public static UnitHealthBar Attach(UnitBase unit)
    {
        var go = new GameObject("HealthBar");
        go.transform.SetParent(unit.transform, false);
        var bar = go.AddComponent<UnitHealthBar>();
        bar.Init(unit);
        return bar;
    }

    private void Init(UnitBase unit)
    {
        _unit = unit;

        // 자식 SpriteRenderer(SPUM 파츠)들을 합친 바운즈로 바 위치/폭을 잡는다.
        // "Shadow"(발밑 그림자)는 프리팹/팩마다 크기·위치가 제각각이라 몸통 실제 폭과 무관하게
        // 바운즈를 왜곡시킬 수 있어서 제외한다 — 일부 모델링에서 체력바 크기가 안 맞던 원인.
        var renderers = unit.GetComponentsInChildren<SpriteRenderer>();
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var renderer in renderers)
        {
            if (renderer.name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        // 머리 위에 살짝 떠 있게(머리와 겹치지 않도록 예전보다 조금 더 위)
        float heightOffset = hasBounds ? bounds.max.y - unit.transform.position.y + 0.02f : 1.0f;
        _barWidth = hasBounds ? Mathf.Clamp(bounds.size.x * 0.5f, MinBarWidth, MaxBarWidth) : 0.6f;

        _hero = unit.statData != null && unit.statData.side == UnitSide.Hero;
        if (_hero) { _barWidth *= HeroWidthScale; _height = HeroHeight; }
        _fillColor = _hero ? new Color(1f, 0.22f, 0.17f, 1f) : new Color(0.72f, 0.4f, 1f, 1f);       // 용사 빨강 / 마왕군 밝은 보라
        _frameColor = _hero ? new Color(1f, 0.82f, 0.45f, 1f) : new Color(0.86f, 0.78f, 1f, 1f);     // 용사 금색 테두리 / 마왕군 보랏빛 은색 테두리

        EnsureSprites();
        float h = _height;
        _outline = Layer("Outline", _white, 1000, new Color(0.03f, 0.015f, 0.04f, 0.95f));
        _frame = Layer("Frame", _gradient, 1001, _frameColor);
        _background = Layer("Background", _gradient, 1002, new Color(0.17f, 0.08f, 0.12f, 1f));
        _trail = Layer("Trail", _white, 1003, new Color(1f, 0.93f, 0.5f, 0.95f));
        _fill = Layer("Fill", _gradient, 1004, _fillColor);
        _shade = Layer("Shade", _white, 1005, new Color(0f, 0f, 0f, 0.22f));
        _shine = Layer("Shine", _white, 1006, new Color(1f, 1f, 1f, 0.38f));
        for (int i = 0; i < _ticks.Length; i++) _ticks[i] = Layer("Tick" + (i + 1), _white, 1007, new Color(0f, 0f, 0f, 0.55f));
        _flash = Layer("Flash", _white, 1008, new Color(1f, 1f, 1f, 0f));

        var list = new System.Collections.Generic.List<SpriteRenderer> { _outline, _frame, _background, _trail, _fill, _shade, _shine, _flash };
        list.AddRange(_ticks);
        _all = list.ToArray();
        _baseAlpha = new float[_all.Length];
        _baseOrder = new int[_all.Length];
        for (int i = 0; i < _all.Length; i++) { _baseAlpha[i] = _all[i].color.a; _baseOrder[i] = _all[i].sortingOrder; }

        // 외곽선·테두리·배경·눈금·성급 표시는 크기가 고정이라 한 번만 놓는다
        SetRect(_outline, 0f, _barWidth + (Frame + Outline) * 2f, h + (Frame + Outline) * 2f, 0f);
        SetRect(_frame, 0f, _barWidth + Frame * 2f, h + Frame * 2f, 0f);
        SetRect(_background, 0f, _barWidth, h, 0f);
        for (int i = 0; i < _ticks.Length; i++)
            SetRect(_ticks[i], (-0.5f + 0.25f * (i + 1)) * _barWidth, 0.014f, h * 0.9f, 0f);

        transform.localPosition = new Vector3(0f, heightOffset, 0f);
        ResetState();
    }

    private SpriteRenderer Layer(string name, Sprite sprite, int order, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>x 가운데 위치·폭·높이를 놓는다. 그라데이션 그림(세로 8칸)은 높이를 8로 나눠 맞춘다.</summary>
    private static void SetRect(SpriteRenderer sr, float centerX, float width, float height, float centerY)
    {
        var t = sr.transform;
        float sy = sr.sprite == _gradient ? height / 8f : height;
        t.localScale = new Vector3(Mathf.Max(0.0001f, width), Mathf.Max(0.0001f, sy), 1f);
        t.localPosition = new Vector3(centerX, centerY, 0f);
    }

    private void ResetState()
    {
        float max = _unit != null && _unit.statData != null ? Mathf.Max(1f, _unit.statData.maxHealth, _unit.currentHealth) : 1f;
        _maxSeen = max;
        _ratio = _trailRatio = _unit != null ? Mathf.Clamp01(_unit.currentHealth / _maxSeen) : 1f;
        _flashK = 0f;
        _lastHitTime = -10f;
    }

    private void LateUpdate()
    {
        if (_unit == null || _unit.statData == null) return;

        // 체력바 오브젝트 자체를 SetActive(false) 하면 LateUpdate도 같이 멈춰서, 풀에서 재사용되는 용사가
        // 부활해도 체력바가 스스로 다시 켜지지 못했음 — 오브젝트는 계속 켜두고 렌더러만 껐다 켠다.
        bool isDead = _unit.currentState == UnitState.Dead;
        if (_visible == isDead)
        {
            _visible = !isDead;
            foreach (var renderer in _all) renderer.enabled = _visible;
        }
        if (isDead) { _wasDead = true; return; }
        if (_wasDead) { _wasDead = false; ResetState(); } // 풀에서 되살아난 유닛은 막대를 가득 찬 상태로 다시 시작

        // 부모(유닛) 스프라이트가 좌우로 뒤집힐 때(FaceDirection) 체력바까지 같이 뒤집혀서 미러링되지
        // 않도록, 부모 스케일의 역수를 곱해 이 오브젝트 기준으로는 항상 정방향이 되게 상쇄한다.
        float parentScaleX = _unit.transform.localScale.x;
        float compensate = Mathf.Abs(parentScaleX) > 0.0001f ? 1f / parentScaleX : 1f;
        transform.localScale = new Vector3(compensate, 1f, 1f);

        // 체력 비율. 최대 체력이 늘어난 유닛(특성·증강)도 정확하도록 지금까지 본 가장 큰 체력을 분모로 쓴다.
        _maxSeen = Mathf.Max(_maxSeen, _unit.currentHealth, _unit.statData.maxHealth);
        float target = Mathf.Clamp01(_unit.currentHealth / _maxSeen);
        float dt = Time.deltaTime;
        if (target < _ratio - 0.0005f) { _flashK = 1f; _lastHitTime = Time.time; }          // 맞음: 번쩍 + 잔상 시작
        else if (target > _ratio + 0.0005f) { _flashK = Mathf.Max(_flashK, 0.35f); }          // 회복: 약하게 번쩍
        _ratio = target;
        if (_ratio >= _trailRatio) _trailRatio = _ratio;                                      // 회복하면 잔상도 같이 올라온다
        else if (Time.time - _lastHitTime > TrailDelay) _trailRatio = Mathf.MoveTowards(_trailRatio, _ratio, TrailSpeed * dt);
        _flashK = Mathf.MoveTowards(_flashK, 0f, dt * 5f);

        float h = _height;
        float fillW = _barWidth * _ratio;
        float fillX = (fillW - _barWidth) * 0.5f;
        SetRect(_fill, fillX, fillW, h, 0f);
        float trailW = _barWidth * _trailRatio;
        SetRect(_trail, (trailW - _barWidth) * 0.5f, trailW, h, 0f);
        SetRect(_shine, fillX, Mathf.Max(0f, fillW - 0.03f), h * 0.2f, h * 0.27f);
        SetRect(_shade, fillX, fillW, h * 0.16f, -h * 0.42f);
        SetRect(_flash, fillX, fillW, h, 0f);

        // 체력이 낮으면 채움·테두리가 맥박처럼 깜빡인다
        bool low = _ratio <= LowHpRatio;
        float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.time * 11f) : 0f;
        var fillColor = Color.Lerp(_fillColor, Color.white, pulse * 0.5f);
        var frameColor = Color.Lerp(_frameColor, _hero ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.45f, 0.75f), pulse);

        // 가득 찬 막대는 아주 살짝만 낮춘다 — 맞으면 바로 최대로
        bool full = _ratio >= 0.999f && Time.time - _lastHitTime > 1.2f;
        float alpha = full ? FullHpAlpha : 1f;

        // 화면 아래쪽(앞쪽) 유닛의 막대가 위에 그려지게 정렬 층을 유닛 높이에 맞춰 민다
        int boost = Mathf.Clamp(Mathf.RoundToInt(-_unit.transform.position.y * 8f), -500, 500) * SortStep;
        if (boost != _sortBoost)
        {
            _sortBoost = boost;
            for (int i = 0; i < _all.Length; i++) _all[i].sortingOrder = _baseOrder[i] + boost;
        }
        for (int i = 0; i < _all.Length; i++)
        {
            var sr = _all[i];
            if (sr == _flash) { sr.color = new Color(1f, 1f, 1f, _flashK * 0.8f); continue; }
            var c = sr == _fill ? fillColor : sr == _frame ? frameColor : sr.color;
            c.a = _baseAlpha[i] * alpha;
            sr.color = c;
        }
        bool show = _visible;
        foreach (var sr in _all) sr.enabled = show;
        _trail.enabled = show && _trailRatio > _ratio + 0.002f;
        _shine.enabled = show && fillW > 0.04f;
        _shade.enabled = show && fillW > 0.04f;
    }

    private static void EnsureSprites()
    {
        if (_white == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        if (_gradient == null)
        {
            // 세로 8칸: 위가 밝고 아래가 어두운 그라데이션(색은 SpriteRenderer.color 가 입힌다)
            var tex = new Texture2D(1, 8) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 8; y++)
            {
                float v = Mathf.Min(1f, Mathf.Lerp(0.58f, 1.08f, y / 7f));
                tex.SetPixel(0, y, new Color(v, v, v, 1f));
            }
            tex.Apply();
            _gradient = Sprite.Create(tex, new Rect(0, 0, 1, 8), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
