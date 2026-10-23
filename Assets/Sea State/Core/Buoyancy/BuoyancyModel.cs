using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>One buoyancy probe in body space: a vertical water column element.</summary>
    public struct BuoyancyProbe
    {
        /// <summary>Probe centre in the body's local frame, m.</summary>
        public float3 LocalPosition;
        /// <summary>Horizontal cross-section the probe stands for, m².</summary>
        public float AreaM2;
        /// <summary>Vertical extent of the probe column, m (its volume is AreaM2 × HeightM).</summary>
        public float HeightM;
    }

    /// <summary>Hydrodynamic coefficients applied per probe.</summary>
    public struct BuoyancyDamping
    {
        /// <summary>Linear damping, m/s: F = −ρ A f c v_rel (f = submerged fraction).</summary>
        public float LinearMs;
        /// <summary>Quadratic drag coefficient Cd: F = −½ ρ A f Cd |v_rel| v_rel.</summary>
        public float QuadraticCd;
    }

    /// <summary>
    /// Per-probe buoyancy and drag — pure functions, no Unity types (P6, testable).
    ///
    /// **Model and its limits.** Each probe is a vertical column; buoyancy is Archimedes on
    /// its submerged height (ρ g A h_sub), vertical. Drag acts on the velocity of the hull
    /// point relative to the water particle (so orbital motion pushes the hull). This is a
    /// probe model: no radiation or diffraction, no added mass, no strip theory — it gives
    /// believable heave/pitch/roll for any hull, not RAO-accurate seakeeping.
    /// </summary>
    public static class BuoyancyModel
    {
        /// <summary>Submerged height of a column centred at <paramref name="probeCentreY"/>, clamped to [0, height].</summary>
        public static float SubmergedHeight(float surfaceY, float probeCentreY, float heightM)
            => math.clamp(surfaceY - (probeCentreY - 0.5f * heightM), 0f, heightM);

        /// <summary>
        /// Total hydrodynamic force on one probe (world frame, up = +Y).
        /// </summary>
        public static float3 ProbeForce(in BuoyancyProbe probe, in BuoyancyDamping damping,
            float waterDensity, float gravity, float surfaceY, float3 probeWorld,
            float3 pointVelocity, float3 waterVelocity)
        {
            float submerged = SubmergedHeight(surfaceY, probeWorld.y, probe.HeightM);
            if (submerged <= 0f) return float3.zero;

            float fraction = probe.HeightM > 0f ? submerged / probe.HeightM : 0f;
            float3 buoyancy = new float3(0f, waterDensity * gravity * probe.AreaM2 * submerged, 0f);

            float3 vRel = pointVelocity - waterVelocity;
            float speed = math.length(vRel);
            float3 drag = -waterDensity * probe.AreaM2 * fraction *
                          (damping.LinearMs + 0.5f * damping.QuadraticCd * speed) * vRel;

            return buoyancy + drag;
        }
    }
}
