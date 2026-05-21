using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuidedFury.Examples
{
    /// <summary>
    /// In-play-mode control panel for the test range. IMGUI-based, sits top-right of the
    /// screen. Lets you fire missiles on demand and control simulation speed without
    /// leaving Play mode.
    ///
    /// **Discovery:** scans the scene for <see cref="GuidedFury_TestRunner"/> and
    /// <see cref="LodComparisonRunner"/> instances; surfaces one Fire button per runner.
    /// New runners added at runtime appear in the panel on the next scan (4 Hz).
    ///
    /// **Keyboard shortcuts:**
    /// - `L` — fire the first single-missile runner
    /// - `K` — fire the first LOD comparison runner
    /// - `1` / `2` / `3` / `4` / `5` — time scale presets (0.1× / 0.5× / 1× / 2× / 5×)
    /// - `P` — pause / resume (toggle timeScale between 0 and 1)
    /// - `R` — reload the current scene
    /// </summary>
    public sealed class MissileControlPanel : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Panel width in pixels.")]
        [SerializeField] private float width = 280f;

        [Tooltip("Panel anchor margin from top-right corner, pixels.")]
        [SerializeField] private Vector2 marginFromTopRight = new Vector2(12f, 12f);

        [Header("Time Scale")]
        [Tooltip("Max time scale exposed by the slider. Higher = faster sim, but eventually physics solver instability.")]
        [SerializeField] private float maxTimeScale = 5f;

        private GuidedFury_TestRunner[] cachedRunners;
        private LodComparisonRunner[] cachedComparisons;
        private float lastScan;
        private const float ScanIntervalS = 0.5f;

        // Cached so we can restore pre-pause timescale.
        private float preTPauseTimeScale = 1f;

        private GUIStyle headerStyle;
        private GUIStyle buttonStyle;

        private void Update()
        {
            if (Time.unscaledTime - lastScan >= ScanIntervalS)
            {
                cachedRunners     = FindObjectsByType<GuidedFury_TestRunner>(FindObjectsSortMode.None);
                cachedComparisons = FindObjectsByType<LodComparisonRunner>(FindObjectsSortMode.None);
                lastScan = Time.unscaledTime;
            }

            HandleKeyboardShortcuts();
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.L) && cachedRunners != null && cachedRunners.Length > 0)
                cachedRunners[0].FireOnce();
            if (Input.GetKeyDown(KeyCode.K) && cachedComparisons != null && cachedComparisons.Length > 0)
                cachedComparisons[0].FireSalvoNow();

            if (Input.GetKeyDown(KeyCode.Alpha1)) Time.timeScale = 0.1f;
            if (Input.GetKeyDown(KeyCode.Alpha2)) Time.timeScale = 0.5f;
            if (Input.GetKeyDown(KeyCode.Alpha3)) Time.timeScale = 1f;
            if (Input.GetKeyDown(KeyCode.Alpha4)) Time.timeScale = 2f;
            if (Input.GetKeyDown(KeyCode.Alpha5)) Time.timeScale = Mathf.Min(5f, maxTimeScale);

            if (Input.GetKeyDown(KeyCode.P)) TogglePause();
            if (Input.GetKeyDown(KeyCode.R)) ReloadScene();
        }

        private void OnGUI()
        {
            EnsureStyles();

            float x = Screen.width - width - marginFromTopRight.x;
            float y = marginFromTopRight.y;

            // Compute height based on the number of runners present.
            int runnerCount = (cachedRunners?.Length ?? 0) + (cachedComparisons?.Length ?? 0);
            float height = 22f                                           // header
                         + 22f                                           // launch section header
                         + Mathf.Max(1, runnerCount) * 26f               // one row per runner (or "no runners" row)
                         + 8f                                            // spacer
                         + 22f                                           // time scale header
                         + 26f                                           // slider row
                         + 28f                                           // preset buttons
                         + 8f                                            // spacer
                         + 28f                                           // pause / reload row
                         + 16f;                                          // padding

            var rect = new Rect(x, y, width, height);
            GUI.Box(rect, "");

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));
            GUILayout.Label("Missile Range Controls", headerStyle);

            // -- Launch section ------------------------------------------------
            GUILayout.Label("Launch:", buttonStyle);
            DrawRunnerButtons();

            GUILayout.Space(6f);

            // -- Time scale section --------------------------------------------
            GUILayout.Label($"Time scale: {Time.timeScale:0.00}×", buttonStyle);

            GUILayout.BeginHorizontal();
            float scale = GUILayout.HorizontalSlider(Time.timeScale, 0f, maxTimeScale);
            if (!Mathf.Approximately(scale, Time.timeScale))
                Time.timeScale = scale;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.1×")) Time.timeScale = 0.1f;
            if (GUILayout.Button("0.5×")) Time.timeScale = 0.5f;
            if (GUILayout.Button("1×"))   Time.timeScale = 1f;
            if (GUILayout.Button("2×"))   Time.timeScale = 2f;
            if (GUILayout.Button("5×"))   Time.timeScale = Mathf.Min(5f, maxTimeScale);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // -- Scene / pause -------------------------------------------------
            GUILayout.BeginHorizontal();
            string pauseLabel = Time.timeScale > 0f ? "Pause [P]" : "Resume [P]";
            if (GUILayout.Button(pauseLabel)) TogglePause();
            if (GUILayout.Button("Reload [R]")) ReloadScene();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawRunnerButtons()
        {
            bool any = false;

            if (cachedRunners != null)
            {
                for (int i = 0; i < cachedRunners.Length; i++)
                {
                    if (cachedRunners[i] == null) continue;
                    any = true;
                    if (GUILayout.Button($"Fire: {cachedRunners[i].gameObject.name} [L]"))
                        cachedRunners[i].FireOnce();
                }
            }
            if (cachedComparisons != null)
            {
                for (int i = 0; i < cachedComparisons.Length; i++)
                {
                    if (cachedComparisons[i] == null) continue;
                    any = true;
                    if (GUILayout.Button($"Salvo: {cachedComparisons[i].gameObject.name} [K]"))
                        cachedComparisons[i].FireSalvoNow();
                }
            }

            if (!any)
                GUILayout.Label("  (no runners in scene)");
        }

        private void TogglePause()
        {
            if (Time.timeScale > 0f)
            {
                preTPauseTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = preTPauseTimeScale > 0f ? preTPauseTimeScale : 1f;
            }
        }

        private void ReloadScene()
        {
            // Restore timescale before reload — paused scenes that reload while paused are
            // a common foot-gun. The fresh scene starts at 1×.
            Time.timeScale = 1f;

            Scene active = SceneManager.GetActiveScene();
            // LoadScene has separate int / string overloads; the ternary form `cond ? int : string`
            // doesn't compile because the two branches have different types and there's no implicit
            // conversion. Splitting into two explicit calls picks the right overload directly.
            if (active.buildIndex >= 0)
                SceneManager.LoadScene(active.buildIndex);
            else
                SceneManager.LoadScene(active.name);
        }

        private void OnDisable()
        {
            // Make sure we don't leave the editor paused if this component is removed mid-play.
            if (Time.timeScale <= 0f)
                Time.timeScale = 1f;
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                };
                headerStyle.normal.textColor = Color.white;
            }
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                };
                buttonStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
