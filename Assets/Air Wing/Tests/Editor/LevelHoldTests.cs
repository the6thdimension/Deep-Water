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
    /// W54 (SIX 2026-10-27: mouse and keyboard "can't hold level flight"): let go of the keys and the jet flies level
    /// on the heading it had; leave the mouse aim near the horizon and it is the horizon. Every part L1.
    /// </summary>
    public class LevelHoldTests
    {
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PlayerSeatSettings Settings() => AssetDatabase.LoadAssetAtPath<PlayerSeatSettingsSO>("Assets/Air Wing/ScriptableObjects/PlayerSeatSettings.asset").BakeSeat();

        static DeviceFrame Keys(float pitch, float roll)
        {
            DeviceFrame f = DeviceFrame.Idle;
            f.Keyboard.StickPitch = pitch;
            f.Keyboard.StickRoll = roll;
            f.Keyboard.Moved = pitch != 0f || roll != 0f;
            return f;
        }

        static float TrackDeg(in AirframeState s) => Mathf.Atan2(s.Velocity.x, s.Velocity.z) * Mathf.Rad2Deg;

        [Test]
        public void LetGoOfTheKeys_AndTheJetFliesLevel_OnTheHeadingItHad()
        {
            AircraftProfile e = E();
            var seat = new PlayerSeat(Settings(), 0.02, startLever: 0.75f);
            seat.UseControls(e.Controls);
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, e, L1, AircraftFactory.LevelStart(e, L1, new Vector3(0f, 3048f, 0f), 0f, 350f * 0.514444f, 0.6f * e.Airframe.InternalFuelKg, 0.75f)), seat);
            Aircraft jet = core.Find(1);
            // Two seconds of right roll and a little back stick: a climbing turn.
            for (int t = 0; t < 100; t++) { seat.Submit(Keys(t < 40 ? 0.3f : 0.15f, t < 40 ? 1f : 0f)); core.Step(); }
            float bankAtRelease = FlightPathHands.BankDeg(jet.State), pathAtRelease = FlightPathHands.PathDeg(jet.State), trackAtRelease = TrackDeg(jet.State);
            // Let go.
            for (int t = 0; t < 15 * 50; t++) { seat.Submit(Keys(0f, 0f)); core.Step(); }
            float bank = FlightPathHands.BankDeg(jet.State), path = FlightPathHands.PathDeg(jet.State);
            float heightAfter15 = jet.State.Position.y, trackAfter15 = TrackDeg(jet.State);
            float worstHeight = 0f, worstTrack = 0f;
            for (int t = 0; t < 60 * 50; t++)
            {
                seat.Submit(Keys(0f, 0f));
                core.Step();
                worstHeight = Mathf.Max(worstHeight, Mathf.Abs(jet.State.Position.y - heightAfter15));
                worstTrack = Mathf.Max(worstTrack, Mathf.Abs(Mathf.DeltaAngle(trackAfter15, TrackDeg(jet.State))));
            }
            Debug.Log($"[AIRWING W54] released at {bankAtRelease:F0}° bank, {pathAtRelease:F1}° path, track {trackAtRelease:F0}°; after 15 s {bank:F1}° bank, {path:F2}° path, track {trackAfter15:F0}° " +
                      $"({Mathf.DeltaAngle(trackAtRelease, trackAfter15):F1}° from release); then 60 s within {worstHeight / 0.3048f:F0} ft and {worstTrack:F1}°; level hold {seat.LevelHolding}");
            Assert.Greater(Mathf.Abs(bankAtRelease), 20f, "it was banked when let go");
            Assert.IsTrue(seat.LevelHolding, "the level hold is flying");
            Assert.Less(Mathf.Abs(bank), 3f, "wings level within 15 s");
            Assert.Less(Mathf.Abs(path), 1f, "the path level within 15 s");
            Assert.Less(worstHeight / 0.3048f, 50f, "then holds its height within 50 ft for a minute");
            Assert.Less(worstTrack, 2f, "and its heading within 2°");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(trackAtRelease, trackAfter15)), 25f, "on about the heading it had when let go");
        }

        [Test]
        public void WithTheHoldOff_ACentredKeyboardIsACentredStick()
        {
            PlayerSeatSettings s = Settings();
            s.LevelHoldDelayS = 0f;
            var seat = new PlayerSeat(s, 0.02);
            AirframeState state = AircraftFactory.LevelStart(E(), new Vector3(0f, 3048f, 0f), 0f, 180f, 3000f);
            seat.Submit(Keys(0f, 1f));
            seat.Issue(0, 1, state);
            PilotInputs i = default;
            for (int t = 1; t < 100; t++) { seat.Submit(Keys(0f, 0f)); i = seat.Issue(t, 1, state); }
            Assert.AreEqual(0f, i.StickPitch);
            Assert.AreEqual(0f, i.StickRoll);
            Assert.IsFalse(seat.LevelHolding);
        }

        [Test]
        public void AMouseAimLeftNearTheHorizon_IsTheHorizon()
        {
            var seat = new PlayerSeat(Settings(), 0.02);
            AirframeState state = AircraftFactory.LevelStart(E(), new Vector3(0f, 3048f, 0f), 0f, 180f, 3000f);
            DeviceFrame move = DeviceFrame.Idle;
            move.MouseMoved = true;
            move.AimPitchDeg = 1.5f;
            seat.Submit(move);
            seat.Issue(0, 1, state);
            Assert.AreEqual(1.5f, Mathf.Asin(seat.AimDirection.y) * Mathf.Rad2Deg, 0.01f, "the aim follows the mouse while it moves");
            seat.Submit(DeviceFrame.Idle);
            seat.Issue(1, 1, state);
            Assert.AreEqual(0f, seat.AimDirection.y, 1e-6f, "once it stops, 1.5° is the horizon");
            move.AimPitchDeg = 4f;
            seat.Submit(move);
            seat.Issue(2, 1, state);
            seat.Submit(DeviceFrame.Idle);
            seat.Issue(3, 1, state);
            Assert.AreEqual(4f, Mathf.Asin(seat.AimDirection.y) * Mathf.Rad2Deg, 0.01f, "outside the detent the aim stays where it was put");
        }
    }
}
