using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ESSMHomingMissile6DoF : MonoBehaviour
{
    [Header("Targeting")]
    public Transform Target;
    [Min(100f)] public float SeekerRange = 50000f;
    [Range(1f, 180f)] public float SeekerFovDeg = 60f;
    [Range(1f, 80f)] public float NavigationConstant = 3.5f;
    [Min(1f)] public float MaxLateralAccel = 40f;
    public LayerMask SeekerOcclusionMask = ~0;
    public bool RequireLineOfSight = false;

    [Header("Propulsion")]
    [Min(0f)] public float BoostThrust = 140000f;
    [Min(0f)] public float BoostDuration = 1.4f;
    [Min(0f)] public float SustainThrust = 18000f;
    [Min(0f)] public float SustainDuration = 7f;
    [Min(0f)] public float LifeTime = 20f;

    [Header("Aerodynamics")]
    [Min(0.01f)] public float ReferenceArea = 0.065f;
    [Min(0.1f)] public float AirDensity = 1.225f;
    [Min(0f)] public float BaseDragCoefficient = 0.18f;
    [Min(0f)] public float AlphaDragScale = 1.8f;
    [Min(0f)] public float LiftSlope = 3.5f;
    [Min(0f)] public float SideForceSlope = 2.5f;

    [Header("Control Moments")]
    [Min(0f)] public float PitchMoment = 4500f;
    [Min(0f)] public float YawMoment = 4500f;
    [Min(0f)] public float RollDamping = 1800f;
    [Min(0f)] public float PitchDamping = 1200f;
    [Min(0f)] public float YawDamping = 1200f;

    [Header("Fuze / Impact")]
    [Min(0f)] public float ProximityFuzeRadius = 7f;
    public LayerMask ImpactMask = ~0;
    public float ImpactRaySafety = 1.2f;
    public GameObject ExplosionPrefab;

    [Header("Debug")]
    public bool DrawDebug = false;

    public bool HasLock { get; private set; }
    public float TimeSinceLaunch { get; private set; }

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        TimeSinceLaunch += dt;

        if (TimeSinceLaunch > LifeTime)
        {
            Detonate();
            return;
        }

        UpdateLockState();

        ApplyPropulsion();
        ApplyAerodynamics();
        ApplyGuidanceAndControl(dt);
        CheckFuzeAndImpact(dt);
    }

    public void SetTarget(Transform newTarget)
    {
        Target = newTarget;
    }

    private void UpdateLockState()
    {
        HasLock = false;
        if (Target == null)
            return;

        Vector3 toTarget = Target.position - transform.position;
        float distance = toTarget.magnitude;
        if (distance > SeekerRange || distance < Mathf.Epsilon)
            return;

        float offBoresight = Vector3.Angle(transform.forward, toTarget);
        if (offBoresight > SeekerFovDeg * 0.5f)
            return;

        if (RequireLineOfSight)
        {
            if (Physics.Raycast(transform.position, toTarget.normalized, out RaycastHit hit, distance, SeekerOcclusionMask))
            {
                if (hit.transform != Target && hit.transform.root != Target.root)
                    return;
            }
        }

        HasLock = true;
    }

    private void ApplyPropulsion()
    {
        float thrust = 0f;
        if (TimeSinceLaunch <= BoostDuration)
        {
            thrust = BoostThrust;
        }
        else if (TimeSinceLaunch <= BoostDuration + SustainDuration)
        {
            thrust = SustainThrust;
        }

        if (thrust > 0f)
            _rb.AddForce(transform.forward * thrust, ForceMode.Force);
    }

    private void ApplyAerodynamics()
    {
        float speed = _rb.linearVelocity.magnitude;
        if (speed < 0.5f)
            return;

        Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
        float alpha = Mathf.Atan2(-localVel.y, Mathf.Max(0.1f, localVel.z)); // pitch AOA (rad)
        float beta = Mathf.Atan2(localVel.x, Mathf.Max(0.1f, localVel.z));   // sideslip (rad)

        float q = 0.5f * AirDensity * speed * speed;
        float cd = BaseDragCoefficient + AlphaDragScale * (alpha * alpha + beta * beta);

        float drag = q * ReferenceArea * cd;
        float liftY = q * ReferenceArea * LiftSlope * alpha;
        float sideX = -q * ReferenceArea * SideForceSlope * beta;

        Vector3 aeroForceBody = new Vector3(sideX, liftY, -drag);
        _rb.AddForce(transform.TransformDirection(aeroForceBody), ForceMode.Force);
    }

    private void ApplyGuidanceAndControl(float dt)
    {
        Vector3 localAngVel = transform.InverseTransformDirection(_rb.angularVelocity);

        float pitchCmd = 0f;
        float yawCmd = 0f;

        if (HasLock && Target != null)
        {
            Vector3 relPos = Target.position - transform.position;
            Vector3 targetVel = Vector3.zero;
            var trgRb = Target.GetComponentInParent<Rigidbody>();
            if (trgRb != null)
                targetVel = trgRb.linearVelocity;

            Vector3 relVel = targetVel - _rb.linearVelocity;
            float r2 = Mathf.Max(1f, relPos.sqrMagnitude);

            // PN: a_cmd = N * |Vm| * (omega_LOS x v_hat_m)
            Vector3 losRate = Vector3.Cross(relPos, relVel) / r2;
            Vector3 velDir = _rb.linearVelocity.sqrMagnitude > 1f ? _rb.linearVelocity.normalized : transform.forward;
            Vector3 cmdAccelWorld = NavigationConstant * _rb.linearVelocity.magnitude * Vector3.Cross(losRate, velDir);

            Vector3 cmdAccelBody = transform.InverseTransformDirection(cmdAccelWorld);
            pitchCmd = Mathf.Clamp(cmdAccelBody.y / MaxLateralAccel, -1f, 1f);
            yawCmd = Mathf.Clamp(cmdAccelBody.x / MaxLateralAccel, -1f, 1f);
        }

        Vector3 controlTorqueBody = new Vector3(
            pitchCmd * PitchMoment - localAngVel.x * PitchDamping,
            yawCmd * YawMoment - localAngVel.y * YawDamping,
            -localAngVel.z * RollDamping
        );

        _rb.AddTorque(transform.TransformDirection(controlTorqueBody), ForceMode.Force);
    }

    private void CheckFuzeAndImpact(float dt)
    {
        if (HasLock && Target != null)
        {
            float distance = Vector3.Distance(transform.position, Target.position);
            if (distance <= ProximityFuzeRadius)
            {
                Detonate();
                return;
            }
        }

        float rayDistance = Mathf.Max(1f, _rb.linearVelocity.magnitude * dt * ImpactRaySafety);
        if (Physics.Raycast(transform.position, _rb.linearVelocity.normalized, out RaycastHit hit, rayDistance, ImpactMask))
        {
            if (Target == null || hit.transform == Target || hit.transform.root == Target.root || hit.distance <= 1f)
            {
                Detonate();
            }
        }
    }

    private void Detonate()
    {
        if (ExplosionPrefab != null)
            Instantiate(ExplosionPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (!DrawDebug)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, ProximityFuzeRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 20f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Min(SeekerRange, 200f));
    }
}
