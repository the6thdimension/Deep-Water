using UnityEngine;
using DeepWater.Missiles;

[RequireComponent(typeof(BurkeVLSSystem))]
public class BurkeVLSFXController : MonoBehaviour
{
    [Header("Plume")]
    [Tooltip("Plume / smoke / booster exhaust prefab spawned at the launch point on each shot.")]
    public GameObject LaunchPlumePrefab;
    [Tooltip("Parent the plume under the launch point so it follows ship motion. If false, plume is detached.")]
    public bool ParentPlumeToLaunchPoint = true;
    [Min(0f)] public float PlumeLifetimeSeconds = 4f;

    [Header("Audio")]
    public AudioSource AudioSource;
    public AudioClip LaunchSfx;
    [Range(0f, 1f)] public float LaunchSfxVolume = 1f;

    [Header("Hatch (optional cross-cutting)")]
    [Tooltip("If true, the BurkeVLSSystem hatch animator triggers handle hatches per-cell — this controller does nothing extra.")]
    public bool UseBurkeVLSHatchAnimators = true;

    private BurkeVLSSystem _vls;

    private void Awake()
    {
        _vls = GetComponent<BurkeVLSSystem>();
        if (AudioSource == null)
            AudioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (_vls != null)
            _vls.OnCellFired += HandleCellFired;
    }

    private void OnDisable()
    {
        if (_vls != null)
            _vls.OnCellFired -= HandleCellFired;
    }

    private void HandleCellFired(int cellIndex, Transform launchPoint, MissileData missile, GameObject missileInstance)
    {
        SpawnPlume(launchPoint);
        PlayLaunchSfx(launchPoint);
    }

    private void SpawnPlume(Transform launchPoint)
    {
        if (LaunchPlumePrefab == null || launchPoint == null)
            return;

        GameObject plume = Instantiate(
            LaunchPlumePrefab,
            launchPoint.position,
            launchPoint.rotation,
            ParentPlumeToLaunchPoint ? launchPoint : null);

        if (PlumeLifetimeSeconds > 0f)
            Destroy(plume, PlumeLifetimeSeconds);
    }

    private void PlayLaunchSfx(Transform launchPoint)
    {
        if (LaunchSfx == null)
            return;

        if (AudioSource != null)
        {
            AudioSource.PlayOneShot(LaunchSfx, LaunchSfxVolume);
            return;
        }

        Vector3 pos = launchPoint != null ? launchPoint.position : transform.position;
        AudioSource.PlayClipAtPoint(LaunchSfx, pos, LaunchSfxVolume);
    }
}
