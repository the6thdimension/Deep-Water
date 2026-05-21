using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All flight input to control the plane is read from this class. A player or AI pilot would
/// set the appropriate values to fly their plane.
/// </summary>
public class FlightInput
{
    public float Pitch = 0f;
    public float Yaw = 0f;
    public float Roll = 0f;

    public float Throttle = 1;
    public bool Reheat = false;

    public bool Flaps = false;
    public bool Brake = false;
    public bool GearDown = true;
}

/// <summary>
/// Helper class for handling parts that extend from the plane and cause drag. Parts extend over
/// time, and their intermediate states can be read to drive visuals on the model or HUD elements.
/// </summary>
[System.Serializable]
public class ExtendablePart
{
    [Tooltip("Time to fully extend/retract the part.")]
    public float ExtendTime = 1f;
    [Tooltip("Drag added to the plane relative to its normal drag value. Higher values impart " +
        "greater drag when the part is fully extended.")]
    public float DragMultiplier = 3f;

    [System.NonSerialized]
    public float ExtendState = 0f;

    public bool IsFullyExtended => ExtendState >= 1f - Mathf.Epsilon;
    public bool IsExtended => ExtendState > Mathf.Epsilon;

    public ExtendablePart(float extendTime, float dragMultiplier)
    {
        ExtendTime = extendTime;
        DragMultiplier = dragMultiplier;
    }

    public float Update(float targetState, float deltaTime)
    {
        ExtendState = ExtendTime > 0f
            ? Mathf.MoveTowards(ExtendState, targetState, 1f / ExtendTime * deltaTime)
            : targetState;

        return ExtendState;
    }
}

public interface IFlightInputProvider
{
    void PopulateInput(Aircraft aircraft, FlightInput input, float deltaTime);
}

public sealed class PlayerFlightInputProvider : IFlightInputProvider
{
    private readonly HashSet<string> _missingAxes = new HashSet<string>();

    public void PopulateInput(Aircraft aircraft, FlightInput input, float deltaTime)
    {
        input.Pitch = GetAxisSafe("Vertical");
        input.Roll = GetAxisSafe("Horizontal");
        input.Yaw = GetAxisSafe("Yaw");

        // Button-style throttle with afterburner detent.
        float targetThrottle;
        float throttleSpeed;
        if (Input.GetButton("Fire1"))
        {
            targetThrottle = 1f;
            throttleSpeed = .25f;
        }
        else if (Input.GetButton("Fire2"))
        {
            targetThrottle = 0f;
            throttleSpeed = .25f;
            input.Reheat = false;
        }
        else
        {
            targetThrottle = input.Throttle;
            throttleSpeed = 0f;
        }

        input.Throttle = Mathf.MoveTowards(
            input.Throttle,
            targetThrottle,
            throttleSpeed * deltaTime);

        // Deadband for detent.
        if (input.Throttle >= 0.995f && Input.GetButtonDown("Fire1"))
            input.Reheat = true;

        if (Input.GetKeyDown(KeyCode.F))
            input.Flaps = !input.Flaps;
        if (Input.GetKeyDown(KeyCode.B))
            input.Brake = !input.Brake;
        if (Input.GetKeyDown(KeyCode.G))
            input.GearDown = !input.GearDown;
    }

    private float GetAxisSafe(string axisName)
    {
        if (_missingAxes.Contains(axisName))
            return 0f;

        try
        {
            return Input.GetAxis(axisName);
        }
        catch (System.ArgumentException)
        {
            _missingAxes.Add(axisName);
            Debug.LogWarning($"{nameof(PlayerFlightInputProvider)}: Input axis '{axisName}' is not configured. Defaulting to 0.");
            return 0f;
        }
    }
}

public sealed class AIFlightInputProvider : IFlightInputProvider
{
    private const float WaypointSwitchDistance = 300f;
    private const float PitchGain = 3f;
    private const float YawGain = 5f;
    private const float RollGain = 3f;
    private const float WingsLevelBlendDegrees = 1.5f;

    private int _selectedPoint;

    public void PopulateInput(Aircraft aircraft, FlightInput input, float deltaTime)
    {
        input.Throttle = 1f;

        // Null-safe: if no target and no valid path, just neutral controls.
        if (aircraft.Target == null && (aircraft.Path == null || aircraft.Path.Points == null || aircraft.Path.Points.Count == 0))
        {
            input.Pitch = input.Roll = input.Yaw = 0f;
            return;
        }

        Vector3 targetPosition;

        if (aircraft.Target == null)
        {
            targetPosition = aircraft.Path.Points[_selectedPoint].position;
            var distanceToTarget = Vector3.Distance(targetPosition, aircraft.transform.position);
            if (distanceToTarget < WaypointSwitchDistance)
                _selectedPoint = (_selectedPoint + 1) % aircraft.Path.Points.Count;
        }
        else
        {
            targetPosition = aircraft.Target.position;
        }

        Vector3 localTargetDirection = aircraft.transform.InverseTransformPoint(targetPosition).normalized;
        input.Pitch = Mathf.Clamp(-localTargetDirection.y * PitchGain, -1f, 1f);
        input.Yaw = Mathf.Clamp(localTargetDirection.x * YawGain, -1f, 1f);

        var wingsLevelRoll = aircraft.transform.right.y * RollGain;
        var turnIntoRoll = localTargetDirection.x * RollGain;

        var angleOffTarget = Vector3.Angle(Vector3.forward, localTargetDirection);
        var wingsLevelInfluence = Mathf.InverseLerp(0f, WingsLevelBlendDegrees, angleOffTarget);
        input.Roll = Mathf.Lerp(wingsLevelRoll, turnIntoRoll, wingsLevelInfluence);
        input.Roll = Mathf.Clamp(input.Roll, -1f, 1f);
    }
}

public class Aircraft : MonoBehaviour
{
    [Header("Profile")]
    public AircraftAeroProfile AeroProfile = null;
    public bool ApplyProfileOnAwake = true;
    public bool ApplyProfileOnValidate = true;

    [Header("Unity Properties")]
    [Tooltip("FixedUpdate is more accurate and consistent, but Update looks smoother at high FPS.")]
    public bool UseFixedUpdate = false;
    [Tooltip("Knots")]
    public float StartSpeed = 350f;

    [Header("AI Pilot")]
    public WaypointPath Path = null;
    public Transform Target = null;

    [Tooltip("Signifies that this is the player aircraft.")]
    public bool IsPlayer = false;

    [Tooltip("Scales the distance moved by the plane. Can be used to create tighter action at the cost " +
        "of things looking like they move slower than normal."), Range(.1f, 1f)]
    public float Scale = 1f;

    [Header("Ground Handling and Collisions")]
    public bool StartGrounded = false;
    public LayerMask CollisionMask = -1;
    public float GearHeight = 1.6f;

    [Header("Thrust to Weight")]
    [Tooltip("Kilograms")]
    public float Mass = 11500f;
    [Tooltip("Newtons")]
    public float MilThrust = 79000f;
    [Tooltip("Newtons")]
    public float ReheatThrust = 129000f;

    [Header("Drag and Stability")]
    [Tooltip("Unitless. Higher values result in slower planes.")]
    public float Drag = .7f;
    [Tooltip("Unitless. Higher values result in angle of attack creating more drag.")]
    public float InducedDrag = .35f;
    [Range(1f, 10f), Tooltip("Unitless. Higher values result in less AOA generated during turns.")]
    public float Responsiveness = 3f;

    [Header("Aerodynamic Model")]
    [Tooltip("Reference wing area in square meters.")]
    public float ReferenceArea = 27.87f;
    [Tooltip("Air density in kg/m^3. Sea-level ISA is about 1.225.")]
    public float AirDensity = 1.225f;
    [Tooltip("Side-force coefficient slope per radian of sideslip.")]
    public float SideForceSlope = 0.35f;
    [Tooltip("Additional max lift coefficient contributed by fully extended flaps.")]
    public float FlapLiftBonus = 0.35f;
    [Tooltip("Additional drag coefficient contributed by fully extended flaps.")]
    public float FlapDragBonus = 0.05f;
    public AnimationCurve LiftCoefficientByAlpha = new AnimationCurve(
        new Keyframe(-20f, -0.6f),
        new Keyframe(-10f, -0.2f),
        new Keyframe(0f, 0.2f),
        new Keyframe(10f, 0.95f),
        new Keyframe(15f, 1.1f),
        new Keyframe(20f, 0.75f),
        new Keyframe(30f, 0.2f));
    public AnimationCurve DragCoefficientByAlpha = new AnimationCurve(
        new Keyframe(-20f, 0.2f),
        new Keyframe(-10f, 0.08f),
        new Keyframe(0f, 0.02f),
        new Keyframe(10f, 0.06f),
        new Keyframe(20f, 0.18f),
        new Keyframe(30f, 0.45f));
    [Tooltip("Pitch effectiveness vs dynamic pressure (Pa).")]
    public AnimationCurve PitchEffectivenessByQ = new AnimationCurve(
        new Keyframe(0f, 0.15f),
        new Keyframe(1000f, 0.55f),
        new Keyframe(6000f, 1f),
        new Keyframe(12000f, 0.9f));
    [Tooltip("Roll effectiveness vs dynamic pressure (Pa).")]
    public AnimationCurve RollEffectivenessByQ = new AnimationCurve(
        new Keyframe(0f, 0.2f),
        new Keyframe(1000f, 0.6f),
        new Keyframe(5000f, 1f),
        new Keyframe(12000f, 0.85f));
    [Tooltip("Yaw effectiveness vs dynamic pressure (Pa).")]
    public AnimationCurve YawEffectivenessByQ = new AnimationCurve(
        new Keyframe(0f, 0.25f),
        new Keyframe(1000f, 0.7f),
        new Keyframe(6000f, 1f),
        new Keyframe(12000f, 0.8f));
    [Tooltip("Max pitch-rate command change (deg/s^2).")]
    public float PitchActuatorAccel = 160f;
    [Tooltip("Max roll-rate command change (deg/s^2).")]
    public float RollActuatorAccel = 600f;
    [Tooltip("Max yaw-rate command change (deg/s^2).")]
    public float YawActuatorAccel = 120f;

    [Header("Stalling")]
    [Tooltip("Knots. Flying slower than this causes a loss in altitude. Affects low speed maneverability as well. " +
        "Higher values result in a more sluggish plane at low speeds, while planes with a low stall speed can easily " +
        "reach their maximum turn rates at low speed.")]
    public float StallSpeedClean = 150f;
    [Tooltip("Knots. When flaps are fully extended, this becomes the new stall speed.")]
    public float StallSpeedFlaps = 130f;
    [Tooltip("Angle of attack (deg) the plane will have when stalled.")]
    public float StallAOA = 10f;

    [Header("Draggy Parts")]
    public ExtendablePart Flaps = new ExtendablePart(1f, 2f);
    public ExtendablePart Gear = new ExtendablePart(2f, 3f);
    public ExtendablePart Brakes = new ExtendablePart(.5f, 3f);

    [Header("G Limits")]
    [Tooltip("Positive G limit. Has a great impact on maneuverability at speed.")]
    public float MaxG = 7f;
    [Tooltip("Negative G limit.")]
    public float MinG = 3f;

    [Header("Maneuverability")]
    [Tooltip("Max theoretical pitch rate (deg/s) the plane can achieve.")]
    public float MaxPitchRate = 20f;
    [Tooltip("How quickly the aircraft reacts to pitch input.")]
    public float PitchResponse = 4f;
    [Tooltip("Angular damping applied to pitch rate command.")]
    public float PitchDamping = 0.35f;
    [Tooltip("Max theoretical roll rate (deg/s) the plane can achieve.")]
    public float MaxRollRate = 120f;
    [Tooltip("How quickly the aircraft reacts to roll input.")]
    public float RollResponse = 5f;
    [Tooltip("Angular damping applied to roll rate command.")]
    public float RollDamping = 0.2f;
    [Tooltip("Max theoretical yaw rate (deg/s) the plane can achieve.")]
    public float MaxYawRate = 6f;
    [Tooltip("How quickly the aircraft reacts to yaw input.")]
    public float YawResponse = 2f;
    [Tooltip("Angular damping applied to yaw rate command.")]
    public float YawDamping = 0.3f;

    /// <summary>
    /// Stick, rudder, and throttle input for flying the plane. If this is the player aircraft,
    /// input will automatically be pulled from the player. Otherwise, some AI should set this
    /// to control the aircraft.
    /// </summary>
    public FlightInput FlightInput = new FlightInput();

    /// <summary>
    /// Velocity in m/s
    /// </summary>
    public Vector3 Velocity { get; private set; } = Vector3.zero;

    /// <summary>
    /// Normalized direction of the velocity vector.
    /// </summary>
    public Vector3 VelocityDirection { get; private set; } = Vector3.forward;

    /// <summary>
    /// Speed in m/s
    /// </summary>
    public float Speed { get; private set; } = 0f;

    /// <summary>
    /// Pitch rate in deg/s
    /// </summary>
    public float PitchRate { get; private set; } = 0f;

    /// <summary>
    /// Roll rate in deg/s
    /// </summary>
    public float RollRate { get; private set; } = 0f;

    /// <summary>
    /// Yaw rate in deg/s
    /// </summary>
    public float YawRate { get; private set; } = 0f;

    /// <summary>
    /// Instantaneous G in the pitch axis. Reads 1G when upright in level flight.
    /// </summary>
    public float PitchG { get; private set; } = 1f;

    /// <summary>
    /// Smoothed value for G in the pitch axis. Reads 1G when upright in level flight.
    /// </summary>
    public float PitchGSmoothed { get; private set; } = 1f;

    /// <summary>
    /// True when the plane has reached the stall speed and is in danger of losing altitude.
    /// Uses dynamic (flap-adjusted) stall speed, in m/s.
    /// </summary>
    public bool IsStalling => Speed < DynamicStallSpeed;

    /// <summary>
    /// Stall speed (m/s) of the aircraft taking into consideration flaps.
    /// </summary>
    public float DynamicStallSpeed { get; private set; } = 77f;
    public float AlphaDegrees { get; private set; } = 0f;
    public float BetaDegrees { get; private set; } = 0f;
    public float DynamicPressure { get; private set; } = 0f;
    public float LiftCoefficient { get; private set; } = 0f;
    public float DragCoefficient { get; private set; } = 0f;
    public float PitchEffectiveness { get; private set; } = 1f;
    public float RollEffectiveness { get; private set; } = 1f;
    public float YawEffectiveness { get; private set; } = 1f;
    public float CommandedPitchRate { get; private set; } = 0f;
    public float CommandedRollRate { get; private set; } = 0f;
    public float CommandedYawRate { get; private set; } = 0f;
    public float TargetPitchRate { get; private set; } = 0f;
    public float TargetRollRate { get; private set; } = 0f;
    public float TargetYawRate { get; private set; } = 0f;

    [Header("Debug")]
    public bool IsGrounded = false;
    public float LandedPitchAngle = 0f;
    public bool DrawAeroVectors = false;
    public float DebugForceScale = 0.0005f;

    /// <summary>
    /// Direct reference to the player aircraft. Can be null if there is no player.
    /// </summary>
    public static Aircraft Player { get; private set; } = null;

    private readonly IFlightInputProvider _playerInputProvider = new PlayerFlightInputProvider();
    private readonly IFlightInputProvider _aiInputProvider = new AIFlightInputProvider();

    private const float GroundedMaxPitchDown = 0f;
    private const float GroundedMaxPitchUp = -30f;
    private const float LandingUprightDot = 0.9f;
    private const float TakeoffSpeedFactor = 1.05f;
    private const float SweepSurfaceOffset = 0.02f;

    private Vector3 _lastPos;
    private float _liftCoefficientMax = 1f;
    private float _pitchCommandRateState = 0f;
    private float _rollCommandRateState = 0f;
    private float _yawCommandRateState = 0f;
    private Vector3 _lastLiftForce = Vector3.zero;
    private Vector3 _lastDragForce = Vector3.zero;
    private Vector3 _lastSideForce = Vector3.zero;
    private Vector3 _lastThrustForce = Vector3.zero;
    private Vector3 _lastGravityForce = Vector3.zero;

    private void Awake()
    {
        if (ApplyProfileOnAwake)
            ApplyAeroProfile();

        Velocity = transform.forward * Units.ToMetersPerSecond(StartSpeed);
        VelocityDirection = transform.forward;
        Speed = Units.ToMetersPerSecond(StartSpeed);

        if (IsPlayer)
            Player = this;

        if (StartGrounded)
        {
            // Ground clamp the plane
            var isGroundUnderneath = Physics.Raycast(
                origin: transform.position,
                direction: Vector3.down,
                hitInfo: out RaycastHit hitInfo,
                maxDistance: 10000f,
                layerMask: CollisionMask);

            if (isGroundUnderneath)
            {
                IsGrounded = true;

                Speed = 0f;
                Velocity = Vector3.zero;

                FlightInput.Throttle = 0f;
                FlightInput.GearDown = true;
                FlightInput.Flaps = true;

                Gear.ExtendState = 1f;
                Flaps.ExtendState = 1f;

                transform.position = hitInfo.point + Vector3.up * GearHeight;
                transform.forward = Vector3.Cross(transform.right, hitInfo.normal);
            }
        }

        _liftCoefficientMax = GetCurveMax(LiftCoefficientByAlpha);
        _lastPos = transform.position;
    }

    private void OnValidate()
    {
        if (ApplyProfileOnValidate)
            ApplyAeroProfile();

        _liftCoefficientMax = GetCurveMax(LiftCoefficientByAlpha);
    }

    [ContextMenu("Apply Aero Profile")]
    public void ApplyAeroProfile()
    {
        if (AeroProfile == null)
            return;

        ReferenceArea = AeroProfile.ReferenceArea;
        AirDensity = AeroProfile.AirDensity;
        SideForceSlope = AeroProfile.SideForceSlope;
        FlapLiftBonus = AeroProfile.FlapLiftBonus;
        FlapDragBonus = AeroProfile.FlapDragBonus;
        LiftCoefficientByAlpha = CloneCurveOrDefault(AeroProfile.LiftCoefficientByAlpha, LiftCoefficientByAlpha);
        DragCoefficientByAlpha = CloneCurveOrDefault(AeroProfile.DragCoefficientByAlpha, DragCoefficientByAlpha);
        PitchEffectivenessByQ = CloneCurveOrDefault(AeroProfile.PitchEffectivenessByQ, PitchEffectivenessByQ);
        RollEffectivenessByQ = CloneCurveOrDefault(AeroProfile.RollEffectivenessByQ, RollEffectivenessByQ);
        YawEffectivenessByQ = CloneCurveOrDefault(AeroProfile.YawEffectivenessByQ, YawEffectivenessByQ);
        PitchActuatorAccel = AeroProfile.PitchActuatorAccel;
        RollActuatorAccel = AeroProfile.RollActuatorAccel;
        YawActuatorAccel = AeroProfile.YawActuatorAccel;
    }

    private AnimationCurve CloneCurveOrDefault(AnimationCurve source, AnimationCurve fallback)
    {
        if (source == null || source.keys == null || source.keys.Length == 0)
            return fallback ?? new AnimationCurve(new Keyframe(0f, 1f));

        return new AnimationCurve(source.keys);
    }

    private void FixedUpdate()
    {
        if (UseFixedUpdate)
            RunFlightModel(Time.fixedDeltaTime);
    }

    private void Update()
    {
        ReadInput(Time.deltaTime);

        if (!UseFixedUpdate)
            RunFlightModel(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (Player == this)
            Player = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"UNITY Collided with {collision.collider.name}");
    }

    private void RunCollisionDetection()
    {
        // Forward-sweep collision is handled by MoveWithSweep() during movement.

        // Gear contact / landing check when upright and gear down.
        if (!IsGrounded && Gear.IsFullyExtended && transform.up.y > LandingUprightDot)
        {
            bool hitSomething = Physics.Raycast(
                origin: transform.position,
                direction: -transform.up,
                hitInfo: out RaycastHit hitInfo,
                maxDistance: GearHeight,
                layerMask: CollisionMask);

            if (hitSomething)
            {
                IsGrounded = true;

                // Get the angle from the horizon. Negative values are pitch-up in our math.
                var flattenedForward = GetFlattenedForward();
                LandedPitchAngle = -Vector3.Angle(flattenedForward, transform.forward);

                // Prevent pitch down into the ground while grounded.
                LandedPitchAngle = Mathf.Clamp(LandedPitchAngle, GroundedMaxPitchUp, GroundedMaxPitchDown);

                // Ground clamp.
                YawRate = 0f;
                RollRate = 0f;
                transform.position = hitInfo.point + Vector3.up * GearHeight;

                _lastPos = transform.position;
            }
        }
    }

    private void ReadInput(float deltaTime)
    {
        if (IsPlayer)
            _playerInputProvider.PopulateInput(this, FlightInput, deltaTime);
        else
            _aiInputProvider.PopulateInput(this, FlightInput, deltaTime);
    }

    private void RunFlightModel(float deltaTime)
    {
        Flaps.Update(FlightInput.Flaps ? 1f : 0f, deltaTime);
        Gear.Update(FlightInput.GearDown ? 1f : 0f, deltaTime);
        Brakes.Update(FlightInput.Brake ? 1f : 0f, deltaTime);

        // Stall speed is affected by flaps.
        DynamicStallSpeed = Mathf.Lerp(
            Units.ToMetersPerSecond(StallSpeedClean),
            Units.ToMetersPerSecond(StallSpeedFlaps),
            Flaps.ExtendState);

        if (IsGrounded)
        {
            RunGroundHandling(deltaTime);
        }
        else
        {
            RunFlightModelLinear(deltaTime);
            RunFlightModelRotations(deltaTime);
        }

        RunCollisionDetection();
    }

    private void RunGroundHandling(float deltaTime)
    {
        var hitSomething = Physics.Raycast(
            origin: transform.position,
            direction: -transform.up,
            hitInfo: out RaycastHit hitInfo,
            maxDistance: GearHeight * 2f,
            layerMask: CollisionMask);

        // Panic early escape if nothing under us (e.g., cliff)
        if (!hitSomething)
        {
            IsGrounded = false;
            return;
        }

        Vector3 thrustForce = CalculateThrustForce();
        Vector3 dragForce   = CalculateDragForce();
        Vector3 gravityForce = CalculateGravityForce();
        var accelerationVector = (thrustForce + dragForce + gravityForce) / Mass;

        if (accelerationVector.y <= 0f)
        {
            // Only forward acceleration while ground-clamped (no slipping)
            var forwardAccel = Vector3.Dot(transform.forward, accelerationVector);
            Speed += forwardAccel * deltaTime;

            // Wheel brakes boost
            Speed = Mathf.MoveTowards(Speed, 0f, Brakes.ExtendState * 5f * deltaTime);
            Speed = Mathf.Max(0f, Speed);

            // Stalling pitches velocity toward ground
            var stallAOA = Maths.Remap(DynamicStallSpeed, DynamicStallSpeed * 1.5f, StallAOA, 0f, Speed);

            var targetVelocityVector = transform.forward;
            targetVelocityVector = Vector3.RotateTowards(targetVelocityVector, Vector3.down, stallAOA * Mathf.Deg2Rad, 0f);

            if (targetVelocityVector.y < 0f)
            {
                targetVelocityVector.y = 0f;
                Velocity = targetVelocityVector * Speed;
                VelocityDirection = targetVelocityVector;

                MoveWithSweep(transform.position + Velocity * Scale * deltaTime);

                // Handle rotation. Re-uses a lot of the same code as the flying stuff.
                PitchG = 1f;
                PitchGSmoothed = 1f;

                // Pitching uses the same control authority as in flight to simulate aerodynamics.
                var controlAuthority = GetControlAuthority();

                // Same pitching code as when in flight, but without the stalling rotation stuff.
                var targetPitch = FlightInput.Pitch * MaxPitchRate * controlAuthority;
                var stallRate = GetStallRate() * deltaTime;
                PitchRate = SmoothDamp.Move(PitchRate + stallRate, targetPitch, PitchResponse, deltaTime);
                LandedPitchAngle += PitchRate * deltaTime;

                // Prevent pitch down into the ground while grounded. Negative is pitch-up.
                LandedPitchAngle = Mathf.Clamp(LandedPitchAngle, GroundedMaxPitchUp, GroundedMaxPitchDown);
                var pitchRotation = Quaternion.AngleAxis(LandedPitchAngle, Vector3.right);

                // Nosewheel steering: roll + yaw blend
                const float NosewheelTurnRate = 45f;
                const float NosewheelSteeringResponse = 3f;
                float nosewheelSteeringYawRate = Speed >= 5f
                    ? Mathf.InverseLerp(45f, 15f, Speed)
                    : Mathf.InverseLerp(0f, 5f, Speed);

                // Scale by gear extension so retracting removes steering authority
                float gearFactor = Gear.ExtendState;
                float maxYawRate = Mathf.Max(
                    NosewheelTurnRate * 0.1f,
                    nosewheelSteeringYawRate * NosewheelTurnRate) * gearFactor;

                var blendedYawInput = Mathf.Clamp(FlightInput.Yaw + FlightInput.Roll, -1f, 1f);
                var targetYaw = blendedYawInput * maxYawRate;

                YawRate = SmoothDamp.Move(YawRate, targetYaw, NosewheelSteeringResponse, deltaTime);
                var yawRotation = Quaternion.AngleAxis(YawRate * deltaTime, Vector3.up);

                // Align with ground normal, then apply yaw and pitch locally
                var flattenedForward = GetFlattenedForward();
                transform.rotation = Quaternion.LookRotation(flattenedForward, hitInfo.normal);
                transform.localRotation *= yawRotation * pitchRotation;
            }
            else
            {
                // Positive vertical tendency: take off!
                if (Speed > DynamicStallSpeed * TakeoffSpeedFactor)
                {
                    IsGrounded = false;
                    Debug.Log($"{name}: Took off!");
                }
            }
        }
        else
        {
            // Wants to go up => airborne
            if (Speed > DynamicStallSpeed * TakeoffSpeedFactor)
            {
                IsGrounded = false;
            }
        }
    }

    public Vector3 GetFlattenedForward()
    {
        var flat = transform.forward;
        flat.y = 0f;
        return flat.normalized;
    }

    private Vector3 CalculateThrustForce()
    {
        float thrust = FlightInput.Reheat ? ReheatThrust : FlightInput.Throttle * MilThrust;
        return transform.forward * thrust;
    }

    private Vector3 CalculateGravityForce()
    {
        return Physics.gravity * Mass;
    }

    private float GetControlAuthority()
    {
        // 0 at half stall, 1 by 2.5x stall speed
        return Mathf.InverseLerp(DynamicStallSpeed * .5f, DynamicStallSpeed * 2.5f, Speed);
    }

    private float GetStallRate()
    {
        // When stalling, the plane pitches down towards the ground.
        var stallRate = Maths.Remap(
            DynamicStallSpeed * .75f, DynamicStallSpeed * 1.25f,
            MaxPitchRate, 0f,
            Speed);

        // Decrease stall turning power as the plane faces down.
        stallRate *= 1f - Vector3.Dot(transform.forward, Vector3.down);
        return stallRate;
    }

    private Vector3 CalculateDragForce()
    {
        // Base quadratic drag
        float linearDrag = Mathf.Pow(Speed, 2f) * Drag;
        float totalDrag = linearDrag;

        // Extending things from the plane increases drag.
        if (Brakes.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Brakes.DragMultiplier * Brakes.ExtendState;
        if (Gear.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Gear.DragMultiplier * Gear.ExtendState;
        if (Flaps.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Flaps.DragMultiplier * Flaps.ExtendState;

        var linearDragForce = -transform.forward * totalDrag;

        // Induced drag: use sin^2(AOA) for smoother growth
        var aoaRad = Vector3.Angle(transform.forward, VelocityDirection) * Mathf.Deg2Rad;
        var induced = Mathf.Pow(Mathf.Sin(aoaRad), 2f); // 0..1
        Vector3 inducedDragForce = -transform.forward * Mathf.Pow(Speed, 2f) * InducedDrag * induced;

        return linearDragForce + inducedDragForce;
    }

    private float CalculateDynamicPressure()
    {
        DynamicPressure = 0.5f * AirDensity * Speed * Speed;
        return DynamicPressure;
    }

    private void UpdateAerodynamicAngles()
    {
        if (Speed <= Mathf.Epsilon)
        {
            AlphaDegrees = 0f;
            BetaDegrees = 0f;
            return;
        }

        Vector3 localVelocityDirection = transform.InverseTransformDirection(VelocityDirection).normalized;

        // Positive alpha means nose above the velocity vector.
        AlphaDegrees = Mathf.Atan2(-localVelocityDirection.y, Mathf.Max(0.0001f, localVelocityDirection.z)) * Mathf.Rad2Deg;

        // Positive beta means velocity coming from aircraft right side.
        BetaDegrees = Mathf.Atan2(localVelocityDirection.x, Mathf.Max(0.0001f, localVelocityDirection.z)) * Mathf.Rad2Deg;
    }

    private float CalculateLiftCoefficient()
    {
        float cl = EvaluateCurveSafe(LiftCoefficientByAlpha, AlphaDegrees, 0.2f);
        cl += FlapLiftBonus * Flaps.ExtendState;
        LiftCoefficient = cl;
        return cl;
    }

    private float CalculateDragCoefficient()
    {
        float cd = EvaluateCurveSafe(DragCoefficientByAlpha, Mathf.Abs(AlphaDegrees), 0.02f);
        cd += FlapDragBonus * Flaps.ExtendState;
        cd = Mathf.Max(0.001f, cd);
        DragCoefficient = cd;
        return cd;
    }

    private void UpdateControlEffectiveness()
    {
        PitchEffectiveness = Mathf.Clamp(EvaluateCurveSafe(PitchEffectivenessByQ, DynamicPressure, 1f), 0f, 1.5f);
        RollEffectiveness = Mathf.Clamp(EvaluateCurveSafe(RollEffectivenessByQ, DynamicPressure, 1f), 0f, 1.5f);
        YawEffectiveness = Mathf.Clamp(EvaluateCurveSafe(YawEffectivenessByQ, DynamicPressure, 1f), 0f, 1.5f);
    }

    private Vector3 CalculateAerodynamicForce()
    {
        if (Speed <= Mathf.Epsilon)
            return Vector3.zero;

        UpdateAerodynamicAngles();

        float dynamicPressure = CalculateDynamicPressure();
        float cl = CalculateLiftCoefficient();
        float cd = CalculateDragCoefficient();
        float betaRad = BetaDegrees * Mathf.Deg2Rad;

        Vector3 velocityDir = VelocityDirection.normalized;
        Vector3 liftDir = Vector3.Cross(velocityDir, transform.right);
        if (liftDir.sqrMagnitude <= Mathf.Epsilon)
            liftDir = transform.up;
        liftDir.Normalize();

        float liftMagnitude = dynamicPressure * ReferenceArea * cl;
        float dragMagnitude = dynamicPressure * ReferenceArea * cd;
        float sideMagnitude = dynamicPressure * ReferenceArea * SideForceSlope * betaRad;

        Vector3 liftForce = liftDir * liftMagnitude;
        Vector3 dragForce = -velocityDir * dragMagnitude;
        Vector3 sideForce = -transform.right * sideMagnitude;

        // Keep extension-induced drag behavior for gear/brakes/flaps.
        float extensionDragScale = 1f;
        extensionDragScale += Brakes.DragMultiplier * Brakes.ExtendState;
        extensionDragScale += Gear.DragMultiplier * Gear.ExtendState;
        extensionDragScale += Flaps.DragMultiplier * Flaps.ExtendState;
        dragForce *= extensionDragScale;

        _lastLiftForce = liftForce;
        _lastDragForce = dragForce;
        _lastSideForce = sideForce;

        return liftForce + dragForce + sideForce;
    }

    private float GetCurveMax(AnimationCurve curve)
    {
        if (curve == null || curve.keys == null || curve.keys.Length == 0)
            return 1f;

        float max = curve.keys[0].value;
        for (int i = 1; i < curve.keys.Length; i++)
            max = Mathf.Max(max, curve.keys[i].value);

        return max;
    }

    private float EvaluateCurveSafe(AnimationCurve curve, float input, float fallback)
    {
        if (curve == null || curve.keys == null || curve.keys.Length == 0)
            return fallback;

        return curve.Evaluate(input);
    }

    private float GetPredictivePitchRateLimit(float controlAuthority)
    {
        if (Speed <= 1f)
            return MaxPitchRate * controlAuthority;

        float gravityMagnitude = Mathf.Abs(Physics.gravity.y);
        float dynamicPressure = CalculateDynamicPressure();
        float clMax = Mathf.Max(0.05f, _liftCoefficientMax + FlapLiftBonus * Flaps.ExtendState);
        float maxLift = dynamicPressure * ReferenceArea * clMax;
        float achievableG = maxLift / Mathf.Max(1f, Mass * gravityMagnitude);
        float cappedG = Mathf.Clamp(achievableG, 0.2f, MaxG);

        float maxPitchRateRad = cappedG * gravityMagnitude / Mathf.Max(5f, Speed);
        float maxPitchRateDeg = Mathf.Rad2Deg * maxPitchRateRad;
        return Mathf.Clamp(maxPitchRateDeg, 2f, MaxPitchRate) * controlAuthority;
    }

    private void RunFlightModelLinear(float deltaTime)
    {
        // Forces integrated on the full velocity vector for better alpha/beta behavior.
        Vector3 gravityForce = CalculateGravityForce();
        Vector3 thrustForce = CalculateThrustForce();
        Vector3 aerodynamicForce = CalculateAerodynamicForce();
        _lastGravityForce = gravityForce;
        _lastThrustForce = thrustForce;
        Vector3 acceleration = (gravityForce + thrustForce + aerodynamicForce) / Mass;

        Velocity += acceleration * deltaTime;
        Speed = Velocity.magnitude;

        if (Speed > Mathf.Epsilon)
        {
            var rawVelocityDirection = Velocity / Speed;
            VelocityDirection = SmoothDamp.Move(
                VelocityDirection,
                rawVelocityDirection,
                Responsiveness,
                deltaTime);
            Velocity = VelocityDirection * Speed;
        }
        else
        {
            Speed = 0f;
            VelocityDirection = transform.forward;
            Velocity = Vector3.zero;
        }

        MoveWithSweep(transform.position + Velocity * Scale * deltaTime);
    }

    private void RunFlightModelRotations(float deltaTime)
    {
        PitchG = Maths.CalculatePitchG(transform, Velocity, PitchRate);
        PitchGSmoothed = SmoothDamp.Move(PitchGSmoothed, PitchG, 3f, deltaTime);

        var controlAuthority = GetControlAuthority();
        UpdateControlEffectiveness();

        // Predictive pitch limiter based on available lift and airspeed.
        float positivePitchLimit = GetPredictivePitchRateLimit(controlAuthority * PitchEffectiveness);
        float negativePitchLimit = positivePitchLimit * (MinG / Mathf.Max(0.001f, MaxG));
        var commandedPitch = FlightInput.Pitch * MaxPitchRate * controlAuthority * PitchEffectiveness;
        commandedPitch = Mathf.Clamp(commandedPitch, -negativePitchLimit, positivePitchLimit);
        _pitchCommandRateState = Mathf.MoveTowards(_pitchCommandRateState, commandedPitch, PitchActuatorAccel * deltaTime);
        var targetPitch = _pitchCommandRateState - PitchRate * PitchDamping;
        CommandedPitchRate = _pitchCommandRateState;
        TargetPitchRate = targetPitch;
        PitchRate = SmoothDamp.Move(PitchRate, targetPitch, PitchResponse, deltaTime);
        var pitchRotation = Quaternion.AngleAxis(PitchRate * deltaTime, Vector3.right);

        // Yaw
        var commandedYaw = FlightInput.Yaw * MaxYawRate * controlAuthority * YawEffectiveness;
        _yawCommandRateState = Mathf.MoveTowards(_yawCommandRateState, commandedYaw, YawActuatorAccel * deltaTime);
        var targetYaw = _yawCommandRateState;
        targetYaw -= (YawRate + BetaDegrees * 0.1f) * YawDamping;
        CommandedYawRate = _yawCommandRateState;
        TargetYawRate = targetYaw;
        YawRate = SmoothDamp.Move(YawRate, targetYaw, YawResponse, deltaTime);
        var yawRotation = Quaternion.AngleAxis(YawRate * deltaTime, Vector3.up);

        // Roll (note the negative sign to match original bank convention)
        var commandedRoll = FlightInput.Roll * MaxRollRate * controlAuthority * RollEffectiveness;
        _rollCommandRateState = Mathf.MoveTowards(_rollCommandRateState, commandedRoll, RollActuatorAccel * deltaTime);
        var targetRoll = _rollCommandRateState;
        targetRoll -= RollRate * RollDamping;
        CommandedRollRate = _rollCommandRateState;
        TargetRollRate = targetRoll;
        RollRate = SmoothDamp.Move(RollRate, targetRoll, RollResponse, deltaTime);
        var rollRotation = Quaternion.AngleAxis(-RollRate * deltaTime, Vector3.forward);

        transform.localRotation *= pitchRotation * rollRotation * yawRotation;

        // Stall rotation nose-down
        var stallRate = GetStallRate();
        if (stallRate > 0f)
        {
            var stallAxis = Vector3.Cross(transform.forward, Vector3.down);
            transform.rotation = Quaternion.AngleAxis(stallRate * deltaTime, stallAxis) * transform.rotation;
        }
    }

    private void MoveWithSweep(Vector3 desiredPosition)
    {
        if (TrySweepBetween(_lastPos, desiredPosition, out RaycastHit hitInfo, out Vector3 resolvedPosition))
        {
            Debug.Log($"{name}: Collided with {hitInfo.collider.name}!");

            // Reflect velocity direction and keep heading synchronized with post-collision velocity.
            VelocityDirection = Vector3.Reflect(VelocityDirection, hitInfo.normal).normalized;
            Velocity = VelocityDirection * Speed;
            transform.forward = VelocityDirection;
        }

        transform.position = resolvedPosition;
        _lastPos = transform.position;
    }

    private bool TrySweepBetween(Vector3 from, Vector3 to, out RaycastHit hitInfo, out Vector3 resolvedPosition)
    {
        var delta = to - from;
        var dist = delta.magnitude;
        if (dist <= Mathf.Epsilon)
        {
            hitInfo = default;
            resolvedPosition = to;
            return false;
        }

        var dir = delta / dist;
        if (Physics.Raycast(from, dir, out hitInfo, dist, CollisionMask))
        {
            // Small offset avoids immediately re-hitting the same surface due to numerical precision.
            resolvedPosition = hitInfo.point + hitInfo.normal * SweepSurfaceOffset;
            return true;
        }

        resolvedPosition = to;
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw capped velocity direction and diagnostics.
        Vector3 dir = (Velocity.sqrMagnitude > 0.001f) ? Velocity.normalized : transform.forward;
        Debug.DrawLine(transform.position, transform.position + dir * 50f, Color.red);
        if (DrawAeroVectors)
        {
            Debug.DrawLine(transform.position, transform.position + _lastLiftForce * DebugForceScale, Color.green);
            Debug.DrawLine(transform.position, transform.position + _lastDragForce * DebugForceScale, Color.yellow);
            Debug.DrawLine(transform.position, transform.position + _lastSideForce * DebugForceScale, Color.cyan);
            Debug.DrawLine(transform.position, transform.position + _lastThrustForce * DebugForceScale, Color.magenta);
            Debug.DrawLine(transform.position, transform.position + _lastGravityForce * DebugForceScale, Color.white);
        }

        UnityEditor.Handles.Label(
            transform.position + dir * 55f,
            $"IAS {Units.ToKnots(Speed):0} kt\nAOA {AlphaDegrees:0.0} deg  BETA {BetaDegrees:0.0} deg\nQ {DynamicPressure:0} Pa  CL {LiftCoefficient:0.00}  CD {DragCoefficient:0.00}\nCTRL P:{PitchEffectiveness:0.00} R:{RollEffectiveness:0.00} Y:{YawEffectiveness:0.00}\nCMD/ACT P:{CommandedPitchRate:0}/{PitchRate:0}  R:{CommandedRollRate:0}/{RollRate:0}  Y:{CommandedYawRate:0}/{YawRate:0}\nG {PitchG:0.0}");
    }
#endif
}



