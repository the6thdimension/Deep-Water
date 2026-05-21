using UnityEngine;
using CarrierOps.Core.State;

namespace CarrierOps.Core.Recovery
{
    /// <summary>
    /// Arresting gear state machine. One <see cref="WireState"/> per physical wire on the
    /// deck (4 on a Ford). Same architectural pattern as <see cref="Catapult.CatapultCycle"/>:
    /// per-step <see cref="Step"/> for ongoing logic, centralised <see cref="Transition"/> +
    /// <see cref="OnEnter"/> for entry side-effects (per AP7).
    ///
    /// **Engagement test:** done outside this file because it needs the aircraft hook
    /// position vs. the wire's world-frame line. The entity (or behaviour adapter) computes
    /// whether a hook crossed a wire and calls <see cref="RequestEngage"/> with the catching
    /// aircraft's ID and approach speed.
    ///
    /// **Deceleration model:** constant G during Decelerating, until either the aircraft is
    /// stopped (speed ≈ 0) OR the runout distance reaches the wire stroke length. Whichever
    /// comes first transitions to Retracting.
    ///
    /// **What this does NOT model:**
    /// - Hydraulic curve (real AAG gives a slightly progressive deceleration; we use flat for simplicity)
    /// - Wire stretch (Obi rope handles the visual; the physics is reduced to a single G value)
    /// - Off-center catches / hook bounce
    /// - In-flight wire selection (multiple wires crossable in one step — handled by the
    ///   adapter, which picks one to engage)
    /// </summary>
    public static class ArrestingGear
    {
        public const float StandardGravity = 9.80665f;

        /// <summary>
        /// External request: engage this wire with the given aircraft. Idempotent — if the
        /// wire is already in a non-Idle stage, the request is ignored.
        /// </summary>
        public static void RequestEngage(ref WireState wire, int aircraftId, float aircraftSpeedAtCatch)
        {
            if (wire.Stage != WireStage.Idle) return;
            wire.EngagedAircraftId = aircraftId;
            wire.AircraftSpeedAtCatch = aircraftSpeedAtCatch;
            wire.RunoutMeters = 0f;
            Transition(ref wire, WireStage.Engaged);
        }

        /// <summary>
        /// Step the wire's state machine + apply deceleration to the engaged aircraft (if any).
        /// </summary>
        /// <param name="profile">Carrier profile — reads WireStrokeM, WireDecelerationG, WireRetractDurationS.</param>
        /// <param name="dt">Fixed step.</param>
        /// <param name="wire">Wire state to advance.</param>
        /// <param name="aircraft">The currently-engaged aircraft (or null). Used for ApplyDeceleration during Decelerating.</param>
        public static void Step(
            in CarrierProfileData profile,
            float dt,
            ref WireState wire,
            IRecoveringAircraft aircraft)
        {
            wire.StageTimer += dt;

            switch (wire.Stage)
            {
                case WireStage.Idle:
                    // Waiting for an engagement request.
                    break;

                case WireStage.Engaged:
                    // Single-step latch into Decelerating — the moment-of-contact transient.
                    // Decoupled as its own stage so any "snag" SFX or hook-flex VFX can be
                    // hooked here exactly once.
                    Transition(ref wire, WireStage.Decelerating);
                    break;

                case WireStage.Decelerating:
                    StepDecelerating(in profile, dt, ref wire, aircraft);
                    break;

                case WireStage.Retracting:
                    if (wire.StageTimer >= profile.WireRetractDurationS)
                        Transition(ref wire, WireStage.Idle);
                    break;
            }
        }

        private static void StepDecelerating(
            in CarrierProfileData profile,
            float dt,
            ref WireState wire,
            IRecoveringAircraft aircraft)
        {
            // Lost the aircraft (unregistered mid-trap) — abort to retract.
            if (aircraft == null)
            {
                Transition(ref wire, WireStage.Retracting);
                return;
            }

            float decel = profile.WireDecelerationG * StandardGravity;
            aircraft.ApplyDeceleration(decel, dt);

            // Estimate runout — aircraft speed along its motion vector times dt. We accumulate
            // until either the speed is below 1 m/s (stopped) or runout exceeds stroke.
            float currentSpeed = aircraft.Velocity.magnitude;
            wire.RunoutMeters += currentSpeed * dt;

            bool stopped = currentSpeed < 1f;
            bool brokeStroke = wire.RunoutMeters >= profile.WireStrokeM;

            if (stopped || brokeStroke)
                Transition(ref wire, WireStage.Retracting);
        }

        // ===========================================================================
        // Transition + OnEnter — same AP7-mitigating pattern as CatapultCycle.
        // ===========================================================================

        private static void Transition(ref WireState wire, WireStage next)
        {
            wire.Stage = next;
            wire.StageTimer = 0f;
            OnEnter(ref wire, next);
        }

        private static void OnEnter(ref WireState wire, WireStage stage)
        {
            switch (stage)
            {
                case WireStage.Idle:
                    // Reset the per-engagement bookkeeping when returning to Idle.
                    wire.EngagedAircraftId = 0;
                    wire.RunoutMeters = 0f;
                    wire.AircraftSpeedAtCatch = 0f;
                    break;

                // Engaged / Decelerating / Retracting currently have no on-entry side
                // effects beyond the timer reset done in Transition. Adding any "snag SFX"
                // or "wire stretch VFX" hooks would go here.
            }
        }
    }
}
