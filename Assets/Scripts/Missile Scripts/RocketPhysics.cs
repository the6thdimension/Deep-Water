using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class RocketPhysics : MonoBehaviour
{
    public float dragCoefficient; // drag coefficient of the rocket
    public float velocity; // velocity of the rocket

    // Function to calculate the drag force
    public float CalculateDragForce()
    {
        // Formula to calculate the drag force: dragForce = 0.5 * dragCoefficient * velocity^2
        float dragForce = 0.5f * dragCoefficient * Mathf.Pow(velocity, 2);
        return dragForce;
    }
}



// Explanation:

// The RocketPhysics script is a MonoBehaviour script in Unity.
// It has two public variables: dragCoefficient (to store the drag coefficient of the rocket) and velocity (to store the velocity of the rocket).
// The CalculateDragForce function calculates the drag force applied to the rocket based on its velocity and drag coefficient.
// The formula used to calculate the drag force is: dragForce = 0.5 * dragCoefficient * velocity^2.
// The Mathf.Pow function is used to calculate the square of the velocity.