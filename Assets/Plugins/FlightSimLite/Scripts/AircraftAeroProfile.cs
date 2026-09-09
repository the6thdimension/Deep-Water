using UnityEngine;

[CreateAssetMenu(fileName = "AircraftAeroProfile", menuName = "FlightSimLite/Aircraft Aero Profile")]
public class AircraftAeroProfile : ScriptableObject
{
    [Header("Reference")]
    public float ReferenceArea = 27.87f;
    public float AirDensity = 1.225f;
    public float SideForceSlope = 0.35f;

    [Header("High-Lift Devices")]
    public float FlapLiftBonus = 0.35f;
    public float FlapDragBonus = 0.05f;

    [Header("Aerodynamic Coefficients")]
    public AnimationCurve LiftCoefficientByAlpha = new AnimationCurve(
        new Keyframe(-20f, -0.6f),
        new Keyframe(-10f, -0.2f),
        new Keyframe(0f, 0.2f),
        new Keyframe(10f, 0.95f),
        new Keyframe(15f, 1.1f),
        new Keyframe(20f, 0.75f),
        new Keyframe(30f, 0.2f));
    public AnimationCurve DragCoefficientByAlpha = new AnimationCurve(
        new Keyframe(-20f, 0.2f),
        new Keyframe(-10f, 0.08f),
        new Keyframe(0f, 0.02f),
        new Keyframe(10f, 0.06f),
        new Keyframe(20f, 0.18f),
        new Keyframe(30f, 0.45f));

    [Header("Control Effectiveness vs Dynamic Pressure (Pa)")]
    public AnimationCurve PitchEffectivenessByQ = new AnimationCurve(
        new Keyframe(0f, 0.15f),
        new Keyframe(1000f, 0.55f),
        new Keyframe(6000f, 1f),
        new Keyframe(12000f, 0.9f));
    public AnimationCurve RollEffectivenessByQ = new AnimationCurve(
        new Keyframe(0f, 0.2f),
        new Keyframe(1000f, 0.6f),
        new Keyframe(5000f, 1f),
        new Keyframe(12000f, 0.85f));
    public AnimationCurve YawEffectivenessByQ = new AnimationCurve(
        new Keyframe(0f, 0.25f),
        new Keyframe(1000f, 0.7f),
        new Keyframe(6000f, 1f),
        new Keyframe(12000f, 0.8f));

    [Header("Actuator Limits")]
    [Tooltip("Max pitch-rate command change (deg/s^2).")]
    public float PitchActuatorAccel = 160f;
    [Tooltip("Max roll-rate command change (deg/s^2).")]
    public float RollActuatorAccel = 600f;
    [Tooltip("Max yaw-rate command change (deg/s^2).")]
    public float YawActuatorAccel = 120f;
}
