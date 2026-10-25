namespace DeepWater.AirWing.Core.LandingGear
{
    /// <summary>The Landing Gear part (BLUEPRINT §4). Struts, wheels and brakes through PhysX contacts arrive at L1.</summary>
    public interface ILandingGear
    {
        /// <summary>Whether the aircraft can meet a surface without crashing.</summary>
        bool CanTouchDown { get; }
    }

    /// <summary>Landing Gear L0: none. L0 is airborne only (W29), so meeting any surface is a crash.</summary>
    public sealed class NoLandingGear : ILandingGear
    {
        public bool CanTouchDown => false;
    }
}
