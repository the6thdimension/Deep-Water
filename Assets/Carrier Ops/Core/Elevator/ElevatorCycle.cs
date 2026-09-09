using UnityEngine;
using CarrierOps.Core.State;

namespace CarrierOps.Core.Elevator
{
    /// <summary>
    /// Pure-C# elevator state machine. Simpler than the catapult cycle — three stages
    /// (Stowed / Moving / Deployed). The `Travel` field [0..1] interpolates between the two
    /// limits at <see cref="CarrierProfileData.ElevatorSpeedMps"/> divided by
    /// <see cref="CarrierProfileData.ElevatorTravelM"/> per second.
    ///
    /// **Requests** are level-triggered: <see cref="ElevatorState.CommandUp"/> describes
    /// where the elevator wants to be. Calling <see cref="RequestDeploy"/> /
    /// <see cref="RequestStow"/> sets that flag; the cycle handles the rest each Step.
    /// </summary>
    public static class ElevatorCycle
    {
        public static void RequestDeploy(ref ElevatorState elev) { elev.CommandUp = true; }
        public static void RequestStow(ref ElevatorState elev)   { elev.CommandUp = false; }

        public static void Step(in CarrierProfileData profile, float dt, ref ElevatorState elev)
        {
            float travelM = Mathf.Max(profile.ElevatorTravelM, 0.001f);
            float normalizedRate = profile.ElevatorSpeedMps / travelM;
            float target = elev.CommandUp ? 1f : 0f;

            // No movement needed → snap to the appropriate Stowed/Deployed stage and return.
            if (Mathf.Approximately(elev.Travel, target))
            {
                elev.Stage = target > 0.5f ? ElevatorStage.Deployed : ElevatorStage.Stowed;
                return;
            }

            elev.Travel = Mathf.MoveTowards(elev.Travel, target, normalizedRate * dt);
            elev.Stage = ElevatorStage.Moving;
        }
    }
}
