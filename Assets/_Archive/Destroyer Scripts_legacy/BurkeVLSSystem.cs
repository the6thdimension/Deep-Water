using System;
using System.Collections;
using System.Collections.Generic;
using DeepWater.Missiles;
using UnityEngine;

public class BurkeVLSSystem : MonoBehaviour
{
    public enum BurkeVariant
    {
        FlightI_90Cells = 90,
        FlightIIA_96Cells = 96
    }

    public enum VLSCellState
    {
        Empty,
        Ready,
        Busy,
        CoolingDown
    }

    public enum LaunchPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    [Serializable]
    public sealed class VLSLoadoutEntry
    {
        public MissileData Missile;
        [Min(0)] public int Quantity = 1;
    }

    [Serializable]
    public sealed class VLSCellSocket
    {
        [Tooltip("Launch transform at the top of this cell.")]
        public Transform LaunchPoint;
        [Tooltip("Optional hatch animator. If set, trigger names below are used.")]
        public Animator HatchAnimator;
    }

    private sealed class VLSCellRuntime
    {
        public VLSCellSocket Socket;
        public MissileData LoadedMissile;
        public VLSCellState State;
        public float NextReadyTime;
        public int CellIndex;
    }

    private sealed class LaunchRequest
    {
        public Transform Target;
        public MissileData Missile;
        public int Count;
        public LaunchPriority Priority;
        public float RequestTime;
    }

    [Header("Burke Setup")]
    public BurkeVariant Variant = BurkeVariant.FlightIIA_96Cells;
    [Tooltip("Populate fore and aft cells with launch transforms.")]
    public List<VLSCellSocket> Cells = new List<VLSCellSocket>();

    [Header("Loadout")]
    [Tooltip("Loadout is packed into cells in list order.")]
    public List<VLSLoadoutEntry> InitialLoadout = new List<VLSLoadoutEntry>();
    public bool AutoLoadOnAwake = true;

    [Header("Launch Sequence")]
    [Min(0f)] public float HatchOpenDelay = 0.35f;
    [Min(0f)] public float PreLaunchDelay = 0.1f;
    [Min(0f)] public float InterShotDelay = 0.25f;
    [Min(0f)] public float CellCooldown = 1.0f;
    [Min(0f)] public float PostLaunchHatchDelay = 0.1f;
    public string HatchOpenTrigger = "Open";
    public string HatchCloseTrigger = "Close";

    [Header("Missile Spawn")]
    [Tooltip("Optional additional up direction impulse from VLS hot/cold launch ejection.")]
    [Min(0f)] public float EjectionImpulse = 0f;
    [Tooltip("Fallback rotation when LaunchPoint is not assigned.")]
    public Vector3 FallbackLaunchEuler = new Vector3(-90f, 0f, 0f);

    public event Action<int, VLSCellState> OnCellStateChanged;
    public event Action<Transform, MissileData> OnMissileLaunched;
    public event Action<string> OnLaunchWarning;
    public event Action<int, Transform, MissileData, GameObject> OnCellFired;

    public int TotalConfiguredCells => _cells.Count;
    public int ReadyCells
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].State == VLSCellState.Ready)
                    count++;
            }
            return count;
        }
    }

    private readonly List<VLSCellRuntime> _cells = new List<VLSCellRuntime>();
    private readonly List<LaunchRequest> _queue = new List<LaunchRequest>();
    private Coroutine _launchRoutine;
    private int _nextCellCursor;

    private void Awake()
    {
        BuildCellRuntime();
        ValidateBurkeCellCount();

        if (AutoLoadOnAwake)
            ApplyInitialLoadout();
    }

    private void Update()
    {
        float now = Time.time;
        for (int i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            if (cell.State == VLSCellState.CoolingDown && now >= cell.NextReadyTime)
            {
                cell.State = cell.LoadedMissile != null ? VLSCellState.Ready : VLSCellState.Empty;
                OnCellStateChanged?.Invoke(cell.CellIndex, cell.State);
            }
        }
    }

    [ContextMenu("Apply Initial Loadout")]
    public void ApplyInitialLoadout()
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            _cells[i].LoadedMissile = null;
            SetCellState(_cells[i], VLSCellState.Empty);
        }

        int slot = 0;
        for (int i = 0; i < InitialLoadout.Count; i++)
        {
            var entry = InitialLoadout[i];
            if (entry == null || entry.Missile == null || entry.Quantity <= 0)
                continue;

            for (int q = 0; q < entry.Quantity && slot < _cells.Count; q++)
            {
                _cells[slot].LoadedMissile = entry.Missile;
                SetCellState(_cells[slot], VLSCellState.Ready);
                slot++;
            }
        }
    }

    public bool RequestLaunch(Transform target, MissileData missile, int count = 1, LaunchPriority priority = LaunchPriority.Normal)
    {
        if (missile == null || missile.missilePrefab == null)
        {
            Warn("Launch rejected: missile or prefab is null.");
            return false;
        }

        if (count <= 0)
            count = 1;

        var req = new LaunchRequest
        {
            Target = target,
            Missile = missile,
            Count = count,
            Priority = priority,
            RequestTime = Time.time
        };

        _queue.Add(req);
        _queue.Sort((a, b) =>
        {
            int prioCmp = b.Priority.CompareTo(a.Priority);
            return prioCmp != 0 ? prioCmp : a.RequestTime.CompareTo(b.RequestTime);
        });

        if (_launchRoutine == null)
            _launchRoutine = StartCoroutine(ProcessQueue());

        return true;
    }

    [ContextMenu("Fire Single (First Loadout Missile)")]
    public void DebugFireSingle()
    {
        for (int i = 0; i < InitialLoadout.Count; i++)
        {
            if (InitialLoadout[i] != null && InitialLoadout[i].Missile != null)
            {
                RequestLaunch(null, InitialLoadout[i].Missile, 1, LaunchPriority.Normal);
                return;
            }
        }

        Warn("No missile found in InitialLoadout for debug fire.");
    }

    public bool HasReadyMissile(MissileData missile)
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].State == VLSCellState.Ready && _cells[i].LoadedMissile == missile)
                return true;
        }
        return false;
    }

    private IEnumerator ProcessQueue()
    {
        while (_queue.Count > 0)
        {
            var req = _queue[0];
            _queue.RemoveAt(0);

            int shotsRemaining = req.Count;
            while (shotsRemaining > 0)
            {
                var cell = FindNextReadyCell(req.Missile);
                if (cell == null)
                {
                    Warn($"No ready cell loaded with {req.Missile.displayName}.");
                    break;
                }

                yield return StartCoroutine(LaunchFromCell(cell, req.Target));
                shotsRemaining--;

                if (shotsRemaining > 0 && InterShotDelay > 0f)
                    yield return new WaitForSeconds(InterShotDelay);
            }
        }

        _launchRoutine = null;
    }

    private IEnumerator LaunchFromCell(VLSCellRuntime cell, Transform target)
    {
        SetCellState(cell, VLSCellState.Busy);

        if (cell.Socket.HatchAnimator != null && !string.IsNullOrWhiteSpace(HatchOpenTrigger))
            cell.Socket.HatchAnimator.SetTrigger(HatchOpenTrigger);

        if (HatchOpenDelay > 0f)
            yield return new WaitForSeconds(HatchOpenDelay);
        if (PreLaunchDelay > 0f)
            yield return new WaitForSeconds(PreLaunchDelay);

        Transform launchPoint = cell.Socket.LaunchPoint != null ? cell.Socket.LaunchPoint : transform;
        Quaternion launchRot = cell.Socket.LaunchPoint != null
            ? cell.Socket.LaunchPoint.rotation
            : transform.rotation * Quaternion.Euler(FallbackLaunchEuler);

        var missileObject = Instantiate(cell.LoadedMissile.missilePrefab, launchPoint.position, launchRot);

        var burkeMissile = missileObject.GetComponent<BurkeMissile>();
        if (burkeMissile != null)
        {
            burkeMissile.Initialize(cell.LoadedMissile);
            burkeMissile.SetTarget(target);
        }
        else
        {
            var behavior = missileObject.GetComponent<MissileBehavior>();
            if (behavior != null)
            {
                behavior.Initialize(cell.LoadedMissile);
                behavior.SetTarget(target);
            }
        }

        if (EjectionImpulse > 0f)
        {
            var rb = missileObject.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(launchPoint.up * EjectionImpulse, ForceMode.Impulse);
        }

        OnMissileLaunched?.Invoke(target, cell.LoadedMissile);
        OnCellFired?.Invoke(cell.CellIndex, launchPoint, cell.LoadedMissile, missileObject);

        // Burke cells are one-shot until reloaded in port.
        cell.LoadedMissile = null;
        cell.NextReadyTime = Time.time + CellCooldown;
        SetCellState(cell, VLSCellState.CoolingDown);

        if (PostLaunchHatchDelay > 0f)
            yield return new WaitForSeconds(PostLaunchHatchDelay);

        if (cell.Socket.HatchAnimator != null && !string.IsNullOrWhiteSpace(HatchCloseTrigger))
            cell.Socket.HatchAnimator.SetTrigger(HatchCloseTrigger);
    }

    private VLSCellRuntime FindNextReadyCell(MissileData missile)
    {
        if (_cells.Count == 0)
            return null;

        int start = _nextCellCursor;
        for (int i = 0; i < _cells.Count; i++)
        {
            int idx = (start + i) % _cells.Count;
            var cell = _cells[idx];
            if (cell.State == VLSCellState.Ready && cell.LoadedMissile == missile)
            {
                _nextCellCursor = (idx + 1) % _cells.Count;
                return cell;
            }
        }

        return null;
    }

    private void BuildCellRuntime()
    {
        _cells.Clear();
        _nextCellCursor = 0;

        if (Cells == null)
            Cells = new List<VLSCellSocket>();

        for (int i = 0; i < Cells.Count; i++)
        {
            var socket = Cells[i];
            if (socket == null || socket.LaunchPoint == null)
                continue;

            _cells.Add(new VLSCellRuntime
            {
                Socket = socket,
                LoadedMissile = null,
                State = VLSCellState.Empty,
                NextReadyTime = 0f,
                CellIndex = _cells.Count
            });
        }
    }

    private void ValidateBurkeCellCount()
    {
        int expected = (int)Variant;
        if (_cells.Count != expected)
            Warn($"Configured VLS cells: {_cells.Count}. Expected {expected} for {Variant}.");
    }

    private void SetCellState(VLSCellRuntime cell, VLSCellState state)
    {
        cell.State = state;
        OnCellStateChanged?.Invoke(cell.CellIndex, state);
    }

    private void Warn(string message)
    {
        Debug.LogWarning($"[BurkeVLSSystem] {message}", this);
        OnLaunchWarning?.Invoke(message);
    }
}
