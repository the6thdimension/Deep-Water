using GuidedFury.Core.Guidance;
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

        // -- Propulsion (boost-only motor model for Phase 2) ------------------
        public float BoostThrustN;       // newtons, constant during boost
        public float BoostDurationS;     // seconds, motor burns then quits

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

        // -- Seeker (Phase 3) -------------------------------------------------
        public SeekerKind SeekerKind;       // None = no seeker (use raw truth target); ConeSeeker etc.
        public float SeekerFovDeg;          // full cone angle of the seeker boresight (typical IR AAM: 4..20°)
        public float SeekerMaxRangeM;       // maximum acquisition range
        public float SeekerAcquisitionTimeS; // dwell time required to declare lock once geometric conditions are met

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
                SeekerKind             = SeekerKind.None,
                SeekerFovDeg           = 30f,
                SeekerMaxRangeM        = 5000f,
                SeekerAcquisitionTimeS = 0.25f,
                GuidanceLaw          = GuidanceLawKind.ProportionalNavigation,
                NavigationGain       = 3f,
                MaxLifetimeS         = 30f,
                FuzeProximityRadiusM = 5f,
                FuzeArmDelayS        = 0.5f,
            };
        }
    }
}
