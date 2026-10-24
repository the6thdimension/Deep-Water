using System.Collections.Generic;
using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.Ballistics
{
    /// <summary>A projectile as the point-mass model sees it (G35).</summary>
    [System.Serializable]
    public struct ProjectileData
    {
        public float MassKg;
        /// <summary>The diameter that flies (a sub-calibre penetrator's, not the cartridge's), m.</summary>
        public float DiameterM;
        public float MuzzleVelocityMps;
        /// <summary>Drag coefficient = form factor × G7(Mach). CALIBRATED per projectile to its published range (G41); 0 flies in a vacuum.</summary>
        public float FormFactor;

        public float AreaM2 => Mathf.PI * 0.25f * DiameterM * DiameterM;
    }

    /// <summary>
    /// A firing table: the trajectories of one projectile at every elevation from the mount's lowest to its highest,
    /// integrated once (RK4, 3-DOF point mass: gravity, ISA density, G7 drag) and kept as samples of horizontal distance
    /// and height. Fire control looks an aim point up in it instead of integrating a shot (as real fire control does),
    /// and the shell flies the table. Tables are cached by projectile and elevation limits, so building many combat
    /// systems pays the integration once.
    /// </summary>
    public sealed class FiringTable
    {
        public const float Gravity = 9.80665f;
        public const float IntegrationStepS = 0.02f;
        public const float SampleStepS = 0.1f;
        const float MaxFlightS = 150f;
        const float FloorM = -100f;

        public readonly ProjectileData Projectile;
        public readonly float ElevationMinDeg, ElevationMaxDeg, ElevationStepDeg;
        /// <summary>Greatest range at the muzzle's height, m, and the elevation that gives it.</summary>
        public readonly float MaxRangeM, MaxRangeElevationDeg;

        // Per elevation: samples of horizontal distance and height every SampleStepS, from the muzzle.
        readonly float[][] _x, _y;

        static readonly Dictionary<(float, float, float, float, float, float, float), FiringTable> s_cache =
            new Dictionary<(float, float, float, float, float, float, float), FiringTable>();

        /// <summary>The table for this projectile and these elevation limits, built once and then shared.</summary>
        public static FiringTable For(in ProjectileData p, float elevationMinDeg, float elevationMaxDeg, float elevationStepDeg = 0.5f)
        {
            var key = (p.MassKg, p.DiameterM, p.MuzzleVelocityMps, p.FormFactor, elevationMinDeg, elevationMaxDeg, elevationStepDeg);
            if (!s_cache.TryGetValue(key, out FiringTable t))
            {
                t = new FiringTable(p, elevationMinDeg, elevationMaxDeg, elevationStepDeg);
                s_cache[key] = t;
            }
            return t;
        }

        FiringTable(in ProjectileData p, float elevationMinDeg, float elevationMaxDeg, float elevationStepDeg)
        {
            Projectile = p;
            ElevationMinDeg = elevationMinDeg;
            ElevationMaxDeg = elevationMaxDeg;
            ElevationStepDeg = elevationStepDeg;
            int n = Mathf.FloorToInt((elevationMaxDeg - elevationMinDeg) / elevationStepDeg + 1e-3f) + 1;
            _x = new float[n][];
            _y = new float[n][];
            var xs = new List<float>(2048);
            var ys = new List<float>(2048);
            for (int k = 0; k < n; k++)
            {
                Fly(p, elevationMinDeg + k * elevationStepDeg, xs, ys);
                _x[k] = xs.ToArray();
                _y[k] = ys.ToArray();
                float r = RangeAtMuzzleHeight(_x[k], _y[k]);
                if (r > MaxRangeM) { MaxRangeM = r; MaxRangeElevationDeg = elevationMinDeg + k * elevationStepDeg; }
            }
        }

        public int Elevations => _x.Length;

        /// <summary>Integrate one shot: samples of (x, y) every <see cref="SampleStepS"/> until it falls below the floor.</summary>
        public static void Fly(in ProjectileData p, float elevationDeg, List<float> xs, List<float> ys)
        {
            xs.Clear();
            ys.Clear();
            float e = elevationDeg * Mathf.Deg2Rad;
            Vector2 pos = Vector2.zero, vel = new Vector2(Mathf.Cos(e), Mathf.Sin(e)) * p.MuzzleVelocityMps;
            float k = p.FormFactor * p.AreaM2 / (2f * Mathf.Max(1e-6f, p.MassKg));
            int perSample = Mathf.RoundToInt(SampleStepS / IntegrationStepS);
            int steps = Mathf.CeilToInt(MaxFlightS / IntegrationStepS);
            float h = IntegrationStepS;
            xs.Add(0f);
            ys.Add(0f);
            for (int s = 1; s <= steps; s++)
            {
                // RK4 on (position, velocity).
                Vector2 a1 = Accel(pos, vel, k);
                Vector2 v2 = vel + a1 * (h * 0.5f);
                Vector2 a2 = Accel(pos + vel * (h * 0.5f), v2, k);
                Vector2 v3 = vel + a2 * (h * 0.5f);
                Vector2 a3 = Accel(pos + v2 * (h * 0.5f), v3, k);
                Vector2 v4 = vel + a3 * h;
                Vector2 a4 = Accel(pos + v3 * h, v4, k);
                pos += (vel + 2f * v2 + 2f * v3 + v4) * (h / 6f);
                vel += (a1 + 2f * a2 + 2f * a3 + a4) * (h / 6f);
                if (s % perSample == 0)
                {
                    xs.Add(pos.x);
                    ys.Add(pos.y);
                }
                if (pos.y < FloorM) break;
            }
        }

        static Vector2 Accel(Vector2 pos, Vector2 vel, float k)
        {
            float speed = vel.magnitude;
            Atmosphere.At(pos.y, out float rho, out float sound);
            float drag = k * rho * DragCurve.G7(speed / sound) * speed;   // |a_drag| / |v|
            return new Vector2(-drag * vel.x, -Gravity - drag * vel.y);
        }

        static float RangeAtMuzzleHeight(float[] x, float[] y)
        {
            for (int i = 1; i < y.Length; i++)
            {
                if (y[i] < 0f && y[i - 1] >= 0f && i > 1)
                {
                    float f = y[i - 1] / (y[i - 1] - y[i]);
                    return x[i - 1] + (x[i] - x[i - 1]) * f;
                }
            }
            return 0f;
        }

        /// <summary>Height (m, relative to the muzzle) and time (s) when the shot at elevation index <paramref name="k"/>
        /// has flown <paramref name="x"/> m horizontally; false if it never gets that far.</summary>
        public bool HeightAt(int k, float x, out float y, out float t)
        {
            float[] xs = _x[k], ys = _y[k];
            y = t = 0f;
            if (x < 0f || xs.Length < 2 || x > xs[xs.Length - 1]) return false;
            // Horizontal distance only grows along a shot (no wind), so a binary search finds the sample pair.
            int lo = 0, hi = xs.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (xs[mid] <= x) lo = mid; else hi = mid;
            }
            float span = xs[hi] - xs[lo];
            float f = span > 1e-6f ? (x - xs[lo]) / span : 0f;
            y = ys[lo] + (ys[hi] - ys[lo]) * f;
            t = (lo + f) * SampleStepS;
            return true;
        }

        /// <summary>
        /// Fire control's question (G38): the lowest elevation whose shot passes through the point <paramref name="x"/> m
        /// out and <paramref name="y"/> m up from the muzzle, and the time of flight to it. False when it is out of reach.
        /// </summary>
        public bool TrySolve(float x, float y, out float elevationDeg, out float timeOfFlightS)
        {
            elevationDeg = timeOfFlightS = 0f;
            bool havePrev = false;
            float prevY = 0f, prevT = 0f;
            for (int k = 0; k < _x.Length; k++)
            {
                if (!HeightAt(k, x, out float yk, out float tk)) { havePrev = false; continue; }
                if (yk >= y)
                {
                    if (!havePrev)
                    {
                        if (k > 0 || yk - y > 1f) return false;   // the lowest elevation already passes over it
                        elevationDeg = ElevationMinDeg;
                        timeOfFlightS = tk;
                        return true;
                    }
                    float f = (y - prevY) / Mathf.Max(1e-6f, yk - prevY);
                    elevationDeg = ElevationMinDeg + (k - 1 + f) * ElevationStepDeg;
                    timeOfFlightS = prevT + (tk - prevT) * f;
                    return true;
                }
                havePrev = true;
                prevY = yk;
                prevT = tk;
            }
            return false;
        }

        /// <summary>The highest point of the shot at <paramref name="elevationDeg"/> (the nearest tabulated elevation), m.</summary>
        public float ApexM(float elevationDeg)
        {
            int k = Mathf.Clamp(Mathf.RoundToInt((elevationDeg - ElevationMinDeg) / ElevationStepDeg), 0, _y.Length - 1);
            float top = 0f;
            foreach (float v in _y[k]) top = Mathf.Max(top, v);
            return top;
        }

        /// <summary>Range at the muzzle's height for the shot at <paramref name="elevationDeg"/> (the nearest tabulated elevation), m.</summary>
        public float RangeAt(float elevationDeg)
        {
            int k = Mathf.Clamp(Mathf.RoundToInt((elevationDeg - ElevationMinDeg) / ElevationStepDeg), 0, _y.Length - 1);
            return RangeAtMuzzleHeight(_x[k], _y[k]);
        }

        /// <summary>
        /// Calibration (G41): the form factor that makes the shot at <paramref name="elevationDeg"/> reach
        /// <paramref name="rangeM"/> (or, with <paramref name="elevationDeg"/> NaN, makes the greatest range over
        /// <paramref name="elevationMaxDeg"/> reach it). Bisection; used by the loadout builder and its tests.
        /// </summary>
        public static float CalibrateFormFactor(ProjectileData p, float rangeM, float elevationDeg, float elevationMaxDeg = 85f)
        {
            float lo = 0.05f, hi = 5f;
            var xs = new List<float>(2048);
            var ys = new List<float>(2048);
            for (int it = 0; it < 40; it++)
            {
                p.FormFactor = 0.5f * (lo + hi);
                float r = 0f;
                if (float.IsNaN(elevationDeg))
                {
                    for (float e = 20f; e <= elevationMaxDeg; e += 0.5f)
                    {
                        Fly(p, e, xs, ys);
                        r = Mathf.Max(r, RangeAtMuzzleHeight(xs.ToArray(), ys.ToArray()));
                    }
                }
                else
                {
                    Fly(p, elevationDeg, xs, ys);
                    r = RangeAtMuzzleHeight(xs.ToArray(), ys.ToArray());
                }
                if (r > rangeM) lo = p.FormFactor; else hi = p.FormFactor;
            }
            return 0.5f * (lo + hi);
        }
    }
}
