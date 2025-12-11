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

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.red;

    private bool hasSpawned = false;
    private List<GameObject> spawnedEnemies = new List<GameObject>();

    public bool HasSpawned => hasSpawned;
    public List<GameObject> SpawnedEnemies => spawnedEnemies;

    private void Start()
    {
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
    }

    private void Update()
    {
        if (hasSpawned && oneTimeSpawn) return;
        if (player == null) return;

        Vector3 checkPosition = GetActivationCenter();
        float distance = Vector3.Distance(checkPosition, player.position);
        
        if (distance <= activationDistance)
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
                prefab = enemyPrefabs[i % enemyPrefabs.Length]; // Cycle through
            }

            if (prefab == null) continue;

            GameObject enemy = Instantiate(
                prefab,
                spawnPoints[i].position,
                spawnPoints[i].rotation
            );
            
            enemy.name = $"{prefab.name}_{i}";
            spawnedEnemies.Add(enemy);
        }

        hasSpawned = true;
        Debug.Log($"[SpawnArea] Spawned {spawnedEnemies.Count} enemies at {gameObject.name}");
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
            
            float t = points.Length > 1 ? (float)i / (points.Length - 1) : 0f;
            Gizmos.color = Color.Lerp(Color.red, Color.green, t);
            Gizmos.DrawSphere(points[i].position, 0.5f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(points[i].position + Vector3.up, $"[{i}]");
            #endif
        }
    }
}
