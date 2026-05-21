using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Damage;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Seekers;
using GuidedFury.Core.State;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Core.Missile
{
    /// <summary>
    /// MonoBehaviour adapter for a MissileEntity. The bridge between Unity (transforms,
    /// FixedUpdate, scene hierarchy) and the pure-C# simulation core.
    ///
    /// DOES:
    /// - Pump the entity's Step() once per FixedUpdate (P2).
    /// - Apply state.Position / state.Orientation back to the transform after each step.
    /// - Surface launch / target-setting / detonate / debug hooks to the rest of the scene.
    /// - Hold the Inspector-facing configuration (profile, LOD, optional target Transform).
    ///
    /// DOES NOT:
    /// - Hold simulation state. That lives in `entity.State`.
    /// - Do physics math. That lives in the integrator.
    /// - Produce guidance commands. That lives in the guidance law.
    /// - Use Update(). All simulation work is on FixedUpdate.
    /// - Use Time.deltaTime. Only Time.fixedDeltaTime is read (and only passed to Step).
    /// - Use reflection or component scans for behavior (AP1, AP3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissileBehaviour : MonoBehaviour
    {
        // -- Inspector configuration ----------------------------------------------
        [Header("Profile")]
        [Tooltip("ScriptableObject holding this missile's data. Required.")]
        [SerializeField] private MissileProfileSO profile;

        [Header("Simulation")]
        [Tooltip("Physics fidelity tier. Higher = more realistic, more CPU.")]
        [SerializeField] private MissileLod lod = MissileLod.L0_Kinematic;

        [Header("Target (optional)")]
        [Tooltip("If set, the missile uses an omniscient TransformTargetSource on this object at launch. "
                 + "Leave null for ballistic / unguided flight, or call SetTarget() at launch.")]
        [SerializeField] private Transform initialTarget;

        [Header("Auto-Launch (test helper)")]
        [Tooltip("If true, the missile auto-launches in Start(). Useful for test scenes.")]
        [SerializeField] private bool autoLaunchOnStart = false;

        [Tooltip("If autoLaunch is true, the world-space direction to fly. Otherwise ignored.")]
        [SerializeField] private Vector3 autoLaunchDirection = Vector3.forward;

        [Header("Fuze (Phase 2.5 minimal)")]
        [Tooltip("If true, the missile polls Physics.OverlapSphere(radius = profile.FuzeProximityRadiusM) "
                 + "each FixedUpdate after the arm delay to detect impacts. Disable for tests where you don't want physics-side detection.")]
        [SerializeField] private bool enableProximityFuze = true;

        [Tooltip("Layer mask of collision layers the fuze will detect. Default = Everything.")]
        [SerializeField] private LayerMask fuzeDetectionMask = ~0;

        // -- Runtime --------------------------------------------------------------
        private MissileEntity entity;
        private bool launched;

        // Shared buffer for the OverlapSphere call. Re-used across all missiles in the scene
        // so we don't allocate every FixedUpdate. 16 is plenty — fuze radii are small and
        // the test range is sparse.
        private static readonly Collider[] s_fuzeBuffer = new Collider[16];

        // -- Unity lifecycle ------------------------------------------------------
        private void Start()
        {
            if (autoLaunchOnStart)
            {
                if (autoLaunchDirection.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.LookRotation(autoLaunchDirection.normalized, Vector3.up);

                Launch(transform.position, transform.rotation);
            }
        }

        private void FixedUpdate()
        {
            if (!launched || entity == null) return;

            // P2: all simulation advances happen here with the engine's fixed delta.
            entity.Step(Time.fixedDeltaTime);

            // Mirror state back to the Unity transform for rendering and downstream consumers.
            transform.SetPositionAndRotation(entity.State.Position, entity.State.Orientation);

            // Proximity fuze check. Skipped while the missile is in a terminal state.
            if (entity.State.Phase != MissilePhase.Detonated && entity.State.Phase != MissilePhase.Failed)
                CheckProximityFuze();

            if (entity.State.Phase == MissilePhase.Detonated || entity.State.Phase == MissilePhase.Failed)
                OnTerminalState();
        }

        // -- Public API -----------------------------------------------------------
        /// <summary>
        /// Set profile / LOD / target before launch. Production launches should pre-assign
        /// on the prefab; code-driven setup uses this. Throws after Launch — Phase 2 does not
        /// support mid-flight reconfiguration.
        /// </summary>
        public void Configure(MissileProfileSO newProfile, MissileLod newLod, Transform target = null)
        {
            if (launched)
                throw new System.InvalidOperationException(
                    $"[GuidedFury] Cannot Configure {name} after launch. Profile, LOD, and target binding " +
                    "are immutable in flight (Phase 2).");

            profile = newProfile;
            lod = newLod;
            initialTarget = target;
        }

        /// <summary>
        /// Bind a target after launch. Allowed because guidance laws are required to handle
        /// HasTrack=false gracefully — adding a target mid-flight just starts the acquisition
        /// process (or guidance directly, if the missile has no seeker).
        /// </summary>
        public void SetTarget(Transform target)
        {
            initialTarget = target;
            if (entity != null)
                entity.TargetSource = BuildTargetSource(entity.Profile, target);
        }

        /// <summary>
        /// Launch this missile from the given pose. Builds the entity from the assigned
        /// profile, the selected LOD's integrator, the profile's guidance law, and the
        /// optional initial target.
        /// </summary>
        public void Launch(Vector3 worldPosition, Quaternion worldOrientation)
        {
            if (launched)
            {
                Debug.LogWarning($"[GuidedFury] {name} is already launched. Ignoring duplicate Launch().");
                return;
            }

            if (profile == null)
            {
                Debug.LogError($"[GuidedFury] {name} has no MissileProfileSO assigned. Cannot launch.");
                return;
            }

            // Bake the SO once. Live edits after this do not affect the in-flight missile.
            MissileProfileData profileData = profile.Bake();

            IPhysicsIntegrator integrator = CreateIntegratorForLod(lod);
            IGuidanceLaw guidance = GuidanceFactory.Create(profileData.GuidanceLaw);
            ITargetSource targetSource = BuildTargetSource(profileData, initialTarget);

            entity = new MissileEntity(in profileData, integrator, guidance, StandardAtmosphere.Instance, targetSource);
            entity.Launch(worldPosition, worldOrientation);
            launched = true;

            // Fuze is polled in FixedUpdate via Physics.OverlapSphere — no setup required here.
        }

        /// <summary>Force-detonate the missile. Phase 2 has no detonation effects yet.</summary>
        public void Detonate() => entity?.Detonate();

        /// <summary>Read-only access to the current simulation state.</summary>
        public MissileState GetState() => entity?.State ?? default;

        /// <summary>Read-only access to the underlying entity (advanced/testing only).</summary>
        public MissileEntity Entity => entity;

        /// <summary>Currently-bound target Transform (read-only, for HUD/debug).</summary>
        public Transform Target => initialTarget;

        /// <summary>
        /// True if a seeker-filtered target source is currently in lock. Returns false for
        /// missiles without a seeker (those see truth directly so the concept doesn't apply).
        /// </summary>
        public bool IsSeekerLocked
        {
            get
            {
                if (entity?.TargetSource is Seekers.SeekerTargetSource seekerSource)
                    return seekerSource.HasLock;
                return false;
            }
        }

        /// <summary>True if this missile has been launched and is still alive.</summary>
        public bool IsLaunched => launched;

        // -- Internals ------------------------------------------------------------
        private static IPhysicsIntegrator CreateIntegratorForLod(MissileLod lod)
        {
            switch (lod)
            {
                case MissileLod.L0_Kinematic:        return new KinematicL0Integrator();
                case MissileLod.L1_PointMass3Dof:    return new PointMass3DofL1Integrator();
                case MissileLod.L2_RateLimited3Dof:  return new RateLimited3DofL2Integrator();
                case MissileLod.L3_PseudoRb6Dof:     return new PseudoRb6DofL3Integrator();

                // Higher LODs not yet implemented — fail loudly. Honest error handling.
                case MissileLod.L4_FullAero6Dof:
                case MissileLod.L5_HardwareInTheLoop:
                    throw new System.NotImplementedException(
                        $"[GuidedFury] Integrator for {lod} is not yet implemented (Phase 4 ships L0..L3).");

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(lod), lod, "Unknown LOD value");
            }
        }

        /// <summary>
        /// Build the target source for the missile based on profile and target reference.
        /// If the profile defines a seeker, wraps the truth source in a SeekerTargetSource.
        /// Otherwise the truth source is used directly (Phase 1/2 behavior).
        /// </summary>
        private static ITargetSource BuildTargetSource(in MissileProfileData profile, Transform target)
        {
            if (target == null)
                return null;

            ITargetSource truth = new TransformTargetSource(target);

            if (profile.SeekerKind == SeekerKind.None)
                return truth;

            var seekerProfile = new SeekerProfile
            {
                FovDeg           = profile.SeekerFovDeg,
                MaxRangeM        = profile.SeekerMaxRangeM,
                AcquisitionTimeS = profile.SeekerAcquisitionTimeS,
            };
            ISeeker seeker = SeekerFactory.Create(profile.SeekerKind, seekerProfile);
            if (seeker == null)
                return truth; // factory returned no seeker (kind = None or unsupported)

            return new SeekerTargetSource(seeker, truth);
        }

        private void OnTerminalState()
        {
            // Phase 2 cleanup: disable. Pooling and effects come later.
            launched = false;
            gameObject.SetActive(false);
        }

        // -- Proximity fuze (Phase 2.5, refactored Phase 5) ----------------------
        // Active polling via Physics.OverlapSphere each FixedUpdate. We previously used a
        // kinematic-Rigidbody + trigger-SphereCollider, but that proved fragile across
        // missile prefab paths (AddComponent<Rigidbody>() can return null in edge cases,
        // and the trigger's pose-sync at low-quality transforms produced cascading
        // physics errors).
        //
        // OverlapSphere is also simpler conceptually: the missile model owns its fuze, no
        // separate physics body is required, no Rigidbody/Collider lifecycle to manage.
        // The buffer is shared/static (one per process) since fuze checks happen from
        // FixedUpdate only — never reentrantly.
        //
        // Phase 3+ will add proper fuze refinements: target filtering, closing-velocity
        // gate, multi-mode (proximity / impact / timed) selection.
        private void CheckProximityFuze()
        {
            if (!enableProximityFuze) return;

            var profile = entity.Profile;
            float radius = profile.FuzeProximityRadiusM;
            if (radius <= 0f) return;

            // Safe-and-arm delay: fuze is inert for the first FuzeArmDelayS after launch.
            // Stops the missile from detonating on the launcher / nearby geometry before
            // it's safely cleared the rail.
            if (entity.State.TimeOfFlight < profile.FuzeArmDelayS) return;

            int hits = Physics.OverlapSphereNonAlloc(
                entity.State.Position, radius, s_fuzeBuffer,
                fuzeDetectionMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits; i++)
            {
                Collider other = s_fuzeBuffer[i];
                if (other == null) continue;

                // Don't trip on ourselves or our own children (trail, decorative pieces).
                if (other.transform == transform || other.transform.IsChildOf(transform)) continue;

                // Notify the hit object if it can respond.
                IHittable hittable = other.GetComponentInParent<IHittable>();
                if (hittable != null)
                    hittable.OnMissileHit(entity.State.Position, entity.State.Velocity, entity.State.Mass);

                entity.Detonate();
                return; // one hit per FixedUpdate is plenty; cleanup in next FixedUpdate
            }
        }
    }
}
