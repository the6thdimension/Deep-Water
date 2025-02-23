using System;
using System.Collections;
using UnityEngine;

namespace HexabodyVR.PlayerController
{
    public class LocoBallCollision : MonoBehaviour
    {
        public float BodyWeightTime = .25f;

        [Header("Debug")]
        public float AverageBodyWeight;
        public float GroundAngle;
        public float GroundDot;

        public CollisionDetector Collision => _collision;

        private readonly CircularBuffer<float> _buffer = new CircularBuffer<float>(1000);
        private CollisionDetector _collision;

        protected virtual void Awake()
        {
            for (int i = 0; i < _buffer.Capacity; i++)
            {
                _buffer.Enqueue(0f);
            }

            if (!TryGetComponent(out _collision))
            {
                _collision = gameObject.AddComponent<CollisionDetector>();
            }
        }

        private void FixedUpdate()
        {
            GroundAngle = Vector3.Angle(Collision.AverageNormal, Vector3.up);
            GroundDot = Vector3.Dot(Collision.AverageNormal, Vector3.up);
            Average(_buffer, Mathf.Max(0f, Collision.CollisionForce.y), BodyWeightTime, BodyWeightTime, 200000f, ref AverageBodyWeight);
        }

        private void Average(CircularBuffer<float> buffer, float force, float time, float resetTime, float maxAllowed, ref float avg)
        {
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
    }


}