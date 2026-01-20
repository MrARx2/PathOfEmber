using UnityEngine;
using Audio;

/// <summary>
/// Manages potion spawning for Freeze, Venom, and Invulnerability Potion talents.
/// Each potion type has its own independent timer with randomized variance.
/// Attach to the Player GameObject.
/// </summary>
public class PotionSpawner : MonoBehaviour
{
    [Header("Freeze Potion")]
    [SerializeField, Tooltip("Freeze potion prefab to spawn")]
    private GameObject freezePotionPrefab;
    [SerializeField, Tooltip("Base seconds between freeze potion spawns")]
    private float freezeSpawnInterval = 8f;
    
    [Header("Venom Potion")]
    [SerializeField, Tooltip("Venom potion prefab to spawn")]
    private GameObject venomPotionPrefab;
    [SerializeField, Tooltip("Base seconds between venom potion spawns")]
    private float venomSpawnInterval = 8f;
    
    [Header("Invulnerability Potion")]
    [SerializeField, Tooltip("Invulnerability potion prefab to spawn")]
    private GameObject invulnerabilityPotionPrefab;
    [SerializeField, Tooltip("Base seconds between invulnerability potion spawns")]
    private float invulnerabilitySpawnInterval = 8f;
    
    [Header("Spawn Settings")]
    [SerializeField, Tooltip("Radius around player to spawn potions")]
    private float spawnRadius = 2f;
    [SerializeField, Tooltip("Height offset for spawn position")]
    private float spawnHeight = 0.5f;
    [SerializeField, Tooltip("Random variance added to spawn interval (+/- this value)")]
    private float spawnTimeVariance = 2f;
    
    [Header("Spawn Sound Effects")]
    [SerializeField, Tooltip("Sound when freeze potion spawns/falls")]
    private SoundEvent freezeSpawnSound;
    [SerializeField, Tooltip("Sound when venom potion spawns/falls")]
    private SoundEvent venomSpawnSound;
    [SerializeField, Tooltip("Sound when invulnerability potion spawns")]
    private SoundEvent invulnerabilitySpawnSound;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    // Independent timers for each potion type
    private float freezeTimer = 0f;
    private float venomTimer = 0f;
    private float invulnerabilityTimer = 0f;
    private float currentFreezeTarget = 0f;
    private float currentVenomTarget = 0f;
    private float currentInvulnerabilityTarget = 0f;
    
    // Stack counts (received from PlayerAbilities)
    private int freezePotionStacks = 0;
    private int venomPotionStacks = 0;
    private int invulnerabilityPotionStacks = 0;
    
    // Stack-based interval reduction constants
    private const float INTERVAL_REDUCTION_PER_STACK = 2f;
    private const float MINIMUM_INTERVAL = 1f;
    
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
        ResetInvulnerabilityTimer();
    }
    
    private void Update()
    {
        // Freeze potion timer (enabled when stacks > 0)
        if (freezePotionStacks > 0 && freezePotionPrefab != null)
        {
            freezeTimer += Time.deltaTime;
            if (freezeTimer >= currentFreezeTarget)
            {
                SpawnPotion(freezePotionPrefab, "Freeze");
                ResetFreezeTimer();
            }
        }
        
        // Venom potion timer (enabled when stacks > 0)
        if (venomPotionStacks > 0 && venomPotionPrefab != null)
        {
            venomTimer += Time.deltaTime;
            if (venomTimer >= currentVenomTarget)
            {
                SpawnPotion(venomPotionPrefab, "Venom");
                ResetVenomTimer();
            }
        }
        
        // Invulnerability potion timer (enabled when stacks > 0)
        if (invulnerabilityPotionStacks > 0 && invulnerabilityPotionPrefab != null)
        {
            invulnerabilityTimer += Time.deltaTime;
            if (invulnerabilityTimer >= currentInvulnerabilityTarget)
            {
                SpawnPotion(invulnerabilityPotionPrefab, "Invulnerability");
                ResetInvulnerabilityTimer();
            }
        }
    }
    
    /// <summary>
    /// Calculates effective spawn interval based on stack count.
    /// Stack 1 = base interval, each additional stack reduces by 2s, minimum 1s.
    /// </summary>
    private float GetEffectiveInterval(float baseInterval, int stacks)
    {
        if (stacks <= 0) return baseInterval;
        float reduction = (stacks - 1) * INTERVAL_REDUCTION_PER_STACK;
        return Mathf.Max(MINIMUM_INTERVAL, baseInterval - reduction);
    }
    
    private void ResetFreezeTimer()
    {
        freezeTimer = 0f;
        float effectiveInterval = GetEffectiveInterval(freezeSpawnInterval, freezePotionStacks);
        currentFreezeTarget = effectiveInterval + Random.Range(-spawnTimeVariance, spawnTimeVariance);
        currentFreezeTarget = Mathf.Max(MINIMUM_INTERVAL, currentFreezeTarget);
        if (debugLog) Debug.Log($"[PotionSpawner] Freeze timer reset. Stacks: {freezePotionStacks}, Interval: {effectiveInterval:F1}s, Target: {currentFreezeTarget:F1}s");
    }
    
    private void ResetVenomTimer()
    {
        venomTimer = 0f;
        float effectiveInterval = GetEffectiveInterval(venomSpawnInterval, venomPotionStacks);
        currentVenomTarget = effectiveInterval + Random.Range(-spawnTimeVariance, spawnTimeVariance);
        currentVenomTarget = Mathf.Max(MINIMUM_INTERVAL, currentVenomTarget);
        if (debugLog) Debug.Log($"[PotionSpawner] Venom timer reset. Stacks: {venomPotionStacks}, Interval: {effectiveInterval:F1}s, Target: {currentVenomTarget:F1}s");
    }
    
    private void ResetInvulnerabilityTimer()
    {
        invulnerabilityTimer = 0f;
        float effectiveInterval = GetEffectiveInterval(invulnerabilitySpawnInterval, invulnerabilityPotionStacks);
        currentInvulnerabilityTarget = effectiveInterval + Random.Range(-spawnTimeVariance, spawnTimeVariance);
        currentInvulnerabilityTarget = Mathf.Max(MINIMUM_INTERVAL, currentInvulnerabilityTarget);
        if (debugLog) Debug.Log($"[PotionSpawner] Invulnerability timer reset. Stacks: {invulnerabilityPotionStacks}, Interval: {effectiveInterval:F1}s, Target: {currentInvulnerabilityTarget:F1}s");
    }
    
    private void SpawnPotion(GameObject prefab, string type)
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = playerTransform.position + new Vector3(randomCircle.x, spawnHeight, randomCircle.y);
        
        GameObject potion = Instantiate(prefab, spawnPos, Quaternion.identity);
        
        // Play spawn sound based on type
        if (AudioManager.Instance != null)
        {
            SoundEvent soundToPlay = null;
            if (type == "Freeze") soundToPlay = freezeSpawnSound;
            else if (type == "Venom") soundToPlay = venomSpawnSound;
            else if (type == "Invulnerability") soundToPlay = invulnerabilitySpawnSound;
            
            if (soundToPlay != null)
                AudioManager.Instance.PlayAtPosition(soundToPlay, spawnPos);
        }
        
        if (debugLog)
            Debug.Log($"[PotionSpawner] Spawned {type} potion at {spawnPos}");
    }
    
    #region Public Methods (Called by PlayerAbilities)
    
    /// <summary>
    /// Sets freeze potion stacks. Spawns initial potion on first stack.
    /// </summary>
    public void SetFreezePotionStacks(int stacks)
    {
        bool wasDisabled = freezePotionStacks == 0;
        freezePotionStacks = stacks;
        
        if (stacks > 0)
        {
            ResetFreezeTimer();
            
            // Spawn one immediately on first activation so player knows what they picked
            if (wasDisabled && freezePotionPrefab != null)
            {
                SpawnPotion(freezePotionPrefab, "Freeze");
                if (debugLog) Debug.Log("[PotionSpawner] Spawned initial Freeze potion on talent pickup!");
            }
            
            if (debugLog) Debug.Log($"[PotionSpawner] Freeze Potion stacks: {stacks}, interval: {GetEffectiveInterval(freezeSpawnInterval, stacks):F1}s");
        }
        else
        {
            freezeTimer = 0f;
            if (debugLog) Debug.Log("[PotionSpawner] Freeze Potion talent deactivated!");
        }
    }
    
    /// <summary>
    /// Sets venom potion stacks. Spawns initial potion on first stack.
    /// </summary>
    public void SetVenomPotionStacks(int stacks)
    {
        bool wasDisabled = venomPotionStacks == 0;
        venomPotionStacks = stacks;
        
        if (stacks > 0)
        {
            ResetVenomTimer();
            
            // Spawn one immediately on first activation so player knows what they picked
            if (wasDisabled && venomPotionPrefab != null)
            {
                SpawnPotion(venomPotionPrefab, "Venom");
                if (debugLog) Debug.Log("[PotionSpawner] Spawned initial Venom potion on talent pickup!");
            }
            
            if (debugLog) Debug.Log($"[PotionSpawner] Venom Potion stacks: {stacks}, interval: {GetEffectiveInterval(venomSpawnInterval, stacks):F1}s");
        }
        else
        {
            venomTimer = 0f;
            if (debugLog) Debug.Log("[PotionSpawner] Venom Potion talent deactivated!");
        }
    }
    
    /// <summary>
    /// Sets invulnerability potion stacks. Spawns initial potion on first stack.
    /// </summary>
    public void SetInvulnerabilityPotionStacks(int stacks)
    {
        bool wasDisabled = invulnerabilityPotionStacks == 0;
        invulnerabilityPotionStacks = stacks;
        
        if (stacks > 0)
        {
            ResetInvulnerabilityTimer();
            
            // Spawn one immediately on first activation so player knows what they picked
            if (wasDisabled && invulnerabilityPotionPrefab != null)
            {
                SpawnPotion(invulnerabilityPotionPrefab, "Invulnerability");
                if (debugLog) Debug.Log("[PotionSpawner] Spawned initial Invulnerability potion on talent pickup!");
            }
            
            if (debugLog) Debug.Log($"[PotionSpawner] Invulnerability Potion stacks: {stacks}, interval: {GetEffectiveInterval(invulnerabilitySpawnInterval, stacks):F1}s");
        }
        else
        {
            invulnerabilityTimer = 0f;
            if (debugLog) Debug.Log("[PotionSpawner] Invulnerability Potion talent deactivated!");
        }
    }
    
    // Legacy methods (kept for backwards compatibility, delegate to new stack methods)
    public void EnableFreezePotion() => SetFreezePotionStacks(Mathf.Max(1, freezePotionStacks + 1));
    public void DisableFreezePotion() => SetFreezePotionStacks(0);
    public void EnableVenomPotion() => SetVenomPotionStacks(Mathf.Max(1, venomPotionStacks + 1));
    public void DisableVenomPotion() => SetVenomPotionStacks(0);
    public void EnableInvulnerabilityPotion() => SetInvulnerabilityPotionStacks(Mathf.Max(1, invulnerabilityPotionStacks + 1));
    public void DisableInvulnerabilityPotion() => SetInvulnerabilityPotionStacks(0);
    
    /// <summary>
    /// Sync with PlayerAbilities stack counts.
    /// </summary>
    public void SyncWithAbilities(int freezeStacks, int venomStacks, int invulnerabilityStacks)
    {
        if (freezeStacks != freezePotionStacks)
            SetFreezePotionStacks(freezeStacks);
        if (venomStacks != venomPotionStacks)
            SetVenomPotionStacks(venomStacks);
        if (invulnerabilityStacks != invulnerabilityPotionStacks)
            SetInvulnerabilityPotionStacks(invulnerabilityStacks);
    }
    
    public void DisableAllPotions()
    {
        SetFreezePotionStacks(0);
        SetVenomPotionStacks(0);
        SetInvulnerabilityPotionStacks(0);
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
    
    [ContextMenu("Debug: Enable Invulnerability Potion")]
    private void DebugEnableInvulnerabilityPotion() => EnableInvulnerabilityPotion();
    
    [ContextMenu("Debug: Disable Invulnerability Potion")]
    private void DebugDisableInvulnerabilityPotion() => DisableInvulnerabilityPotion();
    
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
    
    [ContextMenu("Debug: Spawn Invulnerability Potion Now")]
    private void DebugSpawnInvulnerabilityNow()
    {
        if (invulnerabilityPotionPrefab != null)
            SpawnPotion(invulnerabilityPotionPrefab, "Invulnerability");
        else
            Debug.LogWarning("[PotionSpawner] Invulnerability potion prefab not assigned!");
    }
    
    #endregion
}
