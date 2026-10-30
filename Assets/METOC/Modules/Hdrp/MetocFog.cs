using System;

namespace DeepWater.Metoc.Hdrp
{
    /// <summary>The fog METOC draws for a weather (E51), in HDRP's terms.</summary>
    public readonly struct MetocFogSettings
    {
        public readonly double MeanFreePathM, BaseHeightM, MaximumHeightM, MaxFogDistanceM;

        public MetocFogSettings(double meanFreePathM, double baseHeightM, double maximumHeightM, double maxFogDistanceM)
        {
            MeanFreePathM = meanFreePathM;
            BaseHeightM = baseHeightM;
            MaximumHeightM = maximumHeightM;
            MaxFogDistanceM = maxFogDistanceM;
        }
    }

    /// <summary>
    /// Visibility to HDRP fog (E51). Pure, so a test can pin it. The meteorological optical range is the distance
    /// at which a beam keeps 5 % (WMO), so the extinction is ln 20 / visibility and the mean free path its inverse.
    /// HDRP's fog is uniform below its base height and thins above it to 0.1 % at its maximum height; METOC puts the
    /// base at a share of the obscuration top (LOOK) and the maximum at the top.
    /// </summary>
    public static class MetocFog
    {
        public static readonly double Ln20 = Math.Log(20.0);

        public static double MeanFreePathM(double visibilityM) => visibilityM / Ln20;

        /// <param name="uniformFraction">LOOK: the share of the obscuration top that is uniform fog.</param>
        /// <param name="distanceFactor">LOOK: fog is computed out to this many visibilities.</param>
        /// <param name="minDistanceM">LOOK: but never less far than this, metres.</param>
        public static MetocFogSettings For(in MetocWeather w, double uniformFraction, double distanceFactor, double minDistanceM) =>
            new MetocFogSettings(
                Math.Max(1.0, MeanFreePathM(w.VisibilityM)),   // HDRP's minimum
                w.ObscurationTopM * uniformFraction,
                w.ObscurationTopM,
                Math.Max(minDistanceM, distanceFactor * w.VisibilityM));
    }
}
