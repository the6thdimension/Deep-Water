using System;
using System.Collections;
using System.Collections.Generic;
using HexabodyVR.PlayerController;
using UnityEngine;

public class HexaResetZone : MonoBehaviour
{
    public Transform ResetPosition;
    public bool reset;
    public HexaBodyPlayer4 hexa;
    
    public void FixedUpdate()
    {
        if (reset)
        {
            if (hexa) hexa.MoveToPosition(ResetPosition.position);
            reset = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (reset) return;
        hexa = other.gameObject.GetComponentInParent<HexaBodyPlayer4>();
        if (hexa) reset = true;
    }
}
