using System.Collections.Generic;
using UnityEngine;
using CarrierOps.Core.Catapult;
using CarrierOps.Core.Elevator;
using CarrierOps.Core.Motion;
using CarrierOps.Core.Movement;
using CarrierOps.Core.Recovery;
using CarrierOps.Core.State;

namespace CarrierOps.Core.Carrier
{
    /// <summary>
    /// Pure-C# carrier aggregate. Owns the profile, the state, the motion model, and a
    /// registry of approaching aircraft (for FLOLS + arresting gear). Orchestrates the
    /// per-FixedUpdate Step that runs every subsystem in the right order.
    ///
    /// **No Unity scene dependencies** — same rule as MissileEntity. The behaviour adapter
    /// is responsible for any scene-geometry work (computing world positions of the FLOLS
    /// reference, detecting hook-wire crossings) and feeds events into the entity:
    /// - <see cref="RegisterRecoveringAircraft"/> / <see cref="UnregisterRecoveringAircraft"/>
    /// - <see cref="SetFlolsState"/>
    /// - <see cref="RequestWireEngage"/>
    /// </summary>
    public sealed class CarrierEntity
    {
        private CarrierProfileData _profile;
        public ref readonly CarrierProfileData Profile => ref _profile;

        public ISeaStateMotion Motion { get; }
        public CarrierState State { get; }

        // -- Recovery registry --------------------------------------------------
        // Maps a stable integer ID → the IRecoveringAircraft. WireState holds the ID
        // (unmanaged-friendly); we look up the live ref here for deceleration calls.
        private readonly Dictionary<int, IRecoveringAircraft> recovering = new Dictionary<int, IRecoveringAircraft>();
        private int nextRecoveryId = 1;

        public CarrierEntity(in CarrierProfileData profile, ISeaStateMotion motion = null)
        {
            _profile = profile;
            Motion = motion ?? SumOfSinesMotion.Instance;

            State = new CarrierState(
                catapultCount: Mathf.Max(profile.CatapultCount, 1),
                elevatorCount: Mathf.Max(profile.ElevatorCount, 1),
                wireCount:     Mathf.Max(profile.WireCount, 1));
        }

        // -- Public command API -------------------------------------------------
        public void RequestCatapultLaunch(int catapultIndex)
        {
            if (catapultIndex < 0 || catapultIndex >= State.Catapults.Length) return;
            CatapultCycle.RequestLaunch(ref State.Catapults[catapultIndex]);
        }

        public void RequestElevator(int elevatorIndex, bool deploy)
        {
            if (elevatorIndex < 0 || elevatorIndex >= State.Elevators.Length) return;
            if (deploy) ElevatorCycle.RequestDeploy(ref State.Elevators[elevatorIndex]);
            else        ElevatorCycle.RequestStow(ref State.Elevators[elevatorIndex]);
        }

        /// <summary>Register an approaching aircraft for FLOLS + wire engagement. Returns the assigned ID.</summary>
        public int RegisterRecoveringAircraft(IRecoveringAircraft aircraft)
        {
            if (aircraft == null) return 0;
            int id = nextRecoveryId++;
            aircraft.RegistrationId = id;
            recovering[id] = aircraft;
            return id;
        }

        public void UnregisterRecoveringAircraft(IRecoveringAircraft aircraft)
        {
            if (aircraft == null || aircraft.RegistrationId == 0) return;
            recovering.Remove(aircraft.RegistrationId);
            aircraft.RegistrationId = 0;
        }

        /// <summary>Behaviour-supplied FLOLS state — computed against the nearest aircraft using scene geometry.</summary>
        public void SetFlolsState(in FlolsState flolsState) { State.Flols = flolsState; }

        /// <summary>Behaviour-supplied engagement request — fired when a hook crosses a wire.</summary>
        public void RequestWireEngage(int wireIndex, int aircraftId, float aircraftSpeedAtCatch)
        {
            if (wireIndex < 0 || wireIndex >= State.Wires.Length) return;
            ArrestingGear.RequestEngage(ref State.Wires[wireIndex], aircraftId, aircraftSpeedAtCatch);
        }

        public IRecoveringAircraft GetRecovering(int id) =>
            recovering.TryGetValue(id, out var a) ? a : null;

        /// <summary>
        /// Enumeration of all currently-registered recovering aircraft. The behaviour
        /// iterates this each FixedUpdate to compute scene geometry (FLOLS, wire crossings).
        /// </summary>
        public IEnumerable<IRecoveringAircraft> RecoveringAircraft => recovering.Values;

        // -- Step ---------------------------------------------------------------
        public void Step(in ShipCommand command, float dt)
        {
            // -- Time -------------------------------------------------------
            State.TimeOfSim += dt;

            // -- Movement ---------------------------------------------------
            ShipKinematics.Step(in _profile, in command, dt, State);

            // -- Motion model (sea-state sway) ------------------------------
            Motion.Sample(in _profile.SeaState, State.TimeOfSim,
                out float heave, out float rollDeg, out float pitchDeg);
            State.SwayOffset = new Vector3(0f, heave, 0f);
            State.SwayRollDeg = rollDeg;
            State.SwayPitchDeg = pitchDeg;

            // -- Catapults --------------------------------------------------
            for (int i = 0; i < State.Catapults.Length; i++)
                CatapultCycle.Step(in _profile, dt, ref State.Catapults[i]);

            // -- Elevators --------------------------------------------------
            for (int i = 0; i < State.Elevators.Length; i++)
                ElevatorCycle.Step(in _profile, dt, ref State.Elevators[i]);

            // -- Arresting wires -------------------------------------------
            for (int i = 0; i < State.Wires.Length; i++)
            {
                IRecoveringAircraft engaged = State.Wires[i].EngagedAircraftId != 0
                    ? GetRecovering(State.Wires[i].EngagedAircraftId)
                    : null;
                ArrestingGear.Step(in _profile, dt, ref State.Wires[i], engaged);
            }
        }
    }
}
