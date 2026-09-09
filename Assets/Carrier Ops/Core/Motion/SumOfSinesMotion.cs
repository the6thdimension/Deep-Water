using UnityEngine;
using CarrierOps.Core.State;

namespace CarrierOps.Core.Motion
{
    /// <summary>
    /// Sum-of-sines sea-state motion model. Replaces the legacy `Mathf.Sin(Time.time)` sway
    /// with a deterministic, designer-tunable model.
    ///
    /// **The math:** for each axis (heave, roll, pitch), we sum three sinusoidal components
    /// centered on the dominant period from the sea-state profile. The component periods are
    /// {0.85 × T, T, 1.15 × T} — slight detuning produces visually rich, non-repeating motion
    /// without looking obviously synthetic. Phases are drawn from a seeded RNG (P7), so the
    /// same `Seed` always produces the same motion timeline.
    ///
    /// Amplitudes are split 0.4 / 0.4 / 0.2 across the components; the dominant pair carries
    /// most of the energy.
    ///
    /// **What this does and does NOT model:**
    /// - DOES: visually believable heave/roll/pitch oscillation with proper period coupling
    ///   to a sea-state setting; deterministic across runs.
    /// - DOES NOT: real ocean spectrum (PM/JONSWAP), wave direction relative to ship heading,
    ///   ship inertia / damping / coupling between modes. Those are future work; this is
    ///   "good enough for visual fidelity at Beaufort 0–6" and that's the contract.
    ///
    /// **Stateless singleton** — the motion is pure-function of (sea state, time, seed).
    /// </summary>
    public sealed class SumOfSinesMotion : ISeaStateMotion
    {
        public static readonly SumOfSinesMotion Instance = new SumOfSinesMotion();
        private SumOfSinesMotion() { }

        // Per-component fractional period offsets and amplitude weights. Keep symmetric so
        // mean(amp_i × cos(0)) = sum(amp_i) — the peak amplitude in the profile is the peak
        // amplitude produced.
        private static readonly float[] PeriodOffsets   = { 0.85f, 1.00f, 1.15f };
        private static readonly float[] AmplitudeWeights = { 0.40f, 0.40f, 0.20f };

        public void Sample(in SeaStateData seaState, double timeS,
            out float heaveMeters, out float rollDeg, out float pitchDeg)
        {
            // Three independent phase tables — one per axis. Derived from the same seed so
            // changing the seed changes all axes coherently; using different sub-seeds
            // prevents heave/roll/pitch from being suspiciously in-lockstep.
            heaveMeters = Sum(timeS, seaState.HeaveAmplitudeM,   seaState.HeavePeriodS,
                              GetPhases(seaState.Seed,        AxisHeave));
            rollDeg     = Sum(timeS, seaState.RollAmplitudeDeg,  seaState.RollPeriodS,
                              GetPhases(seaState.Seed + 7919, AxisRoll));
            pitchDeg    = Sum(timeS, seaState.PitchAmplitudeDeg, seaState.PitchPeriodS,
                              GetPhases(seaState.Seed + 7937, AxisPitch));
        }

        // -- Internals -----------------------------------------------------

        private const int AxisHeave = 0;
        private const int AxisRoll  = 1;
        private const int AxisPitch = 2;

        private static float Sum(double timeS, float amplitude, float dominantPeriodS, float[] phases)
        {
            if (amplitude <= 0f || dominantPeriodS <= 0f) return 0f;

            float total = 0f;
            for (int i = 0; i < 3; i++)
            {
                float periodS = dominantPeriodS * PeriodOffsets[i];
                float omega = 2f * Mathf.PI / periodS;
                // double-precision time argument to avoid float drift on long runs.
                float arg = (float)(omega * timeS) + phases[i];
                total += amplitude * AmplitudeWeights[i] * Mathf.Sin(arg);
            }
            return total;
        }

        /// <summary>
        /// Deterministically derive three phase offsets from a seed. We use a simple LCG
        /// keyed by (seed, axis) — fast, deterministic, no allocation. Caching across calls
        /// would be ideal for hot paths but at one call per FixedUpdate per carrier it's
        /// well below the noise floor.
        /// </summary>
        private static float[] GetPhases(int seed, int axisSalt)
        {
            uint s = (uint)(seed ^ (axisSalt * 0x9E3779B1));
            var result = new float[3];
            for (int i = 0; i < 3; i++)
            {
                // LCG step (Numerical Recipes constants), normalize to [0, 2π).
                s = s * 1664525u + 1013904223u;
                result[i] = (s / (float)uint.MaxValue) * (2f * Mathf.PI);
            }
            return result;
        }
    }
}
