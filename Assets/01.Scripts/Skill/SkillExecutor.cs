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
        [Tooltip("Pixel Art VFX 프리팹의 기준 월드 크기(칸). UI 캔버스 스케일이 64px=1칸이라 1")]
        [SerializeField] private float _vfxBaseSize = 1f;
        [Tooltip("맵 전체(radius 큰) 스킬의 VFX 스케일 상한 — 이 칸수로 캡")]
        [SerializeField] private float _vfxMaxRadius = 9f;
        [Tooltip("피해 판정에 더하는 여유(유닛 스프라이트 반폭) — 링에 걸친 적도 맞게")]
        [SerializeField] private float _hitMargin = 0.35f;
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
        }

        private Vector3 CasterPos => _caster != null ? _caster.position : Vector3.zero;

        /// <summary>
        /// 증강 "연쇄 폭발" 전용 — 스킬이 아닌 외부 트리거(용사 사망 등)로 즉시 폭발 피해를 준다.
        /// 코드 연출 폴백만 사용(전용 VFX 없음).
        /// </summary>
        public void Detonate(Vector3 point, float power, float radius)
        {
            if (_enemies == null || power <= 0f) return;
            _enemyBuf.Clear();
            _enemies.QueryInRadius(point, radius + _hitMargin, _enemyBuf);
            foreach (var e in _enemyBuf) e.TakeDamage(power);
            StartCoroutine(ExpandFade(point, radius, new Color(1f, 0.35f, 0.1f), 0.3f, false));
        }

        // ─────────────────────────────────────────── 라우팅

        private void OnCast(SkillCastRequest req)
        {
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

                case SkillEffectType.ChainDamage: StartCoroutine(Chain(d, power, p)); break;
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

            Vector3 from = point + Vector3.up * _skyHeight;
            GameObject meteor = d.projectileVfx != null
                ? SpawnVfx(d.projectileVfx, from, Quaternion.identity, d.vfxIsUi)
                : Code(Disc(), new Color(1f, 0.5f, 0.15f), from, 0.6f, 22);
            yield return Move(meteor.transform, from, point, 0.18f);
            KillVfx(meteor);
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
            for (int i = 0; i < d.barrageCount; i++)
            {
                Vector3 p = center + (Vector3)(Random.insideUnitCircle * radius);
                Impact(d, power, p, 1.7f); // 개별 낙하는 작게
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
            }

            if (d.castVfx != null)
            {
                var fx = SpawnVfx(d.castVfx, point, Quaternion.identity, d.vfxIsUi);
                ScaleAreaVfx(fx, r);
                AutoDestroy(fx);
            }
            else
            {
                StartCoroutine(ExpandFade(point, r, new Color(1f, 0.5f, 0.15f), 0.35f, false));
            }
        }

        private IEnumerator Chain(SkillData d, float power, Vector3 from)
        {
            // 시전 지점에 한 번 크게 터지는 연출을 깔아서(전기 소용돌이 등) 체인 전체가 더 커 보이게 함.
            if (d.castVfx != null) AutoDestroy(SpawnVfx(d.castVfx, from, Quaternion.identity, d.vfxIsUi));

            var hit = new HashSet<IDamageable>();
            Vector3 cursor = from;
            for (int i = 0; i < d.chainCount; i++)
            {
                IDamageable next = NearestUnhit(cursor, hit);
                if (next == null) break;
                next.TakeDamage(power);
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
            Vector3 to = from + dir * d.lineLength;
            float hitR = Mathf.Max(radius, 0.4f);

            GameObject proj = d.castVfx != null
                ? SpawnVfx(d.castVfx, from, Face(dir), d.vfxIsUi)
                : Code(Disc(), new Color(0.6f, 0.85f, 1f), from, 0.45f, 22);

            var hit = new HashSet<IDamageable>();
            const float dur = 0.16f;
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
                        hit.Add(e);
                    }
                }
                yield return null;
            }

            KillVfx(proj);
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
            else StartCoroutine(ExpandFade(point, Mathf.Min(radius, 14f), new Color(0.4f, 0.8f, 1f), 0.5f, true));
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
                _enemies.QueryInRadius(point, radius * 2.5f, _enemyBuf);
                foreach (var e in _enemyBuf)
                {
                    (e as IStatusReceiver)?.ApplyKnockback(point - e.Position, d.force * Time.deltaTime * 4f);
                }
                yield return null;
            }

            _enemyBuf.Clear();
            _enemies.QueryInRadius(point, radius + _hitMargin, _enemyBuf);
            foreach (var e in _enemyBuf) e.TakeDamage(power);

            if (fx != null) AutoDestroy(fx);
            if (d.finishVfx != null) AutoDestroy(SpawnVfx(d.finishVfx, point, Quaternion.identity, d.vfxIsUi));
            else StartCoroutine(ExpandFade(point, radius * 1.4f, new Color(0.6f, 0.3f, 0.85f), 0.35f, false));
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
                Vector3 p = center + (Vector3)(Random.insideUnitCircle * spread);
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
                if (d.castVfx != null) { ScaleAreaVfx(visual, radius); ApplyTint(visual, d.vfxTint); }
                visual.transform.SetParent(go.transform, true);
                Destroy(visual, d.effectType == SkillEffectType.MovingZone ? duration : duration + 0.5f);
            }
        }

        /// <summary>기존 VFX 프리팹 색만 곱해서 바꾼다 — 새 프리팹 없이 같은 이펙트를 다른 속성처럼
        /// 보이게 할 때 씀(예: 용암 이펙트를 초록빛으로 틴트해서 늪처럼). 흰색(기본값)이면 원본 그대로.</summary>
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
                if (Target == null || Target.IsDead || Time.time >= ExpireTime) { Destroy(gameObject); return; }
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

        /// <summary>영역 스킬 VFX 를 radius 지름에 맞춰 스케일. 맵 전체는 _vfxMaxRadius 로 캡.</summary>
        private void ScaleAreaVfx(GameObject fx, float radius)
        {
            if (fx == null || radius <= 0f) return;
            float targetDiameter = Mathf.Min(radius, _vfxMaxRadius) * 2f;
            float mul = Mathf.Clamp(targetDiameter / Mathf.Max(_vfxBaseSize, 0.1f), 0.6f, 8f);
            fx.transform.localScale *= mul;
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

        private IDamageable NearestUnhit(Vector3 from, HashSet<IDamageable> exclude)
        {
            IDamageable best = null;
            float bestSqr = float.MaxValue;
            foreach (var c in _enemies.All)
            {
                if (c.IsDead || exclude.Contains(c)) continue;
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
