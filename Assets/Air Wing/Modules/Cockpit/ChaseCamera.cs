using UnityEngine;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Modules.Aircraft;

namespace DeepWater.AirWing.Modules.Cockpit
{
    /// <summary>
    /// The chase camera (W11), decoupled from the airframe. It sits behind and above the jet and turns with it, eased
    /// so a roll does not whip the horizon around. While the mouse instructor flies, it looks along the aim instead,
    /// so you point by moving the view (W15). Presentation only; it runs in LateUpdate after the view's interpolation.
    /// </summary>
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [Tooltip("Follow whichever aircraft the player flies (the seat cycle, W42); off, or with no player, follow Aircraft Id.")]
        [SerializeField] bool followPlayer = true;
        [SerializeField] int aircraftId = 1;
        [Tooltip("Distance behind the jet, metres.")]
        [SerializeField] float distanceM = 26f;
        [Tooltip("Height above the jet's centreline, metres.")]
        [SerializeField] float heightM = 6f;
        [Tooltip("How fast the view follows the jet's turn (per second).")]
        [SerializeField] float followRate = 4f;
        [Tooltip("How fast the view follows the mouse aim (per second).")]
        [SerializeField] float aimRate = 10f;

        AircraftView _view;
        int _resets;

        void LateUpdate()
        {
            if ((_view == null || _view.AircraftId != Target()) && !Find()) return;
            Transform jet = _view.transform;
            PlayerSeat seat = runner.PlayerSeat;
            bool aiming = seat != null && seat.Owner == DeviceSource.Mouse && _view.AircraftId == runner.PlayerAircraftId;

            Quaternion goal = aiming
                ? Quaternion.LookRotation(seat.AimDirection, Vector3.up)
                : Quaternion.LookRotation(jet.forward, jet.up);
            float rate = aiming ? aimRate : followRate;
            bool reset = _view.Aircraft.Resets != _resets;
            _resets = _view.Aircraft.Resets;
            transform.rotation = reset ? goal : Quaternion.Slerp(transform.rotation, goal, 1f - Mathf.Exp(-rate * Time.deltaTime));
            transform.position = jet.position - transform.forward * distanceM + transform.up * heightM;
        }

        int Target() => followPlayer && runner != null && runner.PlayerAircraftId != 0 ? runner.PlayerAircraftId : aircraftId;

        /// <summary>Finds the aircraft to follow, and snaps behind it (a seat change cuts, it does not swing across the sky).</summary>
        bool Find()
        {
            if (runner == null) return false;
            int id = Target();
            _view = null;
            foreach (AircraftView v in runner.Views)
                if (v.AircraftId == id && v.Aircraft != null) { _view = v; break; }
            if (_view == null) return false;
            _resets = _view.Aircraft.Resets;
            transform.rotation = _view.transform.rotation;
            return true;
        }
    }
}
