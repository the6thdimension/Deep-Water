using System.Collections.Generic;
using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Damage;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Propulsion;
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
    /// - Simulate in Update(). All simulation work is on FixedUpdate; Update only
    ///   interpolates the *rendered* transform between the last two fixed states so the
    ///   missile moves smoothly at any frame rate or time scale (see Update()).
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

        [Header("Vendor Prefab Compatibility")]
        [Tooltip("On Launch, find any Rigidbody on this GameObject and force isKinematic=true, useGravity=false. "
                 + "Required when the missile prefab ships with a non-kinematic Rigidbody (otherwise PhysX gravity fights our transform writes — the missile falls straight down).")]
        [SerializeField] private bool neutralizeOwnRigidbody = true;

        [Tooltip("On Launch, disable any sibling MonoBehaviour on this GameObject whose type lives OUTSIDE the GuidedFury.* namespaces. "
                 + "Catches legacy missile controllers shipped with vendor prefabs (e.g. the ESSM_RIM-162 prefab has its own 6DOF script that fights our integrator).")]
        [SerializeField] private bool disableForeignScriptsOnLaunch = true;

        // -- Runtime --------------------------------------------------------------
        private MissileEntity entity;
        private bool launched;

        // Audio sources spawned at launch — flight loop is parented to the missile so it
        // moves with it; launch/explosion one-shots are played at world positions via
        // AudioSource.PlayClipAtPoint so they survive the missile's destruction.
        private AudioSource flightAudio;

        // Cached hit info for the explosion spawn — populated by CheckProximityFuze when a
        // collider is detected so OnTerminalState can pick aerial vs ground without having
        // to re-raycast.
        private Vector3 lastHitPoint;
        private bool    hasHitPoint;

        [Header("Rendering")]
        [Tooltip("Interpolate the rendered transform between the last two FixedUpdate states. " +
                 "The simulation still steps only in FixedUpdate (deterministic, P2/P6); this is " +
                 "purely visual. Without it the missile teleports once per fixed step, which reads " +
                 "as stutter whenever frames outpace fixed steps - most obviously at low time scales " +
                 "(0.1x = one fixed step per 200 ms of real time). Costs one fixed step of visual latency.")]
        [SerializeField] private bool interpolateVisuals = true;

        // Pose pair for visual interpolation: the sim pose from the last two fixed steps.
        // Update() blends between them; FixedUpdate() shifts them along.
        private Vector3 prevSimPos, currSimPos;
        private Quaternion prevSimRot = Quaternion.identity, currSimRot = Quaternion.identity;

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
            // Update() re-writes the transform with an interpolated pose before rendering;
            // this write keeps FixedUpdate-time consumers (other scripts' FixedUpdates,
            // physics queries against our colliders) seeing the exact sim pose.
            prevSimPos = currSimPos;
            prevSimRot = currSimRot;
            currSimPos = entity.State.Position;
            currSimRot = entity.State.Orientation;
            transform.SetPositionAndRotation(currSimPos, currSimRot);

            // Proximity fuze check. Skipped while the missile is in a terminal state.
            if (entity.State.Phase != MissilePhase.Detonated && entity.State.Phase != MissilePhase.Failed)
                CheckProximityFuze();

            if (entity.State.Phase == MissilePhase.Detonated || entity.State.Phase == MissilePhase.Failed)
                OnTerminalState();
        }

        private void Update()
        {
            // Visual smoothing only - no simulation here (P2). Renders the pose interpolated
            // between the previous and current fixed steps. alpha is how far the render clock
            // has advanced into the current fixed step; at 0.1x time scale a fixed step spans
            // ~200 ms of real time, so without this the missile visibly teleports step to step.
            if (!launched || entity == null || !interpolateVisuals) return;

            float step = Time.fixedDeltaTime;
            float alpha = step > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / step) : 1f;
            transform.SetPositionAndRotation(
                Vector3.Lerp(prevSimPos, currSimPos, alpha),
                Quaternion.Slerp(prevSimRot, currSimRot, alpha));
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

            // Neutralize foreign components BEFORE the integrator takes over the transform.
            // Otherwise PhysX gravity or a legacy missile script will fight our writes and
            // the missile will fall straight down (or wander, depending on what the legacy
            // script is doing).
            NeutralizeForeignComponents();

            // Bake the SO once. Live edits after this do not affect the in-flight missile.
            MissileProfileData profileData = profile.Bake();

            // Pick the integrator for the requested LOD. If unimplemented, downgrade to the
            // highest available LOD with a clear warning — the user gets a working missile
            // and an actionable message instead of an exception that aborts the launch.
            IPhysicsIntegrator integrator = CreateIntegratorForLod(lod, profile, profileData);
            if (integrator == null)
            {
                MissileLod fallback = HighestImplementedLod;
                Debug.LogWarning(
                    $"[GuidedFury] {name}: integrator for {lod} not implemented yet — " +
                    $"downgrading to {fallback}. Change LOD on this MissileBehaviour to silence.");
                lod = fallback;
                integrator = CreateIntegratorForLod(fallback, profile, profileData);
            }
            IGuidanceLaw guidance = GuidanceFactory.Create(profileData.GuidanceLaw);
            ITargetSource targetSource = BuildTargetSource(profileData, initialTarget);

            entity = new MissileEntity(in profileData, integrator, guidance, StandardAtmosphere.Instance, targetSource);
            entity.Launch(worldPosition, worldOrientation);
            launched = true;

            // Seed the interpolation pair at the launch pose - a pooled/reused missile would
            // otherwise blend its first visible frames from wherever it previously detonated.
            prevSimPos = currSimPos = worldPosition;
            prevSimRot = currSimRot = worldOrientation;

            // Reset per-flight hit cache in case this GameObject is being re-launched (pooling).
            hasHitPoint = false;
            lastHitPoint = worldPosition;

            // Audio: one-shot launch SFX at the muzzle + start the flight loop riding on the
            // missile. Both are gated on the SO actually having clips assigned, so a profile
            // without audio is silent (no missing-clip warnings).
            StartLaunchAudio(worldPosition);

            // Fuze is polled in FixedUpdate via Physics.OverlapSphere — no setup required here.
        }

        /// <summary>
        /// Neutralize sibling Rigidbody / legacy missile-control scripts on this GameObject.
        /// Runs once on Launch. Why this exists: vendor missile prefabs (e.g. the rim-162essm
        /// `ESSM Shell` prefab) ship with their own non-kinematic Rigidbody and homing scripts.
        /// Those compete with our integrator and produce the "falls straight down on fire"
        /// failure mode. We force kinematic + no-gravity on the rigidbody, and disable any
        /// MonoBehaviour outside the GuidedFury.* namespaces.
        /// </summary>
        private void NeutralizeForeignComponents()
        {
            if (neutralizeOwnRigidbody)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    bool wasActive = !rb.isKinematic || rb.useGravity;
                    // Zero velocities BEFORE flipping kinematic — PhysX warns (and ignores the
                    // write) when velocity is set on an already-kinematic body.
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    if (wasActive)
                        Debug.Log($"[GuidedFury] {name}: neutralized Rigidbody (set kinematic, gravity off). Vendor prefab compatibility.");
                }
            }

            if (disableForeignScriptsOnLaunch)
            {
                var siblings = GetComponents<MonoBehaviour>();
                foreach (var c in siblings)
                {
                    if (c == null) continue;
                    if (ReferenceEquals(c, this)) continue;

                    string ns = c.GetType().Namespace ?? "";
                    bool isGuidedFury = ns.StartsWith("GuidedFury");
                    if (isGuidedFury) continue;

                    // Skip plainly-decorative Unity engine components — they don't drive motion.
                    // (TrailRenderer, ParticleSystem etc. are sealed engine types so a MonoBehaviour
                    // here is always a user script anyway. Belt + suspenders.)
                    if (c.GetType().FullName?.StartsWith("UnityEngine") == true) continue;

                    if (c.enabled)
                    {
                        c.enabled = false;
                        Debug.LogWarning(
                            $"[GuidedFury] {name}: disabled foreign script '{c.GetType().FullName}' on launch. " +
                            "It was competing with MissileBehaviour for motion control. " +
                            "Either strip the script from your prefab, or set 'Disable Foreign Scripts On Launch' = false on the MissileBehaviour to keep it.");
                    }
                }

                // Vendor prefabs can carry a whole embedded rig, not just scripts. An enabled
                // Camera anywhere under the missile becomes a fullscreen view that hijacks the
                // display on spawn and dies with the missile (seen with ESSM Shell's baked
                // "Missile Cam", far clip 1000 -> the range "disappears" mid-flight). An extra
                // AudioListener draws "2 audio listeners" warnings every frame. Disable both
                // anywhere in our hierarchy; same opt-out as foreign scripts.
                foreach (var cam in GetComponentsInChildren<Camera>(includeInactive: true))
                {
                    if (cam.enabled)
                    {
                        cam.enabled = false;
                        Debug.LogWarning(
                            $"[GuidedFury] {name}: disabled embedded Camera '{cam.gameObject.name}' on launch. " +
                            "It was rendering over the scene cameras. Strip it from the prefab, or set " +
                            "'Disable Foreign Scripts On Launch' = false to keep it.");
                    }
                }
                foreach (var listener in GetComponentsInChildren<AudioListener>(includeInactive: true))
                {
                    if (listener.enabled)
                        listener.enabled = false;
                }
            }
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
        /// <summary>
        /// The highest LOD that has an actual integrator in the codebase right now. Used to
        /// auto-downgrade when the user picks an unimplemented LOD. Bump this as L4 / L5 land.
        /// </summary>
        public const MissileLod HighestImplementedLod = MissileLod.L4_FullAero6Dof;

        /// <summary>
        /// Build an integrator for the requested LOD, OR null if that LOD isn't implemented
        /// yet. Caller is responsible for handling the null (typically: log + downgrade).
        ///
        /// The L4 integrator needs an <see cref="IAeroModel"/> — built from the profile's
        /// `AeroModel` enum and (for Tabulated) the AnimationCurves on the SO. Lower LODs
        /// ignore aero kind / curves.
        ///
        /// We don't throw here even for unknown enum values — the factory's job is "produce
        /// an integrator or signal you can't"; the policy of what to do about that lives one
        /// layer up in Launch.
        /// </summary>
        private IPhysicsIntegrator CreateIntegratorForLod(MissileLod lod, MissileProfileSO so, in MissileProfileData data)
        {
            switch (lod)
            {
                case MissileLod.L0_Kinematic:        return new KinematicL0Integrator();
                case MissileLod.L1_PointMass3Dof:    return new PointMass3DofL1Integrator();
                case MissileLod.L2_RateLimited3Dof:  return new RateLimited3DofL2Integrator();
                case MissileLod.L3_PseudoRb6Dof:     return new PseudoRb6DofL3Integrator();
                case MissileLod.L4_FullAero6Dof:     return new FullAero6DofL4Integrator(
                                                            BuildAeroModel(so, data),
                                                            AutopilotFactory.Create(data.Autopilot),
                                                            ThrustModelFactory.Create(data.ThrustModel));
                default:                             return null;   // L5 / unknown — Launch downgrades
            }
        }

        /// <summary>
        /// Build the aero model for L4 based on the profile's <see cref="AeroModelKind"/>.
        /// Tabulated mode reads the AnimationCurves directly from the SO (curves are managed
        /// references that don't fit in the unmanaged profile struct).
        /// </summary>
        private static IAeroModel BuildAeroModel(MissileProfileSO so, in MissileProfileData data)
        {
            switch (data.AeroModel)
            {
                case AeroModelKind.Simple:
                    return SimpleAeroModel.Instance;
                case AeroModelKind.Tabulated:
                    return new TabulatedAeroModel(
                        so.cdVsMach, so.clAlphaVsMach, so.cmAlphaVsMach, so.cmDeltaVsMach);
                default:
                    return SimpleAeroModel.Instance;
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
            // Position used for the FX spawn — prefer the cached fuze hit point (the actual
            // contact) over the missile's last-frame position, which can be slightly past the
            // target after the OverlapSphere catches it.
            Vector3 fxPos = hasHitPoint ? lastHitPoint : transform.position;

            SpawnExplosionEffects(fxPos);
            ApplyBlastImpulse(fxPos);
            StopFlightAudio();

            // Phase 2 cleanup: disable. Pooling and effects come later.
            launched = false;
            gameObject.SetActive(false);
        }

        // ============================================================
        // Audio + explosion FX
        // ============================================================

        /// <summary>
        /// Spawn the appropriate explosion prefab (aerial vs ground based on altitude) and
        /// play the one-shot explosion clip at the same point. Both pieces are optional —
        /// a profile with neither configured will detonate silently and invisibly. The
        /// spawned prefab is auto-destroyed after `explosionLifetimeS` so trails and lights
        /// don't accumulate in the scene over the course of a long run.
        /// </summary>
        // Shared, allocation-free scratch buffers for the blast overlap query. Static is
        // fine: ApplyBlastImpulse runs on the main thread only, and the buffers are fully
        // re-filled/cleared on every call.
        private static readonly Collider[] BlastOverlapBuffer = new Collider[256];
        private static readonly HashSet<Rigidbody> BlastSeenBodies = new HashSet<Rigidbody>();

        /// <summary>
        /// Physical blast: applies an outward impulse to every non-kinematic Rigidbody within
        /// the profile's blast radius, with Unity's built-in linear distance falloff
        /// (AddExplosionForce) and an upwards modifier for a cinematic toss. Disabled when
        /// blastRadiusM is 0. All tuning lives on the MissileProfileSO.
        /// </summary>
        private void ApplyBlastImpulse(Vector3 worldPos)
        {
            if (profile == null || profile.blastRadiusM <= 0f || profile.blastImpulseNs <= 0f)
                return;

            int count = Physics.OverlapSphereNonAlloc(worldPos, profile.blastRadiusM, BlastOverlapBuffer);
            if (count >= BlastOverlapBuffer.Length)
                Debug.LogWarning($"[GuidedFury] {name}: blast overlap filled its {BlastOverlapBuffer.Length}-collider buffer; " +
                                 "some rigidbodies may not receive the impulse. Consider a smaller blastRadiusM.");

            BlastSeenBodies.Clear();
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = BlastOverlapBuffer[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (rb.transform.root == transform.root) continue;      // never punt ourselves
                if (!BlastSeenBodies.Add(rb)) continue;                 // one impulse per body, not per collider

                rb.AddExplosionForce(profile.blastImpulseNs, worldPos, profile.blastRadiusM,
                                     profile.blastUpwardsModifier, ForceMode.Impulse);
            }
            BlastSeenBodies.Clear();
        }

        private void SpawnExplosionEffects(Vector3 worldPos)
        {
            if (profile == null) return;

            bool aerial = worldPos.y > profile.aerialAltitudeThresholdM;
            GameObject prefab = aerial ? profile.explosionPrefabAerial : profile.explosionPrefabGround;
            // Fall back to the *other* prefab if only one is wired — better some VFX than none.
            if (prefab == null)
                prefab = aerial ? profile.explosionPrefabGround : profile.explosionPrefabAerial;

            if (prefab != null)
            {
                var fx = Instantiate(prefab, worldPos, Quaternion.identity);
                if (profile.explosionLifetimeS > 0f)
                    Destroy(fx, profile.explosionLifetimeS);
            }

            if (profile.explosionSfx != null)
            {
                AudioSource.PlayClipAtPoint(profile.explosionSfx, worldPos, profile.explosionSfxVolume);
            }
        }

        /// <summary>
        /// Play the launch one-shot and start the in-flight loop. The loop's AudioSource is
        /// parented to the missile transform so panning/distance attenuation follow it
        /// naturally. We don't create either if the profile leaves the clip empty.
        /// </summary>
        private void StartLaunchAudio(Vector3 launchPos)
        {
            if (profile == null) return;

            if (profile.launchSfx != null)
            {
                AudioSource.PlayClipAtPoint(profile.launchSfx, launchPos, profile.launchSfxVolume);
            }

            if (profile.flightSfx != null)
            {
                flightAudio = gameObject.AddComponent<AudioSource>();
                flightAudio.clip = profile.flightSfx;
                flightAudio.loop = true;
                flightAudio.volume = profile.flightSfxVolume;
                flightAudio.spatialBlend = profile.flightSfxSpatialBlend;
                flightAudio.rolloffMode = AudioRolloffMode.Linear;
                flightAudio.minDistance = 5f;
                flightAudio.maxDistance = profile.flightSfxMaxDistanceM;
                flightAudio.dopplerLevel = 0.5f;
                flightAudio.playOnAwake = false;
                flightAudio.Play();
            }
        }

        private void StopFlightAudio()
        {
            if (flightAudio == null) return;
            flightAudio.Stop();
            // Don't bother destroying — the GameObject is about to be SetActive(false) and,
            // once pooled/reused, the source will be reconfigured on the next Launch.
            Destroy(flightAudio);
            flightAudio = null;
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

                // Record the contact point for the FX spawn — use the collider's closest
                // point to the missile, which is where the warhead's actually going off.
                lastHitPoint = other.ClosestPoint(entity.State.Position);
                hasHitPoint = true;

                entity.Detonate();
                return; // one hit per FixedUpdate is plenty; cleanup in next FixedUpdate
            }
        }
    }
}
