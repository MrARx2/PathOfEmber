using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// Miniboss Arena Trigger - locks the player in with the Miniboss.
    /// Place this on a trigger volume (isTrigger = true).
    /// When the player enters, blockades are activated.
    /// Blockades only deactivate when the Miniboss dies.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MinibossArenaTrigger : MonoBehaviour
    {
        [Header("=== BLOCKADES ===")]
        [SerializeField, Tooltip("First blockade to enable when arena activates")]
        private GameObject blockade1;
        
        [SerializeField, Tooltip("Second blockade to enable when arena activates")]
        private GameObject blockade2;
        
        [Header("=== MINIBOSS REFERENCE ===")]
        [SerializeField, Tooltip("The Miniboss in this arena. Blockades disable when it dies.")]
        private EnemyHealth minibossHealth;
        
        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Tag to detect (usually 'Player')")]
        private string playerTag = "Player";
        
        [SerializeField, Tooltip("Disable trigger after activation (one-shot)")]
        private bool disableTriggerAfterActivation = true;
        
        [SerializeField, Tooltip("Show debug messages")]
        private bool debugLog = false;
        
        // State
        private bool arenaActive = false;
        private Collider triggerCollider;
        
        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();
            
            // Ensure it's a trigger
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                Debug.LogWarning($"[MinibossArenaTrigger] Collider on {gameObject.name} is not set to isTrigger! Fixing...");
                triggerCollider.isTrigger = true;
            }
        }
        
        private void Start()
        {
            // Ensure blockades are initially disabled
            if (blockade1 != null) blockade1.SetActive(false);
            if (blockade2 != null) blockade2.SetActive(false);
            
            // Subscribe to miniboss death event
            if (minibossHealth != null)
            {
                minibossHealth.OnDeath.AddListener(OnMinibossDeath);
                if (debugLog) Debug.Log($"[MinibossArenaTrigger] Subscribed to {minibossHealth.gameObject.name} death event");
            }
            else
            {
                Debug.LogWarning($"[MinibossArenaTrigger] No Miniboss Health assigned on {gameObject.name}!");
            }
        }
        
        private void Update()
        {
            // BACKUP: Poll for death in case event doesn't fire (e.g., if object destroyed)
            if (arenaActive && minibossHealth != null)
            {
                if (minibossHealth.IsDead)
                {
                    if (debugLog) Debug.Log("[MinibossArenaTrigger] Detected miniboss death via polling!");
                    DeactivateArena();
                }
            }
            
            // Also check if miniboss was destroyed (null reference)
            if (arenaActive && minibossHealth == null)
            {
                if (debugLog) Debug.Log("[MinibossArenaTrigger] Miniboss was destroyed - deactivating arena!");
                DeactivateArena();
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            if (minibossHealth != null)
            {
                minibossHealth.OnDeath.RemoveListener(OnMinibossDeath);
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Already active? Skip
            if (arenaActive) return;
            
            // Check if it's the player
            if (other.CompareTag(playerTag))
            {
                ActivateArena();
            }
        }
        
        /// <summary>
        /// Activates the arena - enables blockades and locks combat.
        /// </summary>
        public void ActivateArena()
        {
            if (arenaActive) return;
            
            arenaActive = true;
            
            // Enable blockades
            if (blockade1 != null)
            {
                blockade1.SetActive(true);
                if (debugLog) Debug.Log($"[MinibossArenaTrigger] Blockade 1 ACTIVATED");
            }
            
            if (blockade2 != null)
            {
                blockade2.SetActive(true);
                if (debugLog) Debug.Log($"[MinibossArenaTrigger] Blockade 2 ACTIVATED");
            }
            
            if (debugLog) Debug.Log("[MinibossArenaTrigger] ARENA ACTIVATED - Fight the Miniboss!");
            
            // Disable trigger if one-shot
            if (disableTriggerAfterActivation && triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
        
        /// <summary>
        /// Called when the Miniboss dies - disables blockades.
        /// </summary>
        private void OnMinibossDeath()
        {
            if (!arenaActive) return;
            
            DeactivateArena();
        }
        
        /// <summary>
        /// Deactivates the arena - disables blockades.
        /// </summary>
        public void DeactivateArena()
        {
            arenaActive = false;
            
            // Disable blockades
            if (blockade1 != null)
            {
                blockade1.SetActive(false);
                if (debugLog) Debug.Log($"[MinibossArenaTrigger] Blockade 1 DEACTIVATED");
            }
            
            if (blockade2 != null)
            {
                blockade2.SetActive(false);
                if (debugLog) Debug.Log($"[MinibossArenaTrigger] Blockade 2 DEACTIVATED");
            }
            
            if (debugLog) Debug.Log("[MinibossArenaTrigger] ARENA DEACTIVATED - Miniboss defeated!");
        }
        
        /// <summary>
        /// Returns true if the arena is currently active (blockades up).
        /// </summary>
        public bool IsArenaActive => arenaActive;
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            // Draw trigger zone
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = arenaActive ? new Color(1f, 0f, 0f, 0.3f) : new Color(1f, 1f, 0f, 0.2f);
                
                if (col is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                    Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
                }
            }
            
            // Draw lines to blockades
            Gizmos.color = Color.red;
            if (blockade1 != null)
                Gizmos.DrawLine(transform.position, blockade1.transform.position);
            if (blockade2 != null)
                Gizmos.DrawLine(transform.position, blockade2.transform.position);
            
            // Draw line to miniboss
            if (minibossHealth != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, minibossHealth.transform.position);
            }
        }
        
        #endregion
    }
}
