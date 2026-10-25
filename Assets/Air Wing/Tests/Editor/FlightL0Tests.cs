using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Airframe;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Powerplant;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// Slice 1b: the F/A-18E at L0, headless. Flight behaviour against closed-form physics, then the one CALIBRATED
    /// number and the VALIDATION figures (C16), which must match the mirror <c>Calibration~/l0_calibration.py</c> (C17).
    /// </summary>
    public class FlightL0Tests
    {
        const string ProfilePath = "Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset";
        const float G0 = Units.StandardGravity;
        const double Ft = Units.FootToMetre;

        static AircraftProfile F18E()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            Assert.IsNotNull(so, ProfilePath);
            return so.Bake();
        }

        static FidelitySheet Sheet(int substeps = 4, FidelityLevel airframe = FidelityLevel.L0) =>
            new FidelitySheet(airframe, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, substeps, 1442);

        /// <summary>Half internal fuel: the calibration's test weight.</summary>
        static float HalfFuel(in AircraftProfile p) => 0.5f * p.Airframe.InternalFuelKg;

        static Aircraft Jet(in AircraftProfile p, float altitudeM, float speedMps, float throttle = 0.8f, int substeps = 4) =>
            AircraftFactory.Build(1, p, Sheet(substeps), AircraftFactory.LevelStart(p, new Vector3(0f, altitudeM, 0f), 0f, speedMps, HalfFuel(p), throttle));

        static void Fly(Aircraft a, Func<long, PilotInputs> seat, int ticks, float surface = 0f, int substeps = 4)
        {
            var clock = new SubstepClock(0.02, substeps);
            for (long t = 0; t < ticks; t++) a.Step(seat(t).Clamped(), surface, clock);
        }

        static PilotInputs Stick(float pitch, float roll, float throttle, bool ab = false) =>
            new PilotInputs { StickPitch = pitch, StickRoll = roll, Throttle = throttle, Afterburner = ab };

        /// <summary>
        /// A 60 s manoeuvre on full military power that stays well clear of the ground: a gentle pull, roll right to
        /// about 60° of bank and turn at 2.3 g (n cos φ > 1, so it climbs a little), reverse to about 60° left, roll out.
        /// The first version rolled with 0.6 stick: with the roll lag's tail that passed 90°, and 2.3 g from there is a
        /// descending spiral into the sea after 43 s. Correct physics, wrong script.
        /// </summary>
        static PilotInputs Manoeuvre(long tick)
        {
            double t = tick * 0.02;
            if (t < 5) return Stick(0.2f, 0f, 1f);
            if (t < 6) return Stick(0.1f, 0.4f, 1f);
            if (t < 24) return Stick(0.2f, 0f, 1f);
            if (t < 26) return Stick(0.1f, -0.4f, 1f);
            if (t < 44) return Stick(0.2f, 0f, 1f);
            if (t < 45) return Stick(0f, 0.4f, 1f);
            return Stick(0f, 0f, 1f);
        }

        static PilotInputs Mirrored(PilotInputs i) { i.StickRoll = -i.StickRoll; return i; }

        // ── Assembly ──────────────────────────────────────────────────────

        [Test]
        public void Factory_BuildsEveryPartAtL0_AndSaysSoWhenAHigherLevelIsAsked()
        {
            AircraftProfile p = F18E();
            Aircraft a = AircraftFactory.Build(1, p, Sheet(airframe: FidelityLevel.L2), AircraftFactory.LevelStart(p, Vector3.up * 3000f, 0f, 200f, 1000f));
            Assert.AreEqual(5, a.Levels.Count);
            FidelityResolution airframe = default;
            foreach (var r in a.Levels) if (r.Part == PartKind.Airframe) airframe = r;
            Assert.IsTrue(airframe.FellBack, "L2 asked, L0 is all that is built");
            Assert.AreEqual(FidelityLevel.L0, airframe.Resolved);
            Assert.IsInstanceOf<AirframeL0>(a.Airframe);
        }

        // ── Determinism (W7) ──────────────────────────────────────────────

        sealed class ScriptSeat : ISeat
        {
            readonly Func<long, PilotInputs> _script;
            public ScriptSeat(Func<long, PilotInputs> script) => _script = script;
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state) => _script(tick);
        }

        static FlightCore Run(int ticks)
        {
            AircraftProfile p = F18E();
            var core = new FlightCore(new SubstepClock(0.02, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, p, Sheet(), AircraftFactory.LevelStart(p, new Vector3(0f, 4000f, 0f), 0f, 220f, HalfFuel(p))), new ScriptSeat(Manoeuvre));
            // The second jet flies the mirror image (rolls the other way). An earlier version replayed the script 8 s late,
            // which started it on a 16 s wings-level 2.3 g pull: a loop kilometres across from 4,200 m, into the sea.
            core.Add(AircraftFactory.Build(2, p, Sheet(), AircraftFactory.LevelStart(p, new Vector3(800f, 4200f, 0f), 90f, 200f, HalfFuel(p))), new ScriptSeat(t => Mirrored(Manoeuvre(t))));
            for (int i = 0; i < ticks; i++) core.Step();
            return core;
        }

        [Test]
        public void TwoRunsWithTheSameInputs_GiveTheSameFixOnEveryTick()
        {
            FlightCore a = Run(3000), b = Run(3000);
            Assert.AreEqual(3000, a.Fixes.All.Count);
            Assert.AreEqual(-1, a.Fixes.FirstDivergence(b.Fixes), "same inputs, same Fix, every tick");
            Assert.AreEqual(6000, a.Inputs.Count, "one input entry per aircraft per tick");
            foreach (Aircraft jet in a.Aircraft) Assert.AreEqual(FlightStatus.Flying, jet.State.Status, "the manoeuvre stays airborne");
        }

        // ── Substeps (W24) ────────────────────────────────────────────────

        [Test]
        public void SubstepCount_ChangesThePathOnlyByIntegrationError()
        {
            AircraftProfile p = F18E();
            Vector3 Final(int n)
            {
                Aircraft a = Jet(p, 4000f, 220f, 1f, n);
                Fly(a, Manoeuvre, 3000, substeps: n);
                Assert.AreEqual(FlightStatus.Flying, a.State.Status, $"airborne after 60 s at {n} substeps");
                return a.State.Position;
            }
            Vector3 p2 = Final(2), p4 = Final(4), p8 = Final(8);
            float path = 60f * 220f;
            float d48 = (p4 - p8).magnitude, d28 = (p2 - p8).magnitude;
            Debug.Log($"[AIRWING] substeps: |p4 - p8| {d48:F2} m, |p2 - p8| {d28:F2} m over ~{path / 1000f:F1} km");
            Assert.Less(d48, 0.005f * path, "4 vs 8 substeps within 0.5 % of the path");
            Assert.Less(d28, 0.01f * path, "2 vs 8 substeps within 1 % of the path");
            Assert.Greater(d28, d48, "the error shrinks as the substep shrinks (first-order integrator)");
        }

        // ── Flight against closed-form physics ────────────────────────────

        /// <summary>The throttle at which thrust equals 1 g drag at this speed and altitude (bisection).</summary>
        static float TrimThrottle(in AircraftProfile p, float altitudeM, float speedMps, float massKg)
        {
            var airframe = new AirframeL0(p.Airframe);
            var engine = new PowerplantL0(p.Powerplant);
            AirData air = Isa.At(altitudeM);
            float drag = airframe.DragN(1f, speedMps, air.DensityKgM3, massKg, false, false, out _);
            float lo = 0f, hi = 1f;
            var scratch = new AirframeState();
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (engine.Step(Stick(0f, 0f, mid), air, ref scratch, 0.005f) < drag) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        [Test]
        public void StraightAndLevel_AtTrim_HoldsAltitudeAndSpeed()
        {
            AircraftProfile p = F18E();
            float mass = p.Airframe.EmptyMassKg + HalfFuel(p);
            float throttle = TrimThrottle(p, 5000f, 200f, mass);
            Assert.That(throttle, Is.InRange(0.05f, 0.95f), "trim throttle is inside the military range");
            Aircraft a = Jet(p, 5000f, 200f, throttle);
            Fly(a, _ => Stick(0f, 0f, throttle), 3000);
            Assert.AreEqual(5000f, a.State.Position.y, 5f, "altitude held over 60 s");
            Assert.AreEqual(200f, a.State.Velocity.magnitude, 0.5f, "speed held over 60 s");
            Assert.AreEqual(1f, a.State.LoadFactor, 1e-4f);
        }

        [Test]
        public void LevelTurn_TurnsAt_gTimesRootNSquaredMinusOne_OverV()
        {
            AircraftProfile p = F18E();
            const float v = 200f, n = 2f;   // bank 60°: n = 1 / cos 60°
            Aircraft a = Jet(p, 5000f, v);
            a.State.Attitude = Quaternion.Euler(0f, 0f, -60f);   // right wing down 60° (Unity: right roll is about −Z)
            a.State.LoadFactor = n;
            float stick = (n - 1f) / (p.Controls.LoadFactorMax - 1f);

            Vector3 v0 = a.State.Velocity;
            Fly(a, _ => Stick(stick, 0f, 1f), 10);   // 0.2 s
            Vector3 v1 = a.State.Velocity;

            float headingRate = (Mathf.Atan2(v1.x, v1.z) - Mathf.Atan2(v0.x, v0.z)) / 0.2f;
            float expected = G0 * Mathf.Sqrt(n * n - 1f) / v;
            Assert.Greater(headingRate, 0f, "a right bank turns right");
            Assert.AreEqual(expected, headingRate, 0.01f * expected, "turn rate = g√(n²−1)/V within 1 %");
            Assert.AreEqual(5000f, a.State.Position.y, 0.5f, "level: n cos φ = 1");
        }

        [Test]
        public void FullAftStick_HoldsTheGLimitWhenFast_AndClMaxWhenSlow()
        {
            AircraftProfile p = F18E();
            Aircraft fast = Jet(p, 3000f, 300f, 1f);
            float peak = 0f;
            for (int i = 0; i < 100; i++) { Fly(fast, _ => Stick(1f, 0f, 1f), 1); peak = Mathf.Max(peak, fast.State.LoadFactor); }
            Assert.LessOrEqual(peak, p.Controls.LoadFactorMax + 1e-4f, "never above the G limit");
            Assert.Greater(peak, p.Controls.LoadFactorMax - 0.05f, "and reaches it");

            Aircraft slow = Jet(p, 3000f, 100f, 1f);
            Fly(slow, _ => Stick(1f, 0f, 1f), 10);   // 0.2 s: the lag has passed the lift limit by then
            AirData air = Isa.At(slow.State.Position.y);
            float qS = 0.5f * air.DensityKgM3 * slow.State.Velocity.sqrMagnitude * p.Airframe.WingAreaM2;
            float clLimited = qS * p.Airframe.CLmax / (slow.State.MassKg * G0);
            Assert.Less(clLimited, p.Controls.LoadFactorMax, "slow enough that lift, not the control law, sets the limit");
            Assert.AreEqual(clLimited, slow.State.LoadFactor, 0.02f * clLimited, "n = qS·CLmax / W");
        }

        [Test]
        public void ZoomClimb_WithThrustAndDragOff_ConservesEnergy()
        {
            AircraftProfile p = F18E();
            p.Airframe.CD0 = 0f;
            p.Airframe.OswaldEfficiency = 1e9f;          // no induced drag
            p.Powerplant.IdleThrustFraction = 0f;        // no thrust at zero throttle
            Aircraft a = Jet(p, 2000f, 250f, 0f);
            float E(in AirframeState s) => 0.5f * s.Velocity.sqrMagnitude + G0 * s.Position.y;
            float e0 = E(a.State);
            Fly(a, t => Stick(t < 150 ? 0.3f : 0f, 0f, 0f), 1000);   // pull up for 3 s, then 1 g for 17 s
            Assert.Greater(a.State.Position.y, 2500f, "it zoomed");
            Assert.AreEqual(e0, E(a.State), 0.002f * e0, "kinetic plus potential energy kept within 0.2 %");
        }

        [Test]
        public void FullRightStick_RollsRight_AndTheLiftTurnsTheJetRight()
        {
            AircraftProfile p = F18E();
            Aircraft a = Jet(p, 5000f, 200f, 1f);
            Fly(a, _ => Stick(0f, 1f, 1f), 50);   // 1 s
            float rollRate = -a.State.BodyRates.z;
            Assert.Greater(rollRate, 0.95f * p.Controls.RollRateMaxRadS, "roll rate reaches 95 % of the limit within 1 s (τ 0.3 s)");
            Vector3 up = a.State.Attitude * Vector3.up;
            Assert.Greater(up.x, 0.5f, "the lift has tilted to the right");
            Assert.Greater(a.State.Velocity.x, 0f, "and the flight path follows it");
        }

        [Test]
        public void MeetingTheSurfaceWithNoGear_IsACrash_AndTheWreckStaysPut()
        {
            AircraftProfile p = F18E();
            Aircraft a = Jet(p, 150f, 200f, 1f);
            Fly(a, _ => Stick(-1f, 0f, 1f), 500, surface: 2f);
            Assert.AreEqual(FlightStatus.Crashed, a.State.Status);
            Assert.AreEqual(2f, a.State.Position.y, 1e-4f, "frozen on the surface it hit");
            Vector3 wreck = a.State.Position;
            Fly(a, _ => Stick(1f, 0f, 1f, true), 50, surface: 2f);
            Assert.AreEqual(wreck, a.State.Position, "a crash is terminal (AP23)");
            Assert.AreEqual(Vector3.zero, a.State.Velocity);
        }

        // ── Calibration (C16) and the mirror (C17) ────────────────────────

        static float BestClimbRate(in AircraftProfile p, float altitudeM, float massKg)
        {
            var airframe = new AirframeL0(p.Airframe);
            var engine = new PowerplantL0(p.Powerplant);
            AirData air = Isa.At(altitudeM);
            var scratch = new AirframeState();
            float thrust = engine.Step(Stick(0f, 0f, 1f, true), air, ref scratch, 0.005f);
            float best = float.NegativeInfinity;
            for (int i = 1; i <= 400; i++)
            {
                float v = 0.9f * air.SpeedOfSoundMps * i / 400f;   // L0 has no transonic drag: climb speeds capped at Mach 0.9
                float drag = airframe.DragN(1f, v, air.DensityKgM3, massKg, false, false, out _);
                best = Mathf.Max(best, (thrust - drag) * v / (massKg * G0));
            }
            return best;
        }

        [Test]
        public void ServiceCeiling_Is50000Feet_Calibrated()
        {
            // CALIBRATED: the thrust lapse exponent was fitted to this one public figure (Navy fact file, "50,000+ feet").
            AircraftProfile p = F18E();
            float mass = p.Airframe.EmptyMassKg + HalfFuel(p);
            float serviceClimb = (float)(100.0 * Ft / 60.0);
            float lo = 0f, hi = 30000f;
            for (int i = 0; i < 50; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (BestClimbRate(p, mid, mass) > serviceClimb) lo = mid; else hi = mid;
            }
            float ceilingFt = Units.Feet(0.5f * (lo + hi));
            Debug.Log($"[AIRWING] CALIBRATED service ceiling {ceilingFt:F0} ft (lapse x = {p.Powerplant.ThrustLapseExponent})");
            Assert.AreEqual(50000f, ceilingFt, 150f, "service ceiling 50,000 ft, as the mirror fitted");
        }

        static float MaxLevelMach(in AircraftProfile p, float altitudeM, float massKg)
        {
            var airframe = new AirframeL0(p.Airframe);
            var engine = new PowerplantL0(p.Powerplant);
            AirData air = Isa.At(altitudeM);
            var scratch = new AirframeState();
            float thrust = engine.Step(Stick(0f, 0f, 1f, true), air, ref scratch, 0.005f);
            float lo = 50f, hi = 2000f;
            for (int i = 0; i < 60; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (thrust - airframe.DragN(1f, mid, air.DensityKgM3, massKg, false, false, out _) > 0f) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi) / air.SpeedOfSoundMps;
        }

        [Test]
        public void MaxLevelSpeed_IsValidation_AndMatchesTheMirror()
        {
            // VALIDATION, not fitted: public "Mach 1.8+" (Navy) / "Mach 1.6" (Boeing). L0 has no Mach effects, so the
            // residual is a known gap until L2 (Calibration~/F18E-SOURCES.md). The numbers pin agreement with the mirror.
            AircraftProfile p = F18E();
            float mass = p.Airframe.EmptyMassKg + HalfFuel(p);
            float m40 = MaxLevelMach(p, (float)(40000 * Ft), mass);
            float m0 = MaxLevelMach(p, 0f, mass);
            Debug.Log($"[AIRWING] VALIDATION max level speed: Mach {m40:F2} at 40,000 ft, {m0:F2} at sea level (public: 1.8+ Navy, 1.6 Boeing)");
            Assert.AreEqual(1.41f, m40, 0.01f, "mirror: Mach 1.41 at 40,000 ft");
            Assert.AreEqual(1.54f, m0, 0.01f, "mirror: Mach 1.54 at sea level");
        }

        [Test]
        public void Profile_NamesTheSourceOfEveryNumber()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            Assert.IsNotNull(so);
            var missing = new List<string>();
            foreach (var f in typeof(AircraftProfileSO).GetFields())
            {
                if (f.FieldType != typeof(float) && f.FieldType != typeof(int)) continue;
                if (!so.provenance.Contains(f.Name)) missing.Add(f.Name);
            }
            Assert.IsEmpty(missing, "fields with no provenance line: " + string.Join(", ", missing));
            StringAssert.Contains("CALIBRATED", so.provenance);
            StringAssert.Contains("AUTHORED", so.provenance);
        }
    }
}
