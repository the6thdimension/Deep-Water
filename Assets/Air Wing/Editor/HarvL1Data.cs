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
        const string VisualsPath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_Visuals.asset";
        // AUTHORED: the centre of mass sits 1.6 m aft of the F-18E model's origin (measured off the model's rig, slice 2d).
        static readonly Vector3 ModelOffsetM = new Vector3(0f, 0f, 1.6f);
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

        /// <summary>
        /// The F-18E model's gear rig (AUTHORED, slice 2d). The model has gear bones but no retraction animation, and the
        /// vendor rig named only two of its parts, so each hinge was solved off the geometry on 2026-10-26: the nose leg
        /// folds forward 105° until its wheel clears the closed doors; each main leg swings aft, up and in to put its
        /// wheel flat in the bay its doors cover; each door turns about its own long edge until it lies flat over the bay.
        /// Braces and the legs hide once stowed, behind the closed doors.
        /// </summary>
        static AircraftVisualsSO.GearPart[] GearRig()
        {
            AircraftVisualsSO.GearPart Leg(string n, Vector3 axis, float deg, bool hide = true) =>
                new AircraftVisualsSO.GearPart { transformName = n, axis = axis, stowedAngleDeg = deg, startFraction = 0.05f, endFraction = 0.75f, hideWhenStowed = hide };
            AircraftVisualsSO.GearPart Door(string n, Vector3 axis, float deg, bool hide = false) =>
                new AircraftVisualsSO.GearPart { transformName = n, axis = axis, stowedAngleDeg = deg, startFraction = 0.7f, endFraction = 1f, hideWhenStowed = hide };
            return new[]
            {
                Leg("CenterUpLeg", Vector3.right, -105f),
                Leg("CenterUpLeg_Aux2.004", Vector3.right, -30f),
                Leg("LeftUpLeg", new Vector3(0.1013f, -0.4080f, -0.9073f), 99.4f),
                Leg("RightUpLeg", new Vector3(0.1299f, 0.4211f, 0.8977f), 96.6f),
                Leg("LeftUpLeg_Aux4.1", Vector3.right, -75f),
                Leg("RightUpLeg_Aux4.1", Vector3.right, -90f),
                // The doors' actuator links: they ride on the doors and hide once the doors are shut.
                Door("LeftUpLeg_Aux1.1", Vector3.right, 0f, hide: true),
                Door("LeftUpLeg_Aux2.1", Vector3.right, 0f, hide: true),
                Door("RightUpLeg_Aux1.1", Vector3.right, 0f, hide: true),
                Door("RightUpLeg_Aux2.1", Vector3.right, 0f, hide: true),
                Door("CenterUpLeg_Aux9.005", new Vector3(-0.001f, -0.016f, -1.000f), -74f),
                Door("CenterUpLeg_Aux6.003", new Vector3(-0.014f, 0.002f, 1.000f), -90f),
                Door("CenterUpLeg_Aux10.003", new Vector3(0.000f, -0.006f, -1.000f), 96f),
                Door("LeftUpLeg_Aux1", new Vector3(-0.087f, -0.005f, 0.996f), 126f),
                Door("LeftUpLeg_Aux3", new Vector3(-0.003f, -0.096f, 0.995f), 93f),
                Door("LeftUpLeg_Aux2", new Vector3(0.005f, -0.004f, 1.000f), 95f),
                Door("RightUpLeg_Aux1", new Vector3(0.009f, -0.003f, -1.000f), 102f),   // a thicker mesh than its twin: its flattest close
                Door("RightUpLeg_Aux3", new Vector3(0.006f, -0.017f, -1.000f), 92f),
                Door("RightUpLeg_Aux2", new Vector3(0.005f, 0.009f, -1.000f), 81f),
            };
        }

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
            // AUTHORED: the drag floor is the L0 polar (this profile's cd0 and K = 1/(pi e AR)).
            so.dragFloorCd0 = so.cd0;
            so.dragFloorK = (float)(1.0 / (System.Math.PI * so.oswaldEfficiency * so.spanFt * so.spanFt / so.wingAreaFt2));

            // Powerplant L1 (slice 2c). AUTHORED: idle to military about 4 s (95 % in 3.6 s), down a little faster; the
            // afterburner lights above 90 % core spool and comes in over about a second.
            so.spoolUpTauS = 1.2f;
            so.spoolDownTauS = 0.8f;
            so.afterburnerLightTauS = 0.4f;
            so.afterburnerOffTauS = 0.25f;
            so.afterburnerMinSpool = 0.9f;

            // Flight Controls L1 (slice 2b). AUTHORED gains, tuned in Calibration~/l1_fbw_mirror.py.
            so.alphaLimitDeg = 30f;
            so.loadFactorCommandTauS = 0.3f;
            so.alphaGainPerS = 2.0f;
            so.loadFactorIntegralDegPerGs = 0.02f * Mathf.Rad2Deg;
            so.pitchRateBandwidth = 6f;
            so.rollRateBandwidth = 5f;
            so.yawRateBandwidth = 3f;
            so.sideslipGainPerS = 2f;
            so.sideslipPerPedalDeg = 8f;
            // PUBLIC: actuators, NASA TM-107601 via Chakraborty, Seiler and Balas 2011, Table 2.
            so.stabilatorBandwidth = 30f; so.stabilatorRateDegS = 40f; so.stabilatorMinDeg = -24f; so.stabilatorMaxDeg = 10.5f;
            so.aileronBandwidth = 48f; so.aileronRateDegS = 100f; so.aileronMinDeg = -25f; so.aileronMaxDeg = 45f;
            so.rudderBandwidth = 40f; so.rudderRateDegS = 61f; so.rudderMinDeg = -30f; so.rudderMaxDeg = 30f;

            // Landing Gear L1 (slice 2d, W4). AUTHORED geometry off the model's wheels; the struts sized so the static
            // load compresses each about half its travel, with the nose taking its moment-arm share (~12 %).
            so.wheels = new[]
            {
                new AircraftProfileSO.Wheel { name = "nose gear", contactPointM = new Vector3(0f, -1.71f, 5.17f), travelM = 0.3f,
                                              springNPerM = 188e3f, damperNsPerM = 25e3f, steerable = true },
                new AircraftProfileSO.Wheel { name = "left main gear", contactPointM = new Vector3(-1.40f, -1.71f, -0.74f), travelM = 0.3f,
                                              springNPerM = 661e3f, damperNsPerM = 88e3f, braked = true },
                new AircraftProfileSO.Wheel { name = "right main gear", contactPointM = new Vector3(1.40f, -1.71f, -0.74f), travelM = 0.3f,
                                              springNPerM = 661e3f, damperNsPerM = 88e3f, braked = true },
            };
            so.hardPointsM = new[]
            {
                new Vector3(0f, -1.0f, 0f),     // belly
                new Vector3(0f, -0.2f, -6.4f),  // tail (a strike near 15° nose up on the mains)
                new Vector3(0f, -0.4f, 10.0f),  // nose
                new Vector3(-6.8f, 0f, -2f), new Vector3(6.8f, 0f, -2f),   // wingtips
            };
            so.gearMaxSinkRateFtS = 24f;
            so.tyreRollingFriction = 0.02f;
            so.tyreBrakingFriction = 0.5f;
            so.tyreLateralFriction = 0.7f;
            so.tyreSlipSpeedMps = 0.5f;
            so.nosewheelSteerMaxDeg = 20f;
            so.nosewheelSteerFadeStartKt = 40f;
            so.nosewheelSteerFadeEndKt = 80f;
            so.gearTransitTimeS = 4f;
            so.gearDownDeltaCd = 0.02f;
            // AUTHORED (SIX, 2026-10-26): the struts stiffen over their last 30 % of travel, so a hands-off touchdown's
            // nose slap is absorbed instead of bottoming the nose strut.
            so.strutEndStopFraction = 0.7f;
            so.strutEndStopStiffening = 30f;

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
                "dragFloorCd0, dragFloorK: AUTHORED, the L0 polar (cd0 and 1/(pi e AR)) as a floor under the fits' drag, which is " +
                "almost nil at low alpha (C_D 0.004 at alpha 0; the authors note their low-alpha data are thin).\n" +
                "spoolUpTauS 1.2, spoolDownTauS 0.8, afterburnerLightTauS 0.4, afterburnerOffTauS 0.25, afterburnerMinSpool 0.9: " +
                "AUTHORED, Powerplant L1's spool (slice 2c); no public F414 spool figures were found.\n" +
                "Flight Controls L1 (slice 2b, W23): alphaLimitDeg 30, loadFactorCommandTauS 0.3, alphaGainPerS 2, " +
                "loadFactorIntegralDegPerGs 1.15, pitchRateBandwidth 6, rollRateBandwidth 5, yawRateBandwidth 3, sideslipGainPerS 2, " +
                "sideslipPerPedalDeg 8: AUTHORED, a nonlinear-dynamic-inversion law after Snell, Enns and Garrard (J. Guidance 15(4), 1992), " +
                "tuned in Calibration~/l1_fbw_mirror.py against criteria fixed before tuning (ROADMAP slice 2b). " +
                "stabilatorBandwidth, stabilatorRateDegS, stabilatorMinDeg, stabilatorMaxDeg, aileronBandwidth, aileronRateDegS, " +
                "aileronMinDeg, aileronMaxDeg, rudderBandwidth, rudderRateDegS, rudderMinDeg, rudderMaxDeg: PUBLIC, the actuators of " +
                "NASA TM-107601 as Chakraborty, Seiler and Balas 2011 Table 2 gives them (30, 48, 40 rad/s lags; 40, 100, 61 deg/s; " +
                "-24..10.5, -25..45, -30..30 deg).\n" +
                "Landing Gear L1 (slice 2d, W4): wheels (nose 0,-1.71,5.17; mains +-1.40,-1.71,-0.74 m about the centre of mass), " +
                "travelM 0.3, springNPerM 188e3 nose and 661e3 mains, damperNsPerM 25e3 and 88e3 (about half critical), hardPointsM " +
                "(belly, tail, nose, wingtips): AUTHORED, measured off the F-18E model with its centre of mass 1.6 m aft of the model's origin; " +
                "the springs sized so the static weight compresses each strut about half its travel. gearMaxSinkRateFtS 24: AUTHORED, the " +
                "carrier-landing design sink rate commonly quoted for Navy jets (source not pinned), used here for any landing. " +
                "tyreRollingFriction 0.02, tyreBrakingFriction 0.5, tyreLateralFriction 0.7, tyreSlipSpeedMps 0.5: AUTHORED, textbook " +
                "dry-concrete tyre values. nosewheelSteerMaxDeg 20, nosewheelSteerFadeStartKt 40, nosewheelSteerFadeEndKt 80, " +
                "gearTransitTimeS 4, gearDownDeltaCd 0.02: AUTHORED. strutEndStopFraction 0.7, strutEndStopStiffening 30: AUTHORED " +
                "(SIX, 2026-10-26), an oleo's stiffening air spring near full stroke, sized so a hands-off touchdown's nose slap does not bottom the nose strut.\n" +
                "Note: the published f18full.m adds the thrust term of beta-dot with the wrong sign (T cos(alpha) sin(beta)/(mV)); " +
                "Air Wing derives its motion from body kinematics and does not inherit it (Calibration~/l1_harv_mirror.py).";
            string prov = so.provenance ?? "";
            int at = prov.IndexOf(Marker, System.StringComparison.Ordinal);
            so.provenance = (at >= 0 ? prov.Substring(0, at).TrimEnd() : prov.TrimEnd()) + "\n\n" + paragraph;

            EditorUtility.SetDirty(so);
            var visuals = AssetDatabase.LoadAssetAtPath<AircraftVisualsSO>(VisualsPath);
            if (visuals != null)
            {
                Undo.RecordObject(visuals, "Apply the model offset");
                visuals.modelOffsetM = ModelOffsetM;
                visuals.gearParts = GearRig();
                EditorUtility.SetDirty(visuals);
            }
            else Debug.LogWarning($"[Air Wing] {VisualsPath} not found; the model offset was not applied.");
            AssetDatabase.SaveAssets();
            so.Bake();   // throws if anything is out of shape
            Debug.Log($"[Air Wing] Applied the HARV L1 data to {ProfilePath}: Ixx {so.ixxSlugFt2:F0}, Iyy {so.iyySlugFt2:F0}, Izz {so.izzSlugFt2:F0}, Ixz {so.ixzSlugFt2:F0} slug·ft² at {weightLb:F0} lb.");
        }
    }
}
