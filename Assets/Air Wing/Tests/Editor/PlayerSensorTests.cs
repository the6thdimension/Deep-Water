using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Radar;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using RHRadarSuite.Tracking;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// The player's sensors, SENSORS-CONCEPT slice 8 (J10; W70): the seat's buttons become the radar's two commands and
    /// the held IFF, chaff and flare switches; the radar's mode button steps the modes round; its lock button takes the
    /// firm track nearest the nose, and unlocks when pressed again. A human flying it in the Editor is not tested here.
    /// </summary>
    public class PlayerSensorTests
    {
        static PlayerSeatSettings Settings() => AssetDatabase.LoadAssetAtPath<PlayerSeatSettingsSO>("Assets/Air Wing/ScriptableObjects/PlayerSeatSettings.asset").BakeSeat();

        static PilotInputs Press(PlayerSeat seat, DeviceSample keys, long tick)
        {
            var frame = DeviceFrame.Idle;
            frame.Keyboard = keys;
            seat.Submit(frame);
            return seat.Issue(tick, 1, default);
        }

        [Test]
        public void TheButtons_AreHeldNotLatched_AndTheRadarButtonsArePresses()
        {
            var seat = new PlayerSeat(Settings(), 0.02, startLever: 0.75f);
            DeviceSample keys = DeviceSample.Idle;
            keys.Held = PilotSwitches.Chaff | PilotSwitches.Interrogate;
            keys.RadarModePressed = true;
            keys.FireRadarMissile = true;
            PilotInputs a = Press(seat, keys, 0);
            Assert.AreEqual(RadarCommand.NextMode, a.Radar);
            Assert.IsTrue((a.Switches & PilotSwitches.Chaff) != 0 && (a.Switches & PilotSwitches.Interrogate) != 0, "held: on");
            Assert.IsTrue(a.RadarPickle);

            PilotInputs b = Press(seat, DeviceSample.Idle, 1);
            Assert.AreEqual(RadarCommand.None, b.Radar, "a press is one tick");
            Assert.AreEqual(PilotSwitches.None, b.Switches & (PilotSwitches.Chaff | PilotSwitches.Interrogate | PilotSwitches.Flare), "let go: off (never latched)");
            Assert.IsFalse(b.RadarPickle);

            keys = DeviceSample.Idle;
            keys.RadarLockPressed = keys.RadarModePressed = true;
            PilotInputs c = Press(seat, keys, 2);
            Assert.AreEqual(RadarCommand.Lock, c.Radar, "the lock wins a tick with both");
            Assert.AreEqual(0, c.RadarTrack, "track 0: the button's lock");
        }

        [Test]
        public void TheModeButton_StepsRound()
        {
            FlightCore core = RadarTests.Fight(out RadarTests.RadarSeat me, 1);
            JetRadar r = core.RadarOf(1);
            var seen = new System.Collections.Generic.List<RadarMode> { r.Mode };
            for (int i = 0; i < 4; i++)
            {
                me.Next = RadarCommand.NextMode;
                core.Step();
                seen.Add(r.Mode);
            }
            Debug.Log($"[PLAYER-RADAR] the mode button from the start: {string.Join(" → ", seen)}");
            Assert.AreEqual(RadarMode.RangeWhileSearch, seen[0], "the jet starts in range-while-search");
            CollectionAssert.AreEqual(new[] { RadarMode.RangeWhileSearch, RadarMode.TrackWhileScan, RadarMode.Dogfight, RadarMode.Off, RadarMode.RangeWhileSearch }, seen);
        }

        [Test]
        public void TheLockButton_TakesTheTrackNearestTheNose_AndUnlocksOnTheNextPress()
        {
            // Two targets ahead: one 25° off the nose at 12 km, one 5° off at 18 km. The lock goes to the one nearer the nose.
            Vector3 Off(float deg, float km) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad)) * km * 1000f;
            FlightCore core = RadarTests.Fight(out RadarTests.RadarSeat me, 3, (Off(25f, 12f), 0f), (Off(5f, 18f), 0f));
            JetRadar r = core.RadarOf(1);
            me.Next = RadarCommand.TrackWhileScan;
            RadarTests.Run(core, 6f);
            Assert.GreaterOrEqual(RadarTests.Firm(r).Count(), 2, "set-up: both tracked");
            me.Next = RadarCommand.Lock;   // track 0
            core.Step();
            Assert.AreEqual(RadarMode.SingleTarget, r.Mode);
            Assert.AreEqual(3, r.LockedAircraftId, "the one 5° off the nose, though farther");
            me.Next = RadarCommand.Lock;
            core.Step();
            Assert.AreEqual(RadarMode.TrackWhileScan, r.Mode, "pressed again: unlocked, back to its search");
            Assert.AreEqual(0, r.LockedTrackId);
        }
    }
}
