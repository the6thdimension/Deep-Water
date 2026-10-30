using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Deep Water → Air Wing → Apply Radar Missile Data to F-A-18E (W68, SENSORS-CONCEPT J7, J15, J18). Writes the generic
    /// medium-range active radar game missile onto the inboard pair of pylons, with the provenance, as
    /// <see cref="MissileData"/> does for the heat-seeker on the outboard pair. Every number is AUTHORED for a game sized to
    /// the 40 km arena (J15: about 25 km): not any real missile, no real data (Air Wing BLUEPRINT constraint 8).
    /// </summary>
    public static class RadarMissileData
    {
        const string ProfilePath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string Marker = "--- Radar missiles (W68) ---";

        [MenuItem("Deep Water/Air Wing/Apply Radar Missile Data to F-A-18E")]
        public static void Apply()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            if (so == null) throw new System.InvalidOperationException($"no profile at {ProfilePath}");
            Undo.RecordObject(so, "Apply radar missile data");

            so.radarMissilePylonsM = new[] { new Vector3(-2.6f, -0.55f, -1.2f), new Vector3(2.6f, -0.55f, -1.2f) };   // inboard, off the model
            so.radarMissile = new MissileProfile
            {
                Name = "MR-AR game missile",
                Seeker = MissileSeeker.ActiveRadar,
                ActiveRangeM = 8000f,       // AUTHORED: its own radar takes over 8 km out; the shooter may turn away after that
                BoostAccelMps2 = 150f,      // AUTHORED: 6 s of motor adds 900 m/s
                BurnS = 6f,
                DragPerM = 2e-5f,           // AUTHORED: k·v², a longer coast than the heat-seeker's 3e-5
                MaxG = 30f,                 // AUTHORED
                NavGain = 4f,               // AUTHORED: proportional navigation, textbook 3–5
                SeekerConeDeg = 45f,        // AUTHORED: once active, a target this far off its path is lost
                LockConeDeg = 45f,          // AUTHORED: the launch cone about the nose
                LockMinM = 1500f,           // AUTHORED: 1.5 km to 25 km (J15)
                LockMaxM = 25000f,
                LockTimeS = 0f,             // the radar's single-target track is the lock
                ArmS = 1f,                  // AUTHORED
                LifeS = 50f,                // AUTHORED
                LethalRadiusM = 15f,        // the heat-seeker's burst (W65 Q3)
                KillChance = 0.7f,
                DamageHp = 6f,
                LaunchIntervalS = 1f,       // AUTHORED: one a second while the radar pickle is held
            };

            string paragraph =
                Marker + "\n" +
                "radarMissile 'MR-AR game missile': a generic medium-range active radar missile archetype for a video game (SIX 2026-10-29, " +
                "SENSORS-CONCEPT J7, J18: two of the four missiles radar-guided); not any real missile, no real data. All AUTHORED: " +
                "radarMissilePylonsM the inboard pylon under each wing at (±2.6, −0.55, −1.2) m, off the model; Seeker ActiveRadar, ActiveRangeM 8000 " +
                "(it flies on the shooter's single-target track until 8 km from it, then its own radar takes over); BoostAccelMps2 150 for BurnS 6; " +
                "DragPerM 2e-5; MaxG 30; NavGain 4; SeekerConeDeg 45; launch cone LockConeDeg 45, LockMinM 1500 to LockMaxM 25000 (J15); " +
                "ArmS 1; LifeS 50; the burst as the heat-seeker's: LethalRadiusM 15, KillChance 0.7, DamageHp 6; LaunchIntervalS 1.";
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
            Debug.Log("[Air Wing] radar missile data applied to " + ProfilePath);
        }
    }
}
