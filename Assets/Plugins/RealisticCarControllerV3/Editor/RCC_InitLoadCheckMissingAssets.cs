//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2022 BoneCracker Games
// https://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

public class RCC_InitLoadCheckMissingAssets : EditorWindow {

    [InitializeOnLoadMethod]
    static void InitOnLoad() {

        EditorApplication.delayCall += EditorUpdate;

    }

    public static void EditorUpdate() {

        if (EditorPrefs.HasKey("RCCCheckedAssets"))
            return;

        foreach (var item in removeAssetsPath)
            FileUtil.DeleteFileOrDirectory(item);

        AssetDatabase.Refresh();

        EditorPrefs.SetInt("RCCCheckedAssets", 1);

    }

    public readonly static string[] removeAssetsPath = new string[]{

        "Assets/RealisticCarControllerV3/Models/Vehicles/Buggy",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Bus/Tex/Bus_Headlights.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Bus/Tex/Bus_Interior.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Bus/Tex/Bus_Tyre.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Bus Trailer/Tex/Bus_Interior.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/M3_E36/Tex/M3_E36 Misc.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_BrakeLight.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_BrakeLight_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Buttom.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Buttom_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Console.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Dashboard.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Door.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Engine.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Engine_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Headlight.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Headlight_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Radiator.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Tyre.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Tyre_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Wood.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Wood_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Tex/Sedan_Lights.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Tex/Skala_.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Tex/Skala_2.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Tex/Skala_Generic.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Tex/Skala_Generic_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Tex/Skala_Lights.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Tex/Skala_Tyre.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Tex/Skala_Tyre_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Trailer2/Tex/Wheel.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Trailer2/Tex/Wheel_N.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Trailer2/Tex/WheelSide.png",
        "Assets/RealisticCarControllerV3/Models/Vehicles/Trailer2/Tex/WheelSide_N.png",
        //"Assets/RealisticCarControllerV3/Editor/RCC_InitLoadCheckMissingAssets.cs",

    };

}
