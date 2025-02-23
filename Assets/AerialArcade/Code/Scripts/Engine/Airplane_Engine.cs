using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Airplane_Engine : MonoBehaviour
{

    #region Variables
    public float maxForce = 200f;
    public float maxRPM = 2550f;

    public AnimationCurve powerCurve = AnimationCurve.Linear(0f,0f,1f,1f);

    [Header("Propellers")]
    public Propeller propeller;
    #endregion

    #region builtin Methods
    #endregion

    #region custom Methds
    public Vector3 CalculateForce(float throttle)
    {
        float finalThrottle = Mathf.Clamp01(throttle);
        finalThrottle = powerCurve.Evaluate(finalThrottle);

        float currentRPM = finalThrottle * maxRPM;

        //Create Force
        float finalPower = finalThrottle * maxForce;
        Vector3 finalForce = transform.forward * finalPower;

        return finalForce;
    }
    #endregion
}
