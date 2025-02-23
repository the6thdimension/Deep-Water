using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WheelCollider))]
public class Airplane_Wheel : MonoBehaviour
{
    [Header("Wheel Properties")]
    public Transform wheelGraphic;
    public bool isBraking = false;
    public float brakePower = 5f;
    public bool isSteering = false;
    public float steerAngle = 20f;
    public float steerSmoothSpeed = 8f;


    #region Variables
    private WheelCollider WheelCol;
    private Vector3 worldPos;
    private Quaternion worldRot;
    private float finalBrakeForce;
    private float finalSteerAngle;
    #endregion

    private void Start()
    {
        WheelCol = GetComponent<WheelCollider>();
    }

    #region Custom Methods
    public void InitWheel()
    {
        if(WheelCol)
        {
            WheelCol.motorTorque = 0.0000000000001f;
        }
    }

    public void HandleWheel(BaseAirplane_Input input)
    {
       if(WheelCol)
        {
            WheelCol.GetWorldPose(out worldPos, out worldRot);
            if(wheelGraphic && input.LandingGearToggle < 1)
            {
                wheelGraphic.rotation = worldRot;
                wheelGraphic.position = worldPos;
            }


            if (isBraking)
            {
                if (input.Brake > 0.1f)
                {
                    finalBrakeForce = Mathf.Lerp(finalBrakeForce, input.Brake * brakePower, Time.deltaTime);
                    WheelCol.brakeTorque = finalBrakeForce;
                }

                else
                {
                    finalBrakeForce = 0f;
                    WheelCol.brakeTorque = 0f;
                    WheelCol.motorTorque = 0.0000000000001f;
                }
            }
            
            if(isSteering && input.LandingGearToggle < 1)
            {
                finalSteerAngle = Mathf.Lerp(finalSteerAngle, input.Yaw * steerAngle, Time.deltaTime * steerSmoothSpeed);
                WheelCol.steerAngle = finalSteerAngle;
            }

        }
    }
    #endregion
}
