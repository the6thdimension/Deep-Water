using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Operational role of a radar profile. Drives preset defaults and lets
    /// consumers reason about what a radar is for.
    /// </summary>
    public enum RadarRole
    {
        GroundSearch,
        AirSearch,
        NavalSurfaceSearch,
        FireControl,
        AirborneIntercept
    }

    /// <summary>
    /// SO-driven radar configuration (METHODOLOGY P1 / Pat1). One profile fully
    /// describes a radar fit: assign it to a RadarSuiteController (or use the
    /// RH Navy Sims &gt; Radar Suite editor commands) and the controller and its
    /// LOD modules configure themselves from it — no string parameters, no
    /// per-instance magic numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "RH Navy Sims/Radar Suite/Radar Profile", fileName = "RadarProfile")]
    public class RadarProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Operational role this profile represents")]
        public RadarRole role = RadarRole.GroundSearch;

        [Tooltip("Free-form notes (real-world analogue, intended platform, caveats)")]
        [TextArea]
        public string notes;

        [Header("Detection")]
        [Tooltip("Maximum detection range in meters — calibrated so a nominal 1 m²-class target is detectable out to this range")]
        [Min(10f)]
        public float maxDetectionRangeM = 5000f;

        [Tooltip("Relative radar power (affects how early strong/weak targets fade)")]
        [Range(0.1f, 10f)]
        public float radarPower = 1f;

        [Tooltip("Layers this radar can detect")]
        public LayerMask targetLayers = ~0;

        [Tooltip("Minimum 0–1 signal strength required for detection")]
        [Range(0.01f, 1f)]
        public float detectionThreshold = 0.1f;

        [Header("Scan")]
        [Tooltip("Beam width in degrees")]
        [Range(1f, 120f)]
        public float beamWidthDeg = 20f;

        [Tooltip("Antenna rotation rate in revolutions per minute")]
        [Range(0.5f, 60f)]
        public float rotationRpm = 10f;

        [Tooltip("Full 360° rotation; disable for a fixed sector scan")]
        public bool fullRotation = true;

        [Tooltip("Sector size in degrees when not in full rotation (body-relative)")]
        [Range(10f, 180f)]
        public float sectorSizeDeg = 90f;

        [Tooltip("Sector center bearing in degrees, relative to the platform's forward")]
        [Range(0f, 360f)]
        public float sectorCenterDeg = 0f;

        [Header("Elevation coverage")]
        [Tooltip("Lower elevation limit in degrees (negative looks down — airborne look-down). Stored in Phase 1, enforced by detection in Phase 2; gizmos draw it now.")]
        [Range(-90f, 90f)]
        public float minElevationDeg = 0f;

        [Tooltip("Upper elevation limit in degrees")]
        [Range(-90f, 90f)]
        public float maxElevationDeg = 60f;

        [Header("Timing & capacity")]
        [Tooltip("Seconds between detection scans")]
        [Range(0.01f, 1f)]
        public float updateInterval = 0.1f;

        [Tooltip("Maximum simultaneous targets")]
        [Range(10, 1000)]
        public int maxTargets = 100;

        [Header("Accuracy")]
        [Tooltip("Range measurement inaccuracy in meters at max range (lower is better)")]
        [Range(1f, 100f)]
        public float rangeAccuracyM = 10f;

        [Header("Fidelity")]
        [Tooltip("LOD module the radar activates with")]
        public RadarLOD defaultLOD = RadarLOD.LOD2_BasicRadar;

        /// <summary>Rotation rate converted to degrees per second.</summary>
        public float RotationDegPerSec => rotationRpm * 6f;

        /// <summary>
        /// Interim Phase-1 signal normalization for active (R⁴) modules — see
        /// RadarMath.NominalSignalScale.
        /// </summary>
        public float ActiveSignalScale =>
            RadarMath.NominalSignalScale(maxDetectionRangeM, radarPower, detectionThreshold, rangeExponent: 4);

        /// <summary>
        /// Interim Phase-1 signal normalization for passive (R²) reception.
        /// </summary>
        public float PassiveSignalScale =>
            RadarMath.NominalSignalScale(maxDetectionRangeM, radarPower, detectionThreshold, rangeExponent: 2);

        /// <summary>
        /// Apply this profile to a controller (and, through it, to every LOD
        /// module present on the same GameObject).
        /// </summary>
        public void ApplyTo(RadarSuiteController controller)
        {
            if (controller != null) controller.ApplyProfile(this);
        }
    }
}
