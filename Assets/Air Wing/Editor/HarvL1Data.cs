using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Deep Water → Air Wing → Apply HARV L1 Data to F-A-18E (slice 2a, W32). Writes the published F/A-18 HARV
    /// aero polynomials into the F/A-18E profile, with the inertia scaled from the HARV's to the E's size (AUTHORED,
    /// computed here from the public figures named below, so the numbers and their provenance cannot drift apart).
    /// Run again after editing this file; it overwrites only the L1 fields and the L1 paragraph of the provenance.
    /// </summary>
    public static class HarvL1Data
    {
        const string ProfilePath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string Marker = "--- Airframe L1 (W32) ---";

        // PUBLIC: NASA TM-107601 (Buttrill, Arbuckle and Hoffler, 1992) Table 3.5, first row: the HARV model's weight and inertia.
        const double HarvWeightLb = 33310, HarvIxx = 23000, HarvIyy = 151293, HarvIzz = 169945, HarvIxz = -2971;
        // PUBLIC: HARV reference span (TM-107601 Table 3.4); Hornet length 56 ft (U.S. Navy F/A-18A-D fact file).
        const double HarvSpanFt = 37.42, HornetLengthFt = 56.0;
        // PUBLIC: Super Hornet span 44.9 ft and length 60.3 ft (U.S. Navy F/A-18E/F fact file, 2021).
        const double SuperHornetSpanFt = 44.9, SuperHornetLengthFt = 60.3;
        // AUTHORED: the E's mean aerodynamic chord, the HARV's 11.52 ft scaled by (S_E/S_A)/(b_E/b_A).
        const double SuperHornetChordFt = 12.0;
        const double FuelFractionForInertia = 0.6;   // AUTHORED: the HARV row is at 60 % internal fuel; match it

        [MenuItem("Deep Water/Air Wing/Apply HARV L1 Data to F-A-18E")]
        public static void Apply()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            if (so == null) { Debug.LogError($"[Air Wing] {ProfilePath} not found; nothing applied."); return; }
            Undo.RecordObject(so, "Apply HARV L1 data");

            // PUBLIC: Chakraborty, Seiler and Balas, J. Guidance 34(1) 2011, Appendix Tables A1–A2 (= F18AeroData.mat to 4 places).
            so.cma = new[] { -0.0866f, 0.5110f, -1.2897f };
            so.cmds = new[] { -0.9051f, -0.3245f, 0.9338f };
            so.cmq = new[] { -4.1186f, 10.9921f, -68.5641f, 64.7190f };
            so.cLift = new[] { -0.0204f, 5.677f, -5.4246f, 1.1645f };
            so.cLiftDs = new[] { 0.5725f, 0.4055f, -2.6975f, 2.1852f };
            so.cDrag = new[] { -1.4994f, -0.1995f, 6.3971f, -5.7341f, 1.4610f };
            so.cDrag0 = 1.5036f;
            so.cDragDs = new[] { 0.0366f, -0.2739f, 4.2360f, -3.8578f };
            so.cyb = new[] { -0.7344f, 0.2654f, -0.1926f };
            so.cyda = new[] { -0.1656f, -0.2403f, 1.5317f, -0.8500f };
            so.cydr = new[] { 0.2054f, 0.4082f, -1.6921f, 0.9351f };
            so.clb = new[] { -0.0556f, -0.4153f, -0.3620f, 2.3843f, -1.6196f };
            so.clda = new[] { 0.1424f, -0.0516f, -0.2646f, 0.1989f };
            so.cldr = new[] { 0.0129f, 0.0014f, 0.0083f, -0.0274f };
            so.clp = new[] { -0.3540f, 0.2377f };
            so.clr = new[] { 0.1983f, 0.7804f, -1.0871f };
            so.cnb = new[] { 0.0885f, 0.0329f, -0.3816f };
            so.cnda = new[] { 0.0104f, 0.0584f, -0.3413f, 0.2694f };
            so.cndr = new[] { -0.0780f, -0.0176f, 0.5564f, -0.8980f, 0.3899f };
            so.cnp = new[] { 0.0792f, -0.0881f };
            so.cnr = new[] { -0.4326f, -0.1307f };
            so.alphaMinDeg = -10f;
            so.alphaMaxDeg = 60f;
            so.meanChordFt = (float)SuperHornetChordFt;

            // AUTHORED scaling: I ∝ m·r². Roll with the span, pitch and yaw with the length, the product with both.
            double weightLb = so.emptyWeightLb + FuelFractionForInertia * so.internalFuelLb;
            double m = weightLb / HarvWeightLb;
            double span = SuperHornetSpanFt / HarvSpanFt, length = SuperHornetLengthFt / HornetLengthFt;
            so.ixxSlugFt2 = (float)(HarvIxx * m * span * span);
            so.iyySlugFt2 = (float)(HarvIyy * m * length * length);
            so.izzSlugFt2 = (float)(HarvIzz * m * length * length);
            so.ixzSlugFt2 = (float)(HarvIxz * m * span * length);

            string paragraph =
                Marker + "\n" +
                "cma, cmds, cmq, cLift, cLiftDs, cDrag, cDrag0, cDragDs, cyb, cyda, cydr, clb, clda, cldr, clp, clr, cnb, cnda, cndr, cnp, cnr: " +
                "PUBLIC (W32): the F/A-18 HARV polynomial fits of Chakraborty, Seiler and Balas, J. Guidance 34(1) 2011, " +
                "Appendix Tables A1-A2, fitted to NASA HARV flight data; checked against the authors' F18AeroData.mat. Valid for alpha " +
                "0-60 deg; alphaMinDeg -10 and alphaMaxDeg 60: AUTHORED, the polynomials held flat outside them. Sideslip enters linearly; no flaps or speed brake in the fits " +
                "(the L0 increments are used, AUTHORED). Applied to the E's own wing (500 ft2, 44.9 ft): the E is flown on the A/B's " +
                "coefficients, scaled by its reference geometry (AUTHORED, W8).\n" +
                $"meanChordFt 12.0: AUTHORED, the HARV's 11.52 ft times (S_E/S_A)/(b_E/b_A).\n" +
                $"ixxSlugFt2, iyySlugFt2, izzSlugFt2, ixzSlugFt2: AUTHORED, NASA TM-107601 Table 3.5 (33,310 lb: Ixx 23,000, Iyy 151,293, Izz 169,945, Ixz -2,971 slug ft2) " +
                $"scaled as m r2 to the E at 60 % fuel ({weightLb:F0} lb, mass x{m:F4}); roll by span ({SuperHornetSpanFt}/{HarvSpanFt} ft), " +
                $"pitch and yaw by length ({SuperHornetLengthFt}/{HornetLengthFt} ft, Navy fact files), the product by both: " +
                $"Ixx {so.ixxSlugFt2:F0}, Iyy {so.iyySlugFt2:F0}, Izz {so.izzSlugFt2:F0}, Ixz {so.ixzSlugFt2:F0} slug ft2. " +
                "Fixed at L1; fuel burn moves them at L3.\n" +
                "Note: the published f18full.m adds the thrust term of beta-dot with the wrong sign (T cos(alpha) sin(beta)/(mV)); " +
                "Air Wing derives its motion from body kinematics and does not inherit it (Calibration~/l1_harv_mirror.py).";
            string prov = so.provenance ?? "";
            int at = prov.IndexOf(Marker, System.StringComparison.Ordinal);
            so.provenance = (at >= 0 ? prov.Substring(0, at).TrimEnd() : prov.TrimEnd()) + "\n\n" + paragraph;

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            so.Bake();   // throws if anything is out of shape
            Debug.Log($"[Air Wing] Applied the HARV L1 data to {ProfilePath}: Ixx {so.ixxSlugFt2:F0}, Iyy {so.iyySlugFt2:F0}, Izz {so.izzSlugFt2:F0}, Ixz {so.ixzSlugFt2:F0} slug·ft² at {weightLb:F0} lb.");
        }
    }
}
