using UnityEngine;
using CarrierOps.Core.State;

namespace CarrierOps.Core.Catapult
{
    /// <summary>
    /// Pure-C# catapult cycle state machine. Advances a single <see cref="CatapultState"/>
    /// instance one step at a time on FixedUpdate.
    ///
    /// **Stages and what each one does** (see also <see cref="CatapultStage"/>):
    /// - Idle: shuttle parked at the aft end. Waiting for `RequestLaunch` to flip into Spotted.
    /// - Spotted: profile-timed pause representing taxi-on and alignment.
    /// - Tensioned: profile-timed pause. JBD raises here (set on entry). Engines spool up,
    ///   shuttle tensions to the launch bar.
    /// - Ready: profile-timed pause for "shooter check". Last call before commit.
    /// - Firing: physics stage. The shuttle accelerates along the track with a profile-driven
    ///   acceleration curve until it reaches `CatapultEndSpeedMps` or the end of stroke. The
    ///   attached aircraft inherits the shuttle's velocity each step.
    /// - Retracting: profile-timed pause while the shuttle slides back to the aft position.
    ///   JBD lowers here (cleared on entry).
    ///
    /// **Why `OnEnter` exists:** stage-entry side effects (JBD raise/lower) must fire exactly
    /// once on transition — not lazily on the next step's case body. The naïve "set JbdRaised
    /// inside the Tensioned case" pattern has a one-step lag: when Spotted's case detects
    /// timeout and calls `Transition`, the switch exits with `break` and the Tensioned case
    /// only runs on the NEXT step. That made a test flaky at the exact step where the
    /// transition fired. Centralising entry-side-effects fixes the class of bug, not just
    /// this instance.
    ///
    /// **Acceleration profile during Firing:**
    /// The simplest model that matches real steam-cat behavior is constant acceleration sized
    /// to reach `EndSpeed` over `Stroke` distance:
    ///     a = EndSpeed² / (2 × Stroke)
    /// This produces a kinematic answer (peak G ≈ a / 9.81). If the resulting peak G exceeds
    /// the profile's `PeakG`, we clip — the aircraft then leaves the cat below the requested
    /// end speed (which is what would happen in reality for an overweight launch).
    /// </summary>
    public static class CatapultCycle
    {
        public const float StandardGravity = 9.80665f;

        /// <summary>
        /// External request: flip this catapult from Idle into the launch sequence.
        /// Ignored if not Idle (a cycle is already running).
        /// </summary>
        public static void RequestLaunch(ref CatapultState cat)
        {
            if (cat.Stage != CatapultStage.Idle) return;
            cat.ShuttleDistanceM = 0f;
            cat.ShuttleVelocityMps = 0f;
            Transition(ref cat, CatapultStage.Spotted);
        }

        public static void Step(in CarrierProfileData profile, float dt, ref CatapultState cat)
        {
            cat.StageTimer += dt;

            switch (cat.Stage)
            {
                case CatapultStage.Idle:
                    // Wait for RequestLaunch.
                    break;

                case CatapultStage.Spotted:
                    if (cat.StageTimer >= profile.CatapultSpottedDurationS)
                        Transition(ref cat, CatapultStage.Tensioned);
                    break;

                case CatapultStage.Tensioned:
                    if (cat.StageTimer >= profile.CatapultTensionedDurationS)
                        Transition(ref cat, CatapultStage.Ready);
                    break;

                case CatapultStage.Ready:
                    if (cat.StageTimer >= profile.CatapultReadyDurationS)
                        Transition(ref cat, CatapultStage.Firing);
                    break;

                case CatapultStage.Firing:
                    StepFiring(in profile, dt, ref cat);
                    break;

                case CatapultStage.Retracting:
                    StepRetracting(in profile, dt, ref cat);
                    break;
            }
        }

        // ===========================================================================
        // Per-stage Step logic (non-trivial cases only)
        // ===========================================================================

        private static void StepFiring(in CarrierProfileData profile, float dt, ref CatapultState cat)
        {
            float stroke = Mathf.Max(profile.CatapultStrokeM, 0.01f);
            float endSpeed = profile.CatapultEndSpeedMps;

            // Constant acceleration to reach endSpeed over stroke, clipped at peak G.
            float aRequested = (endSpeed * endSpeed) / (2f * stroke);
            float aCap = profile.CatapultPeakG * StandardGravity;
            float a = Mathf.Min(aRequested, aCap);

            cat.ShuttleVelocityMps += a * dt;
            cat.ShuttleDistanceM   += cat.ShuttleVelocityMps * dt;

            if (cat.ShuttleDistanceM >= stroke)
            {
                cat.ShuttleDistanceM = stroke;
                Transition(ref cat, CatapultStage.Retracting);
            }
        }

        private static void StepRetracting(in CarrierProfileData profile, float dt, ref CatapultState cat)
        {
            // Linear glide of the shuttle back to start, scaled so the trip takes RetractDuration.
            float retractDuration = Mathf.Max(profile.CatapultRetractDurationS, 0.01f);
            float rate = profile.CatapultStrokeM / retractDuration;
            cat.ShuttleDistanceM = Mathf.MoveTowards(cat.ShuttleDistanceM, 0f, rate * dt);

            if (cat.StageTimer >= profile.CatapultRetractDurationS)
                Transition(ref cat, CatapultStage.Idle);
        }

        // ===========================================================================
        // Transition + OnEnter hooks
        // ===========================================================================

        private static void Transition(ref CatapultState cat, CatapultStage next)
        {
            cat.Stage = next;
            cat.StageTimer = 0f;
            OnEnter(ref cat, next);
        }

        /// <summary>
        /// Per-stage entry side effects — fire exactly once on transition. Keep this short:
        /// just side-effects that need to land at the instant of entry. Anything ongoing
        /// belongs in the per-step Step logic above.
        /// </summary>
        private static void OnEnter(ref CatapultState cat, CatapultStage stage)
        {
            switch (stage)
            {
                case CatapultStage.Tensioned:
                    cat.JbdRaised = true;
                    break;

                case CatapultStage.Retracting:
                    cat.JbdRaised = false;
                    break;

                case CatapultStage.Idle:
                    cat.ShuttleDistanceM = 0f;
                    cat.ShuttleVelocityMps = 0f;
                    cat.JbdRaised = false; // belt + suspenders; should already be false
                    break;
            }
        }
    }
}
