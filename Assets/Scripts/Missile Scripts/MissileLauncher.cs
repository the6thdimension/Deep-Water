using UnityEngine;
using DeepWater.Missiles;

public class MissileLauncher : MonoBehaviour
{
    [SerializeField] private MissileData missileData;  // assign via Inspector
    [SerializeField] private Transform target;

    public Transform CurrentTarget => target;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void LaunchMissile()
    {
        if (missileData == null || missileData.missilePrefab == null)
        {
            Debug.LogWarning("MissileData or Prefab not assigned!");
            return;
        }

        // Instantiate missile prefab
        GameObject missileObj = Instantiate(missileData.missilePrefab, transform.position, transform.rotation);

        // Get custom missile script to initialize values
        MissileBehavior missileBehavior = missileObj.GetComponent<MissileBehavior>();
        if (missileBehavior != null)
        {
            missileBehavior.Initialize(missileData);
            // Assign the target to the missile if needed
            // missileBehavior.SetTarget(target); // Uncomment if MissileBehavior supports targeting
        }
    }
}
