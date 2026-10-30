using System;

namespace DeepWater.Metoc
{
    /// <summary>
    /// Visibility as extinction (E51, E54). The meteorological optical range is the distance at which a beam keeps 5 %
    /// (WMO), so a visibility V is an extinction of ln 20 / V. Extinctions add: rain's (per metre, from its rate) adds to
    /// the air's, and the effective visibility is ln 20 over the sum. Pure, so a test can pin it.
    /// </summary>
    public static class Visibility
    {
        /// <summary>ln 20: the optical depth at which a beam keeps 5 %.</summary>
        public static readonly double Ln20 = Math.Log(20.0);

        /// <summary>The extinction of the air for a visibility, per metre.</summary>
        public static double ExtinctionPerM(double visibilityM) => Ln20 / visibilityM;

        /// <summary>
        /// Rain's extinction of visible light, per metre: a·R^b per km for R mm/h (E54; the profile's constants). 0 when
        /// dry.
        /// </summary>
        public static double RainExtinctionPerM(double rainMmPerHour, IlluminationProfile profile) =>
            rainMmPerHour > 0 ? profile.RainExtinctionPerKm * Math.Pow(rainMmPerHour, profile.RainExtinctionExponent) / 1000.0 : 0.0;

        /// <summary>The visibility through the air and the rain together, metres. Exactly the air's when dry.</summary>
        public static double EffectiveM(double visibilityM, double rainExtinctionPerM) =>
            rainExtinctionPerM > 0 ? Ln20 / (ExtinctionPerM(visibilityM) + rainExtinctionPerM) : visibilityM;
    }
}
