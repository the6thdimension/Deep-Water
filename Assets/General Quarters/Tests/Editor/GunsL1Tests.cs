using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Ballistics;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Editor;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// Stream B slice B1, guns L1 (G35–G41): the atmosphere and drag curve against their sources, the calibrated ranges
    /// and the published figures as VALIDATION, fire control that hits what it aims at and leads what moves, mounts that
    /// slew, ship motion and spotting bias as miss, both Mk 45 fuzes, and a saturation raid that repeats and fits the budget.
    /// </summary>
    public class GunsL1Tests
    {
        static readonly OwnShipState Own = new OwnShipState { Position = Vector3.zero, Velocity = Vector3.zero, HeadingDeg = 0f };

        static float Num(string text, string pattern) =>
            float.Parse(Regex.Match(text, pattern).Groups[1].Value, CultureInfo.InvariantCulture);

        /// <summary>The test loadout at guns L1, with the real mounts' ballistics: [0] Mk 45 (bow), [1] CIWS (aft-facing), [2] Mk 38 (port).</summary>
        static CombatLoadout L1(ulong seed = 1442)
        {
            CombatLoadout l = TestLoadouts.Ddg51L0(seed);
            l.GunsLevel = FidelityLevel.L1;
            l.Guns[0].Ballistics = Ddg51CombatData.Mk45L1;
            l.Guns[1].Ballistics = Ddg51CombatData.CiwsL1;
            l.Guns[2].Ballistics = Ddg51CombatData.Mk38L1;
            l.Vls.Loads[0].Rounds = 0;   // guns only: no ESSM in the way
            return l;
        }

        static CombatLoadout Only(CombatLoadout l, int gun)
        {
            l.Guns = new[] { l.Guns[gun] };
            return l;
        }

        static TruthTarget Target(int id, TargetKind kind, Vector3 position, Vector3 velocity) =>
            new TruthTarget { Id = id, Kind = kind, Identity = Identity.Hostile, Position = position, Velocity = velocity, Alive = true };

        /// <summary>Run <paramref name="ticks"/> with one target flying straight; it stops when killed.</summary>
        static CombatSystem Run(CombatLoadout loadout, TruthTarget target, int ticks, OwnShipState own)
        {
            var cs = new CombatSystem(loadout);
            var world = new List<TruthTarget> { target };
            var kills = new List<KillReport>();
            for (int i = 0; i < ticks; i++)
            {
                TruthTarget t = world[0];
                t.Position = target.Position + target.Velocity * (i * loadout.TickSeconds);
                world[0] = t;
                kills.Clear();
                cs.Step(own, world, new List<ShipHit>(), kills);
                if (kills.Count > 0) { t.Alive = false; world[0] = t; }
            }
            return cs;
        }

        // ── Ballistics against their sources ─────────────────────────────────

        [Test]
        public void Atmosphere_AndG7Curve_MatchTheirSources()
        {
            Atmosphere.Exact(0f, out float rho0, out float a0);
            Atmosphere.Exact(11000f, out float rho11, out float a11);
            Assert.AreEqual(1.2250f, rho0, 1e-4f, "ISA sea-level density");
            Assert.AreEqual(340.29f, a0, 0.01f, "ISA sea-level speed of sound");
            Assert.AreEqual(0.3639f, rho11, 1e-4f, "ISA density at the tropopause");
            Assert.AreEqual(295.07f, a11, 0.01f);
            Atmosphere.At(5025f, out float rhoT, out _);
            Atmosphere.Exact(5025f, out float rhoE, out _);
            Assert.AreEqual(rhoE, rhoT, 1e-4f, "the 50 m table interpolates the formulas");
            Assert.AreEqual(0.1194f, DragCurve.G7(0.5f), 1e-4f, "G7 (BRL) at Mach 0.5");
            Assert.AreEqual(0.3803f, DragCurve.G7(1.0f), 1e-4f, "at Mach 1");
            Assert.AreEqual(0.2424f, DragCurve.G7(3.0f), 1e-4f, "at Mach 3");
            Assert.AreEqual(0.1618f, DragCurve.G7(9f), 1e-4f, "held flat beyond the table");
        }

        [Test]
        public void Vacuum_GreatestRangeIsVSquaredOverG_At45Degrees()
        {
            var p = new ProjectileData { MassKg = 1f, DiameterM = 0.1f, MuzzleVelocityMps = 300f, FormFactor = 0f };
            FiringTable t = FiringTable.For(p, 0f, 85f);
            Assert.AreEqual(300f * 300f / FiringTable.Gravity, t.MaxRangeM, 0.001f * t.MaxRangeM, "no drag: the textbook range");
            Assert.AreEqual(45f, t.MaxRangeElevationDeg, 0.01f);
        }

        [Test]
        public void CalibratedFormFactors_ReproduceThePublishedRanges_AndTheValidationFigures()
        {
            GunBallisticsData mk45 = Ddg51CombatData.Mk45L1, ciws = Ddg51CombatData.CiwsL1, mk38 = Ddg51CombatData.Mk38L1;
            FiringTable t45 = FiringTable.For(mk45.Projectile, mk45.ElevationMinDeg, mk45.ElevationMaxDeg);
            Assert.AreEqual(23130f, t45.RangeAt(47f), 0.005f * 23130f, "CALIBRATED: Mk 45, 23,130 m at 47° (NavWeaps)");
            Assert.AreEqual(47f, t45.MaxRangeElevationDeg, 2f, "VALIDATION: the greatest range comes near the published 47°");
            Assert.Greater(t45.ApexM(65f), 7000f, "VALIDATION: at 65° the shell climbs past the published 7,000 m AA ceiling");

            FiringTable tc = FiringTable.For(ciws.Projectile, ciws.ElevationMinDeg, ciws.ElevationMaxDeg);
            Assert.AreEqual(5500f, tc.RangeAt(45f), 0.005f * 5500f, "CALIBRATED: Mk 149, 5,500 m at 45° (NavWeaps)");

            FiringTable t38 = FiringTable.For(mk38.Projectile, mk38.ElevationMinDeg, mk38.ElevationMaxDeg);
            Assert.AreEqual(6800f, t38.MaxRangeM, 0.005f * 6800f, "CALIBRATED: 25 mm HEI, 6,800 m greatest range (NavWeaps)");
            Assert.LessOrEqual(t38.MaxRangeElevationDeg, mk38.ElevationMaxDeg);

            Assert.AreEqual(ciws.Projectile.FormFactor, FiringTable.CalibrateFormFactor(ciws.Projectile, 5500f, 45f), 0.002f,
                "the calibration finds the stored form factor back");
        }

        [Test]
        public void FireControl_SolutionPassesThroughTheAimPoint_AndRefusesWhatIsOutOfReach()
        {
            GunBallisticsData b = Ddg51CombatData.Mk45L1;
            FiringTable t = FiringTable.For(b.Projectile, b.ElevationMinDeg, b.ElevationMaxDeg);
            var xs = new List<float>();
            var ys = new List<float>();
            foreach (Vector2 point in new[] { new Vector2(8000f, 0f), new Vector2(3000f, 2000f), new Vector2(12000f, 500f), new Vector2(1500f, -7f) })
            {
                Assert.IsTrue(t.TrySolve(point.x, point.y, out float elevation, out float tof), $"{point} is in reach");
                FiringTable.Fly(b.Projectile, elevation, xs, ys);
                int i = xs.FindIndex(x => x >= point.x);
                float f = (point.x - xs[i - 1]) / (xs[i] - xs[i - 1]);
                float y = ys[i - 1] + (ys[i] - ys[i - 1]) * f;
                float flown = (i - 1 + f) * FiringTable.SampleStepS;
                Assert.AreEqual(point.y, y, 3f, $"a shot at {elevation:0.00}° passes through {point}");
                Assert.AreEqual(flown, tof, 0.05f, "and the table's time of flight is the shot's");
            }
            Assert.IsFalse(t.TrySolve(30000f, 0f, out _, out _), "beyond the greatest range");

            // A sea-skimmer just under the muzzle, at every range out to the gun's reach: the solution never flickers.
            // (With a shallow floor the elevation under the solution ended short at long range and lost its bracket.)
            for (float x = 1000f; x <= 22_000f; x += 25f)
                Assert.IsTrue(t.TrySolve(x, -2.7f, out _, out _), $"a target 2.7 m below the muzzle at {x} m is in reach");
            Assert.IsFalse(t.TrySolve(2000f, 20000f, out _, out _), "above the highest shot");
        }

        // ── The mount in the core ────────────────────────────────────────────

        [Test]
        public void Lead_OnACrossingAircraft_PutsTheRoundsWhereItWillBe()
        {
            // An aircraft crossing 2.5 km ahead at 1 km altitude, 250 m/s: the Mk 45 must lead it by its time of flight.
            TruthTarget plane = Target(5, TargetKind.Aircraft, new Vector3(-6000f, 1000f, 2500f), new Vector3(250f, 0f, 0f));
            CombatLoadout l = Only(L1(), 0);
            CombatSystem cs = Run(l, plane, 1500, Own);
            EngagementEvent first = cs.Log.Events.First(e => (e.Kind == EventKind.Kill || e.Kind == EventKind.Miss) && e.Text.Contains("Mk 45"));
            float aimErr = Num(first.Text, @"aim ([0-9.]+)");
            Assert.Less(aimErr, 2f, $"the shell meets the aircraft: {first.Text}");
            Assert.That(first.Text, Does.Contain("lethal radius"), "proximity fuze against air (G39)");
        }

        [Test]
        public void Mount_TrainsAtItsRate_BeforeItsFirstShot()
        {
            // A sea-skimmer 8 km out on bearing 140°: the Mk 45 rests at 0°, so it must train 140° at 30°/s first.
            float brg = 140f * Mathf.Deg2Rad;
            Vector3 start = new Vector3(Mathf.Sin(brg), 0f, Mathf.Cos(brg)) * 8000f + Vector3.up * 5f;
            TruthTarget missile = Target(6, TargetKind.AntiShipMissile, start, -new Vector3(start.x, 0f, start.z).normalized * 290f);
            CombatLoadout l = Only(L1(), 0);
            l.Guns[0].Ballistics.LethalRadiusM = 0f;   // keep it alive: this test is about timing
            CombatSystem cs = Run(l, missile, 600, Own);
            EngagementEvent firstShot = cs.Log.Events.First(e => e.Kind == EventKind.BurstFired);
            float slewTicks = (140f - l.Guns[0].Ballistics.LayToleranceDeg) / l.Guns[0].Ballistics.TrainRateDps / l.TickSeconds;
            Assert.GreaterOrEqual(firstShot.Tick, (long)slewTicks, "it cannot fire before it has trained round");
            Assert.Less(firstShot.Tick, (long)slewTicks + 60, "and fires soon after it is laid (radar and track take a sweep)");
        }

        [Test]
        public void ShipMotion_IsAimError_AndLowersTheKillProbability()
        {
            TruthTarget missile = Target(7, TargetKind.AntiShipMissile, new Vector3(0f, 5f, 9000f), new Vector3(0f, 0f, -290f));
            OwnShipState rolling = Own;
            rolling.RollRateDps = 4f;   // a brisk roll in a seaway
            EngagementEvent calm = Run(Only(L1(), 0), missile, 900, Own).Log.Events.First(e => e.Kind == EventKind.Kill || e.Kind == EventKind.Miss);
            EngagementEvent rough = Run(Only(L1(), 0), missile, 900, rolling).Log.Events.First(e => e.Kind == EventKind.Kill || e.Kind == EventKind.Miss);
            Assert.AreEqual(0f, Num(calm.Text, @"motion ([0-9.]+)"), 1e-3f, calm.Text);
            float range = Num(rough.Text, @"\(([0-9]+) m\)");
            Assert.AreEqual(4f * Mathf.Deg2Rad * Ddg51CombatData.Mk45L1.SolutionLatencyS * range, Num(rough.Text, @"motion ([0-9.]+)"), 0.15f,
                "ω × latency × range (G37)");
            Assert.Less(Num(rough.Text, @"Pk ([0-9.]+) →"), Num(calm.Text, @"Pk ([0-9.]+) →"), $"calm: {calm.Text}\nrolling: {rough.Text}");
        }

        [Test]
        public void Ciws_ClosedLoopSpotting_ShrinksTheBiasBurstByBurst()
        {
            // A sea-skimmer coming up the stern from 2.4 km; no kills, so the CIWS keeps firing on the same track.
            TruthTarget missile = Target(8, TargetKind.AntiShipMissile, new Vector3(0f, 5f, -2400f), new Vector3(0f, 0f, 290f));
            CombatLoadout l = Only(L1(), 1);
            l.Guns[0].Ballistics.PkPerHitAir = 0f;
            CombatSystem cs = Run(l, missile, 400, Own);
            float[] mrad = cs.Log.Events.Where(e => e.Kind == EventKind.BurstFired)
                .Select(e => Num(e.Text, @"spotting bias [0-9.]+ m \(([0-9.]+) mrad\)")).ToArray();
            Assert.GreaterOrEqual(mrad.Length, 3, "several bursts on the one track");
            ShotGroup.SpottingBias(Ddg51CombatData.CiwsL1.SpotBiasMrad, 100 / 4500f * 60f, Ddg51CombatData.CiwsL1.SpotTimeConstantS, out float firstMean, out _);
            Assert.AreEqual(firstMean, mrad[0], 0.001f, "the first burst starts from the full bias (G40)");
            for (int i = 1; i < mrad.Length; i++)
                if (mrad[i - 1] > 0.001f) Assert.Less(mrad[i], mrad[i - 1], $"and each burst after it starts closer: {string.Join(", ", mrad)} mrad");
            Assert.Less(mrad[1], 0.1f * mrad[0], "one burst of spotting takes out most of the bias");
        }

        [Test]
        public void Mk45_BurstsNearAirTargets_ButMustHitSurfaceTargets()
        {
            TruthTarget boat = Target(9, TargetKind.SurfaceShip, new Vector3(0f, 0f, 6000f), new Vector3(0f, 0f, -15f));
            CombatSystem cs = Run(Only(L1(), 0), boat, 600, Own);
            EngagementEvent e = cs.Log.Events.First(x => x.Kind == EventKind.Kill || x.Kind == EventKind.Miss);
            Assert.That(e.Text, Does.Contain("presented radius"), $"impact against a surface target (G39): {e.Text}");
        }

        [Test]
        public void ShotGroup_Math()
        {
            Assert.AreEqual(1f - Mathf.Exp(-0.5f), ShotGroup.RoundHitProbability(1f, 1f, 0f), 1e-6f, "centred: 1 − exp(−r²/2σ²)");
            Assert.AreEqual(1f, ShotGroup.RoundHitProbability(1f, 0f, 0.5f), "no dispersion, inside: a hit");
            Assert.AreEqual(0f, ShotGroup.RoundHitProbability(1f, 0f, 1.5f), "no dispersion, outside: a miss");
            Assert.AreEqual(1f - Mathf.Pow(0.9f, 10), ShotGroup.BurstKillProbability(0.5f, 0.2f, 10), 1e-6f);
            float headOn = ShotGroup.PresentedRadiusM(4.6f, 0.34f, Vector3.forward, Vector3.forward);
            float broadside = ShotGroup.PresentedRadiusM(4.6f, 0.34f, Vector3.forward, Vector3.right);
            Assert.AreEqual(0.17f, headOn, 1e-4f, "head on: the body's disc");
            Assert.AreEqual(Mathf.Sqrt(4.6f * 0.34f / Mathf.PI), broadside, 1e-4f, "broadside: its side");
            ShotGroup.SpottingBias(3f, 1f, 0.5f, out float mean, out float left);
            Assert.AreEqual(3f * (1f - Mathf.Exp(-2f)) * 0.5f, mean, 1e-5f);
            Assert.AreEqual(3f * Mathf.Exp(-2f), left, 1e-5f);
        }

        [Test]
        public void L1_WithoutBallisticsData_RefusesToBuild()
        {
            CombatLoadout l = TestLoadouts.Ddg51L0();
            l.GunsLevel = FidelityLevel.L1;
            Assert.Throws<System.ArgumentException>(() => new CombatSystem(l), "no silent fall back to a level nobody asked for");
        }

        // ── The whole fight ──────────────────────────────────────────────────

        static (ulong fix, ScriptedRaid raid, CombatSystem cs, double ms) Saturation()
        {
            CombatLoadout loadout = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake();
            Assert.AreEqual(FidelityLevel.L1, loadout.GunsLevel, "the range loadout runs guns L1");
            var cs = new CombatSystem(loadout);
            var ai = new Core.Captain.AiCaptainL0(AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset).Bake());
            var raid = new ScriptedRaid(AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(Ddg51CombatData.RaidDir + "/Raid_Saturation.asset").StartingAt(0), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long coreTicks = 0;
            int steps = 0;
            for (int i = 0; i < 12_000; i++)
            {
                ai.Step(cs, Own, Core.Captain.Seat.Tao);
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                long t0 = sw.ElapsedTicks;
                cs.Step(Own, truth, hits, kills);
                coreTicks += sw.ElapsedTicks - t0;
                steps++;
                raid.ApplyKills(kills);
                if (raid.Resolved && cs.Vls.InFlight.Count == 0) break;
            }
            return (cs.Fix, raid, cs, coreTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / steps);
        }

        [Test]
        public void Mk45_StaysOnItsTarget_UntilItIsGone()
        {
            // The Standard raid with the real loadout: the Mk 45 may change target when one is killed, but it must not drop
            // a target and pick it up again moments later (the jitter SIX saw, 2026-10-24: 48 drops, 86 train reversals).
            var cs = new CombatSystem(AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake());
            var ai = new Core.Captain.AiCaptainL0(AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset).Bake());
            var raid = new ScriptedRaid(AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(Ddg51CombatData.RaidDir + "/Raid_Standard.asset").StartingAt(0), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var mk45 = (GunMountL1)cs.Guns.First(g => g.Profile.Name == "Mk 45");
            int last = -1, lost = -1, regains = 0, switches = 0, reversals = 0;
            long lostTick = 0;
            float lastTrain = 0f, lastDelta = 0f;
            for (int i = 0; i < 9000 && !(raid.Resolved && cs.Vls.InFlight.Count == 0); i++)
            {
                ai.Step(cs, Own, Core.Captain.Seat.Tao);
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                cs.Step(Own, truth, hits, kills);
                raid.ApplyKills(kills);

                int now = mk45.TargetTrackId;
                if (now != last)
                {
                    if (now >= 0 && now == lost && cs.Tick - lostTick < 100) regains++;
                    if (last >= 0) { lost = last; lostTick = cs.Tick; }
                    if (last >= 0 && now >= 0) switches++;
                    last = now;
                }
                float d = mk45.TrainDeg - lastTrain;
                if (Mathf.Abs(d) > 1e-3f)
                {
                    if (lastDelta != 0f && Mathf.Sign(d) != Mathf.Sign(lastDelta)) reversals++;
                    lastDelta = d;
                }
                lastTrain = mk45.TrainDeg;
            }
            Debug.Log($"[GQ] Mk 45 through the Standard raid: {switches} target changes, {regains} drop-and-regains, {reversals} train reversals");
            Assert.AreEqual(0, regains, "a target is never dropped and taken up again within 2 s");
            Assert.LessOrEqual(reversals, switches + 2, "the mount only turns back when it changes target");
        }

        [Test]
        public void GunsOnly_AgainstEightSeaSkimmers_L0AndL1_AreMeasured()
        {
            // No ESSM: the guns alone against 8 sea-skimmers, at L0 (a Pk table) and at L1 (ballistics). Reported, not
            // tuned: L1's lethality comes from dispersion and Pk-per-hit numbers that are AUTHORED (G41).
            string Line(FidelityLevel level)
            {
                CombatLoadout l = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake();
                l.GunsLevel = level;
                l.Vls.Loads[0].Rounds = 0;
                l.DefaultRoe = Roe.WeaponsFree;
                var cs = new CombatSystem(l);
                var raid = new ScriptedRaid(TestLoadouts.SeaSkimmerRaid(8, 20_000f), Own);
                var truth = new List<TruthTarget>();
                var hits = new List<ShipHit>();
                var kills = new List<KillReport>();
                for (int i = 0; i < 9000 && !raid.Resolved; i++)
                {
                    hits.Clear();
                    raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                    kills.Clear();
                    cs.Step(Own, truth, hits, kills);
                    raid.ApplyKills(kills);
                }
                Assert.IsTrue(raid.Resolved, $"guns {level}: the raid ends");
                int K(WeaponClass w) => cs.Log.Events.Count(e => e.Kind == EventKind.Kill && e.Weapon == w);
                return $"guns {level}: {raid.Killed} killed, {raid.Leaked} hit (Mk 45 {K(WeaponClass.Mk45)}, CIWS {K(WeaponClass.Ciws)}, Mk 38 {K(WeaponClass.Mk38)}; " +
                       $"{cs.Log.Count(EventKind.BurstFired)} bursts)";
            }
            Debug.Log($"[GQ] guns only vs 8 sea-skimmers from 20 km: {Line(FidelityLevel.L0)} | {Line(FidelityLevel.L1)}");
        }

        [Test]
        public void Saturation_AtGunsL1_Resolves_Repeats_AndStaysInBudget()
        {
            Saturation();   // warm-up: JIT and the firing tables
            var a = Saturation();
            var b = Saturation();
            Assert.AreEqual(a.fix, b.fix, "the same seed and orders give the same Fix");
            Assert.AreEqual(24, a.raid.Killed + a.raid.Leaked, "every threat is accounted for");
            int Kills(WeaponClass w) => a.cs.Log.Events.Count(e => e.Kind == EventKind.Kill && e.Weapon == w);
            Debug.Log($"[GQ] saturation at guns L1: {a.raid.Killed} killed, {a.raid.Leaked} leaked; kills ESSM {Kills(WeaponClass.Essm)}, " +
                      $"Mk 45 {Kills(WeaponClass.Mk45)}, CIWS {Kills(WeaponClass.Ciws)}; core {a.ms:0.000} ms per fixed step (Editor); Fix {a.fix:X16}");
            Assert.LessOrEqual(a.ms, 1.0, "combat-core time per fixed step (G16)");
        }
    }
}
