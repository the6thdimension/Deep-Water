using UnityEngine;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Companion camera for the launcher. Supports multiple viewing modes, FOV zoom, and
    /// target-aware tracking. Designed to share a scene with the existing
    /// <see cref="MissileCameraController"/> (which chases in-flight missiles) — this one
    /// stays anchored to the launcher and looks at the world from there.
    ///
    /// **Modes:**
    /// - <see cref="CameraMode.Overview"/>: above-and-behind the launcher, wide angle.
    /// - <see cref="CameraMode.Chase"/>: behind the launcher at deck height, looking forward.
    /// - <see cref="CameraMode.FPV"/>: at the launcher position, looking along its forward axis.
    /// - <see cref="CameraMode.TargetLock"/>: from the launcher's position, but the camera
    ///   pans to track <see cref="TargetSelector.CurrentTarget"/>. Useful for visual-acquisition
    ///   eyeball-style tracking.
    /// - <see cref="CameraMode.OpticalZoom"/>: like TargetLock but with narrow FOV (telescope
    ///   onto the target). FOV is controlled independently via <see cref="SetZoomNormalized"/>.
    ///
    /// **Zoom**: FOV slider in [0..1]. 0 = wide (default FOV), 1 = narrow (zoomed in). Applies
    /// in any mode but most useful in OpticalZoom.
    /// </summary>
    public sealed class LauncherCameraController : MonoBehaviour
    {
        public enum CameraMode
        {
            Overview     = 0,
            Chase        = 1,
            FPV          = 2,
            TargetLock   = 3,
            OpticalZoom  = 4,
        }

        [Header("Bindings")]
        [Tooltip("Camera that will be positioned and aimed by this controller. If null, uses Camera.main.")]
        [SerializeField] private Camera controlledCamera;

        [Tooltip("Launcher anchor. Camera is positioned relative to this transform.")]
        [SerializeField] private Transform launcher;

        [Tooltip("Target selector to read CurrentTarget from. If null, will look up a TargetSelector in the scene.")]
        [SerializeField] private TargetSelector targetSelector;

        [Header("Mode")]
        [SerializeField] private CameraMode mode = CameraMode.Overview;
        public CameraMode Mode { get => mode; set => mode = value; }

        [Header("FOV")]
        [Tooltip("FOV at zoom=0 (wide). Standard for cinematic / overview shots.")]
        [SerializeField] private float fovWideDeg = 60f;

        [Tooltip("FOV at zoom=1 (narrow / telephoto). Telescope-on-target effect.")]
        [SerializeField] private float fovNarrowDeg = 8f;

        [Tooltip("Default zoom level applied when entering each mode. OpticalZoom overrides to 1.")]
        [SerializeField, Range(0f, 1f)] private float defaultZoom = 0.0f;

        [Header("Smoothing")]
        [Tooltip("How fast the camera moves toward the target position. Lower = smoother / more cinematic, higher = more responsive.")]
        [SerializeField] private float positionSmoothTime = 0.15f;

        [Tooltip("How fast the camera rotates toward the target orientation. Lower = smoother.")]
        [SerializeField] private float rotationSmoothTime = 0.10f;

        // Per-mode offsets relative to launcher (local-space).
        [Header("Mode offsets (local-space, relative to launcher)")]
        [SerializeField] private Vector3 overviewOffset = new Vector3(0f, 25f, -30f);
        [SerializeField] private Vector3 chaseOffset    = new Vector3(0f,  5f, -15f);
        [SerializeField] private Vector3 fpvOffset      = new Vector3(0f,  2f,   1f);

        // -- Runtime -----------------------------------------------------
        private Vector3 posVelocity;
        private float zoomNormalized;

        public float ZoomNormalized { get => zoomNormalized; set => zoomNormalized = Mathf.Clamp01(value); }

        public void SetZoomNormalized(float z) { zoomNormalized = Mathf.Clamp01(z); }

        public void NextMode()
        {
            mode = (CameraMode)(((int)mode + 1) % System.Enum.GetValues(typeof(CameraMode)).Length);
            ApplyDefaultZoomForMode();
        }

        public void PreviousMode()
        {
            int count = System.Enum.GetValues(typeof(CameraMode)).Length;
            mode = (CameraMode)(((int)mode - 1 + count) % count);
            ApplyDefaultZoomForMode();
        }

        public void SetMode(CameraMode m) { mode = m; ApplyDefaultZoomForMode(); }

        private void ApplyDefaultZoomForMode()
        {
            // OpticalZoom snaps to fully zoomed-in; everything else respects the inspector default.
            zoomNormalized = mode == CameraMode.OpticalZoom ? 1f : defaultZoom;
        }

        private void Awake()
        {
            if (controlledCamera == null) controlledCamera = Camera.main;
            if (targetSelector == null)   targetSelector   = FindFirstObjectByType<TargetSelector>();
            zoomNormalized = defaultZoom;
        }

        private void LateUpdate()
        {
            if (controlledCamera == null || launcher == null) return;

            // -- Apply FOV ------------------------------------------------
            float fov = Mathf.Lerp(fovWideDeg, fovNarrowDeg, zoomNormalized);
            controlledCamera.fieldOfView = fov;

            // -- Compute target pose --------------------------------------
            Vector3 targetPos;
            Quaternion targetRot;
            ComputeTargetPose(out targetPos, out targetRot);

            // -- Smooth and apply -----------------------------------------
            controlledCamera.transform.position = Vector3.SmoothDamp(
                controlledCamera.transform.position, targetPos, ref posVelocity, positionSmoothTime);

            // Rotation: Slerp with effective t derived from smooth time. We don't need
            // critically-damped rotation; a simple time-based Slerp factor is fine.
            float rotT = rotationSmoothTime <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime / rotationSmoothTime);
            controlledCamera.transform.rotation = Quaternion.Slerp(
                controlledCamera.transform.rotation, targetRot, rotT);
        }

        private void ComputeTargetPose(out Vector3 pos, out Quaternion rot)
        {
            // Launcher-local offsets transformed to world space.
            switch (mode)
            {
                case CameraMode.Overview:
                    pos = launcher.TransformPoint(overviewOffset);
                    rot = Quaternion.LookRotation(launcher.position + Vector3.up * 2f - pos, Vector3.up);
                    break;

                case CameraMode.Chase:
                    pos = launcher.TransformPoint(chaseOffset);
                    rot = Quaternion.LookRotation(launcher.forward, Vector3.up);
                    break;

                case CameraMode.FPV:
                    pos = launcher.TransformPoint(fpvOffset);
                    rot = Quaternion.LookRotation(launcher.forward, Vector3.up);
                    break;

                case CameraMode.TargetLock:
                case CameraMode.OpticalZoom:
                {
                    pos = launcher.TransformPoint(fpvOffset);
                    Transform target = targetSelector != null ? targetSelector.CurrentTarget : null;
                    if (target != null)
                    {
                        Vector3 dir = (target.position - pos);
                        if (dir.sqrMagnitude > 1e-4f)
                            rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                        else
                            rot = Quaternion.LookRotation(launcher.forward, Vector3.up);
                    }
                    else
                    {
                        rot = Quaternion.LookRotation(launcher.forward, Vector3.up);
                    }
                    break;
                }

                default:
                    pos = launcher.position;
                    rot = launcher.rotation;
                    break;
            }
        }
    }
}
