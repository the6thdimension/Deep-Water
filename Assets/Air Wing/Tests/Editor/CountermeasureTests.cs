using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using static DeepWater.AirWing.Tests.RadarTests;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// Chaff and flares, SENSORS-CONCEPT slice 4 (J4; Air Wing W69): 30 of each, refilled on respawn, one a second while
    /// the button is held; each drop breaks each missile of its kind coming at the jet at its set chance (chaff: radar
    /// missiles; flares: heat-seekers), and never the other kind.
    /// </summary>
    public class CountermeasureTests
    {
        [Test]
        public void TheF18E_CarriesThirtyOfEach_AndAResetRefillsThem()
        {
            AircraftProfile e = E();
            Assert.IsTrue(e.Countermeasures.IsSet);
            Assert.AreEqual(30, e.Countermeasures.Chaff);
            Assert.AreEqual(30, e.Countermeasures.Flares);

            FlightCore core = Fight(out RadarSeat seat, 5);
            seat.Hold = PilotSwitches.Chaff | PilotSwitches.Flare;
            Run(core, 5f);
            Assert.AreEqual(25, core.Find(1).State.Chaff, "one a second: ticks 0, 50 … 200");
            Assert.AreEqual(25, core.Find(1).State.Flares);
            seat.Hold = PilotSwitches.None;
            seat.ResetNext = true;
            Run(core, 0.1f);
            Assert.AreEqual(30, core.Find(1).State.Chaff, "a reset puts the start state back, full");
            Assert.AreEqual(30, core.Find(1).State.Flares);
        }

        [Test]
        public void HeldDown_TheyRunOut()
        {
            FlightCore core = Fight(out RadarSeat seat, 5);
            seat.Hold = PilotSwitches.Chaff;
            Run(core, 40f);
            Assert.AreEqual(0, core.Find(1).State.Chaff);
            Assert.AreEqual(30, core.Missiles.ChaffDropped, "thirty, then nothing");
            Assert.AreEqual(30, core.Find(1).State.Flares, "the flares untouched");
            Assert.AreEqual(0, core.Missiles.FlaresDropped);
        }

        [Test]
        public void EachDrop_BreaksItsKindAtItsChance_OverManySeededDraws_AndNeverTheOtherKind()
        {
            CountermeasureProfile cm = E().Countermeasures;
            const int n = 4000;
            var rng = new DeterministicRng(77);
            int chaffBreaks = 0, flareBreaks = 0;
            for (int i = 0; i < n; i++)
            {
                if (cm.Breaks(radarMissile: true, chaff: true, flare: false, rng)) chaffBreaks++;
                if (cm.Breaks(radarMissile: false, chaff: false, flare: true, rng)) flareBreaks++;
            }
            float pc = chaffBreaks / (float)n, pf = flareBreaks / (float)n;
            float sigma3 = 3f * Mathf.Sqrt(0.25f * 0.75f / n);
            Debug.Log($"[CM] {n} draws each: chaff broke {pc:P1} of radar missiles, flares {pf:P1} of heat-seekers (set 25 %, ±{sigma3:P1} at 3σ)");
            Assert.AreEqual(cm.ChaffBreakChance, pc, sigma3);
            Assert.AreEqual(cm.FlareBreakChance, pf, sigma3);

            long drawn = rng.Draws;
            for (int i = 0; i < n; i++)
            {
                Assert.IsFalse(cm.Breaks(radarMissile: false, chaff: true, flare: false, rng), "chaff does nothing to a heat-seeker");
                Assert.IsFalse(cm.Breaks(radarMissile: true, chaff: false, flare: true, rng), "a flare does nothing to a radar missile");
            }
            Assert.AreEqual(drawn, rng.Draws, "a drop that cannot work takes no draw");
        }

        static FlightCore RadarShot(PilotSwitches targetHolds, ulong seed)
        {
            FlightCore core = RadarMissileTests.Locked(out RadarSeat[] seats, 18000f, seed);
            seats[0].FireRadar = true;
            seats[1].Hold = targetHolds;
            Run(core, 50f);
            Assert.AreEqual(1, core.Missiles.RadarLaunched, "set-up: launched");
            return core;
        }

        [Test]
        public void Chaff_BreaksARadarMissile_FlaresDoNot()
        {
            FlightCore flares = RadarShot(PilotSwitches.Flare, 5);
            Assert.AreEqual(0, flares.Missiles.Decoyed, "flares do nothing to a radar missile");
            Assert.AreEqual(1, flares.Missiles.Bursts);

            FlightCore chaff = RadarShot(PilotSwitches.Chaff, 5);
            MissileEvent d = chaff.Missiles.Events.Single(ev => ev.Kind == MissileEventKind.Decoyed);
            Debug.Log($"[CM] radar missile at 18 km, target dropping chaff: {d} after {chaff.Missiles.ChaffDropped} drops in all");
            Assert.AreEqual(0, chaff.Missiles.Bursts, "broken, it never bursts");
        }

        static FlightCore HeatShot(PilotSwitches targetHolds, ulong seed)
        {
            FlightCore core = FightAll(out RadarSeat[] seats, seed, (new Vector3(0f, 0f, 3000f), 0f));
            seats[0].Pickle = true;
            seats[1].Hold = targetHolds;
            long until = core.Tick + 10 * 50;
            while (core.Missiles.Launched == 0 && core.Tick < until) core.Step();
            seats[0].Pickle = false;
            Assert.AreEqual(1, core.Missiles.Launched, "set-up: one heat-seeker launched");
            Run(core, 20f);
            return core;
        }

        [Test]
        public void Flares_BreakHeatSeekers_ChaffDoesNot()
        {
            int decoyed = 0, burstThroughFlares = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                FlightCore chaff = HeatShot(PilotSwitches.Chaff, seed);
                Assert.AreEqual(0, chaff.Missiles.Decoyed, $"seed {seed}: chaff does nothing to a heat-seeker");
                FlightCore flares = HeatShot(PilotSwitches.Flare, seed);
                decoyed += flares.Missiles.Decoyed;
                burstThroughFlares += flares.Missiles.Bursts;
            }
            Debug.Log($"[CM] heat-seekers at 3 km, six seeds, target dropping flares: {decoyed} decoyed, {burstThroughFlares} burst anyway");
            Assert.Greater(decoyed, 0, "flares break heat-seekers");
        }

        [Test]
        public void ADecoyFight_RepeatsTickForTick()
        {
            string Story(ulong seed) => string.Join("\n", RadarShot(PilotSwitches.Chaff | PilotSwitches.Flare, seed).Missiles.Events.Select(ev => ev.ToString() + " " + ev.Point.ToString("R")));
            Assert.AreEqual(Story(9), Story(9));
        }
    }
}
