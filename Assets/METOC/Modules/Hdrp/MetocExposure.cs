using System;

namespace DeepWater.Metoc.Hdrp
{
    /// <summary>
    /// The auto-exposure floor from the reckoned ground light (E40, E41). Pure, so a test can pin it; the renderer
    /// writes the result to the overlay volume's <c>limitMin</c>.
    /// </summary>
    public static class MetocExposure
    {
        /// <summary>
        /// An incident-light meter reads EV100 = log2(E · S / C) with S = 100 and C = 250 for a flat receptor
        /// (ISO 2720; from memory, verify): 2.5 lux meters EV 0.
        /// </summary>
        public const double LuxAtEv0 = 2.5;

        /// <summary>The metered EV100 of a ground illuminance in lux.</summary>
        public static double MeteredEv(double groundLux) => Math.Log(Math.Max(groundLux, 1e-9) / LuxAtEv0, 2.0);

        /// <summary>The floor: metered EV plus the bias, never below the minimum nor above the day floor.</summary>
        public static double FloorEv(double groundLux, double biasEv, double minimumEv, double dayFloorEv) =>
            Math.Min(dayFloorEv, Math.Max(minimumEv, MeteredEv(groundLux) + biasEv));
    }
}
