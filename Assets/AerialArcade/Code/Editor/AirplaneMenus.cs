using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


public class AirplaneMenus
{
    [MenuItem("Airplane Tools/Create New Airplane")]
    public static void CreateNewAirplane()
    {

        GameObject curSelected = Selection.activeGameObject;
        if(curSelected)
        {
            Airplane_Controller  curController = curSelected.AddComponent<Airplane_Controller>();
            GameObject curCOG = new GameObject("centerOfGravity");
            curCOG.transform.SetParent(curSelected.transform);

            curController.centerOfGravity = curCOG.transform;
        }
        //IP_Airplane_SetupTools.BuildDefaultAirplane("New Airplane");
        //AirplaneSetup_Window.LaunchSetupWindow();
    }
}
