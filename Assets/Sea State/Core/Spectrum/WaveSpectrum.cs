using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// Ocean wave spectra as pure functions (Burst-compatible, double precision).
    ///
    /// Sources:
    /// - Pierson–Moskowitz / JONSWAP in the (Hs, Tp) form of DNV-RP-C205 §3.5.5, including the
    ///   σ = 0.07 / 0.09 width and the A_γ = 1 − 0.287 ln γ normalisation.
    /// - JONSWAP γ rule from Tp/√Hs: DNV-RP-C205 §3.5.5.5.
    /// - Directional spreading: cos-2s, D(Δθ) ∝ cos^{2s}(Δθ/2).
    ///
    /// All frequencies are angular (rad/s). Spectral density S(ω) in m²·s/rad.
    /// </summary>
    public static class WaveSpectrum
    {
        /// <summary>
        /// JONSWAP spectral density. γ = 1 reduces exactly to Pierson–Moskowitz.
        /// </summary>
        public static double Jonswap(double omega, double hs, double tp, double gamma)
        {
            if (omega <= 0.0 || hs <= 0.0 || tp <= 0.0) return 0.0;

            double wp = 2.0 * math.PI_DBL / tp;
            double ratio = wp / omega;
            double r2 = ratio * ratio;
            double pm = 5.0 / 16.0 * hs * hs * wp * wp * wp * wp / (omega * omega * omega * omega * omega)
                        * math.exp(-1.25 * r2 * r2);

            if (gamma <= 1.0) return pm;

            double sigma = omega <= wp ? 0.07 : 0.09;
            double d = (omega - wp) / (sigma * wp);
            double peak = math.pow(gamma, math.exp(-0.5 * d * d));
            return (1.0 - 0.287 * math.log(gamma)) * pm * peak;
        }

        /// <summary>
        /// JONSWAP peak-enhancement factor from the DNV-RP-C205 rule:
        /// γ = 5 for Tp/√Hs ≤ 3.6, γ = exp(5.75 − 1.15·Tp/√Hs) between, γ = 1 for Tp/√Hs ≥ 5.
        /// </summary>
        public static double DnvGamma(double hs, double tp)
        {
            if (hs <= 0.0) return 1.0;
            double c = tp / math.sqrt(hs);
            if (c <= 3.6) return 5.0;
            if (c >= 5.0) return 1.0;
            return math.exp(5.75 - 1.15 * c);
        }

        /// <summary>
        /// Unnormalised cos-2s spreading weight for an angle <paramref name="deltaRad"/> from the
        /// mean direction (|Δθ| ≤ π). Normalisation happens numerically in the sampler.
        /// </summary>
        public static double Cos2s(double deltaRad, double s)
        {
            double c = math.cos(0.5 * deltaRad);
            return c <= 0.0 ? 0.0 : math.pow(c, 2.0 * s);
        }

        /// <summary>Peak period of a fully developed (PM) sea of height Hs: Tp ≈ 4.99 √Hs.</summary>
        /// <remarks>From Hs = 0.21 U²/g and ωp = 0.877 g/U (Pierson &amp; Moskowitz 1964).</remarks>
        public static double PiersonMoskowitzPeakPeriod(double hs, double gravity)
        {
            if (hs <= 0.0) return 0.0;
            double windMs = math.sqrt(hs * gravity / 0.21);
            return 2.0 * math.PI_DBL * windMs / (0.877 * gravity);
        }

        /// <summary>Deep-water wavelength for period T: L = g T² / 2π.</summary>
        public static double DeepWaterWavelength(double periodS, double gravity)
            => gravity * periodS * periodS / (2.0 * math.PI_DBL);

        /// <summary>Deep-water angular frequency of a wave of length L: ω = √(2π g / L).</summary>
        public static double DeepWaterOmega(double wavelengthM, double gravity)
            => math.sqrt(2.0 * math.PI_DBL * gravity / wavelengthM);
    }
}
