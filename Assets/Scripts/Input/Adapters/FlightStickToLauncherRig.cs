using UnityEngine;
using GuidedFury.Examples;

namespace DeepWater.Input.Adapters
{
    /// <summary>
    /// Drives a <see cref="LauncherRig"/> from the VelocityOne. Rate control: stick
    /// deflection at 1.0 = full slew rate; idle = launcher holds position. Trigger fire
    /// still goes through <see cref="FlightStickToTestRunner"/> — this adapter only aims.
    ///
    /// **Mapping:**
    /// - Stick pitch (Y) → elevation rate (pull back = elevate up).
    /// - Stick roll (X) → traverse rate (push right = slew right).
    ///
    /// **Why rate and not absolute position:** absolute position means letting go of the
    /// stick snaps the launcher back to neutral — annoying for an operator who's lined up
    /// on a target. Rate control lets you nudge, then hold position.
    /// </summary>
    public sealed class FlightStickToLauncherRig : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private FlightStickInput reader;

        [Header("Target")]
        [Tooltip("If null, finds the first LauncherRig in the scene.")]
        [SerializeField] private LauncherRig rig;

        [Header("Tuning")]
        [Tooltip("Traverse slew rate (deg/sec) at full stick deflection. Below this, scaled by stick value.")]
        [SerializeField] private float traverseRateDegPerSec = 45f;

        [Tooltip("Elevation slew rate (deg/sec) at full stick deflection.")]
        [SerializeField] private float elevationRateDegPerSec = 30f;

        [Tooltip("Deadzone applied to the stick input — avoids unintended creep from sensor noise.")]
        [Range(0f, 0.2f)] [SerializeField] private float deadzone = 0.07f;

        private void OnEnable()
        {
            if (reader == null) reader = FindFirstObjectByType<FlightStickInput>();
            if (rig == null)    rig    = FindFirstObjectByType<LauncherRig>();
        }

        private void Update()
        {
            if (reader == null || rig == null) return;

            float pitchInput = ApplyDeadzone(reader.Pitch);
            float rollInput  = ApplyDeadzone(reader.Roll);

            float dt = Time.deltaTime;
            // Pitch (+1 = pulled back, user wants nose up) → positive elevation delta.
            // Roll  (+1 = pushed right)                    → positive traverse delta.
            rig.SlewCommand(
                traverseDeltaDeg:  rollInput  * traverseRateDegPerSec  * dt,
                elevationDeltaDeg: pitchInput * elevationRateDegPerSec * dt);
        }

        private float ApplyDeadzone(float v)
        {
            if (Mathf.Abs(v) < deadzone) return 0f;
            // Rescale so motion is smooth past the deadzone (no jump from 0 to deadzone value).
            return Mathf.Sign(v) * (Mathf.Abs(v) - deadzone) / (1f - deadzone);
        }
    }
}
