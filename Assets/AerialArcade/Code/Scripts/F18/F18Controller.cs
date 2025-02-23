using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class F18Controller : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject startupAudio;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartUp()
    {
        AudioSource audio = startupAudio.GetComponent<AudioSource>();

        audio.Play();
    }
}
