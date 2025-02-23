using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class clampTargetHud : MonoBehaviour
{
    public Transform boresight;
    public Image targetImage;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {

            Vector3 targPose = Camera.main.WorldToScreenPoint(boresight.position);
            targetImage.transform.position = targPose;

    }
}
