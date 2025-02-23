//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2022 BoneCracker Games
// https://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trailer attacher point. Trailer will be attached when two trigger colliders triggers each other.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller/Misc/RCC Trailer Attacher")]
public class RCC_TrailerAttachPoint : MonoBehaviour {

    public bool isInRange = false;
    public KeyCode towEnable = KeyCode.T;
    public bool isTowed = false;


    public RCC_CarControllerV3 otherVehicle;
    public RCC_TrailerAttachPoint otherAttacher;


    private void OnTriggerEnter(Collider col) {

        //  Getting other attacher.
        otherAttacher = col.gameObject.GetComponent<RCC_TrailerAttachPoint>();

        //  If no attacher found, return.
        if (!otherAttacher)
            return;

        //  Other vehicle.
        otherVehicle = otherAttacher.gameObject.GetComponentInParent<RCC_CarControllerV3>();

        //  If no vehicle found, return.
        if (!otherVehicle)
            return;

        //  Attach the trailer.

        isInRange = true;

        //GetComponentInParent<ConfigurableJoint>().transform.SendMessage("AttachTrailer", otherVehicle, SendMessageOptions.DontRequireReceiver);

    }

    void OnTriggerExit(Collider other)
    {
        isInRange = false;

        otherAttacher = null;
        otherVehicle = null;
    }

    void Update()
    {



        if (Input.GetKeyDown(towEnable) && isInRange && isTowed)
        {
            GetComponentInParent<ConfigurableJoint>().transform.SendMessage("DetachTrailer", otherVehicle, SendMessageOptions.DontRequireReceiver);
            isTowed = false;
        }
        else if (Input.GetKeyDown(towEnable) && isInRange)
        {
            GetComponentInParent<ConfigurableJoint>().transform.SendMessage("AttachTrailer", otherVehicle, SendMessageOptions.DontRequireReceiver);
            isTowed = true;
        }
    }

    private void Reset() {

        if (GetComponent<BoxCollider>() == null)
            gameObject.AddComponent<BoxCollider>().isTrigger = true;

    }

}
