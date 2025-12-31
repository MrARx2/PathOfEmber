using UnityEngine;
using System.Collections.Generic;

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

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.red;

    private bool hasSpawned = false;
    private List<GameObject> spawnedEnemies = new List<GameObject>();

    public bool HasSpawned => hasSpawned;
    public List<GameObject> SpawnedEnemies => spawnedEnemies;

    private void Start()
    {
        // Cache squared distance for performance (avoid sqrt in Update)
        activationDistanceSqr = activationDistance * activationDistance;
        
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            CollectChildSpawnPoints();
        }
        
        // Generate unique ID if not set
        if (string.IsNullOrEmpty(uniqueId))
        {
            uniqueId = GenerateUniqueId();
        }
        
        // Check if already spawned in global registry
        if (useGlobalRegistry && SpawnAreaRegistry.Instance.HasSpawned(uniqueId))
        {
            hasSpawned = true;
            Debug.Log($"[SpawnArea] '{uniqueId}' already spawned (from registry), skipping.");
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
        if (hasSpawned && oneTimeSpawn) return;
        if (player == null) return;

        // Use sqrMagnitude instead of Distance to avoid expensive sqrt calculation
        Vector3 checkPosition = GetActivationCenter();
        float distanceSqr = (checkPosition - player.position).sqrMagnitude;
        
        if (distanceSqr <= activationDistanceSqr)
        {
            SpawnEnemies();
        }
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
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("[SpawnArea] No enemy prefabs assigned!");
            return;
        }

        // Mode 1: Use only random positions within volume (ignore spawn points)
        if (useOnlyVolumeRandomPositions && volumeArea != null)
        {
            SpawnAtRandomVolumePositions();
            return;
        }

        // Mode 2: Use spawn points (with optional randomization)
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnArea] No spawn points found!");
            return;
        }

        // Spawn ONE enemy per spawn point
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;

            // Pick prefab: cycle through, or random
            GameObject prefab;
            if (randomizePrefabs)
            {
                prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            }
            else
            {
                prefab = enemyPrefabs[i % enemyPrefabs.Length];
            }

            if (prefab == null) continue;

            // Calculate spawn position
            Vector3 spawnPos = spawnPoints[i].position;
            Quaternion spawnRot = spawnPoints[i].rotation;

            if (randomizePositions && volumeArea != null)
            {
                // Use completely random position within volume
                spawnPos = GetRandomPositionInVolume();
                spawnRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
            else if (positionRandomOffset > 0)
            {
                // Add random offset to spawn point
                Vector2 offset = Random.insideUnitCircle * positionRandomOffset;
                spawnPos += new Vector3(offset.x, 0, offset.y);
            }

            GameObject enemy = Instantiate(prefab, spawnPos, spawnRot);
            enemy.name = $"{prefab.name}_{i}";
            spawnedEnemies.Add(enemy);
        }

        hasSpawned = true;
        
        // Register with global registry
        if (useGlobalRegistry)
        {
            SpawnAreaRegistry.Instance.MarkAsSpawned(uniqueId);
        }
        
        Debug.Log($"[SpawnArea] Spawned {spawnedEnemies.Count} enemies at {gameObject.name} (ID: {uniqueId})");
    }

    private void SpawnAtRandomVolumePositions()
    {
        for (int i = 0; i < volumeSpawnCount; i++)
        {
            GameObject prefab;
            if (randomizePrefabs)
            {
                prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            }
            else
            {
                prefab = enemyPrefabs[i % enemyPrefabs.Length];
            }

            if (prefab == null) continue;

            Vector3 spawnPos = GetRandomPositionInVolume();
            Quaternion spawnRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            GameObject enemy = Instantiate(prefab, spawnPos, spawnRot);
            enemy.name = $"{prefab.name}_vol_{i}";
            spawnedEnemies.Add(enemy);
        }

        hasSpawned = true;
        
        // Register with global registry
        if (useGlobalRegistry)
        {
            SpawnAreaRegistry.Instance.MarkAsSpawned(uniqueId);
        }
        
        Debug.Log($"[SpawnArea] Spawned {spawnedEnemies.Count} enemies randomly in volume at {gameObject.name} (ID: {uniqueId})");
    }

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
                Destroy(enemy);
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
            
            #if UNITY_EDITOR
            // Show the enemy name that will spawn here
            string label = !string.IsNullOrEmpty(enemyName) ? enemyName : $"[{i}]";
            UnityEditor.Handles.Label(points[i].position + Vector3.up, label);
            #endif
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
