using UnityEngine;
using UnityEngine.InputSystem;
using DeepWater.AirWing.Modules.Aircraft;

namespace DeepWater.Aviator.Modules
{
    /// <summary>
    /// The player's orders to the AI pilots (A3): RH Aviator's own scene component and input, so Air Wing never learns
    /// about AI orders (W5). One order so far: <b>go home</b> (H, or the Xbox D-pad up) sends every AI pilot that has a
    /// home runway and is not in the jet the player flies home to land (W47). Orders are given between ticks and logged
    /// in each pilot's rule log at the tick they take effect.
    /// </summary>
    public sealed class AviatorOrders : MonoBehaviour
    {
        InputAction _goHome;
        AirWingRunner _runner;

        void OnEnable()
        {
            _goHome = new InputAction("Go Home", InputActionType.Button);
            _goHome.AddBinding("<Keyboard>/h");
            _goHome.AddBinding("<Gamepad>/dpad/up");
            _goHome.Enable();
        }

        void OnDisable()
        {
            _goHome?.Disable();
            _goHome?.Dispose();
            _goHome = null;
        }

        void Update()
        {
            if (_goHome == null || !_goHome.WasPressedThisFrame()) return;
            int sent = GoHomeAll();
            Debug.Log($"[RH Aviator] order: go home; {sent} AI pilot(s) turning for home.", this);
        }

        /// <summary>Sends every AI pilot not in the player's jet home; returns how many went.</summary>
        public int GoHomeAll()
        {
            if (_runner == null) _runner = FindAnyObjectByType<AirWingRunner>();
            int player = _runner != null ? _runner.PlayerAircraftId : 0;
            int sent = 0;
            foreach (AviatorPilot pilot in FindObjectsByType<AviatorPilot>(FindObjectsInactive.Exclude))
            {
                if (pilot.GetComponent<AircraftView>().AircraftId == player) continue;
                if (pilot.GoHome()) sent++;
            }
            return sent;
        }
    }
}
