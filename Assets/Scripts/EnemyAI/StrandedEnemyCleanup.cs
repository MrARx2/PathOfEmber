using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Background system that removes enemies stranded without NavMesh.
/// Runs periodically to clean up enemies left behind when chunks despawn.
/// 
/// PLACEMENT: Add to GameManager or create a dedicated "CleanupManager" GameObject.
/// </summary>
public class StrandedEnemyCleanup : MonoBehaviour
{
    [Header("Cleanup Settings")]
    [SerializeField, Tooltip("How often to check for stranded enemies (seconds)")]
    private float checkInterval = 2f;
    
    [SerializeField, Tooltip("Minimum X boundary (enemies below this are outside map)")]
    private float minX = -3f;
    
    [SerializeField, Tooltip("Maximum X boundary (enemies above this are outside map)")]
    private float maxX = 3f;
    
    [SerializeField, Tooltip("How far below the agent to check for NavMesh")]
    private float navMeshCheckDistance = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    private Transform playerTransform;
    private float checkTimer;
    
    private void Start()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("[StrandedEnemyCleanup] No Player found! Cleanup disabled.");
            enabled = false;
        }
    }
    
    private void Update()
    {
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            CleanupStrandedEnemies();
        }
    }
    
    private void CleanupStrandedEnemies()
    {
        if (playerTransform == null) return;
        
        float playerZ = playerTransform.position.z;
        int cleanedCount = 0;
        
        // Find all NavMeshAgents in scene
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        
        foreach (NavMeshAgent agent in agents)
        {
            if (agent == null || agent.gameObject == null) continue;
            
            Vector3 pos = agent.transform.position;
            
            // Check 1: Is enemy BEHIND the player? (lower Z value)
            bool behindPlayer = pos.z < playerZ;
            
            // Check 2: Is enemy OUTSIDE horizontal map bounds?
            bool outsideX = pos.x < minX || pos.x > maxX;
            
            // Check 3: Is there NO NavMesh beneath the agent?
            bool hasNavMesh = NavMesh.SamplePosition(pos, out NavMeshHit hit, navMeshCheckDistance, NavMesh.AllAreas);
            
            // Remove if:
            // - Behind player AND no NavMesh (stranded because chunk despawned)
            // - OR outside X bounds entirely (fell off map somehow)
            bool shouldRemove = false;
            string reason = "";
            
            if (behindPlayer && !hasNavMesh)
            {
                shouldRemove = true;
                reason = "behind player with no NavMesh";
            }
            else if (outsideX && !hasNavMesh)
            {
                shouldRemove = true;
                reason = "outside X bounds with no NavMesh";
            }
            
            if (!shouldRemove) continue;
            
            if (debugLog)
            {
                Debug.Log($"[StrandedEnemyCleanup] Removing {agent.gameObject.name} at {pos} ({reason})");
            }
            
            Destroy(agent.gameObject);
            cleanedCount++;
        }
        
        if (debugLog && cleanedCount > 0)
        {
            Debug.Log($"[StrandedEnemyCleanup] Cleaned up {cleanedCount} stranded enemies");
        }
    }
}
