using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WingFold : MonoBehaviour
{

    public bool wingFold = false;
    public Transform leftWing;
    public Transform rightWing;
    public float maxAngle = 90f;
    public Vector3 axis_L = Vector3.right;
    public Vector3 axis_R = Vector3.right;

    public float smoothSpeed = 2f;

    private float wantedAngle;
    private Vector3 startAngle_L;
    private Vector3 startAngle_R;



    // Start is called before the first frame update
    void Start()
    {
        startAngle_L = leftWing.localRotation.eulerAngles;
        startAngle_R = rightWing.localRotation.eulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        float inputValue = 0f;

        if(wingFold)
        {
            inputValue = 1f;
        }
        else
        {
            inputValue = 0f;
        }

        wantedAngle = maxAngle * inputValue;

        if (leftWing && rightWing)
        {
            Vector3 finalAngleAxis_L = axis_L * wantedAngle;
            Vector3 finalAngleAxis_R = axis_R * wantedAngle;


            leftWing.localRotation = Quaternion.Lerp(leftWing.localRotation, Quaternion.Euler(startAngle_L + finalAngleAxis_L), Time.deltaTime * smoothSpeed);
            rightWing.localRotation = Quaternion.Lerp(rightWing.localRotation, Quaternion.Euler(startAngle_R + finalAngleAxis_R), Time.deltaTime * smoothSpeed);

        }
    }
}
