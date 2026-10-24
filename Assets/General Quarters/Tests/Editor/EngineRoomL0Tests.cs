using System.IO;
using NUnit.Framework;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.EngineRoom;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Editor;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// Engine Room L0 (MMG) and Helm L0 on the DDG-51 hull, against public figures. Each test says whether its
    /// figure was CALIBRATED (fitted, so the test guards against regressions) or is VALIDATION (not fitted).
    /// Sources: Ddg51EngineRoomData. USNI = Stavridis, "Handling the Arleigh Burkes", Proceedings, Oct 1994.
    /// </summary>
    public class EngineRoomL0Tests
    {
        const float KnToMs = 0.514444f, Yard = 0.9144f, Dt = 0.1f;

        static HullLayout Layout() => HullLayout.Parse(File.ReadAllText(Ddg51HullBuilder.LayoutJson));
        static EngineRoomProfileData Room() => Ddg51EngineRoomData.EngineRoom(Layout(), out _);
        static float Lpp => Layout().hydrostatics.length_wl_m;

        [Test]
        public void TopSpeed_Is31KnotsOn100000Shp()
        {
            // CALIBRATED: the appendage factor is fitted to this.
            EngineRoomProfileData p = Room();
            float v = Ddg51EngineRoomData.TopSpeedKn * KnToMs;
            float n = Mmg.SteadyShaftRps(p.Hull, p.Port, 2, v);
            float shp = 2f * Mmg.DeliveredPowerW(p.Port, v, n, p.Port.WakeStraight) / 745.7f / 0.98f;
            Debug.Log($"[GQ] DDG-51 at 31 kn: {n * 60f:0} rpm, {shp:0} shp at the engines (fact file 100,000)");
            Assert.AreEqual(100000f, shp, 1000f);
            Assert.AreEqual(p.MaxShaftRps, n, 1e-3f, "flank revolutions are the 31 kn revolutions");
        }

        [Test]
        public void SelfPropulsion_At18Knots_IsNearTheSimmanSpec()
        {
            // VALIDATION: SIMMAN 2014's appended-PMM spec runs this propeller at 109.1 rpm at 18 kn (full scale).
            EngineRoomProfileData p = Room();
            float rpm = Mmg.SteadyShaftRps(p.Hull, p.Port, 2, 18f * KnToMs) * 60f;
            Debug.Log($"[GQ] DDG-51 self-propulsion at 18 kn: {rpm:0.0} rpm (SIMMAN 2014 spec 109.1)");
            Assert.AreEqual(109.1f, rpm, 0.10f * 109.1f);
        }

        [TestCase(10f, 35f, 500f)]
        [TestCase(15f, 15f, 1000f)]
        [TestCase(30f, 15f, 1200f)]
        public void TurningCircle_TacticalDiameter_MatchesUsni(float knots, float rudderDeg, float usniYards)
        {
            // CALIBRATED: γ_R and ℓ'_R are a least-squares fit to these three (residuals −15 / +19 / −6 %). The
            // figures are a shiphandler's rounded numbers, so the band is ±25 %.
            TurningCircle tc = ManoeuvringSim.TurningCircle(Room(), knots * KnToMs, rudderDeg, Dt, 900f);
            float target = usniYards * Yard;
            Debug.Log($"[GQ] DDG-51 turn {knots:0} kn, {rudderDeg:0}° rudder: advance {tc.AdvanceM:0} m, tactical diameter {tc.TacticalDiameterM:0} m " +
                      $"(USNI {target:0} m, {(tc.TacticalDiameterM / target - 1f) * 100f:+0;-0} %), steady radius {tc.SteadyRadiusM:0} m at {tc.SteadySpeedMs / KnToMs:0.0} kn, drift {tc.SteadyDriftDeg:0.0}°");
            Assert.AreEqual(target, tc.TacticalDiameterM, 0.25f * target);
        }

        [Test]
        public void TurningCircle_AtServiceSpeed_MeetsImoCriteria()
        {
            // VALIDATION: IMO MSC.137(76): 35° turn at ≥ 90 % of top speed: advance < 4.5 L, tactical diameter < 5 L.
            TurningCircle tc = ManoeuvringSim.TurningCircle(Room(), 28f * KnToMs, 35f, Dt, 900f);
            Debug.Log($"[GQ] DDG-51 IMO turn at 28 kn: advance {tc.AdvanceM / Lpp:0.00} L, tactical diameter {tc.TacticalDiameterM / Lpp:0.00} L");
            Assert.Less(tc.AdvanceM, 4.5f * Lpp);
            Assert.Less(tc.TacticalDiameterM, 5f * Lpp);
        }

        [Test]
        public void ZigZag20_At30Knots_IsNearPublished5415Results()
        {
            // VALIDATION: 5415M 20/20 at Fn 0.413: CFD 4.5° / 4.8° (Shen, Wan & Carrica 2014); MMG 5.3° / 5.1° (Dash et al. 2015).
            ZigZag z = ManoeuvringSim.ZigZagTest(Room(), 30f * KnToMs, 20f, 20f, Dt);
            Debug.Log($"[GQ] DDG-51 20/20 zig-zag at 30 kn: overshoots {z.FirstOvershootDeg:0.0}° / {z.SecondOvershootDeg:0.0}° (CFD 4.5 / 4.8, MMG 5.3 / 5.1)");
            Assert.That(z.FirstOvershootDeg, Is.InRange(3f, 8f));
            Assert.That(z.SecondOvershootDeg, Is.InRange(3f, 8f));
        }

        [Test]
        public void ZigZag10_AtServiceSpeed_MeetsImoCriteria()
        {
            // VALIDATION: IMO MSC.137(76) 10/10: first overshoot ≤ 10° when L/V < 10 s; second ≤ 25°.
            float v = 28f * KnToMs;
            ZigZag z = ManoeuvringSim.ZigZagTest(Room(), v, 10f, 10f, Dt);
            Debug.Log($"[GQ] DDG-51 10/10 zig-zag at 28 kn (L/V {Lpp / v:0.0} s): overshoots {z.FirstOvershootDeg:0.0}° / {z.SecondOvershootDeg:0.0}°");
            Assert.Less(Lpp / v, 10f);
            Assert.LessOrEqual(z.FirstOvershootDeg, 10f);
            Assert.LessOrEqual(z.SecondOvershootDeg, 25f);
        }

        [Test]
        public void CrashStop_FromAheadFull_InAboutAMinute_Under600Yards()
        {
            // Time CALIBRATED (astern thrust fitted to "about 1 min", USNI); distance VALIDATION (< 600 yd, USNI).
            Run run = Drive(20f, HelmCommand.Apply(HelmCommand.Stopped(0f), Order.EngineOrder(OrderIssuer.Captain, EngineBell.BackFull)),
                400f, (s, t) => s.Body.U <= 0f);
            Debug.Log($"[GQ] DDG-51 crash stop from ahead full (20 kn), back full: {run.Seconds:0.0} s over {run.State.Z:0} m (USNI ~60 s, < {600f * Yard:0} m)");
            Assert.AreEqual(60f, run.Seconds, 10f);
            Assert.Less(run.State.Z, 600f * Yard);
        }

        [Test]
        public void Acceleration_FromAllStop_ToFlank_Under90Seconds()
        {
            // VALIDATION: "all stop to ahead flank in less than 90 seconds" (USNI). Read both ways: revolutions and speed.
            Run run = Drive(0f, HelmCommand.Apply(HelmCommand.Stopped(0f), Order.EngineOrder(OrderIssuer.Captain, EngineBell.AheadFlank)),
                300f, (s, t) => s.Body.U >= 30f * KnToMs);
            EngineRoomProfileData p = Room();
            float revsSeconds = p.MaxShaftRps / p.ShaftRateRpsPerS;
            Debug.Log($"[GQ] DDG-51 all stop to ahead flank: flank revolutions in {revsSeconds:0} s, 30 kn at {run.Seconds:0} s");
            Assert.Less(revsSeconds, 90f);
            Assert.Less(run.Seconds, 90f);
        }

        [Test]
        public void SteadyHeel_InAFullRudderTurnFromFlank_IsNearUsni()
        {
            // VALIDATION: "8–10 degrees of heel at full speed with full rudder" (USNI). Steady heel from the roll moment
            // the drive applies (hydrodynamic sway force m·a_y: rudders at their depth, the rest at half draught) over
            // m·g·GM. The transient outward swing is larger; the PhysX run measures it (a known L0 gap, see ROADMAP).
            HullLayout.Hydrostatics h = Layout().hydrostatics;
            EngineRoomProfileData p = Room();
            float zG = h.centre_of_mass[1], heel = 0f;
            HelmCommand flankHardOver = HelmCommand.Apply(HelmCommand.Apply(HelmCommand.Stopped(0f),
                Order.EngineOrder(OrderIssuer.Captain, EngineBell.AheadFlank)), Order.Rudder(OrderIssuer.Captain, 35f));
            Run run = Drive(Ddg51EngineRoomData.TopSpeedKn, flankHardOver, 400f, null,
                (f, s) =>
                {
                    float ay = f.Vdot + p.Hull.XgM * f.Rdot + s.Body.U * s.Body.R;
                    float rudders = f.YRudder0 + f.YRudder1;
                    float moment = (h.mass_kg * ay - rudders) * (zG + 0.5f * h.draft_m) + rudders * (zG + p.PortRudder.DepthM);
                    heel = moment / (h.mass_kg * 9.81f * h.gm_m) * Mathf.Rad2Deg;
                });
            Debug.Log($"[GQ] DDG-51 steady heel, flank and full rudder: {heel:0.0}° to port at {run.State.Body.U / KnToMs:0.0} kn (USNI 8–10°)");
            Assert.That(heel, Is.InRange(6f, 10f), "outward (to port in a starboard turn), near Barry's 8–10°");
        }

        [Test]
        public void CourseHold_OnThisHull_TurnsNinetyDegreesWithoutOvershoot()
        {
            float overshoot = 0f;
            Run run = Drive(20f, HelmCommand.Apply(HelmCommand.Stopped(0f), Order.Helm(OrderIssuer.Captain, 90f, 20f)), 300f, null, null,
                s => overshoot = Mathf.Max(overshoot, s.HeadingRad * Mathf.Rad2Deg - 90f));
            float heading = run.State.HeadingRad * Mathf.Rad2Deg;
            Debug.Log($"[GQ] DDG-51 course hold 000 → 090 at 20 kn: heading {heading:0.0}° after {run.Seconds:0} s, overshoot {overshoot:0.0}°, speed {run.State.Body.U / KnToMs:0.0} kn");
            Assert.AreEqual(90f, heading, 1f);
            Assert.Less(overshoot, 2f);
            Assert.AreEqual(20f, run.State.Body.U / KnToMs, 1f, "speed hold brings her back to 20 kn after the turn");
        }

        [Test]
        public void Route_OnThisHull_TurnsOntoEachLeg()
        {
            // North 4 km, east 4 km, south 4 km at 20 kn: after each wheel-over she should run down the next leg.
            var p = Room();
            var room = new EngineRoomL0(default, p);
            var helm = HelmFactory.Create(FidelityLevel.L0, Ddg51EngineRoomData.Helm(Layout(), out _), room);
            helm.AddRoute(1, new Route(new Vector2(0f, 0f), new Vector2(0f, 4000f), new Vector2(4000f, 4000f), new Vector2(4000f, 0f)));
            var command = HelmCommand.Apply(HelmCommand.Stopped(0f), Order.FollowRoute(OrderIssuer.Captain, 1, 20f));
            room.SetShafts(room.SteadyShaftRps(20f * KnToMs));
            var s = new ManoeuvringSim.State { Body = new MmgState { U = 20f * KnToMs } };
            float worstOnLeg3 = 0f;
            for (float t = 0f; t < 1500f; t += Dt)
            {
                var motion = new ShipMotion { Position = new Vector3(s.X, 0f, s.Z), HeadingDeg = s.HeadingRad * Mathf.Rad2Deg, SurgeMs = s.Body.U, SwayMs = s.Body.Vm, YawRateRadS = s.Body.R };
                HelmOutput o = helm.Step(command, motion, Dt);
                ManoeuvringSim.Integrate(ref s, room.Step(o, s.Body, Dt), Dt, out _);
                if (o.Waypoint == 3 && s.Z < 3000f && s.Z > 500f) worstOnLeg3 = Mathf.Max(worstOnLeg3, Mathf.Abs(s.X - 4000f));
            }
            Debug.Log($"[GQ] DDG-51 route at 20 kn: worst cross-track on the southbound leg {worstOnLeg3:0} m; ended at ({s.X:0}, {s.Z:0})");
            Assert.Less(worstOnLeg3, 50f, "settled onto the southbound leg");
            Assert.Less(s.Z, 1000f, "and running down it");
        }

        [Test]
        public void Manoeuvring_IsDeterministic()
        {
            TurningCircle a = ManoeuvringSim.TurningCircle(Room(), 20f * KnToMs, 25f, Dt, 600f);
            TurningCircle b = ManoeuvringSim.TurningCircle(Room(), 20f * KnToMs, 25f, Dt, 600f);
            Assert.AreEqual(a.TacticalDiameterM, b.TacticalDiameterM);
            Assert.AreEqual(a.SteadyRadiusM, b.SteadyRadiusM);
        }

        // ── a ship driven by its own Helm and Engine Room, without the sea ──

        struct Run
        {
            public ManoeuvringSim.State State;
            public float Seconds;
        }

        static Run Drive(float startKn, HelmCommand command, float seconds, System.Func<ManoeuvringSim.State, float, bool> stop,
            System.Action<MmgResult, ManoeuvringSim.State> onForces = null, System.Action<ManoeuvringSim.State> onStep = null)
        {
            var room = new EngineRoomL0(default, Room());
            var helm = HelmFactory.Create(FidelityLevel.L0, Ddg51EngineRoomData.Helm(Layout(), out _), room);
            room.SetShafts(room.SteadyShaftRps(startKn * KnToMs));
            var s = new ManoeuvringSim.State { Body = new MmgState { U = startKn * KnToMs } };
            float t = 0f;
            for (; t < seconds; t += Dt)
            {
                var motion = new ShipMotion { HeadingDeg = s.HeadingRad * Mathf.Rad2Deg, SurgeMs = s.Body.U, SwayMs = s.Body.Vm, YawRateRadS = s.Body.R };
                HelmOutput o = helm.Step(command, motion, Dt);
                MmgResult f = room.Step(o, s.Body, Dt);
                onForces?.Invoke(f, s);
                ManoeuvringSim.Integrate(ref s, f, Dt, out _);
                onStep?.Invoke(s);
                if (stop != null && stop(s, t)) break;
            }
            return new Run { State = s, Seconds = t };
        }
    }
}
