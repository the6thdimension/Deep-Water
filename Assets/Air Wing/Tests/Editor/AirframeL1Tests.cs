using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Airframe;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.FlightControls;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// Slice 2a (W32, W33): Airframe L1 against the published F/A-18 HARV model. The reference numbers come from
    /// <c>Calibration~/l1_harv_mirror.py</c>, a line-for-line port of the authors' f18full.m; the profile here is
    /// transcribed from the paper (Chakraborty, Seiler and Balas, J. Guidance 2011, Tables A1–A2) and NASA TM-107601
    /// Table 3.5, independently of the F/A-18E asset.
    /// </summary>
    public class AirframeL1Tests
    {
        const double Ft = Units.FootToMetre;
        const float D2R = Mathf.Deg2Rad;
        const float PaperG = (float)(32.2 * Ft);                                 // f18_data.m
        const float Rho = (float)(1.0660e-3 * Units.SlugPerCubicFootToKilogramPerCubicMetre);   // 25,000 ft in f18_data.m
        const float MassKg = (float)(1034.5 * Units.SlugToKilogram);

        static float Si(double ft) => (float)(ft * Ft);
        static float Inertia(double slugFt2) => (float)(slugFt2 * Units.SlugFootSquaredToKilogramMetreSquared);

        internal static AirframeL1Profile Harv() => new AirframeL1Profile
        {
            Aero = new AeroPolynomialsL1
            {
                Clb = new[] { -0.0556f, -0.4153f, -0.3620f, 2.3843f, -1.6196f }, Cldr = new[] { 0.0129f, 0.0014f, 0.0083f, -0.0274f },
                Clda = new[] { 0.1424f, -0.0516f, -0.2646f, 0.1989f }, Clp = new[] { -0.3540f, 0.2377f }, Clr = new[] { 0.1983f, 0.7804f, -1.0871f },
                Cnb = new[] { 0.0885f, 0.0329f, -0.3816f }, Cndr = new[] { -0.0780f, -0.0176f, 0.5564f, -0.8980f, 0.3899f },
                Cnda = new[] { 0.0104f, 0.0584f, -0.3413f, 0.2694f }, Cnr = new[] { -0.4326f, -0.1307f }, Cnp = new[] { 0.0792f, -0.0881f },
                Cyb = new[] { -0.7344f, 0.2654f, -0.1926f }, Cyda = new[] { -0.1656f, -0.2403f, 1.5317f, -0.8500f }, Cydr = new[] { 0.2054f, 0.4082f, -1.6921f, 0.9351f },
                Cma = new[] { -0.0866f, 0.5110f, -1.2897f }, Cmds = new[] { -0.9051f, -0.3245f, 0.9338f }, Cmq = new[] { -4.1186f, 10.9921f, -68.5641f, 64.7190f },
                CL = new[] { -0.0204f, 5.677f, -5.4246f, 1.1645f }, CLds = new[] { 0.5725f, 0.4055f, -2.6975f, 2.1852f },
                CD = new[] { -1.4994f, -0.1995f, 6.3971f, -5.7341f, 1.4610f }, CD0 = 1.5036f, Cdds = new[] { 0.0366f, -0.2739f, 4.2360f, -3.8578f },
            },
            ReferenceAreaM2 = (float)(400.0 * Units.SquareFootToSquareMetre),
            SpanM = Si(37.42),
            ChordM = Si(11.52),
            IxxKgM2 = Inertia(23000), IyyKgM2 = Inertia(151293), IzzKgM2 = Inertia(169945), IxzKgM2 = Inertia(-2971),
            AlphaMinRad = -10f * D2R,
            AlphaMaxRad = 60f * D2R,
        };

        // ── 1. The coefficients, term for term ─────────────────────────────

        static void AssertCoefficients(AeroCoefficients c, params double[] mirror)
        {
            float[] got = { c.Cl, c.Cm, c.Cn, c.CY, c.CLift, c.CDrag };
            string[] names = { "C_l", "C_m", "C_n", "C_Y", "C_lift", "C_drag" };
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(mirror[i], got[i], 2e-6 + 2e-6 * System.Math.Abs(mirror[i]), names[i]);
        }

        [Test]
        public void Coefficients_MatchTheMirror()
        {
            AirframeL1Profile p = Harv();
            AssertCoefficients(AirframeL1.Coefficients(p, Si(500), 0f, 0f, 0f, 0f, 0f, default),
                0.0, -0.0866, 0.0, 0.0, -0.0204, 0.0042);
            AssertCoefficients(AirframeL1.Coefficients(p, Si(500), 3 * D2R, 10 * D2R, 0.1f, 0.05f, -0.05f, new ControlSurfaces(-3 * D2R, 5 * D2R, -4 * D2R)),
                0.001672496, 0.009894708, 0.011216209, -0.066830916, 0.780889972, 0.131897133);
            AssertCoefficients(AirframeL1.Coefficients(p, Si(300), -5 * D2R, 0.6632251157578453f, -0.2f, 0.1f, 0.1f, new ControlSurfaces(5 * D2R, -10 * D2R, 10 * D2R)),
                0.006323117, -0.392475973, -0.000058870, 0.039335379, 1.720986612, 1.348148031);
        }

        [Test]
        public void Lift_PeaksNear38Degrees_AndFallsPastIt()
        {
            AirframeL1Profile p = Harv();
            float best = 0f, bestAlpha = 0f;
            for (float a = 0f; a <= 60f; a += 0.25f)
            {
                float cl = AirframeL1.Coefficients(p, 100f, 0f, a * D2R, 0f, 0f, 0f, default).CLift;
                if (cl > best) { best = cl; bestAlpha = a; }
            }
            Debug.Log($"[AIRWING L1] C_L max {best:F4} at α {bestAlpha:F2}°");
            Assert.AreEqual(38.2f, bestAlpha, 0.5f, "the cubic's peak: α = 0.666 rad");
            Assert.AreEqual(1.699f, best, 0.002f);
            Assert.Less(AirframeL1.Coefficients(p, 100f, 0f, 55f * D2R, 0f, 0f, 0f, default).CLift, 1.5f, "past the peak the lift falls: a stall");
        }

        [Test]
        public void ThePolynomials_AreHeldFlatOutsideTheirRange()
        {
            AirframeL1Profile p = Harv();
            var s = new ControlSurfaces(0.05f, 0.05f, 0.05f);
            AeroCoefficients at60 = AirframeL1.Coefficients(p, 100f, 0.05f, 60f * D2R, 0.1f, 0.1f, 0.1f, s);
            AeroCoefficients at85 = AirframeL1.Coefficients(p, 100f, 0.05f, 85f * D2R, 0.1f, 0.1f, 0.1f, s);
            Assert.AreEqual(at60.CLift, at85.CLift);
            Assert.AreEqual(at60.Cm, at85.Cm);
            AeroCoefficients atMinus10 = AirframeL1.Coefficients(p, 100f, 0f, -10f * D2R, 0f, 0f, 0f, default);
            AeroCoefficients atMinus30 = AirframeL1.Coefficients(p, 100f, 0f, -30f * D2R, 0f, 0f, 0f, default);
            Assert.AreEqual(atMinus10.CLift, atMinus30.CLift);
        }

        // ── 2. The equations of motion, with no integrator in the way ─────

        /// <summary>V̇ (ft/s²), β̇, α̇ (rad/s) and ṗ, q̇, ṙ (rad/s²) from Airframe L1's forces and Euler's equations, by body-frame kinematics.</summary>
        static double[] Derivatives(double vFt, float beta, float alpha, float p, float q, float r, float phi, float theta, ControlSurfaces s, double thrustLbf)
        {
            AirframeL1Profile prof = Harv();
            float v = Si(vFt);
            var uvw = new Vector3(v * Mathf.Cos(alpha) * Mathf.Cos(beta), v * Mathf.Sin(beta), v * Mathf.Sin(alpha) * Mathf.Cos(beta));
            var pqr = new Vector3(p, q, r);
            AirframeL1.ForcesAndMoments(prof, Rho, uvw, pqr, s, (float)(thrustLbf * Units.PoundForceToNewton), 0f, 0f,
                                        out Vector3 f, out Vector3 m, out _, out _);
            Vector3 g = PaperG * new Vector3(-Mathf.Sin(theta), Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi) * Mathf.Cos(theta));
            Vector3 vdot = f / MassKg + g - Vector3.Cross(pqr, uvw);   // body-frame derivative of the body velocity
            double u = uvw.x, vv = uvw.y, w = uvw.z, ud = vdot.x, vd = vdot.y, wd = vdot.z;
            double vDot = (u * ud + vv * vd + w * wd) / v;
            double alphaDot = (u * wd - w * ud) / (u * u + w * w);
            double betaDot = (v * vd - vv * vDot) / (v * v * System.Math.Cos(beta));
            Vector3 omegaDot = AirframeL1.AngularAcceleration(prof, m, pqr);
            return new[] { vDot / Ft, betaDot, alphaDot, omegaDot.x, omegaDot.y, omegaDot.z };
        }

        [Test]
        public void AtTheMirrorsTrim_EverythingIsStill()
        {
            // Mirror trim at 500 ft/s: α 0.132893394 rad, stabilator −0.044506794 rad, thrust 4,018.383401 lbf.
            double[] d = Derivatives(500, 0f, 0.132893394f, 0f, 0f, 0f, 0f, 0.132893394f, new ControlSurfaces(-0.044506794f, 0f, 0f), 4018.383401);
            Debug.Log($"[AIRWING L1] trim residual: V̇ {d[0]:E2} ft/s², α̇ {d[2]:E2}, q̇ {d[4]:E2}");
            Assert.AreEqual(0.0, d[0], 2e-3, "V̇");
            Assert.AreEqual(0.0, d[1], 1e-6, "β̇");
            Assert.AreEqual(0.0, d[2], 2e-6, "α̇");
            Assert.AreEqual(0.0, d[3], 1e-6, "ṗ");
            Assert.AreEqual(0.0, d[4], 2e-6, "q̇");
            Assert.AreEqual(0.0, d[5], 1e-6, "ṙ");
        }

        [Test]
        public void AtASkewedState_TheDerivativesMatchTheMirror()
        {
            double[] d = Derivatives(500, 3 * D2R, 10 * D2R, 0.1f, 0.05f, -0.05f, 20 * D2R, 5 * D2R,
                                     new ControlSurfaces(-3 * D2R, 5 * D2R, -4 * D2R), 9000.0);
            // β̇ is the mirror's value corrected for a sign slip in the published model: f18full.m adds T·cosα·sinβ/(mV)
            // where body kinematics (and Stevens and Lewis) subtract it. The mirror prints 8.298978540e-02; its own
            // body-kinematics check gives 8.119619352e-02, and the difference is exactly 2·T·cosα·sinβ/(mV).
            double[] mirror = { 4.630671909e+00, 8.119619352e-02, 2.211861206e-02, 1.301985151e-01, 3.544821243e-02, 1.255397901e-01 };
            string[] names = { "V̇", "β̇", "α̇", "ṗ", "q̇", "ṙ" };
            for (int i = 0; i < 6; i++)
            {
                Debug.Log($"[AIRWING L1] skew {names[i]}: {d[i]:E6} against {mirror[i]:E6}");
                Assert.AreEqual(mirror[i], d[i], 2e-4 * System.Math.Abs(mirror[i]) + 1e-6, names[i]);
            }
        }

        // ── 3. Integrated: the doublet, and torque-free rotation ──────────

        static AirframeState LevelTrimmed(float vMps, float alpha) => new AirframeState
        {
            Position = new Vector3(0f, 7620f, 0f),
            Velocity = new Vector3(0f, 0f, vMps),
            Attitude = Quaternion.Euler(-alpha * Mathf.Rad2Deg, 0f, 0f),   // nose up by α, flight path level
            MassKg = MassKg,
            LoadFactor = 1f,
            Status = FlightStatus.Flying,
        };

        static void Measure(in AirframeState s, out double vFt, out double alpha, out double q, out double theta)
        {
            Vector3 uvw = AirframeL1.ToNasa(Quaternion.Inverse(s.Attitude) * s.Velocity);
            vFt = uvw.magnitude / Ft;
            alpha = System.Math.Atan2(uvw.z, uvw.x);
            q = AirframeL1.RatesToNasa(s.BodyRates).y;
            theta = System.Math.Asin((s.Attitude * Vector3.forward).y);
        }

        [Test]
        public void AStabilatorDoublet_TracksTheMirror()
        {
            // Mirror, standard gravity: trim α 0.132785057, stabilator −0.044526765, thrust 4,011.371970 lbf; then −2° for
            // 0.5 s, +2° for 0.5 s, trim again. Reference: RK4 at 1 ms. Here: the midpoint method at 5 ms (4 × 50 Hz).
            const float alpha0 = 0.132785057f, stab0 = -0.044526765f;
            float thrust = (float)(4011.371970 * Units.PoundForceToNewton);
            double[,] mirror =
            {   // t, V ft/s, α, q, θ
                { 0.5, 499.887431, 0.148849206, 0.064982177, 0.149056336 },
                { 1.0, 498.970032, 0.159233407, 0.002841330, 0.165679338 },
                { 1.5, 498.006183, 0.157023480, 0.009285697, 0.168755358 },
                { 2.0, 496.988472, 0.158075306, 0.015059933, 0.174854867 },
                { 2.5, 495.815910, 0.161630763, 0.020776084, 0.183807053 },
                { 3.0, 494.398132, 0.167313217, 0.026786824, 0.195681375 },
            };
            var airframe = new AirframeL1(FlightL0Tests_Profile(), Harv());
            var air = new AirData(Rho, 0f, 0f, 300f);
            AirframeState s = LevelTrimmed(Si(500), alpha0);
            const float dt = 0.005f;
            int row = 0;
            double worstAlpha = 0, worstQ = 0, worstTheta = 0, worstV = 0;
            for (int k = 1; k <= 600; k++)
            {
                // The schedule by step index: (k − 1) · 0.005f is 0.49999998 at the switch, and held the −2° one step too long.
                int i = k - 1;
                float ds = i < 100 ? -2 * D2R : i < 200 ? 2 * D2R : 0f;
                airframe.Step(ref s, new ControlDemand(new ControlSurfaces(stab0 + ds, 0f, 0f)), thrust, air, default, dt);
                if (k % 100 == 0)
                {
                    Measure(s, out double v, out double a, out double q, out double th);
                    worstV = System.Math.Max(worstV, System.Math.Abs(v - mirror[row, 1]));
                    worstAlpha = System.Math.Max(worstAlpha, System.Math.Abs(a - mirror[row, 2]));
                    worstQ = System.Math.Max(worstQ, System.Math.Abs(q - mirror[row, 3]));
                    worstTheta = System.Math.Max(worstTheta, System.Math.Abs(th - mirror[row, 4]));
                    row++;
                }
            }
            Debug.Log($"[AIRWING L1] doublet, worst over 3 s: V {worstV:F4} ft/s, α {worstAlpha:E2} rad, q {worstQ:E2} rad/s, θ {worstTheta:E2} rad");
            // Measured 2026-10-26: V 0.0007 ft/s, α and θ under 1e-6 rad, q under 1e-5 rad/s.
            Assert.Less(worstV, 0.01, "V within 0.01 ft/s");
            Assert.Less(worstAlpha, 1e-5, "α within 1e-5 rad");
            Assert.Less(worstQ, 1e-4, "q within 1e-4 rad/s");
            Assert.Less(worstTheta, 1e-5, "θ within 1e-5 rad");
        }

        [Test]
        public void TorqueFreeRotation_KeepsItsAngularMomentumAndEnergy()
        {
            // In a vacuum Euler's equations conserve the angular momentum in the world and the rotational energy.
            AirframeL1Profile p = Harv();
            var airframe = new AirframeL1(FlightL0Tests_Profile(), p);
            var air = new AirData(0f, 0f, 0f, 300f);
            AirframeState s = LevelTrimmed(100f, 0f);
            s.BodyRates = AirframeL1.RatesToUnity(new Vector3(1.0f, 0.5f, 0.2f));
            Matrix4x4 J = Matrix4x4.zero;
            J[0, 0] = p.IxxKgM2; J[1, 1] = p.IyyKgM2; J[2, 2] = p.IzzKgM2; J[0, 2] = J[2, 0] = -p.IxzKgM2; J[3, 3] = 1f;

            Vector3 H(in AirframeState st, out float energy)
            {
                Vector3 w = AirframeL1.RatesToNasa(st.BodyRates);
                Vector3 hb = J.MultiplyVector(w);
                energy = 0.5f * Vector3.Dot(w, hb);
                return st.Attitude * AirframeL1.ToUnity(hb);
            }
            Vector3 h0 = H(s, out float e0);
            for (int k = 0; k < 1000; k++) airframe.Step(ref s, new ControlDemand(default(ControlSurfaces)), 0f, air, default, 0.005f);
            Vector3 h1 = H(s, out float e1);
            Debug.Log($"[AIRWING L1] torque-free 5 s: |ΔH|/|H| {(h1 - h0).magnitude / h0.magnitude:E2}, ΔE/E {(e1 - e0) / e0:E2}");
            Assert.Less((h1 - h0).magnitude / h0.magnitude, 0.01f, "angular momentum in the world within 1 %");
            Assert.AreEqual(e0, e1, 0.01f * e0, "rotational energy within 1 %");
        }

        [Test]
        public void TwoRuns_AreBitIdentical()
        {
            AirframeState Run()
            {
                var airframe = new AirframeL1(FlightL0Tests_Profile(), Harv());
                var air = new AirData(Rho, 0f, 0f, 300f);
                AirframeState s = LevelTrimmed(Si(500), 0.13f);
                for (int k = 0; k < 2000; k++)
                    airframe.Step(ref s, new ControlDemand(new ControlSurfaces(-0.04f + 0.01f * Mathf.Sin(k * 0.01f), 0.02f, -0.01f)), 18000f, air, default, 0.005f);
                return s;
            }
            AirframeState a = Run(), b = Run();
            Assert.AreEqual(a.Position.x, b.Position.x); Assert.AreEqual(a.Position.y, b.Position.y); Assert.AreEqual(a.Position.z, b.Position.z);
            Assert.AreEqual(a.Attitude, b.Attitude);
            Assert.AreEqual(a.BodyRates.x, b.BodyRates.x); Assert.AreEqual(a.BodyRates.z, b.BodyRates.z);
        }

        [Test]
        public void TheF18EProfile_CarriesThePapersPolynomials_AndAScaledInertia()
        {
            AircraftProfile e = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
            Assert.IsTrue(e.AirframeL1.IsSet, "Deep Water → Air Wing → Apply HARV L1 Data to F-A-18E");
            AeroPolynomialsL1 a = e.AirframeL1.Aero, h = Harv().Aero;
            CollectionAssert.AreEqual(h.Cma, a.Cma); CollectionAssert.AreEqual(h.Cmds, a.Cmds); CollectionAssert.AreEqual(h.Cmq, a.Cmq);
            CollectionAssert.AreEqual(h.CL, a.CL); CollectionAssert.AreEqual(h.CLds, a.CLds); CollectionAssert.AreEqual(h.CD, a.CD);
            Assert.AreEqual(h.CD0, a.CD0); CollectionAssert.AreEqual(h.Cdds, a.Cdds);
            CollectionAssert.AreEqual(h.Cyb, a.Cyb); CollectionAssert.AreEqual(h.Cyda, a.Cyda); CollectionAssert.AreEqual(h.Cydr, a.Cydr);
            CollectionAssert.AreEqual(h.Clb, a.Clb); CollectionAssert.AreEqual(h.Clda, a.Clda); CollectionAssert.AreEqual(h.Cldr, a.Cldr);
            CollectionAssert.AreEqual(h.Clp, a.Clp); CollectionAssert.AreEqual(h.Clr, a.Clr);
            CollectionAssert.AreEqual(h.Cnb, a.Cnb); CollectionAssert.AreEqual(h.Cnda, a.Cnda); CollectionAssert.AreEqual(h.Cndr, a.Cndr);
            CollectionAssert.AreEqual(h.Cnp, a.Cnp); CollectionAssert.AreEqual(h.Cnr, a.Cnr);

            AirframeL1Profile harv = Harv(), l1 = e.AirframeL1;
            Debug.Log($"[AIRWING L1] E inertia, kg·m²: Ixx {l1.IxxKgM2:F0}, Iyy {l1.IyyKgM2:F0}, Izz {l1.IzzKgM2:F0}, Ixz {l1.IxzKgM2:F0}; chord {l1.ChordM:F3} m");
            Assert.Greater(l1.IxxKgM2, harv.IxxKgM2 * 1.5f, "the E is heavier and spans more: more roll inertia");
            Assert.Greater(l1.IyyKgM2, harv.IyyKgM2 * 1.2f);
            Assert.Less(l1.IxzKgM2, 0f, "signed as TM-107601 signs it");
            Assert.AreEqual(e.Airframe.WingAreaM2, l1.ReferenceAreaM2, "the E's own wing");
        }

        /// <summary>The L0 airframe numbers L1 borrows (flap and speed-brake increments); any valid set will do here.</summary>
        static AirframeProfile FlightL0Tests_Profile() => new AirframeProfile
        {
            WingAreaM2 = 37.16f, SpanM = 11.4f, EmptyMassKg = 10000f, InternalFuelKg = 5000f, CD0 = 0.02f, OswaldEfficiency = 0.8f,
            CLmax = 1.6f, CLmaxNegative = 0.8f, CLalphaPerRad = 3.5f, FlapsDeltaCLmax = 0.5f, FlapsDeltaCD = 0.04f, SpeedBrakeDeltaCD = 0.05f,
        };
    }
}
