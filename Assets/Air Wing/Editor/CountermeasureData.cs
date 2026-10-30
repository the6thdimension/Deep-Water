using DeepWater.AirWing.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Deep Water → Air Wing → Apply Chaff and Flare Data to F-A-18E (W69, SENSORS-CONCEPT J4). Writes the jet's chaff and
    /// flares with their provenance, as <see cref="RadarMissileData"/> does for the radar missile. Every number is AUTHORED
    /// for a game: not any real dispenser, no real data (Air Wing BLUEPRINT constraint 8).
    /// </summary>
    public static class CountermeasureData
    {
        const string ProfilePath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string Marker = "--- Chaff and flares (W69) ---";

        [MenuItem("Deep Water/Air Wing/Apply Chaff and Flare Data to F-A-18E")]
        public static void Apply()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            if (so == null) throw new System.InvalidOperationException($"no profile at {ProfilePath}");
            Undo.RecordObject(so, "Apply chaff and flare data");

            so.cmSet = true;
            so.cmChaff = 30;                  // AUTHORED: SIX's open question 2 at the leaning, 30 each, refilled on respawn
            so.cmFlares = 30;
            so.cmChaffBreakChance = 0.25f;    // AUTHORED: one drop in four breaks a radar missile; a few drops usually save you
            so.cmFlareBreakChance = 0.25f;    // AUTHORED: the same for a heat-seeker
            so.cmIntervalS = 1f;              // AUTHORED: a held button drops one a second, so 30 last half a minute

            string paragraph =
                Marker + "\n" +
                "Chaff and flares (SENSORS-CONCEPT J4, SIX 2026-10-29), a game's numbers, not any real dispenser; all AUTHORED: " +
                "cmChaff 30 and cmFlares 30, refilled on respawn (the open question's leaning); cmChaffBreakChance 0.25 (each chaff drop " +
                "breaks each radar missile coming at the jet one time in four) and cmFlareBreakChance 0.25 (the same for heat-seekers); " +
                "cmIntervalS 1 (a held button drops one a second).";
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
            Debug.Log("[Air Wing] chaff and flare data applied to " + ProfilePath);
        }
    }
}
