using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OZGL2.Contracts;
using OZGL2.Skill;
using OZGL2.Synergy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OZGL2.InGame.Editor
{
    public static class InGameSkillLifecycleVerification
    {
        public static string Result { get; private set; } = "Not run";

        [MenuItem("OZGL2/InGame/Verify Skill Lifecycle (Empty Play Scene)")]
        public static async void Run()
        {
            if (!Application.isPlaying) { Result = "Play Mode required"; return; }
            if (Object.FindFirstObjectByType<InGamePrototypeBootstrap>() != null)
            { Result = "Empty Play scene required"; return; }
            Result = "Running";
            var root = new GameObject("SkillLifecycleVerification");
            var data = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                var target = new Target();
                var targets = new Targets(target);
                var manager = new SkillManager(targets);
                var executor = root.AddComponent<SkillExecutor>();
                executor.Bind(manager, targets, null, Vector3.zero);
                data.skillId = "verification_skill";
                data.cooldown = 0f;
                data.skillPower = 10f;
                data.castMode = SkillCastMode.Targeted;
                data.effectType = SkillEffectType.SingleDamage;
                var skill = manager.Register(data, true);
                Check(manager.TryEquip(skill), "Equip");

                manager.IsCastingEnabled = false;
                Check(!manager.TryCastTargeted(skill, Vector3.zero), "Targeted cast blocked");
                data.castMode = SkillCastMode.Instant;
                Check(!manager.TryCastInstant(skill), "Instant cast blocked");
                Check(target.Damage == 0f && skill.RemainingCooldown(Time.time) == 0f, "No damage or cooldown while blocked");

                data.castMode = SkillCastMode.Targeted;
                manager.IsCastingEnabled = true;
                Check(manager.TryCastTargeted(skill, Vector3.zero), "Combat cast accepted");
                manager.IsCastingEnabled = false;
                executor.CancelActiveEffects();
                executor.CancelActiveEffects();
                await WaitSeconds(0.5f);
                Check(target.Damage == 0f, "Cancelled projectile cannot damage a reused target");
                Check(root.transform.childCount == 1, "Only caster anchor remains");

                manager.IsCastingEnabled = true;
                Check(manager.TryCastTargeted(skill, Vector3.zero), "Next combat cast accepted");
                await WaitSeconds(0.5f);
                Check(target.Damage == 10f, "Next combat damages once, without old subscription");
                executor.CancelActiveEffects();

                data.effectType = SkillEffectType.PersistentZone;
                data.zoneEffect = ZoneEffect.DamageOverTime;
                data.duration = 10f;
                data.zoneMagnitude = 2f;
                Check(manager.TryCastTargeted(skill, Vector3.zero), "Zone cast accepted");
                Check(root.GetComponentsInChildren<SkillZone>().Length == 1, "Zone exists");
                manager.IsCastingEnabled = false;
                executor.CancelActiveEffects();
                float damageBefore = target.Damage;
                await WaitSeconds(0.3f);
                Check(target.Damage == damageBefore && root.GetComponentsInChildren<SkillZone>().Length == 0, "Zone stops immediately");

                var bar = new GameObject("VerificationSkillBar").AddComponent<SkillBarUI>();
                bar.transform.SetParent(root.transform);
                bar.Bind(manager, Camera.main, Vector3.zero);
                var beginAim = typeof(SkillBarUI).GetMethod("BeginAim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var icons = (System.Collections.IList)typeof(SkillBarUI).GetField("_icons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(bar);
                beginAim.Invoke(bar, new[] { icons[0] });
                Check(bar.transform.Find("AimReticle").gameObject.activeSelf, "Aim started");
                bar.gameObject.SetActive(false);
                Check(!bar.transform.Find("AimReticle").gameObject.activeSelf, "Disable cancels aim");
                bar.gameObject.SetActive(true);
                Check(!bar.transform.Find("AimReticle").gameObject.activeSelf, "Aim does not return next round");

                var syncObject = new GameObject("VerificationSynergy");
                syncObject.transform.SetParent(root.transform);
                var sync = syncObject.AddComponent<RealSynergySync>();
                sync.BeginRun();
                var previousManager = sync.SkillManager;
                var oldConnection = new InGameSkillConnection(sync);
                var oldSynergy = new InGameSynergyConnection(null, sync);
                sync.BeginRun();
                sync.SetCombatEnabled(true);
                oldConnection.StopCombat();
                await oldConnection.CleanupAsync(null);
                oldSynergy.Dispose();
                Check(!oldConnection.IsCurrent && !oldSynergy.IsConnected, "Old run no longer owns persistent services");
                Check(!previousManager.IsCastingEnabled && sync.SkillManager.IsCastingEnabled, "Late cleanup preserves new run and retires old manager");
                bool rejected = false;
                try { oldConnection.SetCombatEnabled(true); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Old run cannot resume combat");
                sync.StopCombat();

                var invalidBootstrap = root.AddComponent<InGamePrototypeBootstrap>();
                invalidBootstrap.enabled = false;
                invalidBootstrap.StartPrototype();
                await invalidBootstrap.Completion;
                var actualSync = Object.FindFirstObjectByType<RealSynergySync>();
                Check(invalidBootstrap.Error != null && actualSync != null && !actualSync.SkillManager.IsCastingEnabled,
                    "Configuration failure keeps skills blocked");
                Result = "PASS: instant/targeted gate, no blocked cooldown, delayed damage cancellation, repeat cleanup, next-round cast, zone cleanup, aim cancellation, stale run ownership, configuration failure";
            }
            catch (Exception exception) { Result = "FAIL: " + exception; }
            finally { Object.Destroy(root); Object.Destroy(data); }
        }

        private static async Task WaitSeconds(float seconds)
        {
            double until = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < until)
            {
                if (!Application.isPlaying) throw new InvalidOperationException("Play Mode ended during verification.");
                await Task.Delay(16);
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private sealed class Target : IDamageable
        {
            public float Damage { get; private set; }
            public float CurrentHp => MaxHp - Damage;
            public float MaxHp => 1000f;
            public bool IsDead => false;
            public Vector3 Position => Vector3.zero;
            public void TakeDamage(float amount) => Damage += amount;
        }

        private sealed class Targets : ITargetProvider
        {
            public IReadOnlyList<IDamageable> All { get; }
            public Targets(IDamageable target) { All = new[] { target }; }
            public IDamageable Nearest(Vector3 from) => All[0];
            public void QueryInRadius(Vector3 center, float radius, List<IDamageable> results)
            { results.AddRange(All.Where(target => Vector3.Distance(center, target.Position) <= radius)); }
        }
    }
}
