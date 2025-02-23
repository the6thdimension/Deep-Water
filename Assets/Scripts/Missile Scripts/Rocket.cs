using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rocket : MonoBehaviour
{
    // Adjustable rocket parameters
    public float thrust = 100f; // The force applied to the rocket
    public float rotationSpeed = 10f; // The speed at which the rocket rotates
    public float maxFuel = 100f; // The maximum fuel capacity of the rocket
    public float fuelConsumptionRate = 1f; // The rate at which the rocket consumes fuel

    private float currentFuel; // The current fuel level of the rocket

    // Start is called before the first frame update
    void Start()
    {
        currentFuel = maxFuel; // Set the current fuel level to the maximum fuel capacity
    }

    // Update is called once per frame
    void Update()
    {
        if (currentFuel > 0f) // If there is still fuel left
        {
            // Handle user input for rocket movement
            float rotation = Input.GetAxis("Horizontal");
            float throttle = Input.GetAxis("Vertical");

            // Rotate the rocket
            transform.Rotate(Vector3.forward * rotation * rotationSpeed * Time.deltaTime);

            // Apply thrust to the rocket
            GetComponent<Rigidbody2D>().AddForce(transform.up * throttle * thrust * Time.deltaTime);

            // Consume fuel based on throttle input
            currentFuel -= fuelConsumptionRate * Mathf.Abs(throttle) * Time.deltaTime;
        }
    }
}



// Explanation:

// This code defines a Rocket class that extends MonoBehaviour, which is the base class for all Unity scripts. It includes adjustable rocket parameters such as thrust, rotationSpeed, maxFuel, and fuelConsumptionRate.

// The Start() method initializes the currentFuel variable to the maximum fuel capacity specified. This method is called before the first frame update.

// The Update() method is called once per frame and handles user input for rocket movement. It first checks if there is still fuel left. If so, it retrieves the user input for rotation and throttle. It then rotates the rocket using transform.Rotate() and applies thrust to the rocket using GetComponent<Rigidbody2D>().AddForce().

// The fuel is consumed based on the throttle input using the currentFuel variable and the fuelConsumptionRate. The fuel is reduced by the product of fuelConsumptionRate, the absolute value of throttle, and Time.deltaTime to account for the time passed
