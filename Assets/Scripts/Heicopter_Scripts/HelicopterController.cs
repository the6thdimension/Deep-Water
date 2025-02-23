using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class HelicopterConfig
{
    public float weight = 1000f; // Helicopter weight in kg
    public float mainRotorPower = 1000f; // Thrust power of the main rotor
    public float tailRotorPower = 200f; // Power for the tail rotor
    public float stabilizerPower = 300f; // Power for stabilizers
    public float maxTiltAngle = 30f; // Maximum tilt angle in degrees
}

public class HelicopterController : MonoBehaviour
{
    [Header("Helicopter Settings")]
    public HelicopterConfig helicopterConfig;

    [Header("Input Action Asset")]
    public InputActionAsset inputActions;

    [Header("Physics Components")]
    public Rigidbody helicopterRigidbody;

    [Header("Rotor Components")]
    public Transform mainRotor;
    public Transform tailRotor;

    private InputAction rollAction;
    private InputAction pitchAction;
    private InputAction yawAction;
    private InputAction throttleAction;
    private InputAction powerAction;

    private float currentThrottle;
    private bool isPoweredOn = true;

    public delegate void HelicopterEvent();
    public event HelicopterEvent OnPowerOn;
    public event HelicopterEvent OnPowerOff;
    public event HelicopterEvent OnThrottleChange;

    private void Awake()
    {
        // Assign input actions from the Input Action Asset
        var actionMap = inputActions.FindActionMap("HELOAIR");
        rollAction = actionMap.FindAction("RH_HeloRollAxis");
        pitchAction = actionMap.FindAction("RH_HeloPitchAxis");
        yawAction = actionMap.FindAction("RH_HeloYawAxis");
        throttleAction = actionMap.FindAction("RH_HeloThrottle");
        powerAction = actionMap.FindAction("Ignition");
    }

    private void OnEnable()
    {
        rollAction.Enable();
        pitchAction.Enable();
        yawAction.Enable();
        throttleAction.Enable();
        powerAction.Enable();
        powerAction.performed += HandlePowerToggle;
    }

    private void OnDisable()
    {
        rollAction.Disable();
        pitchAction.Disable();
        yawAction.Disable();
        throttleAction.Disable();
        powerAction.Disable();
        powerAction.performed -= HandlePowerToggle;
    }

    private void Start()
    {
        if (helicopterRigidbody == null)
        {
            helicopterRigidbody = GetComponent<Rigidbody>();
        }

        // Set the helicopter's mass to reflect its weight
        helicopterRigidbody.mass = helicopterConfig.weight;
    }

    private void Update()
    {
        if (isPoweredOn)
        {
            RotateRotors();
        }
    }

    private void FixedUpdate()
    {
        if (!isPoweredOn) return;

        // Gather joystick inputs
        float rollInput = rollAction.ReadValue<float>(); // Roll
        float pitchInput = pitchAction.ReadValue<float>(); // Pitch
        float yawInput = yawAction.ReadValue<float>(); // Yaw
        float newThrottle = Mathf.Clamp(throttleAction.ReadValue<float>(), 0f, 1f); // Throttle

        if (!Mathf.Approximately(newThrottle, currentThrottle))
        {
            currentThrottle = newThrottle;
            OnThrottleChange?.Invoke();
        }

        // Apply forces and torques
        ApplyMainRotorForce(currentThrottle);
        ApplyTiltForces(rollInput, pitchInput);
        ApplyTailRotorForce(yawInput);
    }

    private void ApplyMainRotorForce(float throttle)
    {
        // Calculate upward thrust from the main rotor
        float thrust = throttle * helicopterConfig.mainRotorPower;
        Vector3 upwardForce = Vector3.up * thrust;
        helicopterRigidbody.AddForce(upwardForce, ForceMode.Force);

        // Apply gravity compensation
        Vector3 gravityCompensation = Vector3.up * Physics.gravity.magnitude * helicopterRigidbody.mass;
        helicopterRigidbody.AddForce(gravityCompensation, ForceMode.Force);
    }

    private void ApplyTiltForces(float rollInput, float pitchInput)
    {
        // Convert inputs to tilt angles
        float targetRollAngle = rollInput * helicopterConfig.maxTiltAngle;
        float targetPitchAngle = pitchInput * helicopterConfig.maxTiltAngle;

        // Calculate the tilt forces
        Vector3 tiltForce = new Vector3(targetPitchAngle, 0, -targetRollAngle) * helicopterConfig.stabilizerPower;
        helicopterRigidbody.AddRelativeTorque(tiltForce, ForceMode.Force);
    }

    private void ApplyTailRotorForce(float yawInput)
    {
        // Calculate yaw torque from the tail rotor
        float yawTorque = yawInput * helicopterConfig.tailRotorPower;
        helicopterRigidbody.AddTorque(Vector3.up * yawTorque, ForceMode.Force);
    }

    private void RotateRotors()
    {
        if (mainRotor != null)
        {
            mainRotor.Rotate(Vector3.up, 1000f * Time.deltaTime * currentThrottle, Space.Self);
        }
        if (tailRotor != null)
        {
            tailRotor.Rotate(Vector3.right, 1000f * Time.deltaTime * currentThrottle, Space.Self);
        }
    }

    public void PowerOn()
    {
        isPoweredOn = true;
        OnPowerOn?.Invoke();
    }

    public void PowerOff()
    {
        isPoweredOn = false;
        currentThrottle = 0f;
        OnPowerOff?.Invoke();
    }

    private void HandlePowerToggle(InputAction.CallbackContext context)
    {
        if (isPoweredOn)
        {
            PowerOff();
        }
        else
        {
            PowerOn();
        }
    }

    public void LoadConfig(HelicopterConfig config)
    {
        helicopterConfig = config;
        helicopterRigidbody.mass = helicopterConfig.weight;
    }
}
