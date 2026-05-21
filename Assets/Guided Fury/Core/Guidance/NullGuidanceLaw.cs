using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// "Do nothing" guidance. Returns a default command every step. Used by unguided
    /// munitions (dumb bombs, ballistic rockets, ignition-only test rounds) and by tests that
    /// want to exercise the integrator in isolation.
    ///
    /// Shared singleton — the law has no state, so we don't need per-missile copies.
    /// </summary>
    public sealed class NullGuidanceLaw : IGuidanceLaw
    {
        public static readonly NullGuidanceLaw Instance = new NullGuidanceLaw();
        private NullGuidanceLaw() { }

        public GuidanceLawKind Kind => GuidanceLawKind.None;

        public MissileCommand ComputeCommand(in GuidanceContext context, in MissileProfileData profile)
        {
            // default(MissileCommand) == { accel=0, throttle=0, arm=false, detonate=false }
            return default;
        }
    }
}
