using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit.Example.Developer {
    public class TargetGraphics : MonoBehaviour {
        [Header("References")]
        public Material Mat1;
        public Material Mat2;
        public GameObject Diamond;
        public GameObject Border;
        [Header("Runtime")]
        public GameObject Target;

        Renderer[] renderers;

        void Awake() {
            renderers = gameObject.GetComponentsInChildren<Renderer>();
        }

        public void SetStyle(int code) {
            var mat = code == 0 ? Mat1 : Mat2;
            foreach (var renderer in renderers) {
                renderer.sharedMaterial = mat;
            }
        }

        void LateUpdate() {
            if (Target == null) {
                Diamond.SetActive(false);
                Border.SetActive(false);
                return;
            }
            Diamond.SetActive(true);
            Border.SetActive(true);
            var bounds = GetBoundsOfTarget();
            transform.position = bounds.center;
            Diamond.transform.localPosition = Vector3.up * bounds.extents.y;
            Border.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
            var cam = Camera.main;
            transform.LookAt(cam.transform.position);
        }

        Bounds GetBoundsOfTarget() {
            Bounds bounds = default;
            var renderers = Target.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers) {
                if (bounds.size == Vector3.zero) {
                    bounds = renderer.bounds;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }
    }
}