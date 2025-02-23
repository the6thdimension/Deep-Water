using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileGizmos : MonoBehaviour
{
    public float lineLength = 10f;
    private MissileController missileController;

    void Start()
    {
        missileController = GetComponent<MissileController>();
    }

    void OnDrawGizmos()
    {
        if (missileController != null && missileController.target != null)
        {
            // Red line towards target
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + (missileController.target.position - transform.position).normalized * lineLength);

            // White line representing velocity
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, transform.position + GetComponent<Rigidbody>().linearVelocity);
        }
    }
}

