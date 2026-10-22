using UnityEngine;
using GuidedFury.Examples;

namespace DeepWater.Input.Adapters
{
    /// <summary>
    /// Wires the VelocityOne to the launcher's companion camera + target selector:
    /// - **Hat up / down** → cycle through targets (TargetSelector.Next/Previous).
    /// - **Hat left / right** → cycle through camera modes (Overview / Chase / FPV /
    ///   TargetLock / OpticalZoom).
    /// - **ThrottleSecondary lever** → zoom (FOV slider). Lever at idle = wide; lever
    ///   pushed = telephoto.
    /// - **NextCameraTarget button (action map)** → also cycles targets, for users who
    ///   prefer a dedicated button to the hat.
    /// - **CameraToggle button** → switches between TargetLock and OpticalZoom for
    ///   one-press zoom-on-target.
    ///
    /// **Hat edge detection:** the hat is a Vector2 that reads continuous values while
    /// held. We track previous state so cycling only fires on edges (push-to-cycle),
    /// not on every frame.
    /// </summary>
    public sealed class FlightStickToLauncherCamera : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private FlightStickInput reader;

        [Header("Targets")]
        [SerializeField] private TargetSelector targetSelector;
        [SerializeField] private LauncherCameraController launcherCamera;

        [Header("Tuning")]
        [Tooltip("Magnitude on the hat axis above which we consider it 'held' for edge detection.")]
        [Range(0.1f, 1f)] [SerializeField] private float hatThreshold = 0.5f;

        // Hat edge tracking — debounces continuous hat presses to single events.
        private bool hatUpHeld, hatDownHeld, hatLeftHeld, hatRightHeld;

        private void OnEnable()
        {
            if (reader == null)         reader         = FindFirstObjectByType<FlightStickInput>();
            if (targetSelector == null) targetSelector = FindFirstObjectByType<TargetSelector>();
            if (launcherCamera == null) launcherCamera = FindFirstObjectByType<LauncherCameraController>();

            if (reader != null)
            {
                reader.OnNextCameraTarget += HandleNextTarget;
                reader.OnCameraToggle     += HandleCameraToggle;
            }
        }

        private void OnDisable()
        {
            if (reader != null)
            {
                reader.OnNextCameraTarget -= HandleNextTarget;
                reader.OnCameraToggle     -= HandleCameraToggle;
            }
        }

        private void Update()
        {
            if (reader == null) return;

            // -- Hat edge detection --------------------------------------
            Vector2 hat = reader.Hat;
            EdgeFire(hat.y >  hatThreshold, ref hatUpHeld,    OnHatUp);
            EdgeFire(hat.y < -hatThreshold, ref hatDownHeld,  OnHatDown);
            EdgeFire(hat.x < -hatThreshold, ref hatLeftHeld,  OnHatLeft);
            EdgeFire(hat.x >  hatThreshold, ref hatRightHeld, OnHatRight);

            // -- Continuous: secondary throttle drives zoom --------------
            if (launcherCamera != null)
                launcherCamera.SetZoomNormalized(Mathf.Clamp01(reader.ThrottleSecondary));
        }

        private static void EdgeFire(bool currentlyHeld, ref bool wasHeld, System.Action onPress)
        {
            if (currentlyHeld && !wasHeld) onPress?.Invoke();
            wasHeld = currentlyHeld;
        }

        // -- Hat handlers --------------------------------------------------
        private void OnHatUp()    { if (targetSelector != null) targetSelector.NextTarget(); }
        private void OnHatDown()  { if (targetSelector != null) targetSelector.PreviousTarget(); }
        private void OnHatRight() { if (launcherCamera != null) launcherCamera.NextMode(); }
        private void OnHatLeft()  { if (launcherCamera != null) launcherCamera.PreviousMode(); }

        // -- Button handlers ----------------------------------------------
        private void HandleNextTarget()  { if (targetSelector != null) targetSelector.NextTarget(); }

        private void HandleCameraToggle()
        {
            if (launcherCamera == null) return;
            // One-press swap between TargetLock and OpticalZoom — useful for "snap to zoom-on-target."
            launcherCamera.SetMode(launcherCamera.Mode == LauncherCameraController.CameraMode.OpticalZoom
                ? LauncherCameraController.CameraMode.TargetLock
                : LauncherCameraController.CameraMode.OpticalZoom);
        }
    }
}
