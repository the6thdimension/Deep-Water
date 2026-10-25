namespace DeepWater.AirWing.Core.Simulation
{
    /// <summary>
    /// The truth feed's ground (BLUEPRINT §3.1): the height of whatever surface lies under a point, the sea or a
    /// runway. The Unity side answers it from Sea State's <c>ISeaSurface</c>, never from HDRP water (AP11). Asked once
    /// per aircraft per tick, at the start of the tick.
    /// </summary>
    public interface ISurfaceTruth
    {
        float HeightAt(float x, float z);
    }

    /// <summary>A flat surface at one height: tests, and a calm-sea stand-in.</summary>
    public sealed class FlatSurface : ISurfaceTruth
    {
        readonly float _height;

        public FlatSurface(float heightM) => _height = heightM;

        public float HeightAt(float x, float z) => _height;
    }
}
