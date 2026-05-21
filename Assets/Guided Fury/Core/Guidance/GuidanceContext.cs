using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// Everything a guidance law needs to produce a command — bundled into one struct so the
    /// IGuidanceLaw interface stays tight. Built once per FixedUpdate by the entity, passed
    /// by `in` to the law.
    ///
    /// Guidance laws read only — they do not mutate state. The command they return is the
    /// only mechanism by which they influence the simulation. This keeps the seam clean
    /// (anti-pattern AP4: no cross-component reach-arounds).
    /// </summary>
    public struct GuidanceContext
    {
        public MissileState     State;   // own state (read-only inside the law)
        public TargetTrack      Target;  // observation of the target (may be None)
        public AtmosphereSample Atmo;    // local atmosphere (used by Mach-aware laws at L4+)
        public float            Dt;      // fixed step in seconds
    }
}
