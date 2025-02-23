using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controls : MonoBehaviour
{
        public enum ControlType
    {
        Throttle,
        Stick
    }

    public ControlType type = ControlType.Throttle;
    public float maxAnglex = 10f;
    public float maxAngley = 10f;
    public float maxAngle = 30f;



    public Transform controlGraphic;
    public Vector3 axis = Vector3.right;
    public Vector3 axisx = Vector3.right;
    public Vector3 axisy = Vector3.up;

    public float smoothSpeed = 2f;

    private float wantedAngle;
    private float wantedAnglex;
    private float wantedAngley;

    private Vector3 startAngle;



    // Start is called before the first frame update
    void Start()
    {
        startAngle = controlGraphic.localRotation.eulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        if (controlGraphic)
        {
            if(type == ControlType.Throttle)
            {
                Vector3 finalAngleAxis = axis * wantedAngle;

                controlGraphic.localRotation = Quaternion.Slerp(controlGraphic.localRotation, Quaternion.Euler(startAngle + finalAngleAxis), Time.deltaTime * smoothSpeed);
            }
            else if(type == ControlType.Stick)
            {
                Vector3 finalAngleAxisx = axisx * wantedAnglex;
                Vector3 finalAngleAxisy = axisy * wantedAngley;


                controlGraphic.localRotation = Quaternion.Slerp(controlGraphic.localRotation, Quaternion.Euler(startAngle + finalAngleAxisx + finalAngleAxisy), Time.deltaTime * smoothSpeed);

            }
            
        }
    }

     public void HandleControls(BaseAirplane_Input input)
    {
        float inputValue = 0f;
        float inputValuex = 0f;
        float inputValuey = 0f;

        switch (type)
        {
            case ControlType.Throttle:
                inputValue = input.StickyThrottle;
                break;

            case ControlType.Stick:
                inputValuex = input.Pitch;
                inputValuey = input.Roll;
                break;

            default:
                break;
        }

        wantedAnglex = maxAnglex * inputValuex;
        wantedAngley = maxAngley * inputValuey;

        wantedAngle = maxAngle * inputValue;
    }
}
