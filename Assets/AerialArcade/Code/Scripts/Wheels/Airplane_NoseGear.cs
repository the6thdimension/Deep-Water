using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NWH.WheelController3D;
public class Airplane_NoseGear : MonoBehaviour
{

    public enum NWS_Setting
    {
        HI,
        LOW,
        OFF
    }

    public Transform target;
    public RCC_TrailerAttachPoint trailer;


    [Header("NWS Properties")]
    public NWS_Setting NWS_setting = NWS_Setting.HI;
    public float steerAngle_LOW = 16f ;  //Per NATOPS-F18 2.10.2
    public float steerAngle_HI = 75f;    //Per NATOPS-F18 2.10.2
    public float steerSmoothSpeed = 8f;

    [Header("Suspension Properties")]
    public Transform suspension;
    public Transform suspensionToWheel;
    public float suspensionHeightOffset = 0.254f;


    [Header("Wheel Properties")]
    public List<WheelController> wheelControllers = new List<WheelController>();
    public bool isBraking = false;
    public float brakePower = 5f;

    // public bool isSteering = false;


    [Header("Debug Properties")]
    public Transform SteerDebug;


    #region Variables
    //private List<NWH> wheelControllers = new List<NWH.WheelController>();
    // private WheelCollider WheelCol;
    private Vector3 worldPos;
    private Quaternion worldRot;
    private float finalBrakeForce;
    private float finalsteerAngle;

    
    #endregion
    // Start is called before the first frame update
    void Start()
    {
        if(wheelControllers != null)
        {
            foreach (WheelController wheel in wheelControllers)
            {
                wheel.motorTorque = 0.0000000000001f;
            }
           
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void HandleNoseGear(BaseAirplane_Input input)
    {
        if(input.LandingGearToggle < 1)
        {
            if(suspension)
            {
                suspension.position = new Vector3(suspension.position.x, suspensionToWheel.position.y + suspensionHeightOffset, suspension.position.z);
            }

            HandleSteering(input);

            foreach(WheelController wheelController in wheelControllers)
            {
                HandleBraking(input, wheelController);
            }
        }
    }

    public void HandleSteering(BaseAirplane_Input input)
    {
        if(NWS_setting == NWS_Setting.HI)
        {
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, steerAngle_HI, 0) * SteerDebug.forward, Color.blue);
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, -steerAngle_HI, 0) * SteerDebug.forward, Color.blue);
            Debug.DrawLine(SteerDebug.position - new Vector3(0f,0f,.10f), SteerDebug.position - new Vector3(0f,0f,.10f) +  SteerDebug.right * input.Yaw , Color.red);


            finalsteerAngle = Mathf.Lerp(finalsteerAngle, input.Yaw * steerAngle_HI, Time.deltaTime * steerSmoothSpeed);
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, finalsteerAngle, 0) * SteerDebug.forward, Color.green);


            transform.localRotation = Quaternion.Euler(finalsteerAngle, transform.localEulerAngles.y, transform.localEulerAngles.z);
        }


        else if(NWS_setting == NWS_Setting.LOW)
        {
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, steerAngle_LOW, 0) * SteerDebug.forward, Color.blue);
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, -steerAngle_LOW, 0) * SteerDebug.forward, Color.blue);
            Debug.DrawLine(SteerDebug.position - new Vector3(0f,0f,.10f), SteerDebug.position - new Vector3(0f,0f,.10f) +  SteerDebug.right * input.Yaw , Color.red);


            finalsteerAngle = Mathf.Lerp(finalsteerAngle, input.Yaw * steerAngle_LOW, Time.deltaTime * steerSmoothSpeed);
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, finalsteerAngle, 0) * SteerDebug.forward, Color.green);


            transform.localRotation = Quaternion.Euler(finalsteerAngle, transform.localEulerAngles.y, transform.localEulerAngles.z);   
        }
        else
        {
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, 0, 0) * SteerDebug.forward, Color.blue);
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, 0, 0) * SteerDebug.forward, Color.blue);
            Debug.DrawLine(SteerDebug.position - new Vector3(0f,0f,.10f), SteerDebug.position - new Vector3(0f,0f,.10f) +  SteerDebug.right * input.Yaw , Color.red);


            finalsteerAngle = Mathf.Lerp(finalsteerAngle, input.Yaw * 0, Time.deltaTime * steerSmoothSpeed);
            Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, finalsteerAngle, 0) * SteerDebug.forward, Color.green);


            transform.localRotation = Quaternion.Euler(finalsteerAngle, transform.localEulerAngles.y, transform.localEulerAngles.z);    
        }
        
    }

    public void HandleBraking(BaseAirplane_Input input, WheelController wheel)
    {
        
        if (isBraking)
        {
            
            if (input.Brake > 0.1f)
                {
                    finalBrakeForce = Mathf.Lerp(finalBrakeForce, input.Brake * brakePower, Time.deltaTime);
                    
                    wheel.brakeTorque = finalBrakeForce;
                }

                else
                {
                    finalBrakeForce = 0f;
                    wheel.brakeTorque = 0f;
                    wheel.motorTorque = 0.0000000000001f;
                }
            }
    }

}
           

            // float x = input.Yaw;
            // float y = input.Throttle;
            // if (x != 0.0f || y != 0.0f) {
            //     angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            //     Debug.DrawLine(SteerDebug.position, SteerDebug.position + Quaternion.Euler( 0, angle, 0) * SteerDebug.forward, Color.red);
            // }
        

            // var SteerDirection = new Vector3(0f,finalsteerAngle,0f);



            // var steerDirection = new Vector3();
            // finalsteerAngle = Mathf.Lerp(finalsteerAngle, input.Yaw * steerAngle_HI, Time.deltaTime * steerSmoothSpeed);
            
            // steerDirection.x = transform.position.x;
            // steerDirection.y = transform.position.y;
            // steerDirection.z = transform.position.z;

            // var rotation = Quaternion.LookRotation(steerDirection);

            // transform.rotation = rotation;



            // transform.rotation = new Vector3(transform.rotation.x, finalsteerAngle, transform.rotation.z);

            // Debug.DrawLine(transform.position, transform.position - transform.up * 5, Color.red);

            //     WheelCol.steerAngle_HI = finalsteerAngle;

        // finalsteerAngle = Mathf.Lerp(finalsteerAngle, input.Yaw * steerAngle_HI, Time.deltaTime * steerSmoothSpeed);
        // WheelCol.steerAngle_HI = finalsteerAngle;

       
        

        // finalsteerAngle = Mathf.Lerp(finalsteerAngle, input.Yaw * steerAngle_HI, Time.deltaTime * steerSmoothSpeed);
        // var rotation = Quaternion.LookRotation(new Vector3(transform.rotation.x, finalsteerAngle, transform.rotation.z));
        // transform.rotation = rotation;



        // if (trailer.isTowed)
        // {
        //     var lookPos = target.position - transform.position;
        //     lookPos.y = 0;
        //     var rotation = Quaternion.LookRotation(lookPos);

        //     transform.rotation = rotation;
        // }
        // else
        // {
        //     // if(isSteering && input.LandingGearToggle < 1)
        //     // {

        //     // }
        // }

