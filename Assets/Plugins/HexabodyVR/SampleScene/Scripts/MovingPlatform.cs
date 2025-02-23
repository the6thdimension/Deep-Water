using UnityEngine;

namespace HexabodyVR.SampleScene
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovingPlatform : MonoBehaviour
    {
        public Rigidbody Rigidbody { get; set; }
        public Transform End;
        public float Speed = 3f;
        public float Delay = 5f;
        public float TimeToMaxSpeed = 1f;

        private Vector3 _target;
        private float _speed;
        private float _timer;
        private bool _waiting;
        private Vector3 _start;
        private bool _endTarget;
        private float _elapsed;
    
        void Start()
        {
            Rigidbody = GetComponent<Rigidbody>();
            _target = End.position;
            _start = transform.position;
            _endTarget = true;
        }

        void FixedUpdate()
        {
            if (!_waiting)
            {
                _speed = Mathf.Lerp(0, Speed, _elapsed / TimeToMaxSpeed);
                Rigidbody.MovePosition(Vector3.MoveTowards(Rigidbody.position, _target, _speed * Time.deltaTime));
            }

            if ((_target - Rigidbody.position).magnitude < .01)
            {
                _speed = 0f;
                _target = _endTarget ? _start : End.position;
                _waiting = true;
                _endTarget = !_endTarget;
            }

            _timer += Time.deltaTime;
            _elapsed += Time.deltaTime;
            if (_timer > Delay && _waiting)
            {
                _waiting = false;
                _timer = 0f;
                _elapsed = 0f;
            }
        }
    }
}
