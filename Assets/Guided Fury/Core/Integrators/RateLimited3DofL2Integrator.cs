using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Integrators
{
    /// <summary>
    /// LOD 2 — 3DOF point mass with realistic maneuver limits.
    ///
    /// **What L2 adds vs. L1:**
    /// - **g-limit:** the commanded lateral acceleration magnitude is clipped to
    ///   `MaxLoadFactorG × g`. Real airframes have a structural limit on how hard they can
    ///   maneuver; an AAM is typically 30–50 g, an SAM 20–35 g.
    /// - **Turn-rate limit:** even within g-limits, the velocity vector can only rotate so
    ///   fast — modeled as the angular rate of the velocity direction. For a given speed v
    ///   and angular rate ω, the corresponding lateral accel is ω·v. We clip the commanded
    ///   accel to whichever of (g_max, ω_max·v) is more restrictive at the current speed.
    ///
    /// **What L2 does NOT add (vs. L3+):**
    /// - Rotation dynamics — orientation still tracks velocity instantly at each step.
    ///   Real airframes have rotational inertia and a finite body-pitch rate.
    /// - Angle-of-attack physics — no lift from AoA, no induced drag.
    /// - Mach effects, control surface dynamics — that's L4.
    ///
    /// **Implementation note:** the g-limit applies to the *commanded* lateral accel, not
    /// to total accel. Gravity still acts in full — physical structures don't get to "pull
    /// less gravity." Drag and thrust pass through unchanged from L1.
    /// </summary>
    public sealed class RateLimited3DofL2Integrator : IPhysicsIntegrator
    {
        public MissileLod Lod => MissileLod.L2_RateLimited3Dof;

        private const float Gravity = 9.80665f;

        public void Initialize(in MissileProfileData profile, ref MissileState state)
        {
            // Identical to L1 — Initialize is about the airframe's starting conditions,
            // which don't depend on maneuver limits.
            state.Mass = profile.DryMassKg + profile.PropellantMassKg;
            state.Fuel = profile.PropellantMassKg;

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
                state.Phase = MissilePhase.Cruise;
                state.Fuel  = 0f;
                state.Mass  = profile.DryMassKg;
            }

            // -- Maneuver-limited command ------------------------------------
            // Compute the effective max lateral accel as min(g-limit, turn-rate × speed).
            float speedSq = state.Velocity.sqrMagnitude;
            float speed   = speedSq > 1e-6f ? Mathf.Sqrt(speedSq) : 0f;

            float gLimit       = Mathf.Max(profile.MaxLoadFactorG, 0f) * Gravity;
            float turnRateRad  = Mathf.Max(profile.MaxTurnRateDegPerSec, 0f) * Mathf.Deg2Rad;
            float turnAccelCap = turnRateRad * speed; // a = ω × v
            float effectiveMax = Mathf.Min(gLimit, turnAccelCap);
            // If either limit is zero (e.g. profile not yet authored), fall back to the other.
            // If both are zero, effectiveMax = 0 → no lateral accel applied (no clipping
            // failure, just an over-constrained airframe).
            if (gLimit <= 0f && turnAccelCap > 0f)       effectiveMax = turnAccelCap;
            else if (turnAccelCap <= 0f && gLimit > 0f)  effectiveMax = gLimit;

            Vector3 limitedCommandAccel = command.CommandedAccelerationWorld;
            float commandMagSq = limitedCommandAccel.sqrMagnitude;
            if (commandMagSq > effectiveMax * effectiveMax && effectiveMax > 0f)
            {
                // Magnitude exceeds the limit — clip while preserving direction.
                float commandMag = Mathf.Sqrt(commandMagSq);
                limitedCommandAccel *= effectiveMax / commandMag;
            }

            // -- Force accumulation -------------------------------------------
            Vector3 totalForce = Vector3.zero;

            // Gravity (constant downward).
            totalForce += Vector3.down * (state.Mass * Gravity);

            // Drag. F = -½·ρ·|v|·v·Cd·A
            if (speedSq > 1e-6f && profile.DragCoefficient > 0f && profile.ReferenceAreaM2 > 0f)
            {
                float dragMagnitude = 0.5f * atmo.Density * speedSq * profile.DragCoefficient * profile.ReferenceAreaM2;
                Vector3 dragForce = -(state.Velocity / speed) * dragMagnitude;
                totalForce += dragForce;
            }

            // Thrust during boost (full-duration boost, same as L1 — throttle is for cruise
            // motors that don't exist yet).
            if (boosting)
            {
                Vector3 thrustDirection = speedSq > 1e-6f
                    ? state.Velocity / speed
                    : state.Orientation * Vector3.forward;

                totalForce += thrustDirection * profile.BoostThrustN;
            }

            // Convert forces to acceleration.
            float invMass = state.Mass > 1e-3f ? 1f / state.Mass : 0f;
            Vector3 accel = totalForce * invMass;

            // Apply the (now-limited) commanded lateral accel.
            accel += limitedCommandAccel;

            // -- State integration (forward Euler) ----------------------------
            state.Velocity += accel * dt;
            state.Position += state.Velocity * dt;

            // -- Orientation tracks velocity (no rotation dynamics yet — L3) --
            if (state.Velocity.sqrMagnitude > 1e-6f)
                state.Orientation = Quaternion.LookRotation(state.Velocity.normalized, Vector3.up);

            // -- Mass burn ----------------------------------------------------
            if (boosting && profile.BoostDurationS > 1e-6f)
            {
                float burnRate = profile.PropellantMassKg / profile.BoostDurationS;
                state.Fuel = Mathf.Max(0f, state.Fuel - burnRate * dt);
                state.Mass = profile.DryMassKg + state.Fuel;
            }

            // -- Time bookkeeping and lifetime guard --------------------------
            state.TimeOfFlight += dt;
            if (state.TimeOfFlight >= profile.MaxLifetimeS)
                state.Phase = MissilePhase.Failed;
        }
    }
}
