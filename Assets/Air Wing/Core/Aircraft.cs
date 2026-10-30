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

        /// <summary>The state the aircraft was built with; <see cref="PilotSwitches.Reset"/> returns it here.</summary>
        public readonly AirframeState Start;

        /// <summary>
        /// The IFF code its transponder answers in (SENSORS-CONCEPT J5); 0: none, it never answers. Set by whoever runs the
        /// fight (<see cref="FlightCore.SetIffCode"/>): the core knows codes, not sides.
        /// </summary>
        public int IffCode { get; internal set; }

        /// <summary>How many times the aircraft has been reset (presentation watches it to snap, not sweep).</summary>
        public int Resets { get; private set; }

        /// <summary>The Pilot Inputs of the last tick stepped (for presentation: the HUD and moving surfaces).</summary>
        public PilotInputs LastInputs { get; private set; }

        /// <summary>Why the last flight ended, or null while flying.</summary>
        public string CrashReason { get; private set; }

        /// <summary>Who shot it down (Gunnery K5), or 0.</summary>
        public int KilledBy { get; private set; }

        /// <summary>
        /// Shot down (Gunnery K5): it leaves the fight and falls, no longer flown, until it meets the surface and is
        /// Crashed. Only a flying aircraft can be killed; a reset brings it back.
        /// </summary>
        public void Kill(int by, string reason)
        {
            if (State.Status != FlightStatus.Flying) return;
            State.Status = FlightStatus.Killed;
            State.LoadFactor = 0f;
            State.BodyRates = Vector3.zero;
            KilledBy = by;
            CrashReason = reason;
        }

        /// <summary>
        /// Back in the fight at <paramref name="at"/> (the arena's respawn, W59): whatever it was doing, it flies from
        /// there with the rounds and hit points <paramref name="at"/> carries. Counted as a reset, so presentation snaps.
        /// </summary>
        internal void Respawn(in AirframeState at)
        {
            State = at;
            Resets++;
            CrashReason = null;
            KilledBy = 0;
        }

        /// <summary>A killed aircraft's fall: gravity and a crude drag that bleeds its speed; the nose follows the path.</summary>
        void Fall(float surfaceHeightM, float dt)
        {
            const float g = 9.80665f, bleedPerS = 0.05f;   // AUTHORED: presentation-grade; a wreck's aero is not modelled
            State.Velocity = State.Velocity * Mathf.Max(0f, 1f - bleedPerS * dt) + Vector3.down * (g * dt);
            State.Position += State.Velocity * dt;
            if (State.Velocity.sqrMagnitude > 1f) State.Attitude = Quaternion.LookRotation(State.Velocity, State.Attitude * Vector3.up);
            if (State.Position.y <= surfaceHeightM)
            {
                State.Position.y = surfaceHeightM;
                Crash(CrashReason + "; fell and met the surface");
            }
        }

        void Crash(string reason)
        {
            State.Velocity = Vector3.zero;
            State.BodyRates = Vector3.zero;
            State.LoadFactor = 0f;
            State.Status = FlightStatus.Crashed;
            CrashReason = reason;
        }

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
            Start = start;
        }

        /// <summary>
        /// One tick: <paramref name="clock"/>'s substeps of atmosphere → flight controls → powerplant → airframe, then
        /// the surface check after each substep. Meeting the surface with no gear that can take it is a crash, and a
        /// crash freezes the aircraft where it hit (AP23: every terminal verdict gets an end state). A Reset in the
        /// inputs puts it back at its start first, crashed or not, and the tick then flies from there.
        /// </summary>
        public void Step(in PilotInputs inputs, float surfaceHeightM, in SubstepClock clock) => Step(inputs, surfaceHeightM, null, clock);

        /// <summary>As above, with the ground's contact probe for the landing gear (null: no wheel ever touches).</summary>
        public void Step(in PilotInputs inputs, float surfaceHeightM, IContactProbe probe, in SubstepClock clock)
        {
            LastInputs = inputs;
            if (inputs.Has(PilotSwitches.Reset))
            {
                State = Start;
                Resets++;
                CrashReason = null;
                KilledBy = 0;
            }
            float dt = (float)clock.SubstepSeconds;
            if (State.Status == FlightStatus.Killed)
            {
                for (int k = 0; k < clock.Substeps && State.Status == FlightStatus.Killed; k++) Fall(surfaceHeightM, dt);
                return;
            }
            if (State.Status != FlightStatus.Flying) return;
            for (int k = 0; k < clock.Substeps; k++)
            {
                AirData air = Atmosphere.Sample(State.Position.y);
                ControlDemand demand = Controls.Demand(inputs, ref State, air, dt);
                float thrust = Powerplant.Step(inputs, air, ref State, dt);
                GearLoads gear = Gear.Step(ref State, inputs, probe, dt);
                if (gear.Failure != null) { Crash(gear.Failure); return; }
                Airframe.Step(ref State, demand, thrust, air, inputs, dt, gear, Gear.DragIncrement(State));

                // Any hard point (the centre of mass at L0; belly, tail, nose and wingtips at L1) meeting the surface ends it.
                foreach (Vector3 hp in Gear.HardPoints)
                    if (State.Position.y + (State.Attitude * hp).y <= surfaceHeightM)
                    {
                        State.Position.y = Mathf.Max(State.Position.y, surfaceHeightM);
                        Crash(hp == Vector3.zero ? "met the surface" : $"a hard point at {hp} met the surface");
                        return;
                    }
            }
        }
    }
}
