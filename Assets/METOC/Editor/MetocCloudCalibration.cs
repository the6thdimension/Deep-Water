using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeepWater.Metoc.Hdrp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace DeepWater.Metoc.Editor
{
    /// <summary>
    /// Measures how much of the sky HDRP's volumetric clouds cover at each <c>shapeFactor</c> (E48) and stores the table
    /// in the METOC look. Play mode only: HDRP renders cameras to textures only in its normal loop (a hand-called
    /// <c>Camera.Render()</c> to a texture comes out black, C13).
    /// <para>
    /// Five cameras look up, north, east, south and west with a 90° view each: the top face and the side faces' upper
    /// halves tile the upper hemisphere exactly, as a cube map does. A calibration volume above METOC's overlay fixes the
    /// exposure, turns off fog, the 2D cloud layer, tonemapping and bloom, and sets the clouds. First a clear reference
    /// (clouds on at zero density); then each shapeFactor at several cloud offsets. A pixel is cloud where its luminance
    /// differs from the clear reference by more than τ; each pixel is weighted by its solid angle, (1 + u² + v²)^−1.5.
    /// The day is paused for the run through a logged order and restored after; METOC's renderer is switched off for the
    /// run, so the measurement does not depend on what its overlay held.
    /// </para>
    /// <para>
    /// The clouds stay enabled from the cameras' first frame. A camera that has rendered with HDRP's volumetric clouds
    /// off, in a play session where they have never been on, draws them black once a volume turns them on (2026-10-30:
    /// runs 1 and 4 read a black sky in every face; cameras made with the clouds already on rendered them, exp4). So the
    /// clear reference zeroes the density instead of switching the clouds off.
    /// </para>
    /// </summary>
    public static class MetocCloudCalibration
    {
        public const string ResultFile = "Temp/metoc-cloud-calibration.txt";

        static readonly float[] Sweep = { 1.0f, 0.95f, 0.9f, 0.85f, 0.8f, 0.75f, 0.7f, 0.65f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f, 0.0f };
        // HDRP's shapeOffset is in noise-tile units, not metres: one tile is 100 km / shapeScale (20 km at the default 5),
        // and the noise repeats, so a whole-number offset draws the same clouds (2026-10-30: 12000 and −9000 did, and the
        // spread across offsets read 0.002 while two runs a minute apart differed by 0.08). Fractions sample new skies.
        // One sky varies a lot (the up face sees about one cloud's width of deck), so the table averages twelve, spread
        // evenly over the tile by the R2 sequence (golden-ratio steps in two dimensions).
        static readonly Vector3[] Offsets = R2Offsets(12);

        static Vector3[] R2Offsets(int n)
        {
            const double a1 = 0.7548776662466927, a2 = 0.5698402909980532;   // 1/g and 1/g², g³ = g + 1
            var o = new Vector3[n];
            for (int i = 0; i < n; i++)
                o[i] = new Vector3((float)((0.5 + a1 * i) % 1.0), 0f, (float)((0.5 + a2 * i) % 1.0));
            return o;
        }
        static readonly float[] Taus = { 0.08f, 0.15f, 0.3f };   // the table uses the middle one; the others show the sensitivity
        const int TableTau = 1;
        const float FixedExposureEv = 13f;
        const float CalibrationPriority = 200f;

        static readonly (string name, Vector3 forward, Vector3 up)[] Faces =
        {
            ("up", Vector3.up, Vector3.forward),
            ("north", Vector3.forward, Vector3.up),
            ("east", Vector3.right, Vector3.up),
            ("south", Vector3.back, Vector3.up),
            ("west", Vector3.left, Vector3.up),
        };

        static Run _run;

        [MenuItem("Deep Water/METOC/Calibrate Cloud Cover (play mode)")]
        static void Menu() => Start(256, 30);

        [MenuItem("Deep Water/METOC/Calibrate Cloud Cover (play mode)", true)]
        static bool MenuValid() => EditorApplication.isPlaying && _run == null;

        /// <summary>
        /// Starts a calibration in play mode; it runs over the next frames and writes <see cref="ResultFile"/>, starting
        /// "DONE" or "FAILED". Delete an old result first if you wait on the file. With <paramref name="dumpDir"/>, the
        /// up and north faces of the clear reference and of each shapeFactor at the first offset are saved as PNGs.
        /// </summary>
        public static string Start(int resolution, int settleFrames, string dumpDir = null)
        {
            if (!EditorApplication.isPlaying) return "[METOC] Calibration needs play mode.";
            if (_run != null) return "[METOC] A calibration is already running.";
            var renderer = Object.FindFirstObjectByType<HdrpMetocRenderer>();
            if (renderer == null || renderer.Look == null || renderer.Director == null || !renderer.Director.Ready)
                return "[METOC] No running METOC renderer with a look and a director in the scene.";
            _run = new Run(renderer, Mathf.Clamp(resolution, 64, 1024), Mathf.Max(5, settleFrames), dumpDir);
            EditorApplication.update += Tick;
            return $"[METOC] Calibration started: {Sweep.Length} shapeFactors × {Offsets.Length} offsets, {resolution}² per face, {settleFrames} frames to settle.";
        }

        static void Tick()
        {
            if (_run == null) { EditorApplication.update -= Tick; return; }
            string error = null;
            bool done;
            try { done = _run.Step(); }
            catch (Exception e)
            {
                error = e.ToString();
                done = true;
            }
            if (!done && EditorApplication.isPlaying) return;
            if (!EditorApplication.isPlaying && error == null) error = "play mode ended before the calibration finished";
            string text = _run.Finish(error);
            File.WriteAllText(ResultFile, text);
            if (error == null) Debug.Log(text);
            else Debug.LogError(text);
            EditorApplication.update -= Tick;
            _run = null;
        }

        sealed class Run
        {
            readonly HdrpMetocRenderer _renderer;
            readonly HdrpMetocLookSO _look;
            readonly MetocDirector _director;
            readonly int _res, _settle;
            readonly string _dumpDir;
            readonly GameObject _root;
            readonly Camera[] _cams = new Camera[Faces.Length];
            readonly RenderTexture[] _rts = new RenderTexture[Faces.Length];
            readonly Texture2D _read;
            readonly float[] _weight;           // solid-angle weight per pixel
            readonly float[][] _clearY = new float[Faces.Length][];
            readonly VolumeProfile _profile;
            readonly VolumetricClouds _clouds;
            readonly double _daySpeed;
            readonly bool _runInBackground;
            readonly float[,,] _cover;          // [sweep, offset, tau]
            readonly MetocReport _report;
            int _step = -1, _waitUntilFrame;    // step −1 is the clear reference
            float _skyUpperY, _seaLowerY, _eyeY;

            public Run(HdrpMetocRenderer renderer, int res, int settle, string dumpDir)
            {
                _dumpDir = dumpDir;
                _renderer = renderer;
                _look = renderer.Look;
                _director = renderer.Director;
                _res = res;
                _settle = settle;
                _report = _director.Report;
                _cover = new float[Sweep.Length, Offsets.Length, Taus.Length];

                // METOC's overlay steps aside while HDRP's clouds are measured alone, so the table does not depend on the
                // weather the overlay was drawing when the run began.
                _renderer.enabled = false;

                // Hold the sun still: a logged order, undone at the end.
                _daySpeed = _director.Simulation.DaySpeed;
                _director.Issue(MetocOrder.DaySpeed(0, "cloud calibration"));
                _runInBackground = Application.runInBackground;
                Application.runInBackground = true;

                Camera main = Camera.main;
                Vector3 eye = main != null ? main.transform.position : new Vector3(0f, 10f, 0f);
                _root = new GameObject("METOC cloud calibration") { hideFlags = HideFlags.DontSave };
                _root.transform.position = eye;
                _eyeY = eye.y;
                for (int f = 0; f < Faces.Length; f++)
                {
                    var go = new GameObject($"Calibration {Faces[f].name}") { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(_root.transform, false);
                    go.transform.rotation = Quaternion.LookRotation(Faces[f].forward, Faces[f].up);
                    var cam = go.AddComponent<Camera>();
                    go.AddComponent<HDAdditionalCameraData>();
                    cam.fieldOfView = 90f;
                    cam.aspect = 1f;
                    cam.nearClipPlane = 0.3f;
                    cam.farClipPlane = 100000f;
                    _rts[f] = new RenderTexture(res, res, 24, RenderTextureFormat.ARGBHalf) { name = $"METOC calibration {Faces[f].name}", hideFlags = HideFlags.DontSave };
                    _rts[f].Create();
                    cam.targetTexture = _rts[f];
                    _cams[f] = cam;
                }
                _read = new Texture2D(res, res, TextureFormat.RGBAHalf, false, true) { hideFlags = HideFlags.DontSave };

                _weight = new float[res * res];
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float u = 2f * (x + 0.5f) / res - 1f, v = 2f * (y + 0.5f) / res - 1f;
                    _weight[y * res + x] = Mathf.Pow(1f + u * u + v * v, -1.5f);
                }

                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.hideFlags = HideFlags.DontSave;
                var exposure = _profile.Add<Exposure>(false);
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(FixedExposureEv);
                _profile.Add<Fog>(false).enabled.Override(false);
                _profile.Add<CloudLayer>(false).opacity.Override(0f);
                _profile.Add<Tonemapping>(false).mode.Override(TonemappingMode.None);
                _profile.Add<Bloom>(false).intensity.Override(0f);
                _clouds = _profile.Add<VolumetricClouds>(false);
                _clouds.bottomAltitude.Override((float)Math.Max(0.01, _report.Weather.CloudBaseM));
                _clouds.altitudeRange.Override((float)Math.Max(100.0, _report.Weather.CloudThicknessM));
                _clouds.temporalAccumulationFactor.Override(0.5f);
                // The clear reference: clouds on, density 0 (see the class note on why not "off").
                _clouds.enable.Override(true);
                _clouds.densityMultiplier.Override(0f);
                var volume = _root.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = CalibrationPriority;
                volume.sharedProfile = _profile;

                _waitUntilFrame = Time.frameCount + _settle;
            }

            /// <summary>One editor tick: wait for the scene to settle, then measure and set up the next point.</summary>
            public bool Step()
            {
                if (Time.frameCount < _waitUntilFrame) return false;
                if (_step < 0) ReadClear();
                else Measure(_step / Offsets.Length, _step % Offsets.Length);
                _step++;
                if (_step >= Sweep.Length * Offsets.Length) return true;
                _clouds.densityMultiplier.overrideState = false;   // the shared sky's density from here on
                _clouds.shapeFactor.Override(Sweep[_step / Offsets.Length]);
                _clouds.shapeOffset.Override(Offsets[_step % Offsets.Length]);
                _waitUntilFrame = Time.frameCount + _settle;
                return false;
            }

            void ReadClear()
            {
                for (int f = 0; f < Faces.Length; f++) _clearY[f] = ReadLuminance(f, f <= 1 ? "clear" : null);
                // An orientation check: on a side face the sky above the horizon is brighter than the sea below it.
                float[] north = _clearY[1];
                double up = 0, down = 0;
                for (int y = 0; y < _res; y++)
                for (int x = 0; x < _res; x++)
                    if (y >= _res / 2) up += north[y * _res + x]; else down += north[y * _res + x];
                _skyUpperY = (float)(up / (_res * _res / 2));
                _seaLowerY = (float)(down / (_res * _res / 2));
            }

            void Measure(int s, int o)
            {
                var cloud = new double[Taus.Length];
                double total = 0;
                for (int f = 0; f < Faces.Length; f++)
                {
                    string dump = o == 0 && f <= 1 ? $"sf{Sweep[s]:0.00}" : null;
                    float[] on = ReadLuminance(f, dump), off = _clearY[f];
                    int y0 = f == 0 ? 0 : _res / 2;   // side faces: the upper half only
                    for (int y = y0; y < _res; y++)
                    for (int x = 0; x < _res; x++)
                    {
                        int i = y * _res + x;
                        float w = _weight[i];
                        total += w;
                        float rel = Mathf.Abs(on[i] - off[i]) / Mathf.Max(off[i], 1e-6f);
                        for (int t = 0; t < Taus.Length; t++)
                            if (rel > Taus[t]) cloud[t] += w;
                    }
                }
                for (int t = 0; t < Taus.Length; t++) _cover[s, o, t] = (float)(cloud[t] / total);
            }

            float[] ReadLuminance(int f, string dumpTag)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _rts[f];
                _read.ReadPixels(new Rect(0, 0, _res, _res), 0, 0, false);
                RenderTexture.active = prev;
                Color[] px = _read.GetPixels();
                var y = new float[px.Length];
                for (int i = 0; i < px.Length; i++) y[i] = 0.2126f * px[i].r + 0.7152f * px[i].g + 0.0722f * px[i].b;
                if (_dumpDir != null && dumpTag != null)
                {
                    var ldr = new Texture2D(_res, _res, TextureFormat.RGBA32, false);
                    var c = new Color[px.Length];
                    for (int i = 0; i < px.Length; i++) c[i] = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f);
                    ldr.SetPixels(c);
                    Directory.CreateDirectory(_dumpDir);
                    File.WriteAllBytes(Path.Combine(_dumpDir, $"cal-{dumpTag}-{Faces[f].name}.png"), ldr.EncodeToPNG());
                    Object.Destroy(ldr);
                }
                return y;
            }

            public string Finish(string error)
            {
                var sb = new StringBuilder();
                try
                {
                    _director.Issue(MetocOrder.DaySpeed(_daySpeed, "cloud calibration"));
                    _renderer.enabled = true;
                    Application.runInBackground = _runInBackground;
                    Object.Destroy(_root);
                    foreach (var rt in _rts) if (rt != null) { rt.Release(); Object.Destroy(rt); }
                    Object.Destroy(_read);
                    Object.Destroy(_profile);
                }
                catch (Exception e) { error = (error == null ? "" : error + "\n") + "cleanup: " + e.Message; }

                if (error != null) return $"FAILED [METOC] Cloud calibration: {error}";
                int n = Sweep.Length;
                var table = new CloudCoverCalibration
                {
                    shapeFactor = (float[])Sweep.Clone(),
                    coverMean = new float[n],
                    coverMin = new float[n],
                    coverMax = new float[n],
                };
                var ci = CultureInfo.InvariantCulture;
                sb.AppendLine("shapeFactor  mean   min    max    | mean at tau 0.08  0.30");
                for (int s = 0; s < n; s++)
                {
                    float sum = 0, lo = 1, hi = 0, sumLo = 0, sumHi = 0;
                    for (int o = 0; o < Offsets.Length; o++)
                    {
                        float c = _cover[s, o, TableTau];
                        sum += c;
                        lo = Mathf.Min(lo, c);
                        hi = Mathf.Max(hi, c);
                        sumLo += _cover[s, o, 0];
                        sumHi += _cover[s, o, 2];
                    }
                    table.coverMean[s] = sum / Offsets.Length;
                    table.coverMin[s] = lo;
                    table.coverMax[s] = hi;
                    sb.AppendLine(string.Format(ci, "{0,11:0.00}  {1:0.000}  {2:0.000}  {3:0.000}  |       {4:0.000}  {5:0.000}",
                        Sweep[s], table.coverMean[s], lo, hi, sumLo / Offsets.Length, sumHi / Offsets.Length));
                }
                string orientation = _skyUpperY > _seaLowerY ? "ok" : "SUSPECT (the lower half is brighter: the image may be flipped)";
                table.provenance = string.Format(ci,
                    "Measured {0:yyyy-MM-dd HH:mm} by Deep Water → METOC → Calibrate Cloud Cover in {1}. Eye at {2:0} m; sun {3:0.0}° elevation, " +
                    "{4:0.0}° azimuth. Clouds: base {5:0} m, thickness {6:0} m, the shared profile's density and shape otherwise; fog, the 2D layer, " +
                    "tonemapping and bloom off; fixed exposure EV {7:0}. Five 90° faces at {8}² (the upper hemisphere), solid-angle weighted; " +
                    "{9} fractional noise-tile offsets per point (min and max are the spread across them), {10} frames to settle, temporal accumulation 0.5. A pixel is cloud where its luminance differs " +
                    "from the clear reference (the same clouds at density 0) by more than τ = {11:0.00}. Orientation check {12} (north face upper {13:0.###}, lower {14:0.###}).\n{15}",
                    DateTime.Now, UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, _eyeY,
                    _report.Sun.Sky.ElevationDeg, _report.Sun.Sky.AzimuthDeg, _report.Weather.CloudBaseM, _report.Weather.CloudThicknessM,
                    FixedExposureEv, _res, Offsets.Length, _settle, Taus[TableTau], orientation, _skyUpperY, _seaLowerY, sb);

                // A table that does not change with shapeFactor is a broken measurement, not a calibration.
                float rise = table.coverMean[n - 1] - table.coverMean[0];
                string problem = table.Problem() ?? (rise < 0.3f ? $"the cover rises only {rise:0.000} across the sweep" : null);
                if (problem != null) return $"FAILED [METOC] Cloud calibration measured a table that cannot be inverted: {problem}\n{table.provenance}";
                Undo.RecordObject(_look, "METOC cloud calibration");
                _look.cloudCover = table;
                EditorUtility.SetDirty(_look);
                AssetDatabase.SaveAssets();
                return $"DONE [METOC] Cloud calibration saved to {AssetDatabase.GetAssetPath(_look)}.\n{table.provenance}";
            }
        }
    }
}
