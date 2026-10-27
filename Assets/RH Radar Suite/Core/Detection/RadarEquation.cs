using System;

namespace RHRadarSuite.Detection
{
    /// <summary>
    /// The terms of one radar's detection (ROADMAP Phase 2, Skolnik): what the radar puts out and how it listens, in
    /// the units a radar engineer quotes. A game model built on the textbook monostatic radar equation; every number
    /// that describes a particular radar comes from its profile (P1), none from here.
    /// </summary>
    public struct RadarTerms
    {
        /// <summary>Peak transmitted power, watts.</summary>
        public double PeakPowerW;
        /// <summary>Antenna gain, dBi (the same antenna transmits and receives).</summary>
        public double GainDbi;
        /// <summary>Carrier frequency, hertz.</summary>
        public double FrequencyHz;
        /// <summary>Receiver noise bandwidth, hertz.</summary>
        public double BandwidthHz;
        /// <summary>Receiver noise figure, dB.</summary>
        public double NoiseFigureDb;
        /// <summary>System losses, dB (positive).</summary>
        public double LossesDb;
        /// <summary>Pulses integrated coherently per look (1: a single pulse).</summary>
        public int PulsesIntegrated;
    }

    /// <summary>
    /// The radar equation and the detection statistics, pure C# (P6), double precision, in dB where radar people
    /// work in dB. Public textbook relations only:
    /// <list type="bullet">
    /// <item>SNR = Pt G² λ² σ n / ((4π)³ k T₀ B F L R⁴) (Skolnik, the monostatic radar equation, coherent integration n).</item>
    /// <item>The SNR needed for a probability of detection and of false alarm: Albersheim's approximation
    /// (non-fluctuating target, linear detector; good to about 0.2 dB for 10⁻⁷ ≤ Pfa ≤ 10⁻³, 0.1 ≤ Pd ≤ 0.9).</item>
    /// <item>The probability of detection at an SNR: North's approximation for a steady target; the exact closed form
    /// Pd = Pfa^(1/(1+SNR)) for a Swerling 1 (slowly fluctuating) target, single look.</item>
    /// <item>The radar horizon over a smooth Earth with standard refraction (4/3 Earth radius).</item>
    /// </list>
    /// </summary>
    public static class RadarEquation
    {
        public const double Boltzmann = 1.380649e-23;       // J/K, exact (SI 2019)
        public const double ReferenceTempK = 290.0;         // T₀, the standard noise reference
        public const double SpeedOfLight = 299792458.0;     // m/s, exact
        public const double EarthRadiusM = 6371000.0;       // mean
        public const double StandardRefraction = 4.0 / 3.0; // effective Earth radius factor

        public static double ToDb(double ratio) => 10.0 * Math.Log10(ratio);
        public static double FromDb(double db) => Math.Pow(10.0, db / 10.0);

        public static double WavelengthM(double frequencyHz) => SpeedOfLight / frequencyHz;

        /// <summary>The SNR, dB, of a target of <paramref name="rcsDbsm"/> at <paramref name="rangeM"/>.</summary>
        public static double SnrDb(in RadarTerms r, double rcsDbsm, double rangeM)
        {
            Check(r);
            if (!(rangeM > 0.0)) throw new ArgumentOutOfRangeException(nameof(rangeM), rangeM, "range must be positive");
            double lambda = WavelengthM(r.FrequencyHz);
            double num = ToDb(r.PeakPowerW) + 2.0 * r.GainDbi + ToDb(lambda * lambda) + rcsDbsm + ToDb(Math.Max(1, r.PulsesIntegrated));
            double den = 3.0 * ToDb(4.0 * Math.PI) + ToDb(Boltzmann * ReferenceTempK * r.BandwidthHz) + r.NoiseFigureDb + r.LossesDb + 4.0 * ToDb(rangeM);
            return num - den;
        }

        /// <summary>The range, metres, at which a target of <paramref name="rcsDbsm"/> gives <paramref name="requiredSnrDb"/>.</summary>
        public static double RangeForSnrM(in RadarTerms r, double rcsDbsm, double requiredSnrDb)
        {
            // SNR falls 40 dB per decade of range: solve SnrDb(R) = required from SnrDb(1 m).
            double at1m = SnrDb(r, rcsDbsm, 1.0);
            return Math.Pow(10.0, (at1m - requiredSnrDb) / 40.0);
        }

        /// <summary>Albersheim: the SNR, dB, for <paramref name="pd"/> and <paramref name="pfa"/> with <paramref name="pulses"/> integrated (steady target).</summary>
        public static double RequiredSnrDb(double pd, double pfa, int pulses = 1)
        {
            if (!(pd > 0.0 && pd < 1.0)) throw new ArgumentOutOfRangeException(nameof(pd), pd, "0 < Pd < 1");
            if (!(pfa > 0.0 && pfa < 1.0)) throw new ArgumentOutOfRangeException(nameof(pfa), pfa, "0 < Pfa < 1");
            int n = Math.Max(1, pulses);
            double a = Math.Log(0.62 / pfa), b = Math.Log(pd / (1.0 - pd));
            return -5.0 * Math.Log10(n) + (6.2 + 4.54 / Math.Sqrt(n + 0.44)) * Math.Log10(a + 0.12 * a * b + 1.7 * b);
        }

        /// <summary>North: the probability of detecting a steady target at <paramref name="snrDb"/> with <paramref name="pfa"/>.</summary>
        public static double PdSteady(double snrDb, double pfa)
        {
            double x = FromDb(snrDb);
            return 0.5 * Erfc(Math.Sqrt(-Math.Log(pfa)) - Math.Sqrt(x + 0.5));
        }

        /// <summary>Swerling 1, one look: Pd = Pfa^(1/(1+SNR)), exact.</summary>
        public static double PdSwerling1(double snrDb, double pfa) => Math.Pow(pfa, 1.0 / (1.0 + FromDb(snrDb)));

        /// <summary>
        /// The radar horizon, metres: the farthest a target at <paramref name="targetHeightM"/> can be seen from an
        /// antenna at <paramref name="antennaHeightM"/> over a smooth Earth with standard refraction (heights above the
        /// surface; negative heights count as zero).
        /// </summary>
        public static double HorizonM(double antennaHeightM, double targetHeightM)
        {
            double re = StandardRefraction * EarthRadiusM;
            return Math.Sqrt(2.0 * re * Math.Max(0.0, antennaHeightM)) + Math.Sqrt(2.0 * re * Math.Max(0.0, targetHeightM));
        }

        /// <summary>Complementary error function (Numerical Recipes' Chebyshev fit; fractional error under 1.2e-7).</summary>
        public static double Erfc(double x)
        {
            double z = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.5 * z);
            double ans = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
                         t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 +
                         t * (-0.82215223 + t * 0.17087277)))))))));
            return x >= 0.0 ? ans : 2.0 - ans;
        }

        static void Check(in RadarTerms r)
        {
            if (!(r.PeakPowerW > 0.0)) throw new ArgumentOutOfRangeException(nameof(r.PeakPowerW), r.PeakPowerW, "power must be positive");
            if (!(r.FrequencyHz > 0.0)) throw new ArgumentOutOfRangeException(nameof(r.FrequencyHz), r.FrequencyHz, "frequency must be positive");
            if (!(r.BandwidthHz > 0.0)) throw new ArgumentOutOfRangeException(nameof(r.BandwidthHz), r.BandwidthHz, "bandwidth must be positive");
        }
    }
}
