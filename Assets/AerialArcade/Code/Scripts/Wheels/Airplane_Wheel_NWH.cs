using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NWH.WheelController3D;

public class Airplane_Wheel_NWH : MonoBehaviour
{
    [Header("Wheel Properties")]
    public bool isBraking = false;
    public float brakePower = 5f;


    #region Variables
    private WheelController WheelCol;
    private Vector3 worldPos;
    private Quaternion worldRot;
    private float finalBrakeForce;
    private float finalSteerAngle;
    #endregion
    // Start is called before the first frame update
       private void Start()
    {
        WheelCol = GetComponent<WheelController>();
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
        }
    }
    #endregion
}
