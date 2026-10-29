using DeepWater.AirWing.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Deep Water → Air Wing → Apply Missile Data to F-A-18E (W65). Writes the game missile's arcade numbers and the jet's
    /// four pylons into the F/A-18E profile, with the provenance, as GunData does for the gun. Every number is AUTHORED for
    /// a video game (SIX, 2026-10-29: "make game missile sufficient for video game air to air"): a generic short-range
    /// heat-seeker archetype, not any real missile.
    /// </summary>
    public static class MissileData
    {
        const string ProfilePath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string Marker = "--- Game missiles (W65) ---";

        [MenuItem("Deep Water/Air Wing/Apply Missile Data to F-A-18E")]
        public static void Apply()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            if (so == null) throw new System.InvalidOperationException($"no profile at {ProfilePath}");
            Undo.RecordObject(so, "Apply missile data");

            so.missileName = "SR-IR game missile";
            // Two under each wing, outboard first, alternating sides (AUTHORED off the model; checked in a capture).
            so.missilePylonsM = new[]
            {
                new Vector3(-4.2f, -0.45f, -1.8f), new Vector3(4.2f, -0.45f, -1.8f),
                new Vector3(-2.6f, -0.55f, -1.2f), new Vector3(2.6f, -0.55f, -1.2f),
            };
            so.missileBoostAccelMps2 = 100f;  // AUTHORED: 3 s of motor adds 300 m/s: about 2.5 times a 400 kt jet
            so.missileBurnS = 3f;
            so.missileDragPerM = 3e-5f;       // AUTHORED: coasting from 500 m/s, about 400 m/s after 17 s (1e-4 lost tail chases from 3 km, 2026-10-29)
            so.missileMaxG = 30f;             // AUTHORED: out-turns the jet's 7.5 G, but not without limit
            so.missileNavGain = 4f;           // AUTHORED: inside proportional navigation's textbook 3-5
            so.missileSeekerConeDeg = 45f;    // AUTHORED: a target that gets this far off the missile's path is lost
            so.missileLockConeDeg = 30f;      // AUTHORED (SIX's pick, Q2): 30° of the nose
            so.missileLockMinM = 500f;        // AUTHORED (Q2): 500 m to 5 km
            so.missileLockMaxM = 5000f;
            so.missileLockTimeS = 1f;         // AUTHORED (Q2): held for 1 s
            so.missileArmS = 0.5f;            // AUTHORED: no burst in the first half second
            so.missileLifeS = 20f;            // AUTHORED (Q2): self-destructs at 20 s
            so.missileLethalRadiusM = 15f;    // AUTHORED (Q3): a 15 m proximity burst
            so.missileKillChance = 0.7f;      // AUTHORED (Q3): 70 % a kill
            so.missileDamageHp = 6f;          // AUTHORED (Q3): otherwise 6 of the jet's 10 hit points
            so.missileLaunchIntervalS = 1f;   // AUTHORED: one launch a second while the pickle is held

            string paragraph =
                Marker + "\n" +
                "missileName 'SR-IR game missile': a generic short-range heat-seeker archetype for a video game (SIX 2026-10-29, Air Wing W65); " +
                "not any real missile, no real data. All AUTHORED: four pylons, two under each wing at (±4.2, −0.45, −1.8) and " +
                "(±2.6, −0.55, −1.2) m, off the model; missileBoostAccelMps2 100 for missileBurnS 3 (about 2.5 times a 400 kt jet), " +
                "missileDragPerM 3e-5 (k·v²; a tail chase from 3 km closes inside the life, one from 5 km on a jet running away does not), missileMaxG 30, missileNavGain 4 (proportional navigation, textbook 3–5), missileSeekerConeDeg 45; " +
                "the lock (SIX's picks): missileLockConeDeg 30, missileLockMinM 500, missileLockMaxM 5000, missileLockTimeS 1; missileArmS 0.5, " +
                "missileLifeS 20; the hit (SIX's picks): missileLethalRadiusM 15, missileKillChance 0.7, missileDamageHp 6; missileLaunchIntervalS 1.";
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
            Debug.Log("[Air Wing] missile data applied to " + ProfilePath);
        }
    }
}
