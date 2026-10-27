using UnityEngine;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Modules.Input;

namespace DeepWater.AirWing.ScriptableObjects
{
    /// <summary>
    /// How the player's devices feel (W10, W15, W27): deadzone, mouse sensitivity, throttle rate, the afterburner
    /// detent and the mouse instructor's gains. All AUTHORED, tuned by feel; none of it reaches the flight model
    /// except as Pilot Inputs.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlayerSeatSettings", menuName = "Deep Water/Air Wing/Player Seat Settings", order = 30)]
    public class PlayerSeatSettingsSO : ScriptableObject
    {
        [Header("Devices")]
        [Tooltip("On top of Unity's own stick deadzone (0.125, rescaled): below this a flight axis reads zero and does not take the seat.")]
        [Range(0f, 0.5f)] public float deadzone = 0.05f;
        [Tooltip("Mouse aim, degrees per pixel.")]
        public float mouseDegreesPerPixel = 0.08f;
        [Tooltip("Mouse travel per tick that counts as moving the mouse, pixels.")]
        public float mouseMoveThresholdPixels = 0.5f;
        [Tooltip("Change in the stick's lever (0 … 1) that counts as moving it.")]
        public float leverMoveThreshold = 0.01f;
        public bool invertLever;

        [Header("Throttle")]
        [Tooltip("Lever travel per second at full throttle-up or -down (keys, triggers).")]
        public float leverRatePerSecond = 0.5f;
        [Tooltip("Lever position of the afterburner detent (0 … 1); 1 means no afterburner on the lever.")]
        [Range(0.5f, 1f)] public float afterburnerDetent = 0.9f;

        [Header("Mouse instructor (W15)")]
        public float rollGain = 2.5f;
        public float pitchGain = 4f;
        public float wingsLevelBlendDeg = 5f;

        [Header("Level flight (W54)")]
        [Tooltip("Keyboard or pad: with the stick centred this long (s), hold level flight on the heading it had. 0: off.")]
        public float levelHoldDelayS = 0.4f;
        [Tooltip("Mouse: an aim within this angle of the horizon snaps to level once the mouse stops (degrees). 0: off.")]
        public float aimLevelDetentDeg = 2f;
        [Tooltip("The level hold's bank limit (degrees) and G limit.")]
        public float holdMaxBankDeg = 45f;
        public float holdMaxG = 3f;
        [Tooltip("The level hold's height gain (flight-path degrees per metre) and its climb and dive limits (degrees).")]
        public float holdClimbDegPerM = 0.06f;
        public float holdMaxPathDeg = 10f;
        [Tooltip("The level hold's G per degree of flight-path error.")]
        public float holdGPerPathDeg = 0.2f;

        public PlayerSeatSettings BakeSeat() => new PlayerSeatSettings
        {
            LeverRatePerSecond = leverRatePerSecond,
            AfterburnerDetent = afterburnerDetent,
            Instructor = new InstructorSettings { RollGain = rollGain, PitchGain = pitchGain, WingsLevelBlendDeg = wingsLevelBlendDeg },
            LevelHoldDelayS = levelHoldDelayS,
            AimLevelDetentDeg = aimLevelDetentDeg,
            // The AI's hands (A2) as the hold: the L1 law's classic gain 2, roll 2 per degree; the ground guard off (a
            // player's jet is the player's to fly into the ground, Air Wing Q5).
            Hold = new HandsSettings
            {
                GuidanceGain = 2f, MaxBankDeg = holdMaxBankDeg, RollRatePerBankDeg = 2f,
                ClimbDegPerM = holdClimbDegPerM, MaxClimbDeg = holdMaxPathDeg, MaxDiveDeg = holdMaxPathDeg, GPerPathDeg = holdGPerPathDeg,
                MaxG = holdMaxG, FloorAglM = 0f, GuardLookAheadS = 0f,
            },
        };

        public ReaderSettings BakeReader() => new ReaderSettings
        {
            Deadzone = deadzone,
            MouseDegreesPerPixel = mouseDegreesPerPixel,
            MouseMoveThresholdPixels = mouseMoveThresholdPixels,
            LeverMoveThreshold = leverMoveThreshold,
            InvertLever = invertLever,
        };
    }
}
