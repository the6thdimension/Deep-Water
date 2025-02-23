using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIFixedWiing : MonoBehaviour
{

    [SerializeField] private Transform targetPositionTranform;

    private FixedWingController fixedWingController;
    private Vector3 targetPosition;

    public float distanceToTarget;
    public float angleToDir;
    public Transform localPosition;
    public float stoppingDistance = 7f;
    public float stoppingSpeed = 2f;

    // Start is called before the first frame update
    void Start()
    {
        fixedWingController = GetComponent<FixedWingController>();
    }

    // Update is called once per frame
    void Update()
    {
        SetTargetPosition(targetPositionTranform.position);

        float forwardAmount = 0f;
        float turnAmount = 0f;

        float reachedTargetDistance = 3f;
        distanceToTarget = Vector3.Distance(localPosition.position, targetPosition);



        if (distanceToTarget > reachedTargetDistance) {
            // Still too far, keep going
            Vector3 dirToMovePosition = (targetPosition - localPosition.position).normalized;
            float dot = Vector3.Dot(localPosition.forward, dirToMovePosition);

            if (dot > 0) 
            {
                // Target in front
                fixedWingController.rawThrottle = 1f;

                
                if (distanceToTarget < stoppingDistance && fixedWingController.currentSpeed > stoppingSpeed) 
                {
                    fixedWingController.rawBrake = 1f;
                }

                angleToDir = Vector3.SignedAngle(localPosition.forward, dirToMovePosition, Vector3.up);

                if (angleToDir > 0) 
                {
                    fixedWingController.rawNoseWheel = 1f;
                } 
                else 
                {
                    fixedWingController.rawNoseWheel = -1f;
                }

            }
            else 
            {
                // Reached target
                fixedWingController.rawBrake = 1f;
                forwardAmount = 0f;
                fixedWingController.rawNoseWheel = 0f;
                //turnAmount = 0f;
            }
            

            
                
                // if (distanceToTarget < stoppingDistance && carDriver.GetSpeed() > stoppingSpeed) {
                //     // Within stopping distance and moving forward too fast
                //     forwardAmount = -1f;
                // }
            // else {
            //     // Target behind
            //     float reverseDistance = 25f;
            //     if (distanceToTarget > reverseDistance) {
            //         // Too far to reverse
            //         forwardAmount = 1f;
            //     } else {
            //         forwardAmount = -1f;
            //     }
            
            // }
        }

    }

    public void SetTargetPosition(Vector3 targetPosition) 
    {
        this.targetPosition = targetPosition;
    }
}
