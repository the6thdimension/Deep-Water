using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Missile
{
    /// <summary>
    /// Pure-C# missile aggregate. Owns the state, the integrator, the profile data, the
    /// guidance law, the target source, and the atmosphere reference.
    ///
    /// **This class has no Unity scene dependencies** (Vector3/Quaternion are value-type math
    /// only). It can be:
    /// - Unit-tested without a scene (construct, Launch, Step in a loop, assert state).
    /// - Hosted by anything — MissileBehaviour today, an ECS component-system pair tomorrow.
    /// - Stepped at arbitrary `dt` for replay or fast-forward scenarios.
    ///
    /// **Phase 2 changes vs Phase 1:**
    /// - The entity now holds an <see cref="IGuidanceLaw"/> and <see cref="ITargetSource"/>.
    ///   Step() polls the target source, builds a GuidanceContext, asks the law for a command,
    ///   and feeds that command to the integrator. The MonoBehaviour no longer drives commands.
    /// - Step() lost its `in MissileCommand` parameter — commands are produced internally.
    ///   Test paths that want a fixed command construct the entity with
    ///   <see cref="NullGuidanceLaw"/> (returns default) and feed the integrator directly via
    ///   a manual stepping helper if needed.
    /// </summary>
    public sealed class MissileEntity
    {
        // -- Configuration (set at construction, immutable thereafter) ----------
        private MissileProfileData _profile;
        public ref readonly MissileProfileData Profile => ref _profile;

        public IPhysicsIntegrator Integrator { get; }
        public IGuidanceLaw       Guidance   { get; }
        public ITargetSource      TargetSource { get; set; } // settable: launchers may bind/unbind targets
        public IAtmosphere        Atmosphere { get; }

        // -- State (mutates each Step) ------------------------------------------
        public MissileState State;

        public MissileEntity(
            in MissileProfileData profile,
            IPhysicsIntegrator integrator,
            IGuidanceLaw guidance,
            IAtmosphere atmosphere,
            ITargetSource targetSource = null)
        {
            _profile     = profile;
            Integrator   = integrator ?? throw new System.ArgumentNullException(nameof(integrator));
            Guidance     = guidance   ?? throw new System.ArgumentNullException(nameof(guidance));
            Atmosphere   = atmosphere; // null allowed — integrators get SeaLevelIsa as fallback
            TargetSource = targetSource; // null allowed — guidance receives TargetTrack.None

            State = MissileState.AtRest(Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Place the missile in the world and arm the integrator. Must be called before Step.
        /// </summary>
        public void Launch(Vector3 worldPosition, Quaternion worldOrientation)
        {
            State = MissileState.AtRest(worldPosition, worldOrientation);
            Integrator.Initialize(in _profile, ref State);
        }

        /// <summary>
        /// Advance simulation by `dt` seconds. The entity polls its target source, asks
        /// guidance for a command, and steps physics. Called once per FixedUpdate by the
        /// MonoBehaviour adapter (P2).
        /// </summary>
        public void Step(float dt)
        {
            if (State.Phase == MissilePhase.Detonated || State.Phase == MissilePhase.Failed)
                return;

            // Sample atmosphere (L0 ignores it; L1+ uses density).
            AtmosphereSample atmo = Atmosphere != null
                ? Atmosphere.Sample(State.Position.y)
                : AtmosphereSample.SeaLevelIsa;

            // Advance the target source before sampling. This is where seekers tick their
            // lock state, and where truth sources update their finite-difference velocity
            // estimates. Stateless sources no-op.
            TargetSource?.Update(in State, dt);

            // Build the guidance context. Note: the target source can return None freely;
            // guidance laws are contractually required to handle that.
            TargetTrack target = TargetSource != null ? TargetSource.Sample() : TargetTrack.None;
            var context = new GuidanceContext
            {
                State  = State,
                Target = target,
                Atmo   = atmo,
                Dt     = dt,
            };

            // Compute the command. Guidance is a pure function; we don't mutate it.
            MissileCommand command = Guidance.ComputeCommand(in context, in _profile);

            // Detonation request from guidance (e.g. self-destruct or external command). The
            // integrator doesn't enforce this — the entity does, so terminal effects fire
            // exactly once.
            if (command.DetonateCommand)
            {
                Detonate();
                return;
            }

            Integrator.Step(in _profile, in command, in atmo, dt, ref State);
        }

        /// <summary>
        /// Force the missile into a Detonated phase. Called by the fuze or external command.
        /// </summary>
        public void Detonate()
        {
            State.Phase = MissilePhase.Detonated;
        }
    }
}
