using UnityEngine;
using System.Collections;
using Hazards;

namespace Boss
{
    /// <summary>
    /// Titan's core meteor blast attack.
    /// Shoots meteors up from the core, then they return as classic meteors hitting the arena.
    /// </summary>
    public class TitanCoreBlast : MonoBehaviour
    {
        [Header("Launch Configuration")]
        [SerializeField, Tooltip("Visual meteor prefab for the launch phase (flies up and is destroyed)")]
        private GameObject launchMeteorPrefab;
        
        [SerializeField, Tooltip("Point where meteors launch from (core position)")]
        private Transform corePoint;
        
        [SerializeField, Tooltip("Number of meteors to launch")]
        private int meteorCount = 5;
        
        [SerializeField, Tooltip("Speed meteors fly upward")]
        private float launchSpeed = 30f;
        
        [SerializeField, Tooltip("Height at which launched meteors are destroyed")]
        private float destroyHeight = 50f;
        
        [SerializeField, Tooltip("Spread angle for launched meteors")]
        private float launchSpreadAngle = 30f;
        
        [SerializeField, Tooltip("Delay between each meteor launch")]
        private float launchInterval = 0.15f;
        
        [Header("Return Configuration")]
        [SerializeField, Tooltip("MeteorStrike prefab for returning meteors")]
        private GameObject meteorStrikePrefab;
        
        [SerializeField, Tooltip("Delay after all launches before meteors start returning")]
        private float returnDelay = 1f;
        
        [SerializeField, Tooltip("Delay between each returning meteor")]
        private float returnInterval = 0.3f;
        
        [SerializeField, Tooltip("Center of the arena for meteor targeting (fallback if no player)")]
        private Transform arenaCenter;
        
        [SerializeField, Tooltip("Maximum radius around arena center where meteors can land")]
        private float arenaRadius = 8f;
        
        [Header("Player Targeting")]
        [SerializeField, Tooltip("Reference to player transform. If set, meteors target around player.")]
        private Transform playerTarget;
        
        [SerializeField, Tooltip("How much meteors target player vs random (0 = fully random, 1 = all on player)")]
        [Range(0f, 1f)]
        private float playerTargetWeight = 0.7f;
        
        [SerializeField, Tooltip("Spread radius around player for meteors")]
        private float playerSpreadRadius = 3f;
        
        [Header("Animation Sync")]
        [SerializeField, Tooltip("Delay after animation trigger before launching")]
        private float startDelay = 0.5f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        /// <summary>
        /// Executes the core blast attack.
        /// </summary>
        public void Execute()
        {
            StartCoroutine(CoreBlastSequence());
        }
        
        private IEnumerator CoreBlastSequence()
        {
            // Wait for animation sync
            yield return new WaitForSeconds(startDelay);
            
            Vector3 launchOrigin = corePoint != null ? corePoint.position : transform.position;
            
            if (debugLog)
                Debug.Log($"[TitanCoreBlast] Starting blast with {meteorCount} meteors");
            
            // Phase 1: Launch meteors upward
            for (int i = 0; i < meteorCount; i++)
            {
                LaunchMeteorUp(launchOrigin);
                yield return new WaitForSeconds(launchInterval);
            }
            
            // Wait before return phase
            yield return new WaitForSeconds(returnDelay);
            
            // Phase 2: Spawn returning meteors targeting player area
            Vector3 arenaPos = arenaCenter != null ? arenaCenter.position : transform.position;
            
            for (int i = 0; i < meteorCount; i++)
            {
                SpawnReturningMeteor(arenaPos);
                yield return new WaitForSeconds(returnInterval);
            }
            
            if (debugLog)
                Debug.Log("[TitanCoreBlast] Core blast complete");
        }
        
        private void LaunchMeteorUp(Vector3 origin)
        {
            if (launchMeteorPrefab == null) return;
            
            // Random spread direction
            float randomAngleX = Random.Range(-launchSpreadAngle, launchSpreadAngle);
            float randomAngleZ = Random.Range(-launchSpreadAngle, launchSpreadAngle);
            Vector3 direction = Quaternion.Euler(randomAngleX, 0, randomAngleZ) * Vector3.up;
            
            // Spawn meteor
            Quaternion rotation = Quaternion.LookRotation(direction);
            GameObject meteor = Instantiate(launchMeteorPrefab, origin, rotation);
            
            // Start flying up
            StartCoroutine(FlyMeteorUp(meteor, direction));
            
            if (debugLog)
                Debug.Log($"[TitanCoreBlast] Launched meteor from {origin}");
        }
        
        private IEnumerator FlyMeteorUp(GameObject meteor, Vector3 direction)
        {
            if (meteor == null) yield break;
            
            while (meteor != null && meteor.transform.position.y < destroyHeight)
            {
                meteor.transform.position += direction * launchSpeed * Time.deltaTime;
                yield return null;
            }
            
            if (meteor != null)
            {
                Destroy(meteor);
            }
        }
        
        private void SpawnReturningMeteor(Vector3 arenaCenterPos)
        {
            if (meteorStrikePrefab == null)
            {
                if (debugLog) Debug.LogWarning("[TitanCoreBlast] MeteorStrike prefab not assigned!");
                return;
            }
            
            Vector3 targetPos;
            
            // Determine target position based on player targeting weight
            if (playerTarget != null && Random.value < playerTargetWeight)
            {
                // Target around player with spread
                Vector2 randomOffset = Random.insideUnitCircle * playerSpreadRadius;
                targetPos = playerTarget.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                
                if (debugLog)
                    Debug.Log($"[TitanCoreBlast] Meteor targeting player area");
            }
            else
            {
                // Random position within arena (fallback)
                Vector2 randomOffset = Random.insideUnitCircle * arenaRadius;
                targetPos = arenaCenterPos + new Vector3(randomOffset.x, 0, randomOffset.y);
            }
            
            targetPos.y = 0; // Ground level
            
            // Spawn meteor strike (uses existing MeteorStrike system)
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Get(meteorStrikePrefab, targetPos, Quaternion.identity);
            }
            else
            {
                Instantiate(meteorStrikePrefab, targetPos, Quaternion.identity);
            }
            
            if (debugLog)
                Debug.Log($"[TitanCoreBlast] Returning meteor at {targetPos}");
        }
        
        /// <summary>
        /// Called from animation event at core blast moment.
        /// </summary>
        public void OnCoreBlastPulse()
        {
            // Can be used for VFX/sound sync
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw core point
            Vector3 core = corePoint != null ? corePoint.position : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(core, 1f);
            
            // Draw launch cone
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Vector3 upLeft = Quaternion.Euler(-launchSpreadAngle, 0, 0) * Vector3.up * 10f;
            Vector3 upRight = Quaternion.Euler(launchSpreadAngle, 0, 0) * Vector3.up * 10f;
            Gizmos.DrawLine(core, core + upLeft);
            Gizmos.DrawLine(core, core + upRight);
            
            // Draw arena radius
            if (arenaCenter != null)
            {
                Gizmos.color = Color.red;
                DrawGizmoCircle(arenaCenter.position, arenaRadius);
            }
        }
        
        private void DrawGizmoCircle(Vector3 center, float radius)
        {
            int segments = 32;
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }
    }
}
