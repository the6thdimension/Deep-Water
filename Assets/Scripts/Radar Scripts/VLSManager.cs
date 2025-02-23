using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VLSManager : MonoBehaviour
{
    [Header("VLS Configuration")]
    [Tooltip("Missile prefab to be launched")]
    public GameObject missilePrefab;
    [Tooltip("Total number of VLS cells")]
    public int totalCells = 32;
    [Tooltip("Number of cells per module")]
    public int cellsPerModule = 8;
    [Range(0.1f, 2f)]
    public float launchInterval = 0.5f;
    
    [Header("Missile Types")]
    public MissileConfiguration[] missileConfigurations;
    
    [Header("System References")]
    public RH_RadarModule radar;
    public Transform launchPoint;
    public AudioClip launchSound;
    public ParticleSystem launchEffects;

    [Header("Environmental Considerations")]
    [Range(0f, 50f)]
    public float maxWindSpeed = 25f;
    [Range(0f, 45f)]
    public float maxRollAngle = 15f;
    [Range(0f, 45f)]
    public float maxPitchAngle = 10f;

    [Header("Combat Settings")]
    public bool autoEngageTargets = false;
    public float minEngagementRange = 10f;
    public float maxEngagementRange = 80f;
    public MissileType defaultMissileType = MissileType.StandardMissile;

    // Runtime data
    private List<VLSCell> cells;
    private Queue<LaunchRequest> launchQueue;
    private bool isLaunching;
    private AudioSource audioSource;
    private Vector3 windVector;
    private float lastWindUpdateTime;
    private const float WIND_UPDATE_INTERVAL = 5f;

    // Events
    public event Action<VLSCell> OnCellStatusChanged;
    public event Action<LaunchRequest> OnLaunchRequested;
    public event Action<LaunchRequest> OnLaunchCompleted;
    public event Action<string> OnSystemWarning;

    public IReadOnlyList<VLSCell> Cells => cells;
    public bool IsLaunching => isLaunching;
    public int AvailableMissiles => cells?.FindAll(c => c.Status == CellStatus.Ready).Count ?? 0;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        cells = new List<VLSCell>();
        launchQueue = new Queue<LaunchRequest>();
        InitializeCells();
    }

    private void Start()
    {
        if (radar != null)
        {
            radar.OnTargetDetected += HandleTargetDetected;
        }
        StartCoroutine(UpdateEnvironmentalConditions());
    }

    private void OnDestroy()
    {
        if (radar != null)
        {
            radar.OnTargetDetected -= HandleTargetDetected;
        }
    }

    private void HandleTargetDetected(RadarContact contact)
    {
        if (!autoEngageTargets) return;

        float distance = contact.Distance;
        if (distance >= minEngagementRange && distance <= maxEngagementRange)
        {
            LaunchPriority priority = DetermineLaunchPriority(distance);
            RequestLaunch(contact.Target.transform, defaultMissileType, priority);
        }
    }

    private LaunchPriority DetermineLaunchPriority(float distance)
    {
        if (distance < minEngagementRange * 1.5f)
        {
            return LaunchPriority.Emergency;
        }
        else if (distance < maxEngagementRange * 0.5f)
        {
            return LaunchPriority.High;
        }
        return LaunchPriority.Normal;
    }

    private void Update()
    {
        if (!isLaunching && launchQueue.Count > 0)
        {
            StartCoroutine(ProcessLaunchQueue());
        }
    }

    private void InitializeCells()
    {
        int moduleCount = Mathf.CeilToInt((float)totalCells / cellsPerModule);
        
        for (int module = 0; module < moduleCount; module++)
        {
            for (int cell = 0; cell < cellsPerModule; cell++)
            {
                int globalIndex = module * cellsPerModule + cell;
                if (globalIndex >= totalCells) break;

                var vlsCell = new VLSCell
                {
                    Index = globalIndex,
                    ModuleIndex = module,
                    CellIndex = cell,
                    Status = CellStatus.Ready,
                    MissileInstance = CreateMissileInstance(globalIndex)
                };
                
                cells.Add(vlsCell);
            }
        }
    }

    private GameObject CreateMissileInstance(int index)
    {
        GameObject missile = Instantiate(missilePrefab, transform);
        missile.name = $"Missile_{index}";
        missile.SetActive(false);
        return missile;
    }

    public void RequestLaunch(Transform target, MissileType missileType, LaunchPriority priority = LaunchPriority.Normal)
    {
        if (!ValidateTarget(target))
        {
            OnSystemWarning?.Invoke("Invalid target for launch request");
            return;
        }

        var config = GetMissileConfiguration(missileType);
        if (config == null)
        {
            OnSystemWarning?.Invoke($"No configuration found for missile type: {missileType}");
            return;
        }

        var cell = FindBestCell(missileType);
        if (cell == null)
        {
            OnSystemWarning?.Invoke($"No available cells for missile type: {missileType}");
            return;
        }

        var request = new LaunchRequest
        {
            Cell = cell,
            Target = target,
            MissileType = missileType,
            Configuration = config,
            Priority = priority,
            RequestTime = Time.time
        };

        EnqueueLaunchRequest(request);
    }

    private void EnqueueLaunchRequest(LaunchRequest request)
    {
        // Insert based on priority
        var tempQueue = new List<LaunchRequest>(launchQueue);
        tempQueue.Add(request);
        tempQueue.Sort((a, b) => 
        {
            int priorityCompare = b.Priority.CompareTo(a.Priority);
            if (priorityCompare != 0) return priorityCompare;
            return a.RequestTime.CompareTo(b.RequestTime);
        });

        launchQueue = new Queue<LaunchRequest>(tempQueue);
        OnLaunchRequested?.Invoke(request);
    }

    private IEnumerator ProcessLaunchQueue()
    {
        isLaunching = true;

        while (launchQueue.Count > 0)
        {
            if (!ValidateEnvironmentalConditions(out string warning))
            {
                OnSystemWarning?.Invoke(warning);
                yield return new WaitForSeconds(1f);
                continue;
            }

            var request = launchQueue.Dequeue();
            if (!ValidateRequest(request))
            {
                OnSystemWarning?.Invoke("Launch request no longer valid");
                continue;
            }

            yield return StartCoroutine(ExecuteLaunch(request));
            yield return new WaitForSeconds(launchInterval);
        }

        isLaunching = false;
    }

    private bool ValidateRequest(LaunchRequest request)
    {
        return request.Cell.Status == CellStatus.Ready && 
               request.Target != null && 
               ValidateTarget(request.Target);
    }

    private bool ValidateTarget(Transform target)
    {
        if (target == null) return false;
        
        // Check if target is within radar range and currently detected
        if (radar != null)
        {
            var contact = radar.GetRadarContact(target.gameObject);
            if (contact == null || !contact.IsCurrentlyDetected)
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateEnvironmentalConditions(out string warning)
    {
        warning = string.Empty;

        // Check ship orientation
        float roll = transform.rotation.eulerAngles.z;
        roll = (roll > 180) ? roll - 360 : roll;
        if (Mathf.Abs(roll) > maxRollAngle)
        {
            warning = $"Launch aborted: Roll angle ({roll:F1}°) exceeds maximum ({maxRollAngle}°)";
            return false;
        }

        float pitch = transform.rotation.eulerAngles.x;
        pitch = (pitch > 180) ? pitch - 360 : pitch;
        if (Mathf.Abs(pitch) > maxPitchAngle)
        {
            warning = $"Launch aborted: Pitch angle ({pitch:F1}°) exceeds maximum ({maxPitchAngle}°)";
            return false;
        }

        // Check wind conditions
        if (windVector.magnitude > maxWindSpeed)
        {
            warning = $"Launch aborted: Wind speed ({windVector.magnitude:F1} m/s) exceeds maximum ({maxWindSpeed} m/s)";
            return false;
        }

        return true;
    }

    private IEnumerator ExecuteLaunch(LaunchRequest request)
    {
        var cell = request.Cell;
        cell.Status = CellStatus.Launching;
        OnCellStatusChanged?.Invoke(cell);

        // Pre-launch sequence
        yield return StartCoroutine(PreLaunchSequence(cell));

        // Launch
        if (launchEffects != null)
        {
            launchEffects.transform.position = cell.MissileInstance.transform.position;
            launchEffects.Play();
        }

        if (audioSource != null && launchSound != null)
        {
            audioSource.PlayOneShot(launchSound);
        }

        cell.MissileInstance.SetActive(true);
        var controller = cell.MissileInstance.GetComponent<MissileController>();
        if (controller != null)
        {
            controller.Configure(request.Configuration);
            controller.Launch(request.Target);
        }

        // Post-launch cleanup
        cell.Status = CellStatus.Empty;
        OnCellStatusChanged?.Invoke(cell);
        OnLaunchCompleted?.Invoke(request);
    }

    private IEnumerator PreLaunchSequence(VLSCell cell)
    {
        // Simulate pre-launch checks and preparations
        yield return new WaitForSeconds(0.5f);
    }

    private VLSCell FindBestCell(MissileType missileType)
    {
        return cells.Find(c => c.Status == CellStatus.Ready);
    }

    private MissileConfiguration GetMissileConfiguration(MissileType type)
    {
        return Array.Find(missileConfigurations, c => c.Type == type);
    }

    private IEnumerator UpdateEnvironmentalConditions()
    {
        while (true)
        {
            // Simulate changing wind conditions
            if (Time.time - lastWindUpdateTime > WIND_UPDATE_INTERVAL)
            {
                windVector = new Vector3(
                    UnityEngine.Random.Range(-maxWindSpeed, maxWindSpeed),
                    0,
                    UnityEngine.Random.Range(-maxWindSpeed, maxWindSpeed)
                );
                lastWindUpdateTime = Time.time;
            }

            yield return new WaitForSeconds(WIND_UPDATE_INTERVAL);
        }
    }

    public void ReloadCell(int index)
    {
        if (index < 0 || index >= cells.Count) return;

        var cell = cells[index];
        if (cell.Status != CellStatus.Empty) return;

        StartCoroutine(ReloadSequence(cell));
    }

    private IEnumerator ReloadSequence(VLSCell cell)
    {
        cell.Status = CellStatus.Reloading;
        OnCellStatusChanged?.Invoke(cell);

        // Simulate reload time
        yield return new WaitForSeconds(5f);

        cell.MissileInstance = CreateMissileInstance(cell.Index);
        cell.Status = CellStatus.Ready;
        OnCellStatusChanged?.Invoke(cell);
    }

    public void ReloadAllEmptyCells()
    {
        foreach (var cell in cells)
        {
            if (cell.Status == CellStatus.Empty)
            {
                StartCoroutine(ReloadSequence(cell));
            }
        }
    }
}

[Serializable]
public class MissileConfiguration
{
    public MissileType Type;
    public float MaxRange;
    public float Speed;
    public float TurnRate;
    public float LaunchDelay;
    public AnimationCurve FlightProfile;
}

public class VLSCell
{
    public int Index { get; set; }
    public int ModuleIndex { get; set; }
    public int CellIndex { get; set; }
    public CellStatus Status { get; set; }
    public GameObject MissileInstance { get; set; }
}

public class LaunchRequest
{
    public VLSCell Cell { get; set; }
    public Transform Target { get; set; }
    public MissileType MissileType { get; set; }
    public MissileConfiguration Configuration { get; set; }
    public LaunchPriority Priority { get; set; }
    public float RequestTime { get; set; }
}

public enum MissileType
{
    StandardMissile,
    LongRange,
    AntiShip,
    AntiAir
}

public enum CellStatus
{
    Empty,
    Ready,
    Launching,
    Reloading,
    Malfunction
}

public enum LaunchPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Emergency = 3
}
