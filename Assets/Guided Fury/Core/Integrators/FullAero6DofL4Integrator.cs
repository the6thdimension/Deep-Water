using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Propulsion;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Integrators
{
    /// <summary>
    /// LOD 4 — Full-aero 6DOF. Same rigid-body integration shape as L3 but with:
    /// - Aerodynamic forces and moments derived from a Mach-aware
    ///   <see cref="IAeroModel"/> instead of L3's scalar coefficients.
    /// - Mach number computed from velocity / atmosphere sound speed.
    /// - Pitch / yaw moments from `Cm_α(M)·α` (stability — replaces L3's separate
    ///   "weather-vane coefficient"). The signs make sense as long as Cm_α is negative
    ///   for a stable airframe.
    ///
    /// **What's still inline (extracted in Phase B):**
    /// - Autopilot: same proportional rate controller as L3, applied as direct torque on
    ///   the body. Phase B introduces <see cref="IAutopilot"/> and an L4 autopilot that
    ///   commands control surface deflections; those deflections then produce aero
    ///   moments via Cm_δ(M).
    /// - Engine: same single-stage boost-only as L3. Phase C introduces <see cref="IThrustModel"/>
    ///   for boost-sustain profiles.
    ///
    /// **Forward Euler** for v, p. Phase C may upgrade to symplectic / RK4.
    ///
    /// **Coordinate conventions:** identical to L3. Body forward = local +Z.
    /// </summary>
    public sealed class FullAero6DofL4Integrator : IPhysicsIntegrator
    {
        public MissileLod Lod => MissileLod.L4_FullAero6Dof;

        private const float Gravity = 9.80665f;

        private readonly IAeroModel aero;
        private readonly IAutopilot autopilot;
        private readonly IThrustModel thrust;

        public FullAero6DofL4Integrator(IAeroModel aero, IAutopilot autopilot = null, IThrustModel thrust = null)
        {
            this.aero = aero ?? SimpleAeroModel.Instance;
            this.autopilot = autopilot ?? SurfaceDeflectionAutopilot.Instance;
            this.thrust = thrust ?? ConstantBoostThrustModel.Instance;
        }

        public void Initialize(in MissileProfileData profile, ref MissileState state)
        {
            state.Mass = profile.DryMassKg + profile.PropellantMassKg;
            state.Fuel = profile.PropellantMassKg;
            Vector3 forward = state.Orientation * Vector3.forward;
            state.Velocity = forward * profile.CruiseSpeedMps;
            state.AngularVelocity = Vector3.zero;
            state.Phase = MissilePhase.Boost;
        }

        public void Step(
            in MissileProfileData profile,
            in MissileCommand command,
            in AtmosphereSample atmo,
            float dt,
            ref MissileState state)
        {
            // The fin/rate loop is faster than Unity's usual 50 Hz physics tick.
            // Integrate it on bounded substeps without changing the game's fixed clock.
            const float maxStep = 0.001f;
            int count = Mathf.Max(1, Mathf.CeilToInt(dt / maxStep));
            float step = dt / count;
            for (int i = 0; i < count; i++)
                StepInternal(in profile, in command, in atmo, step, ref state);
        }

        private void StepInternal(
            in MissileProfileData profile,
            in MissileCommand command,
            in AtmosphereSample atmo,
            float dt,
            ref MissileState state)
        {
            // -- Sample thrust model (handles boost / boost-sustain / etc.) --
            bool burning = thrust.Sample(in state, in profile, out float thrustNewtons, out float burnRateKgPerSec);
            // Phase advance: when burning stops, transition Boost → Cruise.
            if (state.Phase == MissilePhase.Boost && !burning)
                state.Phase = MissilePhase.Cruise;

            // -- Body-frame setup ---------------------------------------------
            Vector3 bodyForward = state.Orientation * Vector3.forward;
            float speedSq = state.Velocity.sqrMagnitude;
            float speed = speedSq > 1e-6f ? Mathf.Sqrt(speedSq) : 0f;
            Vector3 velocityDir = speed > 1e-6f ? state.Velocity / speed : bodyForward;

            // -- Angle of attack ----------------------------------------------
            float aoaRad = 0f;
            Vector3 aoaAxis = Vector3.zero;
            if (speed > 0.1f)
            {
                float cosAoa = Mathf.Clamp(Vector3.Dot(bodyForward, velocityDir), -1f, 1f);
                aoaRad = Mathf.Acos(cosAoa);
                Vector3 cross = Vector3.Cross(bodyForward, velocityDir);
                if (cross.sqrMagnitude > 1e-8f)
                    aoaAxis = cross.normalized;
            }

            float dynamicPressure = 0.5f * atmo.Density * speedSq;
            float refArea = Mathf.Max(profile.ReferenceAreaM2, 1e-6f);
            float refLength = Mathf.Max(profile.LengthM, 0.1f);

            // -- Sample aero from the injected model --------------------------
            AeroSample aeroSample = aero.Sample(in state, in atmo, in profile);

            // -- Force accumulation -------------------------------------------
            Vector3 totalForce = Vector3.zero;

            totalForce += Vector3.down * (state.Mass * Gravity);

            // Drag along velocity. F = -v̂ · q · Cd · A
            if (speed > 1e-3f && aeroSample.Cd > 0f)
            {
                float dragMag = dynamicPressure * aeroSample.Cd * refArea;
                totalForce += -velocityDir * dragMag;
            }

            // Lift from AoA — coefficient now comes from the aero model (Mach-aware).
            // Stall handling: above StallAoaDeg, Cl collapses (same shape as L3).
            if (speed > 1e-3f && aeroSample.ClAlphaPerRad > 0f && aoaRad > 1e-4f)
            {
                float stallRad = profile.StallAoaDeg * Mathf.Deg2Rad;
                float liftCl;
                if (aoaRad <= stallRad)
                {
                    liftCl = aeroSample.ClAlphaPerRad * aoaRad;
                }
                else if (aoaRad <= 2f * stallRad)
                {
                    float clMax = aeroSample.ClAlphaPerRad * stallRad;
                    liftCl = clMax * (1f - (aoaRad - stallRad) / stallRad);
                }
                else
                {
                    liftCl = 0f;
                }

                // Lift points toward the nose's component perpendicular to airflow.
                Vector3 liftDir = Vector3.Cross(velocityDir, aoaAxis).normalized;
                float liftMag = dynamicPressure * liftCl * refArea;
                totalForce += liftDir * liftMag;
            }

            // Thrust along body axis. Magnitude from thrust model (constant boost OR boost-sustain).
            if (thrustNewtons > 0f)
                totalForce += bodyForward * thrustNewtons;

            float invMass = state.Mass > 1e-3f ? 1f / state.Mass : 0f;
            Vector3 accel = totalForce * invMass;

            // -- Autopilot ----------------------------------------------------
            // L4 default autopilot is SurfaceDeflection — produces commanded fin deflections
            // (pitch + yaw). The integrator rate-limits those toward state.PitchDeflectionRad
            // / state.YawDeflectionRad and computes aero moments via Cm_δ × q × A × arm.
            // L3's SimpleRate autopilot is also accepted; it produces ControlTorqueWorld
            // directly which we add to the moment sum as a fallback.
            AutopilotOutput apOut = autopilot.Compute(in state, in command, in profile);

            // Rate-limit the actual fin deflection toward the commanded value.
            float maxRateRad = profile.MaxControlRateDegPerSec * Mathf.Deg2Rad;
            float maxDeflRad = profile.MaxControlDeflectionDeg * Mathf.Deg2Rad;
            state.PitchDeflectionRad = Mathf.Clamp(
                Mathf.MoveTowards(state.PitchDeflectionRad, apOut.PitchDeflectionRad, maxRateRad * dt),
                -maxDeflRad, maxDeflRad);
            state.YawDeflectionRad = Mathf.Clamp(
                Mathf.MoveTowards(state.YawDeflectionRad, apOut.YawDeflectionRad, maxRateRad * dt),
                -maxDeflRad, maxDeflRad);

            // -- Aero stability moment (Cm_α · α) ----------------------------
            // Negative Cm_α (stable) rotates body BACK toward velocity (same direction as L3's weather-vane).
            Vector3 aeroMomentWorld = Vector3.zero;
            if (aoaRad > 1e-4f && Mathf.Abs(aeroSample.CmAlphaPerRad) > 1e-6f && dynamicPressure > 0f)
            {
                float momentMag = aeroSample.CmAlphaPerRad * aoaRad * dynamicPressure * refArea * refLength;
                aeroMomentWorld = -aoaAxis * momentMag;
            }

            // -- Control surface moment (Cm_δ · δ) ---------------------------
            // Pitch and yaw deflections produce moments about the body X (pitch) and Y (yaw)
            // axes respectively. Compute in body frame, then rotate to world for accumulation.
            Vector3 surfaceMomentBody = Vector3.zero;
            if (dynamicPressure > 0f && Mathf.Abs(aeroSample.CmDeltaPerRad) > 1e-6f)
            {
                float surfaceCoeff = aeroSample.CmDeltaPerRad * dynamicPressure * refArea * refLength;
                surfaceMomentBody.x = surfaceCoeff * state.PitchDeflectionRad;
                surfaceMomentBody.y = surfaceCoeff * state.YawDeflectionRad;
            }
            Vector3 surfaceMomentWorld = state.Orientation * surfaceMomentBody;

            // SimpleRate autopilot fallback — if the user wired the L3-style autopilot, its
            // direct torque enters here. SurfaceDeflection autopilots zero this field.
            Vector3 totalTorqueWorld = apOut.ControlTorqueWorld + aeroMomentWorld + surfaceMomentWorld;

            // -- Angular velocity update -------------------------------------
            Vector3 torqueBody = Quaternion.Inverse(state.Orientation) * totalTorqueWorld;
            float Iyy = Mathf.Max(profile.TransverseInertiaKgM2, 1e-3f);
            float Ixx = Mathf.Max(profile.RollInertiaKgM2, 1e-3f);
            Vector3 angAccelBody = new Vector3(
                torqueBody.x / Iyy,
                torqueBody.y / Iyy,
                torqueBody.z / Ixx);
            state.AngularVelocity += angAccelBody * dt;

            // -- Orientation update via quaternion derivative ----------------
            Vector3 omegaBody = state.AngularVelocity;
            Quaternion omegaQuat = new Quaternion(omegaBody.x, omegaBody.y, omegaBody.z, 0f);
            Quaternion dq = QuatScale(QuatMul(state.Orientation, omegaQuat), 0.5f * dt);
            state.Orientation = QuatNormalize(QuatAdd(state.Orientation, dq));

            // -- Linear state integration (forward Euler) --------------------
            state.Velocity += accel * dt;
            state.Position += state.Velocity * dt;

            // -- Mass burn (rate from thrust model — handles boost-sustain split correctly) --
            if (burning && burnRateKgPerSec > 0f)
            {
                state.Fuel = Mathf.Max(0f, state.Fuel - burnRateKgPerSec * dt);
                state.Mass = profile.DryMassKg + state.Fuel;
            }

            // -- Time bookkeeping --------------------------------------------
            state.TimeOfFlight += dt;
            if (state.TimeOfFlight >= profile.MaxLifetimeS)
                state.Phase = MissilePhase.Failed;
        }

        // -- Quaternion helpers (duplicated from L3 for now; extract to MathHelpers if more integrators need them) --
        private static Quaternion QuatMul(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        private static Quaternion QuatScale(Quaternion q, float s) => new Quaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        private static Quaternion QuatAdd(Quaternion a, Quaternion b) => new Quaternion(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        private static Quaternion QuatNormalize(Quaternion q)
        {
            float magSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (magSq < 1e-12f) return Quaternion.identity;
            float inv = 1f / Mathf.Sqrt(magSq);
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }
    }
}
