using UnityEngine;

public class SimpleHomingMissile : MonoBehaviour
{
    public float Speed = 600f;             // m/s
    public float TurnRateDeg = 45f;        // deg/s
    public float Lifetime = 12f;
    public float ProximityRadius = 6f;
    public LayerMask HitMask = ~0;

    private Transform _target;
    private System.Action _onKill;
    private float _t;

    public void Launch(Transform target, System.Action onKill)
    {
        _target = target;
        _onKill = onKill;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= Lifetime) { Destroy(gameObject); return; }

        Vector3 fwd = transform.forward;
        Vector3 aim = fwd;
        if (_target)
        {
            Vector3 toT = (_target.position - transform.position).normalized;
            float maxRad = TurnRateDeg * Mathf.Deg2Rad * Time.deltaTime;
            aim = Vector3.RotateTowards(fwd, toT, maxRad, 0f).normalized;
        }

        transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
        Vector3 next = transform.position + aim * Speed * Time.deltaTime;

        // Proximity fuse
        if (_target && Vector3.Distance(next, _target.position) <= ProximityRadius)
        {
            _onKill?.Invoke();
            Destroy(gameObject);
            return;
        }

        // Simple collision
        if (Physics.Linecast(transform.position, next, out var hit, HitMask))
        {
            // If we hit target, call kill
            if (_target && hit.collider.transform.IsChildOf(_target))
                _onKill?.Invoke();

            Destroy(gameObject);
            return;
        }

        transform.position = next;
    }
}
