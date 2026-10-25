using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Modules.Hull;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// Stream C slice C1: Damage Control L1, compartments and flooding (G53–G60). The hull-stability-check ladder for a
    /// damaged ship: closed-form predictions from <c>.claude/skills/hull-stability-check/scripts/flood_predict.py</c>, then
    /// the force model (Sea State's probe jobs carrying the water) brought to rest in EditMode. PhysX is checked on the
    /// range (ROADMAP, slice C1).
    ///
    /// Pass criteria, set before the first measurement: list within 5 % (or 0.2°), trim within 15 % (or 3 cm), sinkage
    /// within 15 % (or 2 cm), GM within 5 %. The predictions are first order (wall-sided, small angles), so the
    /// percentages are what a closed form can promise; the absolute floors keep small numbers from failing on noise.
    /// </summary>
    public class DamageControlL1Tests
    {
        const float Rho = 1025f, G = 9.81f;
        static readonly OwnShipState Upright = new OwnShipState { Position = Vector3.zero, HeadingDeg = 0f };

        static void Within(string what, float predicted, float measured, float fraction, float floor)
        {
            float allowed = Mathf.Max(fraction * Mathf.Abs(predicted), floor);
            Assert.LessOrEqual(Mathf.Abs(measured - predicted), allowed,
                $"{what}: measured {measured:0.####}, predicted {predicted:0.####} (allowed ±{allowed:0.####})");
        }

        // ── The plan ────────────────────────────────────────────────────────

        [Test]
        public void Plan_CutsTheHullIntoSpaces_FromItsOwnColumns()
        {
            // flood_predict.py --list-spaces: 36 spaces holding 19,901 m³; the hull is 23,616 m³ to the deck.
            var plan = new FloodPlan(FloodedHullRig.Loadout().Flooding);
            Assert.AreEqual(12 * 2 + 6 * 2, plan.Spaces.Length, "12 holds and upper spaces, 6 pairs of wings");
            Assert.AreEqual(19_901f, plan.Spaces.Sum(s => s.CapacityM3), 2f, "capacity, m³");
            Assert.AreEqual(23_616f, plan.HullVolumeM3, 2f, "hull volume, m³");
            Assert.AreEqual(276.9f, plan.Spaces[plan.Find("06 wing S")].CapacityM3, 0.2f);
            Assert.AreEqual(1_058.1f, plan.Spaces[plan.Find("06 hold")].CapacityM3, 0.2f);
            Assert.AreEqual(0.85f, plan.Spaces[plan.Find("06 hold")].Permeability, 1e-6f, "machinery (SOLAS)");
            Assert.AreEqual(12, plan.Openings.Count(o => o.Kind == OpeningKind.Hatch));
            Assert.AreEqual(11, plan.Openings.Count(o => o.Kind == OpeningKind.Door));
            Assert.AreEqual(24, plan.Openings.Count(o => o.Kind == OpeningKind.Weather));

            Assert.AreEqual("06 wing S", plan.Spaces[plan.SpaceAt(new Vector3(8f, -3f, 10f))].Name);
            Assert.AreEqual("06 wing P", plan.Spaces[plan.SpaceAt(new Vector3(-8f, -3f, 10f))].Name);
            Assert.AreEqual("06 hold", plan.Spaces[plan.SpaceAt(new Vector3(0f, -3f, 10f))].Name);
            Assert.AreEqual("06 upper", plan.Spaces[plan.SpaceAt(new Vector3(8f, 5f, 10f))].Name);
            Assert.AreEqual(-1, plan.SpaceAt(new Vector3(0f, 12f, 10f)), "the superstructure is not subdivided");
            Assert.AreEqual(-1, plan.SpaceAt(new Vector3(0f, 0f, 78f)), "the forepeak belongs to no space");
        }

        [Test]
        public void SpaceOpenToTheSea_HoldsExactlyTheBuoyancyItsColumnsLose()
        {
            // The probe model's lost-buoyancy identity: water standing at sea level in a space weighs exactly what its
            // columns' buoyancy loses (× permeability), and acts at the same points. So added weight and lost buoyancy
            // agree at equilibrium, by construction, not by tuning.
            using (var rig = new FloodedHullRig(FloodedHullRig.Loadout()))
            {
                FloodPlan plan = rig.Flooding.Plan;
                int wing = plan.Find("06 wing S");
                FloodSpace s = plan.Spaces[wing];
                float below = 0f;
                for (int i = s.FirstSegment; i < s.FirstSegment + s.SegmentCount; i++)
                    below += plan.Segments[i].AreaM2 * Mathf.Clamp(-plan.Segments[i].Y0, 0f, plan.Segments[i].Y1 - plan.Segments[i].Y0);
                rig.Flooding.Water.SetVolume(wing, s.Permeability * below);
                rig.Flooding.Water.Step(0f, Upright);
                Assert.AreEqual(0f, rig.Flooding.Water.Head(wing), 1e-4f, "its level is the sea's");

                rig.Load(out _, out _, out _, out _, Vector3.zero);
                var columns = new HashSet<(float, float)>();
                for (int i = s.FirstSegment; i < s.FirstSegment + s.SegmentCount; i++) columns.Add((plan.Segments[i].X, plan.Segments[i].Z));
                float lift = 0f, liftX = 0f, liftY = 0f, liftZ = 0f;
                for (int i = 0; i < rig.Probes.Length; i++)
                {
                    Vector3 p = rig.Probes[i].LocalPosition;
                    if (!columns.Contains((p.x, p.z))) continue;
                    float f = s.Permeability * rig.ProbeOutputs[i].Force.y;
                    float h = rig.ProbeOutputs[i].SubmergedVolume / rig.Probes[i].AreaM2;
                    lift += f;
                    liftX += f * p.x;
                    liftY += f * (p.y - 0.5f * rig.Probes[i].HeightM + 0.5f * h);
                    liftZ += f * p.z;
                }
                float weight = Rho * G * rig.Flooding.Water.Volume(wing);
                Vector3 c = rig.Flooding.Water.CentroidShip(wing);
                Debug.Log($"[GQ] DC L1 identity, 06 wing S at sea level: water {weight / 1e6f:0.0000} MN, lost buoyancy {lift / 1e6f:0.0000} MN; " +
                          $"centroid ({c.x:0.0000}, {c.y:0.0000}, {c.z:0.0000}) vs ({liftX / lift:0.0000}, {liftY / lift:0.0000}, {liftZ / lift:0.0000}) m");
                Assert.AreEqual(lift, weight, 1e-4f * lift, "weight = lost buoyancy");
                Assert.AreEqual(liftX / lift, c.x, 1e-3f, "same lever athwartships");
                Assert.AreEqual(liftY / lift, c.y, 1e-3f, "same height");
                Assert.AreEqual(liftZ / lift, c.z, 1e-3f, "same lever fore and aft");
            }
        }

        // ── The flooding model ──────────────────────────────────────────────

        /// <summary>A 20 × 10 m box of 1 m² columns, keel at −5, deck at +5, one space below a deck at +4, permeability 1.</summary>
        static FloodingProfileData BoxProfile()
        {
            var columns = new List<HullColumn>();
            for (int ix = 0; ix < 10; ix++)
                for (int iz = 0; iz < 20; iz++)
                    columns.Add(new HullColumn { X = ix - 4.5f, Z = iz - 9.5f, AreaM2 = 1f, BottomY = -5f, TopY = 5f });
            return new FloodingProfileData
            {
                Columns = columns.ToArray(),
                Stations = new[] { new HullStation { Name = "B", AftZ = -10f, ForeZ = 10f, HoldUse = SpaceUse.Void } },
                DeckY = 4f, WingInboardX = 100f, WingFirstStation = 99, WingLastStation = -1,
                PermeabilityStores = 1f, PermeabilityMachinery = 1f, PermeabilityAccommodation = 1f, PermeabilityVoid = 1f,
                ShipMassKg = 1e6f, KeelY = -5f, LengthWlM = 20f,
                SeawaterDensityKgM3 = Rho, Gravity = G, DischargeCoefficient = 0.6f,
                HoleAreaAtRefM2 = 1f, HoleRefWarheadKg = 1f, CapsizeHeelDeg = 45f, CapsizeHoldS = 5f,
            };
        }

        [Test]
        public void Orifice_FillsAsTorricelliSays()
        {
            // A 1 m² hole 3 m below the sea into a dry 200 m² box held still. While the water inside is below the hole it
            // pours at the full head: Q = Cd·a·√(2g·3) = 4.603 m³/s, for 400 m³ (86.9 s). Then the head falls as the box
            // fills: t = 2A(√3 − √h)/(Cd·a·√(2g)); to 1 cm, 245.6 s. Total 332.5 s.
            var plan = new FloodPlan(BoxProfile());
            var water = new FloodModel(plan);
            int hole = water.AddOpening(new FloodOpening { Kind = OpeningKind.Hole, SpaceA = 0, SpaceB = -1, PointShip = new Vector3(5f, -3f, 0f), AreaM2 = 1f });
            water.Step(0.02f, Upright);
            float q0 = water.Flow(hole);
            float t = 0.02f;
            while (water.Head(0) < -0.01f && t < 1000f) { water.Step(0.02f, Upright); t += 0.02f; }
            float expected = 400f / (0.6f * Mathf.Sqrt(2f * G * 3f)) + 2f * 200f * (Mathf.Sqrt(3f) - 0.1f) / (0.6f * Mathf.Sqrt(2f * G));
            Debug.Log($"[GQ] DC L1 orifice: first flow {q0:0.0000} m³/s (Torricelli 4.6026); filled to 1 cm in {t:0.0} s (theory {expected:0.0} s)");
            Assert.AreEqual(0.6f * Mathf.Sqrt(2f * G * 3f), q0, 1e-3f * q0);
            Assert.AreEqual(expected, t, 0.01f * expected);
        }

        // ── Damage stability against the closed form ────────────────────────

        static (float list, float trim, float sink, float gm, FloodedHullRig rig) Flood(params (Vector3 at, float m2)[] holes)
        {
            var rig = new FloodedHullRig(FloodedHullRig.Loadout());
            Assert.IsTrue(rig.Settle(300f), "intact, she comes to rest");
            float l0 = rig.ListDeg, t0 = rig.TrimM, z0 = rig.SinkageM;
            foreach (var h in holes) rig.Combat.Issue(Order.Breach(OrderIssuer.Scenario, h.at, h.m2));
            Assert.IsTrue(rig.Settle(900f, 10f), "flooded, she comes to rest");
            rig.Step();   // one more step so the once-a-second stability has the rest pose
            for (int i = 0; i < 10; i++) rig.Step();
            return (rig.ListDeg - l0, rig.TrimM - t0, rig.SinkageM - z0, rig.Flooding.Stability.GmM, rig);
        }

        [Test]
        public void FloodedWing_ListsTrimsAndLosesGm_AsPredicted()
        {
            // python -I flood_predict.py --bilge "06 wing S":
            //   LIST +4.34 deg, TRIM +12.9 cm by the head, sinkage at amidships 8.0 cm, GM 1.560 -> 1.345 m (lost buoyancy).
            var r = Flood((new Vector3(8f, -3f, 10f), 2f));
            using (r.rig)
            {
                Debug.Log($"[GQ] DC L1 06 wing S bilged: list {r.list:0.000}° (4.34), trim {r.trim * 100f:0.0} cm (12.9), sinkage {r.sink * 100f:0.00} cm (8.0), " +
                          $"GM {r.gm:0.000} m (1.345), {r.rig.Flooding.Water.WaterMassKg / 1000f:0.0} t of water");
                Within("list, deg", 4.34f, r.list, 0.05f, 0.2f);
                Within("trim, m", 0.129f, r.trim, 0.15f, 0.03f);
                Within("sinkage, m", 0.080f, r.sink, 0.15f, 0.02f);
                Within("GM, m", 1.345f, r.gm, 0.05f, 0f);
                Assert.AreEqual(FloodVerdict.Afloat, r.rig.Flooding.Verdict);
            }
        }

        [Test]
        public void FloodedBowHold_TrimsAndSinks_AsPredicted()
        {
            // python -I flood_predict.py --bilge "02 hold": TRIM +89.8 cm, sinkage 15.6 cm, list 0, GM 1.624 m.
            var r = Flood((new Vector3(0f, -4f, 58f), 2f));
            using (r.rig)
            {
                Debug.Log($"[GQ] DC L1 02 hold bilged: list {r.list:0.000}°, trim {r.trim * 100f:0.0} cm (89.8), sinkage {r.sink * 100f:0.00} cm (15.6), GM {r.gm:0.000} m (1.624)");
                Within("trim, m", 0.898f, r.trim, 0.15f, 0.03f);
                Within("sinkage, m", 0.156f, r.sink, 0.15f, 0.02f);
                Within("list, deg", 0f, r.list, 0.05f, 0.2f);
                Within("GM, m", 1.624f, r.gm, 0.05f, 0f);
            }
        }

        [Test]
        public void PartFilledHold_FreeSurfaceComesFromTheLevel()
        {
            // python -I flood_predict.py --water "06 hold=0.5": GM 1.850 m with the water solid, 1.725 m free (12.6 cm).
            // The core has no free-surface formula: the water's level stays horizontal as she heels, so its centroid runs
            // to the low side. Measured from the righting moment at ±2°, with the water free and then frozen upright.
            // (The first prediction took BM from the drawn waterplane, wall-sided, and said 1.691 / 1.565: 0.14 m low. The
            // DDG flares, so 26 cm of sinkage widens her waterplane; the predictor now takes it at the sunk draft.)
            using (var rig = new FloodedHullRig(FloodedHullRig.Loadout()))
            {
                int hold = rig.Flooding.Plan.Find("06 hold");
                rig.Flooding.Water.SetVolume(hold, 0.5f * rig.Flooding.Plan.Spaces[hold].CapacityM3);
                Assert.IsTrue(rig.Settle(300f));
                Vector3 frozen = rig.Flooding.Water.WaterCentroidShip;
                float roll0 = rig.RollRad, heel = 2f * Mathf.Deg2Rad;

                float Moment(float roll, bool free)
                {
                    rig.Pose(roll);
                    rig.Flooding.Water.Step(0f, rig.Own());
                    rig.Load(out _, out _, out _, out Vector3 torque, free ? rig.Flooding.Water.WaterCentroidShip : frozen);
                    return torque.z;
                }
                float weight = (rig.Hull.massKg + rig.Flooding.Water.WaterMassKg) * G;
                float fluid = (Moment(roll0 - heel, true) - Moment(roll0 + heel, true)) / (2f * weight * Mathf.Sin(heel));
                float solid = (Moment(roll0 - heel, false) - Moment(roll0 + heel, false)) / (2f * weight * Mathf.Sin(heel));
                Debug.Log($"[GQ] DC L1 free surface, 06 hold half full: GM {solid:0.000} m solid (1.850), {fluid:0.000} m free (1.725); " +
                          $"free-surface loss {(solid - fluid) * 100f:0.0} cm (12.6)");
                Within("GM solid, m", 1.850f, solid, 0.05f, 0f);
                Within("GM free, m", 1.725f, fluid, 0.05f, 0f);
                Within("free-surface loss, m", 0.126f, solid - fluid, 0.15f, 0.01f);
            }
        }

        // ── The margin (G60) ────────────────────────────────────────────────

        static (FloodVerdict verdict, long tick, ulong fix, int wet, int holed, double ms, CombatSystem cs) Breach(int firstStation, int stations, float maxS)
        {
            using (var rig = new FloodedHullRig(FloodedHullRig.Loadout()))
            {
                rig.Settle(300f);
                for (int st = firstStation; st < firstStation + stations; st++)
                {
                    float z = 70f - 12f * st;   // mid-station
                    rig.Combat.Issue(Order.Breach(OrderIssuer.Scenario, new Vector3(0f, -4f, z), 3f));
                    rig.Combat.Issue(Order.Breach(OrderIssuer.Scenario, new Vector3(7f, -2f, z), 2f));
                    rig.Combat.Issue(Order.Breach(OrderIssuer.Scenario, new Vector3(-7f, -2f, z), 2f));
                }
                float end = rig.Seconds + maxS;
                while (rig.Seconds < end && rig.Flooding.Verdict == FloodVerdict.Afloat) rig.Step();
                int holed = Enumerable.Range(0, rig.Flooding.Plan.Spaces.Length).Count(rig.Flooding.IsBreached);
                return (rig.Flooding.Verdict, rig.Combat.Tick, rig.Combat.Fix, rig.Flooding.WetSpaces, holed, rig.CoreMsPerTick, rig.Combat);
            }
        }

        [Test]
        public void WithinTheMargin_ThreeHoldsAmidships_SheFloats()
        {
            // Holds 05-07 and their wings of this game hull's AUTHORED arrangement (flood_predict.py puts her 1.3 m down and
            // 2.2 m by the head). Afloat after half an hour, with most of her reserve. The margin is the game hull's, not a
            // statement about the real DDG-51 (BLUEPRINT standing constraint 8).
            var r = Breach(4, 3, 1800f);
            Debug.Log($"[GQ] DC L1 within the margin (holds 05-07): {r.verdict} after {r.tick * 0.02f:0} s, {r.wet} spaces wet, reserve " +
                      $"{r.cs.DamageControl.Flooding.ReserveBuoyancy:0.00}");
            Assert.AreEqual(FloodVerdict.Afloat, r.verdict);
            Assert.Greater(r.cs.DamageControl.Flooding.ReserveBuoyancy, 0.5f);
            Assert.IsFalse(r.cs.DamageControl.OutOfAction);
        }

        [Test]
        public void PastTheMargin_SevenStations_SinksHer_TheSameWayEveryTime()
        {
            // Holds 03-09 and their wings holed (a 84 m gash). The holes let in about two thirds of what sinks her; the
            // rest climbs through the hatches and doors and, once the deck edge is under, the weather openings. SINKING
            // is counted (G60): the water aboard reaches her whole reserve buoyancy. Twice, bit for bit.
            var a = Breach(2, 7, 3600f);
            var b = Breach(2, 7, 3600f);
            EngagementEvent sunk = a.cs.Log.Events.Last(e => e.Kind == EventKind.SystemOffline);
            Debug.Log($"[GQ] DC L1 past the margin (holds 03-09): {a.verdict} at {a.tick * 0.02f:0} s; {a.wet} spaces wet, {a.holed} holed; " +
                      $"core {a.ms:0.000} ms per tick (Editor, Debug code); Fix {a.fix:X16}. {sunk.Text}");
            Assert.AreEqual(FloodVerdict.Sinking, a.verdict, "past the margin she sinks");
            Assert.Greater(a.wet, a.holed, "flooding progressed into spaces no hole reached");
            Assert.AreEqual(a.tick, b.tick, "on the same tick");
            Assert.AreEqual(a.fix, b.fix, "with the same Fix");
            Assert.IsTrue(a.cs.Log.IsComplete(sunk.Id), "the SINKING verdict's cause chain ends in roots (the Breach Orders)");
            Assert.IsTrue(a.cs.DamageControl.OutOfAction);
            Assert.LessOrEqual(a.ms, 1.0, "combat-core time per fixed step (G16)");
        }

        // ── Holes (G57) and loss (G60) ──────────────────────────────────────

        [Test]
        public void BreachOrder_OpensAHole_AndItsWaterTracesToTheOrder()
        {
            var cs = new CombatSystem(FloodedHullRig.Loadout());
            var none = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            Order o = cs.Issue(Order.Breach(OrderIssuer.Scenario, new Vector3(8f, -3f, 10f), 1f));
            for (int i = 0; i < 50; i++) cs.Step(Upright, none, hits, kills);
            EngagementEvent breach = cs.Log.Events.Single(e => e.Kind == EventKind.Breach);
            EngagementEvent flooding = cs.Log.Events.First(e => e.Kind == EventKind.Flooding);
            StringAssert.Contains("06 wing S", breach.Text);
            StringAssert.Contains("3.0 m below the sea", breach.Text);
            StringAssert.Contains("06 wing S taking water through the hole", flooding.Text);
            Assert.AreEqual(breach.Id, flooding.CauseA, "the water came in through that hole");
            Assert.AreEqual(o.Sequence, cs.Log.Events[breach.CauseA].OrderSequence, "and the hole came from the Order");
            Assert.IsTrue(cs.Log.IsComplete(flooding.Id));
            Assert.AreEqual(1, cs.DamageControl.Flooding.Breaches);
        }

        [Test]
        public void LeakerHit_OpensAHoleSizedFromItsWarhead()
        {
            // G57: area ∝ W^(2/3) through USS Stark's 165 kg / 13.9 m². A 220 kg Harpoon-class warhead: 16.8 m².
            CombatLoadout l = FloodedHullRig.Loadout();
            var dc = new DamageControlL1(l.Damage, l.Flooding);
            Assert.AreEqual(13.9f, dc.HoleAreaM2(165f), 1e-4f, "the calibration point");
            Assert.AreEqual(13.9f * Mathf.Pow(220f / 165f, 2f / 3f), dc.HoleAreaM2(220f), 1e-4f);

            var log = new EngagementLog();
            var offline = new List<ShipSystem>();
            dc.ApplyHit(0, new ShipHit { ThreatId = 1, Kind = TargetKind.AntiShipMissile, Point = new Vector3(8f, 1f, 10f), WarheadKg = 220f }, Upright, -1, log, offline);
            dc.ApplyHit(0, new ShipHit { ThreatId = 2, Kind = TargetKind.AntiShipMissile, Point = new Vector3(0f, 14f, 10f), WarheadKg = 220f }, Upright, -1, log, offline);
            Assert.AreEqual(1, dc.Breaches, "one hit in the hull, one in the superstructure");
            StringAssert.Contains("16.8 m² hole into 06 wing S", log.Events.First(e => e.Kind == EventKind.ShipHit).Text);
            StringAssert.Contains("nothing floods", log.Events.Last(e => e.Kind == EventKind.ShipHit).Text);
            Assert.IsTrue(dc.IsBreached(dc.Plan.Find("06 wing S")));
        }

        [Test]
        public void HullHitPoints_NoLongerDecideLoss_AtL1()
        {
            // G60: five 220 kg hits above the waterline take the hull's 1,000 hit points. At L0 she is out of action; at L1
            // she is not, because nothing has flooded.
            OwnShipState own = Upright;
            CombatLoadout l1 = FloodedHullRig.Loadout();
            CombatLoadout l0 = TestLoadouts.Ddg51L0();
            foreach (CombatLoadout l in new[] { l0, l1 })
            {
                IDamageControl dc = DamageControlFactory.Create(l.DamageLevel, l.Damage, l.Flooding, out _);
                var log = new EngagementLog();
                var offline = new List<ShipSystem>();
                for (int i = 0; i < 5; i++)
                    dc.ApplyHit(i, new ShipHit { ThreatId = i, Kind = TargetKind.AntiShipMissile, Point = new Vector3(0f, 9f, -30f + 4f * i), WarheadKg = 220f }, own, -1, log, offline);
                for (int i = 0; i < 50; i++) dc.Step(10 + i, 0.02f, own, log);
                Assert.AreEqual(0f, dc.HullHitPoints, 1e-3f, $"{dc.Level}: hull points spent");
                Assert.AreEqual(dc.Level == FidelityLevel.L0, dc.OutOfAction, $"{dc.Level}: out of action only at L0");
            }
        }

        [Test]
        public void DamageControlL0_DoesNotFlood()
        {
            var cs = new CombatSystem(TestLoadouts.Ddg51L0());
            cs.Issue(Order.Breach(OrderIssuer.Scenario, new Vector3(8f, -3f, 10f), 1f));
            cs.Step(Upright, new List<TruthTarget>(), new List<ShipHit>(), new List<KillReport>());
            Assert.IsNull(cs.DamageControl.Flooding);
            StringAssert.Contains("breach not modelled", cs.Log.Events.Last(e => e.Kind == EventKind.Note).Text);
        }

        // ── The hull carries the water (G55) ─────────────────────────────────

        [Test]
        public void ShipHull_CarriesTheFloodWater_AboutTheMovedCentre()
        {
            var profile = ScriptableObject.CreateInstance<ShipHullProfileSO>();
            var go = new GameObject("ship under test");
            try
            {
                go.AddComponent<BoxCollider>().size = new Vector3(20f, 12f, 150f);
                var body = go.AddComponent<Rigidbody>();
                var hull = go.AddComponent<ShipHull>();
                profile.massKg = 8.7e6f;
                profile.centerOfMassM = new Vector3(0f, 0.68f, 0.81f);
                profile.gyradiusM = new Vector3(35.25f, 35.25f, 6.56f);
                var so = new UnityEditor.SerializedObject(hull);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();
                hull.Apply();

                var water = new Vector3(6.8f, -2f, 9.8f);
                hull.ApplyFloodWater(150e3f, water);
                FloodMass.Combine(profile.massKg, profile.centerOfMassM, profile.InertiaTensor, 150e3f, water, out float m, out Vector3 c, out Vector3 inertia);
                Assert.AreEqual(8.85e6f, body.mass, 1f);
                Assert.AreEqual(c.x, body.centerOfMass.x, 1e-5f, "the centre moves toward the water");
                Assert.AreEqual(c.y, body.centerOfMass.y, 1e-5f);
                Assert.AreEqual(c.z, body.centerOfMass.z, 1e-5f);
                Assert.AreEqual(inertia.z, body.inertiaTensor.z, 1e-4f * inertia.z, "roll inertia about the new centre (parallel axis)");
                Assert.Greater(body.inertiaTensor.z, profile.InertiaTensor.z, "the water adds to it");
                Assert.AreEqual(Quaternion.identity, body.inertiaTensorRotation);

                hull.ApplyFloodWater(0f, Vector3.zero);
                Assert.AreEqual(profile.massKg, body.mass, 1f, "pumped dry, she is herself again");
                Assert.AreEqual(profile.centerOfMassM.y, body.centerOfMass.y, 1e-5f);
                Assert.AreEqual(profile.InertiaTensor.z, body.inertiaTensor.z, 1e-4f * profile.InertiaTensor.z);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
