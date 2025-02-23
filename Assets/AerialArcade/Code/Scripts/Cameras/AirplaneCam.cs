using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AirplaneCam : BasicFollowCam
{
    #region Variables
    [Header("Airplane Camera Properties")]
    public float minHeightFromGround = 2f;
    #endregion


    protected override void HandleCamera()
    {
        //Airplane Camera Code
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit))
        {
            if (hit.distance < minHeightFromGround && hit.transform.tag == "ground")
            {
                //                    Debug.Log("Hitting Ground");
                float wantedHeight = origHeight + (minHeightFromGround - hit.distance);
                height = wantedHeight;
            }
        }

        base.HandleCamera();
    }
}
