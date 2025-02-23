using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class RH_RadarModule : MonoBehaviour
{
    public bool IsActive { get; protected set; }

    [Header("Radar Properties")]
    [Range(0f, 360f)]
    public float beamWidth = 30f;
    [Min(0f)]
    public float beamRange = 1000f;
    [Min(0f)]
    public float detectionRange = 1000f;
    public LayerMask targetLayerMask = ~0;

    [Header("Scan Settings")]
    [Tooltip("Radar rotation speed in degrees per second")]
    [Range(1f, 360f)]
    public float scanSpeed = 60f;
    [Tooltip("Current scan angle")]
    [SerializeField] private float currentScanAngle;
    [Tooltip("Enable 360-degree scanning")]
    public bool enableRotation = true;
    [Tooltip("Scan sector size in degrees when not in 360 mode")]
    [Range(10f, 180f)]
    public float sectorSize = 90f;
    [Tooltip("Center angle for sector scan")]
    [Range(0f, 360f)]
    public float sectorCenter = 0f;

    [Header("Detection Settings")]
    [SerializeField] private int maxTargets = 100;
    [SerializeField] private float targetTimeout = 10f;
    [SerializeField] private float updateRate = 0.1f;
    [Tooltip("Minimum signal strength required for detection")]
    [Range(0f, 1f)]
    public float detectionThreshold = 0.1f;
    [Tooltip("Environmental interference (0-1)")]
    [Range(0f, 1f)]
    public float interference = 0f;
    
    [Header("Advanced Features")]
    public bool enableDopplerTracking = true;
    public bool enableECMDetection = true;
    public bool enableTerrainMasking = true;
    [Range(0.1f, 10f)]
    public float weatherAttenuation = 1f;

    [Header("Contact Management")]
    [Tooltip("Time in seconds to maintain contact information after losing detection")]
    [Range(0f, 300f)]
    public float contactPersistenceTime = 30f;
    [Tooltip("Show all contacts, including those no longer detected but within persistence time")]
    public bool showPersistentContacts = true;

    private Collider[] targetColliderBuffer;
    private readonly List<GameObject> detectedTargets = new List<GameObject>();
    private readonly List<GameObject> persistentTargets = new List<GameObject>();
    private readonly Dictionary<GameObject, RadarContact> radarContacts = new Dictionary<GameObject, RadarContact>();
    private readonly Dictionary<GameObject, ECMStatus> ecmStatuses = new Dictionary<GameObject, ECMStatus>();
    
    private float nextUpdateTime;
    private Vector3 lastScanDirection;
    private readonly List<Vector3> scanPoints = new List<Vector3>();
    private int maxScanPoints = 360;

    public IReadOnlyList<GameObject> DetectedTargets => showPersistentContacts ? persistentTargets : detectedTargets;
    public IReadOnlyList<Vector3> ScanPoints => scanPoints;
    public float CurrentScanAngle => currentScanAngle;

    public delegate void TargetDetected(RadarContact contact);
    public event TargetDetected OnTargetDetected;
    public event TargetDetected OnTargetLost;
    public event Action<Vector3> OnScanPointAdded;

    protected virtual void Awake()
    {
        IsActive = false;
        targetColliderBuffer = new Collider[maxTargets];
        ValidateParameters();
    }

    private void Update()
    {
        if (!IsActive) return;
        UpdateScan();
        UpdateRadar();
    }

    private void UpdateScan()
    {
        if (!enableRotation && !IsTargetInSector(transform.forward)) return;

        float deltaAngle = scanSpeed * Time.deltaTime;
        currentScanAngle = (currentScanAngle + deltaAngle) % 360f;

        Vector3 scanDirection = Quaternion.Euler(0, currentScanAngle, 0) * Vector3.forward;
        
        if (enableRotation || IsTargetInSector(scanDirection))
        {
            lastScanDirection = scanDirection;
            AddScanPoint(transform.position + scanDirection * beamRange);
        }
    }

    private bool IsTargetInSector(Vector3 direction)
    {
        float angle = Vector3.SignedAngle(Vector3.forward, direction, Vector3.up);
        angle = (angle + 360f) % 360f;
        float halfSector = sectorSize / 2f;
        float minAngle = (sectorCenter - halfSector + 360f) % 360f;
        float maxAngle = (sectorCenter + halfSector + 360f) % 360f;

        if (minAngle > maxAngle)
        {
            return angle >= minAngle || angle <= maxAngle;
        }
        return angle >= minAngle && angle <= maxAngle;
    }

    private void AddScanPoint(Vector3 point)
    {
        scanPoints.Add(point);
        OnScanPointAdded?.Invoke(point);
        
        if (scanPoints.Count > maxScanPoints)
        {
            scanPoints.RemoveAt(0);
        }
    }

    private void ValidateParameters()
    {
        if (beamWidth <= 0f || beamWidth > 360f)
        {
            Debug.LogError($"[{name}] Invalid beam width: {beamWidth}. Setting to default 30°");
            beamWidth = 30f;
        }

        if (beamRange <= 0f)
        {
            Debug.LogError($"[{name}] Invalid beam range: {beamRange}. Setting to default 1000");
            beamRange = 1000f;
        }
    }

    public virtual void ActivateModule()
    {
        IsActive = true;
        nextUpdateTime = Time.time;
        currentScanAngle = transform.eulerAngles.y;
        Debug.Log($"[{name}] Radar activated");
    }

    public virtual void DeactivateModule()
    {
        IsActive = false;
        ClearAllContacts();
        scanPoints.Clear();
        Debug.Log($"[{name}] Radar deactivated");
    }

    public virtual void UpdateRadar()
    {
        if (!IsActive || Time.time < nextUpdateTime) return;

        DetectTargets();
        nextUpdateTime = Time.time + updateRate;
    }

    private void DetectTargets()
    {
        int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, targetColliderBuffer, targetLayerMask);
        HashSet<GameObject> currentDetections = new HashSet<GameObject>();

        for (int i = 0; i < targetsFound; i++)
        {
            GameObject target = targetColliderBuffer[i].gameObject;
            if (!IsValidTarget(target)) continue;

            currentDetections.Add(target);
            
            if (!radarContacts.TryGetValue(target, out RadarContact contact))
            {
                contact = new RadarContact(target);
                radarContacts[target] = contact;
                detectedTargets.Add(target);
                persistentTargets.Add(target);
                OnTargetDetected?.Invoke(contact);
            }

            contact.IsCurrentlyDetected = true;
            UpdateContactInfo(contact);
        }

        // Update detection status for all contacts
        foreach (var contact in radarContacts.Values)
        {
            if (!currentDetections.Contains(contact.Target))
            {
                contact.IsCurrentlyDetected = false;
            }
        }

        RemoveLostTargets(currentDetections);
    }

    private bool IsValidTarget(GameObject target)
    {
        if (!IsTargetWithinBeam(target.transform)) return false;
        
        Vector3 directionToTarget = target.transform.position - transform.position;
        float distance = directionToTarget.magnitude;

        // Check terrain masking
        if (enableTerrainMasking)
        {
            if (Physics.Raycast(transform.position, directionToTarget.normalized, distance, LayerMask.GetMask("Terrain")))
            {
                return false;
            }
        }

        // Calculate signal strength with all factors
        float signalStrength = CalculateSignalStrength(distance);
        
        // Apply weather attenuation
        signalStrength *= Mathf.Exp(-weatherAttenuation * distance / 1000f);
        
        // Apply interference
        signalStrength *= (1f - interference);

        // Check ECM
        if (enableECMDetection && target.TryGetComponent<IECM>(out var ecm))
        {
            if (!ecmStatuses.TryGetValue(target, out var status))
            {
                status = new ECMStatus();
                ecmStatuses[target] = status;
            }
            signalStrength *= (1f - ecm.GetJammingStrength());
            status.IsJamming = ecm.GetJammingStrength() > 0;
        }

        return signalStrength >= detectionThreshold;
    }

    private void UpdateContactInfo(RadarContact contact)
    {
        if (contact.Target == null) return;

        Vector3 newPosition = contact.Target.transform.position;
        float deltaTime = Time.time - contact.LastUpdateTime;
        
        if (deltaTime > 0)
        {
            Vector3 newVelocity = (newPosition - contact.Position) / deltaTime;
            
            if (enableDopplerTracking)
            {
                // Calculate radial velocity for Doppler effect
                Vector3 directionToTarget = (newPosition - transform.position).normalized;
                contact.RadialVelocity = Vector3.Dot(newVelocity, directionToTarget);
            }
            
            contact.Velocity = newVelocity;
        }

        contact.Position = newPosition;
        contact.Distance = Vector3.Distance(transform.position, newPosition);
        contact.SignalStrength = CalculateSignalStrength(contact.Distance);
        contact.LastUpdateTime = Time.time;

        if (enableECMDetection && ecmStatuses.TryGetValue(contact.Target, out var status))
        {
            contact.IsJammed = status.IsJamming;
        }
    }

    private float CalculateSignalStrength(float distance)
    {
        // Radar equation: SignalStrength ∝ 1/r⁴
        return Mathf.Clamp01(1f / Mathf.Pow(distance / (beamRange * 0.5f), 4));
    }

    private void RemoveLostTargets(HashSet<GameObject> currentDetections)
    {
        // Remove from active detection list immediately
        detectedTargets.RemoveAll(target => !currentDetections.Contains(target));

        // Check persistent contacts
        for (int i = persistentTargets.Count - 1; i >= 0; i--)
        {
            GameObject target = persistentTargets[i];
            if (target == null) 
            {
                persistentTargets.RemoveAt(i);
                radarContacts.Remove(target);
                ecmStatuses.Remove(target);
                continue;
            }

            if (!currentDetections.Contains(target))
            {
                var contact = radarContacts[target];
                float timeSinceLastDetection = Time.time - contact.LastUpdateTime;

                if (timeSinceLastDetection > contactPersistenceTime)
                {
                    persistentTargets.RemoveAt(i);
                    radarContacts.Remove(target);
                    ecmStatuses.Remove(target);
                    OnTargetLost?.Invoke(contact);
                }
            }
        }
    }

    private void ClearAllContacts()
    {
        detectedTargets.Clear();
        persistentTargets.Clear();
        radarContacts.Clear();
        ecmStatuses.Clear();
    }

    private bool IsTargetWithinBeam(Transform target)
    {
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float angleToTarget = Vector3.Angle(lastScanDirection, directionToTarget);
        float distance = Vector3.Distance(transform.position, target.position);

        return angleToTarget <= (beamWidth / 2) && distance <= beamRange;
    }

    public RadarContact GetRadarContact(GameObject target)
    {
        if (target == null) return null;
        radarContacts.TryGetValue(target, out RadarContact contact);
        return contact;
    }

    protected virtual void OnDrawGizmos()
    {
        if (!IsActive) return;
        
        // Draw radar cone
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0, currentScanAngle, 0), Vector3.one);
        Gizmos.matrix = matrix;
        
        float radius = Mathf.Tan(beamWidth * 0.5f * Mathf.Deg2Rad) * beamRange;
        Vector3 endPoint = Vector3.forward * beamRange;
        DrawRadarCone(Vector3.zero, endPoint, radius);

        // Draw scan points
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < scanPoints.Count; i++)
        {
            Gizmos.DrawLine(scanPoints[i - 1], scanPoints[i]);
        }

        // Draw detection sphere
        Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw sector if not in 360 mode
        if (!enableRotation)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            DrawRadarSector();
        }
    }

    private void DrawRadarCone(Vector3 start, Vector3 end, float radius)
    {
        int segments = 32;
        float deltaAngle = 360f / segments;
        Vector3[] circle = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * deltaAngle * Mathf.Deg2Rad;
            circle[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, end.z);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            Gizmos.DrawLine(start, circle[i]);
            Gizmos.DrawLine(circle[i], circle[next]);
        }
    }

    private void DrawRadarSector()
    {
        float halfSector = sectorSize * 0.5f;
        Vector3 center = transform.position;
        Vector3 forward = Quaternion.Euler(0, sectorCenter, 0) * Vector3.forward;
        Vector3 left = Quaternion.Euler(0, -halfSector, 0) * forward;
        Vector3 right = Quaternion.Euler(0, halfSector, 0) * forward;

        Gizmos.DrawLine(center, center + left * beamRange);
        Gizmos.DrawLine(center, center + right * beamRange);

        int segments = 32;
        float deltaAngle = sectorSize / segments;
        Vector3 previous = center + left * beamRange;

        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfSector + deltaAngle * i;
            Vector3 current = center + (Quaternion.Euler(0, sectorCenter + angle, 0) * Vector3.forward * beamRange);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}

public class RadarContact
{
    public GameObject Target { get; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float RadialVelocity { get; set; }
    public float Distance { get; set; }
    public float SignalStrength { get; set; }
    public float LastUpdateTime { get; set; }
    public bool IsJammed { get; set; }
    public bool IsCurrentlyDetected { get; set; }

    public RadarContact(GameObject target)
    {
        Target = target;
        Position = target.transform.position;
        LastUpdateTime = Time.time;
        IsCurrentlyDetected = true;
    }
}

public class ECMStatus
{
    public bool IsJamming { get; set; }
}

public interface IECM
{
    float GetJammingStrength();
}
