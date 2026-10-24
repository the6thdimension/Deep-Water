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
        /// <summary>
        /// When set, drag sees the relative velocity in the hull's frame scaled per axis by
        /// <see cref="AxisScale"/>. Off (the default) is isotropic drag, the original behaviour.
        /// </summary>
        public bool PerAxis;
        /// <summary>Drag scale per hull axis: x sway, y heave, z surge. Used only with <see cref="PerAxis"/>.</summary>
        public float3 AxisScale;
    }

    /// <summary>
    /// Per-probe buoyancy and drag — pure functions, no Unity types (P6, testable).
    ///
    /// **Model and its limits.** Each probe is a vertical column; buoyancy is Archimedes on
    /// its submerged height (ρ g A h_sub), vertical, acting at the centroid of the submerged
    /// part of the column (<see cref="SubmergedCentroid"/>). Drag acts on the velocity of that
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
        /// Where a column's buoyancy acts: the centroid of its submerged part, half the
        /// submerged height up from the column's lower end, along the column's axis
        /// (<paramref name="columnAxis"/>, the body's up). The wet part starts at whichever end
        /// is lower in the world, so past 90° of heel (upside down) the axis is flipped.
        ///
        /// Why not the column centre: for a vertical force the point only matters once the hull
        /// heels, and then it carries the whole "centre of buoyancy sits below the centre of
        /// gravity" term of the stability height GM = KB + BM − KG. Acting at the column centre
        /// makes KB the column's mid-height, so a box with its centre of mass there gets GM = BM:
        /// the 154 m demo box (GM 2.44 m, BM 5.29 m) rolled √(5.29 / 2.44) ≈ 1.5× too fast.
        ///
        /// Limit: the probe model assumes columns nearer vertical than horizontal. Exactly on its
        /// side (axis horizontal) the point is the column centre, and it jumps to the other end
        /// as the hull passes 90°.
        /// </summary>
        public static float3 SubmergedCentroid(float3 probeWorld, float3 columnAxis, float submergedHeight, float heightM)
        {
            float3 upward = columnAxis * math.sign(columnAxis.y);
            return probeWorld + upward * (0.5f * submergedHeight - 0.5f * heightM);
        }

        /// <summary>
        /// Total hydrodynamic force on one probe (world frame, up = +Y). <paramref name="bodyRotation"/>
        /// is the hull's orientation; only per-axis drag uses it.
        ///
        /// Why per-axis drag exists: probe drag is a crude damping, sized for heave and roll. Applied
        /// to surge and sway it is enormous: on the DDG-51 hull at 30 kn it is 85 MN against 3.4 MN of
        /// thrust. A ship with its own manoeuvring model (resistance, sway and yaw forces) keeps the
        /// probes' heave damping and sets surge and sway to 0.
        /// </summary>
        public static float3 ProbeForce(in BuoyancyProbe probe, in BuoyancyDamping damping,
            float waterDensity, float gravity, float surfaceY, float3 probeWorld,
            float3 pointVelocity, float3 waterVelocity, quaternion bodyRotation)
        {
            float submerged = SubmergedHeight(surfaceY, probeWorld.y, probe.HeightM);
            if (submerged <= 0f) return float3.zero;

            float fraction = probe.HeightM > 0f ? submerged / probe.HeightM : 0f;
            float3 buoyancy = new float3(0f, waterDensity * gravity * probe.AreaM2 * submerged, 0f);

            float3 vRel = pointVelocity - waterVelocity;
            if (damping.PerAxis)
            {
                float3 hull = math.mul(math.conjugate(bodyRotation), vRel) * damping.AxisScale;
                vRel = math.mul(bodyRotation, hull);
            }
            float speed = math.length(vRel);
            float3 drag = -waterDensity * probe.AreaM2 * fraction *
                          (damping.LinearMs + 0.5f * damping.QuadraticCd * speed) * vRel;

            return buoyancy + drag;
        }
    }
}
