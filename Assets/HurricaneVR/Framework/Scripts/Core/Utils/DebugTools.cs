using UnityEngine;

namespace HurricaneVR.Framework.Core.Utils
{
    public class DebugTools : MonoBehaviour
    {

        public float MaxVelocity;
        public Rigidbody Rigidbody;
        public float VelocityCap = 3f;
        public bool CapVelocity;

        void Start()
        {
            if (!Rigidbody)
            {
                Rigidbody = GetComponent<Rigidbody>();
            }
        }

        private void FixedUpdate()
        {
            if (Rigidbody)
            {
                if (CapVelocity)
                {
                    var velocity = Rigidbody.linearVelocity;
                    velocity.x = Mathf.Clamp(velocity.x, -VelocityCap, VelocityCap);
                    velocity.y = Mathf.Clamp(velocity.y, -VelocityCap, VelocityCap);
                    velocity.z = Mathf.Clamp(velocity.z, -VelocityCap, VelocityCap);
                    Rigidbody.linearVelocity = velocity;
                }

                if (Rigidbody.linearVelocity.magnitude > MaxVelocity)
                {
                    MaxVelocity = Rigidbody.linearVelocity.magnitude;
                }

           
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            //Debug.Log($"Impulse: {other.impulse}. Velocity: {other.relativeVelocity}");
        }
    }
}
