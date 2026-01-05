using UnityEngine;

/// <summary>
/// Manages potion spawning for Freeze and Venom Potion talents.
/// Each potion type has its own independent timer with randomized variance.
/// Attach to the Player GameObject.
/// </summary>
public class PotionSpawner : MonoBehaviour
{
    [Header("Freeze Potion")]
    [SerializeField, Tooltip("Enable freeze potion spawning")]
    private bool freezePotionEnabled = false;
    [SerializeField, Tooltip("Freeze potion prefab to spawn")]
    private GameObject freezePotionPrefab;
    [SerializeField, Tooltip("Base seconds between freeze potion spawns")]
    private float freezeSpawnInterval = 8f;
    
    [Header("Venom Potion")]
    [SerializeField, Tooltip("Enable venom potion spawning")]
    private bool venomPotionEnabled = false;
    [SerializeField, Tooltip("Venom potion prefab to spawn")]
    private GameObject venomPotionPrefab;
    [SerializeField, Tooltip("Base seconds between venom potion spawns")]
    private float venomSpawnInterval = 8f;
    
    [Header("Spawn Settings")]
    [SerializeField, Tooltip("Radius around player to spawn potions")]
    private float spawnRadius = 2f;
    [SerializeField, Tooltip("Height offset for spawn position")]
    private float spawnHeight = 0.5f;
    [SerializeField, Tooltip("Random variance added to spawn interval (+/- this value)")]
    private float spawnTimeVariance = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    // Independent timers for each potion type
    private float freezeTimer = 0f;
    private float venomTimer = 0f;
    private float currentFreezeTarget = 0f;
    private float currentVenomTarget = 0f;
    
    private Transform playerTransform;
    
    private void Awake()
    {
        playerTransform = transform;
    }
    
    private void Start()
    {
        // Set initial random targets
        ResetFreezeTimer();
        ResetVenomTimer();
    }
    
    private void Update()
    {
        // Freeze potion timer
        if (freezePotionEnabled && freezePotionPrefab != null)
        {
            freezeTimer += Time.deltaTime;
            if (freezeTimer >= currentFreezeTarget)
            {
                SpawnPotion(freezePotionPrefab, "Freeze");
                ResetFreezeTimer();
            }
        }
        
        // Venom potion timer
        if (venomPotionEnabled && venomPotionPrefab != null)
        {
            venomTimer += Time.deltaTime;
            if (venomTimer >= currentVenomTarget)
            {
                SpawnPotion(venomPotionPrefab, "Venom");
                ResetVenomTimer();
            }
        }
    }
    
    private void ResetFreezeTimer()
    {
        freezeTimer = 0f;
        currentFreezeTarget = freezeSpawnInterval + Random.Range(-spawnTimeVariance, spawnTimeVariance);
        currentFreezeTarget = Mathf.Max(1f, currentFreezeTarget); // Minimum 1 second
    }
    
    private void ResetVenomTimer()
    {
        venomTimer = 0f;
        currentVenomTarget = venomSpawnInterval + Random.Range(-spawnTimeVariance, spawnTimeVariance);
        currentVenomTarget = Mathf.Max(1f, currentVenomTarget); // Minimum 1 second
    }
    
    private void SpawnPotion(GameObject prefab, string type)
    {
        // Random position in a circle around the player
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = playerTransform.position + new Vector3(randomCircle.x, spawnHeight, randomCircle.y);
        
        GameObject potion = Instantiate(prefab, spawnPos, Quaternion.identity);
        
        if (debugLog)
            Debug.Log($"[PotionSpawner] Spawned {type} potion at {spawnPos}");
    }
    
    #region Public Methods (Called by PlayerAbilities)
    
    /// <summary>
    /// Enable freeze potion spawning. Timer starts immediately.
    /// </summary>
    public void EnableFreezePotion()
    {
        freezePotionEnabled = true;
        ResetFreezeTimer();
        if (debugLog) Debug.Log("[PotionSpawner] Freeze Potion talent activated!");
    }
    
    /// <summary>
    /// Disable freeze potion spawning.
    /// </summary>
    public void DisableFreezePotion()
    {
        freezePotionEnabled = false;
        freezeTimer = 0f;
        if (debugLog) Debug.Log("[PotionSpawner] Freeze Potion talent deactivated!");
    }
    
    /// <summary>
    /// Enable venom potion spawning. Timer starts immediately.
    /// </summary>
    public void EnableVenomPotion()
    {
        venomPotionEnabled = true;
        ResetVenomTimer();
        if (debugLog) Debug.Log("[PotionSpawner] Venom Potion talent activated!");
    }
    
    /// <summary>
    /// Disable venom potion spawning.
    /// </summary>
    public void DisableVenomPotion()
    {
        venomPotionEnabled = false;
        venomTimer = 0f;
        if (debugLog) Debug.Log("[PotionSpawner] Venom Potion talent deactivated!");
    }
    
    /// <summary>
    /// Sync with PlayerAbilities boolean states.
    /// </summary>
    public void SyncWithAbilities(bool freezeEnabled, bool venomEnabled)
    {
        // Handle freeze
        if (freezeEnabled && !freezePotionEnabled)
            EnableFreezePotion();
        else if (!freezeEnabled && freezePotionEnabled)
            DisableFreezePotion();
            
        // Handle venom
        if (venomEnabled && !venomPotionEnabled)
            EnableVenomPotion();
        else if (!venomEnabled && venomPotionEnabled)
            DisableVenomPotion();
    }
    
    /// <summary>
    /// Disable all potion spawning. Used when resetting abilities.
    /// </summary>
    public void DisableAllPotions()
    {
        DisableFreezePotion();
        DisableVenomPotion();
    }
    
    #endregion
    
    #region Debug Context Menu
    
    [ContextMenu("Debug: Enable Freeze Potion")]
    private void DebugEnableFreezePotion() => EnableFreezePotion();
    
    [ContextMenu("Debug: Disable Freeze Potion")]
    private void DebugDisableFreezePotion() => DisableFreezePotion();
    
    [ContextMenu("Debug: Enable Venom Potion")]
    private void DebugEnableVenomPotion() => EnableVenomPotion();
    
    [ContextMenu("Debug: Disable Venom Potion")]
    private void DebugDisableVenomPotion() => DisableVenomPotion();
    
    [ContextMenu("Debug: Spawn Freeze Potion Now")]
    private void DebugSpawnFreezeNow()
    {
        if (freezePotionPrefab != null)
            SpawnPotion(freezePotionPrefab, "Freeze");
        else
            Debug.LogWarning("[PotionSpawner] Freeze potion prefab not assigned!");
    }
    
    [ContextMenu("Debug: Spawn Venom Potion Now")]
    private void DebugSpawnVenomNow()
    {
        if (venomPotionPrefab != null)
            SpawnPotion(venomPotionPrefab, "Venom");
        else
            Debug.LogWarning("[PotionSpawner] Venom potion prefab not assigned!");
    }
    
    #endregion
}
