using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Editor;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Tests
{
    /// <summary>
    /// Spotting by size, SENSORS-CONCEPT slice 5 (J8; RH Aviator A20): a jet in view is first seen by a seeded draw each
    /// tick, at a rate that falls with distance and with how small it looks; nose-on is hardest.
    /// </summary>
    public class SpottingTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Pilot(string path) => AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path).Bake();
        static AirframeState At(in AircraftProfile e, Vector3 at, float headingDeg) =>
            AircraftFactory.LevelStart(e, L1, at, headingDeg, 300f * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        [Test]
        public void TheRate_FallsWithDistance_AndNoseOnIsHardest()
        {
            AircraftProfile e = E();
            PilotProfile p = Pilot(AviatorTools.LinePilotPath);
            // Beam-on: the target 90° off its nose from the eye at the origin.
            float Beam(float d) => Perception.SpotRatePerS(p, Vector3.zero, At(e, new Vector3(0f, 0f, d), 90f));
            float[] near = { Beam(1000f), Beam(2000f), Beam(4000f), Beam(8000f) };
            for (int i = 1; i < near.Length; i++) Assert.Less(near[i], near[i - 1], "farther, rarer");
            Assert.AreEqual(p.SpotRatePerS / 2f, Beam(p.SpotHalfRangeM), 1e-4f, "half the rate at the half range, from the beam");

            float nose = Perception.SpotRatePerS(p, Vector3.zero, At(e, new Vector3(0f, 0f, 4000f), 180f));  // pointing at the eye
            float tail = Perception.SpotRatePerS(p, Vector3.zero, At(e, new Vector3(0f, 0f, 4000f), 0f));    // pointing away
            Debug.Log($"[SPOT] line pilot at 4 km: {Beam(4000f):F3}/s from the beam, {tail:F3}/s from behind, {nose:F3}/s nose-on; beam at 1, 2, 8 km: {near[0]:F3}, {near[1]:F3}, {near[3]:F3}/s");
            Assert.Less(nose, tail, "nose-on is smaller than tail-on");
            Assert.Less(tail, Beam(4000f), "and both smaller than the beam");
        }

        /// <summary>Ticks until a pilot's eyes first hold the target, over <paramref name="trials"/> seeds.</summary>
        static double MeanTicksToSpot(in PilotProfile p, AirframeState me, AirframeState target, int trials, int maxTicks = 50 * 120)
        {
            long total = 0;
            for (int s = 0; s < trials; s++)
            {
                var eyes = new Perception(1, p, 0.02, truth: false, seed: (ulong)(1000 + s));
                var picture = new[] { new AircraftRecord(1, me), new AircraftRecord(2, target) };
                int t = 0;
                for (; t < maxTicks && !eyes.TryGet(2, out _); t++) eyes.Look(t, picture);
                total += t;
            }
            return total / (double)trials;
        }

        [Test]
        public void OverManySeededLooks_NearIsSoonerThanFar_AndTheBeamSoonerThanNoseOn_AtTheSetRate()
        {
            AircraftProfile e = E();
            PilotProfile p = Pilot(AviatorTools.LinePilotPath);
            AirframeState me = At(e, Vector3.zero, 0f);
            const int n = 200;
            AirframeState beam3 = At(e, new Vector3(0f, 0f, 3000f), 90f), beam6 = At(e, new Vector3(0f, 0f, 6000f), 90f), nose3 = At(e, new Vector3(0f, 0f, 3000f), 180f);
            double tBeam3 = MeanTicksToSpot(p, me, beam3, n), tBeam6 = MeanTicksToSpot(p, me, beam6, n), tNose3 = MeanTicksToSpot(p, me, nose3, n);
            Debug.Log($"[SPOT] line pilot, {n} seeds each, mean time to spot: beam 3 km {tBeam3 / 50:F2} s, beam 6 km {tBeam6 / 50:F2} s, nose-on 3 km {tNose3 / 50:F2} s");
            Assert.Less(tBeam3, tBeam6, "nearer, sooner");
            Assert.Less(tBeam3, tNose3, "from the beam, sooner than nose-on");

            // An exponential wait: the mean is one over the rate, after the reaction delay.
            double expected = p.ReactionDelayS * 50 + 1.0 / (Perception.SpotRatePerS(p, me.Position, beam6) * 0.02);
            Assert.AreEqual(expected, tBeam6, 0.15 * expected, "beam 6 km: the mean wait matches the set rate within 15 %");
        }

        [Test]
        public void TheSameSeed_SpotsOnTheSameTick()
        {
            AircraftProfile e = E();
            PilotProfile p = Pilot(AviatorTools.AcePilotPath);
            AirframeState me = At(e, Vector3.zero, 0f), far = At(e, new Vector3(500f, 0f, 7000f), 200f);
            Assert.AreEqual(MeanTicksToSpot(p, me, far, 5), MeanTicksToSpot(p, me, far, 5));
        }

        [Test]
        public void AJetItKnows_IsSeenAgainTheMomentItIsInView()
        {
            AircraftProfile e = E();
            PilotProfile p = Pilot(AviatorTools.LinePilotPath);
            var eyes = new Perception(1, p, 0.02, truth: false, seed: 3);
            AirframeState me = At(e, Vector3.zero, 0f), target = At(e, new Vector3(0f, 0f, 2000f), 90f);
            int t = 0;
            for (; !eyes.TryGet(2, out _); t++) eyes.Look(t, new[] { new AircraftRecord(1, me), new AircraftRecord(2, target) });
            AirframeState behind = target;
            behind.Position = new Vector3(0f, 0f, -2000f);
            for (int k = 0; k < eyes.DelayTicks + 5; k++, t++) eyes.Look(t, new[] { new AircraftRecord(1, me), new AircraftRecord(2, behind) });
            Assert.IsTrue(eyes.TryGet(2, out Contact lost) && !lost.InView, "out of view, remembered");
            for (int k = 0; k <= eyes.DelayTicks; k++, t++) eyes.Look(t, new[] { new AircraftRecord(1, me), new AircraftRecord(2, target) });
            Assert.IsTrue(eyes.TryGet(2, out Contact back) && back.InView, "back in view: seen at once, no new draw needed");
        }
    }
}
