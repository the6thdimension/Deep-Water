using System;

namespace DeepWater.AirWing.Core.Profiles
{
    /// <summary>The airframe's numbers, SI (W22). Baked from an <c>AircraftProfileSO</c>, which carries each number's source.</summary>
    public struct AirframeProfile
    {
        public float WingAreaM2;
        public float SpanM;
        public float EmptyMassKg;
        public float InternalFuelKg;
        /// <summary>Zero-lift drag coefficient.</summary>
        public float CD0;
        /// <summary>Span efficiency in the induced-drag term K = 1 / (π e AR).</summary>
        public float OswaldEfficiency;
        public float CLmax;
        /// <summary>Largest negative lift coefficient, as a positive number.</summary>
        public float CLmaxNegative;
        /// <summary>Lift-curve slope, per radian (gives the angle of attack the HUD shows at L0).</summary>
        public float CLalphaPerRad;
        public float FlapsDeltaCLmax;
        public float FlapsDeltaCD;
        public float SpeedBrakeDeltaCD;

        public float AspectRatio => SpanM * SpanM / WingAreaM2;
        public float InducedDragFactor => 1f / ((float)Math.PI * OswaldEfficiency * AspectRatio);

        public void Validate()
        {
            Positive(WingAreaM2, nameof(WingAreaM2));
            Positive(SpanM, nameof(SpanM));
            Positive(EmptyMassKg, nameof(EmptyMassKg));
            NonNegative(InternalFuelKg, nameof(InternalFuelKg));
            NonNegative(CD0, nameof(CD0));
            Positive(OswaldEfficiency, nameof(OswaldEfficiency));
            Positive(CLmax, nameof(CLmax));
            NonNegative(CLmaxNegative, nameof(CLmaxNegative));
            Positive(CLalphaPerRad, nameof(CLalphaPerRad));
        }

        internal static void Positive(float v, string name)
        {
            if (!(v > 0f) || float.IsInfinity(v)) throw new ArgumentOutOfRangeException(name, v, "must be positive and finite");
        }

        internal static void NonNegative(float v, string name)
        {
            if (!(v >= 0f) || float.IsInfinity(v)) throw new ArgumentOutOfRangeException(name, v, "must be zero or positive and finite");
        }
    }

    /// <summary>The engines' numbers, SI.</summary>
    public struct PowerplantProfile
    {
        public int EngineCount;
        /// <summary>Static sea-level thrust per engine at military power (no afterburner), newtons.</summary>
        public float MilitaryThrustPerEngineN;
        /// <summary>Static sea-level thrust per engine at maximum afterburner, newtons.</summary>
        public float MaxThrustPerEngineN;
        /// <summary>Idle thrust as a fraction of military thrust.</summary>
        public float IdleThrustFraction;
        /// <summary>Thrust falls with density as σ^x, σ = ρ/ρ₀ (L0 has no ram or Mach effect).</summary>
        public float ThrustLapseExponent;
        /// <summary>
        /// Powerplant L1's spool (AUTHORED): the core spool follows the throttle with these time constants, seconds;
        /// the afterburner lights only above <see cref="AfterburnerMinSpool"/> and fades in and out with its own.
        /// All zero: no L1 data (L1 then falls back to L0 and says so).
        /// </summary>
        public float SpoolUpTauS, SpoolDownTauS, AfterburnerLightTauS, AfterburnerOffTauS, AfterburnerMinSpool;

        public bool HasSpool => SpoolUpTauS > 0f && SpoolDownTauS > 0f && AfterburnerLightTauS > 0f && AfterburnerOffTauS > 0f;

        public void Validate()
        {
            if (EngineCount < 1) throw new ArgumentOutOfRangeException(nameof(EngineCount), EngineCount, "at least one engine");
            AirframeProfile.NonNegative(MilitaryThrustPerEngineN, nameof(MilitaryThrustPerEngineN));
            AirframeProfile.NonNegative(MaxThrustPerEngineN, nameof(MaxThrustPerEngineN));
            if (MaxThrustPerEngineN < MilitaryThrustPerEngineN)
                throw new ArgumentOutOfRangeException(nameof(MaxThrustPerEngineN), MaxThrustPerEngineN, "afterburner thrust below military thrust");
            if (!(IdleThrustFraction >= 0f && IdleThrustFraction <= 1f))
                throw new ArgumentOutOfRangeException(nameof(IdleThrustFraction), IdleThrustFraction, "must be 0 … 1");
            AirframeProfile.NonNegative(ThrustLapseExponent, nameof(ThrustLapseExponent));
            if (HasSpool && !(AfterburnerMinSpool >= 0f && AfterburnerMinSpool <= 1f))
                throw new ArgumentOutOfRangeException(nameof(AfterburnerMinSpool), AfterburnerMinSpool, "must be 0 … 1");
        }
    }

    /// <summary>The flight-control law's limits and response, SI.</summary>
    public struct ControlsProfile
    {
        /// <summary>Positive load-factor limit, g (full aft stick).</summary>
        public float LoadFactorMax;
        /// <summary>Negative load-factor limit, g, as a negative number (full forward stick).</summary>
        public float LoadFactorMin;
        /// <summary>Roll rate at full stick, radians per second.</summary>
        public float RollRateMaxRadS;
        /// <summary>First-order time constant of the load-factor response, seconds.</summary>
        public float LoadFactorTimeConstantS;
        /// <summary>First-order time constant of the roll-rate response, seconds.</summary>
        public float RollTimeConstantS;

        public void Validate()
        {
            if (!(LoadFactorMax > 1f)) throw new ArgumentOutOfRangeException(nameof(LoadFactorMax), LoadFactorMax, "must exceed 1 g");
            if (!(LoadFactorMin < 1f)) throw new ArgumentOutOfRangeException(nameof(LoadFactorMin), LoadFactorMin, "must be below 1 g");
            AirframeProfile.Positive(RollRateMaxRadS, nameof(RollRateMaxRadS));
            AirframeProfile.Positive(LoadFactorTimeConstantS, nameof(LoadFactorTimeConstantS));
            AirframeProfile.Positive(RollTimeConstantS, nameof(RollTimeConstantS));
        }
    }

    /// <summary>One aircraft type's baked profile (C7): every part's numbers.</summary>
    public struct AircraftProfile
    {
        public string Name;
        public AirframeProfile Airframe;
        public PowerplantProfile Powerplant;
        public ControlsProfile Controls;
        /// <summary>The L1 airframe's aero model and inertia (W32); unset for a type with no L1 data yet.</summary>
        public AirframeL1Profile AirframeL1;
        /// <summary>The L1 fly-by-wire's gains and actuators (W23); unset for a type with no L1 law yet.</summary>
        public ControlsL1Profile ControlsL1;
        /// <summary>Landing Gear L1's wheels and limits; unset for a type with no gear data.</summary>
        public LandingGearProfile LandingGear;
        /// <summary>The gun (Gunnery K4); unset for an unarmed type.</summary>
        public GunMount Gun;
        /// <summary>Hit points and hit size (Gunnery K5); unset: rounds pass through it.</summary>
        public CombatProfile Combat;
        /// <summary>Game missiles on pylons (W65); unset for a type that carries none.</summary>
        public DeepWater.AirWing.Core.Missiles.MissileMount Missiles;

        /// <summary>Airframe L1 and Flight Controls L1 come as a pair: one without the other cannot fly.</summary>
        public bool HasL1 => AirframeL1.IsSet && ControlsL1.IsSet;

        public void Validate()
        {
            Airframe.Validate();
            Powerplant.Validate();
            Controls.Validate();
            if (AirframeL1.IsSet) AirframeL1.Validate();
            if (ControlsL1.IsSet) ControlsL1.Validate();
            if (LandingGear.IsSet) LandingGear.Validate();
            if (Gun.IsSet) Gun.Validate();
            if (Combat.IsSet) Combat.Validate();
            if (Missiles.IsSet) Missiles.Validate();
        }
    }
}
