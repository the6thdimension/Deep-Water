using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.Airframe;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.FlightControls;
using DeepWater.AirWing.Core.LandingGear;
using DeepWater.AirWing.Core.Powerplant;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core
{
    /// <summary>
    /// Assembles an <see cref="Aircraft"/> from a profile and a fidelity sheet through each part's ladder (C14). The
    /// ladders are the one table of built levels: register a level here in the same commit that adds it and its tests.
    /// </summary>
    public static class AircraftFactory
    {
        /// <summary>The atmosphere takes no profile of its own.</summary>
        public readonly struct NoProfile { }

        static readonly PartLadder<NoProfile, IAtmosphere> AtmosphereLadder = new PartLadder<NoProfile, IAtmosphere>(PartKind.Atmosphere)
            .Register(FidelityLevel.L0, _ => new AtmosphereL0());                                  // slice 1b

        static readonly PartLadder<AirframeProfile, IAirframe> AirframeLadder = new PartLadder<AirframeProfile, IAirframe>(PartKind.Airframe)
            .Register(FidelityLevel.L0, p => new AirframeL0(p));                                   // slice 1b

        static readonly PartLadder<PowerplantProfile, IPowerplant> PowerplantLadder = new PartLadder<PowerplantProfile, IPowerplant>(PartKind.Powerplant)
            .Register(FidelityLevel.L0, p => new PowerplantL0(p));                                 // slice 1b

        static readonly PartLadder<ControlsProfile, IFlightControls> ControlsLadder = new PartLadder<ControlsProfile, IFlightControls>(PartKind.FlightControls)
            .Register(FidelityLevel.L0, p => new FlightControlsL0(p));                             // slice 1b

        static readonly PartLadder<NoProfile, ILandingGear> GearLadder = new PartLadder<NoProfile, ILandingGear>(PartKind.LandingGear)
            .Register(FidelityLevel.L0, _ => new NoLandingGear());                                 // slice 1b (none)

        public static Aircraft Build(int id, in AircraftProfile profile, in FidelitySheet sheet, in AirframeState start)
        {
            profile.Validate();
            var levels = new List<FidelityResolution>(5);
            IAtmosphere atmosphere = AtmosphereLadder.Build(sheet.Level(PartKind.Atmosphere), default, out var r0); levels.Add(r0);
            IAirframe airframe = AirframeLadder.Build(sheet.Level(PartKind.Airframe), profile.Airframe, out var r1); levels.Add(r1);
            IPowerplant powerplant = PowerplantLadder.Build(sheet.Level(PartKind.Powerplant), profile.Powerplant, out var r2); levels.Add(r2);
            IFlightControls controls = ControlsLadder.Build(sheet.Level(PartKind.FlightControls), profile.Controls, out var r3); levels.Add(r3);
            ILandingGear gear = GearLadder.Build(sheet.Level(PartKind.LandingGear), default, out var r4); levels.Add(r4);
            return new Aircraft(id, profile, atmosphere, airframe, powerplant, controls, gear, levels, start);
        }

        /// <summary>
        /// A start state: level flight at <paramref name="position"/>, heading <paramref name="headingDeg"/> (0 = +Z, 90 = +X),
        /// at <paramref name="speedMps"/>, carrying <paramref name="fuelKg"/>, at 1 g with the engine at <paramref name="throttle"/>.
        /// </summary>
        public static AirframeState LevelStart(in AircraftProfile profile, Vector3 position, float headingDeg, float speedMps,
                                               float fuelKg, float throttle = 0f)
        {
            if (!(speedMps >= 0f)) throw new ArgumentOutOfRangeException(nameof(speedMps), speedMps, "speed must be zero or positive");
            if (!(fuelKg >= 0f && fuelKg <= profile.Airframe.InternalFuelKg))
                throw new ArgumentOutOfRangeException(nameof(fuelKg), fuelKg, $"fuel must be 0 … {profile.Airframe.InternalFuelKg} kg");
            Quaternion attitude = Quaternion.Euler(0f, headingDeg, 0f);
            return new AirframeState
            {
                Position = position,
                Velocity = attitude * Vector3.forward * speedMps,
                Attitude = attitude,
                LoadFactor = 1f,
                MassKg = profile.Airframe.EmptyMassKg + fuelKg,
                FuelKg = fuelKg,
                EngineSpool = throttle,
                Status = FlightStatus.Flying,
            };
        }
    }
}
