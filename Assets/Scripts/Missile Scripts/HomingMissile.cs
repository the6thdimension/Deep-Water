using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomingMissile : MonoBehaviour
{
    public Transform target;
    public float speed = 20f;
    public float rotationSpeed = 2f;
    public float fuelLimit = 10f; // Time in seconds
    public float ttl = 15f; // Time-to-live in seconds
    public float radarRange = 50f; // Range of radar acquisition
    public float infraredRange = 10f; // Range for infrared seeking

    private Rigidbody rb;
    private float fuelRemaining;
    private float timeSinceLaunch;
    private bool targetAcquired = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        fuelRemaining = fuelLimit;
        timeSinceLaunch = 0f;
    }

    void Update()
    {
        timeSinceLaunch += Time.deltaTime;
        fuelRemaining -= Time.deltaTime;

        if (timeSinceLaunch > ttl || fuelRemaining <= 0)
        {
            Destroy(gameObject); // Destroy missile due to TTL expiry or fuel depletion
        }

        if (!targetAcquired)
        {
            RadarTargetAcquisition();
        }
        else
        {
            if (target != null && Vector3.Distance(transform.position, target.position) <= infraredRange)
            {
                InfraredSeeking();
            }
            else
            {
                LeadPursuitGuidance();
            }
        }
    }

    void RadarTargetAcquisition()
    {
        // Implement radar acquisition logic here
        // For example, find the nearest target within radarRange
        // Set targetAcquired to true once a target is locked
    }

    void InfraredSeeking()
    {
        // Implement infrared seeking behavior when close to the target
        // This could involve adjusting the missile's path more aggressively
    }

    void LeadPursuitGuidance()
    {
        if (target == null) return;

        // Lead pursuit calculations
        Vector3 targetDir = target.position - transform.position;
        Vector3 futurePosition = target.position + targetDir.normalized * speed * Time.deltaTime;
        Vector3 desiredDirection = (futurePosition - transform.position).normalized;

        // Apply rotation towards the target
        Quaternion rotation = Quaternion.LookRotation(desiredDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);

        rb.linearVelocity = transform.forward * speed;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.transform == target)
        {
            Debug.Log("Target hit!");
            Destroy(target.gameObject); // Destroy the target
            Destroy(gameObject); // Destroy the missile
        }
    }
}
