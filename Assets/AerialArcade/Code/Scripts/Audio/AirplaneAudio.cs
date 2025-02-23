using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AirplaneAudio : MonoBehaviour
{

    #region variables
    [Header("Airplane Audio")]
    public BaseAirplane_Input input;
    public AudioSource idleSource01;
    public AudioSource idleSource02;
    public AudioSource fullThrottleSource;
    public float maxPitchValue = 1.2f;
    public float maxIdleVolumeValue = .8f;


    private float finalVolumeValue;
    private float finalPitchValue;
    private float finalIdleVolume;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        if (fullThrottleSource)
        {
            fullThrottleSource.volume = 0f;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (input)
        {
            HandleAudio();
        }
    }

    #region custom Methods
    protected virtual void HandleAudio()
    {


        finalVolumeValue = Mathf.Lerp(0f, .8f, input.StickyThrottle);
        finalIdleVolume = Mathf.Lerp(0.35f, maxIdleVolumeValue, input.StickyThrottle);
        finalPitchValue = Mathf.Lerp(1f, maxPitchValue, input.StickyThrottle*2f);

        idleSource01.pitch = finalPitchValue;


        // if (fullThrottleSource)
        // {
        //     fullThrottleSource.volume = finalVolumeValue;
        //     idleSource01.pitch = finalPitchValue;
        //     idleSource02.pitch = finalPitchValue;
        // }
    }
    #endregion
}
