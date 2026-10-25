using System;
using UnityEngine;
using DeepWater.AirWing.Core.Gates;

namespace DeepWater.AirWing.ScriptableObjects
{
    /// <summary>
    /// A course of gates (W14), flown in order, lap after lap. Positions in metres in the scene; bands in the HUD's
    /// units (W22). A band (0, 0) is "any"; (min, 0) is "at least min". Every value is AUTHORED scenery for a test
    /// bed; <see cref="provenance"/> says how it was laid out.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGateCourse", menuName = "Deep Water/Air Wing/Gate Course", order = 50)]
    public class GateCourseSO : ScriptableObject
    {
        [Serializable]
        public struct GateEntry
        {
            public string name;
            [Tooltip("Centre of the ring in the scene, m.")]
            public Vector3 centreM;
            [Tooltip("The way through: 0 = north (+Z), 90 = east (+X).")]
            public float headingDeg;
            [Tooltip("Radius of the ring, m.")]
            public float radiusM;
            [Tooltip("True airspeed to hold through the gate, kt (min, max). (0, 0): any; (min, 0): at least min.")]
            public Vector2 speedBandKt;
            [Tooltip("Altitude above the sea datum to hold through the gate, ft (min, max). (0, 0): any; (min, 0): at least min.")]
            public Vector2 altitudeBandFt;
        }

        public GateEntry[] gates = Array.Empty<GateEntry>();
        [TextArea(2, 8)] public string provenance;

        /// <summary>The course for the core. Throws on a gate the core would refuse, naming it.</summary>
        public GateCourse Bake()
        {
            var baked = new Gate[gates.Length];
            for (int i = 0; i < gates.Length; i++)
            {
                GateEntry g = gates[i];
                baked[i] = new Gate(string.IsNullOrEmpty(g.name) ? $"Gate {i + 1}" : g.name, g.centreM, g.headingDeg, g.radiusM,
                                    ToBand(g.speedBandKt, $"{name} gate {i + 1} speed"), ToBand(g.altitudeBandFt, $"{name} gate {i + 1} altitude"));
            }
            return new GateCourse(baked);
        }

        static Band ToBand(Vector2 v, string what)
        {
            if (v.x == 0f && v.y == 0f) return Band.Any;
            if (v.y == 0f && v.x > 0f) return new Band(v.x, float.PositiveInfinity);
            if (v.x > v.y) throw new ArgumentException($"{what} band {v.x} … {v.y}: the minimum must not exceed the maximum");
            return new Band(v.x, v.y);
        }
    }
}
