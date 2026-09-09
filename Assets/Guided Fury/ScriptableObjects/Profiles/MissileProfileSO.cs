using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Propulsion;
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

        [Header("Propulsion")]
        [Tooltip("Boost motor thrust. Newtons. Used by L1+ — L0 ignores and uses CruiseSpeed instead.")]
        public float boostThrustN = 30000f;

        [Tooltip("Boost motor burn duration. Seconds.")]
        public float boostDurationS = 3f;

        [Tooltip("ConstantBoost = single boost stage (L0..L3 default). BoostSustain = adds a lower-thrust sustain stage after boost (typical AAM motor profile).")]
        public ThrustModelKind thrustModel = ThrustModelKind.ConstantBoost;

        [Tooltip("Sustain motor thrust. Lower than boost. Only used when thrustModel = BoostSustain.")]
        public float sustainThrustN = 0f;

        [Tooltip("Sustain stage duration. Only used when thrustModel = BoostSustain.")]
        public float sustainDurationS = 0f;

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

        [Header("L4 Full Aero (Tabulated)")]
        [Tooltip("Aero model kind. Simple = scalar (L3-equivalent fallback). Tabulated = use the curves below; Mach-aware.")]
        public AeroModelKind aeroModel = AeroModelKind.Simple;

        [Tooltip("Reference length for moment arm calculations. Typical missile body length in meters.")]
        public float lengthM = 3f;

        [Tooltip("Drag coefficient Cd as a function of Mach number. Authored: ~0.3 subsonic, spiking to ~0.6 around M=1, easing back to ~0.4 supersonic.")]
        public AnimationCurve cdVsMach = AnimationCurve.Linear(0f, 0.3f, 5f, 0.3f);

        [Tooltip("Lift slope Cl_α (per radian) as a function of Mach. Typically peaks subsonic, drops past M≈1.5.")]
        public AnimationCurve clAlphaVsMach = AnimationCurve.Linear(0f, 8f, 5f, 5f);

        [Tooltip("Pitch stability Cm_α (per radian) as a function of Mach. NEGATIVE for a stable airframe (body returns toward velocity).")]
        public AnimationCurve cmAlphaVsMach = AnimationCurve.Linear(0f, -2f, 5f, -1.5f);

        [Tooltip("Control surface effectiveness Cm_δ (per radian) as a function of Mach. Typically negative (positive δ produces nose-down moment). Magnitude drops at high Mach.")]
        public AnimationCurve cmDeltaVsMach = AnimationCurve.Linear(0f, -2f, 5f, -1f);

        [Header("L4 Autopilot + Control Surfaces")]
        [Tooltip("SimpleRate (L3-style direct torque) or SurfaceDeflection (L4 with fins driving aero moment via Cm_δ).")]
        public AutopilotKind autopilot = AutopilotKind.SimpleRate;

        [Tooltip("Max fin deflection. Typical AAM ±20°.")]
        public float maxControlDeflectionDeg = 20f;

        [Tooltip("Max fin deflection rate. Typical 200..600°/s. Inner-loop saturation; integrator rate-limits the actual surface.")]
        public float maxControlRateDegPerSec = 400f;

        [Header("Seeker")]
        [Tooltip("Which seeker the missile carries. None = guidance reads truth directly (Phase 1/2 behavior).")]
        public SeekerKind seekerKind = SeekerKind.None;

        [Tooltip("Full cone angle of the seeker FOV (centered on missile forward axis). Degrees.")]
        public float seekerFovDeg = 30f;

        [Tooltip("Maximum acquisition / lock-retention range. Meters.")]
        public float seekerMaxRangeM = 5000f;

        [Tooltip("Dwell time required for seeker to declare lock once target is in FOV and range. Seconds.")]
        public float seekerAcquisitionTimeS = 0.25f;

        [Tooltip("Track memory ('coast') after break-lock, seconds. The seeker keeps reporting a " +
                 "dead-reckoned track for this long when the target leaves the FOV cone, and " +
                 "reacquires without a dwell penalty. Carries guidance through the terminal LOS " +
                 "swing where a body-fixed seeker always breaks lock. 0 = drop lock instantly.")]
        public float seekerCoastTimeS = 0f;

        [Tooltip("Midcourse datalink: while the seeker has no lock, guidance flies on the truth " +
                 "track (launcher-fed command guidance / TVM midcourse), handing over to the seeker " +
                 "on acquisition. Required for vertical launch and any shot where the target starts " +
                 "outside the seeker cone. Off = seeker-only (missile flies ballistic until lock).")]
        public bool seekerMidcourseDatalink = false;

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

        [Header("Detonation Effects")]
        [Tooltip("Prefab spawned at the impact point when the target is AERIAL (above " +
                 "aerialAltitudeThresholdM). Typically an air-burst FX. Leave null to skip.")]
        public GameObject explosionPrefabAerial;

        [Tooltip("Prefab spawned at the impact point when the target is GROUND/SURFACE (at or " +
                 "below aerialAltitudeThresholdM). Typically a dirt+fireball FX. Leave null to skip.")]
        public GameObject explosionPrefabGround;

        [Tooltip("World-Y above this is considered an AERIAL detonation; at or below is GROUND. " +
                 "Meters. Tune for your scene's ground height (default 5 m suits a flat range at y=0).")]
        public float aerialAltitudeThresholdM = 5f;

        [Tooltip("How long to keep the spawned explosion alive before destroying it. Seconds. " +
                 "Set well past the visible burst so trails fade naturally. <=0 keeps it forever (relies on the prefab self-destructing).")]
        public float explosionLifetimeS = 6f;

        [Header("Audio (optional)")]
        [Tooltip("One-shot clip played at the muzzle the moment the missile launches.")]
        public AudioClip launchSfx;

        [Tooltip("Looping clip played from the missile while it's in flight (rocket motor / whoosh).")]
        public AudioClip flightSfx;

        [Tooltip("One-shot clip played at the impact point when the warhead detonates.")]
        public AudioClip explosionSfx;

        [Range(0f, 1f)]
        [Tooltip("Volume of the launch SFX.")]
        public float launchSfxVolume = 1f;

        [Range(0f, 1f)]
        [Tooltip("Volume of the looped in-flight SFX. Keep moderate so it doesn't drown the scene.")]
        public float flightSfxVolume = 0.7f;

        [Range(0f, 1f)]
        [Tooltip("Volume of the explosion SFX.")]
        public float explosionSfxVolume = 1f;

        [Header("Detonation — Physical Blast")]
        [Tooltip("Radius of the physical blast in meters. Non-kinematic Rigidbodies inside get " +
                 "an outward impulse with linear distance falloff (Unity AddExplosionForce). " +
                 "0 disables the physical blast entirely.")]
        public float blastRadiusM = 0f;

        [Tooltip("Impulse applied to a rigidbody at the blast center, in N*s (ForceMode.Impulse). " +
                 "A 1500 kg vehicle 0 m from a 20000 N*s blast picks up ~13 m/s.")]
        public float blastImpulseNs = 20000f;

        [Tooltip("AddExplosionForce upwards modifier, meters. Shifts the effective blast origin " +
                 "downward so objects get tossed up as well as away - more cinematic knockback.")]
        public float blastUpwardsModifier = 1.5f;

        [Tooltip("3D spatial blend for the in-flight loop. 1 = fully 3D (positional); 0 = 2D (always center).")]
        [Range(0f, 1f)]
        public float flightSfxSpatialBlend = 1f;

        [Tooltip("Max audible distance for the in-flight loop. Beyond this the AudioSource is silent.")]
        public float flightSfxMaxDistanceM = 800f;

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
                ThrustModel          = thrustModel,
                SustainThrustN       = sustainThrustN,
                SustainDurationS     = sustainDurationS,
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
                AeroModel              = aeroModel,
                LengthM                = lengthM,
                Autopilot              = autopilot,
                MaxControlDeflectionDeg = maxControlDeflectionDeg,
                MaxControlRateDegPerSec = maxControlRateDegPerSec,
                SeekerKind             = seekerKind,
                SeekerFovDeg           = seekerFovDeg,
                SeekerMaxRangeM        = seekerMaxRangeM,
                SeekerAcquisitionTimeS = seekerAcquisitionTimeS,
                SeekerCoastTimeS       = seekerCoastTimeS,
                SeekerMidcourseDatalink = seekerMidcourseDatalink,
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
