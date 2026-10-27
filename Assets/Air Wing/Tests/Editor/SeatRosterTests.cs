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
    /// <summary>The seat cycle (W42, W18): who flies what, and what happens to the aircraft the player leaves.</summary>
    public class SeatRosterTests
    {
        static readonly PlayerSeatSettings Settings = new PlayerSeatSettings
        {
            LeverRatePerSecond = 0.5f,
            AfterburnerDetent = 0.9f,
            Instructor = new InstructorSettings { RollGain = 2.5f, PitchGain = 4f, WingsLevelBlendDeg = 5f },
        };

        static AircraftProfile F18E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static FidelitySheet Sheet() => new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);

        /// <summary>Three jets 1 km apart at 5,000 m, each on a hold seat at its own throttle; the player in jet 1.</summary>
        static (FlightCore core, PlayerSeat player, SeatRoster roster) Three(float jet2HeightM = 5000f)
        {
            AircraftProfile e = F18E();
            FidelitySheet sheet = Sheet();
            var core = new FlightCore(new SubstepClock(0.02, 4), new FlatSurface(0f));
            var player = new PlayerSeat(Settings, 0.02, startLever: 0.5f);
            for (int id = 1; id <= 3; id++)
            {
                var start = AircraftFactory.LevelStart(e, sheet, new Vector3(1000f * id, id == 2 ? jet2HeightM : 5000f, 0f), 0f, 200f, 3000f, 0.5f);
                core.Add(AircraftFactory.Build(id, e, sheet, start), id == 1 ? (ISeat)player : new HoldSeat(new PilotInputs { Throttle = 0.2f * id }));
            }
            return (core, player, new SeatRoster(core, player, playerAircraftId: 1));
        }

        [Test]
        public void TheCycle_StepsThroughTheAircraftInIdOrder_AndWrapsRound()
        {
            var (core, _, roster) = Three();
            core.Step();
            Assert.AreEqual(2, roster.Cycle());
            Assert.AreEqual(3, roster.Cycle());
            Assert.AreEqual(1, roster.Cycle(), "wraps round to the first");
            Assert.AreSame(roster.Player, core.SeatOf(1));
        }

        [Test]
        public void TheAircraftLeft_GoesToItsOwnSeat_OrHoldsWhatThePlayerGaveIt()
        {
            var (core, player, roster) = Three();
            var own = new HoldSeat(new PilotInputs { Throttle = 0.9f });
            roster.SetOwnSeat(2, own);
            Assert.AreSame(own, core.SeatOf(2), "an own seat flies the jet at once when the player is not in it");
            core.Step();
            float playerThrottle = core.Find(1).LastInputs.Throttle;

            roster.TakeSeat(2);
            Assert.AreSame(player, core.SeatOf(2), "the player is in 2");
            ISeat left = core.SeatOf(1);
            Assert.AreEqual(SeatKind.Hold, left.Kind, "jet 1 had no own seat: it holds");
            core.Step();
            Assert.AreEqual(playerThrottle, core.Find(1).LastInputs.Throttle, 1e-6f, "it holds the throttle the player left");

            roster.TakeSeat(1);
            Assert.AreSame(own, core.SeatOf(2), "jet 2 goes back to its own seat");
            Assert.AreEqual(1, roster.PlayerAircraftId);
        }

        [Test]
        public void ThePlayerTakesOver_AtTheThrottleAndSwitchesTheJetHad()
        {
            var (core, player, roster) = Three();
            core.Step();
            roster.TakeSeat(3);   // jet 3 held 0.6 of military
            core.Step();
            Assert.AreEqual(0.6f, core.Find(3).LastInputs.Throttle, 1e-4f, "the lever starts at the jet's throttle: nothing jumps");
            Assert.IsFalse(core.Find(3).LastInputs.Afterburner);
            Assert.AreEqual(SeatKind.Player, core.Inputs.All[core.Inputs.Count - 1].Seat, "the log names the player for jet 3 from the next tick");
        }

        [Test]
        public void TheCycle_SkipsACrashedAircraft_AndTheListSaysWhoFliesWhat()
        {
            var (core, _, roster) = Three(jet2HeightM: -5f);   // jet 2 starts under the sea: it meets the surface on the first tick
            core.Step();
            Assert.AreEqual(FlightStatus.Crashed, core.Find(2).State.Status);
            Assert.AreEqual(3, roster.Cycle(), "2 is down: on to 3");
            Assert.AreEqual("HOLD", roster.Describe(1));
            Assert.AreEqual("YOU", roster.Describe(3));
        }
    }
}
