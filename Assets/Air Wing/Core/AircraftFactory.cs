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

        static readonly PartLadder<AircraftProfile, IAirframe> AirframeLadder = new PartLadder<AircraftProfile, IAirframe>(PartKind.Airframe)
            .Register(FidelityLevel.L0, p => new AirframeL0(p.Airframe))                           // slice 1b
            .Register(FidelityLevel.L1, p => new AirframeL1(p.Airframe, p.AirframeL1));            // slice 2a, paired in 2b

        static readonly PartLadder<PowerplantProfile, IPowerplant> PowerplantLadder = new PartLadder<PowerplantProfile, IPowerplant>(PartKind.Powerplant)
            .Register(FidelityLevel.L0, p => new PowerplantL0(p));                                 // slice 1b

        static readonly PartLadder<AircraftProfile, IFlightControls> ControlsLadder = new PartLadder<AircraftProfile, IFlightControls>(PartKind.FlightControls)
            .Register(FidelityLevel.L0, p => new FlightControlsL0(p.Controls))                     // slice 1b
            .Register(FidelityLevel.L1, p => new FlightControlsL1(p));                             // slice 2b

        static readonly PartLadder<NoProfile, ILandingGear> GearLadder = new PartLadder<NoProfile, ILandingGear>(PartKind.LandingGear)
            .Register(FidelityLevel.L0, _ => new NoLandingGear());                                 // slice 1b (none)

        public static Aircraft Build(int id, in AircraftProfile profile, in FidelitySheet sheet, in AirframeState start)
        {
            profile.Validate();
            var levels = new List<FidelityResolution>(5);
            IAtmosphere atmosphere = AtmosphereLadder.Build(sheet.Level(PartKind.Atmosphere), default, out var r0); levels.Add(r0);
            ResolveAirframeAndControls(profile, sheet, out FidelityLevel airframeLevel, out FidelityLevel controlsLevel, out string pairing);
            IAirframe airframe = AirframeLadder.BuildAt(sheet.Level(PartKind.Airframe), airframeLevel, profile, pairing, out var r1); levels.Add(r1);
            IPowerplant powerplant = PowerplantLadder.Build(sheet.Level(PartKind.Powerplant), profile.Powerplant, out var r2); levels.Add(r2);
            IFlightControls controls = ControlsLadder.BuildAt(sheet.Level(PartKind.FlightControls), controlsLevel, profile, pairing, out var r3); levels.Add(r3);
            ILandingGear gear = GearLadder.Build(sheet.Level(PartKind.LandingGear), default, out var r4); levels.Add(r4);
            return new Aircraft(id, profile, atmosphere, airframe, powerplant, controls, gear, levels, start);
        }

        /// <summary>
        /// Airframe L1 flies the surfaces only Flight Controls L1 sets, and Flight Controls L1 sets surfaces only
        /// Airframe L1 flies: they come as a pair, from the type's L1 data. Anything else falls back to L0 for both and
        /// says why (C14).
        /// </summary>
        static void ResolveAirframeAndControls(in AircraftProfile profile, in FidelitySheet sheet,
                                               out FidelityLevel airframe, out FidelityLevel controls, out string reason)
        {
            airframe = AirframeLadder.Resolve(sheet.Level(PartKind.Airframe)).Resolved;
            controls = ControlsLadder.Resolve(sheet.Level(PartKind.FlightControls)).Resolved;
            reason = null;
            bool wantsL1 = airframe >= FidelityLevel.L1 || controls >= FidelityLevel.L1;
            if (!wantsL1) return;
            if (!profile.HasL1) reason = $"{profile.Name} has no L1 data";
            else if (airframe < FidelityLevel.L1 || controls < FidelityLevel.L1) reason = "Airframe L1 and Flight Controls L1 fly only as a pair";
            if (reason == null)
            {
                airframe = controls = FidelityLevel.L1;
                return;
            }
            airframe = controls = FidelityLevel.L0;
        }

        /// <summary>
        /// A start state for <paramref name="sheet"/>: <see cref="LevelStart(in AircraftProfile, Vector3, float, float, float, float)"/>,
        /// then, when the airframe resolves to L1, trimmed for level flight at the start's height in the ISA: the nose
        /// raised by the trim α and the stabilator at its trim (Flight Controls L1 starts there, its G command at 1).
        /// Thrust is left to the seat.
        /// </summary>
        public static AirframeState LevelStart(in AircraftProfile profile, in FidelitySheet sheet, Vector3 position, float headingDeg,
                                               float speedMps, float fuelKg, float throttle = 0f)
        {
            AirframeState s = LevelStart(profile, position, headingDeg, speedMps, fuelKg, throttle);
            ResolveAirframeAndControls(profile, sheet, out FidelityLevel airframe, out _, out _);
            if (airframe < FidelityLevel.L1 || speedMps < 30f) return s;
            AirData air = Isa.At(position.y);
            if (!AirframeL1.TrimLevel(profile.AirframeL1, speedMps, air.DensityKgM3, s.MassKg, out float alpha, out float stab, out _))
                throw new InvalidOperationException($"{profile.Name}: no level trim at {speedMps:F0} m/s and {position.y:F0} m (L1)");
            s.Attitude = Quaternion.Euler(-alpha * Units.RadToDeg, headingDeg, 0f);
            s.AngleOfAttack = alpha;
            s.Controls = new ControlState { StabilatorRad = stab, LoadFactorCommand = 1f };
            return s;
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
