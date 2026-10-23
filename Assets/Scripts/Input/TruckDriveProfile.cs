using UnityEngine;

namespace DeepWater.Input
{
    [CreateAssetMenu(menuName = "Deep Water/Input/Truck Drive Profile")]
    public sealed class TruckDriveProfile : ScriptableObject
    {
        public float massKg = 8000f;
        public Vector3 centerOfMass = new Vector3(0f, 0.8f, 1f);
        public float motorTorquePerWheel = 6000f;
        public float brakeTorquePerWheel = 14000f;
        public float maxForwardSpeed = 22f;
        public float maxReverseSpeed = 6f;
        public float directionChangeSpeed = 0.5f;
        public float maxSteerAngle = 30f;
        public float steeringRate = 65f;
        public float wheelRadius = 0.55f;
        public float suspensionDistance = 0.25f;
        public float suspensionSpring = 110000f;
        public float suspensionDamper = 14000f;
    }
}
