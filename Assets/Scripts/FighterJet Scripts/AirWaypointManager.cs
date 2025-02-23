using System.Collections;
using System.Collections.Generic;

using UnityEngine;

[System.Serializable]
public struct AirWaypoint
{
    public Vector3 position;
    public float altitude;
    public float speed;
    public float expectedTime; // Time expected to reach this waypoint
}


public class AirWaypointManager : MonoBehaviour
{



    public List<AirWaypoint> waypoints = new List<AirWaypoint>();

    public void AddWaypoint(Vector3 position, float altitude, float speed, float expectedTime)
    {
        waypoints.Add(new AirWaypoint { position = position, altitude = altitude, speed = speed, expectedTime = expectedTime });
    }

    public void RemoveWaypoint(int index)
    {
        if (index >= 0 && index < waypoints.Count)
        {
            waypoints.RemoveAt(index);
        }
    }

    public void ClearWaypoints()
    {
        waypoints.Clear();
    }
}



