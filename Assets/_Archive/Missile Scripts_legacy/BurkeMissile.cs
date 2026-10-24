using UnityEngine;
using DeepWater.Missiles;

[RequireComponent(typeof(Rigidbody))]
public class BurkeMissile : MonoBehaviour
{
    [Header("Flight Profile")]
    [Tooltip("Vertical-only boost phase. Missile flies along its initial up direction with no homing.")]
    [Min(0f)] public float BoostDuration = 1.5f;
    [Tooltip("Sustain phase: missile pitches over toward target while thrust continues.")]
    [Min(0f)] public float SustainDuration = 6.5f;
    [Tooltip("Turn rate during coast (post-burnout). Usually lower than the powered turn rate.")]
    [Min(0f)] public float CoastTurnRateDegPerSec = 20f;

    [Header("Terminal")]
    [Tooltip("Detonate when within this distance of the target.")]
    [Min(0.1f)] public float ProximityFuzeMeters = 30f;
    [Tooltip("Maximum mission time before self-destruct.")]
    [Min(1f)] public float MaxFlightSeconds = 60f;
    [Tooltip("Self-destruct if altitude drops below this world Y value.")]
    public float MinWorldYBeforeSelfDestruct = -50f;

    [Header("Optional VFX")]
    public GameObject ImpactVfxPrefab;
    [Min(0f)] public float ImpactVfxLifetimeSeconds = 3f;

    private enum Phase { Boost, Sustain, Coast }

    private Rigidbody _rb;
    private Transform _target;
    private MissileData _data;
    private Phase _phase;
    private float _elapsed;
    private float _maxSpeed;
    private float _thrust;
    private float _maxTurnRate;
    private float _blastRadius;
    private bool _initialized;

    public void Initialize(MissileData data)
    {
        _data = data;
        _maxSpeed = data.maxSpeed;
        _thrust = data.thrust;
        _maxTurnRate = data.maxTurnRate;
        _blastRadius = Mathf.Max(0.1f, data.blastRadius);
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = true;
        _elapsed = 0f;
        _phase = Phase.Boost;
        _initialized = true;
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    private void FixedUpdate()
    {
        if (!_initialized || _rb == null)
            return;

        float dt = Time.fixedDeltaTime;
        _elapsed += dt;

        if (_phase == Phase.Boost && _elapsed >= BoostDuration)
            _phase = Phase.Sustain;
        if (_phase == Phase.Sustain && _elapsed >= BoostDuration + SustainDuration)
            _phase = Phase.Coast;

        bool isPowered = _phase != Phase.Coast;
        bool isHoming = _phase != Phase.Boost && _target != null;

        if (isHoming)
        {
            Vector3 toTarget = _target.position - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                float turnRate = isPowered ? _maxTurnRate : CoastTurnRateDegPerSec;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnRate * dt);
            }
        }

        if (isPowered)
        {
            _rb.AddForce(transform.forward * _thrust, ForceMode.Force);
        }

        if (_rb.linearVelocity.magnitude > _maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _maxSpeed;

        if (_target != null)
        {
            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist <= ProximityFuzeMeters)
            {
                Detonate(_target.position);
                return;
            }
        }

        if (_elapsed >= MaxFlightSeconds || transform.position.y < MinWorldYBeforeSelfDestruct)
        {
            Destroy(gameObject);
        }
    }

    private void Detonate(Vector3 at)
    {
        if (ImpactVfxPrefab != null)
        {
            GameObject vfx = Instantiate(ImpactVfxPrefab, at, Quaternion.identity);
            if (ImpactVfxLifetimeSeconds > 0f)
                Destroy(vfx, ImpactVfxLifetimeSeconds);
        }

        if (_target != null && Vector3.Distance(transform.position, _target.position) <= _blastRadius)
        {
            Destroy(_target.gameObject);
        }

        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_initialized)
            return;
        Detonate(collision.GetContact(0).point);
    }
}
