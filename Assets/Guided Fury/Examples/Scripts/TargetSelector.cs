using System.Collections.Generic;
using UnityEngine;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Canonical "what's the current target?" component. Discovers every <see cref="HittableBox"/>
    /// and <see cref="MovingTarget"/> in the scene on a 0.5 s scan, keeps an ordered list,
    /// and exposes <see cref="CurrentTarget"/> + cycling methods.
    ///
    /// Other components — LauncherCameraController, TargetBoundingBoxRenderer, future
    /// missile-fire adapters that auto-aim — all read from this one source of truth so the
    /// "selected target" is consistent across the HUD, camera, and weapons.
    ///
    /// **Why a separate component:** keeps target-selection state out of the camera. The
    /// camera reads `CurrentTarget`; so does the bounding-box renderer; so do future
    /// adapters. One canonical owner, many consumers.
    /// </summary>
    public sealed class TargetSelector : MonoBehaviour
    {
        [Header("Discovery")]
        [Tooltip("How often (seconds) to rescan the scene for targets. New targets added between scans aren't seen until the next refresh.")]
        [SerializeField] private float scanIntervalS = 0.5f;

        [Tooltip("Include HittableBox components as targets (default scene targets, crates, scenario targets).")]
        [SerializeField] private bool includeHittables = true;

        [Tooltip("Include MovingTarget components as targets even if they don't also have HittableBox.")]
        [SerializeField] private bool includeMovingTargets = true;

        // -- State -------------------------------------------------------
        private readonly List<Transform> targets = new List<Transform>();
        private int currentIndex = -1;
        private float lastScan = -999f;

        public Transform CurrentTarget =>
            (currentIndex >= 0 && currentIndex < targets.Count) ? targets[currentIndex] : null;

        public int TargetCount => targets.Count;
        public int CurrentIndex => currentIndex;

        // -- Public API --------------------------------------------------
        /// <summary>Cycle to the next target. Wraps. No-op if no targets exist.</summary>
        public void NextTarget()
        {
            RefreshIfStale();
            if (targets.Count == 0) { currentIndex = -1; return; }
            currentIndex = (currentIndex + 1) % targets.Count;
        }

        /// <summary>Cycle to the previous target. Wraps.</summary>
        public void PreviousTarget()
        {
            RefreshIfStale();
            if (targets.Count == 0) { currentIndex = -1; return; }
            currentIndex = (currentIndex - 1 + targets.Count) % targets.Count;
        }

        /// <summary>Pick the closest target to the given world position.</summary>
        public void SelectClosest(Vector3 fromWorldPos)
        {
            RefreshIfStale();
            if (targets.Count == 0) { currentIndex = -1; return; }

            float best = float.MaxValue;
            int idx = -1;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;
                float d = (targets[i].position - fromWorldPos).sqrMagnitude;
                if (d < best) { best = d; idx = i; }
            }
            currentIndex = idx;
        }

        public void Clear() => currentIndex = -1;

        // -- Lifecycle ---------------------------------------------------
        private void Update()
        {
            RefreshIfStale();
        }

        private void RefreshIfStale()
        {
            if (Time.unscaledTime - lastScan < scanIntervalS) return;
            lastScan = Time.unscaledTime;

            // Remember which transform is currently selected so we can preserve selection
            // across rescans even if the target list reorders.
            Transform previouslySelected = CurrentTarget;
            targets.Clear();

            if (includeHittables)
            {
                foreach (var h in FindObjectsByType<HittableBox>(FindObjectsSortMode.None))
                    if (h != null) targets.Add(h.transform);
            }
            if (includeMovingTargets)
            {
                foreach (var m in FindObjectsByType<MovingTarget>(FindObjectsSortMode.None))
                {
                    if (m == null) continue;
                    if (targets.Contains(m.transform)) continue; // already added via HittableBox
                    targets.Add(m.transform);
                }
            }

            // Restore selection if the previously-selected transform is still in the list.
            currentIndex = previouslySelected != null ? targets.IndexOf(previouslySelected) : -1;
            // If selection was lost and we have targets, default to the first.
            if (currentIndex < 0 && targets.Count > 0) currentIndex = 0;
        }
    }
}
