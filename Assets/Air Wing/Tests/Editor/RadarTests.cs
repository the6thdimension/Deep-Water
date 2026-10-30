using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Radar;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using RHRadarSuite.Detection;
using RHRadarSuite.Tracking;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// The jets' radar, SENSORS-CONCEPT slice 1 (J1, J2, J6, J16): detection falls with range and with low flight over the
    /// sea; track-while-scan holds several tracks; single-target track holds one; dogfight locks the first contact ahead;
    /// a lost lock returns to the search mode; a reset clears the radar; the same seed gives the same tracks.
    /// </summary>
    public class RadarTests
    {
        static readonly FidelitySheet L0 = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);
        const float Kt = 0.514444f;
        const int TicksPerS = 50;
        internal static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        static AirframeState Level(in AircraftProfile e, Vector3 at, float headingDeg, float kt = 400f) =>
            AircraftFactory.LevelStart(e, L0, at, headingDeg, kt * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        /// <summary>A seat that holds the throttle and sends one radar command when told to (and a reset when told to).</summary>
        internal sealed class RadarSeat : ISeat
        {
            public RadarCommand Next;
            public int NextTrack;
            public bool ResetNext;
            /// <summary>Switches held every tick (e.g. Interrogate).</summary>
            public PilotSwitches Hold;
            /// <summary>The radar missile's pickle, for the next tick only.</summary>
            public bool FireRadar;
            /// <summary>The heat-seeker's pickle, held while true.</summary>
            public bool Pickle;
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
            {
                var p = new PilotInputs { Throttle = 0.7f, Radar = Next, RadarTrack = NextTrack, Switches = Hold | (ResetNext ? PilotSwitches.Reset : default) };
                p.RadarPickle = FireRadar;
                p.Pickle = Pickle;
                FireRadar = false;
                Next = RadarCommand.None;
                NextTrack = 0;
                ResetNext = false;
                return p;
            }
        }

        /// <summary>A shooter (id 1) at 10,000 ft heading north, and targets (ids 2, 3 …) at the given offsets and headings.</summary>
        internal static FlightCore Fight(out RadarSeat shooter, ulong seed, params (Vector3 offset, float heading)[] targets)
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: seed);
            shooter = new RadarSeat();
            var origin = new Vector3(0f, 3048f, 0f);
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, origin, 0f)), shooter);
            for (int i = 0; i < targets.Length; i++)
                core.Add(AircraftFactory.Build(2 + i, e, L0, Level(e, origin + targets[i].offset, targets[i].heading)), new RadarSeat());
            return core;
        }

        /// <summary>As the one-seat Fight, handing back every seat (index 0 the shooter).</summary>
        internal static FlightCore FightAll(out RadarSeat[] seats, ulong seed, params (Vector3 offset, float heading)[] targets)
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: seed);
            seats = new RadarSeat[targets.Length + 1];
            var origin = new Vector3(0f, 3048f, 0f);
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, origin, 0f)), seats[0] = new RadarSeat());
            for (int i = 0; i < targets.Length; i++)
                core.Add(AircraftFactory.Build(2 + i, e, L0, Level(e, origin + targets[i].offset, targets[i].heading)), seats[i + 1] = new RadarSeat());
            return core;
        }

        internal static void Run(FlightCore core, float seconds)
        {
            long end = core.Tick + (long)Mathf.Round(seconds * TicksPerS);
            while (core.Tick < end) core.Step();
        }

        internal static IEnumerable<RadarTrack> Firm(JetRadar r) => r.Tracks.Where(t => t.State == TrackState.Confirmed || t.State == TrackState.Coasting);

        /// <summary>One look from a radar at 6 km at a target nose-on at <paramref name="rangeM"/> and <paramref name="heightM"/>.</summary>
        static JetLookResult LookAt(in RadarProfile p, float rangeM, float heightM, double? seaGamma, ulong seed = 1)
        {
            var radar = new Vector3(0f, 6000f, 0f);
            var target = new Vector3(0f, heightM, rangeM);
            Quaternion facing = Quaternion.LookRotation(radar - target);
            AspectRcs rcs = E().RadarSignature.Rcs;
            return JetRadarLook.Look(p, JetRadarLook.SnrAt1mDb(p), 0.0, seaGamma, radar, 6000f, target, facing, heightM, rcs, new DeterministicRng(seed));
        }

        [Test]
        public void TheF18E_CarriesAGameRadar_SolvedToSeeA5DbsmTargetAt40kmHalfTheTime()
        {
            AircraftProfile e = E();
            Assert.IsTrue(e.Radar.IsSet);
            Assert.IsTrue(e.RadarSignature.IsSet);
            double snr = JetRadarLook.SnrAt1mDb(e.Radar) + 5.0 - 40.0 * System.Math.Log10(40000.0);
            Assert.AreEqual(0.5, RadarEquation.PdSwerling1(snr, e.Radar.FalseAlarmProbability), 0.005);
            Assert.AreEqual(RadarMode.RangeWhileSearch, e.Radar.StartMode);
        }

        [Test]
        public void Detection_FallsWithRange()
        {
            RadarProfile p = E().Radar;
            double last = 1.0;
            var pds = new List<double>();
            foreach (float km in new[] { 10f, 20f, 30f, 40f, 50f })
            {
                JetLookResult look = LookAt(p, km * 1000f, 6000f, null);
                Assert.IsTrue(look.AboveHorizon);
                Assert.IsFalse(look.InClutter, "level with the radar: sky behind it");
                Assert.Less(look.Pd, last, $"{km} km");
                last = look.Pd;
                pds.Add(look.Pd);
            }
            Debug.Log($"[RADAR] nose-on Pd per look at 10…50 km: {string.Join(", ", pds.Select(x => x.ToString("0.000")))}");
            Assert.Greater(pds[0], 0.95, "a jet at 10 km is seen almost every look");
            Assert.Less(pds[4], 0.3, "past the arena's edge it is rarely seen");
        }

        [Test]
        public void LowFlightOverTheSea_HidesAJet_InTheClutter()
        {
            RadarProfile p = E().Radar;
            double sea = Clutter.SeaGammaDb(4.0, RadarEquation.WavelengthM(p.Terms.FrequencyHz));
            JetLookResult high = LookAt(p, 20000f, 3000f, sea);
            JetLookResult low = LookAt(p, 20000f, 60f, sea);
            JetLookResult lowNoSea = LookAt(p, 20000f, 60f, null);
            Debug.Log($"[RADAR] 20 km, Beaufort 4: high Pd {high.Pd:0.000}; low Pd {low.Pd:0.000} (CNR {low.CnrDb:0.0} dB); low with no sea {lowNoSea.Pd:0.000}");
            Assert.IsFalse(high.InClutter, "3 km up: above the beam's lower edge at 20 km");
            Assert.IsTrue(low.InClutter);
            Assert.Less(low.Pd, high.Pd - 0.3, "low over the sea is much harder to see");
            Assert.IsFalse(lowNoSea.InClutter, "no sea, no clutter");
            Assert.Greater(lowNoSea.Pd, low.Pd);

            // And the radar beneath the target never sees clutter behind it.
            var radar = new Vector3(0f, 60f, 0f);
            var target = new Vector3(0f, 3000f, 20000f);
            JetLookResult up = JetRadarLook.Look(p, JetRadarLook.SnrAt1mDb(p), 0.0, sea, radar, 60f, target, Quaternion.LookRotation(radar - target), 3000f,
                                                 E().RadarSignature.Rcs, new DeterministicRng(1));
            Assert.IsFalse(up.InClutter, "looking up: sky behind it");
        }

        [Test]
        public void TheSea_InTheCore_ClutterALowJet()
        {
            JetLookResult LowLook(bool sea)
            {
                FlightCore core = Fight(out _, 5, (new Vector3(0f, -3048f + 60f, 20000f), 180f));
                if (sea) core.UseRadarWorld(new RadarEnvironment { Sea = true, SeaBeaufort = 4.0 });
                Run(core, 5f);
                Assert.IsTrue(core.RadarOf(1).LastLooks.ContainsKey(2), "the search looked at it");
                return core.RadarOf(1).LastLooks[2];
            }
            JetLookResult overSea = LowLook(true), overNothing = LowLook(false);
            Assert.IsTrue(overSea.InClutter);
            Assert.IsFalse(overNothing.InClutter);
            Assert.Less(overSea.Pd, overNothing.Pd);
        }

        [Test]
        public void TrackWhileScan_HoldsSeveralTracks()
        {
            FlightCore core = Fight(out RadarSeat me, 7,
                (new Vector3(-3000f, 0f, 15000f), 0f), (new Vector3(0f, 0f, 15000f), 0f), (new Vector3(3000f, 0f, 15000f), 0f));
            me.Next = RadarCommand.TrackWhileScan;
            Run(core, 12f);
            JetRadar r = core.RadarOf(1);
            Assert.AreEqual(RadarMode.TrackWhileScan, r.Mode);
            var who = Firm(r).Select(t => r.TryTruth(t.Id, out int id) ? id : 0).OrderBy(x => x).ToList();
            Debug.Log($"[RADAR] TWS after 12 s: {r.Tracks.Count} tracks, firm on {string.Join(", ", who)}; {r.Looks} looks");
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, who, "one firm track on each of the three");
            Assert.AreEqual(0, r.LockedAircraftId, "track-while-scan locks nothing");
        }

        [Test]
        public void ALock_GivesSingleTargetTrack_OnOne_AndUnlockReturnsToTheSearch()
        {
            FlightCore core = Fight(out RadarSeat me, 7,
                (new Vector3(-3000f, 0f, 15000f), 0f), (new Vector3(0f, 0f, 15000f), 0f), (new Vector3(3000f, 0f, 15000f), 0f));
            me.Next = RadarCommand.TrackWhileScan;
            Run(core, 12f);
            JetRadar r = core.RadarOf(1);
            RadarTrack middle = Firm(r).Single(t => r.TryTruth(t.Id, out int id) && id == 3);
            me.Next = RadarCommand.Lock;
            me.NextTrack = middle.Id;
            Run(core, 5f);
            Assert.AreEqual(RadarMode.SingleTarget, r.Mode);
            Assert.AreEqual(3, r.LockedAircraftId);
            Assert.AreEqual(1, r.Tracks.Count, "the others coast and drop: the beam is on one");
            Assert.AreEqual(r.LockedTrackId, r.Tracks[0].Id);

            me.Next = RadarCommand.Unlock;
            Run(core, 0.1f);
            Assert.AreEqual(RadarMode.TrackWhileScan, r.Mode, "back to the search it came from");
            Assert.AreEqual(0, r.LockedAircraftId);
        }

        [Test]
        public void ALock_IsRefused_OnATentativeOrUnknownTrack()
        {
            FlightCore core = Fight(out RadarSeat me, 7, (new Vector3(0f, 0f, 15000f), 0f));
            me.Next = RadarCommand.Lock;
            me.NextTrack = 999;
            Run(core, 0.1f);
            Assert.AreEqual(RadarMode.RangeWhileSearch, core.RadarOf(1).Mode);
        }

        [Test]
        public void Dogfight_LocksTheNearestContactAhead()
        {
            FlightCore core = Fight(out RadarSeat me, 9, (new Vector3(200f, 0f, 6000f), 0f), (new Vector3(-200f, 0f, 3000f), 0f));
            me.Next = RadarCommand.Dogfight;
            Run(core, 2f);
            JetRadar r = core.RadarOf(1);
            Assert.AreEqual(RadarMode.SingleTarget, r.Mode);
            Assert.AreEqual(3, r.LockedAircraftId, "the one at 3 km, not 6 km");
        }

        [Test]
        public void ALostLock_ReturnsToTheSearchMode()
        {
            // A jet head-on, offset so they pass: once it is behind, outside the gimbal, the looks miss and the lock goes.
            FlightCore core = Fight(out RadarSeat me, 9, (new Vector3(600f, 300f, 6000f), 180f));
            me.Next = RadarCommand.Dogfight;
            Run(core, 2f);
            JetRadar r = core.RadarOf(1);
            Assert.AreEqual(2, r.LockedAircraftId);
            Run(core, 25f);
            Assert.AreEqual(RadarMode.Dogfight, r.Mode, "lost, back to dogfight search");
            Assert.AreEqual(0, r.LockedAircraftId);
        }

        [Test]
        public void TheSameSeed_GivesTheSameTracks()
        {
            string Picture(ulong seed)
            {
                FlightCore core = Fight(out RadarSeat me, seed,
                    (new Vector3(-3000f, 0f, 25000f), 0f), (new Vector3(0f, 2000f, 30000f), 180f), (new Vector3(4000f, 0f, 35000f), 90f));
                me.Next = RadarCommand.TrackWhileScan;
                Run(core, 20f);
                return string.Join(";", core.RadarOf(1).Tracks.Select(t => $"{t.Id}:{t.State}:{t.Position.X:R},{t.Position.Y:R},{t.Position.Z:R}"));
            }
            string a = Picture(21), b = Picture(21), c = Picture(22);
            Assert.IsNotEmpty(a);
            Assert.AreEqual(a, b, "same seed, same tracks to the last bit");
            Assert.AreNotEqual(a, c, "another seed, other noise");
        }

        [Test]
        public void AReset_ClearsTheRadar_AndOff_IsSilent()
        {
            FlightCore core = Fight(out RadarSeat me, 7, (new Vector3(0f, 0f, 15000f), 0f));
            me.Next = RadarCommand.TrackWhileScan;
            Run(core, 6f);
            JetRadar r = core.RadarOf(1);
            Assert.Greater(r.Tracks.Count, 0);
            me.ResetNext = true;
            core.Step();
            Assert.AreEqual(RadarMode.RangeWhileSearch, r.Mode, "back to the start mode");
            Assert.AreEqual(0, r.Tracks.Count, "a respawned jet remembers nothing");

            me.Next = RadarCommand.Off;
            core.Step();
            long looks = r.Looks;
            Run(core, 5f);
            Assert.AreEqual(RadarMode.Off, r.Mode);
            Assert.IsFalse(r.Emitting);
            Assert.AreEqual(looks, r.Looks, "off: no looks");
        }
    }
}
