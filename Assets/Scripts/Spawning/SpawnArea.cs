using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Audio;

/// <summary>
/// Spawns enemies when player enters activation distance.
/// Spawns one enemy per spawn point, cycling through prefabs if needed.
/// </summary>
public class SpawnArea : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField, Tooltip("Player transform - auto-finds if null")]
    private Transform player;
    [SerializeField, Tooltip("Distance from this object to trigger spawn")]
    private float activationDistance = 30f;
    [SerializeField] private bool oneTimeSpawn = true;

    [Header("Volume Area (Optional)")]
    [SerializeField, Tooltip("Assign a GameObject with a Collider to use as spawn volume bounds")]
    private Collider volumeArea;
    [SerializeField, Tooltip("If true, use Volume Area center for activation distance check")]
    private bool useVolumeForActivation = true;

    [Header("Spawn Configuration")]
    [SerializeField, Tooltip("Enemy prefabs to spawn. Will cycle through if fewer prefabs than spawn points.")]
    private GameObject[] enemyPrefabs;
    [SerializeField, Tooltip("Spawn points. If empty, uses child transforms.")]
    private Transform[] spawnPoints;
    [SerializeField, Tooltip("If true, randomize which prefab spawns at each point")]
    private bool randomizePrefabs = false;
    
    [SerializeField, Tooltip("If true, randomize spawn positions within the volume area")]
    private bool randomizePositions = false;
    
    [SerializeField, Tooltip("Random offset range around spawn point (if randomizePositions is off)")]
    private float positionRandomOffset = 0f;
    
    [SerializeField, Tooltip("If true, ignore spawn points entirely and use random positions in volume")]
    private bool useOnlyVolumeRandomPositions = false;
    
    [SerializeField, Tooltip("Number of enemies to spawn when using volume random positions")]
    private int volumeSpawnCount = 5;

    [Header("Spawn-Once Tracking")]
    [SerializeField, Tooltip("Unique ID for this spawn area. If empty, uses GameObject name + position hash.")]
    private string uniqueId = "";
    [SerializeField, Tooltip("If true, uses the global SpawnAreaRegistry to track spawns across chunk recycling")]
    private bool useGlobalRegistry = true;

    [Header("Second Wave (Bonus)")]
    [SerializeField, Tooltip("If true, spawns a 2nd wave of enemies after first wave is cleared")]
    private bool enableSecondWave = false;
    
    [SerializeField, Tooltip("Enemy prefabs for 2nd wave. If empty, uses same prefabs as wave 1")]
    private GameObject[] wave2EnemyPrefabs;
    
    [SerializeField, Tooltip("Number of enemies in 2nd wave (uses random volume positions)")]
    private int wave2EnemyCount = 3;
    
    [SerializeField, Tooltip("Delay before 2nd wave spawns after wave 1 is cleared (seconds)")]
    private float wave2SpawnDelay = 1f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.red;

    [Header("Spawn Indicator")]
    [SerializeField, Tooltip("If true, shows indicator decals before spawning enemies")]
    private bool useSpawnIndicator = true;
    
    [SerializeField, Tooltip("Prefab for the indicator decal/VFX (e.g., warning circle, glow effect)")]
    private GameObject indicatorPrefab;
    
    [SerializeField, Tooltip("Position offset for indicator (relative to spawn point)")]
    private Vector3 indicatorPositionOffset = new Vector3(0f, 0.05f, 0f);
    
    [SerializeField, Tooltip("Rotation offset for indicator (X=90 makes it face down for ground decals)")]
    private Vector3 indicatorRotationOffset = new Vector3(90f, 0f, 0f);
    
    [SerializeField, Tooltip("How long the indicator is shown before enemy spawns (seconds)")]
    private float indicatorDuration = 1.5f;
    
    [SerializeField, Tooltip("Delay between spawning each enemy (0 = all at once)")]
    private float spawnStagger = 0.15f;
    
    [SerializeField, Tooltip("Optional VFX to play when enemy actually spawns (e.g., poof effect)")]
    private GameObject spawnVFXPrefab;
    
    [SerializeField, Tooltip("Position offset for spawn VFX (relative to spawn point)")]
    private Vector3 spawnVFXPositionOffset = Vector3.zero;
    
    [SerializeField, Tooltip("Rotation offset for spawn VFX (X=-90 makes it face up for ground effects)")]
    private Vector3 spawnVFXRotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("Sound Effects")]
    [SerializeField] private SoundEvent spawnSound;

    private bool hasSpawned = false;
    private bool isSpawning = false; // Prevents double-triggering during spawn sequence
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private List<GameObject> activeIndicators = new List<GameObject>();
    
    // Second wave tracking
    private int currentWave = 0;
    private bool wave2Triggered = false;
    private bool isCheckingForWave2 = false;

    public bool HasSpawned => hasSpawned;
    public bool IsSpawning => isSpawning;
    public int CurrentWave => currentWave;
    public List<GameObject> SpawnedEnemies => spawnedEnemies;

    private void OnEnable()
    {
        // Reset state for pooling
        hasSpawned = false;
        isSpawning = false;
        wave2Triggered = false;
        isCheckingForWave2 = false;
        currentWave = 0;
        spawnedEnemies.Clear();
        activeIndicators.Clear();
        
        // Cache squared distance
        activationDistanceSqr = activationDistance * activationDistance;
        
        if (player == null)
        {
            // Try to find player efficiently - optimized for 2023+
            // If ObjectPoolManager exists, player might be cached there? No.
            // Helper: FindFirstObjectByType is better than FindGameObjectWithTag performance-wise usually? 
            // Stick to Tag as it's standard here.
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            CollectChildSpawnPoints();
        }
        
        // Always recalculate ID if it's auto-generated (empty in inspector)
        // because the chunk might be at a new position now.
        // We only clear uniqueId if it looks like an auto-generated one (contains name and coord)
        // OR simply: if inspector field is empty, always regenerate.
        // NOTE: We cannot easily distinguish inspector value vs runtime value if we overwrote it.
        // But since we are pooling, we should re-generate it based on new position.
        // Assuming uniqueId field in inspector is meant to be permanent if set.
        // If it was empty in inspector, we want to regenerate.
        // We can't know if it was empty in inspector at runtime easily unless we check prefab?
        // Let's assume if it starts with gameObject.name it might be auto-generated.
        // Safer: Just regenerate if it's not explicitly manually set (hard to track).
        // Best approach: If we are pooling, we *must* regenerate ID based on position, 
        // otherwise all recycled chunks share the same ID as their previous life.
        
        // We will regenerate uniqueID if it was NOT set in inspector (we assume empty string means auto).
        // But 'uniqueId' variable is serialized. 
        // We need to keep the "original inspector value" separate? No.
        // Let's just regenerate it every OnEnable if UseGlobalRegistry is true.
        // Wait, if user SET a specific ID in inspector, they want THAT ID.
        // Use a private flag? 
        // Actually, for procedurally generated chunks, the ID *must* be position-based.
        // If the user manually placed a SpawnArea in a fixed scene, they might want a fixed ID.
        // But these are CHUNKS. They move.
        // So we should ALWAYS regenerate the ID based on current position.
        
        uniqueId = GenerateUniqueId();
        
        // Check if already spawned in global registry
        if (useGlobalRegistry && SpawnAreaRegistry.Instance.HasSpawned(uniqueId))
        {
            hasSpawned = true;
        }
    }
    
    private string GenerateUniqueId()
    {
        // Create a unique ID based on name and position
        Vector3 pos = transform.position;
        return $"{gameObject.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
    }

    private float activationDistanceSqr; // Cached squared distance for performance
    
    private void Update()
    {
        if (isSpawning) return; // Don't trigger again while spawn sequence is running
        if (player == null) return;
        
        // Check for 2nd wave trigger (if enabled and wave 1 completed)
        if (enableSecondWave && hasSpawned && currentWave == 1 && !wave2Triggered && !isCheckingForWave2)
        {
            if (AllEnemiesDefeated())
            {
                isCheckingForWave2 = true;
                StartCoroutine(TriggerWave2AfterDelay());
            }
        }
        
        // Don't check for initial spawn if already spawned and oneTimeSpawn is true
        if (hasSpawned && oneTimeSpawn && (!enableSecondWave || wave2Triggered)) return;

        // Use sqrMagnitude instead of Distance to avoid expensive sqrt calculation
        Vector3 checkPosition = GetActivationCenter();
        float distanceSqr = (checkPosition - player.position).sqrMagnitude;
        
        if (distanceSqr <= activationDistanceSqr && !hasSpawned)
        {
            SpawnEnemies();
        }
    }
    
    private IEnumerator TriggerWave2AfterDelay()
    {
        yield return new WaitForSeconds(wave2SpawnDelay);
        
        wave2Triggered = true;
        currentWave = 2;
        spawnedEnemies.Clear(); // Clear references to dead enemies
        
        SpawnWave2();
    }
    
    private void SpawnWave2()
    {
        if (isSpawning) return;
        
        // Use wave 2 prefabs if set, otherwise fall back to wave 1 prefabs
        GameObject[] prefabsToUse = (wave2EnemyPrefabs != null && wave2EnemyPrefabs.Length > 0) 
            ? wave2EnemyPrefabs 
            : enemyPrefabs;
            
        if (prefabsToUse == null || prefabsToUse.Length == 0)
        {
            Debug.LogWarning("[SpawnArea] No prefabs for wave 2!");
            return;
        }
        
        StartCoroutine(SpawnWave2Coroutine(prefabsToUse));
    }
    
    private IEnumerator SpawnWave2Coroutine(GameObject[] prefabs)
    {
        isSpawning = true;
        
        // Use the generalized CollectSpawnData method to get wave 2 positions
        // This ensures it follows the exact same logic (points vs volume) as wave 1
        // We pass the wave 2 prefabs and wave 2 count (used only if in volume mode)
        List<SpawnData> wave2Data = CollectSpawnData(prefabs, wave2EnemyCount);
        
        // Show indicators if enabled
        if (useSpawnIndicator && indicatorPrefab != null)
        {
            foreach (var data in wave2Data)
            {
                ShowIndicator(data.position, data.rotation);
            }
            yield return new WaitForSeconds(indicatorDuration);
        }
        
        // Spawn wave 2 enemies
        for (int i = 0; i < wave2Data.Count; i++)
        {
            var data = wave2Data[i];
            SpawnEnemyAt(data.prefab, data.position, data.rotation, i);
            
            if (spawnStagger > 0 && i < wave2Data.Count - 1)
            {
                yield return new WaitForSeconds(spawnStagger);
            }
        }
        
        ClearIndicators();
        isSpawning = false;
    }

    private Vector3 GetActivationCenter()
    {
        if (useVolumeForActivation && volumeArea != null)
            return volumeArea.bounds.center;
        return transform.position;
    }

    private void CollectChildSpawnPoints()
    {
        List<Transform> points = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (volumeArea == null || child.gameObject != volumeArea.gameObject)
                points.Add(child);
        }
        spawnPoints = points.ToArray();
    }

    [ContextMenu("Spawn Enemies Now")]
    public void SpawnEnemies()
    {
        if (hasSpawned && oneTimeSpawn) return;
        if (isSpawning) return;
        
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("[SpawnArea] No enemy prefabs assigned!");
            return;
        }

        // Start the spawn sequence (with or without indicators)
        if (useSpawnIndicator && indicatorPrefab != null)
        {
            StartCoroutine(SpawnWithIndicatorsCoroutine());
        }
        else
        {
            StartCoroutine(SpawnImmediateCoroutine());
        }
    }

    /// <summary>
    /// Spawns enemies immediately (with optional stagger delay, no indicators).
    /// </summary>
    private IEnumerator SpawnImmediateCoroutine()
    {
        isSpawning = true;

        // Use the same data collection logic for consistency
        List<SpawnData> spawnDataList = CollectSpawnData();

        for (int i = 0; i < spawnDataList.Count; i++)
        {
            var data = spawnDataList[i];
            SpawnEnemyAt(data.prefab, data.position, data.rotation, i);

            if (spawnStagger > 0 && i < spawnDataList.Count - 1)
            {
                yield return new WaitForSeconds(spawnStagger);
            }
        }

        FinishSpawning();
    }

    /// <summary>
    /// Shows indicators first, waits, then spawns enemies.
    /// </summary>
    private IEnumerator SpawnWithIndicatorsCoroutine()
    {
        isSpawning = true;
        
        // Collect spawn data first
        List<SpawnData> spawnDataList = CollectSpawnData();
        
        // Show all indicators
        foreach (var data in spawnDataList)
        {
            ShowIndicator(data.position, data.rotation);
        }
        
        // Wait for indicator duration
        yield return new WaitForSeconds(indicatorDuration);
        
        // Spawn enemies at indicator positions (with stagger)
        for (int i = 0; i < spawnDataList.Count; i++)
        {
            var data = spawnDataList[i];
            SpawnEnemyAt(data.prefab, data.position, data.rotation, i);
            
            if (spawnStagger > 0 && i < spawnDataList.Count - 1)
            {
                yield return new WaitForSeconds(spawnStagger);
            }
        }
        
        // Clean up indicators
        ClearIndicators();
        
        FinishSpawning();
    }

    /// <summary>
    /// Data structure to hold spawn information.
    /// </summary>
    private struct SpawnData
    {
        public GameObject prefab;
        public Vector3 position;
        public Quaternion rotation;
    }

    /// <summary>
    /// Collects all spawn positions and prefabs based on current settings.
    /// Allows overrides for Wave 2 or other variations.
    /// </summary>
    /// <param name="prefabsOverride">Optional. Use these prefabs instead of default. (e.g. Wave 2 list)</param>
    /// <param name="countOverride">Optional. Use this count for random volume spawning. (e.g. Wave 2 count)</param>
    private List<SpawnData> CollectSpawnData(GameObject[] prefabsOverride = null, int countOverride = -1)
    {
        List<SpawnData> dataList = new List<SpawnData>();
        
        // Determine which config to use
        GameObject[] targetPrefabs = (prefabsOverride != null && prefabsOverride.Length > 0) ? prefabsOverride : enemyPrefabs;
        int targetCount = (countOverride >= 0) ? countOverride : volumeSpawnCount;

        if (useOnlyVolumeRandomPositions && volumeArea != null)
        {
            // Volume random mode
            for (int i = 0; i < targetCount; i++)
            {
                GameObject prefab = randomizePrefabs 
                    ? targetPrefabs[Random.Range(0, targetPrefabs.Length)]
                    : targetPrefabs[i % targetPrefabs.Length];
                    
                if (prefab == null) continue;

                dataList.Add(new SpawnData
                {
                    prefab = prefab,
                    position = GetRandomPositionInVolume(),
                    rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0)
                });
            }
        }
        else
        {
            // Spawn points mode
            if (spawnPoints == null || spawnPoints.Length == 0) return dataList;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;

                GameObject prefab = randomizePrefabs
                    ? targetPrefabs[Random.Range(0, targetPrefabs.Length)]
                    : targetPrefabs[i % targetPrefabs.Length];

                if (prefab == null) continue;

                Vector3 spawnPos = spawnPoints[i].position;
                Quaternion spawnRot = spawnPoints[i].rotation;

                if (randomizePositions && volumeArea != null)
                {
                    spawnPos = GetRandomPositionInVolume();
                    spawnRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                }
                else if (positionRandomOffset > 0)
                {
                    Vector2 offset = Random.insideUnitCircle * positionRandomOffset;
                    spawnPos += new Vector3(offset.x, 0, offset.y);
                }

                dataList.Add(new SpawnData
                {
                    prefab = prefab,
                    position = spawnPos,
                    rotation = spawnRot
                });
            }
        }

        return dataList;
    }

    /// <summary>
    /// Shows an indicator at the specified position.
    /// </summary>
    private void ShowIndicator(Vector3 position, Quaternion rotation)
    {
        if (indicatorPrefab == null) return;
        
        // Apply configurable position offset
        Vector3 indicatorPos = position + indicatorPositionOffset;
        
        // Apply configurable rotation offset
        Quaternion indicatorRotation = Quaternion.Euler(indicatorRotationOffset);
        
        GameObject indicator = ObjectPoolManager.Instance != null 
            ? ObjectPoolManager.Instance.Get(indicatorPrefab, indicatorPos, indicatorRotation)
            : Instantiate(indicatorPrefab, indicatorPos, indicatorRotation);
            
        indicator.name = "SpawnIndicator";
        activeIndicators.Add(indicator);
    }

    /// <summary>
    /// Clears all active indicators.
    /// </summary>
    private void ClearIndicators()
    {
        foreach (var indicator in activeIndicators)
        {
            if (indicator != null)
            {
                if (ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.Return(indicator);
                else
                    Destroy(indicator);
            }
        }
        activeIndicators.Clear();
    }

    /// <summary>
    /// Spawns a single enemy at the given position.
    /// </summary>
    private void SpawnEnemyAt(GameObject prefab, Vector3 position, Quaternion rotation, int index)
    {
        // Spawn VFX if configured - with configurable offset
        if (spawnVFXPrefab != null)
        {
            Vector3 vfxPos = position + spawnVFXPositionOffset;
            Quaternion vfxRotation = Quaternion.Euler(spawnVFXRotationOffset);
            
            GameObject vfx = ObjectPoolManager.Instance != null 
                ? ObjectPoolManager.Instance.Get(spawnVFXPrefab, vfxPos, vfxRotation)
                : Instantiate(spawnVFXPrefab, vfxPos, vfxRotation);
                
            // Auto-cleanup VFX
            if (ObjectPoolManager.Instance != null)
            {
                StartCoroutine(ReturnVFXAfterDelay(vfx, 3f));
            }
            else
            {
                Destroy(vfx, 3f); 
            }
        }
        
        // Play spawn sound
        if (spawnSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayAtPosition(spawnSound, position);
        
        GameObject enemy = ObjectPoolManager.Instance != null
            ? ObjectPoolManager.Instance.Get(prefab, position, rotation)
            : Instantiate(prefab, position, rotation);
            
        enemy.name = $"{prefab.name}_{index}";
        spawnedEnemies.Add(enemy);
    }
    
    // Helper for returning VFX to pool
    private IEnumerator ReturnVFXAfterDelay(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Return(vfx);
        }
    }

    /// <summary>
    /// Spawns at spawn points with optional stagger (no indicators).
    /// </summary>
    private IEnumerator SpawnAtPointsCoroutine(bool showIndicators)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnArea] No spawn points found!");
            yield break;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;

            GameObject prefab = randomizePrefabs
                ? enemyPrefabs[Random.Range(0, enemyPrefabs.Length)]
                : enemyPrefabs[i % enemyPrefabs.Length];

            if (prefab == null) continue;

            Vector3 spawnPos = spawnPoints[i].position;
            Quaternion spawnRot = spawnPoints[i].rotation;

            if (randomizePositions && volumeArea != null)
            {
                spawnPos = GetRandomPositionInVolume();
                spawnRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
            else if (positionRandomOffset > 0)
            {
                Vector2 offset = Random.insideUnitCircle * positionRandomOffset;
                spawnPos += new Vector3(offset.x, 0, offset.y);
            }

            SpawnEnemyAt(prefab, spawnPos, spawnRot, i);

            if (spawnStagger > 0 && i < spawnPoints.Length - 1)
            {
                yield return new WaitForSeconds(spawnStagger);
            }
        }
    }

    /// <summary>
    /// Spawns at random volume positions with optional stagger (no indicators).
    /// </summary>
    private IEnumerator SpawnAtRandomVolumePositionsCoroutine(bool showIndicators)
    {
        for (int i = 0; i < volumeSpawnCount; i++)
        {
            GameObject prefab = randomizePrefabs
                ? enemyPrefabs[Random.Range(0, enemyPrefabs.Length)]
                : enemyPrefabs[i % enemyPrefabs.Length];

            if (prefab == null) continue;

            Vector3 spawnPos = GetRandomPositionInVolume();
            Quaternion spawnRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            SpawnEnemyAt(prefab, spawnPos, spawnRot, i);

            if (spawnStagger > 0 && i < volumeSpawnCount - 1)
            {
                yield return new WaitForSeconds(spawnStagger);
            }
        }
    }

    /// <summary>
    /// Finalizes the spawn sequence.
    /// </summary>
    private void FinishSpawning()
    {
        hasSpawned = true;
        isSpawning = false;
        currentWave = 1; // Mark as wave 1 complete (enables wave 2 detection)
        
        if (useGlobalRegistry)
        {
            SpawnAreaRegistry.Instance.MarkAsSpawned(uniqueId);
        }
    }

    // SpawnAtRandomVolumePositions removed - now handled by SpawnAtRandomVolumePositionsCoroutine

    private Vector3 GetRandomPositionInVolume()
    {
        if (volumeArea == null) return transform.position;

        Bounds bounds = volumeArea.bounds;
        
        // Get random point within bounds
        Vector3 randomPoint = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y, // Keep Y at center (ground level)
            Random.Range(bounds.min.z, bounds.max.z)
        );

        return randomPoint;
    }

    [ContextMenu("Despawn All")]
    public void DespawnAll()
    {
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                if (ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.Return(enemy);
                else
                    Destroy(enemy);
            }
        }
        spawnedEnemies.Clear();
        hasSpawned = false;
    }

    public bool AllEnemiesDefeated()
    {
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                var health = enemy.GetComponent<EnemyHealth>();
                if (health != null && !health.IsDead)
                    return false;
            }
        }
        return hasSpawned;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Vector3 center = GetActivationCenter();
        
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.15f);
        Gizmos.DrawSphere(center, activationDistance);
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(center, activationDistance);

        Transform[] points = spawnPoints;
        if (points == null || points.Length == 0)
        {
            List<Transform> children = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (volumeArea == null || child.gameObject != volumeArea.gameObject)
                    children.Add(child);
            }
            points = children.ToArray();
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            
            // Get the prefab that would spawn at this index (same logic as SpawnEnemies)
            string enemyName = "";
            if (enemyPrefabs != null && enemyPrefabs.Length > 0)
            {
                GameObject prefab = enemyPrefabs[i % enemyPrefabs.Length];
                if (prefab != null)
                {
                    enemyName = prefab.name;
                    Gizmos.color = GetColorFromPrefabName(enemyName);
                }
                else
                {
                    Gizmos.color = Color.gray;
                }
            }
            else
            {
                Gizmos.color = Color.gray;
            }
            
            Gizmos.DrawSphere(points[i].position, 0.5f);
            
            // NOTE: Handles.Label REMOVED for performance - was causing 30+ FPS drops!
            // Uncomment below if you need to see enemy names at spawn points (will hurt editor FPS)
            // #if UNITY_EDITOR
            // string label = !string.IsNullOrEmpty(enemyName) ? enemyName : $"[{i}]";
            // UnityEditor.Handles.Label(points[i].position + Vector3.up, label);
            // #endif
        }
    }
    
    /// <summary>
    /// Determines gizmo color based on prefab name.
    /// Chaser = Green, Bomber = Red, Sniper = Blue, Miniboss = Magenta
    /// </summary>
    private Color GetColorFromPrefabName(string prefabName)
    {
        string nameLower = prefabName.ToLower();
        
        if (nameLower.Contains("chaser"))
            return Color.green;
        if (nameLower.Contains("bomber"))
            return Color.red;
        if (nameLower.Contains("sniper"))
            return Color.blue;
        if (nameLower.Contains("miniboss") || nameLower.Contains("boss"))
            return Color.magenta;
            
        // Default color for unknown enemy types
        return Color.yellow;
    }
    
    private Color GetColorForEnemyType(EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Chaser:
                return Color.green;
            case EnemyType.Bomber:
                return Color.red;
            case EnemyType.Sniper:
                return Color.blue;
            case EnemyType.Miniboss:
                return Color.magenta;
            case EnemyType.Custom:
            default:
                return Color.yellow;
        }
    }
}
