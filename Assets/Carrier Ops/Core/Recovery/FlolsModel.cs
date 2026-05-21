using UnityEngine;
using CarrierOps.Core.State;

namespace CarrierOps.Core.Recovery
{
    /// <summary>
    /// FLOLS (Fresnel Lens Optical Landing System) geometry. Pure function: given the
    /// approaching aircraft's world position and the carrier's FLOLS reference point,
    /// compute the glideslope deviation angle and the normalized ball offset that drives
    /// the visible meatball on the lens unit.
    ///
    /// **Geometry recap:**
    /// - The FLOLS sits on the port side of the angled deck, at a defined reference point.
    /// - The pilot flies a glideslope (3.5° fleet standard) to a touchdown target ahead of
    ///   the FLOLS — geometrically equivalent to saying the aircraft should always be at
    ///   3.5° above horizontal from the FLOLS reference, measured in the deck's vertical
    ///   plane.
    /// - The "ball" is a focused point of light. When the pilot is on glideslope, the ball
    ///   appears centered against the horizontal "datum" lights. High = ball above datum.
    ///   Low = ball below.
    /// - Visible window of the lens is roughly ±0.7° from commanded; outside that the ball
    ///   is gone ("waved off the lens").
    ///
    /// **Implementation:**
    /// 1. Take the vector from FLOLS reference to aircraft, world frame.
    /// 2. Project onto the ship's longitudinal-vertical plane (so cross-deck position
    ///    doesn't affect the apparent glideslope — this matches how the real lens behaves
    ///    along the angled deck).
    /// 3. Compute the angle this vector makes with horizontal.
    /// 4. Subtract the commanded glideslope to get deviation.
    /// 5. Clip to the visible window and normalize to [-1..+1].
    /// 6. Flag wave-off if deviation magnitude exceeds threshold.
    ///
    /// **What this does NOT model (yet):**
    /// - Ship-motion-coupled stabilization (real FLOLS stabilizes the lens against ship pitch
    ///   so the ball doesn't move with sea state).
    /// - Glide-path lighting state (yellow/red/green color states) beyond the wave-off flag.
    /// - LSO "talk-down" / paddles input.
    /// </summary>
    public static class FlolsModel
    {
        /// <summary>
        /// Sample FLOLS state for one approaching aircraft.
        /// </summary>
        /// <param name="profile">Carrier profile (read FlolsGlideslopeDeg, window, wave-off threshold).</param>
        /// <param name="flolsReferenceWorld">FLOLS lens reference point in world coords. Provided by the adapter (transform on the prefab).</param>
        /// <param name="shipForwardWorld">Ship's forward direction (world frame), used to define the vertical projection plane.</param>
        /// <param name="aircraftWorld">Aircraft position, world frame.</param>
        /// <returns>Filled FlolsState. If the aircraft is behind or co-located with the FLOLS, returns NoTrack.</returns>
        public static FlolsState Sample(
            in CarrierProfileData profile,
            Vector3 flolsReferenceWorld,
            Vector3 shipForwardWorld,
            Vector3 aircraftWorld)
        {
            Vector3 toAircraft = aircraftWorld - flolsReferenceWorld;

            // Project to the longitudinal-vertical plane. We define "longitudinal" as
            // shipForward, "vertical" as world up. The cross-deck (lateral) component is
            // discarded so the pilot's ball only reflects altitude vs. glideslope, not
            // lateral line-up.
            Vector3 fwd = shipForwardWorld.sqrMagnitude > 1e-6f
                ? shipForwardWorld.normalized
                : Vector3.forward;

            float distanceAlongShip = Vector3.Dot(toAircraft, fwd);
            float verticalOffset    = Vector3.Dot(toAircraft, Vector3.up);

            // Aircraft must be AHEAD of the FLOLS to have meaningful glideslope. If it's
            // behind (or basically on top of), report no track.
            if (distanceAlongShip < 0.5f)
                return FlolsState.NoTrack;

            // Angle above horizontal of the line from FLOLS → aircraft, in the longitudinal-
            // vertical plane.
            float angleDeg = Mathf.Atan2(verticalOffset, distanceAlongShip) * Mathf.Rad2Deg;
            float deviationDeg = angleDeg - profile.FlolsGlideslopeDeg;

            // Normalize to lens window. Magnitudes beyond the window saturate to ±1.
            float halfAngle = Mathf.Max(profile.FlolsWindowHalfAngleDeg, 0.01f);
            float normalized = Mathf.Clamp(deviationDeg / halfAngle, -1f, 1f);

            return new FlolsState
            {
                HasTrack               = true,
                BallOffsetNormalized   = normalized,
                GlideslopeDeviationDeg = deviationDeg,
                IsWaveOff              = Mathf.Abs(deviationDeg) > profile.FlolsWaveOffThresholdDeg,
            };
        }
    }
}
