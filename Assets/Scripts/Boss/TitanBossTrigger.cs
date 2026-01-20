using UnityEngine;

namespace Boss
{
    /// <summary>
    /// Trigger zone that starts the Titan boss fight when player enters.
    /// Also grants attack range bonus while player is inside the arena.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TitanBossTrigger : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TitanBossController bossController;
        [SerializeField] private bool destroyAfterTrigger = false; // Changed default to false for range bonus
        
        [Header("Attack Range Bonus")]
        [SerializeField, Tooltip("Extra attack range granted while inside boss arena")]
        private float attackRangeBonus = 3f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private bool hasTriggered = false;
        private bool hasBonusApplied = false;
        private PlayerShooting playerShooting;
        
        private void Awake()
        {
            // Ensure collider is trigger
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            // Cache player shooting reference
            if (playerShooting == null)
            {
                playerShooting = other.GetComponent<PlayerShooting>();
                if (playerShooting == null)
                    playerShooting = other.GetComponentInParent<PlayerShooting>();
            }
            
            // Apply attack range bonus
            if (playerShooting != null && !hasBonusApplied)
            {
                playerShooting.AddAttackRangeBonus(attackRangeBonus);
                hasBonusApplied = true;
                if (debugLog)
                    Debug.Log($"[TitanBossTrigger] Applied +{attackRangeBonus} attack range bonus");
            }
            
            // Start boss fight (only once)
            if (!hasTriggered)
            {
                hasTriggered = true;
                
                if (debugLog)
                    Debug.Log("[TitanBossTrigger] Player entered boss zone!");
                
                // Start boss fight logic via controller
                // Note: Arena Boundary activation and Hazard Pause are now handled inside TitanBossController.StartBossFight()
                // to ensure perfect sync with camera and game state.
                
                if (bossController != null)
                {
                    bossController.StartBossFight();
                }
                else if (TitanBossController.Instance != null)
                {
                    TitanBossController.Instance.StartBossFight();
                }
                else
                {
                    Debug.LogError("[TitanBossTrigger] No TitanBossController found!");
                }
            }
            
            // Clean up trigger if configured (note: only if we don't need exit detection)
            if (destroyAfterTrigger)
            {
                Destroy(gameObject);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            // Remove attack range bonus
            if (playerShooting != null && hasBonusApplied)
            {
                playerShooting.RemoveAttackRangeBonus(attackRangeBonus);
                hasBonusApplied = false;
                if (debugLog)
                    Debug.Log($"[TitanBossTrigger] Removed +{attackRangeBonus} attack range bonus");
            }
        }
        
        /// <summary>
        /// Manually triggers the exit of the boss fight (camera mode).
        /// Can be called by unity events or other scripts.
        /// </summary>
        public void TriggerExit()
        {
            if (debugLog) Debug.Log("[TitanBossTrigger] TriggerExit called - requesting EndBossFight");
            
            if (bossController != null)
                bossController.EndBossFight();
            else if (TitanBossController.Instance != null)
                TitanBossController.Instance.EndBossFight();
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(col.center, col.size);
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(col.center, col.size);
            }
            
            var sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}
