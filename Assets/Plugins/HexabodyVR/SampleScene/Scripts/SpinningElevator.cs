using UnityEngine;

namespace HexabodyVR.SampleScene
{
    [RequireComponent(typeof(Rigidbody))]
    public class SpinningElevator : MonoBehaviour
    {
        public Rigidbody Rigidbody;
        public float AnglesPerSecond = 70;


        private void Start()
        {
            Rigidbody = gameObject.GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Rigidbody.MoveRotation(transform.rotation * Quaternion.Euler(0, AnglesPerSecond * Time.fixedDeltaTime, 0));
        }
    }
}