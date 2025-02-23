using System;
using UnityEngine;

namespace NWH.NPhysics
{
    /// <summary>
    ///     Class for active Rigidbody movement prediction inside single FixedUpdate.
    ///     Allows for per-rigidbody sub-stepping without changing fixedDeltaTime on global level.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    [RequireComponent(typeof(Rigidbody))]
    public class NRigidbody : MonoBehaviour
    {
        private static bool TransformsSyncedThisFrame = false;

        /// <summary>
        ///     Rigidbody to which NRigidbody will be synced to each FixedUpdate.
        /// </summary>
        [UnityEngine.Tooltip("    Rigidbody to which NRigidbody will be synced to each FixedUpdate.")]
        public Rigidbody targetRigidbody;

        /// <summary>
        ///     Cached value of the transform.
        /// </summary>
        [UnityEngine.Tooltip("    Cached value of the transform.")]
        public Transform targetTransform;

        /// <summary>
        ///     Substep dt.
        /// </summary>
        [UnityEngine.Tooltip("    Substep dt.")]
        public float dt;


        /// <summary>
        /// Cached Time.fixedDeltaTime
        /// </summary>
        public float fdt;

        /// <summary>
        ///     Time at the beginning of the latest substep.
        /// </summary>
        [UnityEngine.Tooltip("    Time at the beginning of the latest substep.")]
        public float t;

        /// <summary>
        ///     Mass in [kg].
        /// </summary>
        [UnityEngine.Tooltip("    Mass in [kg].")]
        public float mass;

        /// <summary>
        ///     Angular drag. Equivalent to rigidbody.angularDrag.
        /// </summary>
        [UnityEngine.Tooltip("    Angular drag. Equivalent to rigidbody.angularDrag.")]
        public float angularDrag;

        /// <summary>
        ///     Angular velocity in rad/s.
        /// </summary>
        [UnityEngine.Tooltip("    Angular velocity in rad/s.")]
        public Vector3 angularVelocity;

        /// <summary>
        ///     Center of mass of the body in local coordinates.
        /// </summary>
        [UnityEngine.Tooltip("    Center of mass of the body in local coordinates.")]
        public Vector3 centerOfMass;

        /// <summary>
        ///     Linear drag. Equivalent to rigidbody.drag.
        /// </summary>
        [UnityEngine.Tooltip("    Linear drag. Equivalent to rigidbody.drag.")]
        public float drag;

        /// <summary>
        ///     Inertia of the body. Equivalent to rigidbody.inertiaTensor.
        /// </summary>
        [UnityEngine.Tooltip("    Inertia of the body. Equivalent to rigidbody.inertiaTensor.")]
        public Vector3 inertia;

        /// <summary>
        ///     Position of the NRigidbody in world coordinates.
        /// </summary>
        [UnityEngine.Tooltip("    Position of the NRigidbody in world coordinates.")]
        public Vector3 nPosition;

        /// <summary>
        ///     Rotation of the NRigidbody in world coordinates.
        /// </summary>
        [UnityEngine.Tooltip("    Rotation of the NRigidbody in world coordinates.")]
        public Quaternion nRotation;

        /// <summary>
        ///     NRigidbody velocity in m/s in world coordinates.
        /// </summary>
        [UnityEngine.Tooltip("    NRigidbody velocity in m/s in world coordinates.")]
        public Vector3 velocity;

        /// <summary>
        ///     Equivalent to Rigidbody.useGravity.
        /// </summary>
        [UnityEngine.Tooltip("    Equivalent to Rigidbody.useGravity.")]
        public bool useGravity;

        /// <summary>
        ///     TRS matrix for local to world conversion.
        /// </summary>
        [UnityEngine.Tooltip("    TRS matrix for local to world conversion.")]
        public Matrix4x4 localToWorldMatrix;

        public Matrix4x4 worldToLocalMatrix;

        /// <summary>
        ///     Number of substeps per FixedUpdate.
        /// </summary>
        [SerializeField] private int substeps = 4;
        
        private int _tmpSubsteps = -1;

        private Vector3 _angularImpulse;
        private Vector3 _linearImpulse;
        private Vector3 _totalAngularImpulse;
        private Vector3 _totalLinearImpulse;
        private float   _invMass;
        private Vector3 _invInertia;
        private Vector3 _zeroVector;

        private Vector3    _initNPosition;
        private Quaternion _initNRotation;
        private Vector3    _stepInitNPosition;
        private Quaternion _stepInitNRotation;

        private Vector3 _stepPositionDelta;
        private Vector3 _worldInvInertia;

        /// <summary>
        ///     Sets the number of substeps after the physics update is finished to avoid modifying simulation
        ///     while it is happening.
        /// </summary>
        public int Substeps
        {
            get { return substeps; }
            set { _tmpSubsteps = value < 1 ? 1 : value > 50 ? 50 : value; }
        }

        /// <summary>
        ///     Position delta between this and previous substep.
        /// </summary>
        public Vector3 StepPositionDelta
        {
            get { return _stepPositionDelta; }
        }

        /// <summary>
        ///     Rotation delta between this and previous substep.
        /// </summary>
        public Quaternion StepRotationDelta { get; private set; }

        /// <summary>
        ///     Total position delta between current substep and beginning of FixedUpdate.
        /// </summary>
        public Vector3 TotalPositionDelta { get; private set; }

        /// <summary>
        ///     Total rotation delta between current substep and beginning of FixedUpdate.
        /// </summary>
        public Quaternion TotalRotationDelta { get; private set; }

        /// <summary>
        ///     Current predicted transform position.
        /// </summary>
        public Vector3 TransformPosition
        {
            get 
            {
                Vector3 comOffset = localToWorldMatrix.MultiplyVector(targetRigidbody.centerOfMass);
                Vector3 position;
                position.x = nPosition.x - comOffset.x;
                position.y = nPosition.y - comOffset.y;
                position.z = nPosition.z - comOffset.z;
                return position; 
            }
        }

        /// <summary>
        ///     Current predicted transform rotation.
        /// </summary>
        public Quaternion TransformRotation
        {
            get { return nRotation; }
        }

        /// <summary>
        ///     Current predicted transform up direction.
        /// </summary>
        public Vector3 TransformUp { get; private set; }

        /// <summary>
        ///     Current predicted transform right direction.
        /// </summary>
        public Vector3 TransformRight { get; private set; }

        /// <summary>
        ///     Current predicted transform forward direction.
        /// </summary>
        public Vector3 TransformForward { get; private set; }


        private void Awake()
        {
            targetTransform = transform;
            targetRigidbody = GetComponent<Rigidbody>();
        }


        private void Start()
        {
            SyncAllFromTarget();

            _tmpSubsteps = substeps;
            _totalAngularImpulse = Vector3.zero;
            _totalLinearImpulse  = Vector3.zero;
            _zeroVector          = Vector3.zero;
        }


        private void FixedUpdate()
        {
            if (targetRigidbody.isKinematic) return;

            if (!TransformsSyncedThisFrame)
            {
                Physics.SyncTransforms();
                TransformsSyncedThisFrame = true;
            }

            SyncMovementFromTarget();

            localToWorldMatrix = transform.localToWorldMatrix;
            worldToLocalMatrix = localToWorldMatrix.inverse;

            substeps = _tmpSubsteps;
            bool initSyncTransforms = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;

            float fixedTime      = Time.fixedTime;
            fdt = Time.fixedDeltaTime;
            t = fixedTime;

            if (substeps <= 0)
            {
                substeps = 1;
            }

            dt = fdt / substeps;

            if (dt < 1e-5f)
            {
                return;
            }

            _initNPosition = nPosition;
            _initNRotation = nRotation;

            _worldInvInertia   = localToWorldMatrix.MultiplyVector(_invInertia);
            _worldInvInertia.x = _worldInvInertia.x < 0 ? -_worldInvInertia.x : _worldInvInertia.x;
            _worldInvInertia.y = _worldInvInertia.y < 0 ? -_worldInvInertia.y : _worldInvInertia.y;
            _worldInvInertia.z = _worldInvInertia.z < 0 ? -_worldInvInertia.z : _worldInvInertia.z;

            TransformForward = targetTransform.forward;
            TransformRight   = targetTransform.right;
            TransformUp      = targetTransform.up;

            OnPrePhysicsSubstep?.Invoke(fixedTime, fdt);

            // Substep
            for (int i = 0; i < substeps; i++)
            {
                _stepInitNPosition = nPosition;
                _stepInitNRotation = nRotation;

                OnPhysicsSubstep?.Invoke(t, dt, i);
                Step();

                _stepPositionDelta.x = nPosition.x - _stepInitNPosition.x;
                _stepPositionDelta.y = nPosition.y - _stepInitNPosition.y;
                _stepPositionDelta.z = nPosition.z - _stepInitNPosition.z;

                StepRotationDelta = (nRotation * Quaternion.Inverse(_stepInitNRotation)).normalized;
                localToWorldMatrix *= Matrix4x4.TRS(StepPositionDelta, StepRotationDelta, Vector3.one);
                worldToLocalMatrix = localToWorldMatrix.inverse;

                TransformForward = StepRotationDelta * TransformForward;
                TransformRight   = StepRotationDelta * TransformRight;
                TransformUp      = StepRotationDelta * TransformUp;

                t += dt;
            }

            TotalPositionDelta = nPosition - _initNPosition;
            TotalPositionDelta = nPosition - _initNPosition;
            TotalPositionDelta = nPosition - _initNPosition;

            TotalRotationDelta = (nRotation * Quaternion.Inverse(_initNRotation)).normalized;

            OnPostPhysicsSubstep?.Invoke(t, fdt);

            targetRigidbody.AddForce(_totalLinearImpulse, ForceMode.Impulse);
            targetRigidbody.AddTorque(_totalAngularImpulse, ForceMode.Impulse);
            //SyncMovementToTarget();

            _totalLinearImpulse  = _zeroVector;
            _totalAngularImpulse = _zeroVector;

            Physics.autoSyncTransforms = initSyncTransforms;
        }


        /// <summary>
        ///     Invoked at a beginning of a physics substep.
        ///     t  - equal to Time.fixedTime + (substeps * dt)
        ///     dt - equal to Time.fixedDeltaTime / substeps
        ///     i - current substep
        /// </summary>
        public event Action<float, float, int> OnPhysicsSubstep;

        /// <summary>
        ///     Invoked at the beginning of FixedUpdate(), before substepping.
        ///     Equivalent to FixedUpdate()
        ///     t  - equal to Time.fixedTime
        ///     dt - equal to Time.fixedDeltaTime

        /// </summary>
        public event Action<float, float> OnPrePhysicsSubstep;

        /// <summary>
        ///     Invoked at the end of FixedUpdate(), after substepping.
        ///     t  - equal to Time.fixedTime + Time.fixedDeltaTime
        ///     dt - equal to Time.fixedDeltaTime
        /// </summary>
        public event Action<float, float> OnPostPhysicsSubstep;


        public void AddForceAtPosition(Vector3 force, Vector3 forcePoint, bool isSubstepped = true)
        {
            if (!isSubstepped)
            {
                force *= substeps;
            }

            _linearImpulse.x += force.x;
            _linearImpulse.y += force.y;
            _linearImpulse.z += force.z;

            _angularImpulse += Vector3.Cross(
                new Vector3(
                    forcePoint.x - nPosition.x,
                    forcePoint.y - nPosition.y,
                    forcePoint.z - nPosition.z
                ), force);
        }


        public void AddTorque(Vector3 torque, bool isSubstepped = true)
        {
            if (!isSubstepped)
            {
                torque *= substeps;
            }
            
            _angularImpulse += torque;
        }


        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            Vector3 cross = Vector3.Cross(angularVelocity,
                                          new Vector3(
                                              worldPoint.x - nPosition.x,
                                              worldPoint.y - nPosition.y,
                                              worldPoint.z - nPosition.z));
            return new Vector3(velocity.x + cross.x, velocity.y + cross.y, velocity.z + cross.z);
        }


        private void Step()
        {
            // Apply gravity
            if (useGravity)
            {
                velocity.x += Physics.gravity.x * dt;
                velocity.y += Physics.gravity.y * dt;
                velocity.z += Physics.gravity.z * dt;
            }

            // Apply impulses
            float invMassDt = _invMass * dt;
            velocity.x += _linearImpulse.x * invMassDt;
            velocity.y += _linearImpulse.y * invMassDt;
            velocity.z += _linearImpulse.z * invMassDt;

            angularVelocity.x += _angularImpulse.x * _worldInvInertia.x * dt;
            angularVelocity.y += _angularImpulse.y * _worldInvInertia.y * dt;
            angularVelocity.z += _angularImpulse.z * _worldInvInertia.z * dt;

            // Apply drag
            float dragMultiplier = 1.0f - drag * dt;
            if (dragMultiplier < 0.0f)
            {
                dragMultiplier = 0.0f;
            }

            velocity.x *= dragMultiplier;
            velocity.y *= dragMultiplier;
            velocity.z *= dragMultiplier;

            float dragDt = angularDrag * dt;
            angularVelocity.x -= angularVelocity.x * dragDt;
            angularVelocity.y -= angularVelocity.y * dragDt;
            angularVelocity.z -= angularVelocity.z * dragDt;

            // Apply velocity
            float c = Mathf.Rad2Deg * dt;
            Vector3 eulerRotation;
            eulerRotation.x = angularVelocity.x * c;
            eulerRotation.y = angularVelocity.y * c;
            eulerRotation.z = angularVelocity.z * c;

            nRotation = Quaternion.Euler(eulerRotation) * nRotation;

            nPosition.x += velocity.x * dt;
            nPosition.y += velocity.y * dt;
            nPosition.z += velocity.z * dt;

            _totalLinearImpulse.x += _linearImpulse.x * dt;
            _totalLinearImpulse.y += _linearImpulse.y * dt;
            _totalLinearImpulse.z += _linearImpulse.z * dt;

            _totalAngularImpulse.x += _angularImpulse.x * dt;
            _totalAngularImpulse.y += _angularImpulse.y * dt;
            _totalAngularImpulse.z += _angularImpulse.z * dt;

            // Reset impulses
            _angularImpulse = _zeroVector;
            _linearImpulse  = _zeroVector;
        }


        public void SyncAllFromTarget()
        {
            localToWorldMatrix = targetTransform.localToWorldMatrix;

            useGravity = targetRigidbody.useGravity;

            angularDrag = targetRigidbody.angularDamping;
            angularVelocity = targetRigidbody.angularVelocity;
            centerOfMass = targetRigidbody.centerOfMass;
            mass = targetRigidbody.mass;
            drag = targetRigidbody.linearDamping;
            inertia = targetRigidbody.inertiaTensor;
            nRotation = targetRigidbody.rotation;
            nPosition = targetRigidbody.position + localToWorldMatrix.MultiplyVector(targetRigidbody.centerOfMass);
            velocity = targetRigidbody.linearVelocity;

            _invMass      = 1f / mass;
            _invInertia.x = inertia.x == 0 ? 1e-8f : 1f / inertia.x;
            _invInertia.y = inertia.y == 0 ? 1e-8f: 1f / inertia.y;
            _invInertia.z = inertia.z == 0 ? 1e-8f : 1f / inertia.z;
        }


        private void SyncMovementFromTarget()
        {
            nRotation = targetRigidbody.rotation;
            nPosition = targetRigidbody.position + localToWorldMatrix.MultiplyVector(targetRigidbody.centerOfMass);
            velocity = targetRigidbody.linearVelocity;
            angularVelocity = targetRigidbody.angularVelocity;
            centerOfMass = targetRigidbody.centerOfMass;
            drag = targetRigidbody.linearDamping;
            angularDrag = targetRigidbody.angularDamping;

            mass     = targetRigidbody.mass;
            _invMass = 1f / mass;

            inertia       = targetRigidbody.inertiaTensor;
            _invInertia.x = inertia.x == 0 ? 1e-8f : 1f / inertia.x;
            _invInertia.y = inertia.y == 0 ? 1e-8f: 1f / inertia.y;
            _invInertia.z = inertia.z == 0 ? 1e-8f : 1f / inertia.z;
        }
    }
}