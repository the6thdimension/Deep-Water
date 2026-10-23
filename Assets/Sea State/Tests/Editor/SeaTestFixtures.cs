using DeepWater.Sea.Core;

namespace DeepWater.Sea.Tests
{
    /// <summary>Reference sea states for the tests (values mirror the Douglas builder table).</summary>
    static class SeaTestFixtures
    {
        public const float Gravity = 9.81f;

        public static WaveSystemSpec WindSea(float hs, int count = 48, uint seed = 1005u, float choppiness = 1f)
        {
            return new WaveSystemSpec
            {
                SignificantHeightM = hs,
                PeakPeriodS = (float)WaveSpectrum.PiersonMoskowitzPeakPeriod(hs, Gravity),
                Gamma = 1f,
                SpreadingS = 10f,
                DirectionRad = 0.3f,
                Choppiness = choppiness,
                ComponentCount = count,
                Seed = seed,
            };
        }

        public static WaveSystemSpec Swell(float hs, float lengthM, int count = 16, uint seed = 2004u)
        {
            return new WaveSystemSpec
            {
                SignificantHeightM = hs,
                PeakPeriodS = (float)System.Math.Sqrt(2.0 * System.Math.PI * lengthM / Gravity),
                Gamma = 3.3f,
                SpreadingS = 50f,
                DirectionRad = -1.2f,
                Choppiness = 1f,
                ComponentCount = count,
                Seed = seed,
            };
        }

        /// <summary>Douglas 5 (Rough, Hs 3.25 m) wind sea, no swell.</summary>
        public static SeaBakeInput Douglas5(float minWavelengthM = 10f) => new SeaBakeInput
        {
            WindSea = WindSea(3.25f),
            Swell = default,
            MinWavelengthM = minWavelengthM,
            Gravity = Gravity,
            WhitecapFraction = 0.022f,
            FoamCalibrationGrid = 32,
        };

        /// <summary>Douglas 6 (Very rough, Hs 5 m) plus a moderate average swell.</summary>
        public static SeaBakeInput Douglas6WithSwell() => new SeaBakeInput
        {
            WindSea = WindSea(5f, seed: 1006u),
            Swell = Swell(3f, 150f),
            MinWavelengthM = 10f,
            Gravity = Gravity,
            WhitecapFraction = 0.09f,
            FoamCalibrationGrid = 32,
        };
    }
}
