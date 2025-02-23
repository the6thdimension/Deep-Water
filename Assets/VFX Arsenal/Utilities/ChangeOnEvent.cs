using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.UI;

public class ChangeOnEvent : MonoBehaviour
{
    public GameObject[] Objects;
    public int currentObject = 0;
    public float TimeInterval = 4;
    public Text fxName;
    public Camera theCam;
    public enum ChangeOn
    {
        MouseButton0,
        TimeInterval,
    }
    public ChangeOn ChangeEvent;
    void Start ()
    {
        StartCoroutine(ActivateCurrent ());
        if (ChangeEvent == ChangeOn.TimeInterval)
            StartCoroutine (ActivateTimer ());
    }

    void Update ()
    {
        if (ChangeEvent == ChangeOn.MouseButton0)
        {
            if (Input.GetMouseButtonDown (0))
            {
                currentObject++;
                if (currentObject == Objects.Length) currentObject = 0;
                StartCoroutine(ActivateCurrent ());
            }
        }
    }

    IEnumerator ActivateCurrent ()
    {
        if (currentObject >= Objects.Length) currentObject = Objects.Length - 1;
        foreach (GameObject g in Objects) g.GetComponent<VisualEffect>().Stop();
        yield return new WaitForSeconds(1.5f);
        Objects[currentObject].SetActive (true);
        Objects[currentObject].GetComponent<VisualEffect>().Play();
        foreach (GameObject g in Objects)
            if (g != Objects[currentObject]) g.SetActive (false);
        fxName.text = Objects[currentObject].name;
    }
    IEnumerator ActivateTimer ()
    {
        while (true)
        {
            yield return new WaitForSeconds (TimeInterval);
            currentObject++;
            if (currentObject == Objects.Length) currentObject = 0;
            StartCoroutine(ActivateCurrent ());
        }
    }
}