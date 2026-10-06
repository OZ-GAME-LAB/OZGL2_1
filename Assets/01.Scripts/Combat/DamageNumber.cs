using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 피격 시 유닛 머리 위로 떠올랐다 사라지는 월드 공간 데미지 숫자. 타격이 잦아서(번 틱·스플래시 등)
/// 매번 새로 만들지 않고 풀링한다. CombatEffects.PlayDamageNumber()가 진입점.
/// 순수 연출이라 전투 로직에는 관여하지 않는다.
/// </summary>
public sealed class DamageNumber : MonoBehaviour
{
    private const int SortingOrder = 2000; // 체력바(1000~1001)보다 위
    private const float PopDuration = 0.12f;
    private const float PopStartScale = 1.4f;
    private const float FadeStartRatio = 0.45f;

    private static readonly Stack<DamageNumber> Pool = new Stack<DamageNumber>();
    private static Transform _root;

    private TextMeshPro _text;
    private Color _color;
    private Vector3 _startPosition;
    private float _riseDistance;
    private float _duration;
    private float _elapsed;

    public static void Show(Vector3 worldPosition, int amount, Color color, float fontSize,
        float riseDistance, float duration, string prefix = "")
    {
        DamageNumber number = Acquire();
        number.Begin(worldPosition, amount, color, fontSize, riseDistance, duration, prefix);
    }

    private static DamageNumber Acquire()
    {
        while (Pool.Count > 0)
        {
            DamageNumber pooled = Pool.Pop();
            if (pooled != null)
            {
                return pooled;
            }
        }

        if (_root == null)
        {
            var rootObject = new GameObject("DamageNumbers");
            DontDestroyOnLoad(rootObject);
            _root = rootObject.transform;
        }

        var go = new GameObject("DamageNumber");
        go.transform.SetParent(_root, false);
        var created = go.AddComponent<DamageNumber>();
        created._text = go.AddComponent<TextMeshPro>();
        created._text.alignment = TextAlignmentOptions.Center;
        created._text.fontStyle = FontStyles.Bold;
        created._text.textWrappingMode = TextWrappingModes.NoWrap;
        created._text.sortingOrder = SortingOrder;
        created._text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        return created;
    }

    private void Begin(Vector3 worldPosition, int amount, Color color, float fontSize,
        float riseDistance, float duration, string prefix)
    {
        // 같은 지점에서 연속으로 맞아도 숫자가 한 줄로 겹쳐 안 읽히지 않게 좌우로 살짝 흩뿌린다.
        _startPosition = worldPosition + new Vector3(Random.Range(-0.25f, 0.25f), 0f, 0f);
        _riseDistance = riseDistance;
        _duration = Mathf.Max(0.1f, duration);
        _elapsed = 0f;
        _color = color;

        _text.fontSize = fontSize;
        _text.text = prefix + amount;
        _text.color = color;

        Camera cam = Camera.main;
        transform.rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
        transform.position = _startPosition;
        transform.localScale = Vector3.one * PopStartScale;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        float eased = 1f - (1f - t) * (1f - t); // ease-out: 빠르게 솟았다가 느려짐
        transform.position = _startPosition + Vector3.up * (_riseDistance * eased);

        float pop = Mathf.Clamp01(_elapsed / PopDuration);
        transform.localScale = Vector3.one * Mathf.Lerp(PopStartScale, 1f, pop);

        float alpha = t < FadeStartRatio ? 1f : 1f - (t - FadeStartRatio) / (1f - FadeStartRatio);
        Color c = _color;
        c.a *= alpha;
        _text.color = c;

        if (t >= 1f)
        {
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
