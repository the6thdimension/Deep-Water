using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HexabodyVR.PlayerController
{
    public class CollisionDetector : MonoBehaviour
    {
        public bool IsColliding;
        public bool PreviousCollided;
        public bool JustUncollided;

        public Vector3 AverageNormal;
        public Vector3 CollisionImpulse;
        public Vector3 CollisionForce;
        public Vector3 MaxImpulse;

        public Component[] CollisionHandlers;
        private ICollisionHandler[] _handlers;
        private readonly CircularBuffer<Vector3> _buffer = new CircularBuffer<Vector3>(1000);

        protected virtual void Awake()
        {
            if (CollisionHandlers == null || CollisionHandlers.Length == 0)
            {
                _handlers = GetComponents<ICollisionHandler>();
            }
            else
            {
                var h = new List<ICollisionHandler>();
                foreach (var c in CollisionHandlers)
                {
                    if (c is ICollisionHandler ch)
                    {
                        h.Add(ch);
                    }
                }

                _handlers = h.ToArray();
            }

            for (int i = 0; i < _buffer.Capacity; i++)
            {
                _buffer.Enqueue(Vector3.zero);
            }
        }

        private void FixedUpdate()
        {
            CollisionForce = CollisionImpulse / Time.fixedDeltaTime;
            _buffer.Enqueue(CollisionForce);

            JustUncollided = PreviousCollided && !IsColliding;
            PreviousCollided = IsColliding;
            IsColliding = false;
            CollisionImpulse = Vector3.zero;
            AverageNormal = Vector3.zero;
        }

        protected virtual void OnCollisionEnter(Collision other)
        {
            HandleCollision(other);
            foreach (var handler in _handlers)
            {
                handler.HandleCollision(other, CollisionMethod.OnEnter);
            }
        }

        protected virtual void OnCollisionStay(Collision other)
        {
            HandleCollision(other);
            foreach (var handler in _handlers)
            {
                handler.HandleCollision(other, CollisionMethod.OnStay);
            }
        }

        protected virtual void HandleCollision(Collision other)
        {
            var impulse = other.impulse;

            //ignore tick tack collisions
            if (impulse.sqrMagnitude < .001f)
                return;

            IsColliding = true;

            var normal = other.GetContact(0).normal;

            //impulse sometimes isn't being reported in the correct direction apparently....
            //https://forum.unity.com/threads/inconsistency-with-collision-impulse.936728/
            if (Vector3.Dot(normal, impulse) < 0f)
            {
                impulse *= -1f;
            }

            CollisionImpulse += impulse;
            
            AverageNormal += normal;

            MaxImpulse.x = Mathf.Max(MaxImpulse.x, impulse.x);
            MaxImpulse.y = Mathf.Max(MaxImpulse.y, impulse.y);
            MaxImpulse.z = Mathf.Max(MaxImpulse.z, impulse.z);
        }

        public float Average(float lookBackTime, Vector3 min, Vector3 max)
        {
            var total = Vector3.zero;

            if (Time.fixedDeltaTime < .00000001f)
                return 0f;

            var frames = lookBackTime / Time.fixedDeltaTime;

            for (int i = 0; i < frames; i++)
            {
                if (i < _buffer.Capacity)
                {
                    var sample = _buffer[i];
                    sample.x = Mathf.Clamp(1f, min.x, max.x) * sample.x;
                    sample.y = Mathf.Clamp(1f, min.y, max.y) * sample.y;
                    sample.z = Mathf.Clamp(1f, min.z, max.z) * sample.z;
                    total += sample;
                }
            }

            return total.magnitude / frames;
        }
    }

    public interface ICollisionHandler
    {
        void HandleCollision(Collision c, CollisionMethod method);
    }

    public enum CollisionMethod
    {
        OnEnter, OnStay
    }
}