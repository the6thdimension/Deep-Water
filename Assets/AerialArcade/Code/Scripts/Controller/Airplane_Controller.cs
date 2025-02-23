using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Airplane_Characteristics))]
public class Airplane_Controller : BaseRigidBody_Controller
{
    #region Variables
    [Header("Base Airplane Properties")]
    public BaseAirplane_Input input;
    public Airplane_Characteristics characteristics;
    public Transform centerOfGravity;

    [Tooltip("Weight is in Pounds")]
    public float airplaneWeight = 800f;

    [Header("Engines")]
    public List<Airplane_Engine> engines = new List<Airplane_Engine>();
    

    [Header("Wheels")]
    public List<Airplane_NoseGear> noseGears = new List<Airplane_NoseGear>();
    public List<Airplane_Wheel> wheels = new List<Airplane_Wheel>();
    public List<Airplane_Wheel_NWH> NWHwheels = new List<Airplane_Wheel_NWH>();


    [Header("Control Surfaces")]
    public List<ControlSurface> controlSurfaces = new List<ControlSurface>();

    [Header("Controls")]
    public List<Controls> controls = new List<Controls>();

    #endregion

    #region Constants
    const float poundsToKilos = 0.453592f;
    #endregion

    #region builtin
    public override void Start()
    {
        base.Start();

        float finalMass = airplaneWeight * poundsToKilos;
        if (rb)
        {
            rb.mass = finalMass;
            if (centerOfGravity)
            {
                rb.centerOfMass = centerOfGravity.localPosition;
            }

            characteristics = GetComponent<Airplane_Characteristics>();
            if (characteristics)
            {
                characteristics.InitCharacteristics(rb, input);
            }
        }

        if (wheels != null)
        {
            if (wheels.Count > 0)
            {
                foreach (Airplane_Wheel wheel in wheels)
                {
                    wheel.InitWheel();
                }
            }
        }
    }
    #endregion


    #region Custom Methods
    protected override void HandlePhysics()
    {
        if (input)
        {
            HandleEngines();
            HandleCharacteristics();
            HandleControlSurfaces();
            HandleNoseGear();
            HandleWheels();
            HandleAltitude();
            HandleControls();
        }
    }

    void HandleEngines()
    {
        if (engines != null)
        {
            if (engines.Count > 0)
            {
                foreach (Airplane_Engine engine in engines)
                {
                    rb.AddForce(engine.CalculateForce(input.StickyThrottle));
                }
            }
        }
    }

    void HandleCharacteristics()
    {
        if (characteristics)
        {
            characteristics.updateCharacteristics();
        }
    }

    void HandleControlSurfaces()
    {
        if (controlSurfaces.Count > 0)
        {
            foreach (ControlSurface controlsurface in controlSurfaces)
            {
                controlsurface.HandleControlSurface(input);
            }
        }
    }

    void HandleControls()
    {
        if (controls.Count > 0)
        {
            foreach (Controls control in controls)
            {
                control.HandleControls(input);
            }
        }
    }
    void HandleWheels()
    {
        if(wheels.Count > 0)
        {
            foreach(Airplane_Wheel wheel in wheels)
            {
                wheel.HandleWheel(input);
            }

            foreach(Airplane_Wheel_NWH wheel in NWHwheels)
            {
                wheel.HandleWheel(input);
            }
        }
    }

    void HandleNoseGear()
    {
        if(noseGears.Count > 0)
        {
            foreach(Airplane_NoseGear noseGear in noseGears)
            {
                noseGear.HandleNoseGear(input);
            }
        }
    }

    void HandleAltitude()
    {

    }

    #endregion
}
