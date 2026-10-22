using UnityEngine;
using GuidedFury.Examples;

namespace DeepWater.Input.Adapters
{
    /// <summary>
    /// Hold-to-fire adapter for the VelocityOne trigger. The trigger must be held above
    /// <see cref="triggerHoldThreshold"/> for <see cref="holdSecondsToFire"/> seconds
    /// before the missile launches. Releasing early cancels the charge.
    ///
    /// **Why hold-to-fire and not press-edge:** real launch sequences have an arming
    /// dwell ("squeeze and hold until tone, then release"). It also prevents accidental
    /// salvos from a flinched trigger and gives the operator a beat to confirm the shot.
    /// The progress bar makes it obvious that the trigger is being read — if the bar
    /// never starts climbing, the binding is wrong; if it climbs but never fires, the
    /// runner / launcher is missing.
    ///
    /// **Salvo:** still edge-fired off <see cref="FlightStickInput.OnFireSalvo"/> — the
    /// salvo button is the secondary "panic" button, not the primary trigger.
    /// </summary>
    public sealed class FlightStickToTestRunner : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private FlightStickInput reader;
        [SerializeField] private TargetSelector targetSelector;

        [Header("Hold-to-fire")]
        [Tooltip("How long the trigger must be held above the threshold before the missile fires. Seconds.")]
        [Min(0.1f)]
        [SerializeField] private float holdSecondsToFire = 5f;

        [Tooltip("Trigger value above which we count it as 'pressed' for the hold timer. " +
                 "Set above the noise floor — VelocityOne reports a small non-zero idle.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float triggerHoldThreshold = 0.5f;

        [Tooltip("If true, holding the trigger past the fire moment continues to charge a second shot. " +
                 "If false, the operator has to release and re-pull. Default = release-and-pull (safer).")]
        [SerializeField] private bool allowAutoRepeat = false;

        [Header("HUD")]
        [Tooltip("Draw a small charge bar in the corner of the screen so it's visible whether the trigger is being read.")]
        [SerializeField] private bool drawChargeHud = true;

        [Tooltip("Screen-space anchor: 0,0 = top-left.")]
        [SerializeField] private Vector2 hudAnchorPx = new Vector2(20f, 80f);

        // -- Runtime state ---------------------------------------------------
        private float holdTime;       // accumulated seconds the trigger has been above threshold
        private bool firedThisHold;   // latched once we've fired on this continuous press

        private float lastScan;
        private const float ScanIntervalS = 0.5f;
        private GuidedFury_TestRunner[] cachedRunners;
        private LodComparisonRunner[] cachedLodCompares;
        private SalvoRunner[] cachedSalvos;

        /// <summary>0..1 charge progress, for HUDs / external indicators.</summary>
        public float ChargeFraction => holdSecondsToFire > 0f ? Mathf.Clamp01(holdTime / holdSecondsToFire) : 0f;

        // ============================================================
        // Lifecycle
        // ============================================================
        private void OnEnable()
        {
            if (reader == null) reader = FindFirstObjectByType<FlightStickInput>();
            if (targetSelector == null) targetSelector = FindFirstObjectByType<TargetSelector>();
            holdTime = 0f;
            firedThisHold = false;
            lastScan = float.NegativeInfinity;

            // Salvo stays as an edge event — only primary fire is hold-to-arm.
            if (reader != null)
                reader.OnFireSalvo += HandleFireSalvo;
        }

        private void OnDisable()
        {
            if (reader != null)
                reader.OnFireSalvo -= HandleFireSalvo;
        }

        private void Update()
        {
            RescanIfDue();

            if (reader == null || !reader.isActiveAndEnabled || Time.timeScale <= 0f)
            {
                holdTime = 0f;
                firedThisHold = false;
                return;
            }

            // Use unscaledDeltaTime so the hold-to-fire feel is independent of any in-play
            // timescale slider — the operator's UX shouldn't slow down with bullet-time.
            float dt = Time.unscaledDeltaTime;

            bool held = reader.Trigger >= triggerHoldThreshold;
            if (held)
            {
                holdTime += dt;
                if (!firedThisHold && holdTime >= holdSecondsToFire)
                {
                    Fire();
                    firedThisHold = true;
                    if (allowAutoRepeat)
                    {
                        // Reset for a follow-up shot while still held.
                        holdTime = 0f;
                        firedThisHold = false;
                    }
                }
            }
            else
            {
                // Released — clear so the next pull starts fresh.
                holdTime = 0f;
                firedThisHold = false;
            }
        }

        private void RescanIfDue()
        {
            if (Time.unscaledTime - lastScan < ScanIntervalS) return;
            cachedRunners     = FindObjectsByType<GuidedFury_TestRunner>(FindObjectsSortMode.None);
            cachedLodCompares = FindObjectsByType<LodComparisonRunner>(FindObjectsSortMode.None);
            cachedSalvos      = FindObjectsByType<SalvoRunner>(FindObjectsSortMode.None);
            lastScan = Time.unscaledTime;
        }

        // ============================================================
        // Fire actions
        // ============================================================
        private void Fire()
        {
            if (cachedRunners == null) return;
            foreach (var r in cachedRunners)
            {
                if (r == null) continue;
                if (targetSelector != null && targetSelector.CurrentTarget != null)
                    r.SetTarget(targetSelector.CurrentTarget);
                r.FireOnce();
                return; // fire the first runner only; user can disable extras
            }
            Debug.LogWarning("[FlightStickToTestRunner] Hold completed but no GuidedFury_TestRunner found in the scene.");
        }

        private void HandleFireSalvo()
        {
            // Prefer LodComparison if present (more visually interesting); else first SalvoRunner.
            if (cachedLodCompares != null)
            {
                foreach (var lc in cachedLodCompares)
                {
                    if (lc == null) continue;
                    lc.FireSalvoNow();
                    return;
                }
            }
            if (cachedSalvos != null)
            {
                foreach (var s in cachedSalvos)
                {
                    if (s == null) continue;
                    s.FireNow();
                    return;
                }
            }
        }

        // ============================================================
        // HUD — small charge bar so "trigger isn't responding" is immediately visible
        // ============================================================
        private static Texture2D _whitePx;

        private void OnGUI()
        {
            if (!drawChargeHud) return;
            if (reader == null) return;
            EnsureWhitePx();

            const float w = 240f;
            const float h = 14f;
            float x = hudAnchorPx.x;
            float y = hudAnchorPx.y;

            // Background bar
            DrawRect(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.55f));

            // Fill bar — color shifts red → amber → green as charge builds.
            float frac = ChargeFraction;
            Color fill = firedThisHold
                ? new Color(0.20f, 0.95f, 0.20f, 0.9f)
                : Color.Lerp(new Color(0.85f, 0.20f, 0.10f, 0.9f),
                             new Color(0.20f, 0.95f, 0.20f, 0.9f), frac);
            DrawRect(new Rect(x + 1f, y + 1f, (w - 2f) * frac, h - 2f), fill);

            // Outline frame
            DrawRectOutline(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.4f));

            // Label
            string text = firedThisHold
                ? $"FIRED  •  trigger {reader.Trigger:0.00}"
                : $"HOLD TO FIRE  {holdTime:0.0}/{holdSecondsToFire:0.0}s  •  trigger {reader.Trigger:0.00}";
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
            };
            GUI.Label(new Rect(x + 6f, y - 1f, w, h + 2f), text, style);
        }

        private static void EnsureWhitePx()
        {
            if (_whitePx != null) return;
            _whitePx = new Texture2D(1, 1);
            _whitePx.SetPixel(0, 0, Color.white);
            _whitePx.Apply();
            _whitePx.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void DrawRect(Rect r, Color c)
        {
            var orig = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _whitePx, ScaleMode.StretchToFill);
            GUI.color = orig;
        }

        private static void DrawRectOutline(Rect r, Color c)
        {
            DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }
    }
}
