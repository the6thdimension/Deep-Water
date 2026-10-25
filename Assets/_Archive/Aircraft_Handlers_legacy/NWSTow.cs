using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NWSTow : MonoBehaviour
{

    public Transform target;
    public RCC_TrailerAttachPoint trailer;
    

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (trailer.isTowed)
        {
            var lookPos = target.position - transform.position;
            lookPos.y = 0;
            var rotation = Quaternion.LookRotation(lookPos);

            transform.rotation = rotation;
        }
        
       
    }
}

//Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * damping);