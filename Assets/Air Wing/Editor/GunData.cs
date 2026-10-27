using DeepWater.AirWing.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Deep Water → Air Wing → Apply Gun Data to F-A-18E (Gunnery K4, K5). Writes the M61A2's numbers and the jet's
    /// abstract hit points into the F/A-18E profile, with each number's source in the provenance, as HarvL1Data does
    /// for the aero. Run again after editing this file; it overwrites only the gun and combat fields and their paragraph.
    /// </summary>
    public static class GunData
    {
        const string ProfilePath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string Marker = "--- Gun and combat (Gunnery K4, K5) ---";

        [MenuItem("Deep Water/Air Wing/Apply Gun Data to F-A-18E")]
        public static void Apply()
        {
            var so = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath);
            if (so == null) throw new System.InvalidOperationException($"no profile at {ProfilePath}");
            Undo.RecordObject(so, "Apply gun data");

            so.gunName = "M61A2 20 mm";
            so.gunRoundsPerMinute = 6000f;   // PUBLIC: General Dynamics OTS F/A-18E/F gun system sheet, 4,000 or 6,000 spm (high rate)
            so.gunRoundsCarried = 412;       // PUBLIC: the same sheet
            so.gunMuzzleVelocityFtS = 3450f; // SECONDARY: 3,450 ft/s for PGU-28/B, a simulator reference; not pinned for the E
            so.gunRoundMassKg = 0.1f;        // AUTHORED: a 20 × 102 mm projectile of about 100 g
            so.gunRoundDiameterM = 0.02f;
            so.gunRoundFormFactor = 1.0f;    // AUTHORED: G7 as it is; not calibrated to a published range
            so.gunDispersionMils = 4f;       // AUTHORED: game value
            so.gunMaxTimeOfFlightS = 3f;     // AUTHORED: past ~2,500 m nothing is aimed
            so.gunMuzzleM = new Vector3(0f, 0.5f, 7.5f);        // AUTHORED: the nose, ahead of the canopy, off the model
            so.gunBoreBody = new Vector3(0f, 0.0175f, 1f).normalized;   // AUTHORED: 1° above the body axis
            so.hitPoints = 10f;              // AUTHORED: ten 20 mm hits shoot it down (abstract, K5)
            so.hitRadiusM = 4f;              // AUTHORED: about the jet's presented size

            string paragraph =
                Marker + "\n" +
                "gunName M61A2 20 mm; gunRoundsPerMinute 6000 and gunRoundsCarried 412: PUBLIC, General Dynamics OTS, F/A-18 E/F " +
                "20 mm gun system sheet (4,000 or 6,000 shots a minute; the high rate used). gunMuzzleVelocityFtS 3450: SECONDARY, " +
                "a simulator reference's figure for PGU-28/B (not pinned for the E/F's gun). gunRoundMassKg 0.1, gunRoundDiameterM " +
                "0.02, gunRoundFormFactor 1.0 (on G7), gunDispersionMils 4, gunMaxTimeOfFlightS 3: AUTHORED. gunMuzzleM (0, 0.5, " +
                "7.5) and gunBoreBody 1 deg up: AUTHORED off the model. hitPoints 10 and hitRadiusM 4: AUTHORED, abstract (Gunnery " +
                "K5): a hit is one round, ten shoot the jet down.";
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
            Debug.Log("[Air Wing] gun data applied to " + ProfilePath);
        }
    }
}
