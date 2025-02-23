using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexabodyVR.PlayerController
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract class HexaHandsBase : MonoBehaviour
    {
        public Rigidbody ParentRigidBody;

        [Header("Required Components")]

        [Tooltip("Target transform for position and rotation tracking")]
        public Transform Target;

        [Tooltip("Anchor point for judging max arm length")]
        public Transform Shoulder;

        public HexaHandsBase OtherHand;
        public HexaBodyPlayer4 HexaBody;
        public Transform Pelvis;


        [Header("Settings")]

        [Tooltip("The physics hands will not chase the controller beyond this distance from the supplied shoulder transform.")]
        public float ArmLength = .75f;

        public int SolverIterations = 10;
        public int SolverVelocityIterations = 10;
        public float InertiaTensor = .02f;

        [Header("Stuck Detection")]
        public bool EnableStuckDetection;
        public float StuckForce = 800f;
        public float StuckTime = .5f;
        public float StuckResetTime = .125f;
        public float DistanceThreshold = .3f;

        [Header("Downward Stuck Detection")]
        public bool EnableDownwardStuckDetection;
        public float DownStuckTime = 1f;
        public float DownStuckResetTime = .2f;
        public float DownStuckRatio = .95f;
        public float DownDistanceThreshold = .5f;
        public float DownMaxAllowedForce = 1200f;


        [Tooltip("Environment layers to block hand after resetting")]
        public LayerMask UnstuckHandLayerIgnore = 1 << 8;



        [Tooltip("If not assigned, it will auto populate with all child colliders.")]
        public Collider[] HandColliders;


        [Header("Debug")]
        public HandGrabState HandState;
        public StrengthState StrengthState;

        public float MaxStuckForce;
        public float MaxDownwardStuck;

        public float AverageStuckForce;
        public float AverageDownwardStuck;

        private Vector3 _previousControllerPosition;
        private Quaternion _previousRotation;
        private Quaternion _jointOffset;

        private bool _hasHexa;

        public abstract bool IsLeft { get; }

        public virtual float BodyMass => HexaBody ? HexaBody.CombinedMass : 80f;

        public float BodyWeight => Mathf.Abs(Physics.gravity.y) * BodyMass;

        private readonly CircularBuffer<float> PullBuffer = new CircularBuffer<float>(1000);
        private readonly CircularBuffer<float> DownwardStuckBuffer = new CircularBuffer<float>(1000);
        private readonly CircularBuffer<float> HeldForceBuffer = new CircularBuffer<float>(1000);
        private readonly CircularBuffer<float> HandForceBuffer = new CircularBuffer<float>(1000);

        public Rigidbody RigidBody { get; private set; }

        public ConfigurableJoint Joint { get; protected set; }

        public CollisionDetector Collision => _collision;

        private CollisionDetector _collision;

        protected virtual void Awake()
        {
            RigidBody = GetComponent<Rigidbody>();
            SetupJoint();

            RigidBody.inertiaTensorRotation = Quaternion.identity;
            RigidBody.inertiaTensor = new Vector3(InertiaTensor, InertiaTensor, InertiaTensor);
            RigidBody.maxAngularVelocity = 150f;
            RigidBody.solverIterations = SolverIterations;
            RigidBody.solverVelocityIterations = SolverVelocityIterations;

            if (!HexaBody)
            {
                HexaBody = transform.root.GetComponentInChildren<HexaBodyPlayer4>();
            }

            _hasHexa = HexaBody;


            if (!OtherHand)
            {
                OtherHand = transform.root.GetComponentsInChildren<HexaHandsBase>().FirstOrDefault(e => e != this);
            }

            if (!Pelvis)
            {
                if (!_hasHexa)
                {
                    var hexa3 = transform.root.GetComponentInChildren<HexaBodyPlayer3>();
                    if (hexa3)
                    {
                        Pelvis = hexa3.Torso.transform;
                    }
                }
                else
                {
                    Pelvis = HexaBody.Pelvis.transform;
                }
            }

            if (!Pelvis)
            {
                Debug.LogWarning($"{name} needs Pelvis transform assigned.");
            }


            if (StuckForce < 10f)
                StuckForce = BodyWeight;

            for (int i = 0; i < PullBuffer.Capacity; i++)
            {
                PullBuffer.Enqueue(0f);
            }

            for (int i = 0; i < DownwardStuckBuffer.Capacity; i++)
            {
                DownwardStuckBuffer.Enqueue(0f);
            }
            for (int i = 0; i < HandForceBuffer.Capacity; i++)
            {
                HandForceBuffer.Enqueue(0f);
            }

            for (int i = 0; i < HeldForceBuffer.Capacity; i++)
            {
                HeldForceBuffer.Enqueue(0f);
            }

            if (HandColliders == null || HandColliders.Length == 0)
            {
                HandColliders = GetComponentsInChildren<Collider>().Where(e => e && !e.isTrigger).ToArray();
            }

            StartCoroutine(CleanColliders());

            if (!TryGetComponent(out _collision))
                _collision = gameObject.AddComponent<CollisionDetector>();
        }

        protected virtual void OnEnable()
        {
            TakeJointOffset();
        }

        private IEnumerator CleanColliders()
        {
            yield return null;
            HandColliders = HandColliders.Where(e => e).ToArray();
        }

        public void TakeJointOffset()
        {
            _jointOffset = Quaternion.Inverse(Quaternion.Inverse(ParentRigidBody.rotation) * transform.rotation);
        }

        protected virtual void SetupJoint()
        {
            Joint = ParentRigidBody.transform.gameObject.AddComponent<ConfigurableJoint>();
            Joint.connectedBody = RigidBody;
            Joint.autoConfigureConnectedAnchor = false;
            Joint.anchor = Vector3.zero;
            Joint.connectedAnchor = Vector3.zero;
            Joint.rotationDriveMode = RotationDriveMode.Slerp;
        }

        protected virtual void FixedUpdate()
        {
            UpdateJointAnchors();
            UpdateRotation();
            UpdateTargetVelocity();
            UpdateHandStrength();
            if (EnableStuckDetection || EnableDownwardStuckDetection) UpdateStuckDetection();
        }

        protected virtual void UpdateHandStrength()
        {

        }

        protected virtual bool CanUnstuck() => false;

        private void UpdateStuckDetection()
        {
            var canUnstuck = CanUnstuck();

            var targetDir = (transform.position - Target.position).normalized;
            var distance = Vector3.Distance(transform.position, Target.position);
            if (EnableStuckDetection && StuckDetection(targetDir, distance) && canUnstuck)
            {
                AverageStuckForce = 0f;
                Debug.Log("unstuck");
                Unstuck();
                return;
            }

            if (EnableDownwardStuckDetection && DownwardStuckDetection(targetDir, distance) && canUnstuck)
            {
                AverageDownwardStuck = 0f;
                Debug.Log("down unstuck");
                Unstuck();
            }
        }

        private void Average(CircularBuffer<float> buffer, float force, float time, float resetTime, float maxAllowed, ref float avg, ref float max)
        {
            max = Mathf.Max(force, max);
            force = Mathf.Min(maxAllowed, force);

            buffer.Enqueue(force);

            var total = 0f;

            if (Time.fixedDeltaTime < .00000001f)
                return;

            var gain = force > avg;

            var frames = (gain ? time : resetTime) / Time.fixedDeltaTime;

            for (int i = 0; i < frames; i++)
            {
                if (i < buffer.Capacity)
                    total += buffer[i];
            }

            avg = total / frames;
        }

        protected virtual bool StuckDetection(Vector3 dir, float distance)
        {
            var bodyDir = Pelvis.position - transform.position;

            if (distance < DistanceThreshold || Vector3.Dot(bodyDir, -dir) < 0f)
            {
                AverageStuckForce = 0f;
                PullBuffer.Enqueue(0f);
                return false;
            }

            var collisionForce = Collision.CollisionForce;
            collisionForce.y = Mathf.Min(0f, collisionForce.y);
            var force = Vector3.Dot(dir, collisionForce);

            var gain = Mathf.Min(MaxStuckForce, force) > AverageStuckForce;

            Average(PullBuffer, force, StuckTime, StuckResetTime, StuckForce * 2f, ref AverageStuckForce, ref MaxStuckForce);

            return AverageStuckForce > StuckForce && gain;
        }

        protected virtual bool DownwardStuckDetection(Vector3 dir, float distance)
        {
            if (distance < DownDistanceThreshold)
            {
                AverageDownwardStuck = 0f;
                DownwardStuckBuffer.Enqueue(0f);
                return false;
            }

            var collisionForce = Collision.CollisionForce;
            collisionForce.y = Mathf.Max(0f, collisionForce.y);
            var force = Vector3.Dot(dir, collisionForce);

            var gain = Mathf.Min(DownMaxAllowedForce, force) > AverageDownwardStuck;

            Average(DownwardStuckBuffer, force, DownStuckTime, DownStuckResetTime, DownMaxAllowedForce, ref AverageDownwardStuck, ref MaxDownwardStuck);

            return AverageDownwardStuck > DownStuckRatio * BodyWeight && gain;
        }

        protected virtual void Unstuck()
        {
            var target = RigidBody.position;
            var origin = Pelvis;
            var direction = (target - origin.position).normalized;
            var length = Vector3.Distance(target, origin.position);

            RigidBody.position = origin.position;

            var bounds = GetColliderBounds(HandColliders);
            var maxSide = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            var start = bounds.center - direction * maxSide * 1.05f;
            var rbDelta = bounds.center - RigidBody.position;

            if (Physics.BoxCast(start, bounds.extents, direction, out var hit, Quaternion.identity, length, UnstuckHandLayerIgnore, QueryTriggerInteraction.Ignore))
            {
                //Debug.Log($"hit {hit.distance} {hit.collider.name}");
                RigidBody.position += direction * (hit.distance - maxSide) + rbDelta;
                return;
            }

            //  Debug.Log($"no hit {length}");
            RigidBody.position = target;
            transform.position = RigidBody.position;
        }

        protected virtual void UpdateHandState()
        {
            var state = GetHandState();
            SetHandState(state);
        }

        protected virtual HandGrabState GetHandState()
        {
            return HandGrabState.None;
        }

        protected virtual void SetHandState(HandGrabState newState)
        {
            if (newState == HandState)
                return;

            var old = HandState;
            HandState = newState;

            OnHandStateChanged(newState, old);
        }

        protected virtual void OnHandStateChanged(HandGrabState newState, HandGrabState oldState)
        {
        }

        protected virtual void UpdateRotation()
        {
            Joint.targetRotation = Quaternion.Inverse(ParentRigidBody.rotation) * Target.rotation * _jointOffset;
        }

        protected void UpdateJointAnchors()
        {
            var localTargetPosition = ParentRigidBody.transform.InverseTransformPoint(Target.position);

            if (Shoulder)
            {
                var localAnchor = ParentRigidBody.transform.InverseTransformPoint(Shoulder.position);
                var dir = localTargetPosition - localAnchor;
                dir = Vector3.ClampMagnitude(dir, ArmLength);

                var point = localAnchor + dir;
                Joint.targetPosition = point;
            }
            else
            {
                Joint.targetPosition = localTargetPosition;
            }
        }

        protected virtual void UpdateTargetVelocity()
        {
            var local = ParentRigidBody.transform.InverseTransformPoint(Target.position);

            var tangentialV = Vector3.zero;
            var hmdV = Vector3.zero;
            if (_hasHexa)
            {
                var newPos = Quaternion.AngleAxis(HexaBody.TurnSpeed, Vector3.up) * local;
                tangentialV = (newPos - local) / Time.fixedDeltaTime;
                hmdV = ParentRigidBody.transform.InverseTransformDirection(HexaBody.HMDSpeed);
            }

            var velocity = (local - _previousControllerPosition) / Time.fixedDeltaTime;
            _previousControllerPosition = local;
            Joint.targetVelocity = velocity + tangentialV + hmdV;

            var angularVelocity = AngularVelocity(Target.rotation, _previousRotation);
            Joint.targetAngularVelocity = Quaternion.Inverse(ParentRigidBody.transform.rotation) * angularVelocity;

            _previousRotation = Target.rotation;
        }

        protected virtual void SetStrengthState(StrengthState state)
        {
            if (StrengthState == state)
                return;

            StrengthState = state;

            SetStrength(state);
        }

        protected abstract void SetStrength(StrengthState state);


        public static Vector3 AngularVelocity(Quaternion current, Quaternion previous)
        {
            var deltaRotation = current * Quaternion.Inverse(previous);
            if (deltaRotation.w < 0)
            {
                deltaRotation.x = -deltaRotation.x;
                deltaRotation.y = -deltaRotation.y;
                deltaRotation.z = -deltaRotation.z;
                deltaRotation.w = -deltaRotation.w;
            }

            deltaRotation.ToAngleAxis(out var angle, out var axis);
            angle *= Mathf.Deg2Rad;
            return axis * (angle / Time.fixedDeltaTime);
        }

        public static Bounds GetColliderBounds(Collider[] colliders)
        {
            var bounds = new Bounds();
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (i == 0)
                {
                    bounds = collider.bounds;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return bounds;
        }
    }
}