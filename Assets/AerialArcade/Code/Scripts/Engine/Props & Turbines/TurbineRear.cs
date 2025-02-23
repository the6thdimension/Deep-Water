using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurbineRear : MonoBehaviour
{
    public BaseAirplane_Input input;
    public Vector3 outerAxis = Vector3.right;
    public Vector3 innerAxis = Vector3.right;


    public List<Transform> outerDeflectors;
    public List<Transform> innerDeflectors;
    public float smoothSpeed = 2f;
    public float maxAngle = 7f;



    public List<Vector3> startAngle_Outer;
    public List<Vector3> startAngle_Inner;

    public float wantedAngle;
    public float inputValue;



    // Start is called before the first frame update
    void Start()
    {
        foreach(Transform t in outerDeflectors)
        {
            startAngle_Outer.Add(t.localRotation.eulerAngles);
        }

        foreach(Transform t in innerDeflectors)
        {
            startAngle_Inner.Add(t.localRotation.eulerAngles);
        }
    }

    // Update is called once per frame
    void Update()
    {

        inputValue = -1 + input.StickyThrottle;
        wantedAngle = maxAngle * inputValue;

        int i=0; 
        foreach(Transform t in outerDeflectors)
        {
            if(t)
            {
                Vector3 finalAngleAxis = outerAxis * wantedAngle;

                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(startAngle_Outer[i] + finalAngleAxis), Time.deltaTime * smoothSpeed);
                i++;
            }
        }


        i=0;
        foreach(Transform t in innerDeflectors)
        {
            if(t)
            {
                Vector3 finalAngleAxis = innerAxis * wantedAngle;

                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(startAngle_Inner[i] + finalAngleAxis), Time.deltaTime * smoothSpeed);
                i++;
            }
        }
    }
}
