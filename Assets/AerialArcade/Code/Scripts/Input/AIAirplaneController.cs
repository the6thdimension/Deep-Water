using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIAirplaneController : BaseAirplane_Input
{

    #region Variables

    [Header("Airplane Characteristics")]
    public Airplane_Characteristics AChar;
    public Airplane_Controller ACntrl;
    public float brakeSmoothing = 3f;


    // protected float pitch = 0f;
    // protected float roll = 0f;
    // protected float yaw = 0f;
    // protected float throttle = 0f;
    // public float throttleSpeed = 0.1f;

    [Header("AI Pilot | Navigation")]
    public Transform navigation_Transform;

    public List<Waypoint_FixedWing> waypoints;

    // public WaypointPath Path = null;
    // public Transform Target = null;

    public LayerMask layerMask;
    public Transform detector_Transform;
    public float detector_Distance = 45f;
    public bool collisionDetectionEnabled = true;
    public bool objectDetected = false;


    [Header("Controller Debugs")]
    public Transform throttleDebug;



    private int selectedIndex = 0;
    private Waypoint_FixedWing selected;

    private Vector3 distanceFromWaypoint;

    #endregion

    public void Start() 
    {
        
        
    }

    void FixedUpdate()
    {
        RaycastHit hit;
        // Does the ray intersect any objects excluding the player layer
        if (Physics.Raycast(detector_Transform.position, detector_Transform.TransformDirection(Vector3.forward), out hit, detector_Distance, layerMask))
        {
            Debug.DrawRay(detector_Transform.position, detector_Transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
            objectDetected = true;
            Debug.Log("Did Hit");
        }
        else
        {
            Debug.DrawRay(detector_Transform.position, detector_Transform.TransformDirection(Vector3.forward) * detector_Distance, Color.white);
            objectDetected = false;
            Debug.Log("Did not Hit");
        }

    }


    protected override void HandleInput()
    {
        if(waypoints.Count != 0)
        {
            
            selected = waypoints[selectedIndex];

        
            distanceFromWaypoint = navigation_Transform.position - selected.transform.position;

            
            showDebug();


            // Debug.Log(distanceFromWaypoint.magnitude);

            if(selected.navigationType == Waypoint_FixedWing.NavigationType.Taxing)
            {
                HandleTaxing();
                
            }
            
            else if(selected.navigationType == Waypoint_FixedWing.NavigationType.Flying)
            {
                HandleFlying();
            }

        }
    }

    float map(float s, float a1, float a2, float b1, float b2)
    {
        return b1 + (s-a1)*(b2-b1)/(a2-a1);
    }
    
    public void HandleTaxing()
    {

        float mph = AChar.mph;

        // ======================
        // Throttle Taxi Code
        // ======================


        if(distanceFromWaypoint.magnitude < 3 || objectDetected && collisionDetectionEnabled)
        {
            stickyThrottle =  Mathf.Lerp(stickyThrottle, .0f, Time.deltaTime * brakeSmoothing);
            brake =  Mathf.Lerp(brake, 1f, Time.deltaTime * brakeSmoothing);
        }
        else
        {
            
            if(AChar.mph > selected.navSpeedMax)
            {
                stickyThrottle =  Mathf.Lerp(stickyThrottle, .0f, Time.deltaTime * brakeSmoothing);
                // brake = .1f;

                Debug.Log("Speeding: " + AChar.mph + " | " + selected.navSpeedMax);
            }
            else if(AChar.mph < selected.navSpeedMin)
            {
               
                stickyThrottle =  Mathf.Lerp(stickyThrottle, .1f, Time.deltaTime * brakeSmoothing);

                Debug.Log("Not Speeding: " + AChar.mph + " | " + selected.navSpeedMax);
            }


            //======================
            // Steering Taxi Code
            //======================
            Vector3 localTargetDirection = navigation_Transform.transform.InverseTransformPoint(selected.transform.position).normalized;

            float brakeAmount = map(AChar.mph, 0f , selected.navSpeedMax,  0f, .25f);

            yaw = localTargetDirection.x * 5f;
            yaw = Mathf.Clamp(yaw, -1f, 1f);
            
            brake = Mathf.Abs(Mathf.Clamp(yaw, -1f, 1f)*brakeAmount);

        }
        


        // if(distanceFromWaypoint.magnitude < 3)
        // {
        //     stickyThrottle = 0f;
        //     brake = 1f;
        // }
        // else
        // {
        //     stickyThrottle = .1f;
        //     brake = 0f;
        // }




      

        // Debug.Log(test);

        // if(!navigation_Transform)
        // {
        //     navigation_Transform = transform;
        // }

        Debug.DrawLine(navigation_Transform.position, selected.transform.position , Color.green);





    }

    public void HandleFlying()
    {
        throttle = 1f;
        stickyThrottle = 1f;



        Vector3 targetPosition = selected.transform.position;//Target.position;


        Vector3 localTargetDirection = transform.InverseTransformPoint(targetPosition).normalized;
        pitch = -localTargetDirection.y * 3f;
        pitch = Mathf.Clamp(pitch, -1f, 1f);

        yaw = localTargetDirection.x * 5f;
        yaw = Mathf.Clamp(yaw, -1f, 1f);

        var wingsLevelRoll = transform.right.y * 3f;
        var turnIntoRoll = localTargetDirection.x * 3f;

        // Literally the MouseFlight code
        var angleOffTarget = Vector3.Angle(Vector3.forward, localTargetDirection);
        var wingsLevelInfluence = Mathf.InverseLerp(0f, 1.5f, angleOffTarget);
        roll = Mathf.Lerp(wingsLevelRoll, turnIntoRoll, wingsLevelInfluence);
        roll = Mathf.Clamp(roll, -1f, 1f);
    }

    public void showDebug()
    {

        //Throttle Debugging Code
        if(throttleDebug)
        {
            Debug.DrawLine(throttleDebug.position, throttleDebug.position  - throttleDebug.up * .1f , Color.green);
            Debug.DrawLine(throttleDebug.position, throttleDebug.position  + throttleDebug.up * .1f , Color.green);

            Debug.DrawLine(throttleDebug.position + throttleDebug.forward * 1f, throttleDebug.position  - throttleDebug.up * .1f , Color.green);
            Debug.DrawLine(throttleDebug.position + throttleDebug.forward * 1f, throttleDebug.position  + throttleDebug.up * .1f , Color.green);

            
            Debug.DrawLine(throttleDebug.position - throttleDebug.forward * 1f, throttleDebug.position - throttleDebug.forward * 1f - throttleDebug.up * .1f , Color.green);
            Debug.DrawLine(throttleDebug.position - throttleDebug.forward * 1f, throttleDebug.position - throttleDebug.forward * 1f + throttleDebug.up * .1f , Color.green);

            Debug.DrawLine(throttleDebug.position, throttleDebug.position  - throttleDebug.forward * stickyThrottle , Color.red);
        }



    }
}
