using UnityEngine;
using System.Collections;
using Audio;

/// <summary>
/// Potion behavior: Rise from ground → Idle bobbing → Magnetic attraction → Pickup → Spawn meteor.
/// Attach to the potion prefab.
/// </summary>
public class MagneticPotion : MonoBehaviour
{
    public enum PotionType { Freeze, Venom, Invulnerability }
    
    [Header("Potion Settings")]
    [SerializeField] private PotionType potionType = PotionType.Freeze;
    [SerializeField, Tooltip("Speed of magnetic movement toward player")]
    private float magnetSpeed = 15f;
    
    [Header("Emergence Animation")]
    [SerializeField, Tooltip("Duration of rising animation")]
    private float riseDuration = 1.5f;
    [SerializeField, Tooltip("Y height where potion starts (can be negative for below ground)")]
    private float riseStartHeight = -0.5f;
    [SerializeField, Tooltip("Y height where potion ends after rising")]
    private float riseEndHeight = 0.8f;
    [SerializeField, Tooltip("Duration of idle bobbing before magnetizing")]
    private float idleDuration = 1.5f;
    
    [Header("Idle Bobbing")]
    [SerializeField] private float bobSpeed = 3f;
    [SerializeField] private float bobHeight = 0.15f;
    
    [Header("Meteor Settings (Freeze/Venom only)")]
    [SerializeField, Tooltip("EffectMeteor prefab to spawn on nearest enemy")]
    private GameObject effectMeteorPrefab;
    [SerializeField, Tooltip("Maximum range to search for enemies")]
    private float enemySearchRadius = 30f;
    
    [Header("Invulnerability Settings")]
    [SerializeField, Tooltip("Duration of invulnerability when collected")]
    private float invulnerabilityDuration = 2f;
    
    [Header("Sound Effects")]
    [SerializeField, Tooltip("Sound when any potion is picked up")]
    private SoundEvent pickupSound;
    [SerializeField, Tooltip("Additional sound for freeze potion pickup")]
    private SoundEvent freezePickupSound;
    [SerializeField, Tooltip("Additional sound for venom potion pickup")]
    private SoundEvent venomPickupSound;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    [Header("Target Settings")]
    [SerializeField, Tooltip("Bone name to target on player (e.g., Spine2 for consistent collision)")]
    private string targetBoneName = "mixamorig:Spine2";
    
    private Transform playerTransform;
    private Transform magnetTarget; // The actual target for magnetism (bone or player root)
    private bool isMovingToPlayer = false;
    private bool isCollected = false;
    
    private Vector3 spawnXZ; // Store the X/Z position where it spawned
    private float bobTimer = 0f;
    
    private void Start()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            
            // Try to find the target bone (like CoinManager does)
            magnetTarget = FindBoneRecursive(playerTransform, targetBoneName);
            
            if (magnetTarget == null)
            {
                // Fallback to player root + offset
                magnetTarget = playerTransform;
                if (debugLog)
                    Debug.Log($"[MagneticPotion] Bone '{targetBoneName}' not found, using player root");
            }
            else if (debugLog)
            {
                Debug.Log($"[MagneticPotion] Found bone target: {magnetTarget.name}");
            }
        }
        else
        {
            Debug.LogError("[MagneticPotion] Could not find Player!");
            Destroy(gameObject);
            return;
        }
        
        // Store spawn X/Z position
        spawnXZ = transform.position;
        
        // Set starting position
        transform.position = new Vector3(spawnXZ.x, riseStartHeight, spawnXZ.z);
        
        // Begin emergence sequence
        StartCoroutine(EmergenceSequence());
    }
    
    /// <summary>
    /// Recursively searches for a bone by name in the transform hierarchy.
    /// </summary>
    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName)
            return parent;
        
        foreach (Transform child in parent)
        {
            Transform found = FindBoneRecursive(child, boneName);
            if (found != null)
                return found;
        }
        
        return null;
    }
    
    private IEnumerator EmergenceSequence()
    {
        // Phase 1: Rise from start height to end height
        float riseTimer = 0f;
        Vector3 startPos = new Vector3(spawnXZ.x, riseStartHeight, spawnXZ.z);
        Vector3 endPos = new Vector3(spawnXZ.x, riseEndHeight, spawnXZ.z);
        
        if (debugLog)
            Debug.Log($"[MagneticPotion] {potionType} potion rising...");
        
        while (riseTimer < riseDuration)
        {
            riseTimer += Time.deltaTime;
            float t = riseTimer / riseDuration;
            
            // Smooth ease-out curve for natural emergence
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);
            
            transform.position = Vector3.Lerp(startPos, endPos, smoothT);
            yield return null;
        }
        
        transform.position = endPos;
        
        // Phase 2: Idle bobbing
        float idleTimer = 0f;
        
        if (debugLog)
            Debug.Log($"[MagneticPotion] {potionType} potion idling...");
        
        while (idleTimer < idleDuration)
        {
            idleTimer += Time.deltaTime;
            bobTimer += Time.deltaTime * bobSpeed;
            float yOffset = Mathf.Sin(bobTimer) * bobHeight;
            transform.position = endPos + Vector3.up * yOffset;
            yield return null;
        }
        
        // Phase 3: Start magnetic movement
        isMovingToPlayer = true;
        
        if (debugLog)
            Debug.Log($"[MagneticPotion] {potionType} potion magnetizing toward player!");
    }
    
    private void Update()
    {
        if (magnetTarget == null || isCollected) return;
        
        if (isMovingToPlayer)
        {
            // Move toward magnet target (Spine2 bone for consistent collision)
            Vector3 targetPos = magnetTarget.position;
            Vector3 direction = (targetPos - transform.position).normalized;
            transform.position += direction * magnetSpeed * Time.deltaTime;
            
            // Collection is handled by OnTriggerEnter - no distance check needed
        }
        // Note: Rising and idle bobbing are handled in EmergenceSequence coroutine
    }
    
    // Backup collection via trigger (if potion has a trigger collider)
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            if (debugLog)
                Debug.Log("[MagneticPotion] Collected via trigger collision");
            OnCollected();
        }
    }
    
    private void OnCollected()
    {
        if (isCollected) return;
        isCollected = true;
        
        if (debugLog)
            Debug.Log($"[MagneticPotion] {potionType} potion collected!");
        
        // Play pickup sounds
        if (AudioManager.Instance != null)
        {
            // Generic pickup sound
            if (pickupSound != null)
                AudioManager.Instance.Play(pickupSound);
            
            // Type-specific sounds
            if (potionType == PotionType.Freeze && freezePickupSound != null)
                AudioManager.Instance.Play(freezePickupSound);
            else if (potionType == PotionType.Venom && venomPickupSound != null)
                AudioManager.Instance.Play(venomPickupSound);
        }
        
        // Handle based on potion type
        if (potionType == PotionType.Invulnerability)
        {
            // Apply invulnerability to player
            ApplyInvulnerabilityToPlayer();
        }
        else
        {
            // Freeze/Venom - spawn meteor on nearest enemy
            Transform nearestEnemy = FindNearestEnemy();
            
            if (nearestEnemy != null)
            {
                SpawnMeteorOnEnemy(nearestEnemy);
            }
            else
            {
                if (debugLog)
                    Debug.Log("[MagneticPotion] No enemies found, potion consumed without effect");
            }
        }
        
        // Destroy potion
        Destroy(gameObject);
    }
    
    private void ApplyInvulnerabilityToPlayer()
    {
        if (playerTransform == null) return;
        
        // Try to find PlayerHealth on the player (check self and children)
        PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = playerTransform.GetComponentInChildren<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();
        
        // Fallback: find any PlayerHealth in scene
        if (playerHealth == null)
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        
        if (playerHealth != null)
        {
            playerHealth.SetInvulnerable(invulnerabilityDuration);
            if (debugLog)
                Debug.Log($"[MagneticPotion] Applied {invulnerabilityDuration}s invulnerability to player!");
        }
        else
        {
            Debug.LogWarning("[MagneticPotion] Could not find PlayerHealth component anywhere!");
        }
    }
    
    private Transform FindNearestEnemy()
    {
        // Find all enemies
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;
            
            float distance = Vector3.Distance(playerTransform.position, enemy.transform.position);
            if (distance < nearestDistance && distance <= enemySearchRadius)
            {
                nearestDistance = distance;
                nearest = enemy.transform;
            }
        }
        
        if (debugLog && nearest != null)
            Debug.Log($"[MagneticPotion] Found nearest enemy: {nearest.name} at distance {nearestDistance:F1}");
        
        return nearest;
    }
    
    private void SpawnMeteorOnEnemy(Transform enemy)
    {
        if (effectMeteorPrefab == null)
        {
            Debug.LogWarning("[MagneticPotion] EffectMeteor prefab not assigned!");
            return;
        }
        
        // Spawn meteor at enemy position
        Vector3 spawnPos = enemy.position;
        spawnPos.y = 0f; // Ground level
        
        GameObject meteor = Instantiate(effectMeteorPrefab, spawnPos, Quaternion.identity);
        
        // Configure meteor effect type
        EffectMeteor effectMeteor = meteor.GetComponent<EffectMeteor>();
        if (effectMeteor != null)
        {
            effectMeteor.SetEffectType(potionType == PotionType.Freeze ? 
                EffectMeteor.MeteorEffectType.Freeze : 
                EffectMeteor.MeteorEffectType.Venom);
        }
        
        if (debugLog)
            Debug.Log($"[MagneticPotion] Spawned {potionType} meteor on {enemy.name}");
    }
}
