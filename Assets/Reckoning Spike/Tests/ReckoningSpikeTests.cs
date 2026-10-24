using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using DeepWater.Sea.Core;
using Debug = UnityEngine.Debug;

namespace DeepWater.Reckoning.Spike.Tests
{
    /// <summary>
    /// The spike's four pass criteria, for both variants. Every measurement is logged with a
    /// [SPIKE] tag so the report quotes the run, not a memory of it.
    /// </summary>
    public class ReckoningSpikeTests
    {
        const float Gravity = SpikeRunner.Gravity;

        // ── Seas and hulls ─────────────────────────────────────────────────────

        static SeaBakeInput Calm() => new SeaBakeInput
        {
            WindSea = default, Swell = default, MinWavelengthM = 10f, Gravity = Gravity,
            WhitecapFraction = 0f, FoamCalibrationGrid = 32,
        };

        /// <summary>Douglas 6 (Hs 5 m) plus a 3 m, 150 m swell — the sea suite's own rough fixture.</summary>
        static SeaBakeInput Rough() => new SeaBakeInput
        {
            WindSea = new WaveSystemSpec
            {
                SignificantHeightM = 5f, PeakPeriodS = (float)WaveSpectrum.PiersonMoskowitzPeakPeriod(5f, Gravity),
                Gamma = 1f, SpreadingS = 10f, DirectionRad = 0.3f, Choppiness = 1f, ComponentCount = 48, Seed = 1006u,
            },
            Swell = new WaveSystemSpec
            {
                SignificantHeightM = 3f, PeakPeriodS = (float)System.Math.Sqrt(2.0 * System.Math.PI * 150.0 / Gravity),
                Gamma = 3.3f, SpreadingS = 50f, DirectionRad = -1.2f, Choppiness = 1f, ComponentCount = 16, Seed = 2004u,
            },
            MinWavelengthM = 10f, Gravity = Gravity, WhitecapFraction = 0.09f, FoamCalibrationGrid = 32,
        };

        static SpikeSpawn Destroyer(float2 at, float heading = 0f, float heel = 0f) => new SpikeSpawn
        { LengthM = 154f, BeamM = 20f, HeightM = 12f, DraftM = 6.3f, ProbesAcross = 9, ProbesAlong = 24, PositionXZ = at, HeadingDeg = heading, HeelDeg = heel };
        static SpikeSpawn Frigate(float2 at) => new SpikeSpawn
        { LengthM = 120f, BeamM = 16f, HeightM = 10f, DraftM = 5f, ProbesAcross = 7, ProbesAlong = 20, PositionXZ = at };
        static SpikeSpawn Patrol(float2 at) => new SpikeSpawn
        { LengthM = 40f, BeamM = 8f, HeightM = 4f, DraftM = 1.8f, ProbesAcross = 4, ProbesAlong = 10, PositionXZ = at };
        static SpikeSpawn Rhib(float2 at, float heading, float3 velocity) => new SpikeSpawn
        { LengthM = 11f, BeamM = 3.5f, HeightM = 1.4f, DraftM = 0.45f, ProbesAcross = 3, ProbesAlong = 6, PositionXZ = at, HeadingDeg = heading, Velocity = velocity };

        // ── Criterion 1: same Orders, same Fix, through a collision ────────────

        [TestCase(SpikeVariant.A)]
        [TestCase(SpikeVariant.B)]
        public void Criterion1_SameOrdersGiveTheSameFix_ThroughACollision(SpikeVariant variant)
        {
            const int Ticks = 10000;
            SpikeSpawn[] spawns =
            {
                Destroyer(new float2(0f, 0f)),
                Rhib(new float2(40f, 0f), 270f, new float3(-6f, 0f, 0f)),   // runs into the destroyer's beam
            };
            ulong[] first = Run(variant, spawns, Ticks, out int contactsA, out float penA, out string name);
            ulong[] second = Run(variant, spawns, Ticks, out int contactsB, out float penB, out _);

            int diverged = -1;
            for (int t = 0; t < Ticks; t++) if (first[t] != second[t]) { diverged = t; break; }
            Debug.Log($"[SPIKE] C1 {name}: {Ticks} ticks × 2 runs, rough sea; contact ticks {contactsA}/{contactsB}, " +
                      $"max overlap {penA:0.000}/{penB:0.000} m; final Fix {first[Ticks - 1]:X16} / {second[Ticks - 1]:X16}; " +
                      (diverged < 0 ? "identical on every tick" : $"FIRST DIVERGENCE at tick {diverged}"));
            Assert.Greater(contactsA, 0, "the RHIB never reached the destroyer — no collision was tested");
            Assert.AreEqual(-1, diverged, $"runs diverged at tick {diverged}");
        }

        static ulong[] Run(SpikeVariant variant, SpikeSpawn[] spawns, int ticks, out int contacts, out float penetration, out string name)
        {
            var fixes = new ulong[ticks];
            using (var r = new SpikeRunner(variant, Rough(), spawns))
            {
                for (int t = 0; t < ticks; t++) { r.Step(); fixes[t] = r.RunningFix; }
                contacts = r.World.ContactTicks;
                penetration = r.World.MaxPenetrationM;
                name = r.World.Name;
            }
            return fixes;
        }

        // ── Criterion 2: natural roll period within 10 % of the IMO formula ────

        [TestCase(SpikeVariant.A)]
        [TestCase(SpikeVariant.B)]
        public void Criterion2_RollPeriod_WithinTenPercentOfImo(SpikeVariant variant)
        {
            using (var r = new SpikeRunner(variant, Calm(), new[] { Destroyer(float2.zero, 0f, 5f) }))
            {
                SpikeHull hull = r.Hulls[0];
                var crossings = new List<double>();
                var heels = new List<float>();
                float previous = r.HeelDeg(0);
                for (int t = 1; t <= 4000; t++)   // 80 s
                {
                    r.Step();
                    float heel = r.HeelDeg(0);
                    if (previous < 0f && heel >= 0f)
                        crossings.Add((t - 1 + previous / (previous - heel)) * SpikeRunner.Dt);   // interpolated upward crossing
                    if (t % 50 == 0) heels.Add(heel);
                    previous = heel;
                }
                Assert.GreaterOrEqual(crossings.Count, 3, $"too few roll cycles to measure: {crossings.Count}");
                var periods = new List<double>();
                for (int i = 1; i < crossings.Count; i++) periods.Add(crossings[i] - crossings[i - 1]);
                double measured = periods.Average();
                double imo = hull.ImoRollPeriodS;
                double error = (measured - imo) / imo;

                float rollAdded = variant == SpikeVariant.A ? hull.MeanAddedInertia : hull.AddedInertiaFraction.z;
                double model = 2.0 * math.PI * math.sqrt(hull.Inertia.z * (1f + rollAdded) / (hull.MassKg * Gravity * hull.MetacentricHeight));
                Debug.Log($"[SPIKE] C2 {r.World.Name}: measured roll period {measured:0.00} s over {periods.Count} cycles " +
                          $"(spread {periods.Min():0.00}–{periods.Max():0.00}); IMO {imo:0.00} s (GM {hull.MetacentricHeight:0.00} m) → error {error * 100.0:+0.0;-0.0} %; " +
                          $"ideal box with roll added inertia {rollAdded:0.00}: {model:0.00} s; heel every 1 s: {string.Join(" ", heels.Take(20).Select(h => h.ToString("0.0")))}");
                Assert.LessOrEqual(math.abs(error), 0.10, $"roll period {measured:0.00} s vs IMO {imo:0.00} s");
            }
        }

        // ── Finding: Sea State's buoyancy application point ───────────────────

        [Test]
        public void Finding_SeaStateApplicationPoint_RollsTooFast()
        {
            double corrected = MeasureRollPeriod(false), seaState = MeasureRollPeriod(true);
            double imo;
            using (var r = new SpikeRunner(SpikeVariant.B, Calm(), new[] { Destroyer(float2.zero) })) imo = r.Hulls[0].ImoRollPeriodS;
            Debug.Log($"[SPIKE] F1 154 m box, variant B, calm: roll period with force at the submerged centroid {corrected:0.00} s; " +
                      $"at the column centre (Sea State today) {seaState:0.00} s; IMO {imo:0.00} s → Sea State rolls " +
                      $"{(corrected / seaState - 1.0) * 100.0:0} % faster than the corrected model");
            Assert.Less(seaState, 0.8 * corrected, "expected the column-centre model to roll markedly faster");
        }

        static double MeasureRollPeriod(bool seaStateApplicationPoint)
        {
            using (var r = new SpikeRunner(SpikeVariant.B, Calm(), new[] { Destroyer(float2.zero, 0f, 5f) }))
            {
                r.SeaStateApplicationPoint = seaStateApplicationPoint;
                var crossings = new List<double>();
                float previous = r.HeelDeg(0);
                for (int t = 1; t <= 4000; t++)
                {
                    r.Step();
                    float heel = r.HeelDeg(0);
                    if (previous < 0f && heel >= 0f) crossings.Add((t - 1 + previous / (previous - heel)) * SpikeRunner.Dt);
                    previous = heel;
                }
                return (crossings[crossings.Count - 1] - crossings[0]) / (crossings.Count - 1);
            }
        }

        // ── Criterion 3: tick cost at sandbox scale ────────────────────────────

        [TestCase(SpikeVariant.A)]
        [TestCase(SpikeVariant.B)]
        public void Criterion3_TickCost_AtSandboxScale(SpikeVariant variant)
        {
            SpikeSpawn[] spawns =
            {
                Destroyer(new float2(0f, 0f)), Frigate(new float2(400f, 300f)),
                Patrol(new float2(-350f, 200f)), Rhib(new float2(150f, -400f), 45f, float3.zero),
            };
            using (var r = new SpikeRunner(variant, Rough(), spawns))
            {
                for (int t = 0; t < 200; t++) r.Step();   // warm-up (Burst, caches)
                const int Ticks = 2000;
                var watch = Stopwatch.StartNew();
                for (int t = 0; t < Ticks; t++) r.Step();
                watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds / Ticks;
                int probes = Enumerable.Range(0, r.Hulls.Length).Sum(i => r.Hulls[i].ProbeCount);
                Debug.Log($"[SPIKE] C3 {r.World.Name}: {ms:0.000} ms per tick ({r.Hulls.Length} ships, {probes} probes, " +
                          $"{r.Sea.ActiveComponentCount} wave components, Editor, Burst safety checks as configured); " +
                          $"16× needs ≤ 0.6 ms → {(ms <= 0.6 ? "PASS" : "FAIL")}; max speed ≈ {1000.0 / (50.0 * ms):0}×");
                Assert.LessOrEqual(ms, 0.6, $"{ms:0.000} ms per tick");
            }
        }

        // ── Criterion 4: what reckoned code needs from the engine ──────────────
        // Strict form (noEngineReferences: true) FAILED to compile: 75 errors, all Unity's own
        // NativeArray / IJob / IJobParallelFor / [ReadOnly] / [WriteOnly], which live in
        // UnityEngine.CoreModule; none involved Unity Physics (scratchpad spike_c4_strict_errors.txt).
        // So the guard has to ban APIs, not the engine. This records what the compiled spike uses.

        [Test]
        public void Criterion4_ReckonedAssembly_NeedsOnlyTheEngineCoreModule()
        {
            string[] refs = typeof(SpikeRunner).Assembly.GetReferencedAssemblies().Select(a => a.Name).OrderBy(n => n).ToArray();
            string[] engine = refs.Where(n => n == "UnityEngine" || n.StartsWith("UnityEngine.")).ToArray();
            Debug.Log($"[SPIKE] C4 DeepWater.Reckoning.Spike compiled against Unity.Physics + Entities; " +
                      $"references: {string.Join(", ", refs)}; engine modules: {(engine.Length == 0 ? "none" : string.Join(", ", engine))}");
            CollectionAssert.IsSubsetOf(engine, new[] { "UnityEngine.CoreModule" },
                "reckoned code should need nothing from the engine beyond containers and jobs");
        }
    }
}
