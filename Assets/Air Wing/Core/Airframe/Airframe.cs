using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.FlightControls;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.LandingGear;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.Airframe
{
    /// <summary>The Airframe part (BLUEPRINT §4): forces to motion.</summary>
    public interface IAirframe
    {
        /// <summary>Advance <paramref name="state"/> by one substep under the demand and the thrust.</summary>
        /// <summary>
        /// Advance <paramref name="state"/> by one substep under the demand and the thrust. <paramref name="gear"/> is the
        /// landing gear's force and moment this substep, <paramref name="gearDragCD"/> its drag (Airframe L1 only).
        /// </summary>
        void Step(ref AirframeState state, in ControlDemand demand, float thrustN, in AirData air, in PilotInputs inputs, float dt,
                  in GearLoads gear = default, float gearDragCD = 0f);
    }

    /// <summary>
    /// Airframe L0 (W3): a point mass with an energy model. Thrust minus drag acts along the flight path; lift acts
    /// along the wing's "up" at the demanded load factor, as far as Cl_max at this dynamic pressure allows; gravity
    /// acts down. The flight path is carried by a wind frame (forward along the velocity, up along the lift) that rolls
    /// about the velocity at the demanded rate. With no sideslip, a banked aircraft turns by itself, at
    /// g·√(n² − 1)/V in a level turn. The body is drawn the angle of attack above the flight path (α = C_L / C_Lα);
    /// α is presentation and HUD only. No Mach effect (L2), no ground effect (L2), no gear (L1).
    /// </summary>
    public sealed class AirframeL0 : IAirframe
    {
        /// <summary>Below this speed the flight path's direction is kept from the last step.</summary>
        const float MinSpeedMps = 1f;
        const float G0 = Units.StandardGravity;

        readonly AirframeProfile _p;
        readonly float _k;

        public AirframeL0(in AirframeProfile profile)
        {
            profile.Validate();
            _p = profile;
            _k = profile.InducedDragFactor;
        }

        /// <summary>The largest load factor lift can give at this speed and density (positive, then negative side).</summary>
        public void AeroLoadFactorLimits(float speedMps, float densityKgM3, float massKg, bool flaps, out float nMax, out float nMin)
        {
            float qS = 0.5f * densityKgM3 * speedMps * speedMps * _p.WingAreaM2;
            float weight = massKg * G0;
            nMax = qS * (_p.CLmax + (flaps ? _p.FlapsDeltaCLmax : 0f)) / weight;
            nMin = -qS * _p.CLmaxNegative / weight;
        }

        /// <summary>Drag at load factor <paramref name="n"/>, newtons: parasite plus induced, plus flaps and speed brake.</summary>
        public float DragN(float n, float speedMps, float densityKgM3, float massKg, bool flaps, bool speedBrake, out float cl)
        {
            float qS = 0.5f * densityKgM3 * speedMps * speedMps * _p.WingAreaM2;
            cl = qS > 1e-3f ? n * massKg * G0 / qS : 0f;
            float cd = _p.CD0 + _k * cl * cl + (flaps ? _p.FlapsDeltaCD : 0f) + (speedBrake ? _p.SpeedBrakeDeltaCD : 0f);
            return qS * cd;
        }

        public void Step(ref AirframeState s, in ControlDemand demand, float thrustN, in AirData air, in PilotInputs inputs, float dt,
                         in GearLoads gear = default, float gearDragCD = 0f)
        {
            if (s.Status != FlightStatus.Flying) return;

            bool flaps = inputs.Has(PilotSwitches.FlapsDown);
            bool brake = inputs.Has(PilotSwitches.SpeedBrake);
            Vector3 v = s.Velocity;
            float speed = v.magnitude;

            AeroLoadFactorLimits(speed, air.DensityKgM3, s.MassKg, flaps, out float nMax, out float nMin);
            float n = demand.LoadFactor > nMax ? nMax : demand.LoadFactor < nMin ? nMin : demand.LoadFactor;
            float drag = DragN(n, speed, air.DensityKgM3, s.MassKg, flaps, brake, out float cl);

            // The wind frame, recovered from the body attitude and the angle of attack the last step drew.
            Quaternion wind = s.Attitude * Quaternion.AngleAxis(s.AngleOfAttack * Units.RadToDeg, Vector3.right);
            Vector3 forward = speed > MinSpeedMps ? v / speed : wind * Vector3.forward;
            Vector3 up = wind * Vector3.up;

            // Semi-implicit Euler: the new velocity moves the position.
            Vector3 accel = ((thrustN - drag) / s.MassKg) * forward + (n * G0) * up + new Vector3(0f, -G0, 0f);
            Vector3 vNew = v + accel * dt;
            s.Velocity = vNew;
            s.Position += vNew * dt;

            // Carry the wind frame onto the new flight path by the smallest rotation, then roll it about the path.
            float speedNew = vNew.magnitude;
            Quaternion windNew = wind;
            if (speed > MinSpeedMps && speedNew > MinSpeedMps)
                windNew = Quaternion.FromToRotation(forward, vNew / speedNew) * wind;
            windNew = windNew * Quaternion.AngleAxis(-demand.RollRateRadS * dt * Units.RadToDeg, Vector3.forward);
            windNew = Quaternion.Normalize(windNew);

            // Wind-axis rates from the frame's rotation over the step (small-angle form: ω ≈ 2·v/dt of the delta
            // quaternion), then the roll rate exactly as applied, so the control law's lag reads back what it asked for.
            Quaternion delta = Quaternion.Inverse(wind) * windNew;
            if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
            s.BodyRates = new Vector3(2f * delta.x / dt, 2f * delta.y / dt, -demand.RollRateRadS);

            float alphaLimit = (_p.CLmax + (flaps ? _p.FlapsDeltaCLmax : 0f)) / _p.CLalphaPerRad;
            float alpha = cl / _p.CLalphaPerRad;
            alpha = alpha > alphaLimit ? alphaLimit : alpha < -alphaLimit ? -alphaLimit : alpha;
            s.AngleOfAttack = alpha;
            s.Attitude = windNew * Quaternion.AngleAxis(-alpha * Units.RadToDeg, Vector3.right);
            s.LoadFactor = n;
        }
    }
}
