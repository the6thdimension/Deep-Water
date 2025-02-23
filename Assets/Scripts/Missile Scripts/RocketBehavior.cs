using System.Collections;
using System.Collections.Generic;


using UnityEngine;

public class RocketBehavior : MonoBehaviour
{
    public Transform target; // The target or destination for the rocket

    public float speed = 10f; // The speed of the rocket

    private void Update()
    {
        // Check if there is a valid target
        if (target != null)
        {
            // Calculate the direction towards the target
            Vector3 direction = (target.position - transform.position).normalized;

            // Move the rocket towards the target
            transform.position += direction * speed * Time.deltaTime;

            // Rotate the rocket to face the direction of movement
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}



// Explanation:

// The RocketBehavior script is responsible for the self-guided behavior of the rocket in Unity.
// It uses a Transform variable named target to store the position of the target or destination for the rocket.
// The speed variable controls the movement speed of the rocket.
// In the Update method, the script checks if there is a valid target.
// If there is a target, it calculates the direction towards the target using (target.position - transform.position).normalized.
// The rocket then moves towards the target by updating its position using transform.position += direction * speed * Time.deltaTime.
// Lastly, it rotates the rocket to face the direction of movement using transform.rotation = Quaternion.LookRotation(direction).