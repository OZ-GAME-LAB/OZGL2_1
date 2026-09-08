using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 샌드박스 전용 더미 유닛 (2D 탑뷰). 스프라이트 사각형 + 체력 + 정지 상태.
    /// 실제 씬 이전 시 통째로 버리고 세진의 유닛이 IDamageable / IStatusReceiver 를 대신 구현.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SandboxUnit : MonoBehaviour, IDamageable, IStatusReceiver, IHighlightable
    {
        [SerializeField] private float _maxHp = 60f;

        private float _hp;
        private float _stunUntilTime;
        private bool _highlight;
        private SpriteRenderer _sprite;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        public float CurrentHp => _hp;
        public float MaxHp => _maxHp;
        public bool IsDead => _hp <= 0f;
        public Vector3 Position => transform.position;
        public bool IsStunned => Time.time < _stunUntilTime;

        public void Init(float maxHp, Color color)
        {
            _maxHp = maxHp;
            _hp = maxHp;
            _baseColor = color;

            _sprite = GetComponent<SpriteRenderer>();
            _sprite.color = color;
        }

        private void Awake()
        {
            if (_hp <= 0f)
            {
                _hp = _maxHp;
            }

            if (_sprite == null)
            {
                _sprite = GetComponent<SpriteRenderer>();
            }

            _baseScale = transform.localScale;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead)
            {
                return;
            }

            _hp = Mathf.Max(0f, _hp - amount);
            if (IsDead)
            {
                gameObject.SetActive(false);
            }
        }

        public void ApplyStun(float seconds)
        {
            _stunUntilTime = Mathf.Max(_stunUntilTime, Time.time + seconds);
        }

        public void ApplySlow(float multiplier, float seconds)
        {
            // 샌드박스 유닛은 이동이 없어 시각 표시만. 실제 유닛에서 이동·공격 속도에 적용.
        }

        /// <summary>조준 중 "이 스킬에 맞는 대상" 프리뷰 표시.</summary>
        public void SetHighlight(bool on)
        {
            _highlight = on;
        }

        private void Update()
        {
            if (_sprite == null)
            {
                return;
            }

            if (_highlight)
            {
                _sprite.color = new Color(1f, 0.95f, 0.3f);
                transform.localScale = _baseScale * 1.25f;
            }
            else
            {
                _sprite.color = IsStunned ? new Color(0.4f, 0.85f, 1f) : _baseColor;
                transform.localScale = _baseScale;
            }
        }

        // 스프라이트 위 임시 체력바 (샌드박스 전용, IMGUI).
        private void OnGUI()
        {
            if (IsDead || Camera.main == null)
            {
                return;
            }

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.6f);
            if (screen.z < 0f)
            {
                return;
            }

            var back = new Rect(screen.x - 22f, Screen.height - screen.y, 44f, 5f);
            GUI.color = Color.black;
            GUI.DrawTexture(back, Texture2D.whiteTexture);

            var fill = back;
            fill.width *= _hp / _maxHp;
            GUI.color = IsStunned ? Color.cyan : Color.red;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }
}
