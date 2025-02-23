using UnityEngine;
using DeepWater.Missiles;

public class Missile : MonoBehaviour
{
    private float maxSpeed;
    private float thrust;
    private float burnTime;
    private float timeSinceLaunch;
    
    private float maxTurnRate; // degrees/sec
    private float lockOnRange;
    private float blastRadius;
    private float damage;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void InitializeMissile(MissileData data)
    {
        maxSpeed = data.maxSpeed;
        thrust = data.thrust;
        burnTime = data.burnTime;
        maxTurnRate = data.maxTurnRate;
        lockOnRange = data.lockOnRange;
        blastRadius = data.blastRadius;
        damage = data.damage;
        // etc. ...
    }

    void FixedUpdate()
    {
        timeSinceLaunch += Time.fixedDeltaTime;

        // Simple thrust model
        if(timeSinceLaunch < burnTime)
        {
            rb.AddForce(transform.forward * thrust, ForceMode.Force);
        }

        // Cap speed if you don’t want it to exceed maxSpeed
        if(rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        // Guidance logic (e.g. tracking a target) would go here:
        // ...
    }
}
