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
    /// Visibility to HDRP fog (E51, E54). Pure, so a test can pin it. The extinction is ln 20 / visibility
    /// (<see cref="Visibility"/>) and the mean free path its inverse. HDRP's fog is uniform below its base height and
    /// thins above it to 0.1 % at its maximum height; METOC puts the base at a share of the top (LOOK) and the maximum
    /// at the top.
    /// <para>
    /// The fog draws the effective visibility, the air's and the rain's together. The top is the obscuration top, or the
    /// cloud base when it rains and the base is higher, since rain fills the air below the cloud. HDRP has one fog
    /// profile, so a shallow fog bank under rain is drawn as deep as the rain: an approximation, said here.
    /// </para>
    /// </summary>
    public static class MetocFog
    {
        public static double MeanFreePathM(double visibilityM) => visibilityM / Visibility.Ln20;

        /// <summary>The top of the drawn fog, metres: the obscuration top, raised to the cloud base under rain.</summary>
        public static double TopM(in MetocWeather w) => w.Raining ? Math.Max(w.ObscurationTopM, w.CloudBaseM) : w.ObscurationTopM;

        /// <param name="effectiveVisibilityM">The report's visibility through the air and the rain, metres.</param>
        /// <param name="uniformFraction">LOOK: the share of the top that is uniform fog.</param>
        /// <param name="distanceFactor">LOOK: fog is computed out to this many visibilities.</param>
        /// <param name="minDistanceM">LOOK: but never less far than this, metres.</param>
        public static MetocFogSettings For(in MetocWeather w, double effectiveVisibilityM, double uniformFraction, double distanceFactor,
            double minDistanceM)
        {
            double top = TopM(w);
            return new MetocFogSettings(
                Math.Max(1.0, MeanFreePathM(effectiveVisibilityM)),   // HDRP's minimum
                top * uniformFraction,
                top,
                Math.Max(minDistanceM, distanceFactor * effectiveVisibilityM));
        }

        /// <summary>
        /// How much rain darkens the drawn clouds, 0 to <paramref name="maxDim"/> (E55, LOOK): maxDim · R / (R + R½), half
        /// of it at <paramref name="halfRateMmPerHour"/>. The reckoned light does not change (Kasten &amp; Czeplak on cover).
        /// </summary>
        public static double CloudDim(double rainMmPerHour, double maxDim, double halfRateMmPerHour) =>
            rainMmPerHour > 0 ? maxDim * rainMmPerHour / (rainMmPerHour + halfRateMmPerHour) : 0.0;
    }
}
