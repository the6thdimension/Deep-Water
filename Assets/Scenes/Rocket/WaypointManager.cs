using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaypointManager : MonoBehaviour
{
    public GameObject waypointPrefab;  // Prefab for waypoint objects
    public Transform waypointContainer;  // Parent object to keep the hierarchy organized
    public FighterJetController fighterJetController;  // Reference to the fighter jet controller

    void Update()
    {
        if (Input.GetMouseButtonDown(0))  // Left mouse button click
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                CreateWaypoint(hit.point);
            }
        }
    }

    void CreateWaypoint(Vector3 position)
    {
        // Instantiate a new waypoint at the specified position
        GameObject newWaypoint = Instantiate(waypointPrefab, position, Quaternion.identity, waypointContainer);
        
        // Add the new waypoint to the fighter jet controller's waypoints array
        Transform[] newWaypoints = new Transform[fighterJetController.waypoints.Length + 1];
        fighterJetController.waypoints.CopyTo(newWaypoints, 0);
        newWaypoints[newWaypoints.Length - 1] = newWaypoint.transform;
        fighterJetController.waypoints = newWaypoints;
    }
}
