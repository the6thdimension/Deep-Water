using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.Gates
{
    /// <summary>An inclusive band of values. <see cref="Any"/> takes everything and is not shown as a target.</summary>
    public readonly struct Band
    {
        public readonly float Min;
        public readonly float Max;

        public Band(float min, float max)
        {
            if (float.IsNaN(min) || float.IsNaN(max) || min > max)
                throw new ArgumentException($"band {min} … {max}: the minimum must not exceed the maximum");
            Min = min;
            Max = max;
        }

        public static Band Any => new Band(float.NegativeInfinity, float.PositiveInfinity);
        public bool IsAny => float.IsNegativeInfinity(Min) && float.IsPositiveInfinity(Max);
        public bool Contains(float value) => value >= Min && value <= Max;

        /// <summary>"350–450 kt", "≥ 300 kt", "≤ 500 ft" or "any".</summary>
        public string Describe(string unit)
        {
            if (IsAny) return "any";
            if (float.IsPositiveInfinity(Max)) return $"≥ {Min.ToString("N0", CultureInfo.InvariantCulture)} {unit}";
            if (float.IsNegativeInfinity(Min)) return $"≤ {Max.ToString("N0", CultureInfo.InvariantCulture)} {unit}";
            return $"{Min.ToString("N0", CultureInfo.InvariantCulture)}–{Max.ToString("N0", CultureInfo.InvariantCulture)} {unit}";
        }
    }

    /// <summary>
    /// A gate: an upright ring, flown through one way (W14). Centre and radius in metres. The way through is a heading
    /// (0 = +Z, 90 = +X, as the HUD reads). The bands are what the pilot should hold through it, in the HUD's units
    /// (W22): true airspeed in knots, and altitude above the sea datum in feet.
    /// </summary>
    public readonly struct Gate
    {
        public readonly string Name;
        public readonly Vector3 Centre;
        public readonly float HeadingDeg;
        public readonly float RadiusM;
        public readonly Band SpeedKt;
        public readonly Band AltitudeFt;

        public Gate(string name, Vector3 centre, float headingDeg, float radiusM, Band speedKt, Band altitudeFt)
        {
            if (!(radiusM > 0f) || float.IsInfinity(radiusM)) throw new ArgumentException($"gate '{name}': radius {radiusM} m must be positive");
            if (!IsFinite(centre) || float.IsNaN(headingDeg) || float.IsInfinity(headingDeg)) throw new ArgumentException($"gate '{name}': centre and heading must be finite");
            Name = name;
            Centre = centre;
            HeadingDeg = headingDeg;
            RadiusM = radiusM;
            SpeedKt = speedKt;
            AltitudeFt = altitudeFt;
        }

        /// <summary>The way through, horizontal.</summary>
        public Vector3 Facing => Quaternion.Euler(0f, HeadingDeg, 0f) * Vector3.forward;

        static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    /// <summary>One gate flown: where in the ring, and what the jet held through it against the gate's bands.</summary>
    public readonly struct GatePass
    {
        public readonly int Gate;
        public readonly int Lap;
        public readonly long Tick;
        public readonly float SpeedKt;
        public readonly float AltitudeFt;
        public readonly float OffCentreM;
        public readonly bool SpeedOk;
        public readonly bool AltitudeOk;

        public GatePass(int gate, int lap, long tick, float speedKt, float altitudeFt, float offCentreM, bool speedOk, bool altitudeOk)
        {
            Gate = gate;
            Lap = lap;
            Tick = tick;
            SpeedKt = speedKt;
            AltitudeFt = altitudeFt;
            OffCentreM = offCentreM;
            SpeedOk = speedOk;
            AltitudeOk = altitudeOk;
        }

        /// <summary>Both bands held.</summary>
        public bool Clean => SpeedOk && AltitudeOk;
    }

    /// <summary>
    /// Gates flown in order, lap after lap. Only the next gate counts: flying through it records a <see cref="GatePass"/>
    /// and moves on, and the others are ignored until their turn. It only reads the aircraft's state, so it can never
    /// change the flight or its Fix.
    /// </summary>
    public sealed class GateCourse
    {
        readonly Gate[] _gates;
        readonly List<GatePass> _passes = new List<GatePass>();

        public GateCourse(IReadOnlyList<Gate> gates)
        {
            if (gates == null || gates.Count == 0) throw new ArgumentException("a course needs at least one gate");
            _gates = new Gate[gates.Count];
            for (int i = 0; i < gates.Count; i++) _gates[i] = gates[i];
        }

        public IReadOnlyList<Gate> Gates => _gates;
        public IReadOnlyList<GatePass> Passes => _passes;
        /// <summary>The gate to fly next (an index into <see cref="Gates"/>).</summary>
        public int Next { get; private set; }
        /// <summary>Whole laps flown.</summary>
        public int Laps { get; private set; }

        /// <summary>Starts the course again from gate 1 (after a reset). The passes flown so far stay in the record.</summary>
        public void Restart() => Next = 0;

        /// <summary>
        /// Watches one tick of flight, from the state before the tick to the state after it. True when the next gate
        /// was flown; the speed and altitude are taken where the path crossed the gate's plane.
        /// </summary>
        public bool Observe(long tick, in AirframeState before, in AirframeState after, out GatePass pass)
        {
            pass = default;
            if (before.Status != FlightStatus.Flying) return false;
            Gate gate = _gates[Next];
            if (!Crossed(gate, before.Position, after.Position, out float t, out float offCentre)) return false;

            float speedKt = (float)(Mathf.Lerp(before.Velocity.magnitude, after.Velocity.magnitude, t) / Units.KnotToMetrePerSecond);
            float altitudeFt = (float)(Mathf.Lerp(before.Position.y, after.Position.y, t) / Units.FootToMetre);
            pass = new GatePass(Next, Laps, tick, speedKt, altitudeFt, offCentre, gate.SpeedKt.Contains(speedKt), gate.AltitudeFt.Contains(altitudeFt));
            _passes.Add(pass);
            if (++Next == _gates.Length) { Next = 0; Laps++; }
            return true;
        }

        /// <summary>
        /// Did the path from <paramref name="from"/> to <paramref name="to"/> cross the gate's plane the gate's way,
        /// inside its ring? <paramref name="fraction"/> is where along the path (0 … 1), <paramref name="offCentreM"/>
        /// how far from the centre.
        /// </summary>
        public static bool Crossed(in Gate gate, Vector3 from, Vector3 to, out float fraction, out float offCentreM)
        {
            Vector3 n = gate.Facing;
            float a = Vector3.Dot(from - gate.Centre, n);
            float b = Vector3.Dot(to - gate.Centre, n);
            fraction = 0f;
            offCentreM = float.PositiveInfinity;
            if (!(a < 0f && b >= 0f)) return false;   // not through the plane, or through it the wrong way

            fraction = a / (a - b);
            Vector3 point = Vector3.Lerp(from, to, fraction);
            offCentreM = Vector3.Distance(point, gate.Centre);
            return offCentreM <= gate.RadiusM;
        }
    }
}
