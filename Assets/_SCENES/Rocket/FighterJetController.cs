using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FighterJetController : MonoBehaviour
{
    [Header("Aircraft Configuration")]
    [Tooltip("Maximum thrust force in Newtons")]
    public float maxThrust = 150000f;
    [Tooltip("Minimum thrust as percentage of max thrust")]
    [Range(0f, 1f)]
    public float minThrustPercent = 0.1f;
    [Tooltip("How quickly the engine responds to thrust changes")]
    public float thrustResponseRate = 2f;
    
    [Header("Flight Characteristics")]
    [Tooltip("Maximum speed in m/s")]
    public float maxSpeed = 700f;
    [Tooltip("Stall speed in m/s")]
    public float stallSpeed = 60f;
    [Tooltip("Roll rate in degrees per second")]
    public float maxRollRate = 180f;
    [Tooltip("Pitch rate in degrees per second")]
    public float maxPitchRate = 60f;
    [Tooltip("Yaw rate in degrees per second")]
    public float maxYawRate = 30f;
    [Tooltip("Lift coefficient at zero angle of attack")]
    public float baseLiftCoefficient = 0.3f;
    [Tooltip("How much lift increases with angle of attack")]
    public float liftSensitivity = 0.5f;
    [Tooltip("Maximum angle of attack before stall")]
    public float criticalAngleOfAttack = 15f;

    [Header("AI Navigation")]
    public Transform[] waypoints;
    [Tooltip("Distance at which to consider waypoint reached")]
    public float waypointRadius = 200f;
    [Tooltip("Preferred cruising altitude")]
    public float cruisingAltitude = 1000f;
    [Tooltip("Minimum allowed altitude")]
    public float minAltitude = 100f;
    [Tooltip("Maximum allowed altitude")]
    public float maxAltitude = 5000f;
    [Tooltip("How aggressively to pursue waypoints (0-1)")]
    [Range(0f, 1f)]
    public float aggressiveness = 0.7f;
    [Tooltip("Maximum patrol radius from starting position")]
    public float maxPatrolRadius = 5000f;
    [Tooltip("Minimum patrol radius from starting position")]
    public float minPatrolRadius = 1000f;
    [Tooltip("Number of random waypoints to generate if none provided")]
    [Range(3, 12)]
    public int randomWaypointCount = 6;

    [Header("Patrol Patterns")]
    public PatrolPattern patternType = PatrolPattern.Circular;
    [Tooltip("How many loops in spiral pattern")]
    public int spiralLoops = 3;
    [Tooltip("Size of figure-8 pattern")]
    public float figure8Size = 2000f;
    [Tooltip("Randomness in pattern (0-1)")]
    [Range(0f, 1f)]
    public float patternRandomness = 0.2f;

    [Header("Formation Flying")]
    public FighterJetController leadAircraft;
    public FormationPosition formationPosition = FormationPosition.None;
    [Tooltip("Distance from lead aircraft in formation")]
    public float formationDistance = 100f;
    [Tooltip("Formation spacing multiplier")]
    public float formationSpacing = 1f;
    [Tooltip("How tightly to maintain formation (0-1)")]
    [Range(0f, 1f)]
    public float formationTightness = 0.8f;

    [Header("Terrain Avoidance")]
    public LayerMask terrainLayer;
    public float terrainCheckDistance = 500f;
    public float terrainAvoidanceStrength = 2f;
    public int terrainRayCount = 8;
    [Tooltip("Minimum clearance above terrain")]
    public float minTerrainClearance = 100f;

    [Header("Combat")]
    public Transform target;
    public float maxEngagementRange = 5000f;
    public float minEngagementRange = 500f;
    public float optimalEngagementRange = 2000f;

    // Runtime properties
    private Rigidbody rb;
    private float currentThrust;
    private Vector3 targetPosition;
    private FlightMode currentMode = FlightMode.Patrol;
    private int currentWaypointIndex;
    private float currentBankAngle;
    private float currentPitchAngle;
    private Vector3 lastPosition;
    private float currentSpeed;
    private bool isStalled;

    // Debug visualization
    private readonly List<Vector3> flightPath = new List<Vector3>();
    private const int maxPathPoints = 100;

    private Vector3 startPosition;
    private List<Vector3> randomWaypoints = new List<Vector3>();
    private bool usingRandomWaypoints;
    private Vector3[] terrainAvoidanceRays;
    private RaycastHit[] terrainHits;
    private Vector3 formationOffset;
    private readonly List<FighterJetController> wingmen = new List<FighterJetController>();

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 1f;
        
        startPosition = transform.position;
        InitializeTerrainAvoidance();
        
        if (leadAircraft != null)
        {
            leadAircraft.RegisterWingman(this);
            CalculateFormationOffset();
        }
        else if (waypoints == null || waypoints.Length == 0)
        {
            GeneratePatternWaypoints();
        }

        currentWaypointIndex = 0;
        UpdateTargetPosition();
        lastPosition = transform.position;
    }

    private void InitializeTerrainAvoidance()
    {
        terrainAvoidanceRays = new Vector3[terrainRayCount];
        terrainHits = new RaycastHit[terrainRayCount];
        float angleStep = 360f / terrainRayCount;
        
        for (int i = 0; i < terrainRayCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            terrainAvoidanceRays[i] = new Vector3(Mathf.Cos(angle), -0.5f, Mathf.Sin(angle)).normalized;
        }
    }

    private void RegisterWingman(FighterJetController wingman)
    {
        if (!wingmen.Contains(wingman))
        {
            wingmen.Add(wingman);
        }
    }

    private void CalculateFormationOffset()
    {
        switch (formationPosition)
        {
            case FormationPosition.Left:
                formationOffset = new Vector3(-formationDistance, 0, -formationDistance * 0.5f) * formationSpacing;
                break;
            case FormationPosition.Right:
                formationOffset = new Vector3(formationDistance, 0, -formationDistance * 0.5f) * formationSpacing;
                break;
            case FormationPosition.Trail:
                formationOffset = new Vector3(0, 0, -formationDistance) * formationSpacing;
                break;
            case FormationPosition.HighCover:
                formationOffset = new Vector3(0, formationDistance, -formationDistance) * formationSpacing;
                break;
            default:
                formationOffset = Vector3.zero;
                break;
        }
    }

    private void GeneratePatternWaypoints()
    {
        usingRandomWaypoints = true;
        randomWaypoints.Clear();

        switch (patternType)
        {
            case PatrolPattern.Circular:
                GenerateCircularPattern();
                break;
            case PatrolPattern.Spiral:
                GenerateSpiralPattern();
                break;
            case PatrolPattern.Figure8:
                GenerateFigure8Pattern();
                break;
            case PatrolPattern.Random:
                GenerateRandomPattern();
                break;
            case PatrolPattern.Tactical:
                GenerateTacticalPattern();
                break;
        }

        ApplyPatternRandomness();
        Debug.Log($"Generated {randomWaypoints.Count} waypoints for {patternType} pattern");
    }

    private void GenerateCircularPattern()
    {
        float angleStep = 360f / randomWaypointCount;
        float radius = (minPatrolRadius + maxPatrolRadius) * 0.5f;
        
        for (int i = 0; i < randomWaypointCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float randomAltitude = UnityEngine.Random.Range(minAltitude, maxAltitude);
            
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * radius,
                randomAltitude - startPosition.y,
                Mathf.Sin(angle) * radius
            );
            
            randomWaypoints.Add(startPosition + offset);
        }
    }

    private void GenerateSpiralPattern()
    {
        int pointsPerLoop = randomWaypointCount / spiralLoops;
        float angleStep = 360f / pointsPerLoop;
        float radiusStep = (maxPatrolRadius - minPatrolRadius) / (randomWaypointCount - 1);
        
        for (int i = 0; i < randomWaypointCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float radius = minPatrolRadius + (i * radiusStep);
            float progress = i / (float)(randomWaypointCount - 1);
            float altitude = Mathf.Lerp(minAltitude, maxAltitude, progress);
            
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * radius,
                altitude - startPosition.y,
                Mathf.Sin(angle) * radius
            );
            
            randomWaypoints.Add(startPosition + offset);
        }
    }

    private void GenerateFigure8Pattern()
    {
        for (int i = 0; i < randomWaypointCount; i++)
        {
            float t = i / (float)(randomWaypointCount - 1);
            float angle = t * Mathf.PI * 2;
            
            // Parametric equation for figure-8
            float x = figure8Size * Mathf.Sin(angle);
            float z = figure8Size * Mathf.Sin(angle) * Mathf.Cos(angle);
            float y = Mathf.Lerp(minAltitude, maxAltitude, (Mathf.Sin(angle * 2) + 1) * 0.5f);
            
            Vector3 offset = new Vector3(x, y - startPosition.y, z);
            randomWaypoints.Add(startPosition + offset);
        }
    }

    private void GenerateTacticalPattern()
    {
        // Generate points considering tactical advantages
        List<Vector3> tacticalPoints = new List<Vector3>();
        
        // High altitude observation points
        for (int i = 0; i < 2; i++)
        {
            tacticalPoints.Add(new Vector3(
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius),
                maxAltitude - startPosition.y,
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius)
            ));
        }
        
        // Low altitude approach vectors
        for (int i = 0; i < 2; i++)
        {
            tacticalPoints.Add(new Vector3(
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius),
                minAltitude - startPosition.y,
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius)
            ));
        }
        
        // Mid-altitude patrol points
        float midAltitude = (minAltitude + maxAltitude) * 0.5f;
        for (int i = 0; i < randomWaypointCount - 4; i++)
        {
            tacticalPoints.Add(new Vector3(
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius),
                midAltitude - startPosition.y,
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius)
            ));
        }
        
        // Optimize point order for efficient coverage
        randomWaypoints = OptimizeWaypointOrder(tacticalPoints);
    }

    private void GenerateRandomPattern()
    {
        for (int i = 0; i < randomWaypointCount; i++)
        {
            Vector3 randomPoint = new Vector3(
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius),
                UnityEngine.Random.Range(minAltitude, maxAltitude) - startPosition.y,
                UnityEngine.Random.Range(-maxPatrolRadius, maxPatrolRadius)
            );
            
            randomWaypoints.Add(startPosition + randomPoint);
        }
    }

    private List<Vector3> OptimizeWaypointOrder(List<Vector3> points)
    {
        List<Vector3> optimized = new List<Vector3>();
        List<Vector3> remaining = new List<Vector3>(points);
        Vector3 current = remaining[0];
        remaining.RemoveAt(0);
        optimized.Add(current);
        
        while (remaining.Count > 0)
        {
            float minDist = float.MaxValue;
            int nextIndex = 0;
            
            for (int i = 0; i < remaining.Count; i++)
            {
                float dist = Vector3.Distance(current, remaining[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nextIndex = i;
                }
            }
            
            current = remaining[nextIndex];
            optimized.Add(current);
            remaining.RemoveAt(nextIndex);
        }
        
        return optimized;
    }

    private void ApplyPatternRandomness()
    {
        if (patternRandomness <= 0) return;
        
        for (int i = 0; i < randomWaypoints.Count; i++)
        {
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            ) * (maxPatrolRadius * patternRandomness * 0.1f);
            
            randomWaypoints[i] += randomOffset;
            
            // Ensure altitude stays within bounds
            float y = randomWaypoints[i].y + startPosition.y;
            y = Mathf.Clamp(y, minAltitude, maxAltitude);
            randomWaypoints[i] = new Vector3(
                randomWaypoints[i].x,
                y - startPosition.y,
                randomWaypoints[i].z
            );
        }
    }

    private void HandleFlight()
    {
        if (leadAircraft != null)
        {
            HandleFormationFlight();
        }
        else
        {
            switch (currentMode)
            {
                case FlightMode.Patrol:
                    HandlePatrolMode();
                    break;
                case FlightMode.Combat:
                    HandleCombatMode();
                    break;
                case FlightMode.RTB:
                    HandleReturnToBase();
                    break;
            }
        }

        ApplyTerrainAvoidance();
        ApplyAerodynamics();
    }

    private void HandleFormationFlight()
    {
        // Calculate desired formation position relative to lead
        Vector3 desiredPosition = leadAircraft.transform.TransformPoint(formationOffset);
        Vector3 toDesiredPosition = desiredPosition - transform.position;
        
        // Adjust based on formation tightness
        Vector3 desiredDirection = Vector3.Lerp(
            transform.forward,
            toDesiredPosition.normalized,
            formationTightness
        );
        
        // Match lead's speed and direction
        currentThrust = Mathf.Lerp(currentThrust, leadAircraft.currentThrust, formationTightness);
        
        ExecuteManeuver(desiredDirection);
    }

    private void ApplyTerrainAvoidance()
    {
        Vector3 avoidanceForce = Vector3.zero;
        int hitCount = 0;
        
        for (int i = 0; i < terrainRayCount; i++)
        {
            Vector3 rayDirection = transform.TransformDirection(terrainAvoidanceRays[i]);
            if (Physics.Raycast(transform.position, rayDirection, out terrainHits[i], terrainCheckDistance, terrainLayer))
            {
                float weight = 1f - (terrainHits[i].distance / terrainCheckDistance);
                avoidanceForce += -rayDirection * weight * terrainAvoidanceStrength;
                hitCount++;
            }
        }
        
        if (hitCount > 0)
        {
            avoidanceForce /= hitCount;
            Vector3 currentUp = transform.up;
            Vector3 desiredDirection = Vector3.Lerp(transform.forward, (transform.forward + avoidanceForce + Vector3.up).normalized, 0.5f);
            ExecuteManeuver(desiredDirection);
        }
    }

    private void UpdateFlightMetrics()
    {
        // Calculate current speed
        currentSpeed = rb.linearVelocity.magnitude;
        
        // Update stall state
        float aoa = Vector3.Angle(transform.forward, rb.linearVelocity);
        isStalled = aoa > criticalAngleOfAttack && currentSpeed < stallSpeed * 1.2f;

        // Calculate bank and pitch angles
        currentBankAngle = Vector3.SignedAngle(transform.right, Vector3.ProjectOnPlane(transform.right, Vector3.up), transform.forward);
        currentPitchAngle = Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), transform.forward, transform.right);
    }

    private void HandlePatrolMode()
    {
        Vector3 toWaypoint = targetPosition - transform.position;
        float distanceToWaypoint = toWaypoint.magnitude;

        // Check if we've reached the waypoint
        if (distanceToWaypoint < waypointRadius)
        {
            if (usingRandomWaypoints)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % randomWaypoints.Count;
                // Occasionally generate new random waypoints for variety
                if (currentWaypointIndex == 0 && UnityEngine.Random.value < 0.3f)
                {
                    GeneratePatternWaypoints();
                }
            }
            else
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }
            UpdateTargetPosition();
            return;
        }

        // Calculate desired heading
        Vector3 desiredDirection = toWaypoint.normalized;
        
        // Adjust for altitude
        float altitudeError = cruisingAltitude - transform.position.y;
        Vector3 altitudeCorrection = Vector3.up * Mathf.Clamp(altitudeError / 100f, -1f, 1f);
        desiredDirection = Vector3.Lerp(desiredDirection, (desiredDirection + altitudeCorrection).normalized, 0.3f);

        // Execute the maneuver
        ExecuteManeuver(desiredDirection);
    }

    private void HandleCombatMode()
    {
        if (target == null)
        {
            currentMode = FlightMode.Patrol;
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        float distanceToTarget = toTarget.magnitude;

        // Calculate optimal position
        Vector3 optimalPosition = target.position - toTarget.normalized * optimalEngagementRange;
        Vector3 desiredDirection;

        if (distanceToTarget < minEngagementRange)
        {
            // Too close, extend away
            desiredDirection = -toTarget.normalized;
        }
        else if (distanceToTarget > maxEngagementRange)
        {
            // Too far, close in
            desiredDirection = toTarget.normalized;
        }
        else
        {
            // Maintain optimal range and position for attack
            Vector3 toOptimal = optimalPosition - transform.position;
            desiredDirection = toOptimal.normalized;
        }

        ExecuteManeuver(desiredDirection);
    }

    private void HandleReturnToBase()
    {
        // Implement RTB logic here
        currentMode = FlightMode.Patrol;
    }

    private void ExecuteManeuver(Vector3 desiredDirection)
    {
        // Calculate rotation needed
        Quaternion targetRotation = Quaternion.LookRotation(desiredDirection);
        
        // Calculate rotation rates based on current orientation vs desired
        float angleToTarget = Quaternion.Angle(transform.rotation, targetRotation);
        
        // Adjust thrust based on speed requirements
        float desiredThrust = maxThrust;
        if (currentSpeed > maxSpeed * 0.95f)
        {
            desiredThrust *= 0.5f;
        }
        else if (isStalled)
        {
            desiredThrust = maxThrust;
        }

        // Smoothly adjust thrust
        currentThrust = Mathf.Lerp(currentThrust, desiredThrust, thrustResponseRate * Time.fixedDeltaTime);
        
        // Apply forces
        rb.AddForce(transform.forward * currentThrust);
        
        // Calculate and apply rotation
        if (!isStalled)
        {
            float pitchInput = Mathf.Clamp(Vector3.SignedAngle(transform.forward, desiredDirection, transform.right), -maxPitchRate, maxPitchRate);
            float rollInput = Mathf.Clamp(Vector3.SignedAngle(transform.up, desiredDirection, transform.forward), -maxRollRate, maxRollRate);
            float yawInput = Mathf.Clamp(Vector3.SignedAngle(transform.forward, desiredDirection, transform.up), -maxYawRate, maxYawRate);

            Vector3 rotation = new Vector3(pitchInput, yawInput, -rollInput) * aggressiveness;
            rb.AddRelativeTorque(rotation * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }
    }

    private void ApplyAerodynamics()
    {
        // Calculate angle of attack
        float aoa = Vector3.Angle(transform.forward, rb.linearVelocity);
        aoa = Mathf.Clamp(aoa, -criticalAngleOfAttack, criticalAngleOfAttack);

        // Calculate lift
        float liftCoefficient = baseLiftCoefficient + (liftSensitivity * aoa / criticalAngleOfAttack);
        float dynamicPressure = 0.5f * 1.225f * currentSpeed * currentSpeed; // 1.225 is air density at sea level
        float liftForce = liftCoefficient * dynamicPressure;

        // Apply lift force
        Vector3 liftDirection = Vector3.Cross(rb.linearVelocity, transform.right).normalized;
        rb.AddForce(liftDirection * liftForce);

        // Apply drag
        float dragCoefficient = 0.05f + (liftCoefficient * liftCoefficient) / (3.14159f * 9f); // Induced drag
        float dragForce = dragCoefficient * dynamicPressure;
        rb.AddForce(-rb.linearVelocity.normalized * dragForce);
    }

    private void RecordFlightPath()
    {
        flightPath.Add(transform.position);
        if (flightPath.Count > maxPathPoints)
        {
            flightPath.RemoveAt(0);
        }
    }

    private void UpdateTargetPosition()
    {
        if (usingRandomWaypoints)
        {
            targetPosition = randomWaypoints[currentWaypointIndex];
        }
        else if (waypoints != null && waypoints.Length > 0 && waypoints[currentWaypointIndex] != null)
        {
            targetPosition = waypoints[currentWaypointIndex].position;
        }
        else
        {
            Debug.LogWarning("No valid waypoints available. Generating new random waypoints.");
            GeneratePatternWaypoints();
            currentWaypointIndex = 0;
            targetPosition = randomWaypoints[currentWaypointIndex];
        }
    }

    private void OnDrawGizmos()
    {
        // Draw existing visualization
        if (flightPath != null && flightPath.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < flightPath.Count - 1; i++)
            {
                Gizmos.DrawLine(flightPath[i], flightPath[i + 1]);
            }
        }

        // Draw terrain avoidance rays in debug mode
        if (Application.isPlaying && terrainAvoidanceRays != null)
        {
            for (int i = 0; i < terrainRayCount; i++)
            {
                Vector3 rayDirection = transform.TransformDirection(terrainAvoidanceRays[i]);
                if (terrainHits[i].distance > 0)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(transform.position, terrainHits[i].point);
                }
                else
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(transform.position, rayDirection * terrainCheckDistance);
                }
            }
        }

        // Draw formation position if applicable
        if (leadAircraft != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 desiredPosition = leadAircraft.transform.TransformPoint(formationOffset);
            Gizmos.DrawWireSphere(desiredPosition, 5f);
            Gizmos.DrawLine(transform.position, desiredPosition);
        }

        // Draw patrol pattern
        if (!usingRandomWaypoints && waypoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (Transform waypoint in waypoints)
            {
                if (waypoint != null)
                {
                    Gizmos.DrawWireSphere(waypoint.position, waypointRadius);
                }
            }
        }
        else if (usingRandomWaypoints && randomWaypoints != null)
        {
            // Draw pattern-specific visualization
            switch (patternType)
            {
                case PatrolPattern.Circular:
                    Gizmos.color = new Color(0, 1, 1, 0.1f);
                    Gizmos.DrawWireSphere(startPosition, maxPatrolRadius);
                    Gizmos.color = new Color(0, 1, 1, 0.05f);
                    Gizmos.DrawWireSphere(startPosition, minPatrolRadius);
                    break;
                    
                case PatrolPattern.Figure8:
                    Gizmos.color = new Color(1, 0, 1, 0.1f);
                    Gizmos.DrawWireCube(startPosition, new Vector3(figure8Size * 2, maxAltitude - minAltitude, figure8Size * 2));
                    break;
                    
                case PatrolPattern.Spiral:
                    Gizmos.color = new Color(1, 1, 0, 0.1f);
                    for (float t = 0; t < spiralLoops; t += 0.1f)
                    {
                        float angle = t * Mathf.PI * 2;
                        float radius = Mathf.Lerp(minPatrolRadius, maxPatrolRadius, t / spiralLoops);
                        Vector3 point = startPosition + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                        Gizmos.DrawWireSphere(point, 10f);
                    }
                    break;
            }

            // Draw waypoints
            Gizmos.color = Color.cyan;
            for (int i = 0; i < randomWaypoints.Count; i++)
            {
                Vector3 current = randomWaypoints[i];
                Vector3 next = randomWaypoints[(i + 1) % randomWaypoints.Count];
                
                Gizmos.DrawWireSphere(current, waypointRadius);
                Gizmos.DrawLine(current, next);
            }
        }

        // Draw combat information
        if (target != null && currentMode == FlightMode.Combat)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, minEngagementRange);
            Gizmos.DrawWireSphere(target.position, optimalEngagementRange);
            Gizmos.DrawLine(transform.position, target.position);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (newTarget != null)
        {
            currentMode = FlightMode.Combat;
        }
        else
        {
            currentMode = FlightMode.Patrol;
        }
    }

    public void ReturnToBase()
    {
        currentMode = FlightMode.RTB;
        target = null;
    }
}

public enum PatrolPattern
{
    Circular,
    Spiral,
    Figure8,
    Random,
    Tactical
}

public enum FormationPosition
{
    None,
    Left,
    Right,
    Trail,
    HighCover
}

public enum FlightMode
{
    Patrol,
    Combat,
    RTB
}
