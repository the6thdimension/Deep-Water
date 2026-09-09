using GuidedFury.Core.Aero;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Propulsion;
using GuidedFury.Core.Seekers;

namespace GuidedFury.Core.Missile
{
    /// <summary>
    /// Runtime, unmanaged-friendly snapshot of a missile profile.
    ///
    /// **Why this exists separately from MissileProfileSO:** ScriptableObjects are managed
    /// `UnityEngine.Object` instances — they don't go into Burst Jobs, and reading them at
    /// 60 Hz × N missiles from a hot path is a maintainability/clarity hazard. The SO is the
    /// authoring surface; this struct is what the entity actually carries. The SO produces
    /// a `MissileProfileData` once at launch via `Bake()` and the entity owns the copy.
    ///
    /// **Field growth policy:** add fields per-LOD as integrators come online. Adding fields
    /// is non-breaking; default-zero is a valid "empty profile." Removing fields IS breaking
    /// — be deliberate.
    /// </summary>
    public struct MissileProfileData
    {
        // -- Identification ---------------------------------------------------
        // (string is managed and prevents Burst — id/name stay on the SO, not here)

        // -- Mass properties --------------------------------------------------
        public float DryMassKg;          // airframe + warhead + electronics, propellant-free
        public float PropellantMassKg;   // total propellant carried at launch

        // -- Propulsion -------------------------------------------------------
        public float BoostThrustN;       // newtons, constant during boost
        public float BoostDurationS;     // seconds; boost stage duration

        // L4+: optional sustain stage (boost-sustain motors — most tactical AAMs).
        // Ignored when ThrustModel == ConstantBoost.
        public ThrustModelKind ThrustModel; // ConstantBoost or BoostSustain
        public float SustainThrustN;        // newtons, constant during sustain
        public float SustainDurationS;      // seconds; sustain stage duration

        // -- L0 kinematic tier ------------------------------------------------
        public float CruiseSpeedMps;     // L0 constant-speed cruise (and floor for L1+)
        public bool  L0UseGravity;       // true = ballistic + commanded steering, false = pure command-following

        // -- L1 point-mass tier (Phase 2) -------------------------------------
        public float DragCoefficient;    // dimensionless Cd; total scalar drag for L1
        public float ReferenceAreaM2;    // body cross-section for drag computation

        // -- L2 rate-limited tier (Phase 3) -----------------------------------
        public float MaxLoadFactorG;        // structural g-limit on commanded lateral accel (typical AAM: 30–50)
        public float MaxTurnRateDegPerSec;  // upper bound on velocity-vector rotation rate

        // -- L3 pseudo-6DOF tier (Phase 4) ------------------------------------
        public float TransverseInertiaKgM2;  // pitch and yaw inertia (axisymmetric body assumption: Iyy = Izz)
        public float RollInertiaKgM2;        // roll inertia (about the body long axis); typically small for finned bodies
        public float LiftSlopePerRad;        // Cl_alpha — lift coefficient per radian of AoA, in the linear regime
        public float StallAoaDeg;            // angle of attack at which lift coefficient peaks; lift collapses past this
        public float WeatherVaneCoefficient; // passive aero restoring moment magnitude per (rad of AoA × dynamic pressure × area)
        public float AutopilotGain;          // gain on the inner-loop rate controller — higher = more aggressive tracking

        // -- L4 full-aero tier ------------------------------------------------
        public AeroModelKind AeroModel;       // Simple = scalar (L3-equivalent); Tabulated = Mach-aware curves authored on the SO
        public float LengthM;                 // reference length for moment arm calcs (typical missile body length)
        public AutopilotKind Autopilot;       // SimpleRate (L3-style direct torque) or SurfaceDeflection (L4 with fins)
        public float MaxControlDeflectionDeg; // max fin deflection (typical ±20°); inner loop saturates at this
        public float MaxControlRateDegPerSec; // max fin deflection rate (typical 200..600 deg/s); integrator rate-limits the actual surface

        // -- Seeker (Phase 3) -------------------------------------------------
        public SeekerKind SeekerKind;       // None = no seeker (use raw truth target); ConeSeeker etc.
        public float SeekerFovDeg;          // full cone angle of the seeker boresight (typical IR AAM: 4..20°)
        public float SeekerMaxRangeM;       // maximum acquisition range
        public float SeekerAcquisitionTimeS; // dwell time required to declare lock once geometric conditions are met
        public float SeekerCoastTimeS;      // track-memory duration after break-lock (0 = drop instantly)
        public bool  SeekerMidcourseDatalink; // fly on truth (command guidance) until the seeker acquires

        // -- Guidance ---------------------------------------------------------
        public GuidanceLawKind GuidanceLaw; // which IGuidanceLaw the entity instantiates at Launch
        public float NavigationGain;        // N for ProNav (3..5 typical); also Pursuit gain

        // -- Lifetime & fuzing ------------------------------------------------
        public float MaxLifetimeS;          // hard upper bound on time-of-flight before self-destruct
        public float FuzeProximityRadiusM;  // simple proximity fuze; <=0 disables proximity
        public float FuzeArmDelayS;         // safe-and-arm delay: fuze is inert for this many seconds after launch

        /// <summary>
        /// Minimal sane profile for tests: 250 m/s cruise, 3 s burn, sensible drag, ProNav.
        /// Real missiles come from authored SOs.
        /// </summary>
        public static MissileProfileData TestStub()
        {
            return new MissileProfileData
            {
                DryMassKg            = 100f,
                PropellantMassKg     = 50f,
                BoostThrustN         = 30000f,
                BoostDurationS       = 3f,
                ThrustModel          = ThrustModelKind.ConstantBoost,
                SustainThrustN       = 0f,
                SustainDurationS     = 0f,
                CruiseSpeedMps       = 250f,
                L0UseGravity         = false,
                DragCoefficient      = 0.3f,
                ReferenceAreaM2      = 0.03f,   // ~0.2 m diameter body
                MaxLoadFactorG       = 30f,
                MaxTurnRateDegPerSec = 60f,
                TransverseInertiaKgM2 = 5f,
                RollInertiaKgM2       = 0.5f,
                LiftSlopePerRad        = 8f,    // ~slender body theory ballpark
                StallAoaDeg            = 18f,
                WeatherVaneCoefficient = 5f,
                AutopilotGain          = 4f,
                AeroModel              = AeroModelKind.Simple,
                LengthM                = 3f,    // ~3m typical AAM body
                Autopilot              = AutopilotKind.SimpleRate,
                MaxControlDeflectionDeg = 20f,
                MaxControlRateDegPerSec = 400f,
                SeekerKind             = SeekerKind.None,
                SeekerFovDeg           = 30f,
                SeekerMaxRangeM        = 5000f,
                SeekerAcquisitionTimeS = 0.25f,
                SeekerCoastTimeS       = 0f,
                SeekerMidcourseDatalink = false,
                GuidanceLaw          = GuidanceLawKind.ProportionalNavigation,
                NavigationGain       = 3f,
                MaxLifetimeS         = 30f,
                FuzeProximityRadiusM = 5f,
                FuzeArmDelayS        = 0.5f,
            };
        }
    }
}
