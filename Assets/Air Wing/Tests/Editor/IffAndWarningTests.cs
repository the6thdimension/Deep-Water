using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Radar;
using RHRadarSuite.Tracking;
using static DeepWater.AirWing.Tests.RadarTests;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// IFF and the radar warning receiver, SENSORS-CONCEPT slice 2 (J3, J5): a friend usually answers and its track reads
    /// friend; an enemy stays unknown; a jet painted in search and in track hears each, with the right bearing, and the
    /// warning clears once the radar stops.
    /// </summary>
    public class IffAndWarningTests
    {
        static readonly (Vector3, float) FriendLeft = (new Vector3(-3000f, 0f, 15000f), 0f);
        static readonly (Vector3, float) EnemyRight = (new Vector3(3000f, 0f, 15000f), 0f);

        /// <summary>Jet 1 in code 1 with track-while-scan tracks on a friend (jet 2, code 1) and an enemy (jet 3, code 2).</summary>
        static FlightCore Tracked(out RadarSeat[] seats, ulong seed = 7)
        {
            FlightCore core = FightAll(out seats, seed, FriendLeft, EnemyRight);
            core.SetIffCode(1, 1);
            core.SetIffCode(2, 1);
            core.SetIffCode(3, 2);
            seats[0].Next = RadarCommand.TrackWhileScan;
            Run(core, 12f);
            return core;
        }

        static TrackIdentity IdentityOfAircraft(JetRadar r, int aircraftId)
        {
            RadarTrack t = Firm(r).Single(x => r.TryTruth(x.Id, out int who) && who == aircraftId);
            return r.IdentityOf(t.Id);
        }

        [Test]
        public void TheF18E_CarriesIffAndAWarningReceiver()
        {
            AircraftProfile e = E();
            Assert.IsTrue(e.Iff.IsSet);
            Assert.AreEqual(0.95f, e.Iff.ReplyChance);
            Assert.IsTrue(e.WarningReceiver.IsSet);
            Assert.Greater(e.WarningReceiver.SearchHoldS, e.Radar.RangeWhileSearch.FrameS, "a search radar must stay shown between its sweeps");
        }

        [Test]
        public void AFriend_Answers_AndItsTrackReadsFriend_AnEnemyStaysUnknown()
        {
            FlightCore core = Tracked(out RadarSeat[] seats);
            JetRadar r = core.RadarOf(1);
            Assert.AreEqual(TrackIdentity.Unknown, IdentityOfAircraft(r, 2), "nothing asked yet");
            seats[0].Hold = PilotSwitches.Interrogate;
            Run(core, 3f);
            Assert.AreEqual(3, r.Interrogations, "held 3 s, asked once a second");
            Assert.AreEqual(TrackIdentity.Friend, IdentityOfAircraft(r, 2));
            Assert.AreEqual(TrackIdentity.Unknown, IdentityOfAircraft(r, 3), "IFF never says hostile: no answer, still unknown");
        }

        [Test]
        public void AFriend_AnswersNineteenTimesInTwenty()
        {
            FlightCore core = FightAll(out RadarSeat[] seats, 13, FriendLeft);
            core.SetIffCode(1, 4);
            core.SetIffCode(2, 4);
            seats[0].Next = RadarCommand.TrackWhileScan;
            Run(core, 8f);
            seats[0].Hold = PilotSwitches.Interrogate;
            Run(core, 300f);
            JetRadar r = core.RadarOf(1);
            float rate = (float)r.Answers / r.Interrogations;
            Debug.Log($"[IFF] {r.Answers} answers to {r.Interrogations} challenges ({rate:P1})");
            Assert.GreaterOrEqual(r.Interrogations, 299);
            Assert.That(rate, Is.InRange(0.91f, 0.99f), "the transponder's 0.95 over 300 seeded draws");
        }

        [Test]
        public void NoCode_NoAnswer()
        {
            FlightCore core = FightAll(out RadarSeat[] seats, 7, FriendLeft);
            core.SetIffCode(2, 1);   // the friend has a code; the asker has none
            seats[0].Next = RadarCommand.TrackWhileScan;
            Run(core, 12f);
            seats[0].Hold = PilotSwitches.Interrogate;
            Run(core, 5f);
            Assert.AreEqual(0, core.RadarOf(1).Answers);
        }

        [Test]
        public void ALockedFriend_IsAskedAlone()
        {
            FlightCore core = Tracked(out RadarSeat[] seats);
            JetRadar r = core.RadarOf(1);
            RadarTrack friend = Firm(r).Single(x => r.TryTruth(x.Id, out int who) && who == 2);
            seats[0].Next = RadarCommand.Lock;
            seats[0].NextTrack = friend.Id;
            Run(core, 1f);
            Assert.AreEqual(2, r.LockedAircraftId);
            seats[0].Hold = PilotSwitches.Interrogate;
            Run(core, 3f);
            Assert.AreEqual(TrackIdentity.Friend, r.IdentityOf(r.LockedTrackId));
        }

        [Test]
        public void APaintedJet_HearsSearchThenTrack_WithTheRightBearing_ThenQuiet()
        {
            // Jet 2 heads south, nose toward jet 1, 20 km away: jet 1's radar is dead ahead of it.
            FlightCore core = FightAll(out RadarSeat[] seats, 3, (new Vector3(0f, 0f, 20000f), 180f));
            WarningReceiver rwr = core.WarningReceiverOf(2);
            Run(core, 0.5f);
            seats[0].Next = RadarCommand.TrackWhileScan;
            Run(core, 2.5f);
            Assert.AreEqual(1, rwr.Contacts.Count, "one radar paints it");
            WarningContact c = rwr.Contacts[0];
            Debug.Log($"[RWR] search: {c}");
            Assert.AreEqual(PaintKind.Search, c.Kind);
            Assert.AreEqual(1, rwr.EmitterOf(0));
            Assert.Less(Mathf.Abs(c.AzimuthDeg), 3f, "dead ahead");
            Assert.Less(Mathf.Abs(c.ElevationDeg), 6f, "level; the body frame carries the angle of attack (about 3° nose-up)");

            Run(core, 5f);
            JetRadar r = core.RadarOf(1);
            seats[0].Next = RadarCommand.Lock;
            seats[0].NextTrack = Firm(r).Single().Id;
            Run(core, 0.5f);
            Assert.AreEqual(PaintKind.Track, rwr.Strongest, "locked: it hears the track");

            seats[0].Next = RadarCommand.Off;
            Run(core, 0.6f);
            Assert.AreNotEqual(PaintKind.Track, rwr.Strongest, "the track clears half a second after it stops");
            Run(core, 5f);
            Assert.AreEqual(0, rwr.Contacts.Count, "a quiet sky after the search hold");
        }

        [Test]
        public void AJetBehind_HearsNothing_AndTheBearingFollowsTheEmitter()
        {
            // Jet 2 is behind jet 1 (outside its ±60° search): never painted.
            FlightCore core = FightAll(out RadarSeat[] seats, 3, (new Vector3(0f, 0f, -10000f), 0f), (new Vector3(8000f, 0f, 15000f), 180f));
            seats[1].Next = RadarCommand.Off;   // only jet 1 emits
            seats[2].Next = RadarCommand.Off;
            Run(core, 10f);
            Assert.AreEqual(0, core.WarningReceiverOf(2).Contacts.Count);
            // Jet 3, ahead and to the right of jet 1 and heading south, has jet 1 ahead and to its right.
            WarningReceiver rwr = core.WarningReceiverOf(3);
            Assert.AreEqual(1, rwr.Contacts.Count);
            Debug.Log($"[RWR] jet 3 hears {rwr.Contacts[0]}");
            Assert.Greater(rwr.Contacts[0].AzimuthDeg, 10f, "jet 1 lies right of jet 3's nose");
        }

        [Test]
        public void AReset_ClearsTheWarningsAndIdentities()
        {
            FlightCore core = Tracked(out RadarSeat[] seats);
            seats[0].Hold = PilotSwitches.Interrogate;
            Run(core, 2f);
            seats[0].Hold = PilotSwitches.None;
            Assert.Greater(core.RadarOf(1).Answers, 0);
            seats[1].ResetNext = true;
            core.Step();
            Assert.AreEqual(0, core.WarningReceiverOf(2).Contacts.Count, "a respawned jet hears nothing yet");
        }
    }
}
