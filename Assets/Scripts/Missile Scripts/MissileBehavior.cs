using UnityEngine;
using DeepWater.Missiles;

public class MissileBehavior : MonoBehaviour
{
    private float maxSpeed;
    private float thrust;
    private float burnTime;
    private float maxTurnRate;
    private float lockOnRange;
    private float blastRadius;
    private float damage;
    private Rigidbody rb;
    private float timeSinceLaunch = 0f;

    private Transform target;
    private float guidanceStrength = 5.0f; // Adjust for how aggressively the missile should turn

    public void Initialize(MissileData data)
    {
        maxSpeed = data.maxSpeed;
        thrust = data.thrust;
        burnTime = data.burnTime;
        maxTurnRate = data.maxTurnRate;
        lockOnRange = data.lockOnRange;
        blastRadius = data.blastRadius;
        damage = data.damage;
        rb = GetComponent<Rigidbody>();
        // Additional initialization logic can be added here
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void FixedUpdate()
    {
        if (target != null)
        {
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            Vector3 newDirection = Vector3.RotateTowards(transform.forward, directionToTarget, maxTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            transform.rotation = Quaternion.LookRotation(newDirection);
        }

        // Continue existing thrust and speed logic
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        if (timeSinceLaunch < burnTime)
            rb.AddForce(transform.forward * thrust, ForceMode.Force);

        timeSinceLaunch += Time.fixedDeltaTime;

        // Impact detection
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, rb.linearVelocity.magnitude * Time.fixedDeltaTime))
        {
            // Handle impact
            Debug.Log("Missile hit " + hit.collider.name);
            Destroy(gameObject); // Destroy missile on impact
        }
    }

    // Additional methods for missile behavior can be added here
}
