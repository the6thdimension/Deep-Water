using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Avionics : MonoBehaviour
{
    // public MiniMapController HSI;

    // public float maxZoomOut = 20000f;
    // public float maxZoomIn = 2000f;


    // public float mapZoom = 0f;
    // public float mapincrement = 2000f;
    // public bool leftIsPressed = false;
    // public bool rightIsPressed = false;
    // public bool isNeutral = true;

    // // Start is called before the first frame update
    // void Start()
    // {
    //     if(HSI)
    //     {
    //         mapZoom = HSI.camSize;
    //     }
    // }

    // // Update is called once per frame
    // void Update()
    // {
    //     float horizontalDPad = Input.GetAxis("X_DPad_X");

    //     if (horizontalDPad == 0 && !isNeutral)
    //     {
    //         isNeutral = true;
    //         leftIsPressed = false;
    //         rightIsPressed = false;
    //     }
    //     else if (horizontalDPad > 0 && !rightIsPressed)
    //     {
    //         isNeutral = false;
    //         mapZoom = mapZoom + mapincrement;
    //         rightIsPressed = true;
    //     }
    //     else if (horizontalDPad < 0 && !leftIsPressed)
    //     {
    //         isNeutral = false;
    //         if(mapZoom-mapincrement > 0f)
    //         {
    //             mapZoom = mapZoom - mapincrement;
    //         }
    //         leftIsPressed = true;
    //     }

    //     HSI.camSize = mapZoom;
    // }
}
