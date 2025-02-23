using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMovement : MonoBehaviour
{
    // Start is called before the first frame update

    [Range(-10.0f, 10.0f)]
    public float LR = 0f;

    [Range(-10.0f, 10.0f)]
    public float FS = 0f;


    private Transform startPosition;
    void Start()
    {
        startPosition = transform;
    }

    // Update is called once per frame
    void Update()
    {
        //Left Right
        transform.localPosition = new Vector3(LR,startPosition.localPosition.y,startPosition.localPosition.z);
    }
}
