using System;
using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.EngineRoom
{
    /// <summary>
    /// Hull hydrodynamics for the MMG standard method (Yasukawa &amp; Yoshimura 2015, J. Mar. Sci. Technol.
    /// 20:37–52). Non-dimensional derivatives use ½ρ·Lpp·d·U² for forces and ½ρ·Lpp²·d·U² for the yaw moment,
    /// with v' = v_m/U and r' = r·Lpp/U (v_m is the sway velocity at midship).
    /// </summary>
    [Serializable]
    public struct MmgHullData
    {
        public float LppM, BeamM, DraftM;
        public float MassKg;
        /// <summary>Centre of gravity forward of midship, m.</summary>
        public float XgM;
        /// <summary>Yaw moment of inertia about the centre of gravity, kg·m².</summary>
        public float IzzKgM2;
        /// <summary>Added mass in surge and sway, and added yaw inertia, non-dimensional (by ½ρLpp²d and ½ρLpp⁴d).</summary>
        public float MxNd, MyNd, JzNd;
        /// <summary>Straight-run resistance, N, tabulated against speed (m/s) at <see cref="ResistanceSpeedMs"/>.</summary>
        public float[] ResistanceSpeedMs, ResistanceN;
        /// <summary>
        /// Hull derivatives measured at several Froude numbers, ascending. A destroyer's derivatives change with
        /// speed (5415's Y'vvv doubles from Fn 0.28 to 0.41), so the model interpolates between the sets and
        /// holds the end sets beyond them.
        /// </summary>
        public MmgHullDerivatives[] Derivatives;
    }

    /// <summary>One set of MMG hull derivatives, measured at Froude number <see cref="Froude"/> (on Lpp).</summary>
    [Serializable]
    public struct MmgHullDerivatives
    {
        public float Froude;
        public float Xvv, Xvr, Xrr, Xvvvv;
        public float Yv, Yr, Yvvv, Yvvr, Yvrr, Yrrr;
        public float Nv, Nr, Nvvv, Nvvr, Nvrr, Nrrr;

        public static MmgHullDerivatives At(MmgHullDerivatives[] sets, float froude)
        {
            if (froude <= sets[0].Froude) return sets[0];
            for (int i = 1; i < sets.Length; i++)
            {
                if (froude > sets[i].Froude) continue;
                MmgHullDerivatives a = sets[i - 1], b = sets[i];
                float t = (froude - a.Froude) / (b.Froude - a.Froude);
                return new MmgHullDerivatives
                {
                    Froude = froude,
                    Xvv = Mathf.Lerp(a.Xvv, b.Xvv, t), Xvr = Mathf.Lerp(a.Xvr, b.Xvr, t), Xrr = Mathf.Lerp(a.Xrr, b.Xrr, t), Xvvvv = Mathf.Lerp(a.Xvvvv, b.Xvvvv, t),
                    Yv = Mathf.Lerp(a.Yv, b.Yv, t), Yr = Mathf.Lerp(a.Yr, b.Yr, t), Yvvv = Mathf.Lerp(a.Yvvv, b.Yvvv, t),
                    Yvvr = Mathf.Lerp(a.Yvvr, b.Yvvr, t), Yvrr = Mathf.Lerp(a.Yvrr, b.Yvrr, t), Yrrr = Mathf.Lerp(a.Yrrr, b.Yrrr, t),
                    Nv = Mathf.Lerp(a.Nv, b.Nv, t), Nr = Mathf.Lerp(a.Nr, b.Nr, t), Nvvv = Mathf.Lerp(a.Nvvv, b.Nvvv, t),
                    Nvvr = Mathf.Lerp(a.Nvvr, b.Nvvr, t), Nvrr = Mathf.Lerp(a.Nvrr, b.Nvrr, t), Nrrr = Mathf.Lerp(a.Nrrr, b.Nrrr, t),
                };
            }
            return sets[sets.Length - 1];
        }
    }

    /// <summary>One propeller (MMG standard method §2.3, with the K_T(J) fit below).</summary>
    [Serializable]
    public struct MmgPropellerData
    {
        public float DiameterM;
        /// <summary>K_T = k0 + k1·J + k2·J².</summary>
        public float K0, K1, K2;
        /// <summary>K_Q = q0 + q1·J + q2·J², for shaft power.</summary>
        public float Q0, Q1, Q2;
        /// <summary>Effective wake fraction in straight running, and thrust deduction.</summary>
        public float WakeStraight, ThrustDeduction;
        /// <summary>Longitudinal position non-dimensionalised by Lpp (aft negative), for the inflow angle β_P = β − x'_P·r'.</summary>
        public float XpNd;
        /// <summary>Wake change with inflow angle: (1−w_P)/(1−w_P0) = 1 + (1 − e^(−C1|β_P|))(C2 − 1).</summary>
        public float C1, C2Positive, C2Negative;
        /// <summary>Lateral offset, m (starboard positive): twin shafts at different speeds yaw the ship.</summary>
        public float YpM;
        /// <summary>Astern: K_T as a fraction of the ahead fit (AUTHORED at L0; CPP pitch reversal is L1).</summary>
        public float AsternThrustFraction;
    }

    /// <summary>One rudder (MMG standard method §2.4), with a stall the standard method leaves out.</summary>
    [Serializable]
    public struct MmgRudderData
    {
        public float AreaM2, SpanM;
        /// <summary>Normal-force gradient f_α, per radian (Fujii: 6.13Λ/(Λ + 2.25)).</summary>
        public float FAlpha;
        /// <summary>Flow straightening and propeller-race factors.</summary>
        public float Epsilon, Kappa, GammaRPositive, GammaRNegative;
        /// <summary>Effective longitudinal position of the rudder for the inflow angle, by Lpp.</summary>
        public float LrNd;
        /// <summary>Rudder–hull interaction: resistance deduction t_R, a_H, and x'_H (by Lpp).</summary>
        public float Tr, Ah, XhNd;
        /// <summary>Longitudinal position of the rudder force, by Lpp (aft negative).</summary>
        public float XrNd;
        /// <summary>Lateral offset, m (starboard positive).</summary>
        public float YrM;
        /// <summary>Stall: past this inflow angle the normal force falls away to the flat-plate value.</summary>
        public float StallAngleDeg;
        /// <summary>Post-stall normal force as a fraction of the value at the stall angle.</summary>
        public float PostStallFraction;
        /// <summary>Height of the centre of pressure below the waterline, m (for the heel it causes).</summary>
        public float DepthM;
    }

    /// <summary>Horizontal-plane state, MMG frame: surge u forward, sway v_m to starboard at midship, yaw rate r to starboard.</summary>
    public struct MmgState
    {
        public float U, Vm, R;
    }

    /// <summary>Forces and the accelerations they give, at one instant.</summary>
    public struct MmgResult
    {
        /// <summary>Total surge force, sway force (N) and yaw moment about midship (N·m).</summary>
        public float X, Y, N;
        /// <summary>
        /// The hydrodynamic sway forces of the hull and of each rudder, N. They act below the centre of gravity
        /// (the hull's at about half draught, each rudder's at its depth), so a ship turning heels outward.
        /// </summary>
        public float YHull, YRudder0, YRudder1;
        /// <summary>du/dt, dv_m/dt, dr/dt from the MMG equations of motion with added mass.</summary>
        public float Udot, Vdot, Rdot;
        /// <summary>Per-shaft thrust, N; per-rudder inflow angle, degrees, and whether it is stalled.</summary>
        public float Thrust0, Thrust1, RudderAlpha0Deg, RudderAlpha1Deg;
        public bool Stalled0, Stalled1;
    }

    /// <summary>
    /// The MMG standard method, twin screw and twin rudder: hull + Σ propellers + Σ rudders, pure functions.
    /// Each rudder sits in its own propeller's race. The standard method's equations are for one screw and
    /// one rudder; summing per-side terms is the usual first step for twin-screw ships (rudder–rudder
    /// interaction is left out at L0, and said so).
    /// </summary>
    public static class Mmg
    {
        public const float Rho = 1025f;

        /// <summary>Forces and accelerations for state <paramref name="s"/>, shaft speeds (rev/s) and rudder angles (radians, positive to starboard).</summary>
        public static MmgResult Evaluate(in MmgHullData h, in MmgPropellerData p0, in MmgPropellerData p1,
            in MmgRudderData r0, in MmgRudderData r1, in MmgState s, float n0, float n1, float delta0, float delta1)
        {
            float L = h.LppM, d = h.DraftM;
            float U = Mathf.Sqrt(s.U * s.U + s.Vm * s.Vm);
            var result = new MmgResult();

            // ── Hull ──
            float vNd = 0f, rNd = 0f, beta = 0f;
            // Resistance at the total speed U (the standard method's −R'0·½ρLdU²), opposing the surge direction.
            float xh = -Mathf.Sign(s.U) * Resistance(h, U);
            float yh = 0f, nh = 0f;
            if (U > 1e-3f)
            {
                vNd = s.Vm / U;
                rNd = s.R * L / U;
                beta = Mathf.Atan2(-s.Vm, s.U);   // drift angle, MMG sign: β = tan⁻¹(−v_m/u)
                float qf = 0.5f * Rho * L * d * U * U;
                float v = vNd, r = rNd;
                MmgHullDerivatives k = MmgHullDerivatives.At(h.Derivatives, U / Mathf.Sqrt(9.81f * L));
                xh += qf * (k.Xvv * v * v + k.Xvr * v * r + k.Xrr * r * r + k.Xvvvv * v * v * v * v);
                yh = qf * (k.Yv * v + k.Yr * r + k.Yvvv * v * v * v + k.Yvvr * v * v * r + k.Yvrr * v * r * r + k.Yrrr * r * r * r);
                nh = qf * L * (k.Nv * v + k.Nr * r + k.Nvvv * v * v * v + k.Nvvr * v * v * r + k.Nvrr * v * r * r + k.Nrrr * r * r * r);
            }

            // ── Propellers and rudders, side by side ──
            Side(h, p0, r0, s, U, beta, rNd, n0, delta0, out float xp0, out float np0, out float xr0, out float yr0, out float nr0, out result.Thrust0, out result.RudderAlpha0Deg, out result.Stalled0);
            Side(h, p1, r1, s, U, beta, rNd, n1, delta1, out float xp1, out float np1, out float xr1, out float yr1, out float nr1, out result.Thrust1, out result.RudderAlpha1Deg, out result.Stalled1);

            result.X = xh + xp0 + xp1 + xr0 + xr1;
            result.Y = yh + yr0 + yr1;
            result.N = nh + np0 + np1 + nr0 + nr1;
            result.YHull = yh;
            result.YRudder0 = yr0;
            result.YRudder1 = yr1;

            // ── Equations of motion (MMG eq. 1), solved for the accelerations ──
            float half = 0.5f * Rho;
            float m = h.MassKg;
            float mx = h.MxNd * half * L * L * d;
            float my = h.MyNd * half * L * L * d;
            float jz = h.JzNd * half * L * L * L * L * d;
            float xg = h.XgM;
            // (m + mx) u̇ − (m + my) v r − xG m r² = X
            result.Udot = (result.X + (m + my) * s.Vm * s.R + xg * m * s.R * s.R) / (m + mx);
            // (m + my) v̇ + xG m ṙ = Y − (m + mx) u r
            // xG m v̇ + (IzG + xG² m + Jz) ṙ = N − xG m u r
            float a11 = m + my, a12 = xg * m, b1 = result.Y - (m + mx) * s.U * s.R;
            float a21 = xg * m, a22 = h.IzzKgM2 + xg * xg * m + jz, b2 = result.N - xg * m * s.U * s.R;
            float det = a11 * a22 - a12 * a21;
            result.Vdot = (b1 * a22 - a12 * b2) / det;
            result.Rdot = (a11 * b2 - a21 * b1) / det;
            return result;
        }

        /// <summary>Straight-run resistance at surge speed <paramref name="u"/>, N (sign follows u; table interpolated, V² beyond it).</summary>
        public static float Resistance(in MmgHullData h, float u)
        {
            float speed = Mathf.Abs(u);
            float[] vs = h.ResistanceSpeedMs, rs = h.ResistanceN;
            float r;
            if (speed <= vs[0]) r = rs[0] * (speed / vs[0]) * (speed / vs[0]);
            else if (speed >= vs[vs.Length - 1]) r = rs[rs.Length - 1] * (speed / vs[vs.Length - 1]) * (speed / vs[vs.Length - 1]);
            else
            {
                int i = 1;
                while (vs[i] < speed) i++;
                float t = (speed - vs[i - 1]) / (vs[i] - vs[i - 1]);
                r = Mathf.Lerp(rs[i - 1], rs[i], t);
            }
            return Mathf.Sign(u) * r;
        }

        /// <summary>Propeller thrust, N, at shaft speed <paramref name="n"/> (rev/s), surge <paramref name="u"/> and wake fraction <paramref name="wake"/>.</summary>
        public static float Thrust(in MmgPropellerData p, float u, float n, float wake)
        {
            if (Mathf.Abs(n) < 1e-4f) return 0f;
            float D = p.DiameterM, d4 = D * D * D * D;
            if (n > 0f)
            {
                float j = u * (1f - wake) / (n * D);
                return Rho * n * n * d4 * (p.K0 + p.K1 * j + p.K2 * j * j);   // negative past zero-thrust J: the screw brakes
            }
            // Astern at L0: a fixed fraction of the ahead bollard K_T, pulling aft. Four-quadrant data is L1.
            return -Rho * n * n * d4 * p.AsternThrustFraction * p.K0;
        }

        /// <summary>Power delivered to one propeller, W: 2π·n·Q with Q = K_Q(J)·ρn²D⁵ (ahead only).</summary>
        public static float DeliveredPowerW(in MmgPropellerData p, float u, float n, float wake)
        {
            if (n <= 0f) return 0f;
            float D = p.DiameterM;
            float j = u * (1f - wake) / (n * D);
            float kq = p.Q0 + p.Q1 * j + p.Q2 * j * j;
            return 2f * Mathf.PI * n * Rho * n * n * D * D * D * D * D * kq;
        }

        /// <summary>
        /// Shaft speed (rev/s, per shaft) that holds surge speed <paramref name="u"/> in a straight run:
        /// −R(u) + (1 − t_P)·ΣT = 0, each shaft's K_T(J)·ρn²D⁴ = T solved as a quadratic in n.
        /// </summary>
        public static float SteadyShaftRps(in MmgHullData h, in MmgPropellerData p, int shafts, float u)
        {
            if (u <= 0f) return 0f;
            float thrust = Resistance(h, u) / (shafts * (1f - p.ThrustDeduction));
            float D = p.DiameterM, uP = u * (1f - p.WakeStraight);
            float a = p.K0, b = p.K1 * uP / D, c = p.K2 * uP * uP / (D * D) - thrust / (Rho * D * D * D * D);
            return (-b + Mathf.Sqrt(b * b - 4f * a * c)) / (2f * a);
        }

        static void Side(in MmgHullData h, in MmgPropellerData p, in MmgRudderData rd, in MmgState s, float U, float beta, float rNd,
            float n, float delta, out float xp, out float np, out float xr, out float yr, out float nr,
            out float thrust, out float alphaDeg, out bool stalled)
        {
            float L = h.LppM;
            // Wake at the propeller changes with the local inflow angle β_P.
            float betaP = beta - p.XpNd * rNd;
            float c2 = betaP > 0f ? p.C2Positive : p.C2Negative;
            float oneMinusW = (1f - p.WakeStraight) * (1f + (1f - Mathf.Exp(-p.C1 * Mathf.Abs(betaP))) * (c2 - 1f));
            float wake = 1f - oneMinusW;
            // Each shaft sees its own axial inflow in a turn: the outboard one runs faster through the water.
            float uLocal = s.U - p.YpM * s.R;
            thrust = Thrust(p, uLocal, n, wake);
            xp = (1f - p.ThrustDeduction) * thrust;
            np = -p.YpM * xp;   // a shaft to starboard pushing ahead turns the bow to port

            // Rudder inflow: longitudinal from the propeller race, lateral from drift and yaw.
            float uP = uLocal * oneMinusW;
            float uR;
            if (n > 1e-4f && uP > 1e-3f)
            {
                float j = uP / (n * p.DiameterM);
                float kt = Mathf.Max(p.K0 + p.K1 * j + p.K2 * j * j, 0f);
                // η: the share of the rudder span inside the race. A screw wider than the rudder is tall covers it all.
                float eta = Mathf.Min(p.DiameterM / rd.SpanM, 1f);
                float race = 1f + rd.Kappa * (Mathf.Sqrt(1f + 8f * kt / (Mathf.PI * j * j)) - 1f);
                uR = rd.Epsilon * uP * Mathf.Sqrt(eta * race * race + (1f - eta));
            }
            else
            {
                uR = rd.Epsilon * uP;   // no race to speak of (stopped or astern): the hull's own wake only
            }
            float betaR = beta - rd.LrNd * rNd;
            float gamma = betaR < 0f ? rd.GammaRNegative : rd.GammaRPositive;
            float vR = U * gamma * betaR;
            float alpha = delta - Mathf.Atan2(vR, Mathf.Max(uR, 1e-3f));
            float uR2 = uR * uR + vR * vR;
            float cn = NormalForceCoefficient(rd, alpha, out stalled);
            float fn = 0.5f * Rho * rd.AreaM2 * uR2 * cn;
            alphaDeg = alpha * Mathf.Rad2Deg;

            xr = -(1f - rd.Tr) * fn * Mathf.Sin(delta);
            yr = -(1f + rd.Ah) * fn * Mathf.Cos(delta);
            nr = -(rd.XrNd * L + rd.Ah * rd.XhNd * L) * fn * Mathf.Cos(delta) - rd.YrM * xr;
        }

        /// <summary>
        /// Normal force coefficient: f_α·sin α (MMG) up to the stall angle, then down to a post-stall plateau.
        /// The standard method has no stall; a rudder in a slow ship's weak race at hard over does.
        /// </summary>
        public static float NormalForceCoefficient(in MmgRudderData rd, float alpha, out bool stalled)
        {
            float a = Mathf.Abs(alpha), stall = rd.StallAngleDeg * Mathf.Deg2Rad;
            stalled = a > stall;
            float cn = stalled ? rd.FAlpha * Mathf.Sin(stall) * rd.PostStallFraction : rd.FAlpha * Mathf.Sin(a);
            return Mathf.Sign(alpha) * cn;
        }
    }
}
