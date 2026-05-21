using System;
using System.Collections.Generic;
using DeepWater.Missiles;
using UnityEngine;

public class SPG62FireControlRadar : MonoBehaviour
{
    public enum RadarMode
    {
        Search,
        TrackWhileScan,
        SingleTargetTrack
    }

    [Serializable]
    public sealed class TrackInfo
    {
        public Transform Target;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Range;
        public float AzimuthDeg;
        public float ElevationDeg;
        public float LastUpdateTime;
        public float TrackQuality;
        public bool IsLocked;
    }

    [Header("References")]
    public BurkeVLSSystem VLS;
    public Transform RadarOrigin;

    [Header("Mode")]
    public RadarMode Mode = RadarMode.TrackWhileScan;
    public Transform ManualSTTTarget;
    public bool AutoDesignateBestTrack = true;

    [Header("Scan Volume")]
    [Min(10f)] public float InstrumentedRange = 185000f;
    [Range(1f, 120f)] public float AzimuthFovDeg = 120f;
    [Range(1f, 90f)] public float ElevationFovDeg = 60f;
    [Range(1f, 180f)] public float AzimuthScanRateDegPerSec = 45f;
    [Range(1f, 120f)] public float ElevationScanRateDegPerSec = 20f;
    [Range(0.01f, 1f)] public float ScanUpdatePeriod = 0.1f;
    public LayerMask TargetMask = ~0;
    [Min(1)] public int MaxTargetsPerSweep = 256;

    [Header("Track Management")]
    [Range(0.1f, 10f)] public float TrackAcquireThreshold = 0.35f;
    [Range(0.1f, 10f)] public float LockThreshold = 0.65f;
    [Range(0.1f, 30f)] public float TrackTimeout = 6f;
    [Range(0f, 10f)] public float QualityRiseRate = 1.8f;
    [Range(0f, 10f)] public float QualityDecayRate = 0.9f;

    [Header("Fire Control")]
    public MissileData MissileToFire;
    [Range(1, 8)] public int SalvoSize = 2;
    [Range(0f, 2f)] public float InterSalvoDelay = 0.2f;
    [Range(0f, 10f)] public float MinFireInterval = 1.0f;
    [Range(500f, 300000f)] public float MinEngagementRange = 2000f;
    [Range(500f, 300000f)] public float MaxEngagementRange = 160000f;
    public bool AutoFireWhenLocked = false;

    [Header("Debug")]
    public bool DrawGizmos = true;
    public Color ScanColor = new Color(0.15f, 0.9f, 1f, 0.35f);
    public Color TrackColor = new Color(1f, 0.9f, 0.15f, 0.8f);
    public Color LockColor = new Color(1f, 0.2f, 0.15f, 0.95f);

    public event Action<TrackInfo> OnTrackAcquired;
    public event Action<TrackInfo> OnTrackUpdated;
    public event Action<TrackInfo> OnTrackLost;
    public event Action<TrackInfo> OnTargetLocked;
    public event Action<TrackInfo> OnTargetUnlocked;
    public event Action<TrackInfo, int> OnFireCommandSent;

    public IReadOnlyDictionary<Transform, TrackInfo> Tracks => _tracks;
    public TrackInfo CurrentLockedTrack => _lockedTrack;
    public int ActiveTrackCount => _tracks.Count;
    public bool HasLockedTrack => _lockedTrack != null && _lockedTrack.IsLocked;
    public float SecondsUntilFireAllowed => Mathf.Max(0f, _nextAllowedFireTime - Time.time);
    public float ScanAzimuthDeg => _azimuthScan;
    public float ScanElevationDeg => _elevationScan;
    public Vector3 ScanDirectionWorld
    {
        get
        {
            Transform origin = RadarOrigin != null ? RadarOrigin : transform;
            Quaternion scanRot = origin.rotation * Quaternion.Euler(_elevationScan, _azimuthScan, 0f);
            return scanRot * Vector3.forward;
        }
    }

    private readonly Dictionary<Transform, TrackInfo> _tracks = new Dictionary<Transform, TrackInfo>();
    private Collider[] _hitBuffer;
    private TrackInfo _lockedTrack;
    private float _azimuthScan;
    private float _elevationScan;
    private float _nextScanTime;
    private float _nextAllowedFireTime;
    private int _elevationDirection = 1;

    private void Awake()
    {
        _hitBuffer = new Collider[Mathf.Max(16, MaxTargetsPerSweep)];
        if (RadarOrigin == null)
            RadarOrigin = transform;
    }

    private void Update()
    {
        if (Time.time >= _nextScanTime)
        {
            _nextScanTime = Time.time + ScanUpdatePeriod;
            StepScan();
            ScanAndUpdateTracks();
            UpdateTrackLife();
            UpdateLocks();
            TryAutoFire();
        }
    }

    public bool TryFireCommand(int missileCount = -1, BurkeVLSSystem.LaunchPriority priority = BurkeVLSSystem.LaunchPriority.High)
    {
        if (VLS == null || MissileToFire == null || MissileToFire.missilePrefab == null)
            return false;
        if (_lockedTrack == null || !_lockedTrack.IsLocked || _lockedTrack.Target == null)
            return false;
        if (Time.time < _nextAllowedFireTime)
            return false;
        if (_lockedTrack.Range < MinEngagementRange || _lockedTrack.Range > MaxEngagementRange)
            return false;

        int count = missileCount > 0 ? missileCount : SalvoSize;
        bool ok = VLS.RequestLaunch(_lockedTrack.Target, MissileToFire, count, priority);
        if (!ok)
            return false;

        _nextAllowedFireTime = Time.time + MinFireInterval;
        OnFireCommandSent?.Invoke(_lockedTrack, count);
        return true;
    }

    public bool SetSTTTarget(Transform target)
    {
        ManualSTTTarget = target;
        Mode = RadarMode.SingleTargetTrack;
        return target != null;
    }

    public void SetMode(RadarMode mode)
    {
        Mode = mode;
    }

    public void ClearLock()
    {
        if (_lockedTrack != null)
        {
            _lockedTrack.IsLocked = false;
            OnTargetUnlocked?.Invoke(_lockedTrack);
            _lockedTrack = null;
        }
    }

    public bool TryDesignateBestTrack()
    {
        TrackInfo best = GetBestTrackCandidate();
        if (best == null || best.TrackQuality < LockThreshold)
            return false;

        if (_lockedTrack != null && _lockedTrack != best)
        {
            _lockedTrack.IsLocked = false;
            OnTargetUnlocked?.Invoke(_lockedTrack);
        }

        _lockedTrack = best;
        _lockedTrack.IsLocked = true;
        OnTargetLocked?.Invoke(_lockedTrack);
        return true;
    }

    public void ClearTracks()
    {
        foreach (var kv in _tracks)
            OnTrackLost?.Invoke(kv.Value);

        _tracks.Clear();
        if (_lockedTrack != null)
            OnTargetUnlocked?.Invoke(_lockedTrack);
        _lockedTrack = null;
    }

    private void StepScan()
    {
        float dt = ScanUpdatePeriod;
        _azimuthScan += AzimuthScanRateDegPerSec * dt;
        if (_azimuthScan > 180f)
            _azimuthScan -= 360f;

        _elevationScan += _elevationDirection * ElevationScanRateDegPerSec * dt;
        float maxEl = Mathf.Max(0.5f, ElevationFovDeg * 0.5f);
        if (_elevationScan > maxEl)
        {
            _elevationScan = maxEl;
            _elevationDirection = -1;
        }
        else if (_elevationScan < -maxEl)
        {
            _elevationScan = -maxEl;
            _elevationDirection = 1;
        }
    }

    private void ScanAndUpdateTracks()
    {
        Vector3 origin = RadarOrigin.position;
        int hitCount = Physics.OverlapSphereNonAlloc(origin, InstrumentedRange, _hitBuffer, TargetMask);

        float now = Time.time;
        float halfAz = AzimuthFovDeg * 0.5f;
        float halfEl = ElevationFovDeg * 0.5f;

        for (int i = 0; i < hitCount; i++)
        {
            Transform t = _hitBuffer[i] != null ? _hitBuffer[i].transform : null;
            if (t == null || t == RadarOrigin || t.root == transform.root)
                continue;

            if (Mode == RadarMode.SingleTargetTrack && ManualSTTTarget != null && t != ManualSTTTarget)
                continue;

            Vector3 toTarget = t.position - origin;
            float range = toTarget.magnitude;
            if (range < 1f || range > InstrumentedRange)
                continue;

            if (!IsInCurrentScanVolume(toTarget, halfAz, halfEl))
                continue;

            Vector3 dirLocal = RadarOrigin.InverseTransformDirection(toTarget.normalized);
            float az = Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg;
            float el = Mathf.Atan2(dirLocal.y, new Vector2(dirLocal.x, dirLocal.z).magnitude) * Mathf.Rad2Deg;

            float signal = CalculateSignalQuality(range, az, el, halfAz, halfEl);
            if (signal < TrackAcquireThreshold)
                continue;

            if (!_tracks.TryGetValue(t, out var info))
            {
                info = new TrackInfo
                {
                    Target = t,
                    Position = t.position,
                    Velocity = Vector3.zero,
                    Range = range,
                    AzimuthDeg = az,
                    ElevationDeg = el,
                    LastUpdateTime = now,
                    TrackQuality = signal,
                    IsLocked = false
                };
                _tracks.Add(t, info);
                OnTrackAcquired?.Invoke(info);
            }
            else
            {
                float dt = Mathf.Max(0.0001f, now - info.LastUpdateTime);
                info.Velocity = (t.position - info.Position) / dt;
                info.Position = t.position;
                info.Range = range;
                info.AzimuthDeg = az;
                info.ElevationDeg = el;
                info.LastUpdateTime = now;
                info.TrackQuality = Mathf.Clamp01(info.TrackQuality + signal * QualityRiseRate * ScanUpdatePeriod);
                OnTrackUpdated?.Invoke(info);
            }
        }
    }

    private bool IsInCurrentScanVolume(Vector3 toTargetWorld, float halfAz, float halfEl)
    {
        Vector3 dirLocal = RadarOrigin.InverseTransformDirection(toTargetWorld.normalized);
        float targetAz = Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg;
        float targetEl = Mathf.Atan2(dirLocal.y, new Vector2(dirLocal.x, dirLocal.z).magnitude) * Mathf.Rad2Deg;

        float azErr = Mathf.Abs(Mathf.DeltaAngle(_azimuthScan, targetAz));
        float elErr = Mathf.Abs(targetEl - _elevationScan);

        return azErr <= halfAz && elErr <= halfEl;
    }

    private float CalculateSignalQuality(float range, float az, float el, float halfAz, float halfEl)
    {
        float rangeFactor = Mathf.Clamp01(1f - (range / InstrumentedRange));
        float azFactor = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(_azimuthScan, az)) / Mathf.Max(1f, halfAz));
        float elFactor = 1f - Mathf.Clamp01(Mathf.Abs(el - _elevationScan) / Mathf.Max(1f, halfEl));
        return rangeFactor * azFactor * elFactor;
    }

    private void UpdateTrackLife()
    {
        if (_tracks.Count == 0)
            return;

        float now = Time.time;
        var toRemove = ListPool<Transform>.Get();
        foreach (var kv in _tracks)
        {
            TrackInfo t = kv.Value;
            float age = now - t.LastUpdateTime;
            t.TrackQuality = Mathf.Clamp01(t.TrackQuality - QualityDecayRate * ScanUpdatePeriod * Mathf.Clamp01(age));

            if (age > TrackTimeout || t.Target == null || t.TrackQuality <= 0.01f)
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            if (_tracks.TryGetValue(toRemove[i], out var lost))
            {
                if (_lockedTrack == lost)
                {
                    lost.IsLocked = false;
                    OnTargetUnlocked?.Invoke(lost);
                    _lockedTrack = null;
                }
                _tracks.Remove(toRemove[i]);
                OnTrackLost?.Invoke(lost);
            }
        }

        ListPool<Transform>.Release(toRemove);
    }

    private void UpdateLocks()
    {
        TrackInfo desired = null;

        if (Mode == RadarMode.SingleTargetTrack && ManualSTTTarget != null)
        {
            _tracks.TryGetValue(ManualSTTTarget, out desired);
        }
        else if (AutoDesignateBestTrack)
        {
            desired = GetBestTrackCandidate();
        }

        if (desired != null && desired.TrackQuality >= LockThreshold)
        {
            if (_lockedTrack != desired)
            {
                if (_lockedTrack != null)
                {
                    _lockedTrack.IsLocked = false;
                    OnTargetUnlocked?.Invoke(_lockedTrack);
                }

                _lockedTrack = desired;
                _lockedTrack.IsLocked = true;
                OnTargetLocked?.Invoke(_lockedTrack);
            }
        }
        else if (_lockedTrack != null && _lockedTrack.TrackQuality < LockThreshold * 0.7f)
        {
            _lockedTrack.IsLocked = false;
            OnTargetUnlocked?.Invoke(_lockedTrack);
            _lockedTrack = null;
        }
    }

    private TrackInfo GetBestTrackCandidate()
    {
        TrackInfo desired = null;
        float bestScore = float.MinValue;

        foreach (var kv in _tracks)
        {
            var trk = kv.Value;
            float score = trk.TrackQuality * 2f + Mathf.Clamp01(1f - (trk.Range / InstrumentedRange));
            if (score > bestScore)
            {
                bestScore = score;
                desired = trk;
            }
        }

        return desired;
    }

    private void TryAutoFire()
    {
        if (!AutoFireWhenLocked)
            return;

        if (_lockedTrack == null || !_lockedTrack.IsLocked)
            return;

        TryFireCommand();
    }

    private void OnDrawGizmosSelected()
    {
        if (!DrawGizmos)
            return;

        Transform originTf = RadarOrigin != null ? RadarOrigin : transform;
        Vector3 origin = originTf.position;

        Gizmos.color = ScanColor;
        Gizmos.DrawWireSphere(origin, InstrumentedRange);

        Quaternion scanRot = originTf.rotation * Quaternion.Euler(_elevationScan, _azimuthScan, 0f);
        Vector3 scanDir = scanRot * Vector3.forward;
        Gizmos.DrawLine(origin, origin + scanDir * InstrumentedRange);

        foreach (var kv in _tracks)
        {
            var t = kv.Value;
            if (t == null || t.Target == null)
                continue;

            Gizmos.color = t.IsLocked ? LockColor : TrackColor;
            Gizmos.DrawLine(origin, t.Target.position);
            Gizmos.DrawSphere(t.Target.position, Mathf.Lerp(5f, 12f, t.TrackQuality) * 0.1f);
        }
    }

    // Small list pool to avoid per-frame allocs while pruning tracks.
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

        public static List<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>(32);
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
