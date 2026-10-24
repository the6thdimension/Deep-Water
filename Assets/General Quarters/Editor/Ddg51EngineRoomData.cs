using System.Linq;
using System.Text;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.EngineRoom;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Editor
{
    /// <summary>
    /// The DDG-51's Engine Room and Helm numbers, with where each came from. Hull geometry and mass are the
    /// Blender layout's. Hydrodynamics are DTMB 5415's, the public research hull of the DDG-51 preliminary
    /// design; real-ship handling figures come from Stavridis (USNI Proceedings, Oct 1994, the CO of USS Barry).
    ///
    /// Four numbers are CALIBRATED, each to one public figure, and everything else is checked against the
    /// figures it was not fitted to (EngineRoomL0Tests):
    ///   - the appendage factor on resistance, to 31 kn on 100,000 shp;
    ///   - γ_R and ℓ'_R (rudder flow straightening), least squares on Barry's three turn diameters;
    ///   - astern thrust, to Barry's one-minute stop from ahead full.
    /// The calibration was explored in a Python mirror of this model (2026-10-24) and is re-derived here where
    /// it can be, so the numbers stay reproducible from their sources.
    /// </summary>
    public static class Ddg51EngineRoomData
    {
        const float Rho = 1025f, G = 9.81f, KnToMs = 0.514444f;

        // Navy fact file, "Destroyers (DDG 51)": 100,000 total shp; "in excess of 30 knots".
        public const float InstalledShp = 100000f;
        public const float TopSpeedKn = 31f;            // AUTHORED within "in excess of 30 knots"
        const float ShaftingEfficiency = 0.98f;          // AUTHORED

        // ITTC 27th Resistance Committee world-wide campaign, CEHIPAR 2716 (5415 geosim, Lpp 5.72 m, S 4.786 m², λ 24.824).
        static readonly (float fr, float ct)[] CtPoints = { (0.10f, 3.975e-3f), (0.28f, 4.211e-3f), (0.41f, 6.499e-3f) };
        const float FormFactorK = 0.15f, ModelLpp = 5.72f, ModelScale = 24.824f, ModelWettedM2 = 4.786f, ShipLpp5415 = 142f;
        const float NuModel = 1.1386e-6f, NuShip = 1.1883e-6f;

        // MARIN 5415 propeller 4058 open water, 5th-order polynomials (SIMMAN 2008); D 6.15 m, P/D 0.87, 5 blades.
        static readonly float[] KtMarin = { 0.398399f, -0.067794f, -1.286040f, 2.286960f, -2.039820f, 0.676134f };
        static readonly float[] KqMarin = { 0.051144f, -0.000390f, -0.171650f, 0.330060f, -0.327865f, 0.119477f };

        /// <summary>Extra roll damping from hull lift at flank, fraction of critical (AUTHORED: roll damping grows with speed; Ikeda's breakdown is L1).</summary>
        public const float RollLiftDampingRatio = 0.1f;

        // CALIBRATED (see the class summary).
        const float GammaR = 0.9f, LrNd = -0.9f, AsternThrustFraction = 0.5f;

        public static EngineRoomProfileData EngineRoom(HullLayout layout, out string provenance)
        {
            HullLayout.Hydrostatics h = layout.hydrostatics;
            float L = h.length_wl_m, d = h.draft_m, m = h.mass_kg;
            float mNd = m / (0.5f * Rho * L * L * d);
            (float k0, float k1, float k2) = QuadraticFit(KtMarin, 0.85f);
            (float q0, float q1, float q2) = QuadraticFit(KqMarin, 0.85f);

            var prop = new MmgPropellerData
            {
                DiameterM = 6.15f, K0 = k0, K1 = k1, K2 = k2, Q0 = q0, Q1 = q1, Q2 = q2,
                WakeStraight = 0.031f, ThrustDeduction = 0.095f,
                XpNd = -0.431f, C1 = 2.0f, C2Positive = 1.6f, C2Negative = 1.1f, AsternThrustFraction = AsternThrustFraction,
            };
            MmgPropellerData port = prop, stbd = prop;
            port.YpM = -4.65f;
            stbd.YpM = 4.65f;

            const float area = 15.4f, span = 4.58f;
            float aspect = span * span / area;
            var rudder = new MmgRudderData
            {
                AreaM2 = area, SpanM = span, FAlpha = 6.13f * aspect / (aspect + 2.25f),
                Epsilon = 1.0f, Kappa = 0.5f, GammaRPositive = GammaR, GammaRNegative = GammaR, LrNd = LrNd,
                Tr = 0.3f, Ah = 0.25f, XhNd = -0.45f, XrNd = -0.47f,
                StallAngleDeg = 35f, PostStallFraction = 0.7f, DepthM = 4.5f,
            };
            MmgRudderData portR = rudder, stbdR = rudder;
            portR.YrM = -4.75f;
            stbdR.YrM = 4.75f;

            var hull = new MmgHullData
            {
                LppM = L, BeamM = h.beam_wl_m, DraftM = d, MassKg = m,
                XgM = h.centre_of_mass[2], IzzKgM2 = m * h.gyradius_m.yaw * h.gyradius_m.yaw,
                MxNd = 0.04f * mNd, MyNd = 0.1111f, JzNd = 0.0096f,
                Derivatives = Derivatives(mNd),
            };

            // Appendage factor: the one multiplier on the bare-hull curve that makes 100,000 shp drive her at 31 kn.
            float targetPerShaft = InstalledShp * 745.7f * ShaftingEfficiency / 2f;
            float lo = 0.5f, hi = 6f, kApp = 1f;
            for (int i = 0; i < 60; i++)
            {
                kApp = 0.5f * (lo + hi);
                hull.ResistanceSpeedMs = SpeedTable();
                hull.ResistanceN = hull.ResistanceSpeedMs.Select(v => kApp * BareResistance(v)).ToArray();
                float v31 = TopSpeedKn * KnToMs;
                float n = Mmg.SteadyShaftRps(hull, prop, 2, v31);
                if (Mmg.DeliveredPowerW(prop, v31, n, prop.WakeStraight) > targetPerShaft) hi = kApp; else lo = kApp;
            }
            float maxRps = Mmg.SteadyShaftRps(hull, prop, 2, TopSpeedKn * KnToMs);

            var profile = new EngineRoomProfileData
            {
                Hull = hull, Port = port, Starboard = stbd, PortRudder = portR, StarboardRudder = stbdR,
                MaxShaftRps = maxRps, MinShaftRps = -0.75f * maxRps, ShaftRateRpsPerS = 0.1f,
                MaxRudderDeg = 35f, RudderRateDegPerS = 9f,
            };

            var p = new StringBuilder();
            p.AppendLine("DDG-51 Engine Room L0: MMG standard method (Yasukawa & Yoshimura 2015), twin screw, twin rudder.");
            p.AppendLine($"HULL: L {L:0.0} m, B {h.beam_wl_m:0.00} m, d {d:0.00} m, {m / 1000f:0} t, x_G {hull.XgM:0.00} m, k_zz {h.gyradius_m.yaw:0.00} m: the Blender layout (slice 3).");
            p.AppendLine("DERIVATIVES: DTMB 5415 bare hull, IIHR PMM (Yoon 2009, Iowa PhD thesis, Tables 6-2/6-3) at Fn 0.138 / 0.280 / 0.410, interpolated by speed.");
            p.AppendLine($"  N'r at Fn 0.28/0.41 from Table 6-12 (Table 6-3 repeats Y'r there, a typo). Cross terms Fn 0.28 IIHR (low confidence, 12-152 % facility scatter).");
            p.AppendLine($"  Y'r: published -0.0485 makes the bare hull directionally unstable and turns at a third of Barry's diameters; read as containing the");
            p.AppendLine($"  centripetal mass term, so hydrodynamic Y'r = published + m' ({mNd:0.0000}). CALIBRATED choice between two readings of one table.");
            p.AppendLine("ADDED MASS: m'y 0.1111, J'z 0.0096 (three-facility means, Yoon 2009 Tables 6-11..14). m'x = 0.04 m': AUTHORED (not found for 5415).");
            p.AppendLine($"RESISTANCE: ITTC 27th campaign C_T at Fn 0.10/0.28/0.41 (3.975/4.211/6.499e-3), ITTC-57 with k = 0.15 to full scale (S {ModelWettedM2 * ModelScale * ModelScale:0} m²),");
            p.AppendLine($"  C_R linear in Fn between points. x {kApp:0.000} appendage/condition factor: CALIBRATED so 100,000 shp (fact file, 2 % shafting loss) makes {TopSpeedKn:0} kn (AUTHORED within '30+ kn').");
            p.AppendLine($"PROPELLERS: MARIN 4058 (SIMMAN 2008): D 6.15 m, P/D 0.87; K_T, K_Q quadratic fits of MARIN's polynomials over J 0-0.85: K_T {k0:0.0000} {k1:0.0000} {k2:0.0000}.");
            p.AppendLine("  The real ship has 17 ft controllable-reversible-pitch screws (USNI 1994); at L0 the 5415 propeller at fixed pitch stands in.");
            p.AppendLine("  w_P0 0.031, t_P 0.095: SECONDARY (Kim 2018 'quoted', source not given). x'_P -0.431, y_P 4.65 m: DERIVED from the MARIN arrangement drawing.");
            p.AppendLine("  C1 2.0, C2 1.6/1.1: AUTHORED (the standard method's KVLCC2 values; none for 5415).");
            p.AppendLine($"  Max {maxRps * 60f:0} rpm = steady {TopSpeedKn:0} kn; astern max 75 % of that (AUTHORED). Astern K_T = {AsternThrustFraction} x K0: CALIBRATED to stop from ahead full in about 1 min (USNI 1994).");
            p.AppendLine("  Shaft rate 6 rpm/s: AUTHORED (USNI 1994: all stop to ahead flank in under 90 s; it also stands in for pitch reversal).");
            p.AppendLine($"RUDDERS: 2 spade rudders, 15.4 m² each (SIMMAN 2008), span 4.58 m (DERIVED from the MARIN drawing), aspect {aspect:0.00}, f_alpha {rudder.FAlpha:0.00} (Fujii).");
            p.AppendLine("  x'_R -0.47, y_R 4.75 m: DERIVED from the drawing. Rate 9 deg/s (5415 design value, SIMMAN). Max 35 deg (USNI 1994 'full rudder').");
            p.AppendLine($"  gamma_R {GammaR}, l'_R {LrNd}: CALIBRATED, least squares on Barry's turn diameters (500 yd at 10 kn / 35 deg, 1,000 yd at 15 kn / 15 deg, 1,200 yd at 30 kn / 15 deg).");
            p.AppendLine("  t_R 0.3, a_H 0.25, x'_H -0.45, epsilon 1.0, kappa 0.5, stall 35 deg with 70 % post-stall, centre of pressure 4.5 m deep: AUTHORED (not found for 5415).");
            p.AppendLine("LIMITS: 3-DOF manoeuvring (heel applied from the sway forces' depth); no wind, current or shallow water; 5415 is 7 % beamier than this hull.");
            provenance = p.ToString();
            return profile;
        }

        public static HelmProfileData Helm(HullLayout layout, out string provenance)
        {
            var helm = new HelmProfileData
            {
                AheadBellKn = new[] { 0f, 5f, 10f, 15f, 20f, TopSpeedKn },
                AsternBellFraction = new[] { 1f / 3f, 2f / 3f, 0.85f, 1f },
                CourseKp = 2f, CourseKd = 20f, CourseKi = 0f, CourseMaxRudderDeg = 20f,
                SpeedKp = 0.05f, SpeedKi = 0.001f,
                RouteRudderDeg = 15f, LookAheadShipLengths = 2f, ShipLengthM = layout.hydrostatics.length_wl_m,
            };
            provenance =
                "Bells: USN convention (1/3, 2/3, standard, full = 4/3 standard, flank = maximum); standard 15 kn AUTHORED; flank = the Engine Room's top speed.\n" +
                "Astern bells: 1/3, 2/3, 0.85, 1.0 of maximum astern revolutions (AUTHORED).\n" +
                "Course hold: Kp 2, Kd 20 deg/(deg/s), at most 20 deg of rudder (AUTHORED, tuned on this hull: a 90 deg course change settles to 1 deg without overshoot in 183 / 109 / 82 s at 10 / 20 / 30 kn).\n" +
                "Speed hold: steady-revolution feed-forward plus Kp 0.05 rev/s per kn, Ki 0.001 (AUTHORED).\n" +
                "Routes: plan turns at 15 deg (USN standard rudder), line-of-sight look-ahead 2 ship lengths (AUTHORED).";
            return helm;
        }

        static MmgHullDerivatives[] Derivatives(float mNd)
        {
            // Cross terms: IIHR Table 6-3 at Fn 0.280, used at every speed.
            const float Xvr = 0.0819f, Yvrr = -0.8682f, Yvvr = -1.5172f, Nvrr = -0.1989f, Nvvr = -0.7220f;
            MmgHullDerivatives Set(float fr, float xvv, float xrr, float yv, float yvvv, float nv, float nvvv, float yr, float yrrr, float nr, float nrrr) =>
                new MmgHullDerivatives
                {
                    Froude = fr, Xvv = xvv, Xvr = Xvr, Xrr = xrr, Xvvvv = 0f,
                    Yv = yv, Yr = yr + mNd, Yvvv = yvvv, Yvvr = Yvvr, Yvrr = Yvrr, Yrrr = yrrr,
                    Nv = nv, Nr = nr, Nvvv = nvvv, Nvvr = Nvvr, Nvrr = Nvrr, Nrrr = nrrr,
                };
            return new[]
            {
                Set(0.138f, -0.0301f, -0.0078f, -0.2637f, -1.6256f, -0.1396f, -0.3426f, -0.0276f, -0.0370f, -0.0382f, -0.0211f),
                Set(0.280f, -0.1528f, -0.0282f, -0.2961f, -1.9456f, -0.1667f, -0.4355f, -0.0485f, -0.0452f, -0.0458f, -0.0505f),
                Set(0.410f, -0.1544f, -0.0385f, -0.2963f, -3.7914f, -0.1717f, -1.2591f, -0.0548f, -0.0710f, -0.0456f, -0.0821f),
            };
        }

        static float[] SpeedTable() => Enumerable.Range(1, 68).Select(i => i * 0.5f * KnToMs).ToArray();   // 0.5 to 34 kn

        /// <summary>5415 bare-hull resistance at full scale, N: ITTC-57 friction with form factor, plus residuary C_R linear in Fn.</summary>
        public static float BareResistance(float v)
        {
            float fr = v / Mathf.Sqrt(G * ShipLpp5415);
            float cr = ResiduaryAt(fr);
            float ct = (1f + FormFactorK) * Ittc57(v * ShipLpp5415 / NuShip) + cr;
            float s = ModelWettedM2 * ModelScale * ModelScale;
            return 0.5f * Rho * s * v * v * ct;
        }

        static float ResiduaryAt(float fr)
        {
            float Cr(int i)
            {
                float vm = CtPoints[i].fr * Mathf.Sqrt(G * ModelLpp);
                return Mathf.Max(CtPoints[i].ct - (1f + FormFactorK) * Ittc57(vm * ModelLpp / NuModel), 0f);
            }
            if (fr <= CtPoints[0].fr) return Cr(0);
            for (int i = 1; i < CtPoints.Length; i++)
                if (fr <= CtPoints[i].fr)
                    return Mathf.Lerp(Cr(i - 1), Cr(i), (fr - CtPoints[i - 1].fr) / (CtPoints[i].fr - CtPoints[i - 1].fr));
            int n = CtPoints.Length - 1;   // past the last point: continue the last slope (a short extrapolation, to Fn 0.43)
            return Cr(n) + (Cr(n) - Cr(n - 1)) * (fr - CtPoints[n].fr) / (CtPoints[n].fr - CtPoints[n - 1].fr);
        }

        static float Ittc57(float re) => 0.075f / Mathf.Pow(Mathf.Log10(re) - 2f, 2f);

        /// <summary>Least-squares a + bJ + cJ² through a 5th-order polynomial, sampled on J ∈ [0, jMax].</summary>
        static (float, float, float) QuadraticFit(float[] c5, float jMax)
        {
            double[,] a = new double[3, 3];
            double[] b = new double[3];
            for (int i = 0; i <= 40; i++)
            {
                double j = i * jMax / 40.0, y = 0, jp = 1;
                for (int k = 0; k < c5.Length; k++) { y += c5[k] * jp; jp *= j; }
                double[] phi = { 1, j, j * j };
                for (int r = 0; r < 3; r++) { b[r] += phi[r] * y; for (int c = 0; c < 3; c++) a[r, c] += phi[r] * phi[c]; }
            }
            for (int i = 0; i < 3; i++)   // Gauss-Jordan, 3 × 3
            {
                double piv = a[i, i];
                for (int c = 0; c < 3; c++) a[i, c] /= piv;
                b[i] /= piv;
                for (int r = 0; r < 3; r++)
                {
                    if (r == i) continue;
                    double f = a[r, i];
                    for (int c = 0; c < 3; c++) a[r, c] -= f * a[i, c];
                    b[r] -= f * b[i];
                }
            }
            return ((float)b[0], (float)b[1], (float)b[2]);
        }
    }
}
