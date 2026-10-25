using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.DamageControl
{
    /// <summary>The damaged ship's metacentric height and the terms it is made of (the math is the message).</summary>
    public struct DamagedStability
    {
        /// <summary>GM = <see cref="KbM"/> + <see cref="BmM"/> − <see cref="KgM"/> − <see cref="FreeSurfaceM"/>, m.</summary>
        public float GmM;
        /// <summary>Heights above the keel, m: centre of buoyancy, centre of gravity.</summary>
        public float KbM, KgM;
        /// <summary>Transverse metacentric radius of the intact waterplane, m.</summary>
        public float BmM;
        /// <summary>The free-surface correction of the part-filled spaces, m.</summary>
        public float FreeSurfaceM;
        /// <summary>The intact volume she displaces, m³, and the weight it carries, kg.</summary>
        public float DisplacedM3, WeightKg;
    }

    /// <summary>
    /// The damaged ship's static stability, the way a damage-stability book reckons it, from the hull's own columns:
    /// <list type="bullet">
    /// <item>a space open to the sea is LOST BUOYANCY: its volume below the waterline and its waterplane no longer count;</item>
    /// <item>water in every other space is ADDED WEIGHT, with the free-surface correction of the part-filled ones.</item>
    /// </list>
    /// Heel is taken out (the waterline keeps her sinkage and trim), so GM is the slope of her righting arm at the angle
    /// she lies at. <c>.claude/skills/hull-stability-check/scripts/flood_predict.py</c> is its closed-form mirror.
    /// </summary>
    public static class FloodHydrostatics
    {
        public static DamagedStability Evaluate(FloodPlan plan, FloodModel water, bool[] openToSea, float originY, float pitchDeg)
        {
            FloodingProfileData p = plan.Profile;
            float pitch = pitchDeg * Mathf.Deg2Rad;
            float vy = Mathf.Cos(pitch), vz = Mathf.Sin(pitch);

            // The intact hull's displacement and waterplane (sea level at world y = 0, as the truth feed has it).
            float volume = 0f, momentY = 0f, wpArea = 0f, wpX = 0f, wpXX = 0f;
            foreach (HullColumn c in p.Columns)
            {
                float height = c.TopY - c.BottomY;
                float centre = 0.5f * (c.BottomY + c.TopY);
                float bottom = originY + vy * centre + vz * c.Z - 0.5f * height;
                float h = Mathf.Clamp(-bottom, 0f, height);
                if (h <= 0f) continue;
                volume += c.AreaM2 * h;
                momentY += c.AreaM2 * h * (c.BottomY + 0.5f * h);
                if (h < height) { wpArea += c.AreaM2; wpX += c.AreaM2 * c.X; wpXX += c.AreaM2 * c.X * c.X; }
            }

            // Lost buoyancy: what the spaces open to the sea take out of it.
            float weight = p.ShipMassKg, weightY = p.ShipMassKg * p.ShipCentreOfMass.y, freeSurface = 0f;
            for (int s = 0; s < plan.Spaces.Length; s++)
            {
                FloodSpace space = plan.Spaces[s];
                if (!openToSea[s])
                {
                    float kg = p.SeawaterDensityKgM3 * water.Volume(s);
                    weight += kg;
                    weightY += kg * water.CentroidShip(s).y;
                    freeSurface += p.SeawaterDensityKgM3 * water.FreeSurfaceInertia(s);
                    continue;
                }
                for (int i = space.FirstSegment; i < space.FirstSegment + space.SegmentCount; i++)
                {
                    FloodSegment g = plan.Segments[i];
                    float bottom = originY + vy * g.ColumnCentreY + vz * g.Z + (g.Y0 - g.ColumnCentreY);
                    float height = g.Y1 - g.Y0;
                    float h = Mathf.Clamp(-bottom, 0f, height);
                    if (h <= 0f) continue;
                    float lost = space.Permeability * g.AreaM2;
                    volume -= lost * h;
                    momentY -= lost * h * (g.Y0 + 0.5f * h);
                    if (h < height) { wpArea -= lost; wpX -= lost * g.X; wpXX -= lost * g.X * g.X; }
                }
            }

            var r = new DamagedStability { DisplacedM3 = volume, WeightKg = weight };
            if (volume <= 0f || wpArea <= 0f) { r.GmM = float.NaN; return r; }
            float keel = p.KeelY;
            float transverse = wpXX - wpX * wpX / wpArea;   // about the waterplane's own centroid
            r.KbM = momentY / volume - keel;
            r.BmM = transverse / volume;
            r.KgM = weightY / weight - keel;
            r.FreeSurfaceM = freeSurface / weight;
            r.GmM = r.KbM + r.BmM - r.KgM - r.FreeSurfaceM;
            return r;
        }
    }
}
