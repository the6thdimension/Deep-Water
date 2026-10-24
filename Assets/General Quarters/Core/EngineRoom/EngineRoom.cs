using System;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Helm;
using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.EngineRoom
{
    /// <summary>The Engine Room's machinery and the hull's hydrodynamics, as one profile (built from an Engine Room profile asset).</summary>
    [Serializable]
    public struct EngineRoomProfileData
    {
        public MmgHullData Hull;
        public MmgPropellerData Port, Starboard;
        public MmgRudderData PortRudder, StarboardRudder;
        /// <summary>Shaft speed limits, rev/s (astern negative).</summary>
        public float MaxShaftRps, MinShaftRps;
        /// <summary>How fast a shaft's speed can change, rev/s per second (gas turbine and shaft line response).</summary>
        public float ShaftRateRpsPerS;
        public float MaxRudderDeg;
        /// <summary>Steering gear rate, degrees per second.</summary>
        public float RudderRateDegPerS;
    }

    public interface IEngineRoom : IShipAnswers
    {
        FidelityResolution Fidelity { get; }
        float PortShaftRps { get; }
        float StarboardShaftRps { get; }
        float PortRudderDeg { get; }
        float StarboardRudderDeg { get; }
        /// <summary>Move the shafts and rudders toward the Helm's orders over <paramref name="dt"/>, then the forces for <paramref name="state"/>.</summary>
        MmgResult Step(in HelmOutput orders, in MmgState state, float dt);
    }

    public static class EngineRoomFactory
    {
        public static IEngineRoom Create(FidelityLevel requested, in EngineRoomProfileData profile)
        {
            FidelityResolution r = FidelityLadder.Resolve(ElementKind.EngineRoom, requested);
            return new EngineRoomL0(r, profile);
        }
    }

    /// <summary>
    /// Engine Room L0 (BLUEPRINT §4): two shafts with a thrust curve, twin rudders with lift and stall,
    /// resistance from a speed–resistance table, through the MMG standard method. The shafts follow the
    /// ordered speed at a rate limit; the rudders follow the ordered angle at the steering gear's rate.
    /// No plant yet (four LM2500s, controllable pitch and fuel are L1): shaft speed is ordered directly.
    /// </summary>
    public sealed class EngineRoomL0 : IEngineRoom
    {
        // Turning table: steady radius by approach speed × rudder angle, from 3-DOF runs of this same model.
        static readonly float[] TableSpeedsKn = { 4f, 8f, 12f, 16f, 20f, 24f, 28f, 32f };
        static readonly float[] TableRuddersDeg = { 5f, 10f, 15f, 20f, 25f, 30f, 35f };
        const float KnToMs = 0.514444f;

        readonly EngineRoomProfileData _p;
        float[,] _radius;

        public FidelityResolution Fidelity { get; }
        public float PortShaftRps { get; private set; }
        public float StarboardShaftRps { get; private set; }
        public float PortRudderDeg { get; private set; }
        public float StarboardRudderDeg { get; private set; }
        public float MaxRudderDeg => _p.MaxRudderDeg;
        public float MaxShaftRps => _p.MaxShaftRps;
        public float MinShaftRps => _p.MinShaftRps;

        public EngineRoomL0(FidelityResolution fidelity, in EngineRoomProfileData profile)
        {
            Fidelity = fidelity;
            _p = profile;
        }

        /// <summary>Start already running: shafts at <paramref name="rps"/>, rudders amidships.</summary>
        public void SetShafts(float rps) { PortShaftRps = StarboardShaftRps = rps; }

        public float SteadyShaftRps(float speedMs)
        {
            if (speedMs > 0f) return Mathf.Min(Mmg.SteadyShaftRps(_p.Hull, _p.Port, 2, speedMs), _p.MaxShaftRps);
            if (speedMs == 0f) return 0f;
            // Astern: the L0 astern thrust ρn²D⁴·f·K0 per shaft balances the resistance at |u|.
            MmgPropellerData p = _p.Port;
            float d4 = p.DiameterM * p.DiameterM * p.DiameterM * p.DiameterM;
            float perShaft = Mmg.Resistance(_p.Hull, -speedMs) / (2f * (1f - p.ThrustDeduction));
            return Mathf.Max(-Mathf.Sqrt(perShaft / (Mmg.Rho * d4 * p.AsternThrustFraction * p.K0)), _p.MinShaftRps);
        }

        public float TurningRadiusM(float speedMs, float rudderDeg)
        {
            if (_radius == null) _radius = BuildTurningTable(_p);
            float kn = Mathf.Clamp(speedMs / KnToMs, TableSpeedsKn[0], TableSpeedsKn[TableSpeedsKn.Length - 1]);
            float deg = Mathf.Clamp(Mathf.Abs(rudderDeg), TableRuddersDeg[0], TableRuddersDeg[TableRuddersDeg.Length - 1]);
            return Bilinear(_radius, TableSpeedsKn, TableRuddersDeg, kn, deg);
        }

        public MmgResult Step(in HelmOutput orders, in MmgState state, float dt)
        {
            float rpsOrder = Mathf.Clamp(orders.ShaftOrderRps, _p.MinShaftRps, _p.MaxShaftRps);
            float rudderOrder = Mathf.Clamp(orders.RudderOrderDeg, -_p.MaxRudderDeg, _p.MaxRudderDeg);
            PortShaftRps = Mathf.MoveTowards(PortShaftRps, rpsOrder, _p.ShaftRateRpsPerS * dt);
            StarboardShaftRps = Mathf.MoveTowards(StarboardShaftRps, rpsOrder, _p.ShaftRateRpsPerS * dt);
            PortRudderDeg = Mathf.MoveTowards(PortRudderDeg, rudderOrder, _p.RudderRateDegPerS * dt);
            StarboardRudderDeg = Mathf.MoveTowards(StarboardRudderDeg, rudderOrder, _p.RudderRateDegPerS * dt);
            return Evaluate(state);
        }

        public MmgResult Evaluate(in MmgState state) => Mmg.Evaluate(_p.Hull, _p.Port, _p.Starboard, _p.PortRudder, _p.StarboardRudder, state,
            PortShaftRps, StarboardShaftRps, PortRudderDeg * Mathf.Deg2Rad, StarboardRudderDeg * Mathf.Deg2Rad);

        static float[,] BuildTurningTable(in EngineRoomProfileData p)
        {
            var table = new float[TableSpeedsKn.Length, TableRuddersDeg.Length];
            for (int i = 0; i < TableSpeedsKn.Length; i++)
            for (int j = 0; j < TableRuddersDeg.Length; j++)
            {
                TurningCircle tc = ManoeuvringSim.TurningCircle(p, TableSpeedsKn[i] * KnToMs, TableRuddersDeg[j], 0.1f, 600f);
                table[i, j] = tc.SteadyRadiusM;
            }
            return table;
        }

        static float Bilinear(float[,] t, float[] xs, float[] ys, float x, float y)
        {
            int i = 0; while (i < xs.Length - 2 && xs[i + 1] < x) i++;
            int j = 0; while (j < ys.Length - 2 && ys[j + 1] < y) j++;
            float tx = (x - xs[i]) / (xs[i + 1] - xs[i]), ty = (y - ys[j]) / (ys[j + 1] - ys[j]);
            float a = Mathf.Lerp(t[i, j], t[i + 1, j], tx), b = Mathf.Lerp(t[i, j + 1], t[i + 1, j + 1], tx);
            return Mathf.Lerp(a, b, ty);
        }
    }

    /// <summary>What a turning-circle manoeuvre measured (IMO MSC.137(76) terms).</summary>
    public struct TurningCircle
    {
        /// <summary>Distance run along the original course when the heading has changed 90°, m.</summary>
        public float AdvanceM;
        /// <summary>Lateral distance when the heading has changed 90°, m.</summary>
        public float TransferM;
        /// <summary>Lateral distance when the heading has changed 180°, m.</summary>
        public float TacticalDiameterM;
        /// <summary>Radius of the steady turn, from U/r once the turn has settled, m.</summary>
        public float SteadyRadiusM;
        /// <summary>Speed in the steady turn, m/s, and drift angle there, degrees.</summary>
        public float SteadySpeedMs, SteadyDriftDeg;
    }

    /// <summary>What a zig-zag manoeuvre measured.</summary>
    public struct ZigZag
    {
        public float FirstOvershootDeg, SecondOvershootDeg;
    }

    /// <summary>
    /// A pure 3-DOF manoeuvring integrator over <see cref="Mmg.Evaluate"/>: for tests, for the Engine Room's own
    /// tables, and for comparing with free-running data. The PhysX ship uses the same forces; this runs them
    /// without the sea (semi-implicit Euler at a fixed step).
    /// </summary>
    public static class ManoeuvringSim
    {
        public struct State
        {
            public float X, Z, HeadingRad;
            public MmgState Body;
        }

        /// <summary>Start a straight run at <paramref name="speedMs"/>, then hold <paramref name="rudderDeg"/> at the steering gear's rate.</summary>
        public static TurningCircle TurningCircle(in EngineRoomProfileData p, float speedMs, float rudderDeg, float dt, float maxSeconds)
        {
            var room = new EngineRoomL0(default, p);
            float rps = room.SteadyShaftRps(speedMs);
            room.SetShafts(rps);
            var s = new State { Body = new MmgState { U = speedMs } };
            var orders = new HelmOutput { ShaftOrderRps = rps, RudderOrderDeg = rudderDeg };
            var tc = new TurningCircle { AdvanceM = float.NaN, TransferM = float.NaN, TacticalDiameterM = float.NaN };
            float turned = 0f, lastRadius = float.NaN;
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                MmgResult f = room.Step(orders, s.Body, dt);
                Integrate(ref s, f, dt, out float dHeading);
                turned += Mathf.Abs(dHeading);
                if (float.IsNaN(tc.AdvanceM) && turned >= 0.5f * Mathf.PI) { tc.AdvanceM = s.Z; tc.TransferM = Mathf.Abs(s.X); }
                if (float.IsNaN(tc.TacticalDiameterM) && turned >= Mathf.PI) tc.TacticalDiameterM = Mathf.Abs(s.X);
                if (turned >= 3f * Mathf.PI)
                {
                    float U = Mathf.Sqrt(s.Body.U * s.Body.U + s.Body.Vm * s.Body.Vm);
                    lastRadius = U / Mathf.Max(Mathf.Abs(s.Body.R), 1e-6f);
                    tc.SteadySpeedMs = U;
                    tc.SteadyDriftDeg = Mathf.Atan2(-s.Body.Vm, s.Body.U) * Mathf.Rad2Deg;
                    break;
                }
            }
            tc.SteadyRadiusM = float.IsNaN(lastRadius) ? float.PositiveInfinity : lastRadius;
            return tc;
        }

        /// <summary>A δ/ψ zig-zag (e.g. 10/10) at <paramref name="speedMs"/>: first and second overshoot angles.</summary>
        public static ZigZag ZigZagTest(in EngineRoomProfileData p, float speedMs, float rudderDeg, float headingDeg, float dt)
        {
            var room = new EngineRoomL0(default, p);
            float rps = room.SteadyShaftRps(speedMs);
            room.SetShafts(rps);
            var s = new State { Body = new MmgState { U = speedMs } };
            float target = headingDeg * Mathf.Deg2Rad, sign = 1f;
            var orders = new HelmOutput { ShaftOrderRps = rps, RudderOrderDeg = rudderDeg };
            int switches = 0;
            float peak = 0f;
            var z = new ZigZag();
            for (float t = 0f; t < 600f && switches < 3; t += dt)
            {
                MmgResult f = room.Step(orders, s.Body, dt);
                Integrate(ref s, f, dt, out _);
                float psi = s.HeadingRad;
                if (sign > 0f ? psi >= target : psi <= -target)
                {
                    // Execute: swing the rudder the other way, then measure the overshoot past this heading.
                    if (switches == 1) z.FirstOvershootDeg = peak;
                    if (switches == 2) z.SecondOvershootDeg = peak;
                    sign = -sign;
                    orders.RudderOrderDeg = sign * rudderDeg;
                    switches++;
                    peak = 0f;
                }
                if (switches > 0) peak = Mathf.Max(peak, (Mathf.Abs(psi) - target) * Mathf.Rad2Deg);
            }
            return z;
        }

        /// <summary>One semi-implicit Euler step of the horizontal-plane motion.</summary>
        public static void Integrate(ref State s, in MmgResult f, float dt, out float dHeading)
        {
            s.Body.U += f.Udot * dt;
            s.Body.Vm += f.Vdot * dt;
            s.Body.R += f.Rdot * dt;
            dHeading = s.Body.R * dt;
            s.HeadingRad += dHeading;
            float sin = Mathf.Sin(s.HeadingRad), cos = Mathf.Cos(s.HeadingRad);
            // Heading measured clockwise from +Z: forward = (sin ψ, cos ψ), starboard = (cos ψ, −sin ψ).
            s.X += (s.Body.U * sin + s.Body.Vm * cos) * dt;
            s.Z += (s.Body.U * cos - s.Body.Vm * sin) * dt;
        }
    }
}
