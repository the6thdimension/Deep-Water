using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Airplane_Characteristics : MonoBehaviour
{
    #region Varaibles
    [Header("Characteristic Propertires")]
    public float maxMPH = 110f;
    public float rbLerpSpeed = 0.01f;


    [Header("Lift Properties")]
    public float maxLiftPower = 800f;
    public AnimationCurve liftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float flapLiftPower = 100f;

    [Header("Drag Properties")]
    public float dragFactor = .01f;
    public float flapDragFactor = 0.005f;
    public float airbrakeDragFactor = 0.009f;

    [Header("Contol Properties")]
    public float pitchSpeed = 1000f;
    public float rollSpeed = 1000f;
    public float yawSpeed = 1000f;
    public bool doBanking = false;
    public AnimationCurve controlSurfaceEfficiency = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    private BaseAirplane_Input input;
    private Rigidbody rb;
    private float startDrag;
    private float startAngularDrag;

    private float forwardSpeed;
    public float ForwardSpeed
    {
        get { return forwardSpeed; }
    }

    public float mph;
    public float MPH
    {
        get { return mph; }
    }

    private float maxMPS;
    private float normalizeMPH;

    private float angleOfAttack;
    public float AngleOfAttack
    {
        get { return angleOfAttack; }
    }
    private float pitchAngle;
    private float rollAngle;

    private float csEfficiencyValue;

    #endregion

    #region Constants
    const float mpsToMph = 2.23694f;
    #endregion

    #region custom methods
    public void InitCharacteristics(Rigidbody curRB, BaseAirplane_Input curInput)
    {
        input = curInput;
        rb = curRB;
        startDrag = rb.linearDamping;
        startAngularDrag = rb.angularDamping;

        //Find Max meters per second
        maxMPS = maxMPH / mpsToMph;
    }

    public void updateCharacteristics()
    {
        if(rb)
        {
            //Process Flight Physics
            CalculateForwardSpeed();
            CalculateLift();
            CalculateDrag();

            //Process Flight Control
            HandleControlSurfaceEfficiency();
            HandlePitch();
            HandleRoll();
            HandleYaw();
            HandleBanking();


            //Handle Rigidbody
            HandleRigidbodyTransform();
        }

    }




    void CalculateForwardSpeed()
    {
        //Transform the Rigidbody velocity vector from world space to local space
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        forwardSpeed = Mathf.Max(0f, localVelocity.z);
        forwardSpeed = Mathf.Clamp(forwardSpeed, 0f, maxMPS);

        mph = forwardSpeed * mpsToMph;
        //mph = Mathf.Clamp(mph, 0f, maxMPH);
        normalizeMPH = Mathf.InverseLerp(0f, maxMPH, mph);

    }

    void CalculateLift()
    {
        //Get the angle of Attack
        angleOfAttack = Vector3.Dot(rb.linearVelocity.normalized, transform.forward);
        angleOfAttack *= angleOfAttack;

        //Create the Lift Direction
        Vector3 liftDir = transform.up;
        float liftPower = liftCurve.Evaluate(normalizeMPH) * maxLiftPower;

        //Add Flap Lift
        //float finalLiftPower = flapLiftPower * input.NormalizedFlaps;

        //Apply the final Lift Force to the Rigidbody
        Vector3 finalLiftForce = liftDir * (liftPower) * angleOfAttack;
        rb.AddForce(finalLiftForce);
    }

    void CalculateDrag()
    {
        //Speed drag
        float speedDrag = forwardSpeed * dragFactor;


        //Flap Drag
        float flapDrag = input.Flaps * flapDragFactor;

        float airBrakeDrag = input.Brake * airbrakeDragFactor;

        //Add it all together
        float finalDrag = startDrag + speedDrag + flapDrag + airBrakeDrag;

        rb.linearDamping = finalDrag;
        rb.angularDamping = startAngularDrag * forwardSpeed;
    }

    void HandleControlSurfaceEfficiency()
    {
        csEfficiencyValue = controlSurfaceEfficiency.Evaluate(normalizeMPH);
    }

    void HandlePitch()
    {
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        pitchAngle = Vector3.Angle(transform.forward, flatForward);

        Vector3 pitchTorque = input.Pitch * pitchSpeed * transform.right * csEfficiencyValue;
        rb.AddTorque(pitchTorque);

    }

    void HandleRoll()
    {
        Vector3 flatRight = transform.right;
        flatRight.y = 0f;
        flatRight = flatRight.normalized;
        rollAngle = Vector3.SignedAngle(transform.right, flatRight, transform.forward);

        Vector3 rollTorque = -input.Roll * rollSpeed * transform.forward * csEfficiencyValue;
        rb.AddTorque(rollTorque);
    }

    void HandleYaw()
    {
        Vector3 yawTorque = input.Yaw * yawSpeed * transform.up * csEfficiencyValue;
        rb.AddTorque(yawTorque);
    }

    void HandleBanking()
    {
        if(doBanking)
        {
            float bankSide = Mathf.InverseLerp(-90f, 90f, rollAngle);
            float bankAmount = Mathf.Lerp(-1f, 1f, bankSide);
            Vector3 bankTorque;
            
            if(Vector3.Dot(transform.up, Vector3.down)>0)
            {
                bankTorque = -bankAmount * rollSpeed * transform.up;
            }
            else
            {
                bankTorque = bankAmount * rollSpeed * transform.up;
            }
            
            rb.AddTorque(bankTorque);
        }
        
    }

    void HandleRigidbodyTransform()
    {
        if (rb.linearVelocity.magnitude > 1f)
        {
            Vector3 updatedVelocity = Vector3.Lerp(rb.linearVelocity, transform.forward * forwardSpeed, forwardSpeed * angleOfAttack * Time.deltaTime * rbLerpSpeed);
            rb.linearVelocity = updatedVelocity;


            Quaternion updatedRotation = Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(rb.linearVelocity, transform.up), Time.deltaTime * rbLerpSpeed);
            rb.MoveRotation(updatedRotation);
        }
    }

    #endregion
}
