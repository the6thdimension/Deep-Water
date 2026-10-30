using DeepWater.AirWing.Core.Radar;
using DeepWater.AirWing.ScriptableObjects;
using RHRadarSuite.Detection;
using UnityEditor;
using UnityEngine;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Deep Water → Air Wing → Apply Radar Data to F-A-18E (SENSORS-CONCEPT J1, J2, J6, J15, J16). Writes a generic game
    /// radar and the jet's radar signature into the F/A-18E profile, with the provenance, as MissileData does for the
    /// missile. Every number is AUTHORED for a game sized to the 40 km arena (J15): not any real radar, no real data
    /// (Air Wing BLUEPRINT constraint 8). The peak power is not chosen but solved: the power at which a 5 dBsm target at
    /// 40 km is seen on half the search looks.
    /// </summary>
    public static class RadarData
    {
        const string ProfilePath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string Marker = "--- Radar and signature (SENSORS-CONCEPT J1) ---";

        // The calibration point: what the search radar sees half the time at the arena's edge.
        const double CalibrationRcsDbsm = 5.0, CalibrationRangeM = 40000.0, CalibrationPd = 0.5;

        [MenuItem("Deep Water/Air Wing/Apply Radar Data to F-A-18E")]
        public static void Apply()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            if (so == null) throw new System.InvalidOperationException($"no profile at {ProfilePath}");
            Undo.RecordObject(so, "Apply radar data");

            so.radarName = "Generic fighter radar (game)";
            so.radarGainDbi = 33.0;               // AUTHORED: a nose-sized antenna
            so.radarFrequencyHz = 10e9;           // AUTHORED: X band, the textbook band for a fighter's nose radar
            so.radarBandwidthHz = 1e6;            // AUTHORED: 150 m range resolution
            so.radarNoiseFigureDb = 4.0;          // AUTHORED
            so.radarLossesDb = 8.0;               // AUTHORED
            so.radarPulsesIntegrated = 32;        // AUTHORED: per search look
            so.radarClutterImprovementDb = 30.0;  // AUTHORED: a low jet over a Beaufort 4 sea at 20 km drops from 93 % to about 30 % a look
            so.radarAzimuthBeamwidthDeg = 3.3;    // AUTHORED
            so.radarElevationBeamwidthDeg = 3.3;
            so.radarMonopulseSlope = 1.6;         // the textbook sum-difference value (Barton)
            so.radarRangeFloorM = 15.0;           // AUTHORED
            so.radarAngleFloorDeg = 0.1;          // AUTHORED
            so.radarFalseAlarmProbability = 1e-6; // AUTHORED: sets the threshold
            // Search volumes, body frame (J6): wide and long; narrower and faster with full tracks; narrow, tall and short.
            so.radarRangeWhileSearch = new RadarModeProfile { AzimuthHalfDeg = 60f, ElevationMinDeg = -30f, ElevationMaxDeg = 30f, RangeM = 40000f, FrameS = 4f };
            so.radarTrackWhileScan = new RadarModeProfile { AzimuthHalfDeg = 30f, ElevationMinDeg = -20f, ElevationMaxDeg = 20f, RangeM = 40000f, FrameS = 2f };
            so.radarDogfight = new RadarModeProfile { AzimuthHalfDeg = 10f, ElevationMinDeg = -10f, ElevationMaxDeg = 50f, RangeM = 10000f, FrameS = 0.5f };
            // Single-target track (J2).
            so.radarGimbalHalfDeg = 60f;
            so.radarSttRangeM = 40000f;
            so.radarSttLookS = 0.1f;              // ten looks a second
            so.radarSttGainDb = 6f;               // AUTHORED: the dwell on one target
            so.radarSttDropMisses = 10;           // a second of misses loses the lock
            // The track file.
            so.radarTrackSigmaM = 150f;           // AUTHORED: about the angle error at 40 km
            so.radarTrackMaxSpeedMps = 700f;
            so.radarTrackMaxAccelMps2 = 90f;      // a 9 G turn
            so.radarConfirmHits = 2;
            so.radarConfirmLooks = 3;
            so.radarDropAfterMisses = 3;
            so.radarStartMode = RadarMode.RangeWhileSearch;
            // The signature: a generic fighter, smallest nose-on, largest from the side.
            so.rcsSet = true;
            so.rcsNoseDbsm = 3.0;
            so.rcsBeamDbsm = 12.0;
            so.rcsTailDbsm = 7.0;

            // Solve the power: SNR is linear in power, so one step from a 1 W guess.
            so.radarPeakPowerW = 1.0;
            RadarTerms terms = so.Bake().Radar.Terms;
            double required = RadarEquation.RequiredSnrSwerling1Db(CalibrationPd, so.radarFalseAlarmProbability);
            so.radarPeakPowerW = System.Math.Round(RadarEquation.PeakPowerForSnrW(terms, CalibrationRcsDbsm, CalibrationRangeM, required));

            string paragraph =
                Marker + "\n" +
                "radarName 'Generic fighter radar (game)': a generic nose radar for a video game (SIX 2026-10-29, SENSORS-CONCEPT J1); not any real radar, " +
                "no real data. All AUTHORED: radarGainDbi 33, radarFrequencyHz 10e9 (X band), radarBandwidthHz 1e6, radarNoiseFigureDb 4, radarLossesDb 8, " +
                "radarPulsesIntegrated 32, radarClutterImprovementDb 30, beamwidths 3.3°, monopulse slope 1.6, floors 15 m and 0.1°, false alarms 1e-6. " +
                $"radarPeakPowerW {so.radarPeakPowerW:0} is SOLVED: a 5 dBsm target at 40 km is seen on half the search looks (Swerling 1). " +
                "Modes (J6, body frame): range-while-search ±60° by −30…+30°, 40 km, 4 s frame; track-while-scan ±30° by −20…+20°, 40 km, 2 s; " +
                "dogfight ±10° by −10…+50°, 10 km, 0.5 s. Single-target track (J2), all AUTHORED: radarGimbalHalfDeg 60, radarSttRangeM 40000, " +
                "radarSttLookS 0.1 (ten looks a second), radarSttGainDb 6, radarSttDropMisses 10 (a second of misses loses the lock). " +
                "Track file, all AUTHORED: radarTrackSigmaM 150, radarTrackMaxSpeedMps 700, radarTrackMaxAccelMps2 90 (a 9 G turn), " +
                "radarConfirmHits 2 of radarConfirmLooks 3 to confirm, radarDropAfterMisses 3. Starts in range-while-search. " +
                "Signature: rcsNoseDbsm 3, rcsBeamDbsm 12, rcsTailDbsm 7 (a generic fighter).";
            string prov = so.provenance ?? "";
            int at = prov.IndexOf(Marker, System.StringComparison.Ordinal);
            if (at >= 0)
            {
                int end = prov.IndexOf("\n--- ", at + Marker.Length, System.StringComparison.Ordinal);
                prov = prov.Substring(0, at).TrimEnd() + (end >= 0 ? "\n\n" + prov.Substring(end + 1) : "");
            }
            so.provenance = prov.TrimEnd() + "\n\n" + paragraph;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            so.Bake();   // throws if anything is out of range
            Debug.Log($"[Air Wing] radar data applied to {ProfilePath}: peak power {so.radarPeakPowerW:0} W");
        }
    }
}
