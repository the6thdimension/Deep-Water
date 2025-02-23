using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RCC_SetLayer : MonoBehaviour {

    public string layer = "Default";
    public bool setChildren = false;

    private void Start() {

        gameObject.layer = LayerMask.NameToLayer(layer);

        if (setChildren) {

            foreach (Transform item in transform)
                item.gameObject.layer = LayerMask.NameToLayer(layer);

        }

    }

}
