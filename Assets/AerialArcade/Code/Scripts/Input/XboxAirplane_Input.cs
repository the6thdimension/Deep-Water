using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class XboxAirplane_Input : BaseAirplane_Input
{
    protected bool upIsPressed = false;
    protected bool downIsPressed = false;
    protected bool isNeutral = true;

    protected override void HandleInput()
    {
        //process Keyboard
        base.HandleInput();

        //Process Main Control Input
        pitch += Input.GetAxis("Vertical");
        roll += Input.GetAxis("Horizontal");
        yaw += Input.GetAxis("X_RH_Stick");
        throttle += Input.GetAxis("X_RV_Stick");

        //Process Engine Flame

        float verticalDPad = Input.GetAxis("X_DPad_Y");

        if (verticalDPad == 0 && !isNeutral) 
        {
            isNeutral = true;
            downIsPressed = false;
            upIsPressed = false;
        }
        else if (verticalDPad > 0 && !upIsPressed)
        {
            isNeutral = false;
            flaps += 1;
            upIsPressed = true;
        }
        else if (verticalDPad < 0 && !downIsPressed)
        {
            isNeutral = false;
            flaps -= 1;
            downIsPressed = true;
        }


        //Process LandingGear Input
        if (Input.GetKeyDown("joystick button 9"))
        {
            if (landingGearToggle < 1)
            {
                landingGearToggle = 1;
            }
            else
            {
                landingGearToggle = 0;
            }
        }

        //Process Brake inputs
        brake += Input.GetAxis("Fire1");
    }
}
