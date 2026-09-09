using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Sandbox
{
    public enum UnitSide { Hero, Monster }

    /// <summary>
    /// 샌드박스 더미 유닛 (2D 탑뷰). 용사(적) / 몬스터(아군) 겸용.
    /// 실제 씬 이전 시 버리고 세진의 유닛이 같은 인터페이스를 구현.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SandboxUnit : MonoBehaviour, IDamageable, IStatusReceiver, IHighlightable, IHealable
    {
        [SerializeField] private float _maxHp = 60f;
        [SerializeField] private UnitSide _side = UnitSide.Hero;

        private float _hp;
        private float _stunUntil;
        private float _slowUntil;
        private float _slowMul = 1f;
        private float _vulnUntil;
        private float _vulnMul = 1f;
        private Vector3 _knockVel;
        private bool _highlight;

        private SpriteRenderer _sprite;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        public UnitSide Side => _side;
        public float CurrentHp => _hp;
        public float MaxHp => _maxHp;
        public bool IsDead => _hp <= 0f;
        public Vector3 Position => transform.position;
        public bool IsStunned => Time.time < _stunUntil;
        public float SlowMultiplier => Time.time < _slowUntil ? _slowMul : 1f;

        public void Init(float maxHp, Color color, UnitSide side)
        {
            _maxHp = maxHp;
            _hp = maxHp;
            _baseColor = color;
            _side = side;
            _sprite = GetComponent<SpriteRenderer>();
            _sprite.color = color;
        }

        private void Awake()
        {
            if (_hp <= 0f) _hp = _maxHp;
            if (_sprite == null) _sprite = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        // ─────────── IDamageable

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            float mult = Time.time < _vulnUntil ? _vulnMul : 1f;
            _hp = Mathf.Max(0f, _hp - amount * mult);
            if (IsDead) OnDied();
        }

        private void OnDied()
        {
            if (_side == UnitSide.Monster)
            {
                // 부활 대상으로 남겨둠 — 비활성화 대신 흐리게
                if (_sprite != null) _sprite.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.2f);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // ─────────── IStatusReceiver

        public void ApplyStun(float seconds) => _stunUntil = Mathf.Max(_stunUntil, Time.time + seconds);

        public void ApplySlow(float multiplier, float seconds)
        {
            _slowMul = multiplier;
            _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);
        }

        public void ApplyKnockback(Vector3 dir, float force)
        {
            if (IsDead) return;
            _knockVel += dir.normalized * force;
        }

        public void ApplyVulnerable(float multiplier, float seconds)
        {
            _vulnMul = multiplier;
            _vulnUntil = Mathf.Max(_vulnUntil, Time.time + seconds);
        }

        // ─────────── IHealable (몬스터)

        public void Heal(float amount)
        {
            if (_side != UnitSide.Monster) return;
            bool wasDead = IsDead;
            _hp = Mathf.Min(_maxHp, _hp + Mathf.Max(0f, amount));
            if (wasDead && !IsDead && _sprite != null) _sprite.color = _baseColor;
        }

        public void HealFraction(float fraction) => Heal(_maxHp * fraction);

        public void ApplyBuff(string stat, float multiplier, float seconds)
        {
            // TODO: 스탯 수정자 붙으면 실제 적용. 지금은 로그 + 살짝 커지는 연출.
            Debug.Log($"[Buff] {name} {stat} ×{multiplier:0.00} ({seconds:0.0}s)");
        }

        public void ForceRevive()
        {
            if (_side != UnitSide.Monster || !IsDead) return;
            _hp = _maxHp;
            if (_sprite != null) _sprite.color = _baseColor;
        }

        // ─────────── IHighlightable

        public void SetHighlight(bool on) => _highlight = on;

        // ─────────── 이동(넉백) + 표시

        private void Update()
        {
            if (_knockVel.sqrMagnitude > 0.0001f && !IsDead)
            {
                transform.position += _knockVel * Time.deltaTime;
                _knockVel = Vector3.Lerp(_knockVel, Vector3.zero, 10f * Time.deltaTime);
            }

            if (_sprite == null) return;

            if (_highlight)
            {
                _sprite.color = new Color(1f, 0.95f, 0.3f);
                transform.localScale = _baseScale * 1.25f;
                return;
            }

            transform.localScale = _baseScale;

            if (IsDead && _side == UnitSide.Monster)
            {
                _sprite.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.2f);
            }
            else if (IsStunned)
            {
                _sprite.color = new Color(0.4f, 0.85f, 1f);
            }
            else if (Time.time < _vulnUntil)
            {
                _sprite.color = new Color(0.9f, 0.4f, 0.9f);
            }
            else if (Time.time < _slowUntil)
            {
                _sprite.color = Color.Lerp(_baseColor, new Color(0.5f, 0.5f, 0.7f), 0.5f);
            }
            else
            {
                _sprite.color = _baseColor;
            }
        }
    }
}
