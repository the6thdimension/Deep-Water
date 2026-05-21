using UnityEngine;
using CarrierOps.Core.State;

namespace CarrierOps.Core.Movement
{
    /// <summary>
    /// Ship translational + heading dynamics. Simple kinematic model with realistic
    /// rate-limits — no hull hydrodynamics, no thrust/drag curve. The point is to make the
    /// carrier *actually move* with the right scale and feel.
    ///
    /// **What this models:**
    /// - Speed: first-order lag toward the throttle-commanded fraction of MaxSpeedKnots.
    ///   Rate is bounded by `AccelKnotsPerSec`. Ford takes ~10 minutes to reach top speed
    ///   from a dead stop; the default `AccelKnotsPerSec = 0.05` reflects that.
    /// - Turn rate: first-order lag toward the rudder-commanded fraction of MaxTurnRateDegPerSec.
    ///   Bounded by `TurnRateAccelDegPerSec2`. Sustained turn rate of ~0.5°/s yields a tactical
    ///   diameter of about 3000 yards at 30 kt — realistic for a Ford-class.
    /// - Position: integrated from speed × heading vector, in world XZ plane.
    ///
    /// **What this does NOT model:**
    /// - Sideways drift, leeway, current. Heading == ground track.
    /// - Roll-into-turn coupling — that lives in the motion model.
    /// - Hull resistance vs. speed, fuel state, reverse thrust.
    /// - Twin-shaft differential thrust for tight maneuvering.
    ///
    /// **Conventions:** heading in degrees clockwise from north (0° = +Z by Unity left-handed
    /// world). Positive turn rate = starboard. Speed in knots; converted to m/s for
    /// position integration (1 kt = 0.514444 m/s).
    /// </summary>
    public static class ShipKinematics
    {
        public const float KnotsToMps = 0.514444f;

        public static void Step(
            in CarrierProfileData profile,
            in ShipCommand command,
            float dt,
            CarrierState state)
        {
            // -- Throttle → target speed --------------------------------------
            float throttle = Mathf.Clamp01(command.ThrottleNormalized);
            float targetSpeed = throttle * profile.MaxSpeedKnots;

            // Move SpeedKnots toward targetSpeed at AccelKnotsPerSec.
            float speedStep = profile.AccelKnotsPerSec * dt;
            state.SpeedKnots = Mathf.MoveTowards(state.SpeedKnots, targetSpeed, speedStep);

            // -- Rudder → target turn rate ------------------------------------
            // A ship at zero speed produces no turning moment from its rudder. We model this
            // by scaling effective turn-rate authority by speed/maxSpeed.
            float rudder = Mathf.Clamp(command.RudderNormalized, -1f, 1f);
            float speedFraction = profile.MaxSpeedKnots > 0.01f
                ? Mathf.Clamp01(state.SpeedKnots / profile.MaxSpeedKnots)
                : 0f;
            float targetTurnRate = rudder * profile.MaxTurnRateDegPerSec * speedFraction;

            // Move TurnRateDegPerSec toward targetTurnRate at TurnRateAccelDegPerSec2.
            float turnStep = profile.TurnRateAccelDegPerSec2 * dt;
            state.TurnRateDegPerSec = Mathf.MoveTowards(
                state.TurnRateDegPerSec, targetTurnRate, turnStep);

            // -- Integrate heading --------------------------------------------
            state.HeadingDeg += state.TurnRateDegPerSec * dt;
            // Wrap to [0, 360).
            if (state.HeadingDeg >= 360f) state.HeadingDeg -= 360f;
            else if (state.HeadingDeg < 0f) state.HeadingDeg += 360f;

            // -- Integrate position -------------------------------------------
            // Heading 0° = +Z forward; positive heading rotates toward +X (starboard).
            float headingRad = state.HeadingDeg * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(headingRad), 0f, Mathf.Cos(headingRad));
            float speedMps = state.SpeedKnots * KnotsToMps;
            state.Position += forward * (speedMps * dt);
        }
    }
}
