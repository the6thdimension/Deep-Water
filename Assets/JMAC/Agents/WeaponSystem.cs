using UnityEngine;

/// <summary>
/// Minimal weapons for training: raycast gun + simple missile.
/// Emits flags the Agent reads for rewards/termination.
/// </summary>
public class WeaponSystem : MonoBehaviour
{
    [Header("Mounts")]
    public Transform GunMuzzle;
    public Transform MissileRail;
    public LayerMask HitMask = ~0;

    [Header("Gun")]
    public float GunRPM = 3600f;          // rounds/min
    public float GunDamage = 1f;          // arbitrary
    public float GunSpreadDeg = 0.6f;     // accuracy
    public float GunRange = 1200f;        // meters
    public float GunMuzzleVel = 900f;     // m/s (for lead training, we still raycast per step)
    private float _gunCooldown;

    [Header("Missile")]
    public GameObject MissilePrefab;
    public float MissileReload = 6f;
    public int   MissileCount = 2;
    private float _missileCooldown;
    private int   _missilesLeft;

    // Reward flags (consumed by agent per-step)
    private bool _gunHitFlag;
    private bool _missileKillFlag;

    public void ResetSystem()
    {
        _gunCooldown = 0f;
        _missileCooldown = 0f;
        _missilesLeft = MissileCount;
        _gunHitFlag = false;
        _missileKillFlag = false;
    }

    public bool ConsumedGunHitFlag()
    {
        if (_gunHitFlag) { _gunHitFlag = false; return true; }
        return false;
    }
    public bool ConsumedMissileKillFlag()
    {
        if (_missileKillFlag) { _missileKillFlag = false; return true; }
        return false;
    }

    private void Update()
    {
        _gunCooldown  = Mathf.Max(0f, _gunCooldown  - Time.deltaTime);
        _missileCooldown = Mathf.Max(0f, _missileCooldown - Time.deltaTime);
    }

    public void TryFireGun(Aircraft target, out bool hitThisStep)
    {
        hitThisStep = false;
        if (!GunMuzzle) return;

        float timeBetweenRounds = 60f / Mathf.Max(1f, GunRPM);
        if (_gunCooldown > 0f) return;
        _gunCooldown = timeBetweenRounds;

        // Slight spread
        Quaternion spread = Quaternion.Euler(Random.Range(-GunSpreadDeg, GunSpreadDeg),
                                             Random.Range(-GunSpreadDeg, GunSpreadDeg), 0f);
        Vector3 dir = spread * GunMuzzle.forward;

        if (Physics.Raycast(GunMuzzle.position, dir, out var hit, GunRange, HitMask))
        {
            // If we hit the opponent’s collider, count a hit
            if (target && hit.collider.transform.IsChildOf(target.transform))
            {
                hitThisStep = true;
                _gunHitFlag = true;
                // Optional: accumulate health on a Damageable component
                var dmg = hit.collider.GetComponentInParent<SimpleDamageable>();
                if (dmg) { if (dmg.ApplyDamage(GunDamage)) { _missileKillFlag = true; } }
            }
        }
    }

    public void TryFireMissile(Aircraft target, out bool launched)
    {
        launched = false;
        if (_missileCooldown > 0f || _missilesLeft <= 0 || !MissilePrefab || !MissileRail) return;

        // Simple launch conditions: in front cone + within 1500–3500m
        if (target)
        {
            Vector3 toT = target.transform.position - MissileRail.position;
            float ang = Vector3.Angle(MissileRail.forward, toT);
            float rng = toT.magnitude;
            if (ang > 25f || rng < 800f || rng > 3500f) return;
        }

        var go = Instantiate(MissilePrefab, MissileRail.position, MissileRail.rotation);
        var seeker = go.GetComponent<SimpleHomingMissile>();
        if (seeker)
        {
            seeker.Launch(target ? target.transform : null, OnMissileKill);
            launched = true;
            _missileCooldown = MissileReload;
            _missilesLeft--;
        }
    }

    private void OnMissileKill()
    {
        _missileKillFlag = true;
    }
}
