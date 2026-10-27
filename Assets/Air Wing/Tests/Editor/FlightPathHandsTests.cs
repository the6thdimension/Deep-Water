using System;
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

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// Slice 3a, the Air Wing side (RH Aviator A1, A2): the shared flight-path layer flown on the L1 jet, and the flight
    /// core's picture of the previous tick.
    /// </summary>
    public class FlightPathHandsTests
    {
        const double Kt = Units.KnotToMetrePerSecond;

        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1);

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        /// <summary>A test pilot's hands (the line pilot's numbers, RH Aviator's Pilot_Line).</summary>
        static readonly HandsSettings Hands = new HandsSettings
        {
            GuidanceGain = 2f, MaxBankDeg = 70f, RollRatePerBankDeg = 2f, ClimbDegPerM = 0.03f, MaxClimbDeg = 20f, MaxDiveDeg = 15f,
            GPerPathDeg = 0.25f, MaxG = 6f, ThrottlePerKt = 0.02f, ThrottleTrim = 0.75f, ThrottleIntegralPerKtS = 0.004f, UseAfterburner = true, SpeedBrakeOverKt = 25f,
            FloorAglM = 100f, GuardLookAheadS = 6f, GuardClimbDeg = 8f, GuardMaxBankDeg = 30f,
        };

        sealed class HandsSeat : ISeat
        {
            readonly FlightPathHands _hands;
            readonly Func<AirframeState, FlightPathRequest> _request;
            readonly float _surface;
            public HandsSeat(FlightPathHands hands, Func<AirframeState, FlightPathRequest> request, float surface)
            {
                _hands = hands; _request = request; _surface = surface;
            }
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int id, in AirframeState s) => _hands.Fly(_request(s), s, _surface);
        }

        static (FlightCore core, Core.Aircraft jet) Start(AircraftProfile e, float heightM, Func<AirframeState, FlightPathRequest> request, float surface = 0f)
        {
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(surface));
            var start = AircraftFactory.LevelStart(e, L1, new Vector3(0f, heightM, 0f), 0f, (float)(400 * Kt), 0.6f * e.Airframe.InternalFuelKg, 0.7f);
            core.Add(AircraftFactory.Build(1, e, L1, start), new HandsSeat(new FlightPathHands(Hands, e.Controls), request, surface));
            return (core, core.Find(1));
        }

        [Test]
        public void TheHands_HoldAHeadingAnAltitudeAndASpeed()
        {
            AircraftProfile e = E();
            var (core, jet) = Start(e, 1524f, s => new FlightPathRequest { AimWorld = new Vector3(0f, 0f, 1e6f), AltitudeM = 1524f, SpeedMps = (float)(400 * Kt) });
            for (int t = 0; t < 1500; t++) core.Step();
            AirframeState s = jet.State;
            float kt = s.Velocity.magnitude / (float)Kt;
            Debug.Log($"[AVIATOR HANDS] hold 30 s: {s.Position.y - 1524f:+0.0;-0.0} m, {kt:F1} kt, track {FlightPathHands.TrackDeg(s):F2}°, bank {FlightPathHands.BankDeg(s):F2}°");
            Assert.AreEqual(1524f, s.Position.y, 30f);
            Assert.AreEqual(400f, kt, 10f);
            Assert.AreEqual(0f, FlightPathHands.TrackDeg(s), 2f);
        }

        [Test]
        public void ATurnBehind_StaysInsideTheBankAndGCaps_AndComesRound()
        {
            AircraftProfile e = E();
            var (core, jet) = Start(e, 3000f, s => new FlightPathRequest { AimWorld = new Vector3(0f, 0f, -1e6f), AltitudeM = 3000f, SpeedMps = (float)(400 * Kt) });
            float maxBank = 0f, maxG = 0f;
            int t = 0;
            for (; t < 3000; t++)
            {
                core.Step();
                maxBank = Mathf.Max(maxBank, Mathf.Abs(FlightPathHands.BankDeg(jet.State)));
                maxG = Mathf.Max(maxG, jet.State.LoadFactor);
                if (Mathf.Abs(Mathf.DeltaAngle(FlightPathHands.TrackDeg(jet.State), 180f)) < 3f && t > 100) break;
            }
            Debug.Log($"[AVIATOR HANDS] 180° turn: {t * 0.02f:F1} s, max bank {maxBank:F1}°, max {maxG:F2} G, {jet.State.Position.y - 3000f:+0;-0} m");
            Assert.Less(t, 3000, "came round");
            Assert.Less(maxBank, Hands.MaxBankDeg + 5f, "the bank cap holds");
            Assert.Less(maxG, Hands.MaxG + 0.3f, "the pilot's G cap holds");
            Assert.AreEqual(3000f, jet.State.Position.y, 150f, "and it held its height through the turn");
        }

        [Test]
        public void TheGroundGuard_WillNotFlyIntoTheSea()
        {
            AircraftProfile e = E();
            // Asked for the sea itself, steep, turning: the guard must hold it above the floor.
            var (core, jet) = Start(e, 600f, s => new FlightPathRequest { AimWorld = new Vector3(1e6f, 0f, 0f), AltitudeM = -500f, SpeedMps = (float)(450 * Kt) });
            float lowest = float.MaxValue;
            for (int t = 0; t < 3000; t++)
            {
                core.Step();
                lowest = Mathf.Min(lowest, jet.State.Position.y);
            }
            Debug.Log($"[AVIATOR HANDS] guard: lowest {lowest:F0} m (floor {Hands.FloorAglM} m), {jet.State.Status}");
            Assert.AreEqual(FlightStatus.Flying, jet.State.Status);
            Assert.Greater(lowest, Hands.FloorAglM * 0.5f, "never far below the floor");
        }

        [Test]
        public void ThePicture_IsThePreviousTick_TheSameForEverySeat()
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var a = AircraftFactory.LevelStart(e, L1, new Vector3(0f, 3000f, 0f), 0f, (float)(400 * Kt), 3000f, 0.7f);
            var b = AircraftFactory.LevelStart(e, L1, new Vector3(500f, 3000f, 0f), 0f, (float)(400 * Kt), 3000f, 0.7f);
            core.Add(AircraftFactory.Build(1, e, L1, a), new HoldSeat(new PilotInputs { Throttle = 0.7f, StickRoll = 0.3f }));
            var spy = new PictureSpy(core);
            core.Add(AircraftFactory.Build(2, e, L1, b), spy);
            Assert.AreEqual(0, core.Picture.Count, "empty before the first tick");
            for (int t = 0; t < 20; t++)
            {
                AirframeState oneBefore = core.Find(1).State;
                core.Step();
                Assert.AreEqual(oneBefore.Position, spy.SeenOfOne.Position, $"tick {t}: aircraft 2 saw aircraft 1 where the last tick left it, not after its step this tick");
            }
        }

        sealed class PictureSpy : ISeat
        {
            readonly FlightCore _core;
            public AirframeState SeenOfOne;
            public PictureSpy(FlightCore core) => _core = core;
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int id, in AirframeState s)
            {
                foreach (AircraftRecord r in _core.Picture) if (r.Id == 1) SeenOfOne = r.State;
                return new PilotInputs { Throttle = 0.7f };
            }
        }
    }
}
