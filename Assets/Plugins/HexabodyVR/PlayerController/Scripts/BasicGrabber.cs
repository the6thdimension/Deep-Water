using HexabodyVR.SampleScene;
using UnityEngine;
using UnityEngine.Events;

namespace HexabodyVR.PlayerController
{
    [RequireComponent(typeof(Rigidbody))]
    public class BasicGrabber : MonoBehaviour
    {
        public float Radius = .1f;
        public LayerMask GrabLayer;
        public HexaXRInputs Controller;
        public bool Grabbing;
        public Transform Anchor;
        private readonly Collider[] _colliders = new Collider[100];
        public SpiderHands SpiderHands;

        public ConfigurableJoint Joint { get; private set; }
        public Rigidbody GrabbedBody { get; private set; }

        public UnityEvent Grabbed = new UnityEvent();
        public UnityEvent Released = new UnityEvent();

        void Awake()
        {
            SpiderHands = GetComponent<SpiderHands>();
        }

        void FixedUpdate()
        {
            var grab = Controller.GripButtonState;

            if (Controller.IsKnuckles && Controller.IsXRInputs)
            {
                grab = Controller.PrimaryButtonState;
            }

            if (grab.JustActivated && !Grabbing && !SpiderHands.Grabbing)
            {
                var hits = Physics.OverlapSphereNonAlloc(Anchor.position, Radius, _colliders, GrabLayer, QueryTriggerInteraction.Ignore);
                if (hits > 0)
                {
                    Joint = gameObject.AddComponent<ConfigurableJoint>();
                    Joint.xMotion = Joint.yMotion = Joint.zMotion = ConfigurableJointMotion.Locked;
                    Joint.angularXMotion = Joint.angularYMotion = Joint.angularZMotion = ConfigurableJointMotion.Locked;
                    Joint.anchor = transform.InverseTransformPoint(Anchor.position);
                    Joint.autoConfigureConnectedAnchor = false;
                    var hit = _colliders[0];

                    if (!hit.attachedRigidbody)
                    {
                        for (var index = 0; index < hits; index++)
                        {
                            var col = _colliders[index];
                            if (!col)
                                break;
                            if (col.attachedRigidbody)
                            {
                                hit = col;
                                break;
                            }
                        }
                    }

                    if (hit.attachedRigidbody)
                    {
                        Joint.connectedBody = hit.attachedRigidbody;
                        Joint.connectedAnchor = hit.attachedRigidbody.transform.InverseTransformPoint(Anchor.position);
                    }
                    else
                    {
                        Joint.connectedAnchor = Anchor.position;
                    }

                    GrabbedBody = hit.attachedRigidbody;

                    Grabbing = true;
                    Grabbed.Invoke();
                }
            }
            else if (!grab.Active && Grabbing)
            {
                Grabbing = false;
                if (Joint)
                {
                    Destroy(Joint);
                }

                GrabbedBody = null;
                Released.Invoke();
            }
        }
    }
}