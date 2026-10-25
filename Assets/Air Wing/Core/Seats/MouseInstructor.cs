using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.Seats
{
    /// <summary>The instructor's gains (AUTHORED; tuned by feel, baked from a <c>PlayerSeatSettingsSO</c>).</summary>
    public struct InstructorSettings
    {
        /// <summary>Stick roll per radian of roll error.</summary>
        public float RollGain;
        /// <summary>Stick pitch per radian of pitch error in the lift plane.</summary>
        public float PitchGain;
        /// <summary>Within this angle of the aim, the instructor blends from "roll toward the aim" to "wings level".</summary>
        public float WingsLevelBlendDeg;
    }

    /// <summary>
    /// Mouse aim (W15): you point, and the instructor works the stick, through the same Pilot Inputs as any other seat.
    /// It rolls the lift toward the aim point and pulls the flight path onto it; near the aim it levels the wings
    /// instead, so a small aim error does not keep the jet rolling. It never touches the throttle. The idea is
    /// MouseFlight's (Brian Hernandez, MIT); this is our own code, written against the wind frame of the L0 airframe.
    /// </summary>
    public sealed class MouseInstructor
    {
        readonly InstructorSettings _s;

        public MouseInstructor(in InstructorSettings settings) => _s = settings;

        /// <summary>Stick pitch and roll that bring the flight path onto <paramref name="aimWorld"/> (a direction).</summary>
        public void Steer(Vector3 aimWorld, in AirframeState state, out float stickPitch, out float stickRoll)
        {
            // The wind frame: forward along the flight path, up along the lift (the body less the angle of attack).
            Quaternion wind = state.Attitude * Quaternion.AngleAxis(state.AngleOfAttack * Units.RadToDeg, Vector3.right);
            Quaternion toLocal = Quaternion.Inverse(wind);
            Vector3 aim = toLocal * aimWorld.normalized;
            Vector3 worldUp = toLocal * Vector3.up;

            float offDeg = Vector3.Angle(Vector3.forward, aim);
            float rollToAim = Mathf.Atan2(aim.x, aim.y);            // roll that puts the aim point straight "up" in the lift plane
            float rollToLevel = Mathf.Atan2(worldUp.x, worldUp.y);  // roll that brings the wings level
            float blend = _s.WingsLevelBlendDeg > 0f ? Mathf.Clamp01(offDeg / _s.WingsLevelBlendDeg) : 1f;
            float rollError = Mathf.Lerp(rollToLevel, rollToAim, blend);

            // Pull only for the part of the error that lies in the lift plane; a point behind the wing waits for the roll.
            float pitchError = Mathf.Atan2(aim.y, aim.z);

            stickRoll = Mathf.Clamp(_s.RollGain * rollError, -1f, 1f);
            stickPitch = Mathf.Clamp(_s.PitchGain * pitchError, -1f, 1f);
        }
    }
}
