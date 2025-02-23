using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicFollowCam : MonoBehaviour
{
    #region Varaibles
    [Header("Basic Follow Camera Properties")]
    public Transform target;
    public float distance = 5f;
    public float height = 2f;
    public float smoothSpeed = 0.5f;

    private Vector3 smoothVelocity;
    protected float origHeight;
    #endregion


    #region Builtin Methods
    // Use this for initialization
    void Start()
    {
        origHeight = height;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (target)
        {
            HandleCamera();
        }
    }
    #endregion


    #region Custom Methods
    protected virtual void HandleCamera()
    {
        //Follow Target
        Vector3 wantedPosition = target.position + (-target.forward * distance) + (Vector3.up * height);
        Debug.DrawLine(target.position, wantedPosition, Color.blue);

        transform.position = Vector3.SmoothDamp(transform.position, wantedPosition, ref smoothVelocity, smoothSpeed);
        transform.LookAt(target);
    }
    #endregion
}
