using UnityEngine;
using CarrierOps.Core.Carrier;
using CarrierOps.Core.State;

namespace CarrierOps.Examples
{
    /// <summary>
    /// In-play control panel for a CarrierBehaviour in the scene. Sits in the top-left
    /// (offset from the missile range's top-right panel so they don't overlap if both are
    /// in the same scene). Lets you:
    ///
    /// - Fire any catapult (button per cat + a "fire selected" keyboard shortcut)
    /// - Deploy / stow each elevator
    /// - Set throttle and rudder
    /// - Inspect per-catapult stage in real time
    ///
    /// **Discovery:** finds CarrierBehaviour instances by scanning at 4 Hz, same pattern as
    /// MissileControlPanel. New carriers added at runtime appear within ~0.25 s.
    ///
    /// **Keyboard:**
    /// - C / V / B / N — fire catapults 1 / 2 / 3 / 4 respectively
    /// - Q / E — rudder left / right increment
    /// - W / S — throttle up / down increment
    /// </summary>
    public sealed class CarrierLaunchControlPanel : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Panel width in pixels.")]
        [SerializeField] private float width = 320f;

        [Tooltip("Margin from the top-left corner, pixels.")]
        [SerializeField] private Vector2 marginFromTopLeft = new Vector2(12f, 12f);

        private CarrierBehaviour[] cached;
        private int selectedCarrier = 0;
        private float lastScan;
        private const float ScanIntervalS = 0.5f;

        private GUIStyle headerStyle;
        private GUIStyle smallStyle;

        private void Update()
        {
            if (Time.unscaledTime - lastScan >= ScanIntervalS)
            {
                cached = FindObjectsByType<CarrierBehaviour>(FindObjectsSortMode.None);
                lastScan = Time.unscaledTime;
            }
            HandleKeyboardShortcuts();
        }

        private void HandleKeyboardShortcuts()
        {
            var current = CurrentCarrier();
            if (current == null) return;

            if (Input.GetKeyDown(KeyCode.C)) current.RequestCatapultLaunch(0);
            if (Input.GetKeyDown(KeyCode.V)) current.RequestCatapultLaunch(1);
            if (Input.GetKeyDown(KeyCode.B)) current.RequestCatapultLaunch(2);
            if (Input.GetKeyDown(KeyCode.N)) current.RequestCatapultLaunch(3);

            // Throttle / rudder incremental (held keys produce continuous effect).
            float throttleDelta = 0f;
            if (Input.GetKey(KeyCode.W)) throttleDelta += 0.5f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.S)) throttleDelta -= 0.5f * Time.unscaledDeltaTime;
            if (throttleDelta != 0f && current.State != null)
                current.SetThrottle(Mathf.Clamp01(GuessCurrentThrottle(current) + throttleDelta));

            float rudderDelta = 0f;
            if (Input.GetKey(KeyCode.Q)) rudderDelta -= 1.5f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.E)) rudderDelta += 1.5f * Time.unscaledDeltaTime;
            if (rudderDelta != 0f && current.State != null)
                current.SetRudder(Mathf.Clamp(GuessCurrentRudder(current) + rudderDelta, -1f, 1f));
        }

        // We don't expose throttle/rudder getters on CarrierBehaviour to keep its API small —
        // for the panel we accept the small approximation of round-tripping through the
        // SerializedObject. For Phase 1 this is just used for keyboard delta accumulation.
        // In a future polish pass we can expose proper read-back.
        private float GuessCurrentThrottle(CarrierBehaviour cb) { return _lastThrottle; }
        private float GuessCurrentRudder(CarrierBehaviour cb)   { return _lastRudder; }
        private float _lastThrottle, _lastRudder;

        private void OnGUI()
        {
            EnsureStyles();

            CarrierBehaviour current = CurrentCarrier();
            if (current == null || current.State == null)
            {
                DrawNoCarrier();
                return;
            }

            CarrierState s = current.State;
            int catCount = s.Catapults.Length;
            int elevCount = s.Elevators.Length;
            int wireCount = s.Wires.Length;

            // Panel height grows with subsystem count.
            float height = 30f                 // header
                         + 60f                 // helm
                         + 22f                 // catapult header
                         + catCount * 40f      // cat rows
                         + 22f                 // elevator header
                         + elevCount * 30f     // elev rows
                         + 22f                 // recovery header
                         + 30f                 // FLOLS row
                         + wireCount * 22f     // wire rows
                         + 12f;                // padding

            var rect = new Rect(marginFromTopLeft.x, marginFromTopLeft.y, width, height);
            GUI.Box(rect, "");

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));
            GUILayout.Label($"Carrier: {(cached != null && cached.Length > 1 ? $"[{selectedCarrier+1}/{cached.Length}] " : "")}{current.name}",
                            headerStyle);

            // -- Helm ------------------------------------------------------
            GUILayout.Label($"Speed: {s.SpeedKnots:0.0} kt   Heading: {s.HeadingDeg:0.}°", smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Throttle", GUILayout.Width(60f));
            float newThrottle = GUILayout.HorizontalSlider(_lastThrottle, 0f, 1f);
            if (!Mathf.Approximately(newThrottle, _lastThrottle))
            {
                _lastThrottle = newThrottle;
                current.SetThrottle(newThrottle);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rudder", GUILayout.Width(60f));
            float newRudder = GUILayout.HorizontalSlider(_lastRudder, -1f, 1f);
            if (!Mathf.Approximately(newRudder, _lastRudder))
            {
                _lastRudder = newRudder;
                current.SetRudder(newRudder);
            }
            GUILayout.EndHorizontal();

            // -- Catapults -------------------------------------------------
            GUILayout.Label("Catapults", headerStyle);
            for (int i = 0; i < catCount; i++)
            {
                var cat = s.Catapults[i];
                string label = $"Cat {i + 1}: {cat.Stage}";
                if (cat.Stage == CatapultStage.Firing)
                    label += $" — {cat.ShuttleVelocityMps:0.} m/s";

                GUILayout.BeginHorizontal();
                bool canFire = cat.Stage == CatapultStage.Idle;
                GUI.enabled = canFire;
                if (GUILayout.Button($"Fire {i + 1}", GUILayout.Width(70f)))
                    current.RequestCatapultLaunch(i);
                GUI.enabled = true;
                GUILayout.Label(label, smallStyle);
                GUILayout.EndHorizontal();
            }

            // -- Elevators -------------------------------------------------
            GUILayout.Label("Elevators", headerStyle);
            for (int i = 0; i < elevCount; i++)
            {
                var elev = s.Elevators[i];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(elev.CommandUp ? $"Stow {i + 1}" : $"Deploy {i + 1}",
                                     GUILayout.Width(85f)))
                    current.RequestElevator(i, !elev.CommandUp);
                GUILayout.Label($"E{i + 1}: {elev.Stage} ({elev.Travel:0.00})", smallStyle);
                GUILayout.EndHorizontal();
            }

            // -- Recovery (FLOLS + wires) ----------------------------------
            GUILayout.Label("Recovery", headerStyle);
            string ballText = s.Flols.HasTrack
                ? $"Ball: {s.Flols.BallOffsetNormalized:+0.00;-0.00} ({s.Flols.GlideslopeDeviationDeg:+0.0;-0.0}°)" +
                  (s.Flols.IsWaveOff ? " — WAVE OFF" : "")
                : "Ball: (no track)";
            GUILayout.Label(ballText, smallStyle);

            for (int i = 0; i < wireCount; i++)
            {
                var wire = s.Wires[i];
                string wireText = $"Wire {i + 1}: {wire.Stage}";
                if (wire.Stage == WireStage.Decelerating)
                    wireText += $"  runout {wire.RunoutMeters:0.}/{current.Entity.Profile.WireStrokeM:0.}m";
                GUILayout.Label(wireText, smallStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawNoCarrier()
        {
            var rect = new Rect(marginFromTopLeft.x, marginFromTopLeft.y, width, 40f);
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));
            GUILayout.Label("Carrier Control: no CarrierBehaviour in scene.", smallStyle);
            GUILayout.EndArea();
        }

        private CarrierBehaviour CurrentCarrier()
        {
            if (cached == null || cached.Length == 0) return null;
            selectedCarrier = Mathf.Clamp(selectedCarrier, 0, cached.Length - 1);
            return cached[selectedCarrier];
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                headerStyle.normal.textColor = Color.white;
            }
            if (smallStyle == null)
            {
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                smallStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
