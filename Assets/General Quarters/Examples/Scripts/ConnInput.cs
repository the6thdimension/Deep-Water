using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// Reads the "Conn" action map, the one reader for the conn (the DeepWater.Input pattern: one reader, many
    /// consumers). The map lives in the project-wide actions (Project Settings → Input System Package), so its
    /// bindings are rebound there. Control schemes: Keyboard&amp;Mouse (a laptop), Gamepad (Xbox and any pad Unity
    /// recognises), Joystick (generic HID pads). Every binding is live at once; whichever device moves, steers.
    /// </summary>
    public sealed class ConnInput : MonoBehaviour
    {
        public const string MapName = "Conn";

        [Tooltip("Leave empty to use the project-wide actions (Project Settings → Input System Package).")]
        [SerializeField] InputActionAsset actions;

        InputActionMap _map;
        InputAction _rudder, _rudderHard, _course, _ringUp, _ringDown, _steady, _midships, _allStop, _cameraMode, _togglePanel;
        InputAction _cameraOrbit, _cameraDrag, _cameraZoom, _cameraScroll;

        /// <summary>The device that last moved a conn control, for the panel ("Xbox Controller", "Keyboard", …).</summary>
        public string LastDevice { get; private set; } = "none yet";
        public bool Ready => _map != null;

        void Awake()
        {
            InputActionAsset asset = actions != null ? actions : InputSystem.actions;
            _map = asset != null ? asset.FindActionMap(MapName) : null;
            if (_map == null)
            {
                Debug.LogError($"[ConnInput] No '{MapName}' action map in {(asset != null ? asset.name : "the project-wide actions (none set)")}; " +
                               "the conn has no controls. It ships in Assets/RHMILCOM.inputactions.", this);
                enabled = false;
                return;
            }
            _rudder = _map.FindAction("Rudder", true);
            _rudderHard = _map.FindAction("RudderHard", true);
            _course = _map.FindAction("CourseChange", true);
            _ringUp = _map.FindAction("RingUp", true);
            _ringDown = _map.FindAction("RingDown", true);
            _steady = _map.FindAction("Steady", true);
            _midships = _map.FindAction("Midships", true);
            _allStop = _map.FindAction("AllStop", true);
            _cameraMode = _map.FindAction("CameraMode", true);
            _togglePanel = _map.FindAction("TogglePanel", true);
            _cameraOrbit = _map.FindAction("CameraOrbit", true);
            _cameraDrag = _map.FindAction("CameraDrag", true);
            _cameraZoom = _map.FindAction("CameraZoom", true);
            _cameraScroll = _map.FindAction("CameraScroll", true);
        }

        void OnEnable() { if (_map != null) { _map.Enable(); _map.actionTriggered += Seen; } }
        void OnDisable() { if (_map != null) { _map.actionTriggered -= Seen; _map.Disable(); } }

        void Seen(InputAction.CallbackContext context)
        {
            if (context.control != null) LastDevice = context.control.device.displayName;
        }

        /// <summary>This frame's conn input for <see cref="ConnLogic"/>.</summary>
        public ConnInputFrame Frame() => new ConnInputFrame
        {
            Rudder = CombineRudder(_rudder.ReadValue<float>(), _rudderHard.IsPressed()),
            CourseChange = _course.ReadValue<float>(),
            RingUp = _ringUp.WasPressedThisFrame(),
            RingDown = _ringDown.WasPressedThisFrame(),
            Steady = _steady.WasPressedThisFrame(),
            Midships = _midships.WasPressedThisFrame(),
            AllStop = _allStop.WasPressedThisFrame(),
        };

        /// <summary>
        /// With the hard-rudder modifier held (Shift), any rudder demand goes hard over. A plain modifier button, not a
        /// Shift+A composite: on one action a Scale processor changes A's value but not its actuation, so A and Shift+A
        /// tie and the first key wins; and a composite sharing A with the Rudder action only fires if Shift comes first.
        /// </summary>
        public static float CombineRudder(float rudder, bool hard) => hard && Mathf.Abs(rudder) > 0.01f ? Mathf.Sign(rudder) : rudder;

        public bool CameraModePressed => _cameraMode != null && _cameraMode.WasPressedThisFrame();
        public bool TogglePanelPressed => _togglePanel != null && _togglePanel.WasPressedThisFrame();
        /// <summary>Camera orbit rate from a stick, −1…1 per axis.</summary>
        public Vector2 CameraOrbit => _cameraOrbit != null ? _cameraOrbit.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Mouse movement this frame while the right button is held, pixels.</summary>
        public Vector2 CameraDrag => _cameraDrag != null ? _cameraDrag.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Zoom rate from triggers or R/F, −1 (out) … +1 (in).</summary>
        public float CameraZoom => _cameraZoom != null ? _cameraZoom.ReadValue<float>() : 0f;
        /// <summary>Mouse wheel this frame.</summary>
        public float CameraScroll => _cameraScroll != null ? _cameraScroll.ReadValue<float>() : 0f;
    }
}
