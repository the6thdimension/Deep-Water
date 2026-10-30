using DeepWater.Metoc.Hdrp;
using NUnit.Framework;

namespace DeepWater.Metoc.Tests
{
    /// <summary>The exposure floor follows the reckoned light (E40), inside SIX's day floor and the night minimum.</summary>
    public class MetocExposureTests
    {
        const double Bias = 2, Min = -6, Day = 10.021231;

        [Test]
        public void TwoAndAHalfLux_MetersEvZero() => Assert.AreEqual(0.0, MetocExposure.MeteredEv(2.5), 1e-12);

        [Test]
        public void ByDay_TheFloorIsSixsDayFloor() => Assert.AreEqual(Day, MetocExposure.FloorEv(83600, Bias, Min, Day), 1e-12);

        [Test]
        public void UnderAFullMoon_TheFloorDropsToTheMeteredLightPlusTheBias()
        {
            // log2(0.27 / 2.5) + 2 = −3.2109 + 2 = −1.2109.
            Assert.AreEqual(-1.2109, MetocExposure.FloorEv(0.27, Bias, Min, Day), 1e-4);
        }

        [Test]
        public void OnAMoonlessNight_TheFloorStopsAtTheMinimum() => Assert.AreEqual(Min, MetocExposure.FloorEv(0.002, Bias, Min, Day), 1e-12);
    }
}
