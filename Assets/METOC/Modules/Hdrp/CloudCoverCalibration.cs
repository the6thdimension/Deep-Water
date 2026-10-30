using System;
using UnityEngine;

namespace DeepWater.Metoc.Hdrp
{
    /// <summary>
    /// How much of the sky HDRP's volumetric clouds cover at each <c>shapeFactor</c> (E48), measured by
    /// <b>Deep Water → METOC → Calibrate Cloud Cover</b>: five cube-face views of the upper hemisphere, clouds on
    /// against clouds off at a fixed exposure, solid-angle weighted, several cloud offsets per setting. The renderer
    /// inverts it to draw the reckoned oktas. LOOK: it changes the picture, never the report.
    /// </summary>
    [Serializable]
    public sealed class CloudCoverCalibration
    {
        [Tooltip("The swept shapeFactor values, strictly descending (higher means less cloud).")]
        public float[] shapeFactor = Array.Empty<float>();
        [Tooltip("The measured cover, 0–1 of the upper hemisphere: the mean over the offsets.")]
        public float[] coverMean = Array.Empty<float>();
        [Tooltip("The least and most cover over the offsets: the spread a single view can show.")]
        public float[] coverMin = Array.Empty<float>(), coverMax = Array.Empty<float>();
        [TextArea(3, 10)]
        [Tooltip("How, when and at what the table was measured.")]
        public string provenance = "";

        /// <summary>Null when the table can be inverted, else why not.</summary>
        public string Problem()
        {
            if (shapeFactor == null || coverMean == null || shapeFactor.Length < 2) return "fewer than two measured points";
            if (coverMean.Length != shapeFactor.Length) return $"{shapeFactor.Length} shapeFactors but {coverMean.Length} covers";
            for (int i = 1; i < shapeFactor.Length; i++)
                if (!(shapeFactor[i] < shapeFactor[i - 1])) return $"shapeFactor is not strictly descending at point {i}";
            for (int i = 0; i < coverMean.Length; i++)
                if (!(coverMean[i] >= 0 && coverMean[i] <= 1)) return $"cover {coverMean[i]} at point {i} is outside 0–1";
            return null;
        }

        /// <summary>The shapeFactor that draws <paramref name="coverFraction"/> of the sky.</summary>
        public float ShapeFactorFor(double coverFraction) => Solve(shapeFactor, coverMean, coverFraction);

        /// <summary>
        /// Inverts a measured table, piecewise linear. The points run from high shapeFactor (little cloud) to low. The
        /// measured cover can dip from noise, so the inversion walks its running maximum, which is monotone; a target
        /// outside the measured range takes the nearest end.
        /// </summary>
        public static float Solve(float[] shapeFactor, float[] cover, double target)
        {
            float envelope = cover[0];
            if (target <= envelope) return shapeFactor[0];
            for (int i = 1; i < shapeFactor.Length; i++)
            {
                float next = Math.Max(envelope, cover[i]);
                if (target <= next)
                {
                    double t = (target - envelope) / (next - envelope);   // next > envelope here, since target > envelope
                    return (float)(shapeFactor[i - 1] + (shapeFactor[i] - shapeFactor[i - 1]) * t);
                }
                envelope = next;
            }
            return shapeFactor[shapeFactor.Length - 1];
        }
    }
}
