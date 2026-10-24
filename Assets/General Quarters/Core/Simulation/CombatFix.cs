using System;
using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.Simulation
{
    /// <summary>
    /// The combat state's fingerprint (Reckoning's "Fix"): FNV-1a over exact bit patterns. Two runs with
    /// the same seed and the same Orders must produce the same Fix on every tick; the first tick that
    /// differs names where they split.
    /// </summary>
    public static class CombatFix
    {
        public const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        public static ulong Mix(ulong h, uint w)
        {
            for (int b = 0; b < 4; b++)
            {
                h ^= (w >> (8 * b)) & 0xFF;
                h *= Prime;
            }
            return h;
        }

        public static ulong Mix(ulong h, int v) => Mix(h, unchecked((uint)v));
        public static ulong Mix(ulong h, long v) => Mix(Mix(h, unchecked((uint)v)), unchecked((uint)(v >> 32)));
        public static ulong Mix(ulong h, ulong v) => Mix(Mix(h, unchecked((uint)v)), unchecked((uint)(v >> 32)));
        public static ulong Mix(ulong h, bool v) => Mix(h, v ? 1u : 0u);
        public static ulong Mix(ulong h, float v) => Mix(h, unchecked((uint)BitConverter.SingleToInt32Bits(v)));
        public static ulong Mix(ulong h, Vector3 v) => Mix(Mix(Mix(h, v.x), v.y), v.z);
    }
}
