using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WingTrailHandler : MonoBehaviour
{

    public TrailRenderer[] wingtips;
    public Airplane_Characteristics airplaneChar;
    public AnimationCurve traiCurve = AnimationCurve.EaseInOut(1f, 1f, 0f, 0f);


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        // foreach (var wingtip in wingtips)
        // {
        //     if (airplaneChar.MPH > 75f)
        //     {
        //         wingtip.time = traiCurve.Evaluate(airplaneChar.AngleOfAttack);

        //     }
        //     else
        //     {
        //         wingtip.time = Mathf.Lerp(0,wingtip.time, Time.deltaTime);
        //     }
        // }
    }
}
