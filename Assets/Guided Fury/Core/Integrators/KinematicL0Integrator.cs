using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Integrators
{
    /// <summary>
    /// LOD 0 — Kinematic trajectory.
    ///
    /// **What it models (cheap by design):**
    /// - Position evolves via simple forward-Euler: pos += vel * dt
    /// - If `profile.L0UseGravity` is true, velocity is integrated under gravity (-9.80665 m/s²
    ///   on world Y). Result: ballistic arc. Useful for dumb bombs and unguided rockets past
    ///   burnout.
    /// - If gravity is off, velocity is set to cruise-speed × commanded direction. Result:
    ///   constant-speed pursuit toward whatever guidance is asking for. Useful for distant
    ///   homing missiles where realistic dynamics aren't worth the CPU.
    /// - During boost (TimeOfFlight &lt; BoostDurationS), the missile is accelerating up to
    ///   cruise speed; afterward it stays at cruise speed (or decays under gravity).
    /// - Orientation tracks the velocity vector for visual purposes (so the model points where
    ///   it's going). No torque, no inertia, no AoA.
    ///
    /// **What it does NOT model:** drag, lift, rotation dynamics, turn-rate limits, mass change
    /// from fuel burn (mass stays at launch value). Those start at L1+.
    ///
    /// **Why this is useful at all:** for salvos of dozens or hundreds of missiles, L0 is the
    /// difference between a smooth scene and a stutter. Visually indistinguishable from L1 at
    /// camera distances of more than a few hundred meters. The kind of thing a player will
    /// never notice on a missile they can't make out as more than a smoke trail.
    /// </summary>
    public sealed class KinematicL0Integrator : IPhysicsIntegrator
    {
        public MissileLod Lod => MissileLod.L0_Kinematic;

        // Standard gravity, same as StandardAtmosphere. Pulled out as a local constant so this
        // integrator has no dependency on the atmosphere model (L0 doesn't use the atmo sample
        // at all — it accepts the parameter to satisfy the interface contract).
        private const float Gravity = 9.80665f;

        public void Initialize(in MissileProfileData profile, ref MissileState state)
        {
            // Set the missile's starting mass. L0 doesn't burn mass, but downstream code (UI,
            // fuze, manager) may read it.
            state.Mass = profile.DryMassKg + profile.PropellantMassKg;
            state.Fuel = profile.PropellantMassKg;

            // L0 begins flight already at cruise speed in the missile's forward direction.
            // (No realistic boost-acceleration profile at this tier — the missile is "tossed"
            // into the air at speed. Higher LODs do model the boost phase.)
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
            // -- Phase advance ------------------------------------------------
            // Boost → Cruise once the motor has burned. (L0 doesn't apply boost acceleration,
            // but we mirror the phase machine so downstream consumers can react.)
            if (state.Phase == MissilePhase.Boost && state.TimeOfFlight >= profile.BoostDurationS)
            {
                state.Phase = MissilePhase.Cruise;
                state.Fuel  = 0f;
                state.Mass  = profile.DryMassKg;
            }

            // -- Velocity update ----------------------------------------------
            if (profile.L0UseGravity)
            {
                // Ballistic: only gravity acts. Initial velocity carries the missile; the arc
                // is whatever falls out of v + g*dt.
                state.Velocity += Vector3.down * (Gravity * dt);
            }
            else
            {
                // Command-following: head toward commanded acceleration vector at cruise
                // speed. If no command is given (zero vector), keep current heading.
                Vector3 commanded = command.CommandedAccelerationWorld;
                if (commanded.sqrMagnitude > 1e-6f)
                {
                    Vector3 desiredDir = commanded.normalized;
                    state.Velocity = desiredDir * profile.CruiseSpeedMps;
                }
                else
                {
                    // No command: keep current direction, snap magnitude to cruise speed.
                    // (Defends against numerical drift if anything else has nudged velocity.)
                    Vector3 dir = state.Velocity.sqrMagnitude > 1e-6f
                        ? state.Velocity.normalized
                        : state.Orientation * Vector3.forward;
                    state.Velocity = dir * profile.CruiseSpeedMps;
                }
            }

            // -- Position update (forward Euler) ------------------------------
            // First-order is fine at L0. Higher LODs (L3/L4) will move to symplectic / RK4 if
            // accuracy demands it; at L0 the visual difference is invisible.
            state.Position += state.Velocity * dt;

            // -- Orientation tracks velocity ----------------------------------
            // Purely visual: rotate the missile so its forward axis points along its motion.
            // This is what L0 cheats on — no rotation dynamics, no AoA. The model just points
            // where it's going.
            if (state.Velocity.sqrMagnitude > 1e-6f)
            {
                state.Orientation = Quaternion.LookRotation(state.Velocity.normalized, Vector3.up);
            }

            // -- Time bookkeeping ---------------------------------------------
            state.TimeOfFlight += dt;

            // -- Lifetime guard -----------------------------------------------
            // Hard cap to prevent runaway missiles. Higher-LOD fuzing/proximity logic happens
            // outside the integrator, in the entity.
            if (state.TimeOfFlight >= profile.MaxLifetimeS)
            {
                state.Phase = MissilePhase.Failed;
            }
        }
    }
}
