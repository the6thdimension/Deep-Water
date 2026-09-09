using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Aero
{
    /// <summary>
    /// Scalar aero model — L3-equivalent. Reads the profile's scalar `DragCoefficient` and
    /// `LiftSlopePerRad` directly; no Mach dependence. Used as the L4 fallback when no
    /// tabulated curves are authored, and as the reference behavior for tests.
    ///
    /// CmAlpha and CmDelta default to derived values from the profile's WeatherVane /
    /// AutopilotGain so that L4 with the Simple aero model behaves similarly to L3 — body
    /// stiffness comes from `-WeatherVane`-equivalent pitch-stable moment, control authority
    /// from a placeholder constant. Authors who want real flight dynamics should switch to
    /// <see cref="TabulatedAeroModel"/>.
    /// </summary>
    public sealed class SimpleAeroModel : IAeroModel
    {
        public static readonly SimpleAeroModel Instance = new SimpleAeroModel();
        private SimpleAeroModel() { }

        public AeroModelKind Kind => AeroModelKind.Simple;

        public AeroSample Sample(in MissileState state, in AtmosphereSample atmo, in MissileProfileData profile)
        {
            // L3 has a positive WeatherVaneCoefficient that drives AoA toward zero. In
            // coefficient-table terms that maps to a NEGATIVE Cm_α (stable airframe). We
            // map them: Cm_α = -WeatherVane / ReferenceArea (rough, but produces
            // L3-equivalent dynamics through L4's coefficient-based moment calc).
            // The full conversion would account for moment arm and dyn-pressure division
            // factors, but for a "drop-in L3 substitute" this is close enough.
            float weatherVane = profile.WeatherVaneCoefficient;
            float refArea = profile.ReferenceAreaM2 > 1e-6f ? profile.ReferenceAreaM2 : 0.01f;

            return new AeroSample
            {
                Cd             = profile.DragCoefficient,
                ClAlphaPerRad  = profile.LiftSlopePerRad,
                CmAlphaPerRad  = -weatherVane / refArea,
                // Control authority placeholder. Phase 2 (when surfaces land) uses this only
                // if profile.AeroModel == Simple; tabulated authors will override.
                CmDeltaPerRad  = -2f, // typical control surface slope; negative = nose-down per positive δ
            };
        }
    }
}
