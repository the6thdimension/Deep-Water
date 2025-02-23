using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlSurface : MonoBehaviour
{
    public enum ControlSurfaceType
    {
        Rudder,
        Elevator,
        Flap,
        Aileron,
        Airbrake,
        LandingGear
    }

    #region Variables
    [Header("Control Surfaces Properties")]
    public ControlSurfaceType type = ControlSurfaceType.Rudder;
    public float maxAngle = 30f;
    public Transform controlSurfaceGraphic;
    public Vector3 axis = Vector3.right;
    public float smoothSpeed = 2f;

    private float wantedAngle;
    private Vector3 startAngle;
    #endregion


    // Start is called before the first frame update
    void Start()
    {
        startAngle = controlSurfaceGraphic.localRotation.eulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        if (controlSurfaceGraphic)
        {
            Vector3 finalAngleAxis = axis * wantedAngle;

            controlSurfaceGraphic.localRotation = Quaternion.Slerp(controlSurfaceGraphic.localRotation, Quaternion.Euler(startAngle + finalAngleAxis), Time.deltaTime * smoothSpeed);
        }
    }

    public void HandleControlSurface(BaseAirplane_Input input)
    {
        float inputValue = 0f;

        switch (type)
        {
            case ControlSurfaceType.Rudder:
                inputValue = input.Yaw;
                break;

            case ControlSurfaceType.Elevator:
                inputValue = input.Pitch;
                break;

            case ControlSurfaceType.Flap:
                inputValue = input.Flaps;
                break;

            case ControlSurfaceType.Aileron:
                inputValue = input.Roll;
                break;

            case ControlSurfaceType.LandingGear:
                inputValue = input.LandingGearToggle;
                break;

            case ControlSurfaceType.Airbrake:
                inputValue = input.Brake;
                break;

            default:
                break;
        }

        wantedAngle = maxAngle * inputValue;
    }
}
