using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Integrators
{
    /// <summary>
    /// LOD 1 — 3DOF point mass with thrust, drag, gravity, and commanded lateral acceleration.
    ///
    /// **What it models:**
    /// - Translation only. Orientation tracks the velocity vector at each step — there are
    ///   no rotational dynamics yet (those start at L3).
    /// - Thrust force along the current velocity vector during boost; zero afterward.
    /// - Mass burns linearly during boost from (Dry + Propellant) → Dry at burnout.
    /// - Drag force = ½·ρ·v²·Cd·A, opposing the velocity vector. Atmosphere density comes
    ///   from the supplied AtmosphereSample.
    /// - Gravity acts downward (−Y) at the constant `g = 9.80665` m/s². No latitude-dependent
    ///   gravity or Coriolis — those are above L5.
    /// - Guidance command (lateral accel, world frame) is added directly to the total accel.
    ///   No rate limit, no g-clip; the integrator delivers whatever guidance asks for.
    ///   L2 is where rate/g limits get enforced.
    ///
    /// **What it does NOT model (compared to higher LODs):**
    /// - Angle of attack — no lift, no body-axis-vs-velocity divergence.
    /// - Mach effects — Cd is scalar, not a function of Mach.
    /// - Rotational inertia — orientation snaps to velocity each step.
    /// - Per-axis drag (lateral drag differs from axial drag in reality).
    ///
    /// **Integration scheme:** forward Euler for v and p. Energy-conservation matters less
    /// here than simplicity; the missile is short-lived (seconds) so drift is bounded.
    /// L3/L4 will graduate to semi-implicit / RK4 where the dynamics demand it.
    /// </summary>
    public sealed class PointMass3DofL1Integrator : IPhysicsIntegrator
    {
        public MissileLod Lod => MissileLod.L1_PointMass3Dof;

        private const float Gravity = 9.80665f;

        public void Initialize(in MissileProfileData profile, ref MissileState state)
        {
            // Start with full mass — fuel will burn during Step.
            state.Mass = profile.DryMassKg + profile.PropellantMassKg;
            state.Fuel = profile.PropellantMassKg;

            // Initial velocity: cruise speed in the missile's forward direction. This mirrors
            // the L0 behaviour so an L0→L1 swap doesn't see a velocity discontinuity.
            // (At Phase 2 we don't yet support mid-flight swap, but the contract is consistent.)
            Vector3 forward = state.Orientation * Vector3.forward;
            state.Velocity = forward * profile.CruiseSpeedMps;
            state.Phase    = MissilePhase.Boost;
        }

        public void Step(
            in MissileProfileData profile,
            in MissileCommand command,
            in AtmosphereSample atmo,
            float dt,
            ref MissileState state)
        {
            // -- Phase advance and mass burn ----------------------------------
            bool boosting = state.Phase == MissilePhase.Boost && state.TimeOfFlight < profile.BoostDurationS;
            if (state.Phase == MissilePhase.Boost && !boosting)
            {
                // Just transitioned from Boost to Cruise.
                state.Phase = MissilePhase.Cruise;
                state.Fuel  = 0f;
                state.Mass  = profile.DryMassKg;
            }

            // -- Force accumulation -------------------------------------------
            // Working in world-frame accel; we divide forces by mass once at the end.
            Vector3 totalForce = Vector3.zero;

            // Gravity (constant downward force).
            totalForce += Vector3.down * (state.Mass * Gravity);

            // Drag. F = -½·ρ·|v|·v·Cd·A    (vector form: opposes velocity, magnitude ∝ v²)
            float speedSq = state.Velocity.sqrMagnitude;
            if (speedSq > 1e-6f && profile.DragCoefficient > 0f && profile.ReferenceAreaM2 > 0f)
            {
                float speed = Mathf.Sqrt(speedSq);
                float dragMagnitude = 0.5f * atmo.Density * speedSq * profile.DragCoefficient * profile.ReferenceAreaM2;
                Vector3 dragForce = -(state.Velocity / speed) * dragMagnitude;
                totalForce += dragForce;
            }

            // Thrust during boost. Direction: along current velocity (at L1, orientation
            // tracks velocity, so this is also the body-forward direction).
            //
            // **Phase 2 simplification:** the boost motor burns at full thrust for the entire
            // BoostDurationS. We ignore command.ThrustThrottle here — that field exists for
            // future cruise/sustainer motors (Phase 4+) where guidance might want to extend
            // or reduce thrust. A guided missile that hasn't acquired a target should still
            // boost out of the launcher, so binding boost to throttle would be wrong.
            if (boosting)
            {
                Vector3 thrustDirection;
                if (speedSq > 1e-6f)
                    thrustDirection = state.Velocity.normalized;
                else
                    thrustDirection = state.Orientation * Vector3.forward;

                totalForce += thrustDirection * profile.BoostThrustN;
            }

            // Convert forces to acceleration.
            float invMass = state.Mass > 1e-3f ? 1f / state.Mass : 0f;
            Vector3 accel = totalForce * invMass;

            // Commanded lateral acceleration from guidance — added on top of the physics
            // accel. The integrator does not clip it; that's L2's job.
            accel += command.CommandedAccelerationWorld;

            // -- State integration (forward Euler) ----------------------------
            state.Velocity += accel * dt;
            state.Position += state.Velocity * dt;

            // -- Orientation tracks velocity ---------------------------------
            // L1 has no rotational dynamics — orientation snaps to point along motion.
            if (state.Velocity.sqrMagnitude > 1e-6f)
                state.Orientation = Quaternion.LookRotation(state.Velocity.normalized, Vector3.up);

            // -- Mass burn (linear during boost) ------------------------------
            if (boosting && profile.BoostDurationS > 1e-6f)
            {
                float burnRate = profile.PropellantMassKg / profile.BoostDurationS; // kg/s
                state.Fuel = Mathf.Max(0f, state.Fuel - burnRate * dt);
                state.Mass = profile.DryMassKg + state.Fuel;
            }

            // -- Time bookkeeping ---------------------------------------------
            state.TimeOfFlight += dt;

            // -- Lifetime guard -----------------------------------------------
            if (state.TimeOfFlight >= profile.MaxLifetimeS)
                state.Phase = MissilePhase.Failed;
        }
    }
}
