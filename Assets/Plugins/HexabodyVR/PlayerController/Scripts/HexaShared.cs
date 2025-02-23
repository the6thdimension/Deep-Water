using System;
using UnityEngine;

namespace HexabodyVR.PlayerController
{


    public static class Kinematics
    {
        public static float DistanceToStop(float v0, float a)
        {
            return (v0 * v0) / (2 * a);
        }

        public static float TimeToStop(float d, float v0)
        {
            return SolveTimeToVelocity(d, v0, 0f);
        }

        public static float SolveTimeToVelocity(float d, float v0, float vFinal)
        {
            return d / ((v0 + vFinal) / 2f);
        }

        public static float SolveTime(float v, float a)
        {
            return v / a;
        }

        public static float GetJumpVelocity(float gravity, float height)
        {
            return Mathf.Sqrt(2f * gravity * height);
        }

        public static float GetHeight(float v0, float gravity)
        {
            return .5f * (v0 * v0 / gravity);
        }

        public static float SolveAccelerationByDistance(float d, float t, float v0)
        {
            return (d - v0 * t) / (.5f * t * t);
        }

        public static float SolveAccelerationByVelocity(float v0, float vFinal, float t)
        {
            return (vFinal - v0) / t;
        }


        public static void Solve(float d, float v0, float a, float vMax, out bool accelerate, out bool deaccelerate)
        {
            deaccelerate = false;
            accelerate = false;

            v0 = Mathf.Abs(v0);
            vMax = Mathf.Abs(vMax);

            var distanceToStop = DistanceToStop(v0, a);
            if (distanceToStop < d)
            {
                if (v0 < vMax)
                {
                    accelerate = true;
                }
            }
            else
            {
                deaccelerate = true;
            }
        }

        //public static void Solve(float d, float v0, float a, float vMax, out bool accelerate, out bool deaccelerate, out float accelerationTime, out float deaccelerationTime)
        //{
        //    accelerationTime = 0f;
        //    deaccelerationTime = 0f;
        //    deaccelerate = false;
        //    accelerate = false;

        //    var distanceToStop = DistanceToStop(v0, a);
        //    if (distanceToStop < d)
        //    {
        //        var remainingAccelerationDistance = d - distanceToStop;
        //        if (v0 < vMax)
        //        {
        //            accelerationTime = SolveTime(remainingAccelerationDistance, v0, vMax);
        //            accelerate = true;
        //        }
        //    }
        //    else
        //    {
        //        deaccelerate = true;
        //        deaccelerationTime = SolveTime(distanceToStop, v0, 0f);
        //    }
        //}
    }

    public enum CrouchLevel
    {
        Standing,
        KneeBent,
        Squat,
        SuperSquat,
        ButtOnTheFloor
    }

    public enum LegStage
    {
        Standing,
        Jumping,
        JumpRetract,
        Landing,
    }

    public enum SitStand
    {
        Sitting,
        Standing
    }

    public enum LegStatus
    {
        Standing,
        Jumping
    }

    public enum JumpStage
    {
        Jumping, Retracting, Landing, Stomping, LandingGrounded
    }

    [Serializable]
    public struct PlayerInputState
    {
        public bool Active;
        public bool JustActivated;
        public bool JustDeactivated;
        public float Value;
    }

    public enum HandGrabState
    {
        None,
        DynamicGrab,
        KinematicGrab,
        Climbing
    }

    public enum StrengthState
    {
        Default,
        OneHandSupport,
        TwoHandSupport,
        OneHandKinematic,
        TwoHandKinematic,
        OneHandClimbing,
        TwoHandClimbing
    }

    [Serializable]
    public class HexaJointDrive
    {
        public float Spring;
        public float Damper;
        public float MaxForce;

        public HexaJointDrive(float spring, float damper, float maxForce)
        {
            Spring = spring;
            Damper = damper;
            MaxForce = maxForce;
        }

        public JointDrive CreateJointDrive()
        {
            var drive = new JointDrive();
            drive.positionSpring = Spring;
            drive.positionDamper = Damper;
            drive.maximumForce = MaxForce;
            return drive;
        }

        public void Apply(ConfigurableJoint joint)
        {
            joint.xDrive = joint.yDrive = joint.zDrive = CreateJointDrive();
        }
    }

    [Serializable]
    public class HexaAngularJointDrive
    {
        public float Spring = 200;
        public float Damper = 10;
        public float MaxForce = 50;

        public JointDrive CreateJointDrive()
        {
            var drive = new JointDrive();
            drive.positionSpring = Spring;
            drive.positionDamper = Damper;
            drive.maximumForce = MaxForce;
            return drive;
        }
    }
}


