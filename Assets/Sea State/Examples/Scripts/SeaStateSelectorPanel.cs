using UnityEngine;
using DeepWater.Sea.Core;
using DeepWater.Sea.Modules.Buoyancy;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.Sea.Examples
{
    /// <summary>
    /// In-play sea-state selector: pick the Douglas sea (keys 0–9) and swell (Shift + 0–9),
    /// turn the wind ( [ / ] ) and read back what the sea is doing — every number shown with
    /// the formula that produced it.
    /// </summary>
    public sealed class SeaStateSelectorPanel : MonoBehaviour
    {
        [SerializeField] SeaStateDirector director;
        [Tooltip("Hulls whose submerged fraction is listed (optional).")]
        [SerializeField] BuoyantBody[] hulls = new BuoyantBody[0];
        [SerializeField] bool visible = true;

        const float WindStepDeg = 15f;
        GUIStyle _label, _title;

        void Update()
        {
            if (director == null || !director.IsReady) return;
            if (Input.GetKeyDown(KeyCode.Tab)) visible = !visible;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            for (int d = 0; d <= 9; d++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + d) && !Input.GetKeyDown(KeyCode.Keypad0 + d)) continue;
                if (shift) director.SetSwell(d); else director.SetSeaState(d);
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket)) director.SetWindFrom(director.WindFromDeg - WindStepDeg);
            if (Input.GetKeyDown(KeyCode.RightBracket)) director.SetWindFrom(director.WindFromDeg + WindStepDeg);
        }

        void OnGUI()
        {
            if (!visible || director == null || !director.IsReady) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };

            SeaStateLibrarySO lib = director.Library;
            SeaConditions c = director.Conditions;
            SeaBakeReport bake = director.Simulation.LastBake;
            SeaStateProfileSO sea = lib.GetSea(c.DouglasSea);
            SwellProfileSO swell = lib.GetSwell(c.DouglasSwell);

            GUILayout.BeginArea(new Rect(12, 12, 470, Screen.height - 24), GUI.skin.box);
            GUILayout.Label("Sea State — Douglas", _title);

            GUILayout.Label("Wind sea (keys 0–9)", _label);
            c.DouglasSea = DegreeRow(c.DouglasSea, out bool seaClicked);
            if (seaClicked) director.SetSeaState(c.DouglasSea);

            GUILayout.Label("Swell (Shift + 0–9)", _label);
            c.DouglasSwell = DegreeRow(c.DouglasSwell, out bool swellClicked);
            if (swellClicked) director.SetSwell(c.DouglasSwell);

            GUILayout.Label($"Wind from {director.WindFromDeg:0}°   ( [ / ] )", _label);
            float wind = GUILayout.HorizontalSlider(director.WindFromDeg, 0f, 360f);
            if (!Mathf.Approximately(wind, director.WindFromDeg)) director.SetWindFrom(Mathf.Round(wind / 5f) * 5f);
            GUILayout.Label($"Swell from {director.SwellFromDeg:0}°", _label);
            float swellDir = GUILayout.HorizontalSlider(director.SwellFromDeg, 0f, 360f);
            if (!Mathf.Approximately(swellDir, director.SwellFromDeg)) director.SetSwellFrom(Mathf.Round(swellDir / 5f) * 5f);

            GUILayout.Space(6);
            float tp = c.WindSeaTpS;
            float lambda = (float)WaveSpectrum.DeepWaterWavelength(tp, director.Gravity);
            float knots = c.WindSpeedMs / SeaUnits.MetersPerSecondPerKnot;

            Line($"<b>Douglas {c.DouglasSea} — {(sea != null ? sea.douglasTerm : "?")}</b>");
            Line($"Hs (wind sea) {c.WindSeaHsM:0.00} m   (WMO 3700 band)");
            Line($"Tp {tp:0.0} s   (PM: 2πU/0.877g, U=√(Hs g/0.21))   λp = gTp²/2π = {lambda:0} m");
            Line($"Wind {knots:0.#} kt = {c.WindSpeedMs:0.0} m/s   (NATO STANAG 4194 mean)");
            Line($"Whitecaps W = 3.84e-6·U^3.41 = {c.WhitecapFraction:P1}");
            Line($"<b>Swell {c.DouglasSwell} — {(swell != null ? swell.douglasTerm : "?")}</b>" +
                 (c.SwellHsM > 0f ? $"   Hs {c.SwellHsM:0.0} m, Tp {c.SwellTpS:0.0} s" : ""));
            Line($"Combined Hs √(Hw²+Hs²) = {c.SignificantWaveHeightM:0.00} m");
            Line($"Resolved Hs 4√(Σa²/2) = {c.ResolvedWaveHeightM:0.00} m   ({bake.ResolvedVarianceFraction:P0} of variance; " +
                 $"shorter than {lib.minResolvedWavelengthM:0} m → HDRP ripples only)");
            Line($"Components {director.Simulation.ActiveComponentCount} live ({bake.WindSeaCount} wind + {bake.SwellCount} swell)   " +
                 $"fade {director.Simulation.TransitionProgress:P0}   folded {bake.FoldedFraction:P2}");
            Line($"Sim t = {director.SimulationTimeS:0.00} s   step {director.Simulation.StepCount}");

            if (hulls != null && hulls.Length > 0)
            {
                GUILayout.Space(4);
                foreach (var h in hulls)
                    if (h != null) Line($"{h.name}: {h.SubmergedFraction:P0} of probe volume submerged");
            }

            GUILayout.Space(4);
            Line("<i>Tab hides this panel. Right-drag orbit, wheel zoom, WASD/QE move, F1–F4 views.</i>");
            GUILayout.EndArea();
        }

        int DegreeRow(int current, out bool clicked)
        {
            clicked = false;
            GUILayout.BeginHorizontal();
            for (int d = 0; d <= 9; d++)
            {
                bool on = GUILayout.Toggle(d == current, d.ToString(), GUI.skin.button, GUILayout.Width(40));
                if (on && d != current) { current = d; clicked = true; }
            }
            GUILayout.EndHorizontal();
            return current;
        }

        void Line(string text) => GUILayout.Label(text, _label);
    }
}
