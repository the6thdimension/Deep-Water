using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamRot : MonoBehaviour
{
    public Transform rotator;
    public float speed = 20;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.RotateAround(rotator.position, Vector3.up, speed * Time.deltaTime);
    }
}
