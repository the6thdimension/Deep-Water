using HexabodyVR.PlayerController;
using UnityEngine;

namespace HexabodyVR.SampleScene
{
    [RequireComponent(typeof(LineRenderer))]
    public class SpiderHands : MonoBehaviour
    {
        public float Distance = 350;
        public float PullSpeed = 10f;
        public LayerMask GrabLayer;
        public HexaXRInputs Controller;
        public bool Grabbing;
        public Transform Anchor;
        public Rigidbody Body;
        public float Spring = 50000;
        public float Damper = 1000;
        public float CrossHairMaxScale = 10f;
        public GameObject Crosshair;
        public ConfigurableJoint Joint;
        public BasicGrabber BasicGrabber;

        public LineRenderer LineRenderer;

        private Vector3 _hitPoint;
        private float _distance;

        void Awake()
        {
            LineRenderer = GetComponent<LineRenderer>();
            BasicGrabber = GetComponent<BasicGrabber>();
            
        }

        void Start()
        {
            Crosshair.gameObject.SetActive(true);
        }
        
        void Update()
        {
            if (Grabbing)
            {
                LineRenderer.SetPosition(0, Anchor.position);
                LineRenderer.SetPosition(1, _hitPoint);
            }
        }

        void FixedUpdate()
        {
            var grab = Controller.GripButtonState.Active;

            if (Controller.IsKnuckles && Controller.IsXRInputs)
            {
                grab = Controller.PrimaryButtonState.Active;
            }

            if (!Grabbing && !BasicGrabber.Grabbing)
            {
                Aim();
            }
            else if (Grabbing && !Controller.TriggerButtonState.Active)
            {
                LineRenderer.enabled = false;
                Crosshair.SetActive(true);
                Grabbing = false;
                if (Joint)
                {
                    Destroy(Joint);
                }
            }

            if (Grabbing)
            {
                var limits = Joint.linearLimit;

                if (grab)
                {
                    limits.limit = Mathf.MoveTowards(limits.limit, .5f, Time.fixedDeltaTime * PullSpeed);
                }

                Joint.linearLimit = limits;
            }
        }

        private void Aim()
        {
            if (Physics.Raycast(Anchor.position, Anchor.forward, out var hit, Distance, GrabLayer))
            {
                _hitPoint = hit.point;
                _distance = hit.distance;

                if (Controller.TriggerButtonState.Active)
                {
                    Joint = Body.transform.gameObject.AddComponent<ConfigurableJoint>();
                    Joint.xMotion = Joint.yMotion = Joint.zMotion = ConfigurableJointMotion.Limited;
                    Joint.anchor = transform.InverseTransformPoint(Anchor.position);
                    Joint.autoConfigureConnectedAnchor = false;
                    if (hit.rigidbody)
                    {
                        Joint.connectedBody = hit.rigidbody;
                        Joint.connectedAnchor = hit.rigidbody.transform.InverseTransformPoint(hit.point);
                    }
                    else
                    {
                        Joint.connectedAnchor = hit.point;
                    }

               

                    var limits = Joint.linearLimit;
                    limits.limit = hit.distance;
                    Joint.linearLimit = limits;

                    var limit = Joint.linearLimitSpring;
                    limit.spring = Spring;
                    limit.damper = Damper;
                    Joint.linearLimitSpring = limit;

                    Grabbing = true;

                    LineRenderer.enabled = true;
                    Crosshair.SetActive(false);
                }
                else
                {
                    var scale = _distance / Distance * CrossHairMaxScale;
                    scale = Mathf.Clamp(scale, 1, scale);
                    Crosshair.transform.localScale = new Vector3(scale, scale, scale);
                    Crosshair.transform.position = _hitPoint;
                }
            }
            else
            {
                Crosshair.transform.position = Anchor.position + Anchor.forward.normalized * Distance;
                Crosshair.transform.localScale = new Vector3(CrossHairMaxScale, CrossHairMaxScale, CrossHairMaxScale);
            }
        }
    }
}