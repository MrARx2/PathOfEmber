using UnityEngine;

namespace Boss
{
    /// <summary>
    /// Trigger zone that starts the Titan boss fight when player enters.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TitanBossTrigger : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TitanBossController bossController;
        [SerializeField] private bool destroyAfterTrigger = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private bool hasTriggered = false;
        
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
            if (hasTriggered) return;
            
            if (!other.CompareTag("Player")) return;
            
            hasTriggered = true;
            
            if (debugLog)
                Debug.Log("[TitanBossTrigger] Player entered boss zone!");
            
            // Start boss fight
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
            
            // Clean up trigger
            if (destroyAfterTrigger)
            {
                Destroy(gameObject);
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
