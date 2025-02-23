using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Waypoint_FixedWing : MonoBehaviour
{
    public enum NavigationType
    {
        Taxing,
        Landing,
        Flying
    }


    public NavigationType navigationType = NavigationType.Taxing;

    public float navSpeedMax = 5f;
    public float navSpeedMin = 2f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
