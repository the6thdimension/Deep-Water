using UnityEngine;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Articulated launcher: turret (yaw) + elevation arm (pitch) + muzzle anchor. The rig
    /// owns the *commanded* traverse and elevation angles; per LateUpdate it slews the actual
    /// transforms toward the command at a profile-configured rate, then writes the result to
    /// the turret and elevation Transforms.
    ///
    /// **Why rate-limited slew and not snap-to-command:** real launchers don't teleport to
    /// new angles — gimbals have max slew rates (typically 20–60 deg/s for shipboard launchers).
    /// The rate limit is what makes the visual realistic. Pull the stick hard right and the
    /// launcher slews smoothly to the right at its max rate; release and it holds position.
    ///
    /// **Conventions:**
    /// - Traverse: clockwise-positive when viewed from above. 0° = launcher's parent forward.
    /// - Elevation: 0° = horizontal. +30° = pointing 30° above horizontal. (We invert the
    ///   sign internally for Unity's Euler-X convention, which is opposite.)
    /// - Muzzle: an empty transform at the forward end of the rail. Missiles spawn from its
    ///   position + orientation. Aimed correctly means the muzzle's `forward` points where
    ///   you want the missile to go.
    /// </summary>
    public sealed class LauncherRig : MonoBehaviour
    {
        [Header("Rig parts")]
        [Tooltip("Transform rotated around its local Y for traverse (azimuth).")]
        [SerializeField] private Transform turret;

        [Tooltip("Transform rotated around its local X for elevation (pitch up/down). Parented to the turret.")]
        [SerializeField] private Transform elevation;

        [Tooltip("Anchor at the front of the launch rail. Missile spawns here with this orientation.")]
        [SerializeField] private Transform muzzle;

        [Header("Travel limits")]
        [SerializeField] private float minTraverseDeg = -180f;
        [SerializeField] private float maxTraverseDeg =  180f;
        [SerializeField] private float minElevationDeg = -5f;     // -5° = "depressed" (just below horizon)
        [SerializeField] private float maxElevationDeg = 85f;     // up to nearly vertical

        [Header("Slew rates")]
        [Tooltip("Max degrees-per-second the turret can traverse.")]
        [SerializeField] private float traverseRateDegPerSec = 60f;

        [Tooltip("Max degrees-per-second the elevation arm can move.")]
        [SerializeField] private float elevationRateDegPerSec = 40f;

        [Header("Initial pose")]
        [SerializeField] private float startTraverseDeg = 0f;
        [SerializeField] private float startElevationDeg = 10f;

        // -- Commanded vs actual state ------------------------------------
        public float TraverseDeg { get; private set; }
        public float ElevationDeg { get; private set; }
        public float CommandedTraverseDeg { get; private set; }
        public float CommandedElevationDeg { get; private set; }

        public Transform Muzzle => muzzle;

        // -- Public API ---------------------------------------------------
        /// <summary>Set the absolute commanded traverse + elevation. Values are clamped to travel limits.</summary>
        public void SetCommand(float traverseDeg, float elevationDeg)
        {
            CommandedTraverseDeg  = Mathf.Clamp(traverseDeg,  minTraverseDeg,  maxTraverseDeg);
            CommandedElevationDeg = Mathf.Clamp(elevationDeg, minElevationDeg, maxElevationDeg);
        }

        /// <summary>Add deltas to the commanded values (used for rate-based control from a stick).</summary>
        public void SlewCommand(float traverseDeltaDeg, float elevationDeltaDeg)
        {
            SetCommand(CommandedTraverseDeg + traverseDeltaDeg,
                       CommandedElevationDeg + elevationDeltaDeg);
        }

        /// <summary>Snap actual pose to commanded — skips the rate-limited slew. Useful for instant repositioning.</summary>
        public void SnapToCommand()
        {
            TraverseDeg = CommandedTraverseDeg;
            ElevationDeg = CommandedElevationDeg;
        }

        // -- Lifecycle ----------------------------------------------------
        private void Start()
        {
            CommandedTraverseDeg  = startTraverseDeg;
            CommandedElevationDeg = startElevationDeg;
            TraverseDeg  = startTraverseDeg;
            ElevationDeg = startElevationDeg;
            ApplyToTransforms();
        }

        private void LateUpdate()
        {
            // Rate-limit the actual pose toward the commanded pose.
            TraverseDeg  = Mathf.MoveTowards(TraverseDeg,  CommandedTraverseDeg,
                                             traverseRateDegPerSec  * Time.deltaTime);
            ElevationDeg = Mathf.MoveTowards(ElevationDeg, CommandedElevationDeg,
                                             elevationRateDegPerSec * Time.deltaTime);
            ApplyToTransforms();
        }

        private void ApplyToTransforms()
        {
            if (turret != null)
                turret.localEulerAngles = new Vector3(0f, TraverseDeg, 0f);
            if (elevation != null)
                // Negate: Unity's positive X-Euler tilts +Z forward axis DOWN. We want
                // positive ElevationDeg to point UP, so flip the sign.
                elevation.localEulerAngles = new Vector3(-ElevationDeg, 0f, 0f);
        }

        // Visualise the muzzle direction in the scene view so you can sight the launcher.
        private void OnDrawGizmos()
        {
            if (muzzle == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Gizmos.DrawRay(muzzle.position, muzzle.forward * 5f);
            Gizmos.DrawWireSphere(muzzle.position, 0.15f);
        }
    }
}
