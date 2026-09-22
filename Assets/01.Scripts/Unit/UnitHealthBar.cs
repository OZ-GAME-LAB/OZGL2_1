using System;
using UnityEngine;

/// <summary>
/// 유닛 머리 위에 항상 떠 있는 간단한 체력바. 별도 스프라이트 에셋 없이 1x1 흰 텍스처로
/// 배경(검정)/체력(초록) 두 겹을 런타임에 만들어서 붙인다. UnitBase.Awake()가 자동으로 붙여준다.
/// </summary>
public class UnitHealthBar : MonoBehaviour
{
    private const float BarHeight = 0.12f;
    private static Sprite _sharedWhiteSprite;

    private UnitBase _unit;
    private Transform _fillTransform;
    private SpriteRenderer[] _renderers = new SpriteRenderer[0];
    private bool _visible = true;
    private float _barWidth = 0.8f;

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
        Sprite sprite = GetWhiteSprite();

        // 자식 SpriteRenderer(SPUM 파츠)들을 합친 바운즈로 바 위치/폭을 잡는다.
        // "Shadow"(발밑 그림자)는 프리팹/팩마다 크기·위치가 제각각이라 몸통 실제 폭과 무관하게
        // 바운즈를 왜곡시킬 수 있어서 제외한다 — 일부 모델링에서 체력바 크기가 안 맞던 원인.
        var renderers = unit.GetComponentsInChildren<SpriteRenderer>();
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var renderer in renderers)
        {
            if (renderer.name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        float heightOffset = hasBounds ? bounds.max.y - unit.transform.position.y - 0.15f : 0.9f;
        _barWidth = hasBounds ? Mathf.Max(0.4f, bounds.size.x * 0.6f) : 0.5f;

        GameObject background = new GameObject("Background");
        background.transform.SetParent(transform, false);
        var backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = sprite;
        backgroundRenderer.color = new Color(0f, 0f, 0f, 0.6f);
        backgroundRenderer.sortingOrder = 1000;
        background.transform.localScale = new Vector3(_barWidth, BarHeight, 1f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(transform, false);
        var fillRenderer = fill.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = sprite;
        fillRenderer.color = new Color(0.2f, 0.85f, 0.2f, 0.95f);
        fillRenderer.sortingOrder = 1001;
        _fillTransform = fill.transform;
        _fillTransform.localScale = new Vector3(_barWidth, BarHeight, 1f);
        _renderers = new[] { backgroundRenderer, fillRenderer };

        transform.localPosition = new Vector3(0f, heightOffset, 0f);
    }

    private void LateUpdate()
    {
        if (_unit == null || _unit.statData == null)
        {
            return;
        }

        // 체력바 오브젝트 자체를 SetActive(false) 하면 LateUpdate도 같이 멈춰서, 풀에서 재사용되는 용사가
        // 부활해도 체력바가 스스로 다시 켜지지 못했음 — 오브젝트는 계속 켜두고 렌더러만 껐다 켠다.
        bool isDead = _unit.currentState == UnitState.Dead;
        if (_visible == isDead)
        {
            _visible = !isDead;
            foreach (var renderer in _renderers)
            {
                renderer.enabled = _visible;
            }
        }
        if (isDead)
        {
            return;
        }

        // 부모(유닛) 스프라이트가 좌우로 뒤집힐 때(FaceDirection) 체력바까지 같이 뒤집혀서 미러링되지
        // 않도록, 부모 스케일의 역수를 곱해 이 오브젝트 기준으로는 항상 정방향이 되게 상쇄한다.
        float parentScaleX = _unit.transform.localScale.x;
        float compensate = Mathf.Abs(parentScaleX) > 0.0001f ? 1f / parentScaleX : 1f;
        transform.localScale = new Vector3(compensate, 1f, 1f);

        float ratio = _unit.statData.maxHealth > 0
            ? Mathf.Clamp01((float)_unit.currentHealth / _unit.statData.maxHealth)
            : 0f;

        float fillWidth = _barWidth * ratio;
        _fillTransform.localScale = new Vector3(fillWidth, BarHeight, 1f);
        _fillTransform.localPosition = new Vector3((fillWidth - _barWidth) * 0.5f, 0f, 0f);
    }

    private static Sprite GetWhiteSprite()
    {
        if (_sharedWhiteSprite != null)
        {
            return _sharedWhiteSprite;
        }

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        _sharedWhiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _sharedWhiteSprite;
    }
}
