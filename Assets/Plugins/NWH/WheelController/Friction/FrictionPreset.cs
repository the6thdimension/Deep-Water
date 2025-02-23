using System;
using UnityEngine;

namespace NWH.WheelController3D
{
    /// <summary>
    ///     ScriptableObject holding friction settings for one surface type.
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "NWH Vehicle Physics", menuName = "NWH Vehicle Physics/Friction Preset", order = 1)]
    public class FrictionPreset : ScriptableObject
    {
        public const int LUT_RESOLUTION = 1000;

        /// <summary>
        ///     B, C, D and E parameters of short version of Pacejka's magic formula.
        /// </summary>
        [Tooltip("    B, C, D and E parameters of short version of Pacejka's magic formula.")]
        public Vector4 BCDE;

        /// <summary>
        /// Slip at which the friction preset has highest friction.
        /// </summary>
        [UnityEngine.Tooltip("Slip at which the friction preset has highest friction.")]
        public float peakSlip = 0.12f;

        [SerializeField]
        private AnimationCurve _curve;

        public AnimationCurve Curve
        {
            get { return _curve; }
        }

        /// <summary>
        /// Gets the slip at which the friction is the highest for this friction curve.
        /// </summary>
        /// <returns></returns>
        public float GetPeakSlip()
        {
            float peakSlip = -1;
            float yMax = 0;

            for (float i = 0; i < 1f; i += 0.01f)
            {
                float y = _curve.Evaluate(i);
                if (y > yMax)
                {
                    yMax = y;
                    peakSlip = i;
                }
            }

            return peakSlip;
        }


        /// <summary>
        ///     Generate Curve from B,C,D and E parameters of Pacejka's simplified magic formula
        /// </summary>
        public void UpdateFrictionCurve()
        {
            _curve = new AnimationCurve();
            Keyframe[] frames = new Keyframe[20];
            int        n      = frames.Length;
            float      t      = 0;

            for (int i = 0; i < n; i++)
            {
                float v = GetFrictionValue(t, BCDE);
                _curve.AddKey(t, v);

                if (i <= 10)
                {
                    t += 0.02f;
                }
                else
                {
                    t += 0.1f;
                }
            }

            for (int i = 0; i < n; i++)
            {
                _curve.SmoothTangents(i, 0f);
            }

            peakSlip = GetPeakSlip();
        }


        private static float GetFrictionValue(float slip, Vector4 p)
        {
            float B = p.x;
            float C = p.y;
            float D = p.z;
            float E = p.w;
            float t = Mathf.Abs(slip);
            return D * Mathf.Sin(C * Mathf.Atan(B * t - E * (B * t - Mathf.Atan(B * t))));
        }
    }
}