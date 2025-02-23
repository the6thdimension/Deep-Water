using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MissileController : MonoBehaviour
{
    [Header("Default Configuration")]
    public float defaultThrust = 40000f;
    public float defaultSteeringSpeed = 40f;
    public float defaultTrackingDistance = 40f;

    [Header("Runtime Properties")]
    public Transform target;
    
    private Rigidbody rb;
    private bool isLaunched = false;
    private Vector3 launchPosition;
    private MissileConfiguration configuration;
    private float launchTime;
    private AnimationCurve flightProfile;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
    }

    public void Configure(MissileConfiguration config)
    {
        configuration = config;
        flightProfile = config.FlightProfile ?? AnimationCurve.Linear(0, 0, 1, 1);
    }

    public void Launch(Transform launchTarget)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        target = launchTarget;
        rb.isKinematic = false;
        isLaunched = true;
        launchPosition = transform.position;
        launchTime = Time.time;

        // Use configuration values if available, otherwise use defaults
        float thrust = configuration?.Speed ?? defaultThrust;
        rb.linearVelocity = transform.up * thrust;
    }

    private void FixedUpdate()
    {
        if (!isLaunched) return;

        float timeSinceLaunch = Time.time - launchTime;
        float distanceTraveled = Vector3.Distance(transform.position, launchPosition);
        float trackingDistance = configuration?.MaxRange * 0.1f ?? defaultTrackingDistance;
        float thrust = configuration?.Speed ?? defaultThrust;
        float turnRate = configuration?.TurnRate ?? defaultSteeringSpeed;

        // Initial boost phase
        if (distanceTraveled <= trackingDistance)
        {
            // Apply thrust based on flight profile
            float profileTime = Mathf.Clamp01(distanceTraveled / trackingDistance);
            float thrustMultiplier = flightProfile.Evaluate(profileTime);
            rb.AddForce(transform.up * thrust * thrustMultiplier * Time.deltaTime);
        }
        // Tracking phase
        else if (target != null)
        {
            Vector3 targetDirection = (target.position - transform.position).normalized;
            Vector3 currentDirection = rb.linearVelocity.normalized;

            // Calculate rotation to target
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
            Quaternion currentRotation = transform.rotation;

            // Smoothly rotate towards target
            transform.rotation = Quaternion.RotateTowards(
                currentRotation,
                targetRotation,
                turnRate * Time.deltaTime
            );

            // Apply thrust in new direction
            rb.linearVelocity = transform.forward * thrust;
        }

        // Update missile orientation to match velocity
        if (rb.linearVelocity.sqrMagnitude > 1f)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform == target)
        {
            HandleImpact();
        }
    }

    private void HandleImpact()
    {
        // TODO: Add impact effects, damage calculation, etc.
        Destroy(gameObject);
    }
}
