using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepWater.Input
{
    /// <summary>
    /// Single-component reader for the VelocityOne (or any HID flight stick). Wraps the
    /// generated <see cref="VelocityOne.inputactions"/> asset, exposes typed properties +
    /// edge events for every consumer in the project (adapters, UI, debug HUDs).
    ///
    /// **Wiring:**
    /// 1. Add this component to a persistent GameObject (e.g. a `_Input` root).
    /// 2. Assign the `VelocityOne.inputactions` asset to <see cref="inputActions"/>.
    /// 3. Read properties (Pitch, Roll, etc.) or subscribe to events (OnFireMissile,
    ///    OnFireSalvo, etc.) from any adapter.
    ///
    /// **Why one shared reader and not per-system InputAction wiring:** if every system
    /// (carrier helm, missile fire, F-18, camera) wired its own `InputAction.CallbackContext`,
    /// the same trigger pull could fire in three places out of order. One reader means one
    /// authoritative axis/button state per frame, and adapters consume it. Simpler to reason
    /// about and to debug from the HUD.
    ///
    /// **Stateless wrt physics.** All polling. Update only. Adapters that drive FixedUpdate
    /// systems can read the cached values from Update — those values are stable across the
    /// fixed step.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class FlightStickInput : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Drag the VelocityOne.inputactions asset here.")]
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Name of the action map. Default is 'Flight' — only change if you customised the asset.")]
        [SerializeField] private string actionMapName = "Flight";

        [Header("Diagnostics")]
        [Tooltip("If true, the reader logs a one-line summary of detected joystick devices on Enable.")]
        [SerializeField] private bool logDevicesOnEnable = true;

        // -- Axes (cached each Update) ------------------------------------
        public float Pitch              { get; private set; }
        public float Roll               { get; private set; }
        public float Yaw                { get; private set; }
        public float ThrottleMain       { get; private set; }
        public float ThrottleSecondary  { get; private set; }
        public Vector2 Hat              { get; private set; }
        public float Trigger            { get; private set; } // analog [0..1] if the stick supports it; binary otherwise

        // -- Edge events (called when button goes from up→down) -----------
        public event Action OnFireMissile;
        public event Action OnFireSalvo;
        public event Action OnNextCameraTarget;
        public event Action OnCameraToggle;
        public event Action OnPause;

        // -- Internals -----------------------------------------------------
        private InputActionMap map;
        private InputActionAsset runtimeActions;
        private InputAction aPitch, aRoll, aYaw, aThrottleMain, aThrottleSecondary, aHat, aTrigger;
        private InputAction aFireMissile, aFireSalvo, aNextCameraTarget, aCameraToggle, aPause;

        private void OnEnable()
        {
            if (inputActions == null)
            {
                Debug.LogError("[FlightStickInput] ❌ NO InputActionAsset ASSIGNED. " +
                    "Select the GameObject with this component in the Hierarchy, then in the Inspector " +
                    "drag 'Assets/Scripts/Input/VelocityOne.inputactions' into the 'Input Actions' field.");
                enabled = false;
                return;
            }
            Debug.Log($"[FlightStickInput] ✓ Asset assigned: {inputActions.name}  (maps: {inputActions.actionMaps.Count})");

            runtimeActions = Instantiate(inputActions);
            map = runtimeActions.FindActionMap(actionMapName, throwIfNotFound: false);
            if (map == null)
            {
                var available = new System.Text.StringBuilder();
                foreach (var m in inputActions.actionMaps) available.Append("'" + m.name + "' ");
                Debug.LogError($"[FlightStickInput] ❌ No action map named '{actionMapName}'. " +
                               $"Maps present in the asset: {available}");
                enabled = false;
                return;
            }
            Debug.Log($"[FlightStickInput] ✓ Action map '{map.name}' found  (actions: {map.actions.Count}, bindings: {map.bindings.Count})");

            aPitch             = ResolveAction("Pitch");
            aRoll              = ResolveAction("Roll");
            aYaw               = ResolveAction("Yaw");
            aThrottleMain      = ResolveAction("ThrottleMain");
            aThrottleSecondary = ResolveAction("ThrottleSecondary");
            aHat               = ResolveAction("Hat");
            aTrigger           = ResolveAction("Trigger");

            aFireMissile       = ResolveAction("FireMissile");
            aFireSalvo         = ResolveAction("FireSalvo");
            aNextCameraTarget  = ResolveAction("NextCameraTarget");
            aCameraToggle      = ResolveAction("CameraToggle");
            aPause             = ResolveAction("Pause");

            // Edge subscriptions — performed on Press (rising edge only).
            if (aFireMissile      != null) aFireMissile.performed      += _ => OnFireMissile?.Invoke();
            if (aFireSalvo        != null) aFireSalvo.performed        += _ => OnFireSalvo?.Invoke();
            if (aNextCameraTarget != null) aNextCameraTarget.performed += _ => OnNextCameraTarget?.Invoke();
            if (aCameraToggle     != null) aCameraToggle.performed     += _ => OnCameraToggle?.Invoke();
            if (aPause            != null) aPause.performed            += _ => OnPause?.Invoke();

            map.Enable();
            Debug.Log($"[FlightStickInput] ✓ Map enabled = {map.enabled}");

            if (logDevicesOnEnable)
                LogDetectedDevices();
        }

        /// <summary>
        /// Find an action by name and log a one-line summary of its bindings, so we can see
        /// in the console whether each action has the expected binding path. Catches typos
        /// in the .inputactions and "Unity loaded a stale cached version" issues.
        /// </summary>
        private InputAction ResolveAction(string name)
        {
            var action = map.FindAction(name);
            if (action == null)
            {
                Debug.LogWarning($"[FlightStickInput]   ⚠ Action '{name}' NOT FOUND in the map.");
                return null;
            }

            var bindings = new System.Text.StringBuilder();
            int countShown = 0;
            foreach (var b in action.bindings)
            {
                if (b.isComposite) continue;
                bindings.Append(b.effectivePath);
                bindings.Append("  ");
                countShown++;
            }
            if (countShown == 0)
                bindings.Append("(no bindings!)");
            Debug.Log($"[FlightStickInput]   • {name,-20} {bindings}");
            return action;
        }

        private void OnDisable()
        {
            if (map != null) map.Disable();
            if (runtimeActions != null) Destroy(runtimeActions);
            runtimeActions = null;
            map = null;
            Pitch = Roll = Yaw = ThrottleMain = ThrottleSecondary = Trigger = 0f;
            Hat = Vector2.zero;
        }

        private void Update()
        {
            // Cache axis values once per frame. Adapters read these freely.
            if (aPitch             != null) Pitch             = aPitch.ReadValue<float>();
            if (aRoll              != null) Roll              = aRoll.ReadValue<float>();
            if (aYaw               != null) Yaw               = aYaw.ReadValue<float>();
            if (aThrottleMain      != null) ThrottleMain      = aThrottleMain.ReadValue<float>();
            if (aThrottleSecondary != null) ThrottleSecondary = aThrottleSecondary.ReadValue<float>();
            if (aHat               != null) Hat               = aHat.ReadValue<Vector2>();
            if (aTrigger           != null) Trigger           = aTrigger.ReadValue<float>();
        }

        // ============================================================
        // Diagnostics
        // ============================================================

        private void LogDetectedDevices()
        {
            int joystickCount = 0;
            foreach (var device in InputSystem.devices)
            {
                if (device is Joystick j)
                {
                    joystickCount++;
                    Debug.Log($"[FlightStickInput] Detected joystick: '{j.displayName}' (path: {j.path})");
                }
            }
            if (joystickCount == 0)
                Debug.LogWarning("[FlightStickInput] No joystick detected. Make sure your VelocityOne is plugged in. (Keyboard fallback bindings are still active.)");
        }
    }
}
