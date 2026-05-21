#if UNITY_EDITOR
using System;
using UnityEngine;

namespace JMAC.Editor
{
    /// <summary>
    /// Unity 6+ compatible central bridge for JMAC Editor tools (SAURON, ATLAS, others).
    /// Decouples tools via events and shared utilities.
    /// </summary>
    public static class JmacEditorBridge
    {
        // ---- Cross-tool intents ----
        // Publish when a type (optionally method) is selected in an analysis tool.
        public static event Action<string /*fullTypeName*/, string /*methodName or null*/> TypeSelected;

        // Request ATLAS to open and focus a type. If startFlow is true, begin flow layout from that node.
        public static event Action<string /*fullTypeName*/, bool /*startFlow*/> OpenAtlasTypeRequested;

        // Request ATLAS to open IBD and focus instances of a type.
        public static event Action<string /*fullTypeName*/> OpenAtlasInstancesRequested;

        // Request SAURON to open and select a type.
        public static event Action<string /*fullTypeName*/> OpenSauronTypeRequested;

        // ---- Publishers (helpers) ----
        public static void PublishTypeSelected(string fullTypeName, string methodName = null)
            => TypeSelected?.Invoke(fullTypeName, methodName);

        public static void RequestOpenAtlasForType(string fullTypeName, bool startFlow = false)
            => OpenAtlasTypeRequested?.Invoke(fullTypeName, startFlow);

        public static void RequestOpenAtlasForInstances(string fullTypeName)
            => OpenAtlasInstancesRequested?.Invoke(fullTypeName);

        public static void RequestOpenSauronForType(string fullTypeName)
            => OpenSauronTypeRequested?.Invoke(fullTypeName);

        // ---- Shared namespace color utility (consistent tint across tools) ----
        private static readonly System.Collections.Generic.Dictionary<string, Color> s_NamespaceColors = new();

        public static Color GetNamespaceColor(string ns)
        {
            if (string.IsNullOrEmpty(ns)) ns = "(global)";
            if (s_NamespaceColors.TryGetValue(ns, out var c)) return c;

            // Stable hash -> HSV tint
            unchecked
            {
                int hash = 23;
                foreach (var ch in ns)
                    hash = hash * 31 + ch;
                float h = (hash & 0xFFFF) / (float)0xFFFF; // 0..1
                float s = 0.5f + ((hash >> 16) & 0xFF) / 1024f; // 0.5..0.75
                float v = 0.8f;
                Color.RGBToHSV(new Color(1,1,1), out _, out _, out _); // silence potential warnings
                c = Color.HSVToRGB(h, Mathf.Clamp01(s), v);
                s_NamespaceColors[ns] = c;
                return c;
            }
        }
    }
}
#endif
