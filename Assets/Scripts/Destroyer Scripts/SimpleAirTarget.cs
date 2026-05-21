using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleAirTarget : MonoBehaviour
{
    public Transform[] Waypoints;
    [Min(1f)] public float Speed = 220f;
    [Min(1f)] public float TurnRateDegPerSec = 45f;
    [Min(1f)] public float ArriveRadius = 200f;
    public int StartIndex = 0;

    private Rigidbody _rb;
    private int _index;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _index = StartIndex;
    }

    private void FixedUpdate()
    {
        if (Waypoints == null || Waypoints.Length == 0)
            return;

        Transform target = Waypoints[_index];
        if (target == null)
            return;

        Vector3 toTarget = target.position - transform.position;
        float dist = toTarget.magnitude;

        if (dist <= ArriveRadius)
        {
            _index = (_index + 1) % Waypoints.Length;
            return;
        }

        Vector3 desiredDir = toTarget / dist;
        Quaternion desiredRot = Quaternion.LookRotation(desiredDir, Vector3.up);
        Quaternion nextRot = Quaternion.RotateTowards(
            _rb.rotation,
            desiredRot,
            TurnRateDegPerSec * Time.fixedDeltaTime);

        Vector3 nextPos = _rb.position + nextRot * Vector3.forward * (Speed * Time.fixedDeltaTime);
        _rb.MovePosition(nextPos);
        _rb.MoveRotation(nextRot);
    }

    private void OnDrawGizmosSelected()
    {
        if (Waypoints == null || Waypoints.Length == 0)
            return;

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.85f);
        for (int i = 0; i < Waypoints.Length; i++)
        {
            Transform a = Waypoints[i];
            Transform b = Waypoints[(i + 1) % Waypoints.Length];
            if (a != null && b != null)
                Gizmos.DrawLine(a.position, b.position);
        }
    }
}
