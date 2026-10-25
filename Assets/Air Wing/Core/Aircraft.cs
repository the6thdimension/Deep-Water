using System.Collections.Generic;
using DeepWater.AirWing.Core.Airframe;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.FlightControls;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.LandingGear;
using DeepWater.AirWing.Core.Powerplant;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core
{
    /// <summary>
    /// One aircraft in the flight core: its profile, its parts at their resolved levels, and its state. Built by
    /// <see cref="AircraftFactory"/>. Pure C# (P6); stepped by <see cref="FlightCore"/> on the fixed tick.
    /// </summary>
    public sealed class Aircraft
    {
        public readonly int Id;
        public readonly AircraftProfile Profile;
        public readonly IAtmosphere Atmosphere;
        public readonly IAirframe Airframe;
        public readonly IPowerplant Powerplant;
        public readonly IFlightControls Controls;
        public readonly ILandingGear Gear;

        /// <summary>The level each part runs at, and any fallback (C14), for the log and the HUD.</summary>
        public readonly IReadOnlyList<FidelityResolution> Levels;

        public AirframeState State;

        internal Aircraft(int id, in AircraftProfile profile, IAtmosphere atmosphere, IAirframe airframe, IPowerplant powerplant,
                          IFlightControls controls, ILandingGear gear, IReadOnlyList<FidelityResolution> levels, in AirframeState start)
        {
            Id = id;
            Profile = profile;
            Atmosphere = atmosphere;
            Airframe = airframe;
            Powerplant = powerplant;
            Controls = controls;
            Gear = gear;
            Levels = levels;
            State = start;
        }

        /// <summary>
        /// One tick: <paramref name="clock"/>'s substeps of atmosphere → flight controls → powerplant → airframe, then
        /// the surface check after each substep. Meeting the surface with no gear that can take it is a crash, and a
        /// crash freezes the aircraft where it hit (AP23: every terminal verdict gets an end state).
        /// </summary>
        public void Step(in PilotInputs inputs, float surfaceHeightM, in SubstepClock clock)
        {
            if (State.Status != FlightStatus.Flying) return;
            float dt = (float)clock.SubstepSeconds;
            for (int k = 0; k < clock.Substeps; k++)
            {
                AirData air = Atmosphere.Sample(State.Position.y);
                ControlDemand demand = Controls.Demand(inputs, State, dt);
                float thrust = Powerplant.Step(inputs, air, ref State, dt);
                Airframe.Step(ref State, demand, thrust, air, inputs, dt);

                if (State.Position.y <= surfaceHeightM && !Gear.CanTouchDown)
                {
                    State.Position.y = surfaceHeightM;
                    State.Velocity = Vector3.zero;
                    State.BodyRates = Vector3.zero;
                    State.LoadFactor = 0f;
                    State.Status = FlightStatus.Crashed;
                    return;
                }
            }
        }
    }
}
