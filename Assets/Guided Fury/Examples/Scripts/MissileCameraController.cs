using System.Collections.Generic;
using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Test-range camera. Two modes:
    /// - **Overview**: stays at a fixed pose (the position/rotation it had when the
    ///   component was enabled). Good for watching the whole range.
    /// - **Chase**: follows a specific active missile from a configurable offset, looking
    ///   slightly ahead so the target stays visible in frame.
    ///
    /// **Controls:**
    /// - `F` — toggle Chase / Overview.
    /// - `Space` — cycle to the next active missile when in Chase mode.
    ///
    /// Attach to the Main Camera.
    /// </summary>
    public sealed class MissileCameraController : MonoBehaviour
    {
        [Header("Mode")]
        [Tooltip("If true, auto-chase the first missile that launches. Otherwise start in Overview.")]
        [SerializeField] private bool autoChaseOnLaunch = true;

        [Header("Chase Camera")]
        [Tooltip("Offset from the missile (in missile-local space) where the camera sits.")]
        [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 4f, -15f);

        [Tooltip("How far ahead of the missile the camera looks. Larger = sees more downrange.")]
        [SerializeField] private float lookAheadM = 25f;

        [Tooltip("Camera smoothing time in seconds — how quickly the camera converges on its target pose.")]
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Keys")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F;
        [SerializeField] private KeyCode cycleKey  = KeyCode.Space;

        private enum Mode { Overview, Chase }
        private Mode mode = Mode.Overview;

        private Vector3 overviewPosition;
        private Quaternion overviewRotation;

        private MissileBehaviour chasedMissile;
        private Vector3 velocitySmoothing;

        // Refresh cadence for the active-missile scan.
        private MissileBehaviour[] cachedMissiles;
        private float lastScan;
        private const float ScanIntervalS = 0.25f;

        private void Awake()
        {
            // Remember whatever pose we were placed in — that's the overview pose.
            overviewPosition = transform.position;
            overviewRotation = transform.rotation;
        }

        private void Update()
        {
            RefreshCachedMissiles();

            // Auto-chase first launched missile.
            if (autoChaseOnLaunch && chasedMissile == null)
            {
                var first = FindFirstActiveMissile();
                if (first != null)
                {
                    chasedMissile = first;
                    mode = Mode.Chase;
                }
            }

            // Input.
            if (Input.GetKeyDown(toggleKey))
                mode = mode == Mode.Chase ? Mode.Overview : Mode.Chase;

            if (Input.GetKeyDown(cycleKey) && mode == Mode.Chase)
                chasedMissile = NextActiveMissile(chasedMissile);

            // Drop a missile that's gone terminal — pick another or fall back to overview.
            if (chasedMissile != null && !IsActive(chasedMissile))
                chasedMissile = NextActiveMissile(null);
        }

        private void LateUpdate()
        {
            if (mode == Mode.Chase && chasedMissile != null)
            {
                Vector3 targetPos = chasedMissile.transform.TransformPoint(chaseOffset);
                Vector3 lookPoint = chasedMissile.transform.position
                                  + chasedMissile.transform.forward * lookAheadM;

                transform.position = Vector3.SmoothDamp(
                    transform.position, targetPos, ref velocitySmoothing, smoothTime);
                Quaternion targetRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
            else
            {
                // Overview: glide back to the remembered pose.
                transform.position = Vector3.SmoothDamp(
                    transform.position, overviewPosition, ref velocitySmoothing, smoothTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, overviewRotation, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

        private void OnGUI()
        {
            // Tiny help blurb in the bottom-left so users know the controls exist.
            var rect = new Rect(12f, Screen.height - 50f, 320f, 36f);
            GUI.Box(rect, "");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width, 18f),
                $"Camera: {mode}   [{toggleKey}] toggle  [{cycleKey}] cycle missile");
            string chasedName = chasedMissile != null ? chasedMissile.name : "(none)";
            GUI.Label(new Rect(rect.x + 8f, rect.y + 18f, rect.width, 18f),
                $"Chasing: {chasedName}");
        }

        // ---- helpers --------------------------------------------------------

        private void RefreshCachedMissiles()
        {
            if (Time.time - lastScan >= ScanIntervalS)
            {
                cachedMissiles = FindObjectsByType<MissileBehaviour>(FindObjectsSortMode.None);
                lastScan = Time.time;
            }
        }

        private MissileBehaviour FindFirstActiveMissile()
        {
            if (cachedMissiles == null) return null;
            for (int i = 0; i < cachedMissiles.Length; i++)
                if (IsActive(cachedMissiles[i])) return cachedMissiles[i];
            return null;
        }

        private MissileBehaviour NextActiveMissile(MissileBehaviour current)
        {
            if (cachedMissiles == null || cachedMissiles.Length == 0) return null;

            int startIndex = 0;
            if (current != null)
            {
                int idx = System.Array.IndexOf(cachedMissiles, current);
                if (idx >= 0) startIndex = (idx + 1) % cachedMissiles.Length;
            }

            for (int i = 0; i < cachedMissiles.Length; i++)
            {
                int probe = (startIndex + i) % cachedMissiles.Length;
                if (IsActive(cachedMissiles[probe])) return cachedMissiles[probe];
            }
            return null;
        }

        private static bool IsActive(MissileBehaviour b)
        {
            if (b == null || !b.gameObject.activeInHierarchy) return false;
            if (!b.IsLaunched) return false;
            var phase = b.GetState().Phase;
            return phase != MissilePhase.Detonated && phase != MissilePhase.Failed;
        }
    }
}
