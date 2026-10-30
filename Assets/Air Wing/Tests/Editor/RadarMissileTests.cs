using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Radar;
using static DeepWater.AirWing.Tests.RadarTests;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// The radar missile, SENSORS-CONCEPT slice 3 (J7, J18; Air Wing W68): two on the inboard pylons; launched only on the
    /// radar's single-target track inside 1.5–25 km and 45° of the nose; it flies on the shooter's track, goes active 8 km
    /// out, and from then the target hears "missile" and the shooter may let go; a lock dropped before that loses it.
    /// </summary>
    public class RadarMissileTests
    {
        /// <summary>Jet 1 locked in single-target track on jet 2, <paramref name="aheadM"/> ahead and flying the same way.</summary>
        internal static FlightCore Locked(out RadarSeat[] seats, float aheadM, ulong seed = 5)
        {
            FlightCore core = FightAll(out seats, seed, (new Vector3(0f, 0f, aheadM), 0f));
            seats[1].Next = RadarCommand.Off;   // only jet 1 emits
            seats[0].Next = RadarCommand.TrackWhileScan;
            Run(core, 10f);
            JetRadar r = core.RadarOf(1);
            seats[0].Next = RadarCommand.Lock;
            seats[0].NextTrack = Firm(r).Single().Id;
            Run(core, 1f);
            Assert.AreEqual(2, r.LockedAircraftId, "set-up: locked");
            return core;
        }

        static MissileEvent[] Events(FlightCore core, MissileEventKind kind) => core.Missiles.Events.Where(e => e.Kind == kind).ToArray();

        [Test]
        public void TheF18E_CarriesTwoRadarMissiles_Inboard()
        {
            AircraftProfile e = E();
            Assert.IsTrue(e.Missiles.HasRadarMissiles);
            Assert.AreEqual(2, e.Missiles.RadarCarried, "J18: 2 radar + 2 heat");
            Assert.AreEqual(MissileSeeker.ActiveRadar, e.Missiles.RadarMissile.Seeker);
            Assert.AreEqual(25000f, e.Missiles.RadarMissile.LockMaxM, "J15: about 25 km");
            Assert.AreEqual(2, AircraftFactory.LevelStart(e, new Core.Fidelity.FidelitySheet(Core.Fidelity.FidelityLevel.L0, Core.Fidelity.FidelityLevel.L0,
                Core.Fidelity.FidelityLevel.L0, Core.Fidelity.FidelityLevel.L0, Core.Fidelity.FidelityLevel.L0, 4, 1442), new Vector3(0f, 3000f, 0f), 0f, 200f, 1000f, 0.7f).RadarMissiles);
        }

        [Test]
        public void NoSingleTargetTrack_NoLaunch()
        {
            FlightCore core = FightAll(out RadarSeat[] seats, 5, (new Vector3(0f, 0f, 15000f), 0f));
            seats[0].Next = RadarCommand.TrackWhileScan;
            Run(core, 10f);
            seats[0].FireRadar = true;
            Run(core, 1f);
            Assert.AreEqual(0, core.Missiles.RadarLaunched, "track-while-scan tracks are not a lock");
        }

        [Test]
        public void OutsideItsRange_NoLaunch()
        {
            FlightCore core = Locked(out RadarSeat[] seats, 30000f);
            seats[0].FireRadar = true;
            Run(core, 1f);
            Assert.AreEqual(0, core.Missiles.RadarLaunched, "30 km: past its 25 km");
        }

        [Test]
        public void ItFliesOnTheTrack_GoesActive_ThenTheTargetHearsMissile_AndItBursts()
        {
            FlightCore core = Locked(out RadarSeat[] seats, 18000f);
            WarningReceiver rwr = core.WarningReceiverOf(2);
            seats[0].FireRadar = true;
            core.Step();
            Assert.AreEqual(1, core.Missiles.RadarLaunched);
            Assert.AreEqual(1, core.Find(1).State.RadarMissiles, "one of two left");
            Assert.AreEqual(2, core.Find(1).State.Missiles, "the heat-seekers stay");

            bool heardTrackBeforeActive = false, heardMissileBeforeActive = false, heardMissile = false;
            long launch = core.Tick;
            while (core.Missiles.Bursts == 0 && core.Tick < launch + 50 * 50)
            {
                core.Step();
                bool active = core.Missiles.WentActive > 0;
                if (!active && rwr.Strongest == PaintKind.Track) heardTrackBeforeActive = true;
                if (!active && rwr.Strongest == PaintKind.Missile) heardMissileBeforeActive = true;
                if (active && rwr.Strongest == PaintKind.Missile) heardMissile = true;
            }
            MissileEvent act = Events(core, MissileEventKind.Active).Single();
            MissileEvent burst = Events(core, MissileEventKind.Burst).Single();
            Debug.Log($"[RDR-MSL] launched at 18 km: active at {(act.Tick - launch) / 50f:0.0} s, burst at {(burst.Tick - launch) / 50f:0.0} s (killed {burst.Killed})");
            Assert.IsTrue(heardTrackBeforeActive, "before it goes active the target hears only the shooter's track");
            Assert.IsFalse(heardMissileBeforeActive, "no missile warning before it goes active");
            Assert.IsTrue(heardMissile, "active: the target hears the missile");
            Assert.AreEqual(2, burst.TargetId);
            Assert.Greater(act.Tick, launch);
            Assert.Greater(burst.Tick, act.Tick);
        }

        [Test]
        public void ALockDroppedBeforeActive_LosesIt()
        {
            FlightCore core = Locked(out RadarSeat[] seats, 20000f);
            seats[0].FireRadar = true;
            Run(core, 2f);
            seats[0].Next = RadarCommand.Unlock;
            Run(core, 40f);
            Assert.AreEqual(1, Events(core, MissileEventKind.Lost).Length, "lost when the lock went");
            Assert.AreEqual(0, core.Missiles.WentActive);
            Assert.AreEqual(0, core.Missiles.Bursts);
        }

        [Test]
        public void OnceActive_TheShooterMayLetGo()
        {
            FlightCore core = Locked(out RadarSeat[] seats, 18000f);
            seats[0].FireRadar = true;
            long launch = core.Tick;
            while (core.Missiles.WentActive == 0 && core.Tick < launch + 50 * 50) core.Step();
            Assert.AreEqual(1, core.Missiles.WentActive);
            seats[0].Next = RadarCommand.Unlock;
            Run(core, 30f);
            Assert.AreEqual(0, Events(core, MissileEventKind.Lost).Length);
            Assert.AreEqual(1, core.Missiles.Bursts, "its own radar finished the job");
        }

        [Test]
        public void ARadarMissileFight_RepeatsTickForTick()
        {
            string Story(ulong seed)
            {
                FlightCore core = Locked(out RadarSeat[] seats, 18000f, seed);
                seats[0].FireRadar = true;
                Run(core, 40f);
                return string.Join("\n", core.Missiles.Events.Select(e => e.ToString() + " " + e.Point.ToString("R")));
            }
            string a = Story(9), b = Story(9);
            Assert.IsNotEmpty(a);
            Assert.AreEqual(a, b);
        }
    }
}
