using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.DamageControl
{
    /// <summary>
    /// The water in the ship (G55, G56). Each step:
    /// 1. every wet space finds its free surface, a level that stays horizontal in the world, so the water runs to the
    ///    low side as she heels (the free-surface effect);
    /// 2. a full (pressed-up) space takes the pressure head that balances what flows through its openings;
    /// 3. each opening passes Q = Cd·A·√(2g·Δh) on the difference of the heads either side of it, either way;
    /// 4. the volumes move, and the water's mass and centroid are summed for the hull (the Rigidbody, G55).
    ///
    /// Heads are world heights of the water's surface (or its pressure, when full). The sea's head at an opening is the
    /// sea surface there, which the host samples into <see cref="SetSeaSurface"/> (0, sea level, when it does not).
    /// Pure C#: same pose sequence, same sea, same water, every run. Dry spaces cost nothing: only wet spaces are placed
    /// at the pose and solved, once a step, after the water has moved. The flows read the levels solved at the end of
    /// the last step, so they lag the pose by one step (20 ms, against an 11 s roll), as the hull lags the water.
    ///
    /// Limits at L1: water moves instantly to its level inside a space (no sloshing); an opening is a point at its
    /// centre (a large hole half out of the water passes too much or too little); a pressed-up space is solved one at a
    /// time against its neighbours, a few sweeps a step.
    /// </summary>
    public sealed class FloodModel
    {
        const int PressureSweeps = 3;
        const int BisectionSteps = 40;
        const int NewtonSteps = 12;
        const float LevelToleranceM = 1e-5f;
        const float HeadToleranceM = 1e-4f;
        const float FullFraction = 0.9999f;

        readonly FloodPlan _plan;
        readonly FloodingProfileData _p;
        readonly float[] _volume, _level, _freeSurface, _net, _topW;
        readonly bool[] _full, _moved;
        readonly int[] _placedStep;
        readonly Vector3[] _centroid;
        readonly float[] _bottomW;
        readonly List<int>[] _openingsOf;
        readonly List<FloodOpening> _openings = new List<FloodOpening>();
        readonly List<float> _flow = new List<float>();
        readonly List<float> _seaY = new List<float>();

        Vector3 _vertical = Vector3.up;
        float _originY;
        int _step;

        public FloodModel(FloodPlan plan)
        {
            _plan = plan;
            _p = plan.Profile;
            int n = plan.Spaces.Length;
            _volume = new float[n];
            _level = new float[n];
            _freeSurface = new float[n];
            _net = new float[n];
            _topW = new float[n];
            _full = new bool[n];
            _moved = new bool[n];
            _placedStep = new int[n];
            _centroid = new Vector3[n];
            _bottomW = new float[plan.Segments.Length];
            _openingsOf = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                _level[i] = float.NegativeInfinity;
                _placedStep[i] = -1;
                _centroid[i] = plan.Spaces[i].FullCentroidShip;
                _openingsOf[i] = new List<int>();
            }
            foreach (FloodOpening o in plan.Openings) AddOpening(o);
        }

        public FloodPlan Plan => _plan;
        public int OpeningCount => _openings.Count;
        public FloodOpening Opening(int i) => _openings[i];
        /// <summary>Last step's flow through opening <paramref name="i"/>, m³/s, positive into its SpaceA.</summary>
        public float Flow(int i) => _flow[i];
        public float Volume(int space) => _volume[space];
        public bool IsFull(int space) => _full[space];
        /// <summary>The space's water surface (or its pressure head when full), world y; −∞ when dry.</summary>
        public float Head(int space) => _level[space];
        public float FreeSurfaceAreaM2(int space) => _freeSurface[space];
        public Vector3 CentroidShip(int space) => _centroid[space];

        public float WaterMassKg { get; private set; }
        public Vector3 WaterCentroidShip { get; private set; }
        /// <summary>From the sea into the ship last step, m³/s (negative when she is draining).</summary>
        public float InflowM3s { get; private set; }

        /// <summary>The world y of the sea surface at opening <paramref name="i"/> (only openings to the sea read it).</summary>
        public void SetSeaSurface(int i, float worldY) => _seaY[i] = worldY;

        public int AddOpening(in FloodOpening o)
        {
            int i = _openings.Count;
            _openings.Add(o);
            _flow.Add(0f);
            _seaY.Add(0f);
            _openingsOf[o.SpaceA].Add(i);
            if (!o.ToSea) _openingsOf[o.SpaceB].Add(i);
            return i;
        }

        /// <summary>
        /// A part-filled space's free surface: permeability × its second moment about its own fore-and-aft centroid axis,
        /// m⁴. What the free-surface correction ρ·i/Δ is made of; 0 for a dry or full space.
        /// </summary>
        public float FreeSurfaceInertia(int s)
        {
            if (_volume[s] <= 0f || _full[s]) return 0f;
            Place(s);
            FloodSpace space = _plan.Spaces[s];
            float a = 0f, ax = 0f, axx = 0f;
            for (int i = space.FirstSegment; i < space.FirstSegment + space.SegmentCount; i++)
            {
                FloodSegment g = _plan.Segments[i];
                float h = _level[s] - _bottomW[i];
                if (h <= 0f || h >= g.Y1 - g.Y0) continue;
                a += g.AreaM2;
                ax += g.AreaM2 * g.X;
                axx += g.AreaM2 * g.X * g.X;
            }
            return a > 0f ? space.Permeability * (axx - ax * ax / a) : 0f;
        }

        /// <summary>World y of a ship-frame point at the pose of the last <see cref="Step"/>.</summary>
        public float WorldY(Vector3 pointShip) => _originY + Vector3.Dot(_vertical, pointShip);

        /// <summary>Put water in a space directly (tests, and a space loaded with water at the start of a scenario).</summary>
        public void SetVolume(int space, float m3)
        {
            _volume[space] = Mathf.Clamp(m3, 0f, _plan.Spaces[space].CapacityM3);
            _full[space] = _volume[space] >= FullFraction * _plan.Spaces[space].CapacityM3 && _volume[space] > 0f;
            _moved[space] = true;
        }

        public void Step(float dt, in OwnShipState own)
        {
            _step++;
            _vertical = own.VerticalAxes;
            _originY = own.Position.y;
            float g = _p.Gravity, cd = _p.DischargeCoefficient;

            // 1. Levels: last step's, except where the water was set from outside since; full spaces' tops at this pose.
            for (int s = 0; s < _volume.Length; s++)
            {
                if (_moved[s] || (_volume[s] > 0f && float.IsInfinity(_level[s]))) Settle(s);
                else if (_full[s]) Place(s);
            }
            for (int sweep = 0; sweep < PressureSweeps; sweep++)
                for (int s = 0; s < _volume.Length; s++)
                    if (_full[s]) _level[s] = BalancedHead(s);

            // 2. Flows on the heads, limited so no step overshoots the level that would equalise them.
            for (int s = 0; s < _net.Length; s++) _net[s] = 0f;
            float inflow = 0f;
            for (int i = 0; i < _openings.Count; i++)
            {
                FloodOpening o = _openings[i];
                float y = WorldY(o.PointShip);
                float hA = HeadAt(o.SpaceA, y);
                float hB = o.ToSea ? Mathf.Max(0f, _seaY[i] - y) : HeadAt(o.SpaceB, y);
                float dh = hB - hA;
                float q = 0f;
                if (dh != 0f)
                {
                    q = cd * o.AreaM2 * Mathf.Sign(dh) * Mathf.Sqrt(2f * g * Mathf.Abs(dh));
                    float equalise = Equalising(o, Mathf.Abs(dh));
                    if (Mathf.Abs(q) * dt > equalise) q = Mathf.Sign(q) * equalise / dt;
                }
                _flow[i] = q;
                _net[o.SpaceA] += q;
                if (o.ToSea) inflow += q;
                else _net[o.SpaceB] -= q;
            }
            InflowM3s = inflow;

            // 3. Volumes. A full space keeps its water unless more leaves than arrives. A space that fills mid-step keeps
            //    only what fits: the rest of that step's inflow is not carried over (at most one step's flow, once).
            for (int s = 0; s < _volume.Length; s++)
            {
                if (_net[s] == 0f || (_full[s] && _net[s] > 0f)) continue;
                SetVolume(s, _volume[s] + _net[s] * dt);
            }

            // 4. Where the water is now, at this pose, and what it weighs.
            float mass = 0f;
            Vector3 moment = Vector3.zero;
            for (int s = 0; s < _volume.Length; s++)
            {
                if (_volume[s] > 0f || _moved[s]) Settle(s);
                _moved[s] = false;
                if (_volume[s] <= 0f) continue;
                float kg = _p.SeawaterDensityKgM3 * _volume[s];
                mass += kg;
                moment += kg * _centroid[s];
            }
            WaterMassKg = mass;
            WaterCentroidShip = mass > 0f ? moment / mass : Vector3.zero;
        }

        /// <summary>
        /// A space's segments' world bottoms at this step's pose (once per step), reckoned as Sea State reckons its
        /// probe's: the column centre's world height plus the segment's offset from it, measured vertically. And its top.
        /// </summary>
        void Place(int s)
        {
            if (_placedStep[s] == _step) return;
            _placedStep[s] = _step;
            FloodSpace space = _plan.Spaces[s];
            float top = float.MinValue;
            for (int i = space.FirstSegment; i < space.FirstSegment + space.SegmentCount; i++)
            {
                FloodSegment g = _plan.Segments[i];
                float b = _originY + _vertical.x * g.X + _vertical.y * g.ColumnCentreY + _vertical.z * g.Z + (g.Y0 - g.ColumnCentreY);
                _bottomW[i] = b;
                top = Mathf.Max(top, b + (g.Y1 - g.Y0));
            }
            _topW[s] = top;
        }

        /// <summary>
        /// A wet space's level, free-surface area and centroid. The level solves permeability × volume below it = the
        /// water, by Newton's method from last step's level (the free surface is the slope), kept inside a bracket and
        /// falling back to bisection. Dry spaces have no level; full ones keep their pressure head.
        /// </summary>
        void Settle(int s)
        {
            FloodSpace space = _plan.Spaces[s];
            if (_volume[s] <= 0f || space.SegmentCount == 0)
            {
                _level[s] = float.NegativeInfinity;
                _freeSurface[s] = 0f;
                _centroid[s] = space.FullCentroidShip;
                return;
            }
            Place(s);
            if (_full[s])
            {
                _level[s] = Mathf.Max(_level[s], _topW[s]);
                _freeSurface[s] = 0f;
                _centroid[s] = space.FullCentroidShip;
                return;
            }
            int first = space.FirstSegment, end = first + space.SegmentCount;
            float lo = float.MaxValue, hi = _topW[s];
            for (int i = first; i < end; i++) lo = Mathf.Min(lo, _bottomW[i]);
            float target = _volume[s] / space.Permeability;
            float level = float.IsInfinity(_level[s]) ? 0.5f * (lo + hi) : Mathf.Clamp(_level[s], lo, hi);
            float v = 0f, area = 0f;
            Vector3 moment = Vector3.zero;
            bool solved = false;
            for (int k = 0; k < NewtonSteps && !solved; k++)
            {
                v = DryVolume(space, level, out area, out moment);
                if (Mathf.Abs(v - target) <= LevelToleranceM * Mathf.Max(area, 1f)) break;   // this level holds the water
                if (v < target) lo = level; else hi = level;
                float next = area > 0f ? level + (target - v) / area : 0.5f * (lo + hi);
                if (next <= lo || next >= hi) next = 0.5f * (lo + hi);
                solved = Mathf.Abs(next - level) < LevelToleranceM;
                level = next;
                if (solved) v = DryVolume(space, level, out area, out moment);
            }
            for (int k = 0; k < BisectionSteps && !solved && Mathf.Abs(v - target) > LevelToleranceM * Mathf.Max(area, 1f); k++)
            {
                level = 0.5f * (lo + hi);
                v = DryVolume(space, level, out area, out moment);
                if (v < target) lo = level; else hi = level;
                solved = hi - lo < LevelToleranceM;
            }
            _level[s] = level;
            _freeSurface[s] = space.Permeability * area;
            _centroid[s] = v > 0f ? moment / v : space.FullCentroidShip;
        }

        /// <summary>
        /// Volume of the space's segments below a world level, before permeability; the area its surface cuts; and the
        /// volume's first moment in the ship frame (for the centroid), all in one pass.
        /// </summary>
        float DryVolume(in FloodSpace space, float level, out float surfaceArea, out Vector3 moment)
        {
            float v = 0f;
            surfaceArea = 0f;
            moment = Vector3.zero;
            for (int i = space.FirstSegment; i < space.FirstSegment + space.SegmentCount; i++)
            {
                FloodSegment g = _plan.Segments[i];
                float h = level - _bottomW[i], height = g.Y1 - g.Y0;
                if (h <= 0f) continue;
                if (h < height) surfaceArea += g.AreaM2;
                else h = height;
                float dv = g.AreaM2 * h;
                v += dv;
                moment.x += dv * g.X;
                moment.y += dv * (g.Y0 + 0.5f * h);
                moment.z += dv * g.Z;
            }
            return v;
        }

        /// <summary>The water's head above a world height in a space: 0 when dry or below the height.</summary>
        float HeadAt(int space, float y) => _volume[space] > 0f ? Mathf.Max(0f, _level[space] - y) : 0f;

        /// <summary>
        /// A pressed-up space's head: the one at which what flows in through its openings equals what flows out. If the
        /// water would leave even at the top of the space, it is not pressed: its head is its top, and it drains.
        /// </summary>
        float BalancedHead(int s)
        {
            float top = _topW[s];
            if (NetInflow(s, top) <= 0f) return top;
            if (_openingsOf[s].Count == 1)
            {
                // One way in and out: it balances when its head equals the other side's (a wing on its hole: the sea's).
                int only = _openingsOf[s][0];
                FloodOpening o = _openings[only];
                int other = o.SpaceA == s ? o.SpaceB : o.SpaceA;
                return Mathf.Max(top, other < 0 ? _seaY[only] : _level[other]);
            }
            float lo = top, hi = top;
            foreach (int i in _openingsOf[s])
            {
                FloodOpening o = _openings[i];
                int other = o.SpaceA == s ? o.SpaceB : o.SpaceA;
                float y = WorldY(o.PointShip);
                hi = Mathf.Max(hi, other < 0 ? _seaY[i] : _volume[other] > 0f ? _level[other] : y);
            }
            for (int k = 0; k < BisectionSteps && hi - lo > HeadToleranceM; k++)
            {
                float mid = 0.5f * (lo + hi);
                if (NetInflow(s, mid) > 0f) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        float NetInflow(int s, float head)
        {
            float g = _p.Gravity, net = 0f;
            foreach (int i in _openingsOf[s])
            {
                FloodOpening o = _openings[i];
                float y = WorldY(o.PointShip);
                int other = o.SpaceA == s ? o.SpaceB : o.SpaceA;
                float mine = Mathf.Max(0f, head - y);
                float theirs = other < 0 ? Mathf.Max(0f, _seaY[i] - y) : HeadAt(other, y);
                float dh = theirs - mine;
                net += o.AreaM2 * Mathf.Sign(dh) * Mathf.Sqrt(2f * g * Mathf.Abs(dh));
            }
            return net;
        }

        /// <summary>
        /// The most water one step may pass: half what would bring the two heads level (half, so the approach is a
        /// geometric glide, never an overshoot). Unlimited into or out of a dry or full space, whose head does not move
        /// with its volume the same way.
        /// </summary>
        float Equalising(in FloodOpening o, float dh)
        {
            float a = _full[o.SpaceA] || _volume[o.SpaceA] <= 0f ? 0f : _freeSurface[o.SpaceA];
            if (o.ToSea) return a > 0f ? 0.5f * dh * a : float.MaxValue;
            float b = _full[o.SpaceB] || _volume[o.SpaceB] <= 0f ? 0f : _freeSurface[o.SpaceB];
            if (a <= 0f || b <= 0f) return float.MaxValue;
            return 0.5f * dh * a * b / (a + b);
        }
    }
}
