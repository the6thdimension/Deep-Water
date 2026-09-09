using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Integrators
{
    /// <summary>
    /// One LOD tier of missile physics. Implementations advance MissileState by `dt` seconds
    /// in response to a MissileCommand. They are the "P" in P=ma — guidance produces commands,
    /// integrators apply them.
    ///
    /// **Contract:**
    /// - `Lod` is constant for the lifetime of the integrator instance.
    /// - `Initialize` is called once per launch, before the first Step. It sets initial mass,
    ///   fuel, etc. from the profile and may mutate state (e.g. give it an initial velocity).
    /// - `Step` is called every FixedUpdate while the missile is active (P2). Implementations
    ///   MUST NOT read Time.deltaTime / Time.time — all time information arrives via `dt` and
    ///   `state.TimeOfFlight`.
    /// - Implementations MUST NOT allocate (no `new` of reference types) in `Step`. Profiles
    ///   and atmosphere samples come in as `in`; state goes in as `ref`.
    /// - No `UnityEngine.Random` or `System.Random` without an injected seed (P7). Phase 1
    ///   integrators are deterministic; later integrators (L4 with seeker noise) get RNG
    ///   injected via constructor.
    ///
    /// **Why an interface, not an abstract class:** integrators have no shared state we want
    /// to hoist into a base. They share only this contract. Keeping it an interface also
    /// lets us swap in struct-implementing integrators later for Burst hot paths.
    /// </summary>
    public interface IPhysicsIntegrator
    {
        MissileLod Lod { get; }

        /// <summary>
        /// Set up the integrator and initial state from the profile. Called once per launch.
        /// </summary>
        void Initialize(in MissileProfileData profile, ref MissileState state);

        /// <summary>
        /// Advance the state by `dt` seconds under the given command and atmosphere.
        /// </summary>
        void Step(
            in MissileProfileData profile,
            in MissileCommand command,
            in AtmosphereSample atmo,
            float dt,
            ref MissileState state);
    }
}
