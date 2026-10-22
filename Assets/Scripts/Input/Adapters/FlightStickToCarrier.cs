using UnityEngine;
using CarrierOps.Core.Carrier;

namespace DeepWater.Input.Adapters
{
    /// <summary>
    /// Throttle lever → ship speed. Yaw axis (or secondary throttle) → rudder.
    ///
    /// Reads from <see cref="FlightStickInput"/> in Update, pushes the values to the
    /// nearest <see cref="CarrierBehaviour"/>'s helm setters. Mapping is configurable:
    /// - <see cref="useThrottleSecondaryForRudder"/> = false (default): twist (yaw) axis is
    ///   the rudder — centered, natural for "no rudder = straight ahead".
    /// - true: uses the second throttle lever as rudder. Less natural (a lever's "rest"
    ///   end maps to "full rudder one way").
    ///
    /// **Throttle range:** the action map's `Normalize(min=-1,max=1,zero=-1)` processor
    /// already maps the lever from [-1..+1] to [0..1]. We pass through. <see cref="invertThrottle"/>
    /// flips it (1 → 0) if your physical lever is mounted the opposite way.
    /// </summary>
    public sealed class FlightStickToCarrier : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private FlightStickInput reader;

        [Header("Target")]
        [Tooltip("If null, the first CarrierBehaviour in the scene is used.")]
        [SerializeField] private CarrierBehaviour carrier;

        [Header("Mapping")]
        [Tooltip("Use ThrottleSecondary lever as the rudder. If false (default), uses the stick's yaw (twist) axis — more natural for rudder.")]
        [SerializeField] private bool useThrottleSecondaryForRudder = false;

        [Tooltip("Invert the throttle reading (some lever orientations report 1 at idle and 0 at full).")]
        [SerializeField] private bool invertThrottle = false;

        [Tooltip("Deadzone applied around centre after remapping. Prevents tiny stick noise from creeping the ship around.")]
        [Range(0f, 0.2f)] [SerializeField] private float deadzone = 0.05f;

        private void OnEnable()
        {
            if (reader == null)  reader  = FindFirstObjectByType<FlightStickInput>();
            if (carrier == null) carrier = FindFirstObjectByType<CarrierBehaviour>();
        }

        private void Update()
        {
            if (reader == null || carrier == null) return;

            // Throttle: the action map's Normalize(min=-1,max=1,zero=-1) processor has already
            // mapped the lever from [-1..+1] to [0..1]. Just pass through (with optional invert).
            float throttle01 = Mathf.Clamp01(reader.ThrottleMain);
            if (invertThrottle) throttle01 = 1f - throttle01;
            if (throttle01 < deadzone) throttle01 = 0f;

            float rudder = useThrottleSecondaryForRudder
                ? reader.ThrottleSecondary
                : reader.Yaw;
            if (Mathf.Abs(rudder) < deadzone) rudder = 0f;

            carrier.SetThrottle(throttle01);
            carrier.SetRudder(Mathf.Clamp(rudder, -1f, 1f));
        }
    }
}
