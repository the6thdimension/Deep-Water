using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetSelection : MonoBehaviour
{
    private Transform target;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Cast a ray from the mouse position into the scene
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Check if the ray hits a targetable object
                if (hit.collider.CompareTag("Target"))
                {
                    // Set the target of the rocket to the hit object's transform
                    target = hit.collider.transform;
                }
            }
        }
    }

    // Get the current target of the rocket
    public Transform GetTarget()
    {
        return target;
    }
}






// }
// In this script, we use the Update method to check for user input. When the left mouse button is pressed, we cast a ray from the mouse position into the scene using Physics.Raycast. If the ray hits an object with a tag "Target", we set the target variable to the transform of the hit object.

// We also provide a public method GetTarget to access the current target of the rocket from other scripts. This can be used by the rocket's navigation system to determine the destination.

// To use this script, create an empty GameObject in Unity and attach the TargetSelection script to it. Additionally, make sure to assign the "Target" tag to any objects in the scene that should be selectable targets for the rocket