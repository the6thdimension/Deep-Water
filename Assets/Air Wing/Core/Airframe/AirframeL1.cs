using System;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.FlightControls;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.Airframe
{
    /// <summary>The six aerodynamic coefficients at one instant (W32): body moments, then side force, lift and drag.</summary>
    public readonly struct AeroCoefficients
    {
        public readonly float Cl, Cm, Cn, CY, CLift, CDrag;

        public AeroCoefficients(float cl, float cm, float cn, float cy, float cLift, float cDrag)
        {
            Cl = cl; Cm = cm; Cn = cn; CY = cy; CLift = cLift; CDrag = cDrag;
        }
    }

    /// <summary>
    /// Airframe L1 (W3, W32, W33): a rigid body in six degrees of freedom. The surfaces, the angles of attack and
    /// sideslip and the body rates give the six coefficients from the published F/A-18 HARV fits; lift, drag and side
    /// force act in the wind axes, the moments about the centre of mass; thrust acts along the body; gravity acts down.
    /// Euler's equations carry the full inertia, Ixz included. Nothing limits the angle of attack here: the fly-by-wire
    /// does that (2b), and an airframe pushed past its corners departs (W33).
    /// <para>Two frames meet here. The aero model works in the NASA body frame (x forward, y right, z down; p roll
    /// right, q pitch up, r yaw right). The state is in Unity's (x right, y up, z forward), and
    /// <see cref="AirframeState.BodyRates"/> holds Unity's local angular velocity, (−q, r, −p).</para>
    /// <para>Not here yet: flaps and speed brake come from the L0 increments (AUTHORED), and Mach and ground effect wait
    /// for L2.</para>
    /// </summary>
    public sealed class AirframeL1 : IAirframe
    {
        const float G0 = Units.StandardGravity;
        const float MinSpeedMps = 1f;

        readonly AirframeL1Profile _p;
        readonly AirframeProfile _a;

        public AirframeL1(in AirframeProfile airframe, in AirframeL1Profile l1)
        {
            if (!l1.IsSet) throw new ArgumentException("this aircraft type has no L1 aero data (AirframeL1Profile)");
            airframe.Validate();
            l1.Validate();
            _a = airframe;
            _p = l1;
        }

        static float Poly(float[] c, float a)
        {
            float r = 0f;
            for (int i = c.Length - 1; i >= 0; i--) r = r * a + c[i];   // Horner
            return r;
        }

        /// <summary>
        /// The coefficients of the HARV model (its f18full.m, term for term) at airspeed <paramref name="v"/> m/s, the
        /// aero angles and NASA-frame body rates in radians, and the surfaces. α is clamped to the profile's range for
        /// the polynomials only.
        /// </summary>
        public static AeroCoefficients Coefficients(in AirframeL1Profile p, float v, float beta, float alpha, float pR, float qR, float rR, in ControlSurfaces s)
        {
            AeroPolynomialsL1 c = p.Aero;
            float a = alpha < p.AlphaMinRad ? p.AlphaMinRad : alpha > p.AlphaMaxRad ? p.AlphaMaxRad : alpha;
            float bOver2V = p.SpanM / (2f * v), cOver2V = p.ChordM / (2f * v);
            float ail = s.AileronRad, rud = s.RudderRad, stab = s.StabilatorRad;

            float cl = Poly(c.Clb, a) * beta + Poly(c.Clda, a) * ail + Poly(c.Cldr, a) * rud
                     + Poly(c.Clp, a) * pR * bOver2V + Poly(c.Clr, a) * rR * bOver2V;
            float cn = Poly(c.Cnb, a) * beta + Poly(c.Cnda, a) * ail + Poly(c.Cndr, a) * rud
                     + Poly(c.Cnr, a) * rR * bOver2V + Poly(c.Cnp, a) * pR * bOver2V;
            float cy = Poly(c.Cyb, a) * beta + Poly(c.Cydr, a) * rud + Poly(c.Cyda, a) * ail;
            float cm = Poly(c.Cma, a) + Poly(c.Cmds, a) * stab + Poly(c.Cmq, a) * qR * cOver2V;
            float cLift = Poly(c.CL, a) * Mathf.Cos(2f * beta / 3f) + Poly(c.CLds, a) * stab;
            float cDrag = Poly(c.CD, a) * Mathf.Cos(beta) + c.CD0 + Poly(c.Cdds, a) * stab;
            return new AeroCoefficients(cl, cm, cn, cy, cLift, cDrag);
        }

        /// <summary>
        /// Aerodynamic force plus thrust (newtons) and moment (newton-metres) in the NASA body frame, for body velocity
        /// <paramref name="uvw"/> (m/s, NASA frame) and body rates <paramref name="pqr"/> (rad/s, NASA frame). Gravity is
        /// not included. <paramref name="extraCL"/> and <paramref name="extraCD"/> add flap and speed-brake increments.
        /// </summary>
        public static void ForcesAndMoments(in AirframeL1Profile p, float density, Vector3 uvw, Vector3 pqr, in ControlSurfaces s,
                                            float thrustN, float extraCL, float extraCD,
                                            out Vector3 force, out Vector3 moment, out float alpha, out float beta)
        {
            float v = uvw.magnitude;
            if (v < MinSpeedMps)
            {
                force = new Vector3(thrustN, 0f, 0f);
                moment = Vector3.zero;
                alpha = beta = 0f;
                return;
            }
            alpha = Mathf.Atan2(uvw.z, uvw.x);
            beta = Mathf.Asin(Mathf.Clamp(uvw.y / v, -1f, 1f));
            AeroCoefficients c = Coefficients(p, v, beta, alpha, pqr.x, pqr.y, pqr.z, s);

            float qS = 0.5f * density * v * v * p.ReferenceAreaM2;
            float cb = Mathf.Cos(beta), sb = Mathf.Sin(beta), ca = Mathf.Cos(alpha), sa = Mathf.Sin(alpha);
            float cDrag = c.CDrag + extraCD;
            // Wind-axis drag and side force, as the HARV model forms them; then wind axes to body (Stevens and Lewis).
            float dragW = qS * (cDrag * cb - c.CY * sb);
            float sideW = qS * (c.CY * cb + cDrag * sb);
            float lift = qS * (c.CLift + extraCL);
            force = new Vector3(
                -dragW * ca * cb - sideW * ca * sb + lift * sa + thrustN,
                -dragW * sb + sideW * cb,
                -dragW * sa * cb - sideW * sa * sb - lift * ca);
            moment = new Vector3(qS * p.SpanM * c.Cl, qS * p.ChordM * c.Cm, qS * p.SpanM * c.Cn);
        }

        /// <summary>Euler's equations with the product of inertia (the HARV model's form): ṗ, q̇, ṙ in rad/s².</summary>
        public static Vector3 AngularAcceleration(in AirframeL1Profile p, Vector3 moment, Vector3 pqr)
        {
            float ixx = p.IxxKgM2, iyy = p.IyyKgM2, izz = p.IzzKgM2, ixz = p.IxzKgM2;
            float pR = pqr.x, qR = pqr.y, rR = pqr.z, L = moment.x, M = moment.y, N = moment.z;
            float den = ixx * izz - ixz * ixz;
            float pd = (izz * L + ixz * N - (ixz * (iyy - ixx - izz) * pR + (ixz * ixz + izz * (izz - iyy)) * rR) * qR) / den;
            float qd = (M + (izz - ixx) * pR * rR + (rR * rR - pR * pR) * ixz) / iyy;
            float rd = (ixz * L + ixx * N + (ixz * (iyy - ixx - izz) * rR + (ixz * ixz + ixx * (ixx - iyy)) * pR) * qR) / den;
            return new Vector3(pd, qd, rd);
        }

        /// <summary>Unity local (x right, y up, z forward) to the NASA body frame (x forward, y right, z down).</summary>
        public static Vector3 ToNasa(Vector3 unityLocal) => new Vector3(unityLocal.z, unityLocal.x, -unityLocal.y);
        /// <summary>The NASA body frame to Unity local.</summary>
        public static Vector3 ToUnity(Vector3 nasa) => new Vector3(nasa.y, -nasa.z, nasa.x);
        /// <summary>Unity's local angular velocity (x, y, z) to NASA (p, q, r): p = −ωz, q = −ωx, r = ωy.</summary>
        public static Vector3 RatesToNasa(Vector3 omegaUnity) => new Vector3(-omegaUnity.z, -omegaUnity.x, omegaUnity.y);
        /// <summary>NASA (p, q, r) to Unity's local angular velocity.</summary>
        public static Vector3 RatesToUnity(Vector3 pqr) => new Vector3(-pqr.y, pqr.z, -pqr.x);

        /// <summary>
        /// One substep by the midpoint method (second order): slopes at the start carry the state half a substep on,
        /// and the slopes there carry it the whole way. It costs two force evaluations a substep. Semi-implicit Euler,
        /// first order, gained 1.2 % of rotational energy in 5 s of torque-free tumbling at 200 Hz; the midpoint method
        /// gains 1.7e-7 (slice 2a).
        /// </summary>
        public void Step(ref AirframeState s, in ControlDemand demand, float thrustN, in AirData air, in PilotInputs inputs, float dt)
        {
            float extraCL = inputs.Has(PilotSwitches.FlapsDown) ? _a.FlapsDeltaCLmax : 0f;
            float extraCD = (inputs.Has(PilotSwitches.FlapsDown) ? _a.FlapsDeltaCD : 0f) + (inputs.Has(PilotSwitches.SpeedBrake) ? _a.SpeedBrakeDeltaCD : 0f);
            float h = 0.5f * dt;

            Quaternion att0 = s.Attitude;
            Vector3 v0 = s.Velocity;
            Vector3 pqr0 = RatesToNasa(s.BodyRates);
            Slopes(att0, v0, pqr0, s.MassKg, demand.Surfaces, thrustN, air.DensityKgM3, extraCL, extraCD, out Vector3 a1, out Vector3 wd1, out _, out _);

            Quaternion attM = Turn(att0, pqr0, h);
            Vector3 vM = v0 + a1 * h;
            Vector3 pqrM = pqr0 + wd1 * h;
            Slopes(attM, vM, pqrM, s.MassKg, demand.Surfaces, thrustN, air.DensityKgM3, extraCL, extraCD, out Vector3 a2, out Vector3 wd2, out float alpha, out float fz);

            s.Position += vM * dt;
            s.Velocity = v0 + a2 * dt;
            Vector3 pqr = pqr0 + wd2 * dt;
            s.Attitude = Turn(att0, pqrM, dt);
            s.BodyRates = RatesToUnity(pqr);
            s.AngleOfAttack = alpha;
            s.LoadFactor = -fz / (s.MassKg * G0);   // body-normal load factor at the midpoint: lift and thrust's normal part, in g
        }

        /// <summary>The world acceleration (gravity included) and the body angular acceleration (NASA frame) at one state.</summary>
        void Slopes(Quaternion att, Vector3 velocity, Vector3 pqr, float massKg, in ControlSurfaces surfaces, float thrustN, float density,
                    float extraCL, float extraCD, out Vector3 accel, out Vector3 angularAccel, out float alpha, out float bodyFz)
        {
            Vector3 uvw = ToNasa(Quaternion.Inverse(att) * velocity);
            ForcesAndMoments(_p, density, uvw, pqr, surfaces, thrustN, extraCL, extraCD, out Vector3 force, out Vector3 moment, out alpha, out _);
            accel = att * ToUnity(force) / massKg + Vector3.down * G0;
            angularAccel = AngularAcceleration(_p, moment, pqr);
            bodyFz = force.z;
        }

        /// <summary>The attitude turned by the body rates <paramref name="pqr"/> (NASA frame) held for <paramref name="dt"/>.</summary>
        static Quaternion Turn(Quaternion att, Vector3 pqr, float dt)
        {
            Vector3 omega = RatesToUnity(pqr);
            float rate = omega.magnitude;
            return rate * dt > 0f ? (att * Quaternion.AngleAxis(rate * dt * Units.RadToDeg, omega / rate)).normalized : att;
        }
    }
}
