using GNB;
using GNB.Demo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class turretTestFire : MonoBehaviour
{

    [SerializeField]
    private KeyCode turretFire = KeyCode.K;
    public bool isFiring = false;
    public Gun gunT;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(turretFire) && !isFiring)
        {
            isFiring = true;
        }
        else if(Input.GetKeyDown(turretFire) && isFiring)
        {
            isFiring = false;
        }

        if(isFiring)
        {
            gunT.Fire(Vector3.zero);
        }

    }
}
