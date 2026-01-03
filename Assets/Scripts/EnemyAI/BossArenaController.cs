using UnityEngine;
using UnityEngine.Events;

namespace EnemyAI
{
    /// <summary>
    /// Boss Arena Controller - Activates blockades when player enters, deactivates when boss dies.
    /// Place this on a trigger volume at the arena entrance.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossArenaController : MonoBehaviour
    {
        [Header("=== BLOCKADES ===")]
        [SerializeField, Tooltip("First blockade GameObject to enable when fight starts")]
        private GameObject blockade1;
        
        [SerializeField, Tooltip("Second blockade GameObject to enable when fight starts")]
        private GameObject blockade2;
        
        [Header("=== BOSS REFERENCE ===")]
        [SerializeField, Tooltip("The Miniboss that must be killed to open blockades")]
        private EnemyHealth minibossHealth;
        
        [SerializeField, Tooltip("Auto-find MinibossAI in scene if not assigned")]
        private bool autoFindBoss = true;
        
        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Only trigger once (stays locked after first activation)")]
        private bool triggerOnce = true;
        
        [SerializeField, Tooltip("Player tag to detect")]
        private string playerTag = "Player";
        
        [SerializeField, Tooltip("Delay before activating blockades (seconds)")]
        private float activationDelay = 0.5f;
        
        [Header("=== EVENTS ===")]
        public UnityEvent OnArenaActivated;
        public UnityEvent OnArenaCleared;
        
        [Header("=== DEBUG ===")]
        [SerializeField] private bool debugLog = true;
        
        // State
        private bool isActivated = false;
        private bool isCleared = false;
        
        private void Start()
        {
            // Ensure trigger is set
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                if (debugLog) Debug.Log("[BossArena] Set collider to trigger mode");
            }
            
            // Start with blockades disabled
            SetBlockadesActive(false);
            
            // Auto-find boss if not assigned
            if (minibossHealth == null && autoFindBoss)
            {
                MinibossAI boss = FindFirstObjectByType<MinibossAI>();
                if (boss != null)
                {
                    minibossHealth = boss.GetComponent<EnemyHealth>();
                    if (debugLog) Debug.Log($"[BossArena] Auto-found boss: {boss.name}");
                }
            }
        }
        
        private void Update()
        {
            // Check if boss is dead to open blockades
            if (isActivated && !isCleared)
            {
                if (minibossHealth != null && minibossHealth.IsDead)
                {
                    ClearArena();
                }
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Only activate for player
            if (!other.CompareTag(playerTag)) return;
            
            // Only trigger once if configured
            if (triggerOnce && isActivated) return;
            
            if (debugLog) Debug.Log($"[BossArena] Player entered arena trigger!");
            
            // Activate arena with optional delay
            if (activationDelay > 0)
            {
                Invoke(nameof(ActivateArena), activationDelay);
            }
            else
            {
                ActivateArena();
            }
        }
        
        /// <summary>
        /// Activates the arena - enables blockades and starts the fight.
        /// </summary>
        public void ActivateArena()
        {
            if (isActivated && triggerOnce) return;
            
            isActivated = true;
            SetBlockadesActive(true);
            
            if (debugLog) Debug.Log("[BossArena] ARENA ACTIVATED - Blockades UP!");
            
            OnArenaActivated?.Invoke();
        }
        
        /// <summary>
        /// Clears the arena - disables blockades after boss is defeated.
        /// </summary>
        public void ClearArena()
        {
            if (isCleared) return;
            
            isCleared = true;
            SetBlockadesActive(false);
            
            if (debugLog) Debug.Log("[BossArena] ARENA CLEARED - Blockades DOWN!");
            
            OnArenaCleared?.Invoke();
        }
        
        /// <summary>
        /// Force clear the arena (for testing or special cases).
        /// </summary>
        public void ForceClear()
        {
            isCleared = false; // Reset so ClearArena works
            ClearArena();
        }
        
        private void SetBlockadesActive(bool active)
        {
            if (blockade1 != null)
            {
                blockade1.SetActive(active);
                if (debugLog) Debug.Log($"[BossArena] Blockade 1: {(active ? "ENABLED" : "DISABLED")}");
            }
            
            if (blockade2 != null)
            {
                blockade2.SetActive(active);
                if (debugLog) Debug.Log($"[BossArena] Blockade 2: {(active ? "ENABLED" : "DISABLED")}");
            }
        }
        
        /// <summary>
        /// Check if the arena fight is currently active.
        /// </summary>
        public bool IsArenaActive => isActivated && !isCleared;
        
        /// <summary>
        /// Check if the arena has been cleared.
        /// </summary>
        public bool IsArenaCleared => isCleared;
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            // Draw trigger area
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = isActivated ? (isCleared ? Color.green : Color.red) : Color.yellow;
                
                if (col is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
                }
            }
            
            // Draw lines to blockades
            Gizmos.color = Color.magenta;
            if (blockade1 != null)
                Gizmos.DrawLine(transform.position, blockade1.transform.position);
            if (blockade2 != null)
                Gizmos.DrawLine(transform.position, blockade2.transform.position);
            
            // Draw line to boss
            if (minibossHealth != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, minibossHealth.transform.position);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Highlight blockades when selected
            Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
            
            if (blockade1 != null)
            {
                BoxCollider box = blockade1.GetComponent<BoxCollider>();
                if (box != null)
                {
                    Gizmos.matrix = blockade1.transform.localToWorldMatrix;
                    Gizmos.DrawCube(box.center, box.size);
                }
            }
            
            if (blockade2 != null)
            {
                BoxCollider box = blockade2.GetComponent<BoxCollider>();
                if (box != null)
                {
                    Gizmos.matrix = blockade2.transform.localToWorldMatrix;
                    Gizmos.DrawCube(box.center, box.size);
                }
            }
        }
        
        #endregion
    }
}
