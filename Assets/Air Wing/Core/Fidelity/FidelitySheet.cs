using System;

namespace DeepWater.AirWing.Core.Fidelity
{
    /// <summary>
    /// The baked fidelity sheet (W25, C7): which level each part asks for, how many substeps a tick takes, and the
    /// seed. Baked once from a <c>FidelitySheetSO</c> when a run starts; editing the asset mid-run changes nothing.
    /// </summary>
    public readonly struct FidelitySheet
    {
        readonly FidelityLevel _airframe, _powerplant, _flightControls, _landingGear, _atmosphere;

        public readonly int SubstepsPerTick;
        public readonly ulong Seed;

        public FidelitySheet(FidelityLevel airframe, FidelityLevel powerplant, FidelityLevel flightControls,
                             FidelityLevel landingGear, FidelityLevel atmosphere, int substepsPerTick, ulong seed)
        {
            if (substepsPerTick < 1 || substepsPerTick > Simulation.SubstepClock.MaxSubsteps)
                throw new ArgumentOutOfRangeException(nameof(substepsPerTick), substepsPerTick,
                    $"substeps per tick must be 1 … {Simulation.SubstepClock.MaxSubsteps}");
            _airframe = airframe;
            _powerplant = powerplant;
            _flightControls = flightControls;
            _landingGear = landingGear;
            _atmosphere = atmosphere;
            SubstepsPerTick = substepsPerTick;
            Seed = seed;
        }

        public FidelityLevel Level(PartKind part)
        {
            switch (part)
            {
                case PartKind.Airframe: return _airframe;
                case PartKind.Powerplant: return _powerplant;
                case PartKind.FlightControls: return _flightControls;
                case PartKind.LandingGear: return _landingGear;
                case PartKind.Atmosphere: return _atmosphere;
                default: throw new ArgumentOutOfRangeException(nameof(part), part, null);
            }
        }
    }
}
