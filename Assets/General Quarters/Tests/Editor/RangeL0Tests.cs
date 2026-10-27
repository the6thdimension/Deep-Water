using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Captain;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Editor;
using DeepWater.GeneralQuarters.Examples;
using DeepWater.GeneralQuarters.ScriptableObjects;
using Debug = UnityEngine.Debug;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// Slice 5, the range: the AI captain's named rules, the TAO's picking logic, the loadout and raid assets, the raid
    /// fix the range turned up, and the Phase 1 budget run headless with the real loadout. Measurements carry a [GQ] tag.
    /// </summary>
    public class RangeL0Tests
    {
        static readonly OwnShipState Own = new OwnShipState { Position = Vector3.zero, Velocity = Vector3.zero, HeadingDeg = 0f };

        static ShipLoadoutSO LoadoutAsset() => AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset);
        static AiCaptainProfileData AiProfile() => AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset).Bake();

        static TruthTarget Missile(int id, Vector3 at, Vector3 velocity) =>
            new TruthTarget { Id = id, Kind = TargetKind.AntiShipMissile, Identity = Identity.Hostile, Position = at, Velocity = velocity, Alive = true };

        /// <summary>Step the AI captain and the combat core together over a fixed world for <paramref name="ticks"/>.</summary>
        static void Run(CombatSystem cs, IAiCaptain ai, Seat seat, List<TruthTarget> world, int ticks)
        {
            var kills = new List<KillReport>();
            for (int i = 0; i < ticks; i++)
            {
                ai.Step(cs, Own, seat);
                kills.Clear();
                cs.Step(Own, world, new List<ShipHit>(), kills);
            }
        }

        // ── The assets ─────────────────────────────────────────────────────

        [Test]
        public void LoadoutAsset_BakesTheFlightIIShip_WithItsSources()
        {
            ShipLoadoutSO so = LoadoutAsset();
            Assert.IsNotNull(so, "run Deep Water → General Quarters → Build Range Assets");
            CombatLoadout l = so.Bake();
            Assert.AreEqual(2, l.Guns.Count(g => g.Weapon == WeaponClass.Ciws), "Flight I/II: two CIWS");
            Assert.AreEqual(0f, l.Guns.First(g => g.Name == "CIWS fwd").FacingDeg);
            Assert.AreEqual(180f, l.Guns.First(g => g.Name == "CIWS aft").FacingDeg);
            Assert.AreEqual(32, l.Vls.Loads.Single(x => x.Weapon == WeaponClass.Essm).Rounds);
            Assert.AreEqual(DoctrineMode.SemiAuto, l.DefaultDoctrine.Single(d => d.Weapon == WeaponClass.Essm).Mode, "ESSM waits for the TAO");
            Assert.AreEqual(DoctrineMode.Auto, l.DefaultDoctrine.Single(d => d.Weapon == WeaponClass.Ciws).Mode, "CIWS on Auto (G14)");
            StringAssert.Contains("AUTHORED", so.provenance);
            StringAssert.Contains("public", so.provenance);
            var cs = new CombatSystem(l);
            Assert.IsFalse(cs.Fidelity.Any(f => f.FellBack), "every element exists at L0");
        }

        [Test]
        public void RaidAsset_StartsWhereTheRangeSays()
        {
            RaidScenarioSO raid = AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(Ddg51CombatData.RaidDir + "/Raid_Saturation.asset");
            Assert.AreEqual(24, raid.threats.Length);
            RaidThreatSpec[] shifted = raid.StartingAt(1000);
            for (int i = 0; i < shifted.Length; i++) Assert.AreEqual(raid.threats[i].LaunchTick + 1000, shifted[i].LaunchTick);
            Assert.AreEqual(0, raid.threats[0].LaunchTick, "the asset itself is unchanged");
        }

        // ── Scripted raid fix (found on the range) ──────────────────────────────

        [Test]
        public void Raid_AircraftAtAltitude_FlyTheirPass_InsteadOfVanishingAtLaunch()
        {
            var specs = new[] { new RaidThreatSpec { Kind = TargetKind.Aircraft, Identity = Identity.Hostile, BearingDeg = 0f, RangeM = 80_000f, AltitudeM = 6_000f, SpeedMps = 250f } };
            var raid = new ScriptedRaid(specs, Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            raid.Step(0, 0.02f, Own, truth, hits);
            Assert.AreEqual(1, truth.Count, "slant range 80.2 km at launch is not 'flown out of the scenario'");
            long tick = 1;
            for (; tick < 40_000 && raid.Active > 0; tick++) raid.Step(tick, 0.02f, Own, truth, hits);
            float flown = tick * 0.02f * 250f;
            Assert.AreEqual(0, raid.Active, "it leaves after the pass");
            Assert.AreEqual(160_000f, flown, 300f, "in and out: twice the launch range");
        }

        // ── AI captain (G19, G27) ───────────────────────────────────────────

        [Test]
        public void AiCaptain_C1_FollowsTheRoute_WhenNothingIsInbound()
        {
            var cs = new CombatSystem(LoadoutAsset().Bake());
            var ai = new AiCaptainL0(AiProfile());
            Run(cs, ai, Seat.Captain, new List<TruthTarget>(), 1);
            Assert.AreEqual(CourseMode.Route, cs.Helm.Course);
            Assert.AreEqual(AiProfile().RouteId, cs.Helm.RouteId);
            Assert.AreEqual(20f, cs.Helm.SpeedKn);
            EngagementEvent d = cs.Log.Events.Single(e => e.Kind == EventKind.Decision);
            StringAssert.Contains("C1", d.Rule);
            Assert.AreEqual(OrderIssuer.AiCaptain, cs.Orders.All.Single().Issuer);
            Run(cs, ai, Seat.Captain, new List<TruthTarget>(), 100);
            Assert.AreEqual(1, cs.Orders.All.Count, "already on the route: no second order");
        }

        [Test]
        public void AiCaptain_C2_RingsFlank_AndC3_OpensTheArcs()
        {
            var cs = new CombatSystem(LoadoutAsset().Bake());
            var ai = new AiCaptainL0(AiProfile());
            cs.Issue(Order.SteerCourse(OrderIssuer.Scenario, 0f));
            // A sea-skimmer dead ahead, closing: in arc of the forward CIWS and the Mk 45 only (the aft CIWS is blind ±30° off the bow).
            var world = new List<TruthTarget> { Missile(1, new Vector3(0f, 5f, 20_000f), new Vector3(0f, 0f, -290f)) };
            Run(cs, ai, Seat.Captain, world, 60);

            EngagementEvent flank = cs.Log.Events.First(e => e.Kind == EventKind.Decision && e.Rule.Contains("C2"));
            Assert.AreEqual(SpeedMode.Bell, cs.Helm.Speed);
            Assert.AreEqual(EngineBell.AheadFlank, cs.Helm.Bell);
            Assert.IsTrue(cs.Log.IsComplete(flank.Id), "the decision traces to the track that caused it");

            EngagementEvent arcs = cs.Log.Events.First(e => e.Kind == EventKind.Decision && e.Rule.Contains("C3"));
            Debug.Log($"[GQ] AI captain: {flank.Text} | {arcs.Text}");
            Assert.AreEqual(CourseMode.Hold, cs.Helm.Course);
            Assert.AreEqual(30f, cs.Helm.CourseDeg, 0.01f, "the gentlest turn that brings the aft CIWS to bear: 030 (starboard first on a tie)");
        }

        [Test]
        public void ArcMath_CountsMountsThatBear()
        {
            var cs = new CombatSystem(LoadoutAsset().Bake());
            var threats = new List<RankedThreat> { new RankedThreat { Track = new Track { Position = new Vector3(0f, 5f, 10_000f) } } };
            Assert.AreEqual(2, ArcMath.PairsInArc(threats, Own, 0f, cs.Guns), "dead ahead: forward CIWS and Mk 45");
            Assert.AreEqual(3, ArcMath.PairsInArc(threats, Own, 90f, cs.Guns), "on the beam: all three anti-air mounts");
            Assert.AreEqual(1, ArcMath.PairsInArc(threats, Own, 180f, cs.Guns), "dead astern: the aft CIWS alone");
            cs.Guns.First(g => g.Profile.Name == "CIWS aft").Online = false;
            Assert.AreEqual(2, ArcMath.PairsInArc(threats, Own, 90f, cs.Guns), "a mount that is offline does not count");
        }

        [Test]
        public void AiCaptain_T1_ConfirmsASemiAutoProposal_AfterItsDelay()
        {
            var cs = new CombatSystem(LoadoutAsset().Bake());
            var ai = new AiCaptainL0(AiProfile());
            var world = new List<TruthTarget> { Missile(1, new Vector3(0f, 5f, 20_000f), new Vector3(0f, 0f, -290f)) };
            Run(cs, ai, Seat.Tao, world, 300);

            EngagementEvent proposed = cs.Log.Events.First(e => e.Kind == EventKind.Proposed);
            EngagementEvent confirm = cs.Log.Events.First(e => e.Kind == EventKind.Decision && e.Rule.Contains("T1"));
            EngagementEvent launched = cs.Log.Events.First(e => e.Kind == EventKind.Launched);
            Assert.GreaterOrEqual((confirm.Tick - proposed.Tick) * cs.TickSeconds, AiProfile().ConfirmDelayS - 1e-3f, "waits the delay");
            Assert.AreEqual(proposed.Id, confirm.CauseA, "the confirmation cites the proposal");
            Assert.AreEqual(OrderIssuer.AiCaptain, cs.Orders.All.First(o => o.Kind == OrderKind.Engage).Issuer);
            Assert.GreaterOrEqual(launched.Tick, confirm.Tick);
            Assert.AreEqual(1, cs.Log.Count(EventKind.Launched), "one missile for one confirmation (shoot-look-shoot)");
        }

        // ── TAO logic ──────────────────────────────────────────────────────

        [Test]
        public void Tao_CyclesLiveTracks_AndPicksTheMostUrgent()
        {
            var tracks = new List<Track>
            {
                new Track { Id = 1, Active = true, Identity = Identity.Hostile, Position = new Vector3(0, 5, 20_000), Velocity = new Vector3(0, 0, -290) },
                new Track { Id = 2, Active = true, Destroyed = true },
                new Track { Id = 3, Active = false },
                new Track { Id = 4, Active = true, Identity = Identity.Hostile, Position = new Vector3(8_000, 5, 0), Velocity = new Vector3(-290, 0, 0) },
                new Track { Id = 5, Active = true, Identity = Identity.Unknown, Position = new Vector3(1_000, 5, 0), Velocity = new Vector3(-290, 0, 0) },
            };
            Assert.AreEqual(1, TaoLogic.Cycle(tracks, -1, +1), "nothing selected: the first");
            Assert.AreEqual(4, TaoLogic.Cycle(tracks, 1, +1), "skips the destroyed and the dropped");
            Assert.AreEqual(1, TaoLogic.Cycle(tracks, 5, +1), "wraps");
            Assert.AreEqual(5, TaoLogic.Cycle(tracks, 1, -1), "and backwards");
            Assert.AreEqual(4, TaoLogic.MostUrgent(tracks, Own, 0, 0.02f), "the hostile with the soonest closest approach (the nearer unknown is not hostile)");
            Assert.AreEqual(-1, TaoLogic.Cycle(new List<Track>(), -1, 1));
        }

        // ── The Phase 1 budget, headless, with the real loadout ────────────────────

        static (ulong fix, CombatSystem cs, ScriptedRaid raid, List<KillReport> kills, double meanMs, double maxMs) Saturation(ulong seed)
        {
            CombatLoadout loadout = LoadoutAsset().Bake();
            loadout.Seed = seed;
            var cs = new CombatSystem(loadout);
            var ai = new AiCaptainL0(AiProfile());
            RaidScenarioSO scenario = AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(Ddg51CombatData.RaidDir + "/Raid_Saturation.asset");
            var raid = new ScriptedRaid(scenario.StartingAt(0), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var all = new List<KillReport>();
            var watch = new Stopwatch();
            double max = 0, total = 0;
            int steps = 0;
            for (int i = 0; i < 12_000; i++)
            {
                ai.Step(cs, Own, Seat.Tao);   // the player has the conn; the AI is TAO and confirms the ESSM proposals
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                watch.Restart();
                cs.Step(Own, truth, hits, kills);
                watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds;
                total += ms; steps++;
                if (ms > max) max = ms;
                raid.ApplyKills(kills);
                all.AddRange(kills);
                if (raid.Resolved && cs.Vls.InFlight.Count == 0) break;
            }
            return (cs.Fix, cs, raid, all, total / steps, max);
        }

        [Test]
        public void Saturation_WithTheRealLoadout_ResolvesExplained_Repeats_AndStaysInBudget()
        {
            Saturation(1442);   // warm-up
            var (fixA, cs, raid, kills, mean, max) = Saturation(1442);
            var (fixB, _, _, _, _, _) = Saturation(1442);
            RaidSummary s = RaidSummary.Of("Saturation 24", cs, raid, kills, LoadoutAsset().Bake(), 0);
            Debug.Log($"[GQ] saturation, real loadout, AI as TAO: {s.Killed} killed, {s.Leaked} hit of {s.Threats}; kills ESSM {s.KillsBy[(int)WeaponClass.Essm]}, " +
                      $"Mk 45 {s.KillsBy[(int)WeaponClass.Mk45]}, CIWS {s.KillsBy[(int)WeaponClass.Ciws]}; {s.MissilesLaunched} ESSM, {s.Bursts} bursts; hull {s.HullHitPoints:0}; " +
                      $"{s.AiDecisions} AI decisions; core {mean:0.000} ms mean, {max:0.000} ms max (Editor); Fix {fixA:X16}");
            Assert.IsTrue(raid.Resolved);
            Assert.AreEqual(24, s.Killed + s.Leaked);
            Assert.AreEqual(s.Killed, kills.Count);
            foreach (EngagementEvent e in cs.Log.Events.Where(e => e.Kind == EventKind.Kill))
                Assert.IsTrue(cs.Log.IsComplete(e.Id), $"kill {e.Id} explained to its roots: {e.Text}");
            Assert.Greater(s.AiDecisions, 0, "the AI TAO confirmed proposals");
            Assert.AreEqual(fixA, fixB, "same seed, same orders (the AI's included), same truth: same Fix");
            Assert.LessOrEqual(mean, 1.0, "combat-core time per fixed step (G16)");
        }
    }

    /// <summary>The range's "Tao" and "Range" maps in the project-wide actions, driven by virtual devices.</summary>
    public class RangeBindingTests : InputTestFixture
    {
        const string ActionsPath = "Assets/RHMILCOM.inputactions";
        const string GenericPad = @"{
            ""name"": ""GQTestGenericPad8"", ""extend"": ""Joystick"",
            ""controls"": [
                { ""name"": ""hat"", ""layout"": ""Dpad"", ""offset"": 0, ""bit"": 0, ""sizeInBits"": 4, ""format"": ""BIT"" },
                { ""name"": ""button2"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 5, ""format"": ""BIT"" },
                { ""name"": ""button3"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 6, ""format"": ""BIT"" },
                { ""name"": ""button6"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 9, ""format"": ""BIT"" },
                { ""name"": ""button7"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 10, ""format"": ""BIT"" },
                { ""name"": ""button8"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 11, ""format"": ""BIT"" }
            ]}";

        InputActionAsset _asset;
        InputActionMap _tao, _range;

        public override void Setup()
        {
            base.Setup();
            InputSystem.RegisterLayout(GenericPad);
            _asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            _tao = _asset.FindActionMap(RangeInput.TaoMap, true);
            _range = _asset.FindActionMap(RangeInput.RangeMap, true);
        }

        public override void TearDown()
        {
            _tao?.Disable();
            _range?.Disable();
            Object.DestroyImmediate(_asset);
            base.TearDown();
        }

        InputAction T(string name) => _tao.FindAction(name, true);
        InputAction R(string name) => _range.FindAction(name, true);

        [Test]
        public void EveryRangeAndTaoAction_HasABindingInAllThreeSchemes()
        {
            var keyboardOnlyZoom = new[] { "ScopeZoomIn", "ScopeZoomOut" };   // the pad has them; the generic pad uses the mouse-free hat for tracks
            foreach (InputAction action in _tao.actions.Concat(_range.actions))
                foreach (string scheme in new[] { "Keyboard&Mouse", "Gamepad", "Joystick" })
                {
                    if (scheme == "Joystick" && keyboardOnlyZoom.Contains(action.name)) continue;
                    Assert.IsTrue(action.bindings.Any(b => b.groups != null && b.groups.Contains(scheme)), $"{action.name} has no {scheme} binding");
                }
        }

        [Test]
        public void Laptop()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            _tao.Enable(); _range.Enable();
            foreach (var (key, action, map) in new[]
            {
                (keyboard.digit1Key, "EngageEssm", _tao), (keyboard.digit2Key, "EngageMk45", _tao), (keyboard.digit3Key, "EngageCiws", _tao),
                (keyboard.qKey, "PrevTrack", _tao), (keyboard.eKey, "NextTrack", _tao), (keyboard.backspaceKey, "CeaseFire", _tao),
                (keyboard.deleteKey, "CeaseFireAll", _tao), (keyboard.oKey, "CycleRoe", _tao), (keyboard.equalsKey, "ScopeZoomIn", _tao),
                (keyboard.tabKey, "SwitchSeat", _range), (keyboard.gKey, "StartRaid", _range), (keyboard.tKey, "TimeCompression", _range),
                (keyboard.cKey, "CameraTarget", _range),
            })
            {
                Press(key);
                Assert.IsTrue(map.FindAction(action, true).WasPerformedThisFrame(), $"{key.name}: {action}");
                Release(key);
            }
        }

        [Test]
        public void XboxController_TapStartSwitchesSeat_HoldStartsTheRaid()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            _tao.Enable(); _range.Enable();

            Press(pad.buttonSouth); Assert.IsTrue(T("EngageEssm").WasPerformedThisFrame(), "A: engage ESSM"); Release(pad.buttonSouth);
            Press(pad.rightShoulder); Assert.IsTrue(T("NextTrack").WasPerformedThisFrame(), "RB: next track"); Release(pad.rightShoulder);
            Press(pad.dpad.up); Assert.IsTrue(T("CycleRoe").WasPerformedThisFrame(), "d-pad up: ROE"); Release(pad.dpad.up);

            Press(pad.startButton);
            currentTime += 0.2;
            Release(pad.startButton);
            Assert.IsTrue(R("SwitchSeat").WasPerformedThisFrame(), "a tap on Start switches seat");
            Assert.IsFalse(R("StartRaid").WasPerformedThisFrame(), "a tap does not start the raid");

            Press(pad.startButton);
            currentTime += 1.2;
            InputSystem.Update();
            Assert.IsTrue(R("StartRaid").WasPerformedThisFrame(), "holding Start a second starts the raid");
            Release(pad.startButton);
            Assert.IsFalse(R("SwitchSeat").WasPerformedThisFrame(), "and the hold is not also a seat switch");

            Press(pad.dpad.down);
            currentTime += 0.2;
            Release(pad.dpad.down);
            Assert.IsTrue(T("CeaseFire").WasPerformedThisFrame(), "tap d-pad down: cease fire on the track");
        }

        [Test]
        public void GenericHidPad()
        {
            var pad = (Joystick)InputSystem.AddDevice("GQTestGenericPad8");
            _tao.Enable(); _range.Enable();
            Press(pad.trigger); Assert.IsTrue(T("EngageEssm").WasPerformedThisFrame(), "trigger: ESSM"); Release(pad.trigger);
            var hatRight = (ButtonControl)pad["hat/right"];
            Press(hatRight); Assert.IsTrue(T("NextTrack").WasPerformedThisFrame(), "hat right: next track"); Release(hatRight);
            var b7 = (ButtonControl)pad["button7"];
            Press(b7); Assert.IsTrue(R("TimeCompression").WasPerformedThisFrame(), "button 7: time compression"); Release(b7);
            var b8 = (ButtonControl)pad["button8"];
            Press(b8); Assert.IsTrue(R("CameraTarget").WasPerformedThisFrame(), "button 8: camera target"); Release(b8);
        }
    }
}
