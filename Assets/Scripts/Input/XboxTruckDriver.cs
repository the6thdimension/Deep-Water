using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepWater.Input
{
    // Input is sampled per frame; wheel forces are applied only on the physics clock.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class XboxTruckDriver : MonoBehaviour
    {
        [SerializeField] private TruckDriveProfile profile;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private WheelCollider[] wheels;
        [SerializeField] private Transform[] wheelVisuals;
        private Rigidbody body;
        private InputActionAsset actions;
        private InputAction steerAction, forwardAction, reverseAction, brakeAction;
        private Quaternion[] visualOffsets;
        private float steer, forward, reverse;
        private bool brake;
        public float SpeedMetersPerSecond => body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (profile == null || inputActions == null || wheels == null || wheels.Length != 6 || wheelVisuals == null || wheelVisuals.Length != wheels.Length)
            {
                Debug.LogError("Truck requires a drive profile, driving actions and six paired wheels.", this);
                enabled = false;
                return;
            }
            body.mass = profile.massKg;
            body.centerOfMass = profile.centerOfMass;
            visualOffsets = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                visualOffsets[i] = Quaternion.Inverse(wheels[i].transform.rotation) * wheelVisuals[i].rotation;
            actions = Instantiate(inputActions);
            steerAction = actions.FindAction("Drive/Steer", true);
            forwardAction = actions.FindAction("Drive/Accelerate", true);
            reverseAction = actions.FindAction("Drive/BrakeReverse", true);
            brakeAction = actions.FindAction("Drive/Handbrake", true);
        }

        private void OnEnable() { if (actions != null) actions.Enable(); }
        private void OnDisable()
        {
            if (actions != null) actions.Disable();
            steer = forward = reverse = 0f;
            if (wheels != null) foreach (var wheel in wheels) if (wheel != null)
            { wheel.motorTorque = 0f; wheel.brakeTorque = profile != null ? profile.brakeTorquePerWheel : 0f; }
        }
        private void OnDestroy() { if (actions != null) Destroy(actions); }
        private void Update()
        {
            steer = steerAction.ReadValue<float>();
            forward = forwardAction.ReadValue<float>();
            reverse = reverseAction.ReadValue<float>();
            brake = brakeAction.IsPressed();
        }
        private void FixedUpdate()
        {
            float speed = SpeedMetersPerSecond;
            float demand = forward - reverse;
            bool changingDirection = demand * speed < 0f && Mathf.Abs(speed) > profile.directionChangeSpeed;
            bool stopping = brake || changingDirection || (forward > 0f && reverse > 0f);
            bool atLimit = demand > 0f ? speed >= profile.maxForwardSpeed : speed <= -profile.maxReverseSpeed;
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                wheel.steerAngle = Mathf.MoveTowards(wheel.steerAngle, i < 2 ? steer * profile.maxSteerAngle : 0f, profile.steeringRate * Time.fixedDeltaTime);
                wheel.motorTorque = stopping || atLimit ? 0f : demand * profile.motorTorquePerWheel;
                wheel.brakeTorque = stopping ? profile.brakeTorquePerWheel : 0f;
            }
        }
        private void LateUpdate()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].GetWorldPose(out var position, out var rotation);
                wheelVisuals[i].SetPositionAndRotation(position, rotation * visualOffsets[i]);
            }
        }
    }
}
