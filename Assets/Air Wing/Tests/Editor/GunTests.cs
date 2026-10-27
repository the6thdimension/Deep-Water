using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Gunnery.Guns;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// The jet's gun in the flight core (Gunnery K4–K6): the trigger fires at the gun's rate from its rounds, the rounds
    /// fly in Gunnery and hit what they pass, hits take hit points, and at zero the jet is shot down, falls and crashes;
    /// all of it in the input log and the Fix, so a replay repeats it.
    /// </summary>
    public class GunTests
    {
        static readonly FidelitySheet L0 = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        /// <summary>A seat that holds its throttle and, from a tick on, the trigger.</summary>
        sealed class TriggerSeat : ISeat
        {
            public long FireFrom = long.MaxValue, FireUntil = long.MaxValue;
            public SeatKind Kind => SeatKind.Hold;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state) =>
                new PilotInputs { Throttle = 0.6f, Trigger = tick >= FireFrom && tick < FireUntil };
        }

        /// <summary>A shooter at 10,000 ft and 400 kt, and a target placed where its gun's rounds will be in 400 m.</summary>
        static FlightCore Range(out TriggerSeat trigger, out TriggerSeat other)
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 7);
            trigger = new TriggerSeat();
            other = new TriggerSeat();
            float speed = 400f * 0.514444f, fuel = 0.6f * e.Airframe.InternalFuelKg;
            core.Add(AircraftFactory.Build(1, e, L0, AircraftFactory.LevelStart(e, L0, new Vector3(0f, 3048f, 0f), 0f, speed, fuel, 0.6f)), trigger);
            core.Step();   // let the airframe settle onto its angle of attack before aiming
            AirframeState s = core.Find(1).State;
            GunMount m = e.Gun;
            Vector3 muzzle = s.Position + s.Attitude * m.MuzzleM;
            Assert.IsTrue(LeadSight.RoundAt(m.Gun, muzzle, s.Velocity, s.Attitude * m.BoreBody, 400f, out Vector3 at, out float tof));
            // Flying alongside at the shooter's velocity, the target is at the pipper when the rounds get there.
            AirframeState t = AircraftFactory.LevelStart(e, L0, at - s.Velocity * tof, 0f, speed, fuel, 0.6f);
            core.Add(AircraftFactory.Build(2, e, L0, t), other);
            return core;
        }

        [Test]
        public void TheF18E_CarriesTheM61A2_AndHitPoints()
        {
            AircraftProfile e = E();
            Assert.IsTrue(e.Gun.IsSet);
            Assert.AreEqual(412, e.Gun.Gun.RoundsCarried, "PUBLIC: GD-OTS F/A-18E/F gun system sheet");
            Assert.AreEqual(6000f, e.Gun.Gun.RoundsPerMinute, "PUBLIC: the high rate");
            Assert.IsTrue(e.Combat.IsSet);
            AirframeState start = AircraftFactory.LevelStart(e, L0, new Vector3(0f, 3000f, 0f), 0f, 200f, 1000f);
            Assert.AreEqual(412, start.Rounds, "a jet starts with a full gun");
            Assert.AreEqual(e.Combat.HitPoints, start.HitPoints, "and whole");
        }

        [Test]
        public void HoldingTheTrigger_FiresAtTheGunsRate_UntilLetGo()
        {
            FlightCore core = Range(out TriggerSeat trigger, out _);
            trigger.FireFrom = core.Tick;
            trigger.FireUntil = core.Tick + 50;   // one second
            for (int i = 0; i < 60; i++) core.Step();
            Assert.AreEqual(100, core.Rounds.RoundsFired, "6,000 a minute is 100 in a second");
            Assert.AreEqual(312, core.Find(1).State.Rounds);
            Assert.AreEqual(0f, core.Find(1).State.GunCarry, "a released trigger starts the next burst on a whole round");
        }

        [Test]
        public void ABurstOnTheTarget_ShootsItDown_AndItFallsAndCrashes()
        {
            FlightCore core = Range(out TriggerSeat trigger, out _);
            trigger.FireFrom = core.Tick;
            long start = core.Tick;
            while (core.Kills.Count == 0 && core.Tick < start + 150) core.Step();
            Assert.AreEqual(1, core.Kills.Count, "shot down inside three seconds");
            KillRecord k = core.Kills[0];
            Debug.Log($"[AIRWING GUN] {k}; {core.Rounds.RoundsFired} fired, {core.Rounds.RoundsHit} hit, {core.Hits.Count} hit reports; after {(k.Tick - start) * 0.02:F2} s");
            Assert.AreEqual(2, k.VictimId);
            Assert.AreEqual(1, k.ShooterId);
            Aircraft victim = core.Find(2);
            Assert.AreEqual(FlightStatus.Killed, victim.State.Status);
            Assert.AreEqual(1, victim.KilledBy);
            Assert.AreEqual(0f, victim.State.HitPoints);
            Assert.AreEqual(FlightStatus.Flying, core.Find(1).State.Status, "the shooter flies on");

            trigger.FireUntil = core.Tick;
            for (int i = 0; i < 60 * 50 && victim.State.Status == FlightStatus.Killed; i++) core.Step();
            Assert.AreEqual(FlightStatus.Crashed, victim.State.Status, "a jet shot down falls until it meets the sea");
            StringAssert.Contains("shot down by 1", victim.CrashReason);
            Assert.AreEqual(1, core.Kills.Count, "a wreck is not killed twice");
        }

        [Test]
        public void TwoRuns_HitTheSameWay_TickForTick()
        {
            FixLog Run()
            {
                FlightCore core = Range(out TriggerSeat trigger, out _);
                trigger.FireFrom = core.Tick + 10;
                for (int i = 0; i < 300; i++) core.Step();
                return core.Fixes;
            }
            FixLog a = Run(), b = Run();
            Assert.AreEqual(-1, a.FirstDivergence(b), "same seats, same seed: the same hits and the same Fix on every tick");
        }

        [Test]
        public void AMissedBurst_TakesNoHitPoints()
        {
            FlightCore core = Range(out TriggerSeat trigger, out _);
            // Move the target 60 m to the side: well outside its 4 m and the rounds' spread.
            Aircraft target = core.Find(2);
            target.State.Position += new Vector3(60f, 0f, 0f);
            trigger.FireFrom = core.Tick;
            trigger.FireUntil = core.Tick + 50;
            for (int i = 0; i < 100; i++) core.Step();
            Assert.AreEqual(0, core.Hits.Count);
            Assert.AreEqual(E().Combat.HitPoints, target.State.HitPoints);
        }
    }
}
