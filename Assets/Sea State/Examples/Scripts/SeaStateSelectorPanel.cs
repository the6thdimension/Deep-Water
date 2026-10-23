using System.Text;
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
    ///
    /// Allocation-light by design: the readout is rebuilt into one cached string a few times
    /// a second (and immediately on a sea-state change), and drawn with fixed-rect GUI calls
    /// rather than GUILayout, so the panel does not churn the GC every frame.
    /// </summary>
    public sealed class SeaStateSelectorPanel : MonoBehaviour
    {
        [SerializeField] SeaStateDirector director;
        [Tooltip("Hulls whose submerged fraction is listed (optional).")]
        [SerializeField] BuoyantBody[] hulls = new BuoyantBody[0];
        [SerializeField] bool visible = true;
        [Tooltip("How often the readout text is rebuilt, Hz.")]
        [SerializeField, Range(1f, 30f)] float refreshHz = 4f;

        const float WindStepDeg = 15f;
        const float Width = 470f, Pad = 10f, Line = 20f, Button = 40f;
        static readonly string[] Digits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        readonly StringBuilder _sb = new StringBuilder(1024);
        string _readout = "";
        string _windLabel = "", _swellLabel = "";
        int _readoutLines;
        float _nextRefresh;
        GUIStyle _label, _title;

        void OnEnable()
        {
            if (director != null) director.ConditionsChanged += OnConditionsChanged;
            _nextRefresh = 0f;
        }

        void OnDisable()
        {
            if (director != null) director.ConditionsChanged -= OnConditionsChanged;
        }

        void OnConditionsChanged(SeaConditions _) => _nextRefresh = 0f;

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

            if (visible && Time.unscaledTime >= _nextRefresh)
            {
                RebuildReadout();
                _nextRefresh = Time.unscaledTime + 1f / refreshHz;
            }
        }

        void OnGUI()
        {
            if (!visible || director == null || !director.IsReady) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };

            float y = 12f + Pad;
            float readoutHeight = _readoutLines * Line + Line;
            GUI.Box(new Rect(12f, 12f, Width, 2 * Pad + 8 * Line + 2 * (Button * 0.7f) + readoutHeight), GUIContent.none);

            GUI.Label(new Rect(12f + Pad, y, Width, Line + 4f), "Sea State — Douglas", _title); y += Line + 6f;

            GUI.Label(new Rect(12f + Pad, y, Width, Line), "Wind sea (keys 0–9)", _label); y += Line;
            int sea = DegreeRow(y, director.DouglasSea); y += Button * 0.7f + 4f;
            if (sea != director.DouglasSea) director.SetSeaState(sea);

            GUI.Label(new Rect(12f + Pad, y, Width, Line), "Swell (Shift + 0–9)", _label); y += Line;
            int swell = DegreeRow(y, director.DouglasSwell); y += Button * 0.7f + 4f;
            if (swell != director.DouglasSwell) director.SetSwell(swell);

            GUI.Label(new Rect(12f + Pad, y, Width, Line), _windLabel, _label); y += Line;
            float wind = GUI.HorizontalSlider(new Rect(12f + Pad, y, Width - 2 * Pad, Line), director.WindFromDeg, 0f, 360f); y += Line;
            if (!Mathf.Approximately(wind, director.WindFromDeg)) director.SetWindFrom(Mathf.Round(wind / 5f) * 5f);

            GUI.Label(new Rect(12f + Pad, y, Width, Line), _swellLabel, _label); y += Line;
            float swellDir = GUI.HorizontalSlider(new Rect(12f + Pad, y, Width - 2 * Pad, Line), director.SwellFromDeg, 0f, 360f); y += Line + 4f;
            if (!Mathf.Approximately(swellDir, director.SwellFromDeg)) director.SetSwellFrom(Mathf.Round(swellDir / 5f) * 5f);

            GUI.Label(new Rect(12f + Pad, y, Width - 2 * Pad, readoutHeight), _readout, _label);
        }

        int DegreeRow(float y, int current)
        {
            for (int d = 0; d <= 9; d++)
            {
                var r = new Rect(12f + Pad + d * (Button + 4f), y, Button, Button * 0.7f);
                if (GUI.Toggle(r, d == current, Digits[d], GUI.skin.button) && d != current) return d;
            }
            return current;
        }

        void RebuildReadout()
        {
            SeaStateLibrarySO lib = director.Library;
            SeaConditions c = director.Conditions;
            SeaBakeReport bake = director.Simulation.LastBake;
            SeaStateProfileSO sea = lib.GetSea(c.DouglasSea);
            SwellProfileSO swell = lib.GetSwell(c.DouglasSwell);
            float tp = c.WindSeaTpS;
            float lambda = (float)WaveSpectrum.DeepWaterWavelength(tp, director.Gravity);
            float knots = c.WindSpeedMs / SeaUnits.MetersPerSecondPerKnot;

            _windLabel = $"Wind from {director.WindFromDeg:0}°   ( [ / ] )";
            _swellLabel = $"Swell from {director.SwellFromDeg:0}°";

            _sb.Clear();
            _readoutLines = 0;
            AppendLine($"<b>Douglas {c.DouglasSea} — {(sea != null ? sea.douglasTerm : "?")}</b>");
            AppendLine($"Hs (wind sea) {c.WindSeaHsM:0.00} m   (WMO 3700 band)");
            AppendLine($"Tp {tp:0.0} s   (PM: 2πU/0.877g, U=√(Hs g/0.21))   λp = gTp²/2π = {lambda:0} m");
            AppendLine($"Wind {knots:0.#} kt = {c.WindSpeedMs:0.0} m/s   (NATO STANAG 4194 mean)");
            AppendLine($"Whitecaps W = 3.84e-6·U^3.41 = {c.WhitecapFraction:P1}");
            AppendLine($"<b>Swell {c.DouglasSwell} — {(swell != null ? swell.douglasTerm : "?")}</b>" +
                       (c.SwellHsM > 0f ? $"   Hs {c.SwellHsM:0.0} m, Tp {c.SwellTpS:0.0} s" : ""));
            AppendLine($"Combined Hs √(Hw²+Hs²) = {c.SignificantWaveHeightM:0.00} m");
            AppendLine($"Resolved Hs 4√(Σa²/2) = {c.ResolvedWaveHeightM:0.00} m   ({bake.ResolvedVarianceFraction:P0} of variance)");
            AppendLine($"   waves shorter than {lib.minResolvedWavelengthM:0} m → HDRP ripples only");
            AppendLine($"Components {director.Simulation.ActiveComponentCount} live ({bake.WindSeaCount} wind + {bake.SwellCount} swell)   " +
                       $"fade {director.Simulation.TransitionProgress:P0}   folded {bake.FoldedFraction:P2}");
            AppendLine($"Sim t = {director.SimulationTimeS:0.00} s   step {director.Simulation.StepCount}");
            if (hulls != null)
                foreach (var h in hulls)
                    if (h != null) AppendLine($"{h.name}: {h.SubmergedFraction:P0} of probe volume submerged");
            AppendLine("<i>Tab hides this panel. Right-drag orbit, wheel zoom, WASD/QE move, F1–F4 views.</i>");
            _readout = _sb.ToString();
        }

        void AppendLine(string text)
        {
            _sb.Append(text).Append('\n');
            _readoutLines++;
        }
    }
}
