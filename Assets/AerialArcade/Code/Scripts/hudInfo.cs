using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class hudInfo : MonoBehaviour
{
    public Airplane_Characteristics characteristics;
    [SerializeField]
    Text MPH = null;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        MPH.text = characteristics.MPH.ToString("0000");

    }
}
