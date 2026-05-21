using UnityEngine;
using GuidedFury.Core;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Example missile launcher that can fire missiles at targets.
    /// </summary>
    public class MissileLauncher : MonoBehaviour
    {
        [Header("Launcher Configuration")]
        [SerializeField] private string missileType = "default";
        [SerializeField] private Transform launchPoint;
        [SerializeField] private float fireRate = 1f;
        [SerializeField] private int maxMissiles = 10;
        [SerializeField] private KeyCode fireKey = KeyCode.Space;
        
        [Header("Target Settings")]
        [SerializeField] private Transform targetObject;
        [SerializeField] private bool autoAcquireTarget = true;
        [SerializeField] private float targetAcquisitionRange = 5000f;
        [SerializeField] private LayerMask targetLayers;
        
        private float lastFireTime;
        private int missilesFired;
        
        private void Start()
        {
            // Create launch point if not assigned
            if (launchPoint == null)
            {
                GameObject launchPointObj = new GameObject("LaunchPoint");
                launchPointObj.transform.SetParent(transform);
                launchPointObj.transform.localPosition = Vector3.forward;
                launchPoint = launchPointObj.transform;
            }
            
            // Initialize the missile manager if it doesn't exist
            if (MissileManager.Instance == null)
            {
                Debug.LogWarning("MissileManager not found in scene. Creating one automatically.");
            }
        }
        
        private void Update()
        {
            // Auto-acquire target if enabled and no target is set
            if (autoAcquireTarget && targetObject == null)
            {
                AcquireTarget();
            }
            
            // Fire missile on key press
            if (Input.GetKeyDown(fireKey) && CanFire())
            {
                FireMissile();
            }
        }
        
        /// <summary>
        /// Fire a missile at the current target
        /// </summary>
        public void FireMissile()
        {
            if (!CanFire()) return;
            
            // Launch missile
            MissileBase missile = MissileManager.Instance.LaunchMissile(
                missileType,
                launchPoint.position,
                launchPoint.rotation,
                targetObject
            );
            
            if (missile != null)
            {
                lastFireTime = Time.time;
                missilesFired++;
                
                Debug.Log($"Missile fired! ({missilesFired}/{maxMissiles})");
            }
        }
        
        /// <summary>
        /// Set the target for the launcher
        /// </summary>
        /// <param name="target">The target transform</param>
        public void SetTarget(Transform target)
        {
            targetObject = target;
        }
        
        /// <summary>
        /// Check if the launcher can fire
        /// </summary>
        /// <returns>True if the launcher can fire, false otherwise</returns>
        private bool CanFire()
        {
            // Check if we've reached the maximum number of missiles
            if (maxMissiles > 0 && missilesFired >= maxMissiles)
            {
                return false;
            }
            
            // Check if enough time has passed since the last fire
            if (Time.time - lastFireTime < 1f / fireRate)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Acquire a target automatically
        /// </summary>
        private void AcquireTarget()
        {
            // Find potential targets in range
            Collider[] colliders = Physics.OverlapSphere(transform.position, targetAcquisitionRange, targetLayers);
            
            // Find the closest target
            float closestDistance = float.MaxValue;
            Transform closestTarget = null;
            
            foreach (var collider in colliders)
            {
                // Skip if this is our own collider
                if (collider.transform == transform || collider.transform.IsChildOf(transform))
                    continue;
                    
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = collider.transform;
                }
            }
            
            // Set the closest target
            if (closestTarget != null)
            {
                SetTarget(closestTarget);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw target acquisition range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, targetAcquisitionRange);
            
            // Draw line to target
            if (targetObject != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, targetObject.position);
            }
        }
    }
}
