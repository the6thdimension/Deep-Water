using CarrierOps.Core.State;

namespace CarrierOps.Core.Motion
{
    /// <summary>
    /// Sea-state-driven ship motion model. Produces heave / roll / pitch offsets from the
    /// calm pose as functions of simulation time.
    ///
    /// **Contract:**
    /// - Pure function of (sea state, time). No hidden state — same `t` → same output.
    /// - Implementations MUST honor P7 (deterministic RNG via injected seed).
    /// - Motion is small ("sway") — the ship's underlying position and heading are driven
    ///   separately by <see cref="Movement.ShipKinematics"/>. The motion model only nudges.
    ///
    /// The interface returns the three sway values in one call to avoid duplicate state
    /// lookups across heave/roll/pitch.
    /// </summary>
    public interface ISeaStateMotion
    {
        /// <summary>
        /// Compute sway at simulation time <paramref name="timeS"/>.
        /// </summary>
        /// <param name="seaState">Authored sea-state parameters.</param>
        /// <param name="timeS">Simulation time in seconds since the entity was constructed.</param>
        /// <param name="heaveMeters">Output: vertical sway, meters.</param>
        /// <param name="rollDeg">Output: roll about longitudinal axis, degrees.</param>
        /// <param name="pitchDeg">Output: pitch about transverse axis, degrees.</param>
        void Sample(in SeaStateData seaState, double timeS,
            out float heaveMeters, out float rollDeg, out float pitchDeg);
    }
}
