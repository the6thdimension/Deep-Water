using System;
using UnityEngine;
using CarrierOps.Core.Recovery;
using CarrierOps.Core.State;
using CarrierOps.Core.Movement;
using CarrierOps.ScriptableObjects.Profiles;

namespace CarrierOps.Core.Carrier
{
    /// <summary>
    /// MonoBehaviour adapter for the <see cref="CarrierEntity"/>. Pumps the pure-C# core on
    /// FixedUpdate (P2), mirrors state to the Unity transform, and drives the Animators on
    /// the model's shuttle / JBD / elevator children.
    ///
    /// **Wiring:**
    /// - Assign a <see cref="CarrierProfileSO"/> in the Inspector.
    /// - Populate the <see cref="catapultSlots"/> array — one entry per real catapult, with
    ///   the shuttle's start and end transforms, JBD animator, attached aircraft Rigidbody.
    /// - Populate the <see cref="elevatorSlots"/> array similarly.
    ///
    /// **What this does NOT do (Phase 1 of the rewrite):**
    /// - Drive the optional FLOLS — separate phase.
    /// - Handle arresting wires — separate phase.
    /// - Apply wind-over-deck — separate phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarrierBehaviour : MonoBehaviour
    {
        // -- Inspector ------------------------------------------------------
        [Header("Profile")]
        [SerializeField] private CarrierProfileSO profile;

        [Header("Helm (Inspector or external)")]
        [Tooltip("[0..1] All-stop to full speed.")]
        [Range(0f, 1f)] [SerializeField] private float throttleNormalized = 0f;

        [Tooltip("[-1..+1] Full-left to full-right rudder.")]
        [Range(-1f, 1f)] [SerializeField] private float rudderNormalized = 0f;

        [Header("Catapult Slots")]
        [SerializeField] private CatapultSlot[] catapultSlots = Array.Empty<CatapultSlot>();

        [Header("Elevator Slots")]
        [SerializeField] private ElevatorSlot[] elevatorSlots = Array.Empty<ElevatorSlot>();

        [Header("Recovery — FLOLS")]
        [Tooltip("World-space reference point of the Fresnel lens (lens housing on the port side of the angled deck). FLOLS geometry is computed from this point against approaching aircraft.")]
        [SerializeField] private Transform flolsReference;

        [Tooltip("Transform whose local Y is driven by the FLOLS ball offset. Should be the 'ball' visual element under the lens housing. Optional — used only for cosmetics.")]
        [SerializeField] private Transform flolsBallTransform;

        [Tooltip("Local-Y range of the ball travel. Ball moves between -range/2 and +range/2 as the normalized offset goes -1..+1.")]
        [SerializeField] private float flolsBallTravelLocalUnits = 0.4f;

        [Tooltip("Optional GameObject containing the cut/wave-off lights — activated when the LSO calls wave-off.")]
        [SerializeField] private GameObject flolsCutLights;

        [Header("Recovery — Arresting Wires")]
        [Tooltip("One slot per physical wire on the deck. Wire engagement uses proximity to WireCenterline.")]
        [SerializeField] private WireSlot[] wireSlots = Array.Empty<WireSlot>();

        [Tooltip("Radius around each wire centerline within which a falling hook is considered to have caught the wire. Meters.")]
        [SerializeField] private float wireEngageRadiusM = 0.6f;

        // -- Runtime --------------------------------------------------------
        private CarrierEntity entity;
        private Vector3 baseLocalPosition; // settled-pose anchor (where sway is added on top)
        private Quaternion baseLocalRotation;

        // Cached previous JBD raised-state so we can detect rising/falling edges.
        private bool[] previousJbdRaised;
        // Cached previous catapult stage so we can fire "Launch" on the rising edge of Firing.
        private CatapultStage[] previousStage;

        // Animator parameter hashes — same pattern as the legacy CarrierController.
        private static readonly int Anim_Raise  = Animator.StringToHash("Raise");
        private static readonly int Anim_Lower  = Animator.StringToHash("Lower");
        private static readonly int Anim_Launch = Animator.StringToHash("Launch");

        // -- Public API (helm + commands) -----------------------------------
        public void SetThrottle(float t) { throttleNormalized = Mathf.Clamp01(t); }
        public void SetRudder(float r)   { rudderNormalized = Mathf.Clamp(r, -1f, 1f); }

        public void RequestCatapultLaunch(int catIndex) => entity?.RequestCatapultLaunch(catIndex);
        public void RequestElevator(int elevIndex, bool deploy) => entity?.RequestElevator(elevIndex, deploy);

        public CarrierEntity Entity => entity;
        public CarrierState State => entity?.State;

        // -- Unity lifecycle ------------------------------------------------
        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogError($"[CarrierBehaviour] {name} has no profile assigned.");
                return;
            }

            // Capture the settled pose. We never overwrite the base pose; sway is layered on top.
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;

            CarrierProfileData data = profile.Bake();
            entity = new CarrierEntity(in data);

            // Seed entity state with the current transform pose so the ship starts where the
            // designer placed it in the scene.
            entity.State.Position = transform.position;
            entity.State.HeadingDeg = transform.eulerAngles.y;

            previousJbdRaised = new bool[entity.State.Catapults.Length];
            previousStage = new CatapultStage[entity.State.Catapults.Length];
            for (int i = 0; i < previousStage.Length; i++)
                previousStage[i] = CatapultStage.Idle;
        }

        private void FixedUpdate()
        {
            if (entity == null) return;

            // 1. Step the simulation.
            var command = new ShipCommand
            {
                ThrottleNormalized = throttleNormalized,
                RudderNormalized = rudderNormalized,
            };
            entity.Step(in command, Time.fixedDeltaTime);

            // 2. Mirror position + heading + sway to the Unity transform.
            ApplyTransformFromState();

            // 3. Drive Animator triggers on edge transitions.
            DriveAnimators();

            // 4. Drive elevator transforms.
            DriveElevators();

            // 5. Drive any attached aircraft Rigidbody during catapult Firing / release.
            DriveAttachedAircraft();

            // 6. Recovery scene-geometry: sample FLOLS against the nearest approaching
            //    aircraft, then check for wire engagements. Done AFTER the entity Step
            //    so the world positions are based on the just-applied carrier pose.
            SampleAndPushFlols();
            CheckWireEngagements();

            // 7. Drive FLOLS visuals from the now-updated state.
            DriveFlolsVisuals();
        }

        // -- Per-subsystem drivers -----------------------------------------
        private void ApplyTransformFromState()
        {
            CarrierState s = entity.State;

            // Position: ground position from kinematics + heave from sway.
            Vector3 pos = s.Position + s.SwayOffset;
            transform.position = pos;

            // Rotation: heading around Y, with sway pitch + roll layered on top (small angles
            // — order matters less than that they compose deterministically).
            Quaternion heading = Quaternion.Euler(0f, s.HeadingDeg, 0f);
            Quaternion sway = Quaternion.Euler(s.SwayPitchDeg, 0f, s.SwayRollDeg);
            transform.rotation = heading * sway;
        }

        private void DriveAnimators()
        {
            if (catapultSlots == null) return;
            int count = Mathf.Min(catapultSlots.Length, entity.State.Catapults.Length);
            for (int i = 0; i < count; i++)
            {
                ref CatapultState cat = ref entity.State.Catapults[i];
                CatapultSlot slot = catapultSlots[i];

                // Rising edge of Firing → fire shuttle "Launch" trigger (for any cosmetic
                // animation hooked to it). Our code drives the actual motion of the aircraft.
                if (previousStage[i] != CatapultStage.Firing && cat.Stage == CatapultStage.Firing)
                {
                    if (slot.ShuttleAnimator != null)
                        slot.ShuttleAnimator.SetTrigger(Anim_Launch);
                }
                previousStage[i] = cat.Stage;

                // JBD edges.
                if (cat.JbdRaised && !previousJbdRaised[i])
                {
                    if (slot.JbdAnimator != null) slot.JbdAnimator.SetTrigger(Anim_Raise);
                }
                else if (!cat.JbdRaised && previousJbdRaised[i])
                {
                    if (slot.JbdAnimator != null) slot.JbdAnimator.SetTrigger(Anim_Lower);
                }
                previousJbdRaised[i] = cat.JbdRaised;
            }
        }

        private void DriveElevators()
        {
            if (elevatorSlots == null) return;
            int count = Mathf.Min(elevatorSlots.Length, entity.State.Elevators.Length);
            for (int i = 0; i < count; i++)
            {
                ref ElevatorState elev = ref entity.State.Elevators[i];
                ElevatorSlot slot = elevatorSlots[i];
                if (slot.LiftTransform == null) continue;

                // Lerp between stowed local position (slot's authored value) and the deployed
                // offset along its local axis.
                Vector3 stowed = slot.StowedLocalPosition;
                Vector3 deployed = stowed + slot.DeployedLocalOffset;
                slot.LiftTransform.localPosition = Vector3.Lerp(stowed, deployed, elev.Travel);
            }
        }

        private void DriveAttachedAircraft()
        {
            if (catapultSlots == null) return;
            int count = Mathf.Min(catapultSlots.Length, entity.State.Catapults.Length);
            for (int i = 0; i < count; i++)
            {
                ref CatapultState cat = ref entity.State.Catapults[i];
                CatapultSlot slot = catapultSlots[i];
                if (slot.AttachedAircraft == null) continue;
                if (slot.ShuttleStart == null || slot.ShuttleEnd == null) continue;

                bool isFiring = cat.Stage == CatapultStage.Firing;
                bool wasFiringNowReleased =
                    cat.Stage == CatapultStage.Retracting &&
                    previousStage[i] != CatapultStage.Retracting; // edge only

                if (isFiring)
                {
                    // Aircraft tracks shuttle position. Kinematic so physics doesn't fight us.
                    slot.AttachedAircraft.isKinematic = true;
                    float t = Mathf.Clamp01(cat.ShuttleDistanceM / Mathf.Max(entity.Profile.CatapultStrokeM, 0.01f));
                    Vector3 worldPos = Vector3.Lerp(slot.ShuttleStart.position, slot.ShuttleEnd.position, t);
                    slot.AttachedAircraft.position = worldPos;
                    // Orient along the track so the aircraft points where it's going.
                    Vector3 dir = (slot.ShuttleEnd.position - slot.ShuttleStart.position);
                    if (dir.sqrMagnitude > 1e-4f)
                        slot.AttachedAircraft.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                else if (wasFiringNowReleased)
                {
                    // Hand off to physics with the end-of-run velocity.
                    Vector3 dir = (slot.ShuttleEnd.position - slot.ShuttleStart.position);
                    if (dir.sqrMagnitude > 1e-4f)
                    {
                        slot.AttachedAircraft.isKinematic = false;
                        slot.AttachedAircraft.linearVelocity = dir.normalized * cat.ShuttleVelocityMps;
                    }
                }
            }
        }

        // -- Recovery: FLOLS sampling --------------------------------------
        private void SampleAndPushFlols()
        {
            if (entity == null) return;
            if (flolsReference == null)
            {
                entity.SetFlolsState(FlolsState.NoTrack);
                return;
            }

            // Pick the closest approaching aircraft as "the one on the ball". Multi-aircraft
            // approach is a future concern; with a single recovery slot at a time we just
            // pick the nearest.
            IRecoveringAircraft nearest = null;
            float bestDistSq = float.MaxValue;
            Vector3 flolsWorld = flolsReference.position;

            foreach (var a in entity.RecoveringAircraft)
            {
                if (a == null) continue;
                float d = (a.Position - flolsWorld).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    nearest = a;
                }
            }

            if (nearest == null)
            {
                entity.SetFlolsState(FlolsState.NoTrack);
                return;
            }

            // Ship forward in world frame from current heading.
            float headingRad = entity.State.HeadingDeg * Mathf.Deg2Rad;
            Vector3 shipForward = new Vector3(Mathf.Sin(headingRad), 0f, Mathf.Cos(headingRad));

            var flols = FlolsModel.Sample(in entity.Profile, flolsWorld, shipForward, nearest.Position);
            entity.SetFlolsState(in flols);
        }

        // -- Recovery: wire engagement detection ---------------------------
        private void CheckWireEngagements()
        {
            if (entity == null || wireSlots == null) return;
            int count = Mathf.Min(wireSlots.Length, entity.State.Wires.Length);
            if (count == 0) return;

            // For each idle wire, find the closest hook-down aircraft whose hook tip is
            // within engage radius. If found, request engagement.
            for (int w = 0; w < count; w++)
            {
                ref WireState wire = ref entity.State.Wires[w];
                if (wire.Stage != WireStage.Idle) continue; // already engaged

                WireSlot slot = wireSlots[w];
                if (slot.WireCenterline == null) continue;

                Vector3 wireCenter = slot.WireCenterline.position;
                float bestDistSq = wireEngageRadiusM * wireEngageRadiusM;
                IRecoveringAircraft best = null;

                foreach (var a in entity.RecoveringAircraft)
                {
                    if (a == null || !a.HookDown) continue;
                    float d = (a.HookTipPosition - wireCenter).sqrMagnitude;
                    if (d < bestDistSq)
                    {
                        bestDistSq = d;
                        best = a;
                    }
                }

                if (best != null)
                    entity.RequestWireEngage(w, best.RegistrationId, best.Velocity.magnitude);
            }
        }

        // -- Recovery: drive FLOLS visuals ---------------------------------
        private void DriveFlolsVisuals()
        {
            FlolsState f = entity.State.Flols;

            if (flolsBallTransform != null)
            {
                // Track the ball when we have a track; recenter when we don't.
                float t = f.HasTrack ? f.BallOffsetNormalized : 0f;
                Vector3 local = flolsBallTransform.localPosition;
                local.y = t * (flolsBallTravelLocalUnits * 0.5f);
                flolsBallTransform.localPosition = local;
            }

            if (flolsCutLights != null)
            {
                bool show = f.HasTrack && f.IsWaveOff;
                if (flolsCutLights.activeSelf != show)
                    flolsCutLights.SetActive(show);
            }
        }

        // -- Serializable slot types ---------------------------------------
        [Serializable]
        public struct CatapultSlot
        {
            [Tooltip("Animator on the shuttle GameObject. Optional — used for cosmetic effects only; actual motion comes from the state.")]
            public Animator ShuttleAnimator;

            [Tooltip("Animator on the JBD (Jet Blast Deflector) GameObject. Receives Raise/Lower triggers.")]
            public Animator JbdAnimator;

            [Tooltip("World position of the shuttle at distance 0 (aft end of catapult track).")]
            public Transform ShuttleStart;

            [Tooltip("World position of the shuttle at end of stroke (forward end).")]
            public Transform ShuttleEnd;

            [Tooltip("Aircraft Rigidbody currently attached to this catapult. Optional.")]
            public Rigidbody AttachedAircraft;
        }

        [Serializable]
        public struct ElevatorSlot
        {
            [Tooltip("Transform that moves between stowed and deployed positions.")]
            public Transform LiftTransform;

            [Tooltip("Local position of the lift transform when stowed (deck level).")]
            public Vector3 StowedLocalPosition;

            [Tooltip("Local offset added to StowedLocalPosition when fully deployed.")]
            public Vector3 DeployedLocalOffset;
        }

        [Serializable]
        public struct WireSlot
        {
            [Tooltip("Transform whose position marks the wire's centerline (the point the hook should cross).")]
            public Transform WireCenterline;

            [Tooltip("Optional Obi rope or visual wire object — Phase 2 doesn't drive its stretch, just stores the reference for future use.")]
            public GameObject WireVisual;
        }
    }
}
