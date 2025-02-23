using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NWH.WheelController3D;

public class FixedWingController : MonoBehaviour
{
    public enum handlingMode
    {
        Cold,
        Starting,
        Hot,
        Taxing,
        Flying
    }

    [Header("Aircraft State Settings")]
    public handlingMode AirplaneHandlingState;
    public bool isAI = true; 

    [Header("Input Properties")]
    
    public float pitch = 0f;
    public float roll = 0f;
    public float yaw = 0f;
    public float rawYaw = 0f;
    public float noseWheel = 0f;
    public float rawNoseWheel = 0f;
    public float noseWheelAngle;
    public float brake = 0f;
    public float rawBrake = 0f;
    public float currentSpeed = 0f;


    [Header("Throttle Properties")]
    public float throttle = 0f;
    public float rawThrottle = 0f;


    public bool  stickyThrottleActive = false;
    public float stickyThrottle = 0f;

    public float throttleSpeed = 0.1f;
    

    [Header("Contol Properties")]
    public float speed = 50.0f;
    public float rotationSpeed = 100.0f;
    public float steeringMaxAngle = 0.65f;



    [Header("Taxi Properties")]
    public bool isNwsEngaged = true;
    public List<WheelController> wheelControllers;

    [SerializeField]
    private KeyCode brakeKey = KeyCode.Space;
    public float brakePower = 5f;
    private float finalBrakeForce;

    public Transform NWS;
    private Rigidbody rb;



    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();


    }

    // Update is called once per frame
    void Update()
    {
        yaw = Input.GetAxis("Horizontal");


        var localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        currentSpeed = localVelocity.z;
        
        

        if(AirplaneHandlingState == handlingMode.Taxing)
        {

            if(isNwsEngaged)
            {
                handleNWS();
            }
            handleThrottle();
            handleBrakes();
        }
    }




    void handleThrottle()
    {
        if(!isAI)
        {
            rawThrottle = Input.GetAxis("Vertical");
        }
        
        throttle = rawThrottle * speed;

        if(!stickyThrottleActive)
        {
            throttle *= Time.deltaTime;
            rb.AddRelativeForce(Vector3.forward * speed * throttle);

        }


    }




    void handleNWS()
    {


        if(!isAI)
        {
            rawNoseWheel = Input.GetAxis("Horizontal");
        }

        noseWheel = rawNoseWheel * rotationSpeed;
        noseWheel *= Time.deltaTime;

        noseWheelAngle = NWS.localRotation.y;

        //inbetween steering limits
        if(noseWheelAngle < steeringMaxAngle && noseWheelAngle > -steeringMaxAngle)
        {
            NWS.transform.Rotate(0, noseWheel, 0);
        }
        //too far left
        else if(noseWheelAngle < -steeringMaxAngle)
        {
            if(noseWheel > 0.0f)
            {
                NWS.transform.Rotate(0, noseWheel, 0);
            }
        }
        //too far right
        else
        {
            if(noseWheel < 0.0f)
            {
                NWS.transform.Rotate(0, noseWheel, 0);
            }
        }

        
    }


    void handleBrakes()
    {
        if(!isAI)
        {
            brake = Input.GetKey(brakeKey) ? 1f : 0f;
        }

        brake = rawBrake;


        foreach(WheelController m_wheel in wheelControllers)
        {
            if (brake > 0.1f)
            {
                finalBrakeForce = Mathf.Lerp(finalBrakeForce, brake * brakePower, Time.deltaTime);
                m_wheel.brakeTorque = finalBrakeForce;
            }

            else
            {
                finalBrakeForce = 0f;
                m_wheel.brakeTorque = 0f;
            }
        }
       
                
        }

    public float GetSpeed() 
    {
        return speed;
    }
}
