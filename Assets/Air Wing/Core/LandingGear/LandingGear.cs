using DeepWater.AirWing.Core.Airframe;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.LandingGear
{
    /// <summary>What the gear did this substep: its force and moment on the airframe (NASA body frame), and whether it broke.</summary>
    public readonly struct GearLoads
    {
        public readonly Vector3 ForceNasa;
        public readonly Vector3 MomentNasa;
        /// <summary>Why the gear failed this substep (a crash), or null.</summary>
        public readonly string Failure;

        public GearLoads(Vector3 forceNasa, Vector3 momentNasa, string failure)
        {
            ForceNasa = forceNasa;
            MomentNasa = momentNasa;
            Failure = failure;
        }
    }

    /// <summary>The Landing Gear part (BLUEPRINT §4).</summary>
    public interface ILandingGear
    {
        /// <summary>Points that end the flight if they meet a surface, body frame about the centre of mass (Unity axes).</summary>
        Vector3[] HardPoints { get; }
        /// <summary>One substep: the gear's transit, then each wheel's contact (through <paramref name="probe"/>).</summary>
        GearLoads Step(ref AirframeState state, in PilotInputs inputs, IContactProbe probe, float dt);
        /// <summary>The gear's drag coefficient increment in its present position.</summary>
        float DragIncrement(in AirframeState state);
    }

    /// <summary>Landing Gear L0: none. L0 is airborne only (W29), so the centre of mass meeting any surface is a crash.</summary>
    public sealed class NoLandingGear : ILandingGear
    {
        static readonly Vector3[] Centre = { Vector3.zero };
        public Vector3[] HardPoints => Centre;
        public GearLoads Step(ref AirframeState state, in PilotInputs inputs, IContactProbe probe, float dt) => default;
        public float DragIncrement(in AirframeState state) => 0f;
    }

    /// <summary>
    /// Landing Gear L1 (W4, W29): a spring-damper strut per wheel, meeting the ground where the contact probe says (the
    /// scenes ask PhysX). The strut force acts along its axis; the tyre adds rolling, braking and side friction, each
    /// rising smoothly to its limit μ·N over a small slip speed. The pedals steer the nosewheel, fading out with speed;
    /// the brake control works the mains. The gear comes up and down over its transit time, and will not come up with
    /// weight on the wheels. A wheel arriving faster than the gear's sink limit, or a strut driven through its travel,
    /// breaks the gear: the flight ends there (AP23).
    /// </summary>
    public sealed class LandingGearL1 : ILandingGear
    {
        readonly LandingGearProfile _p;

        public LandingGearL1(in LandingGearProfile profile)
        {
            profile.Validate();
            _p = profile;
        }

        public Vector3[] HardPoints => _p.HardPointsM;

        public float DragIncrement(in AirframeState s) => _p.GearDownDeltaCD * s.GearDeployed;

        public GearLoads Step(ref AirframeState s, in PilotInputs inputs, IContactProbe probe, float dt)
        {
            bool wantDown = inputs.Has(PilotSwitches.GearDown) || s.InContact;   // weight on wheels: it stays down
            s.GearDeployed = Mathf.MoveTowards(s.GearDeployed, wantDown ? 1f : 0f, dt / _p.TransitTimeS);
            s.InContact = false;
            if (s.GearDeployed < 1f || probe == null) return default;            // only down and locked takes a load

            Quaternion att = s.Attitude;
            Vector3 up = att * Vector3.up;
            Vector3 omegaWorld = att * s.BodyRates;
            Quaternion toBody = Quaternion.Inverse(att);
            Vector3 force = Vector3.zero, moment = Vector3.zero;
            float speed = s.Velocity.magnitude;
            float steerGain = 1f - Mathf.InverseLerp(_p.SteerFadeStartMps, _p.SteerFadeEndMps, speed);

            foreach (WheelProfile w in _p.Wheels)
            {
                Vector3 top = s.Position + att * (w.ContactPointM + Vector3.up * w.TravelM);
                if (!probe.Cast(top, -up, w.TravelM, out float d, out Vector3 ground)) continue;   // this wheel is in the air
                if (d <= 0f) return new GearLoads(Vector3.zero, Vector3.zero, $"{w.Name} driven through its strut");

                Vector3 r = att * w.ContactPointM;
                Vector3 vPoint = s.Velocity + Vector3.Cross(omegaWorld, r);
                float compression = w.TravelM - d;
                // The sink is the tyre's speed into the ground, along the ground's normal: not along the strut, which
                // tilts with the jet (slice 2d: at 8° nose up and 150 kt the strut axis saw 10 m/s of forward speed).
                float sink = -Vector3.Dot(vPoint, ground);
                if (sink > _p.MaxSinkRateMps)
                    return new GearLoads(Vector3.zero, Vector3.zero, $"{w.Name} met the ground at {sink:F1} m/s ({sink / 0.3048f:F0} ft/s)");
                float rate = sink / Mathf.Max(Vector3.Dot(up, ground), 0.2f);    // the strut's compression rate
                float normal = Mathf.Max(0f, w.SpringNPerM * compression + w.DamperNsPerM * rate);

                // The tyre's directions lie in the ground's plane.
                Vector3 heading = att * Vector3.forward;
                if (w.Steerable) heading = Quaternion.AngleAxis(inputs.Pedals * _p.SteerMaxRad * steerGain * Units.RadToDeg, up) * heading;
                Vector3 fwd = Vector3.ProjectOnPlane(heading, ground).normalized;
                Vector3 side = Vector3.Cross(ground, fwd);
                float vLong = Vector3.Dot(vPoint, fwd), vSide = Vector3.Dot(vPoint, side);
                float muLong = w.Braked && inputs.Brakes ? _p.BrakingFriction : _p.RollingFriction;
                float fLong = -Mathf.Clamp(vLong / _p.SlipSpeedMps, -1f, 1f) * muLong * normal;
                float fSide = -Mathf.Clamp(vSide / _p.SlipSpeedMps, -1f, 1f) * _p.LateralFriction * normal;
                Vector3 f = up * normal + fwd * fLong + side * fSide;

                force += f;
                Vector3 rN = AirframeL1.ToNasa(w.ContactPointM), fN = AirframeL1.ToNasa(toBody * f);
                moment += Vector3.Cross(rN, fN);                                 // the NASA frame is right-handed
                s.InContact = true;
            }
            return new GearLoads(AirframeL1.ToNasa(toBody * force), moment, null);
        }
    }
}
