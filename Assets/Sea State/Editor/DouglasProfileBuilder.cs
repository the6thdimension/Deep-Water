using System.IO;
using UnityEditor;
using UnityEngine;
using DeepWater.Sea.Core;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.Sea.Editor
{
    /// <summary>
    /// Writes the Douglas sea-state and swell profiles, and the library that orders them, from
    /// the cited tables below (Pat4: reference data as auditable code, not hand-typed YAML).
    /// Re-running resets every profile to these authored defaults; asset GUIDs are kept, so
    /// scene references survive.
    ///
    /// **Sources**
    /// [WMO]  WMO code table 3700 / Douglas wind-sea scale — Hs bands and terms; band means as
    ///        tabulated in MIT OCW 13.42 (2005) Lecture 6, "Wave spectra".
    /// [NATO] NATO Sea State Numeral Table, open ocean North Atlantic (STANAG 4194 / ANEP-11,
    ///        Bales, Lee &amp; Voelker 1981) as reproduced in USNA EN400 Ch. 8, Table 8.1 —
    ///        sustained wind speed means (knots).
    /// [PM]   Pierson &amp; Moskowitz (1964): Tp ≈ 4.99 √Hs for a fully developed wind sea.
    /// [DNV]  DNV-RP-C205 §3.5.5.5 — JONSWAP γ from Tp/√Hs (gives γ ≈ 1 with the PM period).
    /// [MO80] Monahan &amp; O'Muircheartaigh (1980), J. Phys. Oceanogr. 10:2094 —
    ///        whitecap fraction W = 3.84e-6 U^3.41.
    /// [NOAA] NOAA VOS "Guide to Sea State, Wind and Clouds" — Beaufort 0 (&lt; 1 kt), 12 (≥ 64 kt).
    /// [GODA] Goda (2000), Random Seas and Design of Maritime Structures — cos-2s s_max ≈ 10
    ///        wind waves, 25 short-decay swell, 75 long-decay swell (from memory, not re-fetched).
    /// [DSS]  Douglas swell scale: length short &lt; 100 m, average 100–200 m, long &gt; 200 m;
    ///        height low &lt; 2 m, moderate 2–4 m, high &gt; 4 m.
    /// </summary>
    public static class DouglasProfileBuilder
    {
        public const string ProfileFolder = "Assets/Sea State/ScriptableObjects/Profiles";
        public const string LibraryPath = ProfileFolder + "/SeaStateLibrary.asset";

        const float Gravity = 9.81f;          // matches Unity's default Physics.gravity
        const int WindSeaComponents = 48;
        const int SwellComponents = 16;
        const float WindSeaSpreading = 10f;   // [GODA] wind waves

        // Douglas wind sea: degree, term, Hs design (m), NATO wind mean (kt), authored flags.
        static readonly (int degree, string term, float hsM, float windKt, string hsNote, string windNote)[] SeaTable =
        {
            (0, "Calm (glassy)",  0f,     0f,    "WMO: no waves",                         "AUTHORED from [NOAA] Beaufort 0 (< 1 kt)"),
            (1, "Calm (rippled)", 0.05f,  3f,    "[WMO] 0–0.1 m band mean",               "[NATO] sea state 0–1 mean (0–6 kt)"),
            (2, "Smooth",         0.3f,   8.5f,  "[WMO] 0.1–0.5 m band mean",             "[NATO] 7–10 kt mean"),
            (3, "Slight",         0.875f, 13.5f, "[WMO] 0.5–1.25 m band mean",            "[NATO] 11–16 kt mean"),
            (4, "Moderate",       1.875f, 19f,   "[WMO] 1.25–2.5 m band mean",            "[NATO] 17–21 kt mean"),
            (5, "Rough",          3.25f,  24.5f, "[WMO] 2.5–4 m band mean",               "[NATO] 22–27 kt mean"),
            (6, "Very rough",     5f,     37.5f, "[WMO] 4–6 m band mean",                 "[NATO] 28–47 kt mean"),
            (7, "High",           7.5f,   51.5f, "[WMO] 6–9 m band mean",                 "[NATO] 48–55 kt mean"),
            (8, "Very high",      11.5f,  59.5f, "[WMO] 9–14 m band mean",                "[NATO] 56–63 kt mean"),
            (9, "Phenomenal",     16f,    70f,   "AUTHORED: [WMO] gives only > 14 m",     "AUTHORED: [NATO] gives only > 63 kt; [NOAA] Beaufort 12 ≥ 64 kt"),
        };

        // Douglas swell: degree, term, length class, height class, representative Hs (m) and
        // wavelength (m), spreading s. Representative values inside each class are AUTHORED.
        static readonly (int degree, string term, string length, string height, float hsM, float lengthM, float s)[] SwellTable =
        {
            (0, "No swell",       "—",       "—",        0f, 0f,   75f),
            (1, "Very low",       "short or average", "low", 1f, 100f, 25f),
            (2, "Low",            "long",    "low",      1f, 300f, 75f),
            (3, "Light",          "short",   "moderate", 3f, 80f,  25f),
            (4, "Moderate",       "average", "moderate", 3f, 150f, 50f),
            (5, "Moderate rough", "long",    "moderate", 3f, 300f, 75f),
            (6, "Rough",          "short",   "high",     5f, 80f,  25f),
            (7, "High",           "average", "high",     5f, 150f, 50f),
            (8, "Very high",      "long",    "high",     5f, 300f, 75f),
            (9, "Confused",       "indefinable", "indefinable", 3f, 150f, 2f),
        };

        [MenuItem("Deep Water/Sea State/Build Douglas Profiles")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(ProfileFolder);

            var library = LoadOrCreate<SeaStateLibrarySO>(LibraryPath);
            if (library.seaStates == null || library.seaStates.Length != 10) library.seaStates = new SeaStateProfileSO[10];
            if (library.swells == null || library.swells.Length != 10) library.swells = new SwellProfileSO[10];

            foreach (var row in SeaTable)
            {
                var so = LoadOrCreate<SeaStateProfileSO>($"{ProfileFolder}/Douglas_{row.degree}_{Slug(row.term)}.asset");
                FillSea(so, row);
                EditorUtility.SetDirty(so);
                library.seaStates[row.degree] = so;
            }

            foreach (var row in SwellTable)
            {
                var so = LoadOrCreate<SwellProfileSO>($"{ProfileFolder}/Swell_{row.degree}_{Slug(row.term)}.asset");
                FillSwell(so, row);
                EditorUtility.SetDirty(so);
                library.swells[row.degree] = so;
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DouglasProfileBuilder] Wrote 10 sea-state + 10 swell profiles and the library to {ProfileFolder}.");
        }

        static void FillSea(SeaStateProfileSO so,
            (int degree, string term, float hsM, float windKt, string hsNote, string windNote) row)
        {
            float tp = (float)WaveSpectrum.PiersonMoskowitzPeakPeriod(row.hsM, Gravity);
            float gamma = (float)WaveSpectrum.DnvGamma(row.hsM, tp);
            float windMs = row.windKt * SeaUnits.MetersPerSecondPerKnot;
            float whitecaps = SeaUnits.MonahanWhitecapFraction(windMs);

            so.douglasDegree = row.degree;
            so.douglasTerm = row.term;
            so.windSpeedKnots = row.windKt;
            so.whitecapFraction = whitecaps;
            so.windSea = new WaveSystemAuthoring
            {
                significantHeightM = row.hsM,
                peakPeriodS = tp,
                jonswapGamma = gamma,
                spreadingS = WindSeaSpreading,
                choppiness = 1f,
                componentCount = row.hsM > 0f ? WindSeaComponents : 0,
                seed = (uint)(1000 + row.degree),
            };

            bool extrapolated = windMs > 20f;
            so.provenance =
                $"Douglas wind sea {row.degree} — {row.term}\n" +
                $"Hs {row.hsM:0.###} m: {row.hsNote}\n" +
                $"Tp {tp:0.0} s: [PM] fully developed, Tp = 2π U/(0.877 g) with U = √(Hs g / 0.21)\n" +
                $"γ {gamma:0.00}: [DNV] rule at Tp/√Hs = {(row.hsM > 0f ? tp / Mathf.Sqrt(row.hsM) : 0f):0.00}\n" +
                $"Wind {row.windKt:0.#} kt: {row.windNote} (reference height to confirm in ANEP-11)\n" +
                $"Whitecaps {whitecaps:P1}: [MO80] W = 3.84e-6 U^3.41 at U = {windMs:0.0} m/s" +
                (extrapolated ? " — EXTRAPOLATED far beyond typical observations\n" : "\n") +
                $"Spreading s {WindSeaSpreading:0}: [GODA] wind waves. Choppiness 1: first-order Lagrangian model.\n" +
                $"Components {so.windSea.componentCount}, seed {so.windSea.seed}: AUTHORED.";
        }

        static void FillSwell(SwellProfileSO so,
            (int degree, string term, string length, string height, float hsM, float lengthM, float s) row)
        {
            float tp = row.lengthM > 0f ? Mathf.Sqrt(2f * Mathf.PI * row.lengthM / Gravity) : 0f;

            so.douglasDegree = row.degree;
            so.douglasTerm = row.term;
            so.lengthClass = row.length;
            so.heightClass = row.height;
            so.swell = new WaveSystemAuthoring
            {
                significantHeightM = row.hsM,
                peakPeriodS = tp,
                jonswapGamma = 3.3f,
                spreadingS = row.s,
                choppiness = 1f,
                componentCount = row.hsM > 0f ? SwellComponents : 0,
                seed = (uint)(2000 + row.degree),
            };

            so.provenance =
                $"Douglas swell {row.degree} — {row.term} ({row.length} length, {row.height} height) [DSS]\n" +
                (row.hsM > 0f
                    ? $"Hs {row.hsM:0.#} m and wavelength {row.lengthM:0} m: AUTHORED representative values inside the class\n" +
                      $"Tp {tp:0.0} s: deep-water dispersion T = √(2π L / g)\n" +
                      $"γ 3.3: AUTHORED (standard JONSWAP value; swell is narrow-banded)\n" +
                      $"Spreading s {row.s:0}: [GODA]{(row.degree == 9 ? " — AUTHORED broad spread for a confused swell" : "")}\n" +
                      $"Components {SwellComponents}, seed {so.swell.seed}: AUTHORED."
                    : "No swell.");
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static string Slug(string term)
        {
            var chars = term.ToCharArray();
            var sb = new System.Text.StringBuilder(chars.Length);
            foreach (char c in chars)
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }
    }
}
