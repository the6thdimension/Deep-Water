using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RHRadarSuite.Detection;
using RHRadarSuite.Tracking;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Editor;
using DeepWater.GeneralQuarters.ScriptableObjects;
using DeepWater.Sea.ScriptableObjects;
using Debug = UnityEngine.Debug;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// Stream A, Radar L1 + Tracks L1 (G61–G68): the calibration through the public figure, Swerling 1 looks at a fixed Pfa
    /// from the run's seed, one RCS per kind, sea clutter from Sea State's degree, identity from truth, the track file in the
    /// CIC, and whole raids that repeat their Fix and fit the budget. Measurements carry a [GQ] tag.
    /// </summary>
    public class RadarL1Tests
    {
        const double Pfa = 1e-6;
        const string SeaLibrary = "Assets/Sea State/ScriptableObjects/Profiles/SeaStateLibrary.asset";
        static readonly OwnShipState Own = new OwnShipState { Position = Vector3.zero, Velocity = Vector3.zero, HeadingDeg = 0f };

        static RadarProfileData Profile(float clutterImprovementDb = 30f)
        {
            RadarL1Data d = Ddg51CombatData.RadarL1();
            d.ClutterImprovementDb = clutterImprovementDb;
            return new RadarProfileData { InstrumentedRangeM = 185_000f, AntennaHeightM = 20f, SweepPeriodTicks = 25, L1 = d };
        }

        static TruthTarget Target(int id, TargetKind kind, Vector3 position, Vector3 velocity = default) =>
            new TruthTarget { Id = id, Kind = kind, Identity = Identity.Hostile, Position = position, Velocity = velocity, Alive = true };

        /// <summary>Sea State's own wind for a Douglas degree (its NATO STANAG 4194 mean), as the range feeds it.</summary>
        static SeaEnvironment SeaOf(int douglas)
        {
            var library = AssetDatabase.LoadAssetAtPath<SeaStateLibrarySO>(SeaLibrary);
            Assert.IsNotNull(library, SeaLibrary);
            return new SeaEnvironment { DouglasSea = douglas, WindSpeedMps = library.GetSea(douglas).WindSpeedMs };
        }

        /// <summary>What one look would give, without spending the run's draws (a throwaway generator).</summary>
        static LookResult Predict(RadarL1 radar, in SeaEnvironment sea, Vector3 at, TargetKind kind) =>
            RadarLook.Look(radar.Terms, radar.Measurement, radar.Coverage, new SurfaceTerms { GammaDb = radar.SeaGammaDb(sea) }, Pfa,
                radar.Antenna(Own), new Plot(at.x, at.y, at.z), radar.Data.RcsDbsm(kind), new System.Random(1));

        // ── The radar ───────────────────────────────────────────────────────

        [Test]
        public void Calibration_PutsTheReferenceTargetAtItsPd_AtThePublicRange()
        {
            var radar = new RadarL1(Profile(), new DeterministicRng(1));
            RadarTerms r = radar.Terms;
            Assert.AreEqual(12.7719, radar.RequiredSnrDb, 1e-4, "Swerling 1, Pd 0.5 at Pfa 1e-6 (by hand)");
            Assert.AreEqual(radar.RequiredSnrDb, RadarEquation.SnrDb(r, 0.0, 324_100.0), 1e-9, "the equation passes through 175 nmi");
            Assert.AreEqual(0.5, RadarEquation.PdSwerling1(RadarEquation.SnrDb(r, 0.0, 324_100.0), Pfa), 1e-9);
            // What that means for each kind (G65): a −10 dBsm missile at Pd 0.5 is 324.1 km × 10^(−10/40) = 182.25 km away.
            Assert.AreEqual(182_254.8, RadarEquation.RangeForSnrM(r, -10.0, radar.RequiredSnrDb), 1.0);
            Debug.Log($"[GQ] Radar L1 calibrated: peak power {r.PeakPowerW / 1e3:0.0} kW (solved, not authored); Pd 0.5 at " +
                      $"{RadarEquation.RangeForSnrM(r, 0.0, radar.RequiredSnrDb) / 1000:0.0} km for 0 dBsm, " +
                      $"{RadarEquation.RangeForSnrM(r, 5.0, radar.RequiredSnrDb) / 1000:0.0} km for an aircraft (5 dBsm), " +
                      $"{RadarEquation.RangeForSnrM(r, -10.0, radar.RequiredSnrDb) / 1000:0.0} km for a missile (−10 dBsm)");
        }

        [Test]
        public void SeededLooks_DetectAsSwerling1Predicts_AndSpendTheRunsDraws()
        {
            // A −10 dBsm missile at its Pd 0.5 range, 3 km up: above the horizon and the clutter, so Pd is the equation's alone.
            double slant = 182_254.8;
            float y = 3000f;
            float ground = (float)System.Math.Sqrt(slant * slant - (y - 20.0) * (y - 20.0));
            var world = new List<TruthTarget> { Target(1, TargetKind.AntiShipMissile, new Vector3(0f, y, ground)) };
            var rng = new DeterministicRng(1442);
            var radar = new RadarL1(Profile(), rng);
            var seen = new List<Detection>();
            const int looks = 4000;
            for (int i = 0; i < looks; i++) radar.Sweep(Own, new SeaEnvironment(), world, seen);
            double sigma = System.Math.Sqrt(looks * 0.25);
            Debug.Log($"[GQ] {looks} seeded looks at the Pd 0.5 range: {seen.Count} detections ({seen.Count / (double)looks:0.000}); 3σ is ±{3 * sigma / looks:0.000}");
            Assert.AreEqual(looks * 0.5, seen.Count, 3 * sigma, "Swerling 1 at Pfa 1e-6 gives Pd 0.5 here");
            Assert.AreEqual(looks + 6L * seen.Count, rng.Draws, "one draw per look, six more (three Gaussian errors) per detection");

            var again = new List<Detection>();
            var radar2 = new RadarL1(Profile(), new DeterministicRng(1442));
            for (int i = 0; i < looks; i++) radar2.Sweep(Own, new SeaEnvironment(), world, again);
            CollectionAssert.AreEqual(seen.Select(d => d.Position).ToArray(), again.Select(d => d.Position).ToArray(), "the same seed replays every look");
            Assert.AreEqual(seen[0].Chance.Seed, 1442UL);
        }

        [Test]
        public void OneRcsPerKind_AndNoDrawIsSpent_BeyondInstrumentedRangeOrTheHorizon()
        {
            var radar = new RadarL1(Profile(), new DeterministicRng(1));
            var at = new Vector3(0f, 3000f, 60_000f);
            double aircraft = Predict(radar, new SeaEnvironment(), at, TargetKind.Aircraft).SnrDb;
            double missile = Predict(radar, new SeaEnvironment(), at, TargetKind.AntiShipMissile).SnrDb;
            double ship = Predict(radar, new SeaEnvironment(), at, TargetKind.SurfaceShip).SnrDb;
            Assert.AreEqual(15.0, aircraft - missile, 1e-9, "5 dBsm against −10 dBsm (G65)");
            Assert.AreEqual(25.0, ship - aircraft, 1e-9, "30 dBsm against 5 dBsm");

            var rng = new DeterministicRng(1);
            var counted = new RadarL1(Profile(), rng);
            var world = new List<TruthTarget>
            {
                Target(1, TargetKind.Aircraft, new Vector3(0f, 12_000f, 190_000f)),         // beyond the 185 km instrumented range
                Target(2, TargetKind.AntiShipMissile, new Vector3(0f, 5f, 30_000f)),        // below the horizon (27.6 km for 5 m)
                new TruthTarget { Id = 3, Kind = TargetKind.Aircraft, Position = new Vector3(0f, 3000f, 20_000f), Alive = false },   // dead
            };
            var seen = new List<Detection>();
            counted.Sweep(Own, new SeaEnvironment(), world, seen);
            Assert.IsEmpty(seen);
            Assert.AreEqual(0, rng.Draws, "nothing to look at costs no luck");
        }

        [Test]
        public void SeaClutter_FollowsSeaStatesDegree_AndHidesASeaSkimmerOnlyInsideTheClutterHorizon()
        {
            var radar = new RadarL1(Profile(), new DeterministicRng(1));
            var raw = new RadarL1(Profile(clutterImprovementDb: 0f), new DeterministicRng(1));
            // γ = −64 + 6·K_B − 10·log λ at 3 GHz, K_B from Sea State's wind for the degree (worked by hand in double precision).
            double[] gamma = { -53.997, -44.968, -35.918, -29.386, -23.089, -17.380, -5.364 };
            for (int d = 0; d <= 6; d++) Assert.AreEqual(gamma[d], radar.SeaGammaDb(SeaOf(d)), 0.01, $"Douglas {d}");

            var near = new Vector3(0f, 5f, 10_000f);    // a sea-skimmer inside the 18.4 km clutter horizon of a 20 m mast
            var far = new Vector3(0f, 5f, 22_000f);     // outside it, still above the radar horizon
            var line = new System.Text.StringBuilder("[GQ] sea-skimmer (−10 dBsm, 5 m) Pd per look at 10 km, by Douglas degree: ");
            for (int d = 0; d <= 6; d++)
                line.Append($"{d}: {Predict(radar, SeaOf(d), near, TargetKind.AntiShipMissile).Pd:0.000} (γ only {Predict(raw, SeaOf(d), near, TargetKind.AntiShipMissile).Pd:0.000})  ");
            Debug.Log(line.Append("; in brackets with no clutter improvement (G67 to the letter)").ToString());

            Assert.Greater(Predict(radar, SeaOf(0), near, TargetKind.AntiShipMissile).Pd, 0.99, "a calm sea hides nothing");
            Assert.Greater(Predict(radar, SeaOf(4), near, TargetKind.AntiShipMissile).Pd, 0.9, "the range's sea (Douglas 4) with 30 dB of clutter improvement");
            Assert.Less(Predict(raw, SeaOf(4), near, TargetKind.AntiShipMissile).Pd, 0.01, "γ alone at Douglas 4 buries it: why G70 is proposed");
            Assert.Less(Predict(radar, SeaOf(6), near, TargetKind.AntiShipMissile).Pd, 0.2, "a very rough sea still hides it");
            Assert.AreEqual(Predict(radar, SeaOf(0), far, TargetKind.AntiShipMissile).Pd, Predict(raw, SeaOf(6), far, TargetKind.AntiShipMissile).Pd, 0.0,
                "beyond the clutter horizon the sea does not matter");
        }

        [Test]
        public void Plots_AreNoisy_AndCarryTruthsIdentity()
        {
            var radar = new RadarL1(Profile(), new DeterministicRng(7));
            var world = new List<TruthTarget> { Target(9, TargetKind.AntiShipMissile, new Vector3(0f, 5f, 20_000f), new Vector3(0f, 0f, -290f)) };
            var seen = new List<Detection>();
            for (int i = 0; i < 500; i++) radar.Sweep(Own, new SeaEnvironment(), world, seen);
            Assert.Greater(seen.Count, 490, "a −10 dBsm missile at 20 km in a calm sea: Pd ≈ 1");
            Assert.IsTrue(seen.All(d => d.TargetId == 9 && d.Kind == TargetKind.AntiShipMissile && d.Identity == Identity.Hostile), "identity from truth (G68)");
            Assert.IsTrue(seen.All(d => d.Velocity == Vector3.zero), "the radar measures position only; Tracks L1 estimates velocity");
            double sx = System.Math.Sqrt(seen.Average(d => (double)d.Position.x * d.Position.x));
            double sz = System.Math.Sqrt(seen.Average(d => ((double)d.Position.z - 20_000.0) * ((double)d.Position.z - 20_000.0)));
            // Strong target: the floors rule. Cross-range ≈ 20 km × 0.05° = 17.5 m; down-range ≈ the 5 m floor.
            Debug.Log($"[GQ] plot scatter at 20 km: cross-range {sx:0.0} m, down-range {sz:0.0} m (predicted about 17.5 and 5.0)");
            Assert.AreEqual(17.5, sx, 2.5);
            Assert.AreEqual(5.0, sz, 1.0);
            Assert.Greater(sx, 0.0, "never truth");
        }

        [Test]
        public void L1_WithoutItsProfileData_RefusesToBuild()
        {
            CombatLoadout l = TestLoadouts.Ddg51L0();
            l.RadarLevel = FidelityLevel.L1;
            Assert.Throws<System.ArgumentException>(() => new CombatSystem(l), "Radar L1 with no data: no silent fall back");
            l = TestLoadouts.Ddg51L0();
            l.TracksLevel = FidelityLevel.L1;
            Assert.Throws<System.ArgumentException>(() => new CombatSystem(l), "Tracks L1 with no data");
            var cs = new CombatSystem(TestLoadouts.Ddg51L1());
            Assert.AreEqual(FidelityLevel.L1, cs.Radar.Level);
            Assert.AreEqual(FidelityLevel.L1, cs.Tracks.Level);
        }

        [Test]
        public void SeededRandom_DrawsOnlyFromTheRunsGenerator()
        {
            var a = new DeterministicRng(99);
            var b = new DeterministicRng(99);
            var r = new SeededRandom(a);
            for (int i = 0; i < 100; i++)
            {
                double u = r.NextDouble();
                Assert.AreEqual(b.NextDouble01(), u, 0.0);
                Assert.That(u, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
            Assert.AreEqual(100, a.Draws, "every draw is counted, so the Fix sees it");
            Assert.That(r.Next(10), Is.InRange(0, 9));
            Assert.AreEqual(101, a.Draws);
        }

        // ── Tracks L1 in the CIC ─────────────────────────────────────────────

        /// <summary>A closing sea-skimmer and no weapons, so nothing stops it: what the CIC believes, look by look.</summary>
        static CombatSystem Closing(out List<TruthTarget> world, out System.Func<long, Vector3> truthAt)
        {
            CombatLoadout l = TestLoadouts.Ddg51L1();
            l.Vls.Loads[0].Rounds = 0;
            l.Guns = new GunMountProfileData[0];
            var cs = new CombatSystem(l);
            var start = new Vector3(0f, 5f, 25_000f);
            var v = new Vector3(0f, 0f, -290f);
            truthAt = tick => start + v * (tick * l.TickSeconds);
            world = new List<TruthTarget> { Target(5, TargetKind.AntiShipMissile, start, v) };
            return cs;
        }

        static void Step(CombatSystem cs, List<TruthTarget> world, System.Func<long, Vector3> truthAt)
        {
            TruthTarget t = world[0];
            t.Position = truthAt(cs.Tick);
            world[0] = t;
            cs.Step(Own, world, new List<ShipHit>(), new List<KillReport>());
        }

        [Test]
        public void TracksL1_ConfirmTwoOfThree_HoldAClosingMissile_AndFireControlReadsTheFilterNotTruth()
        {
            CombatSystem cs = Closing(out List<TruthTarget> world, out System.Func<long, Vector3> truthAt);
            double sumSq = 0.0, maxPos = 0.0, maxVel = 0.0;
            int looks = 0;
            for (int i = 0; i < 3000; i++)   // 60 s: 25 km in to 7.6 km
            {
                long tick = cs.Tick;
                Step(cs, world, truthAt);
                if (tick % 25 != 0 || cs.Tracks.Tracks.Count == 0) continue;
                Track t = cs.Tracks.Tracks[0];
                looks++;
                if (looks < 10) continue;   // let the filter settle
                double pos = (t.Position - truthAt(tick)).magnitude, vel = (t.Velocity - new Vector3(0f, 0f, -290f)).magnitude;
                sumSq += pos * pos;
                maxPos = System.Math.Max(maxPos, pos);
                maxVel = System.Math.Max(maxVel, vel);
            }
            EngagementEvent created = cs.Log.Events.Single(e => e.Kind == EventKind.TrackCreated);
            Track track = cs.Tracks.Tracks.Single();
            double rms = System.Math.Sqrt(sumSq / (looks - 9));
            Debug.Log($"[GQ] Tracks L1 on a closing sea-skimmer: {created.Text} at tick {created.Tick}; after settling, position error {rms:0.0} m rms, " +
                      $"{maxPos:0.0} m worst, velocity error {maxVel:0.0} m/s worst; quality {track.Quality:0.00}");
            Assert.LessOrEqual(created.Tick, 50, "confirmed on 2 of its first 3 looks (looks every 25 ticks)");
            Assert.AreEqual(RootKind.Chance, created.Root, "a track names the detection draw that confirmed it");
            Assert.IsTrue(cs.Log.IsComplete(created.Id));
            Assert.AreEqual(5, track.TruthId);
            Assert.AreEqual(Identity.Hostile, track.Identity, "identity from truth (G68)");
            Assert.AreEqual(0, cs.Log.Count(EventKind.TrackDropped), "held all the way in");
            Assert.Greater(rms, 0.5, "the CIC reads its filter, never truth");
            Assert.Less(maxPos, 60.0, "within a few sigma of the plots' noise");
            Assert.Less(maxVel, 25.0);
            Assert.GreaterOrEqual(track.Quality, 2f / 3f, "plots on most of its last 3 looks");
        }

        [Test]
        public void TracksL1_WithTheRadarOffline_CoastOnThePrediction_ThenDrop()
        {
            CombatSystem cs = Closing(out List<TruthTarget> world, out System.Func<long, Vector3> truthAt);
            for (int i = 0; i < 26 * 10; i++) Step(cs, world, truthAt);   // ten looks: confirmed and settled
            cs.Radar.Online = false;
            for (int i = 0; i < 25 * 3 + 1; i++) Step(cs, world, truthAt);   // three empty looks
            Track coasting = cs.Tracks.Tracks.Single();
            Assert.IsTrue(coasting.Active && coasting.Coasting, "missed looks: it coasts and stays engageable");
            Assert.Less((coasting.Position - truthAt(coasting.LastTick)).magnitude, 80f, "flown on along its velocity");
            for (int i = 0; i < 25; i++) Step(cs, world, truthAt);   // the fourth
            Track dropped = cs.Tracks.Tracks.Single();
            Assert.IsFalse(dropped.Active, "dropped after 4 looks without a plot");
            EngagementEvent e = cs.Log.Events.Single(x => x.Kind == EventKind.TrackDropped);
            StringAssert.Contains("no plot for 4 looks", e.Text);
            Assert.IsTrue(cs.Log.IsComplete(e.Id));
        }

        // ── Whole raids ──────────────────────────────────────────────────────

        static (List<ulong> fixes, CombatSystem cs, ScriptedRaid raid, double msPerTick) Raid(CombatLoadout loadout, List<RaidThreatSpec> threats,
            SeaEnvironment sea, int maxTicks = 12_000)
        {
            var cs = new CombatSystem(loadout) { Sea = sea };
            var raid = new ScriptedRaid(threats, Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var fixes = new List<ulong>();
            var watch = new Stopwatch();
            for (int i = 0; i < maxTicks; i++)
            {
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                watch.Start();
                cs.Step(Own, truth, hits, kills);
                watch.Stop();
                raid.ApplyKills(kills);
                fixes.Add(cs.Fix);
                if (raid.Resolved && cs.Vls.InFlight.Count == 0) break;
            }
            return (fixes, cs, raid, watch.Elapsed.TotalMilliseconds / fixes.Count);
        }

        [Test]
        public void RaidL1_SameSeedSameFix_EveryTick_AndEveryKillTracesToADetection()
        {
            var (fixesA, cs, raid, ms) = Raid(TestLoadouts.Ddg51L1(1442), TestLoadouts.SeaSkimmerRaid(8), SeaOf(4));
            var (fixesB, _, _, _) = Raid(TestLoadouts.Ddg51L1(1442), TestLoadouts.SeaSkimmerRaid(8), SeaOf(4));
            var (fixesC, _, _, _) = Raid(TestLoadouts.Ddg51L1(7), TestLoadouts.SeaSkimmerRaid(8), SeaOf(4));
            var (_, l0, raidL0, _) = Raid(TestLoadouts.Ddg51L0(1442), TestLoadouts.SeaSkimmerRaid(8), SeaOf(4));

            Debug.Log($"[GQ] raid at Radar/Tracks L1, 8 sea-skimmers from 40 km, Douglas 4: {raid.Killed} killed, {raid.Leaked} hit; " +
                      $"{cs.Log.Count(EventKind.TrackCreated)} tracks made, {cs.Log.Count(EventKind.TrackDropped)} dropped, {cs.Log.Count(EventKind.Launched)} ESSM; " +
                      $"{fixesA.Count} ticks, {ms:0.000} ms/tick (Editor); Fix {fixesA.Last():X16}. At L0, same seed: {raidL0.Killed} killed, {raidL0.Leaked} hit, " +
                      $"{l0.Log.Count(EventKind.Launched)} ESSM");
            Assert.IsTrue(raid.Resolved);
            Assert.AreEqual(8, raid.Killed + raid.Leaked);
            CollectionAssert.AreEqual(fixesA, fixesB, "same seed, same Orders, same truth: the same Fix on every tick, every look replayed");
            Assert.AreNotEqual(fixesA.Last(), fixesC.Last(), "another seed: other looks, other luck");
            foreach (EngagementEvent e in cs.Log.Events.Where(e => e.Kind == EventKind.Kill))
            {
                Assert.IsTrue(cs.Log.IsComplete(e.Id), $"kill {e.Id} has a chain ending in roots: {e.Text}");
                Assert.IsTrue(cs.Log.Chain(e.Id).Any(c => c.Kind == EventKind.TrackCreated && c.Root == RootKind.Chance),
                    "every kill traces to a track and the detection draw that confirmed it");
            }
        }

        [Test]
        public void EssmLaunch_OnAClosingSeaSkimmer_AtL0AndL1_IsMeasured()
        {
            // RangeL0Tests' T1 scenario (one sea-skimmer at 20 km dead ahead, the AI as TAO), but the missile flies: when the
            // track is made, when TEWA proposes ESSM, when the AI confirms (T1) and when the missile leaves, at both levels.
            string Times(FidelityLevel level)
            {
                CombatLoadout l = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake();
                l.RadarLevel = level;
                l.TracksLevel = level;
                var cs = new CombatSystem(l);
                var ai = new Core.Captain.AiCaptainL0(AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset).Bake());
                var world = new List<TruthTarget> { Target(1, TargetKind.AntiShipMissile, new Vector3(0f, 5f, 20_000f), new Vector3(0f, 0f, -290f)) };
                for (int i = 0; i < 1500; i++)
                {
                    TruthTarget m = world[0];
                    m.Position = new Vector3(0f, 5f, 20_000f - 290f * i * l.TickSeconds);
                    world[0] = m;
                    ai.Step(cs, Own, Core.Captain.Seat.Tao);
                    cs.Step(Own, world, new List<ShipHit>(), new List<KillReport>());
                }
                string At(System.Func<EngagementEvent, bool> which)
                {
                    EngagementEvent e = cs.Log.Events.FirstOrDefault(which);
                    return e.Text == null ? "never" : $"{e.Tick * l.TickSeconds:0.0} s";
                }
                string launched = At(e => e.Kind == EventKind.Launched);
                Assert.AreNotEqual("never", launched, $"{level}: an ESSM leaves within 30 s");
                return $"{level}: track {At(e => e.Kind == EventKind.TrackCreated)}, proposed {At(e => e.Kind == EventKind.Proposed)}, " +
                       $"confirmed {At(e => e.Kind == EventKind.Decision && e.Rule.Contains("T1"))}, launched {launched}";
            }
            Debug.Log($"[GQ] one sea-skimmer at 20 km, AI as TAO: {Times(FidelityLevel.L0)} | {Times(FidelityLevel.L1)}");
        }

        [Test]
        public void Mk45_LaysOnL1Tracks_AndTheirNoiseMovesTheLay_IsMeasured()
        {
            // Guns L1 lays on the track as it is (G38: a constant-velocity lead), so at Tracks L1 each look's new position and
            // velocity moves the solution. Smoothing that is Guns L2's item (fire control with track noise). Measured, not tuned;
            // GunsL1Tests pins the target-holding rule at Tracks L0.
            var cs = new CombatSystem(AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake());
            var ai = new Core.Captain.AiCaptainL0(AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset).Bake());
            var raid = new ScriptedRaid(AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(Ddg51CombatData.RaidDir + "/Raid_Standard.asset").StartingAt(0), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var mk45 = (GunMountL1)cs.Guns.First(g => g.Profile.Name == "Mk 45");
            int last = -1, lost = -1, regains = 0, switches = 0, reversals = 0;
            long lostTick = 0;
            float lastTrain = 0f, lastDelta = 0f, swingStart = 0f;
            var swings = new List<float>();
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
                    if (lastDelta != 0f && Mathf.Sign(d) != Mathf.Sign(lastDelta))
                    {
                        reversals++;
                        swings.Add(Mathf.Abs(lastTrain - swingStart));
                        swingStart = lastTrain;
                    }
                    lastDelta = d;
                }
                lastTrain = mk45.TrainDeg;
            }
            swings.Sort();
            float median = swings.Count > 0 ? swings[swings.Count / 2] : 0f;
            int small = swings.Count(x => x < 1f);
            Debug.Log($"[GQ] Mk 45 on Tracks L1 through the Standard raid: {switches} target changes, {regains} drop-and-regains, {reversals} train reversals; " +
                      $"the swing between reversals has median {median:0.00}°, {small} of {swings.Count} under 1° (the rest are re-targeting slews); " +
                      $"{raid.Killed} killed, {raid.Leaked} hit in {cs.Tick * cs.TickSeconds:0} s");
            Assert.Greater(switches, 0, "the mount worked the raid");
        }

        static (ulong fix, ScriptedRaid raid, CombatSystem cs, double ms) Saturation(SeaEnvironment sea, float clutterImprovementDb = 30f)
        {
            CombatLoadout loadout = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake();
            Assert.AreEqual(FidelityLevel.L1, loadout.RadarLevel, "the range loadout runs Radar L1");
            Assert.AreEqual(FidelityLevel.L1, loadout.TracksLevel, "and Tracks L1");
            loadout.Radar.L1.ClutterImprovementDb = clutterImprovementDb;
            var cs = new CombatSystem(loadout) { Sea = sea };
            var ai = new Core.Captain.AiCaptainL0(AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset).Bake());
            var raid = new ScriptedRaid(AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(Ddg51CombatData.RaidDir + "/Raid_Saturation.asset").StartingAt(0), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var sw = Stopwatch.StartNew();
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
            return (cs.Fix, raid, cs, coreTicks * 1000.0 / Stopwatch.Frequency / steps);
        }

        [Test]
        public void Saturation_OnTheRangeLoadout_InTheRangesSea_Repeats_IsMeasured_AndStaysInBudget()
        {
            SeaEnvironment sea = SeaOf(4);
            Saturation(sea);   // warm-up
            var a = Saturation(sea);
            var b = Saturation(sea);
            var calm = Saturation(SeaOf(0));
            var letter = Saturation(sea, clutterImprovementDb: 0f);
            int K(CombatSystem cs, WeaponClass w) => cs.Log.Events.Count(e => e.Kind == EventKind.Kill && e.Weapon == w);
            string Line(string name, (ulong fix, ScriptedRaid raid, CombatSystem cs, double ms) r) =>
                $"{name}: {r.raid.Killed} killed, {r.raid.Leaked} leaked (ESSM {K(r.cs, WeaponClass.Essm)}, Mk 45 {K(r.cs, WeaponClass.Mk45)}, CIWS {K(r.cs, WeaponClass.Ciws)}; " +
                $"{r.cs.Log.Count(EventKind.Launched)} ESSM launched, {r.cs.Log.Count(EventKind.TrackCreated)} tracks made, {r.cs.Log.Count(EventKind.TrackDropped)} dropped; " +
                $"core {r.ms:0.000} ms per fixed step)";
            Debug.Log($"[GQ] saturation (24) at Radar/Tracks L1, range loadout, AI as TAO. {Line("Douglas 4", a)} | {Line("calm", calm)} | " +
                      $"{Line("Douglas 4 with no clutter improvement (G67 to the letter)", letter)}. Fix {a.fix:X16} (Editor timings)");
            Assert.AreEqual(a.fix, b.fix, "the same seed and orders give the same Fix");
            Assert.AreEqual(24, a.raid.Killed + a.raid.Leaked, "every threat is accounted for");
            Assert.AreEqual(24, letter.raid.Killed + letter.raid.Leaked);
            Assert.LessOrEqual(a.ms, 1.0, "combat-core time per fixed step (G16)");
        }
    }
}
