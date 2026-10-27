using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Editor;
using DeepWater.GeneralQuarters.Modules.Presentation;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// Slice 6, presentation: the core says where things happened (and that changes nothing in the run), mounts lay on
    /// their targets, the synthesised sounds are seeded and sane, and the presentation profile matches the ship model.
    /// </summary>
    public class PresentationTests
    {
        static readonly OwnShipState Own = new OwnShipState { Position = Vector3.zero, Velocity = Vector3.zero, HeadingDeg = 0f };

        static CombatSystem RunRaid(int threats, ulong seed)
        {
            CombatLoadout loadout = TestLoadouts.Ddg51L0(seed);
            var cs = new CombatSystem(loadout);
            var raid = new ScriptedRaid(TestLoadouts.SeaSkimmerRaid(threats), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            for (int i = 0; i < 9000; i++)
            {
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                cs.Step(Own, truth, hits, kills);
                raid.ApplyKills(kills);
                if (raid.Resolved && cs.Vls.InFlight.Count == 0) break;
            }
            return cs;
        }

        [Test]
        public void Events_SayWhereTheyHappened_WithWhatAndFromWhere()
        {
            CombatLoadout loadout = TestLoadouts.Ddg51L0(1442);
            CombatSystem cs = RunRaid(8, 1442);
            IReadOnlyList<EngagementEvent> events = cs.Log.Events;
            var spatial = new[] { EventKind.Launched, EventKind.BurstFired, EventKind.Kill, EventKind.Miss, EventKind.ShipHit };
            Assert.Greater(events.Count(e => e.Kind == EventKind.Launched), 0);
            Assert.Greater(events.Count(e => e.Kind == EventKind.BurstFired), 0);
            foreach (EngagementEvent e in events.Where(x => spatial.Contains(x.Kind)))
                Assert.IsTrue(e.HasPoint, $"{e.Kind} event {e.Id} says where: {e.Text}");

            foreach (EngagementEvent e in events.Where(x => x.Kind == EventKind.Launched))
            {
                Assert.That(e.Source, Is.InRange(0, loadout.Vls.Groups.Length - 1), "a launch names its VLS group");
                Assert.AreEqual(0f, Vector3.Distance(Own.ToWorld(loadout.Vls.Groups[e.Source].OffsetShip), e.Point), 1e-3f, "at that group's cells");
                Assert.AreEqual(WeaponClass.Essm, e.Weapon);
            }
            foreach (EngagementEvent e in events.Where(x => x.Kind == EventKind.BurstFired))
            {
                Assert.That(e.Source, Is.InRange(0, loadout.Guns.Length - 1), "a burst names its mount");
                Assert.AreEqual(loadout.Guns[e.Source].Weapon, e.Weapon, "and the mount's weapon");
            }
            foreach (EngagementEvent e in events.Where(x => x.Kind == EventKind.Kill && x.Weapon == WeaponClass.Essm))
                Assert.Less(e.Point.y, 50f, "an ESSM kill of a sea-skimmer happens low: the presentation throws a splash");
            foreach (EngagementEvent e in events.Where(x => !spatial.Contains(x.Kind) && x.Kind != EventKind.Note && x.Kind != EventKind.SystemOffline))
                Assert.IsFalse(e.HasPoint, $"{e.Kind} has no place");
        }

        [Test]
        public void Saturation_Fix_IsUnchangedByPresentationData()
        {
            // The engagement log's places, weapons and sources are not in the Fix: adding them (slice 6) left the
            // saturation run bit-identical to slice 5's (METHODOLOGY C20). Change this constant only for a change of
            // behaviour you meant, and say so in the commit.
            CombatLoadout loadout = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake();
            loadout.GunsLevel = Core.Fidelity.FidelityLevel.L0;   // the slice 5 run this constant pins was at guns L0 (Stream B's L1 has its own test)
            loadout.DamageLevel = Core.Fidelity.FidelityLevel.L0; // and at Damage Control L0 (Stream C's L1 has its own tests)
            loadout.RadarLevel = Core.Fidelity.FidelityLevel.L0;  // and at Radar and Tracks L0 (Stream A's L1 has its own tests)
            loadout.TracksLevel = Core.Fidelity.FidelityLevel.L0;
            var cs = new CombatSystem(loadout);
            var ai = new Core.Captain.AiCaptainL0(AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset).Bake());
            var raid = new ScriptedRaid(AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(Ddg51CombatData.RaidDir + "/Raid_Saturation.asset").StartingAt(0), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            for (int i = 0; i < 12_000; i++)
            {
                ai.Step(cs, Own, Core.Captain.Seat.Tao);
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                cs.Step(Own, truth, hits, kills);
                raid.ApplyKills(kills);
                if (raid.Resolved && cs.Vls.InFlight.Count == 0) break;
            }
            Assert.AreEqual(0x2AF5C040E4AC6B5EUL, cs.Fix, $"Fix {cs.Fix:X16}");
        }

        [Test]
        public void MountAim_LaysTheBarrelOnTheTarget()
        {
            var pivot = new Vector3(0f, 8f, 50f);
            MountAim.Solve(pivot, 0f, pivot + new Vector3(0f, 0f, 1000f), out float train, out float elevation);
            Assert.AreEqual(0f, train, 1e-3f, "dead ahead, from a bow mount: no train");
            Assert.AreEqual(0f, elevation, 1e-3f);
            MountAim.Solve(pivot, 0f, pivot + new Vector3(1000f, 1000f, 0f), out train, out elevation);
            Assert.AreEqual(90f, train, 1e-3f, "starboard beam: 90° right");
            Assert.AreEqual(45f, elevation, 1e-3f, "as high as it is far: 45°");
            MountAim.Solve(pivot, 180f, pivot + new Vector3(0f, 0f, -500f), out train, out _);
            Assert.AreEqual(0f, train, 1e-3f, "an aft mount's rest is astern");
            MountAim.Solve(pivot, 180f, pivot + new Vector3(-500f, 0f, 0f), out train, out _);
            Assert.AreEqual(90f, train, 1e-3f, "port beam from an aft-facing mount is 90° to its right");
            Assert.AreEqual(10f, MountAim.Slew(0f, 90f, 10f), 1e-4f, "slews at its rate");
            Assert.AreEqual(-175f, MountAim.Slew(175f, -170f, 10f), 1e-4f, "the short way round");
        }

        [Test]
        public void MountAnimator_AtGunsL1_ShowsTheCoresLay()
        {
            // The Mk 45 alone at guns L1, against a sea-skimmer on bearing 60°: the core trains the mount, and the model follows.
            CombatLoadout loadout = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake();
            Assert.AreEqual(Core.Fidelity.FidelityLevel.L1, loadout.GunsLevel);
            loadout.Guns = new[] { loadout.Guns.First(g => g.Name == "Mk 45") };
            loadout.Vls.Loads[0].Rounds = 0;
            loadout.Guns[0].Ballistics.LethalRadiusM = 0f;   // keep the target alive: this is about pointing, not killing
            var cs = new CombatSystem(loadout);
            float brg = 60f * Mathf.Deg2Rad;
            var start = new Vector3(Mathf.Sin(brg), 0f, Mathf.Cos(brg)) * 9000f + Vector3.up * 5f;
            var world = new List<TruthTarget> { new TruthTarget { Id = 1, Kind = TargetKind.AntiShipMissile, Identity = Identity.Hostile, Position = start, Velocity = -start.normalized * 290f, Alive = true } };
            var kills = new List<KillReport>();
            for (int i = 0; i < 250; i++)
            {
                TruthTarget t = world[0];
                t.Position = start + t.Velocity * (i * cs.TickSeconds);
                world[0] = t;
                cs.Step(Own, world, new List<ShipHit>(), kills);
            }
            Assert.IsTrue(cs.Guns[0].TryGetLay(out float coreTrain, out float coreElevation), "guns L1 report their lay");
            Assert.AreEqual(60f, coreTrain, 2f, "the core has trained the Mk 45 onto the threat");

            var ship = new GameObject("ship");
            try
            {
                var train = new GameObject("train").transform;
                train.SetParent(ship.transform, false);
                var elevate = new GameObject("elevate").transform;
                elevate.SetParent(train, false);
                var rig = new MountRig { mountName = "Mk 45", trainTransform = "train", elevateTransform = "elevate", trainRateDegS = 30f, elevateRateDegS = 20f, barrelM = 7f, holdS = 2f };
                var animator = new MountAnimator(ship.transform, cs.Guns, new[] { rig });
                for (int i = 0; i < 20; i++) animator.Update(i * 0.2f, 0.2f);   // long enough to catch up from rest
                Assert.AreEqual(coreTrain, Mathf.DeltaAngle(0f, train.localEulerAngles.y), 0.01f, "the model trains to the core's train");
                // At rest the barrel points along +Z (facing 0°); raising it turns about the axis across, so +Z tips up.
                Vector3 barrel = elevate.localRotation * Vector3.forward;
                Assert.AreEqual(coreElevation, Mathf.Asin(barrel.y) * Mathf.Rad2Deg, 0.01f, "and elevates to the core's elevation");
            }
            finally { Object.DestroyImmediate(ship); }
        }

        [Test]
        public void SynthClips_AreSeeded_Audible_AndBounded()
        {
            foreach (SynthSound s in System.Enum.GetValues(typeof(SynthSound)))
            {
                if (s == SynthSound.None) continue;
                float[] a = SynthClips.Samples(s), b = SynthClips.Samples(s);
                CollectionAssert.AreEqual(a, b, $"{s} is the same every run");
                float peak = a.Max(Mathf.Abs);
                Assert.That(peak, Is.InRange(0.5f, 1f), $"{s} is audible and does not clip");
            }
            Assert.AreEqual(1.4f, SynthClips.Samples(SynthSound.CiwsBurst).Length / (float)SynthClips.SampleRate, 0.01f, "a CIWS burst: 100 rounds at 4,500 a minute, and its tail");
            Assert.AreEqual(6f, SynthClips.Samples(SynthSound.Alarm).Length / (float)SynthClips.SampleRate, 0.01f);
        }

        [Test]
        public void PresentationAsset_CoversEveryEffect_AndMatchesTheShipModel()
        {
            var p = AssetDatabase.LoadAssetAtPath<CombatPresentationSO>(CombatEffectsBuilder.PresentationAsset);
            Assert.IsNotNull(p, "run Deep Water → General Quarters → Build Combat Effects");
            EffectSlot[] slots = { p.vlsLaunch, p.muzzleMk45, p.muzzleCiws, p.muzzleMk38, p.airburst, p.warhead, p.splash, p.shipHit, p.fire, p.systemSmoke, p.bowSpray, p.essmTrail, p.threatTrail };
            foreach (EffectSlot s in slots)
            {
                Assert.IsNotNull(s.prefab, "every slot has an effect");
                Assert.IsTrue(s.prefab.GetComponentsInChildren<ParticleSystem>(true).Length + s.prefab.GetComponentsInChildren<TrailRenderer>(true).Length > 0, $"{s.prefab.name} draws something");
            }
            Assert.IsNotNull(p.muzzleCiws.prefab.transform.Find("Tracers"), "the CIWS streams tracers");

            var ship = AssetDatabase.LoadAssetAtPath<GameObject>(Ddg51HullBuilder.PrefabPath).transform;
            string[] names = ship.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            CombatLoadout loadout = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake();
            foreach (GunMountProfileData g in loadout.Guns)
            {
                MountRig rig = p.mounts.Single(m => m.mountName == g.Name);
                CollectionAssert.Contains(names, rig.trainTransform, $"{g.Name} trains");
                if (!string.IsNullOrEmpty(rig.elevateTransform)) CollectionAssert.Contains(names, rig.elevateTransform, $"{g.Name} elevates");
            }
            Assert.AreEqual(32, names.Count(n => n.StartsWith(p.vlsCellPrefixes[0])), "fore VLS: 32 cells");
            Assert.AreEqual(64, names.Count(n => n.StartsWith(p.vlsCellPrefixes[1])), "aft VLS: 64 cells");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/General Quarters/Modules/Presentation/Shaders/GQ_Particle.shader");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "the HDRP particle shader compiles");
        }
    }
}
