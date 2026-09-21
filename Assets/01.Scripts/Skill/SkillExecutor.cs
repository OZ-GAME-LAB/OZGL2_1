using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>
    /// SkillManager.CastRequested 를 받아 시간 있는 스킬 효과를 실행한다 — 데미지·CC·버프 + 연출.
    /// SkillManager + 적/아군 프로바이더만 참조. VFX 프리팹이 있으면 재생, 없으면 코드 연출 폴백.
    /// 실제 씬에서도 Bind 한 번이면 그대로 동작.
    /// </summary>
    public class SkillExecutor : MonoBehaviour
    {
        [SerializeField] private float _projectileTravelTime = 0.25f;
        [SerializeField] private float _uiVfxLifetime = 0.85f;
        [SerializeField] private float _fallbackLifetime = 1.5f;
        [SerializeField] private float _skyHeight = 8f;
        [Tooltip("Pixel Art VFX 프리팹의 기준 월드 크기(칸). 캔버스 스케일이 64px=1칸인데 프리팹 루트가 64x64에 localScale 2라서 실제로는 2칸 — 1로 두면 사거리 표시보다 VFX가 2배 크게 나옴")]
        [SerializeField] private float _vfxBaseSize = 2f;
        [Tooltip("맵 전체(radius 큰) 스킬의 VFX 스케일 상한 — 이 칸수로 캡")]
        [SerializeField] private float _vfxMaxRadius = 9f;
        [Tooltip("피해 판정에 더하는 여유. 0이면 조준 사거리 표시(반경)와 정확히 일치 — 예전 0.35는 표시보다 넓게 맞아서 0으로 맞춤")]
        [SerializeField] private float _hitMargin = 0f;
        [SerializeField] private bool _playSfx = false;

        private SkillManager _manager;
        private ITargetProvider _enemies;
        private IAllyProvider _allies;
        private Transform _caster;
        private SkillModifiers _mods = SkillModifiers.None;

        private readonly List<IDamageable> _enemyBuf = new List<IDamageable>();
        private readonly List<IHealable> _allyBuf = new List<IHealable>();
        private Sprite _disc;
        private Sprite _ring;

        public void Bind(SkillManager manager, ITargetProvider enemies, IAllyProvider allies, Vector3 casterPos, SkillModifiers mods = null)
        {
            if (_manager != null) _manager.CastRequested -= OnCast;
            _manager = manager;
            _enemies = enemies;
            _allies = allies;
            _mods = mods ?? SkillModifiers.None;
            _manager.CastRequested += OnCast;

            var anchor = new GameObject("CasterAnchor");
            anchor.transform.SetParent(transform);
            anchor.transform.position = casterPos;
            _caster = anchor.transform;
        }

        /// <summary>
        /// 시전 위치(마왕/왕좌)를 갱신한다. Bind() 시점엔 아직 라운드가 시작 안 돼서 실제 왕 좌표
        /// (UnitRegistry.KingWorldPosition)를 모를 수 있어 (0,0,0) 같은 임시값으로 고정돼버리는 문제가
        /// 있었다 — 호출자가 매 프레임(또는 왕 위치가 확정된 시점마다) 이걸 불러서 앵커를 실제 위치로
        /// 계속 맞춰줘야 한다.
        /// </summary>
        public void SetCasterPosition(Vector3 pos)
        {
            if (_caster != null) _caster.position = pos;
        }

        private void OnDestroy()
        {
            if (_manager != null) _manager.CastRequested -= OnCast;
            ReleaseSprite(_disc);
            ReleaseSprite(_ring);
        }

        /// <summary>대상이 풀로 반환되기 전에 지연 피해, 장판, 시각 효과를 즉시 중단한다.</summary>
        public void CancelActiveEffects()
        {
            StopAllCoroutines();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == _caster) continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            _enemyBuf.Clear();
            _allyBuf.Clear();
            // StopAllCoroutines로 도트 코루틴이 같이 죽는데 기록만 남으면, 다음에 같은 대상에게 도트를 걸 때
            // "이미 타는 중"으로 착각해 영영 안 걸린다 — 라운드 정리 때 기록도 같이 비운다.
            _dotEnd.Clear();
        }

        private static void ReleaseSprite(Sprite sprite)
        {
            if (sprite == null) return;
            Destroy(sprite.texture);
            Destroy(sprite);
        }

        private Vector3 CasterPos => _caster != null ? _caster.position : Vector3.zero;

        /// <summary>
        /// 증강 "연쇄 폭발" 전용 — 스킬이 아닌 외부 트리거(용사 사망 등)로 즉시 폭발 피해를 준다.
        /// 코드 연출 폴백만 사용(전용 VFX 없음).
        /// </summary>
        public void Detonate(Vector3 point, float power, float radius)
        {
            if (_enemies == null || power <= 0f || (_manager != null && !_manager.IsCastingEnabled)) return;
            _enemyBuf.Clear();
            _enemies.QueryInRadius(point, radius + _hitMargin, _enemyBuf);
            foreach (var e in _enemyBuf) e.TakeDamage(power);
            StartCoroutine(ExpandFade(point, radius, new Color(1f, 0.35f, 0.1f), 0.3f, false));
        }

        // ─────────────────────────────────────────── 라우팅

        private void OnCast(SkillCastRequest req)
        {
            if (_manager == null || !_manager.IsCastingEnabled) return;
            SkillData d = req.Skill.Data;
            float power = req.Power;                          // SkillManager 가 치명타까지 반영해서 넘김
            float radius = req.Skill.EffectiveRadius;         // 특성 "광역 지배"
            float buffDur = req.Skill.EffectiveBuffDuration;  // 특성 "군단의 함성"
            int reviveCount = req.Skill.EffectiveReviveCount;
            Vector3 p = req.CastPoint;

            if (d.flourishVfx != null && d.flourishCount > 0)
            {
                StartCoroutine(Flourish(d, radius, p));
            }

            switch (d.effectType)
            {
                case SkillEffectType.AreaDamage:
                    if (d.barrageCount > 1) StartCoroutine(Barrage(d, power, radius, p));
                    else if (d.fallFromSky || d.castMode == SkillCastMode.Instant && d.castDelay > 0f) StartCoroutine(SkyStrike(d, power, radius, p));
                    else if (d.castMode == SkillCastMode.Targeted) StartCoroutine(ProjectileThenImpact(d, power, radius, p));
                    else StartCoroutine(DelayedImpact(d, power, radius, p));
                    break;

                case SkillEffectType.ChainDamage: StartCoroutine(Chain(d, power, radius, p)); break;
                case SkillEffectType.LineDamage: StartCoroutine(LineShot(d, power, radius, p)); break;
                case SkillEffectType.SingleDamage: StartCoroutine(SingleShot(d, power, p)); break;
                case SkillEffectType.Knockback: KnockbackHit(d, power, radius, p); break;
                case SkillEffectType.Stun: StunHit(d, radius, p); break;
                case SkillEffectType.Vacuum: StartCoroutine(Vacuum(d, power, radius, p)); break;
                case SkillEffectType.MovingZone: SpawnZone(d, CasterPos, (p - CasterPos), radius, d.duration); break;
                case SkillEffectType.PersistentZone: SpawnZone(d, p, Vector3.zero, radius, d.zoneTarget == ZoneTarget.Allies ? buffDur : d.duration); break;
                case SkillEffectType.HealAllies: HealAllies(d); break;
                case SkillEffectType.AllyBuff: BuffAllies(d, buffDur); break;
                case SkillEffectType.Revive: Revive(reviveCount); break;
            }
        }

        // ─────────────────────────────────────────── 딜

        private IEnumerator ProjectileThenImpact(SkillData d, float power, float radius, Vector3 point)
        {
            Vector3 from = CasterPos;
            GameObject proj = d.projectileVfx != null
                ? SpawnVfx(d.projectileVfx, from, Face(point - from), d.vfxIsUi)
                : Code(Disc(), new Color(1f, 0.62f, 0.16f), from, 0.4f, 22);
            if (d.projectileVfx != null) CenterOnVisible(proj);
            yield return Move(proj.transform, from, point, _projectileTravelTime);
            KillVfx(proj);
            Impact(d, power, point, radius);
        }

        private IEnumerator SkyStrike(SkillData d, float power, float radius, Vector3 point)
        {
            // 하늘에서 낙하 예고 → 운석 낙하 → 폭발
            var tele = Code(Ring(), new Color(1f, 0.4f, 0.2f, 0.9f), point, 0.3f, 21);
            var sr = tele.GetComponent<SpriteRenderer>();
            float delay = Mathf.Max(d.castDelay, 0.5f);
            float t = 0f;
            while (t < delay)
            {
                t += Time.deltaTime;
                float k = t / delay;
                tele.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, Mathf.Min(radius, 6f) * 2f, k);
                sr.color = new Color(1f, 0.4f, 0.2f, Mathf.Lerp(0.2f, 0.9f, k));
                yield return null;
            }
            Destroy(tele);

            // 떨어지는 운석 본체는 프리팹(비대칭 초승달 모양이라 "어디가 중심인지"가 프레임마다 흔들림) 대신
            // 코드로 그린 둥근 불덩이 + 꼬리를 쓴다 — 착탄 지점이 조준 링 정중앙과 정확히 일치하도록.
            Vector3 from = point + Vector3.up * _skyHeight;
            var meteor = new GameObject("Meteor");
            meteor.transform.SetParent(transform);
            meteor.transform.position = from;
            for (int i = 0; i < 4; i++)
            {
                var part = Code(Disc(), new Color(1f, Mathf.Lerp(0.85f, 0.35f, i / 3f), 0.1f, Mathf.Lerp(1f, 0.35f, i / 3f)),
                    from + Vector3.up * (i * 0.55f), Mathf.Lerp(1.3f, 0.6f, i / 3f), 22 - i);
                part.transform.SetParent(meteor.transform, true);
            }
            // 운석은 조준 지점(사거리 원 정중앙)에 정확히 수직으로 떨어진다 — 어떤 보정값도 섞지 않는다.
            yield return Move(meteor.transform, point + Vector3.up * _skyHeight, point, 0.22f);
            Destroy(meteor);
            Impact(d, power, point, radius);
        }

        private IEnumerator DelayedImpact(SkillData d, float power, float radius, Vector3 point)
        {
            if (d.castDelay > 0f)
            {
                var tele = Code(Ring(), new Color(1f, 0.35f, 0.2f, 0.9f), point, 0.3f, 21);
                var sr = tele.GetComponent<SpriteRenderer>();
                float t = 0f;
                while (t < d.castDelay)
                {
                    t += Time.deltaTime;
                    float k = t / d.castDelay;
                    tele.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2f, k);
                    sr.color = new Color(1f, 0.35f, 0.2f, Mathf.Lerp(0.2f, 0.9f, k));
                    yield return null;
                }
                Destroy(tele);
            }
            Impact(d, power, point, radius);
        }

        private IEnumerator Barrage(SkillData d, float power, float radius, Vector3 center)
        {
            // 개별 낙하(반경 1.7)가 사거리 표시 밖으로 삐져나가지 않게, 낙하 중심을 (radius - 1.7) 안쪽으로 제한.
            const float dropRadius = 1.7f;
            float spread = Mathf.Max(0f, radius - dropRadius);
            bool mapWide = IsMapWide(radius);

            // 맵 전체 낙하는 화면에 랜덤으로만 떨구면 절반 정도만 맞아서, 살아있는 용사 전원을 한 발씩
            // 먼저 겨냥하고(낙하 시점의 실제 위치 기준) 남는 발수만 랜덤으로 뿌린다.
            List<IDamageable> aimed = null;
            int count = d.barrageCount;
            if (mapWide)
            {
                aimed = new List<IDamageable>();
                foreach (var e in _enemies.All) if (!e.IsDead) aimed.Add(e);
                for (int k = aimed.Count - 1; k > 0; k--)
                {
                    int j = Random.Range(0, k + 1);
                    (aimed[k], aimed[j]) = (aimed[j], aimed[k]);
                }
                count = Mathf.Max(count, aimed.Count);
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 p;
                if (mapWide)
                {
                    bool hasTarget = i < aimed.Count && !(aimed[i] is Object gone && gone == null) && !aimed[i].IsDead;
                    p = hasTarget ? aimed[i].Position + (Vector3)(Random.insideUnitCircle * 0.4f) : RandomScreenPoint(center, spread);
                }
                else p = center + (Vector3)(Random.insideUnitCircle * spread);
                Impact(d, power, p, dropRadius); // 개별 낙하는 작게
                yield return new WaitForSeconds(Mathf.Max(0.05f, d.chainInterval));
            }
        }

        private void Impact(SkillData d, float power, Vector3 point, float radiusOverride = -1f)
        {
            float r = radiusOverride > 0f ? radiusOverride : d.radius;
            _enemyBuf.Clear();
            _enemies.QueryInRadius(point, Mathf.Max(r, 0.5f) + _hitMargin, _enemyBuf);
            foreach (var e in _enemyBuf)
            {
                e.TakeDamage(power);
                if (_mods.OnHitSlowAmount > 0f) (e as IStatusReceiver)?.ApplySlow(_mods.OnHitSlowAmount, 2f);
                ApplySkillHitSlow(d, e);
            }

            if (d.castVfx != null)
            {
                // visualOffsetX/Y: 이펙트 모양 때문에 눈으로 봤을 때 살짝 어긋나는 걸 손으로 미세 조정하는 값(판정 위치는 그대로).
                var fx = SpawnVfx(d.castVfx, point + new Vector3(d.visualOffsetX, d.visualOffsetY, 0f), Quaternion.identity, d.vfxIsUi);
                ScaleAreaVfx(fx, r);
                AutoDestroy(fx);
            }
            else
            {
                // 맵 전체 궁극기(flourish로 연출)는 화면 전체를 덮는 거대한 원이 뜨지 않게 폴백을 생략.
                if (!(IsMapWide(r) && HasFlourish(d)))
                    StartCoroutine(ExpandFade(point, r, new Color(1f, 0.5f, 0.15f), 0.35f, false));
            }
        }

        private IEnumerator Chain(SkillData d, float power, float radius, Vector3 from)
        {
            // 연쇄는 조준 사거리(radius) 안의 대상끼리만 튄다 — 예전엔 맵 전체에서 가장 가까운 적으로
            // 계속 튀어서 사거리 표시보다 훨씬 멀리까지 맞았다. 시전 이펙트도 사거리 크기에 맞춰 스케일.
            if (d.castVfx != null)
            {
                var cast = SpawnVfx(d.castVfx, from, Quaternion.identity, d.vfxIsUi);
                ScaleAreaVfx(cast, radius);
                AutoDestroy(cast);
            }

            var hit = new HashSet<IDamageable>();
            Vector3 cursor = from;
            for (int i = 0; i < d.chainCount; i++)
            {
                IDamageable next = NearestUnhit(cursor, hit, from, radius);
                if (next == null) break;
                next.TakeDamage(power);
                ApplySkillHitSlow(d, next);
                hit.Add(next);
                if (d.perTargetVfx != null)
                {
                    // 연쇄 하나하나가 더 크고 확실하게 보이도록 살짝 키워서 스폰.
                    var fx = SpawnVfx(d.perTargetVfx, next.Position, Quaternion.identity, d.vfxIsUi);
                    if (fx != null) fx.transform.localScale *= 1.6f;
                    AutoDestroy(fx);
                }
                else { Arc(cursor, next.Position, new Color(1f, 0.95f, 0.45f)); StartCoroutine(ExpandFade(next.Position, 0.8f, new Color(1f, 0.95f, 0.4f), 0.22f, false)); }
                cursor = next.Position;
                yield return new WaitForSeconds(d.chainInterval);
            }
        }

        private IEnumerator LineShot(SkillData d, float power, float radius, Vector3 point)
        {
            Vector3 from = CasterPos;
            Vector3 dir = (point - from).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
            Vector3 to = from + dir * d.lineLength;
            float hitR = Mathf.Max(radius, 0.4f);

            GameObject proj = d.castVfx != null
                ? SpawnVfx(d.castVfx, from, Face(dir), d.vfxIsUi)
                : Code(Disc(), new Color(0.6f, 0.85f, 1f), from, 0.45f, 22);
            if (proj != null && d.castVfx != null)
            {
                proj.transform.localScale *= Mathf.Max(d.vfxScale, 0.1f); // 날아가는 이펙트 크기 배율
                CenterOnVisible(proj);
            }

            var hit = new HashSet<IDamageable>();
            // zoneMoveSpeed를 비행 속도로 쓴다(0이면 예전처럼 거의 즉발) — 회오리처럼 눈에 보이게 날아가도록.
            float dur = d.zoneMoveSpeed > 0f ? Mathf.Max(d.lineLength / d.zoneMoveSpeed, 0.05f) : 0.16f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Vector3 cur = Vector3.Lerp(from, to, t / dur);
                if (proj != null) proj.transform.position = cur;
                foreach (var e in _enemies.All)
                {
                    if (e.IsDead || hit.Contains(e)) continue;
                    if ((e.Position - cur).sqrMagnitude <= hitR * hitR)
                    {
                        e.TakeDamage(power);
                        ApplySkillHitSlow(d, e);
                        hit.Add(e);
                        if (d.perTargetVfx != null) AutoDestroy(SpawnVfx(d.perTargetVfx, e.Position, Quaternion.identity, d.vfxIsUi));
                    }
                }
                yield return null;
            }

            KillVfx(proj);
        }

        /// <summary>스킬 자체 피격 부가효과 — 감속(onHitSlow*)과 도트(onHitDot*). 맞을 때마다 호출.</summary>
        private void ApplySkillHitSlow(SkillData d, IDamageable target)
        {
            if (d.onHitSlowMultiplier > 0f) (target as IStatusReceiver)?.ApplySlow(d.onHitSlowMultiplier, d.onHitSlowSeconds);
            if (d.onHitDotDamage > 0f) ApplyDot(target, d.onHitDotDamage, d.onHitDotSeconds, d.onHitDotTick);
        }

        private readonly Dictionary<IDamageable, float> _dotEnd = new Dictionary<IDamageable, float>();

        /// <summary>대상에게 지속 피해. 이미 타고 있으면 새로 겹치지 않고 종료 시각만 연장한다(중첩 폭증 방지).</summary>
        private void ApplyDot(IDamageable target, float damagePerTick, float seconds, float tick)
        {
            if (target == null || seconds <= 0f) return;
            float end = Time.time + seconds;
            if (_dotEnd.TryGetValue(target, out var current))
            {
                _dotEnd[target] = Mathf.Max(current, end);
                return;
            }
            _dotEnd[target] = end;
            StartCoroutine(DotRoutine(target, damagePerTick, Mathf.Max(0.1f, tick)));
        }

        private IEnumerator DotRoutine(IDamageable target, float damagePerTick, float tick)
        {
            while (true)
            {
                yield return new WaitForSeconds(tick);
                // 인터페이스 참조는 유니티 오브젝트가 파괴돼도 null이 아니라 따로 확인.
                bool gone = target == null || (target is Object o && o == null);
                if (gone || target.IsDead || !_dotEnd.TryGetValue(target, out var end) || Time.time > end) break;
                target.TakeDamage(damagePerTick);
            }
            if (target != null) _dotEnd.Remove(target);
        }

        private IEnumerator SingleShot(SkillData d, float power, Vector3 point)
        {
            Vector3 from = CasterPos;
            var proj = d.projectileVfx != null
                ? SpawnVfx(d.projectileVfx, from, Face(point - from), d.vfxIsUi)
                : Code(Disc(), new Color(1f, 0.95f, 0.6f), from, 0.35f, 22);
            IDamageable target = _enemies.Nearest(point);
            Vector3 aim = target != null ? target.Position : point;
            yield return Move(proj.transform, from, aim, _projectileTravelTime);
            KillVfx(proj);
            target?.TakeDamage(power);
            if (d.castVfx != null) AutoDestroy(SpawnVfx(d.castVfx, aim, Quaternion.identity, d.vfxIsUi));
            else StartCoroutine(ExpandFade(aim, 0.7f, new Color(1f, 0.95f, 0.7f), 0.25f, false));
        }

        private void KnockbackHit(SkillData d, float power, float radius, Vector3 point)
        {
            _enemyBuf.Clear();
            _enemies.QueryInRadius(point, radius + _hitMargin, _enemyBuf);
            foreach (var e in _enemyBuf)
            {
                (e as IStatusReceiver)?.ApplyKnockback(e.Position - point, d.force);
                if (power > 0f) e.TakeDamage(power);
            }

            if (d.castVfx != null) { var fx = SpawnVfx(d.castVfx, point, Quaternion.identity, d.vfxIsUi); ScaleAreaVfx(fx, radius); AutoDestroy(fx); }
            else StartCoroutine(ExpandFade(point, radius, new Color(0.7f, 0.9f, 0.7f), 0.3f, true));
        }

        private void StunHit(SkillData d, float radius, Vector3 point)
        {
            _enemyBuf.Clear();
            _enemies.QueryInRadius(point, radius + _hitMargin, _enemyBuf);
            foreach (var e in _enemyBuf) (e as IStatusReceiver)?.ApplyStun(d.duration);
            if (d.castVfx != null) { var fx = SpawnVfx(d.castVfx, point, Quaternion.identity, d.vfxIsUi); ScaleAreaVfx(fx, radius); AutoDestroy(fx); }
            else if (!(IsMapWide(radius) && HasFlourish(d)))
                StartCoroutine(ExpandFade(point, Mathf.Min(radius, 14f), new Color(0.4f, 0.8f, 1f), 0.5f, true));
        }

        private IEnumerator Vacuum(SkillData d, float power, float radius, Vector3 point)
        {
            var fx = d.castVfx != null ? SpawnVfx(d.castVfx, point, Quaternion.identity, d.vfxIsUi) : null;
            ScaleAreaVfx(fx, radius);
            float t = 0f;
            const float pullTime = 0.7f;
            while (t < pullTime)
            {
                t += Time.deltaTime;
                _enemyBuf.Clear();
                _enemies.QueryInRadius(point, radius, _enemyBuf); // 흡입 범위도 사거리 표시 안으로만
                foreach (var e in _enemyBuf)
                {
                    (e as IStatusReceiver)?.ApplyKnockback(point - e.Position, d.force * Time.deltaTime * 4f);
                }
                yield return null;
            }

            _enemyBuf.Clear();
            _enemies.QueryInRadius(point, radius + _hitMargin, _enemyBuf);
            foreach (var e in _enemyBuf) { e.TakeDamage(power); ApplySkillHitSlow(d, e); }

            if (fx != null) AutoDestroy(fx);
            if (d.finishVfx != null) AutoDestroy(SpawnVfx(d.finishVfx, point, Quaternion.identity, d.vfxIsUi));
            else StartCoroutine(ExpandFade(point, radius * 1.4f, new Color(0.6f, 0.3f, 0.85f), 0.35f, false));
        }

        private static bool IsMapWide(float radius) => radius >= 20f;
        private static bool HasFlourish(SkillData d) => d.flourishVfx != null && d.flourishCount > 0;

        /// <summary>맵 전체 스킬용 — 카메라에 보이는 화면 전체에서 랜덤 위치. 시전 지점(가장 가까운 용사 위치)을
        /// 중심으로 한 원으로 뿌리면 한쪽에 몰려서 "맵 전체"로 안 보이던 문제 방지. 카메라를 못 쓰면 원형 폴백.</summary>
        private static Vector3 RandomScreenPoint(Vector3 fallbackCenter, float fallbackRadius)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return fallbackCenter + (Vector3)(Random.insideUnitCircle * fallbackRadius);
            float h = cam.orthographicSize * 0.95f;
            float w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Vector3(c.x + Random.Range(-w, w), c.y + Random.Range(-h, h), 0f);
        }

        private IEnumerator Flourish(SkillData d, float radius, Vector3 center)
        {
            // 유성우·절대영도·심판처럼 radius=99(맵 전체) 스킬은 예전엔 6칸으로 좁게 캡해서 화면 한
            // 구석에서만 터지는 것처럼 보였다 — 맵 전체 연출 반경(_vfxMaxRadius)까지 넓게 뿌리고
            // 간격도 늘려서 실제로 맵 전역에 걸쳐 오래 쏟아지는 느낌이 나게 한다.
            // castVfx를 없앤 궁극기들(유성우·절대영도·심판)은 이 flourish가 사실상 유일한 화면 연출이라 —
            // 큰 이펙트 하나 대신 "작은 이펙트가 맵 전체에서 난리 치는" 느낌을 내야 함. 그래서 개별
            // 크기는 줄이고(0.6배), 터지는 간격은 짧게 잡아서 짧은 시간에 우르르 쏟아지게 한다.
            float spread = Mathf.Min(radius, _vfxMaxRadius);
            for (int i = 0; i < d.flourishCount; i++)
            {
                Vector3 p = IsMapWide(radius) ? RandomScreenPoint(center, spread) : center + (Vector3)(Random.insideUnitCircle * spread);
                var fx = SpawnVfx(d.flourishVfx, p, Quaternion.identity, d.vfxIsUi);
                if (fx != null) fx.transform.localScale *= 0.6f;
                AutoDestroy(fx);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
            }
        }

        private void SpawnZone(SkillData d, Vector3 origin, Vector3 moveDir, float radius, float duration)
        {
            var go = new GameObject($"Zone_{d.skillId}");
            go.transform.SetParent(transform);
            go.transform.position = origin;
            var zone = go.AddComponent<SkillZone>();
            zone.Init(_enemies, _allies, d, radius, duration);
            if (d.effectType == SkillEffectType.MovingZone)
            {
                zone.SetMoving(moveDir, d.zoneMoveSpeed, d.force);
            }

            GameObject visual = d.castVfx != null
                ? SpawnVfx(d.castVfx, origin, Quaternion.identity, d.vfxIsUi)
                : Code(Disc(), ZoneColor(d.zoneEffect), origin, radius * 2f, 6);
            if (visual != null)
            {
                if (d.castVfx != null) { ScaleAreaVfx(visual, radius); ApplyRecolor(visual, d.vfxRecolor); ApplyTint(visual, d.vfxTint); }
                visual.transform.SetParent(go.transform, true);
                Destroy(visual, d.effectType == SkillEffectType.MovingZone ? duration : duration + 0.5f);
            }
        }

        /// <summary>기존 VFX 프리팹 색만 곱해서 바꾼다 — 새 프리팹 없이 같은 이펙트를 다른 속성처럼
        /// 보이게 할 때 씀(예: 용암 이펙트를 초록빛으로 틴트해서 늪처럼). 흰색(기본값)이면 원본 그대로.</summary>
        private static readonly Dictionary<(Texture2D, Color32), Texture2D> _recolorTex = new Dictionary<(Texture2D, Color32), Texture2D>();
        private static readonly Dictionary<(Sprite, Color32), Sprite> _recolorSprite = new Dictionary<(Sprite, Color32), Sprite>();

        /// <summary>VFX의 색조를 target 색으로 교체(밝기·흰 하이라이트 유지). 곱하기 틴트로는 파랑→노랑 같은 색 계열
        /// 변경이 안 돼서, 스프라이트를 복제해 픽셀 색을 바꿔치기한다. 결과는 캐시. 프레임 전환은 오브젝트
        /// 활성화 방식이라 스프라이트를 갈아끼워도 애니메이션에 안 덮인다.</summary>
        private static void ApplyRecolor(GameObject go, Color target)
        {
            if (go == null || target.a <= 0f) return;
            Color32 key = target;
            foreach (var img in go.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                var sp = img.sprite;
                if (sp == null) continue;
                if (!_recolorSprite.TryGetValue((sp, key), out var recolored) || recolored == null)
                {
                    var src = sp.texture;
                    if (!_recolorTex.TryGetValue((src, key), out var tex) || tex == null)
                    {
                        tex = RecolorTexture(src, target);
                        _recolorTex[(src, key)] = tex;
                    }
                    var pivot = new Vector2(sp.pivot.x / sp.rect.width, sp.pivot.y / sp.rect.height);
                    recolored = Sprite.Create(tex, sp.textureRect, pivot, sp.pixelsPerUnit);
                    _recolorSprite[(sp, key)] = recolored;
                }
                img.sprite = recolored;
            }
        }

        private static Texture2D RecolorTexture(Texture2D src, Color target)
        {
            // 읽기 불가 텍스처라 GPU로 복사(Blit)해서 읽는다.
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f;
                float v = Mathf.Max(r, Mathf.Max(g, b));
                float sat = v > 0.0001f ? (v - Mathf.Min(r, Mathf.Min(g, b))) / v : 0f;
                float nr = Mathf.Lerp(v, target.r * v, sat);
                float ng = Mathf.Lerp(v, target.g * v, sat);
                float nb = Mathf.Lerp(v, target.b * v, sat);
                px[i] = new Color32((byte)(nr * 255f), (byte)(ng * 255f), (byte)(nb * 255f), px[i].a);
            }
            tex.SetPixels32(px);
            tex.filterMode = src.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        private static void ApplyTint(GameObject go, Color tint)
        {
            if (go == null || tint == Color.white) return;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>()) sr.color *= tint;
            foreach (var g in go.GetComponentsInChildren<UnityEngine.UI.Graphic>()) g.color *= tint;
        }

        private void HealAllies(SkillData d)
        {
            if (_allies == null) return;
            foreach (var a in _allies.Allies)
            {
                a.HealFraction(d.duration);
                // 즉발 힐이라 실제 "지속시간"은 없지만, 원래 AutoDestroy 기준(UI 0.85초)이 너무 짧게
                // 사라져서 눈에 잘 안 띄었다 — 조금 더 오래 보이게 고정 시간으로 늘림.
                if (d.perTargetVfx != null) Destroy(SpawnVfx(d.perTargetVfx, a.Position, Quaternion.identity, d.vfxIsUi), 1.4f);
                else StartCoroutine(ExpandFade(a.Position, 0.7f, new Color(0.4f, 1f, 0.5f), 0.3f, false));
            }
        }

        private void BuffAllies(SkillData d, float duration)
        {
            if (_allies == null) return;
            string key = BuffKey(d.buffStat);
            foreach (var a in _allies.Allies)
            {
                if (a.IsDead) continue;
                a.ApplyBuff(key, d.buffMultiplier, duration);
                if (d.perTargetVfx != null)
                {
                    // 버프가 실제로 유지되는 동안(duration) 대상 위에 계속 붙어서 보이게 — 예전엔
                    // 한 번 반짝하고 바로 사라져서(AutoDestroy) 버프가 걸려있는지 눈으로 알 수 없었다.
                    var fx = SpawnVfx(d.perTargetVfx, a.Position, Quaternion.identity, d.vfxIsUi);
                    if (fx != null)
                    {
                        var follow = fx.AddComponent<FollowAlly>();
                        follow.Target = a;
                        follow.ExpireTime = Time.time + duration;
                    }
                }
                else StartCoroutine(ExpandFade(a.Position, 0.6f, new Color(1f, 0.8f, 0.3f), 0.3f, true));
            }
        }

        /// <summary>버프 VFX를 대상 위치에 계속 붙여서 유지시키다가, 버프 지속시간이 끝나거나 대상이
        /// 죽으면 스스로 사라진다 — 광폭화·강철 피부처럼 몇 초간 유지되는 버프의 시전 이펙트용.</summary>
        private class FollowAlly : MonoBehaviour
        {
            public IHealable Target;
            public float ExpireTime;
            private void Update()
            {
                // 인터페이스 참조는 유니티 오브젝트가 Destroy돼도 C# null이 아니라서, 파괴 여부를 따로 확인해야
                // 함(안 하면 죽어서 사라진 유닛의 transform을 읽다가 MissingReferenceException).
                bool gone = Target == null || (Target is Object o && o == null);
                if (gone || Target.IsDead || Time.time >= ExpireTime) { Destroy(gameObject); return; }
                transform.position = Target.Position;
            }
        }

        /// <summary>UnitBase.ApplyBuff(string,...)가 기대하는 키로 변환 — BuffStat.ToString()은
        /// "AttackSpeed"(파스칼 케이스)를 주는데 UnitBase 쪽 switch는 "attackSpeed"(카멜 케이스)만
        /// 받아서, 그대로 넘기면 케이스가 안 맞아 아무 case에도 안 걸리고 조용히 무시됐었다(광폭화·
        /// 강철 피부가 연출만 나가고 실제 스탯은 하나도 안 바뀌던 버그의 원인).</summary>
        private static string BuffKey(BuffStat stat) => stat switch
        {
            BuffStat.AttackSpeed => "attackSpeed",
            BuffStat.Defense => "defense",
            BuffStat.Attack => "attack",
            _ => stat.ToString(),
        };

        private void Revive(int count)
        {
            int n = _allies?.ReviveDead(count) ?? 0;
            Debug.Log($"[Skill] 망자 부활 {n}명");
        }

        // ─────────────────────────────────────────── VFX

        /// <summary>영역 스킬 VFX 를 사거리 표시(지름 = radius*2)와 "눈에 보이는 크기"가 같게 스케일. 맵 전체는
        /// _vfxMaxRadius 로 캡. 이펙트 프레임(64px)에는 투명 여백이 많아서(실측 폭 비율 0.5~0.9) 프레임
        /// 크기 기준으로 맞추면 표시보다 작게 보이므로, 스프라이트의 실제 보이는 폭(tight mesh)을 재서 보정한다.</summary>
        private void ScaleAreaVfx(GameObject fx, float radius)
        {
            if (fx == null || radius <= 0f) return;
            float targetDiameter = Mathf.Min(radius, _vfxMaxRadius) * 2f;
            float visibleNative = Mathf.Max(_vfxBaseSize * MeasureVisibleFraction(fx), 0.1f);
            float mul = Mathf.Clamp(targetDiameter / visibleNative, 0.3f, 30f);
            fx.transform.localScale *= mul;
            CenterOnVisible(fx);
        }

        /// <summary>이펙트 프레임 안에서 "눈에 보이는 부분"의 중심이 프레임 중심과 다른 경우(예: 화염 폭발은 8~10px
        /// 아래에 그려져 있음)를 보정해서, 보이는 중심이 정확히 조준 지점(사거리 원의 중심)에 오게 한다.
        /// 가장 넓게 보이는 프레임을 기준으로 한다.</summary>
        private static void CenterOnVisible(GameObject fx)
        {
            if (fx == null || fx.transform.childCount == 0) return;
            var child = fx.transform.GetChild(0) as RectTransform;
            if (child == null) return;

            // 프레임마다 보이는 모양이 다르므로 충분히 큰 프레임들(최대 폭의 50% 이상)의 무게중심 평균을 쓴다.
            // 바운딩 박스 중심이 아니라 알파 무게중심이라, 납작한 지면 폭발처럼 아래쪽에 덩어리가 몰린 이펙트도
            // "눈에 보이는 덩어리"의 중심이 조준 지점에 오게 된다.
            var images = child.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            float maxWidth = 0f;
            foreach (var img in images)
                if (img.sprite != null) maxWidth = Mathf.Max(maxWidth, GetSpriteStats(img.sprite).widthPx);
            if (maxWidth <= 0f) return;

            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (var img in images)
            {
                if (img.sprite == null) continue;
                var st = GetSpriteStats(img.sprite);
                if (st.widthPx < maxWidth * 0.5f) continue;
                sum += st.centroidOffsetPx;
                count++;
            }
            if (count == 0) return;
            // 가로(x)는 보정하지 않는다: 이펙트 프레임은 원래 가로 중앙에 대칭으로 그려져 있고, 프레임별 무게중심 x
            // 흔들림은 뒤쪽 프레임의 파편이 한쪽으로 튀어서 생기는 애니메이션 노이즈라 평균을 내면 오히려 한쪽(왼쪽)
            // 으로 밀린다. 세로(y)는 지면 폭발처럼 덩어리가 아래로 몰린 경우가 있어 무게중심으로 맞춘다.
            Vector2 avg = sum / count;
            child.anchoredPosition = new Vector2(0f, -avg.y * child.localScale.x);
        }

        /// <summary>프리팹 안 모든 프레임 이미지 중 가장 넓게 보이는 폭이 프레임 폭의 몇 배인지(0~1) —
        /// 실제 픽셀 알파로 잰다(스프라이트 메시 bounds는 여유가 있어 실제보다 넓게 나와서 VFX가 작아졌음).</summary>
        private static float MeasureVisibleFraction(GameObject fx)
        {
            float best = 0f;
            foreach (var img in fx.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                var sp = img.sprite;
                if (sp == null || sp.rect.width <= 0f) continue;
                best = Mathf.Max(best, GetSpriteStats(sp).widthPx / sp.rect.width);
            }
            return best > 0.05f ? Mathf.Clamp(best, 0.2f, 1f) : 1f;
        }

        private struct SpriteStats { public float widthPx; public Vector2 centroidOffsetPx; }
        private static readonly Dictionary<Sprite, SpriteStats> _statsCache = new Dictionary<Sprite, SpriteStats>();
        private static readonly Dictionary<Texture2D, Color32[]> _pixelCache = new Dictionary<Texture2D, Color32[]>();

        /// <summary>스프라이트의 실제 보이는 폭(알파 기준)과, 프레임 중심에서 본 "무게중심"(알파 가중 평균) 오프셋.
        /// 읽기 불가 텍스처라 GPU로 복사해서 읽고 결과는 캐시한다.</summary>
        private static SpriteStats GetSpriteStats(Sprite sp)
        {
            if (_statsCache.TryGetValue(sp, out var cached)) return cached;

            var tex = sp.texture;
            if (!_pixelCache.TryGetValue(tex, out var px))
            {
                var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                px = readable.GetPixels32();
                Object.Destroy(readable);
                _pixelCache[tex] = px;
            }

            Rect r = sp.textureRect;
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            int minX = int.MaxValue, maxX = int.MinValue;
            double sumX = 0, sumY = 0, sumA = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a = px[(y0 + y) * tex.width + (x0 + x)].a / 255f;
                    if (a < 0.3f) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    sumX += x * a; sumY += y * a; sumA += a;
                }
            }

            var stats = new SpriteStats();
            if (sumA > 0.0)
            {
                stats.widthPx = maxX - minX + 1;
                // 스프라이트 피벗(=프레임 중심)에서 본 무게중심 오프셋(픽셀, 위쪽 +).
                stats.centroidOffsetPx = new Vector2((float)(sumX / sumA) + 0.5f - sp.pivot.x, (float)(sumY / sumA) + 0.5f - sp.pivot.y);
            }
            _statsCache[sp] = stats;
            return stats;
        }

        private GameObject SpawnVfx(GameObject prefab, Vector3 pos, Quaternion rot, bool isUi)
        {
            GameObject go;
            if (isUi)
            {
                var canvasGo = new GameObject("WorldVfxCanvas");
                canvasGo.transform.SetParent(transform);
                canvasGo.transform.SetPositionAndRotation(pos, rot);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasGo.transform.localScale = Vector3.one * (1f / 64f);
                var child = Instantiate(prefab, canvasGo.transform);
                child.transform.localPosition = Vector3.zero;
                // 프리팹 루트가 RectTransform이면 anchoredPosition도 명시적으로 0으로 맞춰서, 프리팹
                // 원본에 혹시 남아있을 수 있는 오프셋과 무관하게 항상 캔버스(=목표 지점) 정중앙에 오게 함.
                if (child.transform is RectTransform rt) rt.anchoredPosition = Vector2.zero;
                go = canvasGo;
            }
            else
            {
                go = Instantiate(prefab, pos, rot, transform);
            }

            if (!_playSfx)
            {
                foreach (var au in go.GetComponentsInChildren<AudioSource>()) au.enabled = false;
            }

            return go;
        }

        private void KillVfx(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponentInChildren<ParticleSystem>() != null) Destroy(go, 0.1f);
            else Destroy(go);
        }

        private void AutoDestroy(GameObject go)
        {
            if (go == null) return;
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                Destroy(go, Mathf.Max(ps.main.duration + ps.main.startLifetime.constantMax, 0.5f));
                return;
            }

            // uGUI(Animator) 기반 Pixel Art VFX — 한 번 재생 길이만큼만
            bool isUi = go.GetComponentInChildren<Canvas>() != null || go.GetComponentInChildren<Animator>() != null;
            Destroy(go, isUi ? _uiVfxLifetime : _fallbackLifetime);
        }

        // ─────────────────────────────────────────── 코드 연출 유틸

        private IEnumerator ExpandFade(Vector3 c, float targetR, Color col, float dur, bool ringOnly)
        {
            var go = Code(ringOnly ? Ring() : Disc(), col, c, 0.1f, 21);
            var sr = go.GetComponent<SpriteRenderer>();
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, targetR * 2f, k);
                sr.color = new Color(col.r, col.g, col.b, Mathf.Lerp(0.85f, 0f, k));
                yield return null;
            }
            Destroy(go);
        }

        private void Arc(Vector3 a, Vector3 b, Color col)
        {
            var go = new GameObject("Arc");
            go.transform.SetParent(transform);
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Face(b - a);
            go.transform.localScale = new Vector3(Vector3.Distance(a, b), 0.12f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Disc();
            sr.color = col;
            sr.sortingOrder = 20;
            StartCoroutine(FadeKill(sr, 0.15f));
        }

        private IEnumerator FadeKill(SpriteRenderer sr, float dur)
        {
            Color c = sr.color;
            float t = 0f;
            while (t < dur && sr != null)
            {
                t += Time.deltaTime;
                sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, t / dur));
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        private GameObject Code(Sprite s, Color col, Vector3 pos, float scale, int order)
        {
            var go = new GameObject("CodeVfx");
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = col;
            sr.sortingOrder = order;
            return go;
        }

        private static Color ZoneColor(ZoneEffect e) => e switch
        {
            ZoneEffect.Stun => new Color(0.4f, 0.8f, 1f, 0.22f),
            ZoneEffect.Slow => new Color(0.5f, 0.5f, 0.7f, 0.22f),
            ZoneEffect.Vulnerable => new Color(0.9f, 0.4f, 0.9f, 0.22f),
            ZoneEffect.DamageOverTime => new Color(0.6f, 0.9f, 0.3f, 0.22f),
            ZoneEffect.Heal => new Color(0.4f, 1f, 0.5f, 0.22f),
            _ => new Color(1f, 1f, 1f, 0.2f),
        };

        private IDamageable NearestUnhit(Vector3 from, HashSet<IDamageable> exclude, Vector3 areaCenter, float areaRadius)
        {
            IDamageable best = null;
            float bestSqr = float.MaxValue;
            float areaSqr = areaRadius * areaRadius;
            foreach (var c in _enemies.All)
            {
                if (c.IsDead || exclude.Contains(c)) continue;
                if ((c.Position - areaCenter).sqrMagnitude > areaSqr) continue; // 사거리 밖은 연쇄 대상 아님
                float s = (c.Position - from).sqrMagnitude;
                if (s < bestSqr) { bestSqr = s; best = c; }
            }
            return best;
        }

        private static IEnumerator Move(Transform tr, Vector3 from, Vector3 to, float dur)
        {
            float t = 0f;
            while (t < dur && tr != null)
            {
                t += Time.deltaTime;
                tr.position = Vector3.Lerp(from, to, t / dur);
                yield return null;
            }
        }

        private static Quaternion Face(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return Quaternion.identity;
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        private Sprite Disc() => _disc ??= MakeCircle(64, 0f);
        private Sprite Ring() => _ring ??= MakeCircle(96, 0.82f);

        private static Sprite MakeCircle(int size, float innerFraction)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float outer = size * 0.5f - 1f;
            float inner = outer * innerFraction;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dd = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(outer - dd) * (innerFraction > 0f ? Mathf.Clamp01(dd - inner) : 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
