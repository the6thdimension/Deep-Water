using UnityEngine;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Seekers;

namespace GuidedFury.ScriptableObjects.Profiles
{
    /// <summary>
    /// Authoring asset for a missile profile. Designers / sim engineers edit this; the
    /// runtime entity carries a baked `MissileProfileData` struct copy.
    ///
    /// **Design notes:**
    /// - The SO holds NO logic, only data. It's a typed container with tooltips.
    /// - It bakes to the unmanaged struct via `Bake()`. The baked copy is what flies — edits
    ///   to the SO at runtime won't affect missiles already in the air. This is intentional:
    ///   live-tuning is a tooling concern (a dedicated dev UI), not a default behavior. It
    ///   prevents accidental "I tweaked a slider and the missile in flight retroactively
    ///   changed" surprises.
    /// - LOD selection is NOT a property of the missile — it's a property of the *launch*.
    ///   The same ESSM asset can fly at L0 in a distant salvo or L4 in the player's
    ///   engagement. LOD lives on the launcher / MissileBehaviour, not here.
    /// - No reflection-based field setting (anti-pattern AP1). The `Bake()` method does an
    ///   explicit field-by-field copy. Adding a new field means touching both the SO and
    ///   `Bake()` — that's a feature, not a bug: it forces a conscious decision.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMissileProfile", menuName = "Guided Fury/Missile Profile", order = 1)]
    public class MissileProfileSO : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Short identifier code (e.g. 'RIM-162' for ESSM).")]
        public string missileId = "GENERIC";

        [Tooltip("Friendly display name (e.g. 'Evolved Sea Sparrow Missile').")]
        public string displayName = "Generic Missile";

        [Tooltip("One-line description for selection UI and AAR.")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Optional icon for selection UI.")]
        public Sprite icon;

        [Header("Mass Properties")]
        [Tooltip("Airframe + warhead + electronics mass, propellant excluded. Kilograms.")]
        public float dryMassKg = 100f;

        [Tooltip("Total propellant carried at launch. Kilograms.")]
        public float propellantMassKg = 50f;

        [Header("Propulsion (Boost-only Phase 1)")]
        [Tooltip("Boost motor thrust. Newtons. Used by L1+ — L0 ignores and uses CruiseSpeed instead.")]
        public float boostThrustN = 30000f;

        [Tooltip("Boost motor burn duration. Seconds.")]
        public float boostDurationS = 3f;

        [Header("L0 Kinematic Tier")]
        [Tooltip("Cruise speed used by L0 (and as a floor by L1+). Meters/second.")]
        public float cruiseSpeedMps = 250f;

        [Tooltip("If true, L0 integrates gravity (ballistic arc). If false, L0 follows guidance command at cruise speed.")]
        public bool l0UseGravity = false;

        [Header("L1 Point-Mass Aerodynamics")]
        [Tooltip("Scalar drag coefficient (Cd, dimensionless). Used in F_drag = 0.5*rho*v²*Cd*A.")]
        public float dragCoefficient = 0.3f;

        [Tooltip("Reference cross-section area for drag. Square meters. Roughly π·(diameter/2)² for a cylindrical body.")]
        public float referenceAreaM2 = 0.03f;

        [Header("L2 Maneuver Limits")]
        [Tooltip("Structural g-load limit on commanded lateral acceleration. AAM ~30–50, SAM ~20–35.")]
        public float maxLoadFactorG = 30f;

        [Tooltip("Upper bound on velocity-vector rotation rate. Degrees / second.")]
        public float maxTurnRateDegPerSec = 60f;

        [Header("L3 Pseudo-6DOF Aerodynamics")]
        [Tooltip("Pitch / yaw moment of inertia (axisymmetric body). Kilogram-meters squared.")]
        public float transverseInertiaKgM2 = 5f;

        [Tooltip("Roll moment of inertia (about the long axis). Kilogram-meters squared. Typically small.")]
        public float rollInertiaKgM2 = 0.5f;

        [Tooltip("Lift coefficient slope (Cl_alpha) per radian of AoA. Slender-body bodies are ~6–10.")]
        public float liftSlopePerRad = 8f;

        [Tooltip("Stall angle. Lift collapses past this AoA. Degrees.")]
        public float stallAoaDeg = 18f;

        [Tooltip("Passive aerodynamic restoring-moment coefficient. Higher = more aggressively self-aligning ('weather-vane').")]
        public float weatherVaneCoefficient = 5f;

        [Tooltip("Inner-loop rate-controller gain. Higher = more aggressive attitude tracking of guidance commands. Too high = oscillation.")]
        public float autopilotGain = 4f;

        [Header("Seeker")]
        [Tooltip("Which seeker the missile carries. None = guidance reads truth directly (Phase 1/2 behavior).")]
        public SeekerKind seekerKind = SeekerKind.None;

        [Tooltip("Full cone angle of the seeker FOV (centered on missile forward axis). Degrees.")]
        public float seekerFovDeg = 30f;

        [Tooltip("Maximum acquisition / lock-retention range. Meters.")]
        public float seekerMaxRangeM = 5000f;

        [Tooltip("Dwell time required for seeker to declare lock once target is in FOV and range. Seconds.")]
        public float seekerAcquisitionTimeS = 0.25f;

        [Header("Guidance")]
        [Tooltip("Which guidance law to instantiate at launch. None = ballistic; ProNav = workhorse intercept.")]
        public GuidanceLawKind guidanceLaw = GuidanceLawKind.ProportionalNavigation;

        [Tooltip("Navigation gain. ProNav typically 3..5. Used as the single gain for Pursuit too.")]
        public float navigationGain = 3f;

        [Header("Lifetime & Fuzing")]
        [Tooltip("Hard upper bound on time of flight before self-destruct. Seconds.")]
        public float maxLifetimeS = 30f;

        [Tooltip("Proximity fuze trigger radius. <=0 disables proximity. Meters.")]
        public float fuzeProximityRadiusM = 5f;

        [Tooltip("Safe-and-arm delay. The fuze is inert for this many seconds after launch, "
                 + "preventing detonation on the launch rail / launcher / nearby geometry.")]
        public float fuzeArmDelayS = 0.5f;

        /// <summary>
        /// Produce an unmanaged runtime copy of this profile. Called once per missile at
        /// launch; the returned struct travels with the entity for its lifetime.
        /// </summary>
        public MissileProfileData Bake()
        {
            return new MissileProfileData
            {
                DryMassKg            = dryMassKg,
                PropellantMassKg     = propellantMassKg,
                BoostThrustN         = boostThrustN,
                BoostDurationS       = boostDurationS,
                CruiseSpeedMps       = cruiseSpeedMps,
                L0UseGravity         = l0UseGravity,
                DragCoefficient      = dragCoefficient,
                ReferenceAreaM2      = referenceAreaM2,
                MaxLoadFactorG       = maxLoadFactorG,
                MaxTurnRateDegPerSec = maxTurnRateDegPerSec,
                TransverseInertiaKgM2  = transverseInertiaKgM2,
                RollInertiaKgM2        = rollInertiaKgM2,
                LiftSlopePerRad        = liftSlopePerRad,
                StallAoaDeg            = stallAoaDeg,
                WeatherVaneCoefficient = weatherVaneCoefficient,
                AutopilotGain          = autopilotGain,
                SeekerKind             = seekerKind,
                SeekerFovDeg           = seekerFovDeg,
                SeekerMaxRangeM        = seekerMaxRangeM,
                SeekerAcquisitionTimeS = seekerAcquisitionTimeS,
                GuidanceLaw          = guidanceLaw,
                NavigationGain       = navigationGain,
                MaxLifetimeS         = maxLifetimeS,
                FuzeProximityRadiusM = fuzeProximityRadiusM,
                FuzeArmDelayS        = fuzeArmDelayS,
            };
        }

        /// <summary>
        /// Lightweight editor-time sanity check. Called automatically by Unity when values
        /// change in the Inspector. Catches the most common authoring mistakes early.
        /// </summary>
        private void OnValidate()
        {
            if (dryMassKg < 0f) dryMassKg = 0f;
            if (propellantMassKg < 0f) propellantMassKg = 0f;
            if (boostThrustN < 0f) boostThrustN = 0f;
            if (boostDurationS < 0f) boostDurationS = 0f;
            if (cruiseSpeedMps < 0f) cruiseSpeedMps = 0f;
            if (dragCoefficient < 0f) dragCoefficient = 0f;
            if (referenceAreaM2 < 0f) referenceAreaM2 = 0f;
            if (navigationGain < 0f) navigationGain = 0f;
            if (maxLifetimeS <= 0f) maxLifetimeS = 1f;
            if (fuzeArmDelayS < 0f) fuzeArmDelayS = 0f;
            if (maxLoadFactorG < 0f) maxLoadFactorG = 0f;
            if (maxTurnRateDegPerSec < 0f) maxTurnRateDegPerSec = 0f;
            if (transverseInertiaKgM2 < 1e-3f) transverseInertiaKgM2 = 1e-3f; // guard against div-by-zero in L3
            if (rollInertiaKgM2 < 1e-3f) rollInertiaKgM2 = 1e-3f;
            if (liftSlopePerRad < 0f) liftSlopePerRad = 0f;
            if (stallAoaDeg < 1f) stallAoaDeg = 1f;
            if (weatherVaneCoefficient < 0f) weatherVaneCoefficient = 0f;
            if (autopilotGain < 0f) autopilotGain = 0f;
            if (seekerFovDeg < 0f) seekerFovDeg = 0f;
            if (seekerFovDeg > 180f) seekerFovDeg = 180f;
            if (seekerMaxRangeM < 0f) seekerMaxRangeM = 0f;
            if (seekerAcquisitionTimeS < 0f) seekerAcquisitionTimeS = 0f;
        }
    }
}
