using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit.Example.Developer {
    public class ScreenRay : MonoBehaviour {
        [Header("References")]
        public GameObject LineGraphic;
        [Header("Runtime")]
        public Vector2 P1;
        public Vector2 P2;
        public Vector2 Direction => (P2 - P1);
        public bool IsZero => Direction == Vector2.zero;
        public void Clear() {
            P1 = P2 = Vector2.zero;
        }
        void LateUpdate() {
            var mouseGUIPos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            var isOverGUI = TestAngleSensor.GUIRect.Contains(mouseGUIPos);
            if (!isOverGUI) {
                if (Input.GetMouseButtonDown(0)) {
                    P1 = P2 = Input.mousePosition;
                }
                if (Input.GetMouseButton(0)) {
                    P2 = Input.mousePosition;
                }
            }
            Draw(LineGraphic);
        }
         void Draw(GameObject lineGraphic) {
            if (P1 == P2) {
                lineGraphic.SetActive(false);
                return;
            }
            lineGraphic.SetActive(true);
            var cam = Camera.main;
            var p1Proj = cam.ScreenToWorldPoint(new Vector3(P1.x, P1.y, 1f));
            var p2Proj = cam.ScreenToWorldPoint(new Vector3(P2.x, P2.y, 1f));
            lineGraphic.transform.position = p1Proj;
            lineGraphic.transform.LookAt(p2Proj);
            lineGraphic.transform.localScale = new Vector3(1f, 1f, Vector3.Distance(p1Proj, p2Proj));
        }
    }
}