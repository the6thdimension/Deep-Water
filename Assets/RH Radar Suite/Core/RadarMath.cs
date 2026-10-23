using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Pure static radar geometry and calibration helpers. No MonoBehaviour state,
    /// no Unity lifecycle — the P6 seed for the Phase 2 pure-C# detection core.
    /// All angles are degrees; bearings are 0–360 clockwise from the platform's
    /// flattened forward (body-relative), NOT world north.
    /// </summary>
    public static class RadarMath
    {
        /// <summary>
        /// The platform's forward direction projected onto the horizontal plane.
        /// Falls back to world forward for a straight-up/straight-down platform.
        /// </summary>
        public static Vector3 FlatForward(Transform t)
        {
            Vector3 f = t.forward;
            f.y = 0f;
            return f.sqrMagnitude < 1e-8f ? Vector3.forward : f.normalized;
        }

        /// <summary>
        /// 0–360 bearing of a direction relative to the given flattened forward.
        /// </summary>
        public static float BodyRelativeBearing(Vector3 flatForward, Vector3 toTarget)
        {
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-8f) return 0f;
            float a = Vector3.SignedAngle(flatForward, toTarget.normalized, Vector3.up);
            return (a + 360f) % 360f;
        }

        /// <summary>
        /// True if a bearing lies inside a sector. Handles the 0/360 wrap.
        /// </summary>
        public static bool IsAngleInSector(float angleDeg, float sectorCenterDeg, float sectorSizeDeg)
        {
            return Mathf.Abs(Mathf.DeltaAngle(sectorCenterDeg, angleDeg)) <= sectorSizeDeg * 0.5f;
        }

        /// <summary>
        /// World-space direction for a body-relative bearing on the given platform.
        /// </summary>
        public static Vector3 BodyDirection(Transform t, float bodyAngleDeg)
        {
            return Quaternion.AngleAxis(bodyAngleDeg, Vector3.up) * FlatForward(t);
        }

        /// <summary>
        /// World-space direction for a body-relative bearing and elevation
        /// (positive elevation is up).
        /// </summary>
        public static Vector3 BodyDirection(Transform t, float bodyAngleDeg, float elevationDeg)
        {
            Vector3 flat = BodyDirection(t, bodyAngleDeg);
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            return Quaternion.AngleAxis(-elevationDeg, right) * flat;
        }

        /// <summary>
        /// Interim Phase-1 calibration for the modules' 0–1 signal abstraction.
        /// Chooses the normalization scale so that a nominal 1 m²-class target at
        /// <paramref name="maxRangeM"/> returns <paramref name="margin"/> × the
        /// detection threshold — i.e. "max range" actually behaves as the range
        /// knob. Replaced by the real radar equation in Phase 2 (see ROADMAP).
        /// <paramref name="rangeExponent"/> is 4 for active radar (R⁴ two-way
        /// spreading), 2 for passive one-way reception.
        /// </summary>
        public static float NominalSignalScale(
            float maxRangeM, float power, float detectionThreshold,
            int rangeExponent = 4, float margin = 4f)
        {
            maxRangeM = Mathf.Max(1f, maxRangeM);
            double rTerm = 1.0;
            for (int i = 0; i < rangeExponent; i++) rTerm *= maxRangeM;
            double scale = detectionThreshold * margin * rTerm / Mathf.Max(0.0001f, power);
            return (float)scale;
        }
    }
}
