using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class BaseAirplane_Input : MonoBehaviour
{

    #region Variables
    protected float pitch = 0f;
    protected float roll = 0f;
    protected float yaw = 0f;
    protected float throttle = 0f;
    public float throttleSpeed = 0.1f;

    public Material engMat = null;

    protected float stickyThrottle;
    public float StickyThrottle
    {
        get { return stickyThrottle; }
    }

    [SerializeField]
    private KeyCode brakeKey = KeyCode.Space;
    protected float brake = 0f;

    [SerializeField]
    protected int maxFlapIncrements = 2;
    [SerializeField]
    private KeyCode flapUpKey = KeyCode.F;
    [SerializeField]
    private KeyCode flapDownKey = KeyCode.V;
    protected int flaps = 0;

    [SerializeField]
    private KeyCode gearToggleKey = KeyCode.G;
    protected int landingGearToggle = 0;

    #endregion

    #region Properties
    public float Pitch
    {
        get{ return pitch;}
    }

    public float Roll
    {
        get { return roll; }
    }

    public float Yaw
    {
        get { return yaw; }
    }

    public float Throttle
    {
        get { return throttle; }
    }

    public float Brake
    {
        get { return brake; }
    }
    public int Flaps
    {
        get { return flaps; }
    }
    public int flapInc
    {
        get { return maxFlapIncrements; }
    }
    public int LandingGearToggle
    {
        get { return landingGearToggle; }
    }
    #endregion

    #region Builtin
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput();
        StickyThrottleControl();
    }
    #endregion

    #region Custom Methods
    protected virtual void HandleInput()
    {
        //Process Main Control Input
        pitch = Input.GetAxis("Vertical");
        roll = Input.GetAxis("Horizontal");
        yaw = Input.GetAxis("Yaw");
        throttle = Input.GetAxis("Throttle");

        //Process Brake inputs 
        //if then 1. ! then 0
        brake = Input.GetKey(brakeKey) ? 1f : 0f;

        //Process Flaps Inputs
        if (Input.GetKeyDown(flapUpKey))
        {
            flaps += 1;
        }

        if (Input.GetKeyDown(flapDownKey))
        {
            flaps -= 1;
        }

        //Process LandingGear Input
        if(Input.GetKeyDown(gearToggleKey))
        {
            if(landingGearToggle < 1)
            {
                landingGearToggle = 1;
            }
            else
            {
                landingGearToggle = 0;
            }
        }

    }

    //Create a Throttle Value that gradually grows and shrinks
    protected void StickyThrottleControl()
    {
        stickyThrottle = stickyThrottle + (-throttle * throttleSpeed * Time.deltaTime);
        stickyThrottle = Mathf.Clamp01(stickyThrottle);
    }
    #endregion
}
