using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrestingHook : MonoBehaviour
{

    public bool hookDown = false;
    public bool isHooked = false;
    public LayerMask mask;
    public GameObject hookCollider;

    public Transform arrestingHook;
    public float maxAngle = 90f;
    public float newMaxAngle;
    public Vector3 axis = Vector3.right;

    public float smoothSpeed = 2f;

    private float wantedAngle;
    private Vector3 startAngle;

    private SphereCollider col;

    public bool isColliding = false;


    // Start is called before the first frame update
    void Start()
    {
        startAngle = arrestingHook.localRotation.eulerAngles;
        col = hookCollider.GetComponent<SphereCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        float inputValue = 0f;

        if(hookDown)
        {
            inputValue = 1f;
        }
        else
        {
            inputValue = 0f;
        }



        // Debug.Log(wantedAngle);


        if (arrestingHook)
        {

            
            Vector3 finalAngleAxis;

            if(!isColliding)
            {
                wantedAngle = maxAngle * inputValue;
                wantedAngle = wantedAngle++;
                finalAngleAxis = axis * wantedAngle;

                arrestingHook.localRotation = Quaternion.Lerp(arrestingHook.localRotation, Quaternion.Euler(startAngle + finalAngleAxis), Time.deltaTime * smoothSpeed);

            }
            else
            {

            }

                // arrestingHook.localRotation = Quaternion.Euler(startAngle + axis);

        }
        

    }

    private void OnTriggerEnter(Collider other)
    {
        isColliding = true;
    }
    private void OnTriggerExit(Collider other)
    {
        isColliding = false;

    }


    private void OnTriggerStay(Collider other)
    {
        if(arrestingHook)
        {
            wantedAngle = wantedAngle--;
            arrestingHook.localRotation = Quaternion.Lerp(arrestingHook.localRotation, Quaternion.Euler(startAngle + axis), Time.deltaTime * smoothSpeed);
            // arrestingHook.localRotation = Quaternion.Euler(startAngle + axis);
            Debug.Log(other);
        }

    }
}
