using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton registry that tracks which SpawnAreas have already spawned.
/// This survives chunk recycling - once a SpawnArea has spawned, it won't spawn again
/// even if the parent chunk is destroyed and re-instantiated.
/// </summary>
public class SpawnAreaRegistry : MonoBehaviour
{
    private static SpawnAreaRegistry instance;
    public static SpawnAreaRegistry Instance
    {
        get
        {
            if (instance == null)
            {
                // Try to find existing instance
                instance = FindFirstObjectByType<SpawnAreaRegistry>();
                
                // Create new instance if none exists
                if (instance == null)
                {
                    var go = new GameObject("SpawnAreaRegistry");
                    instance = go.AddComponent<SpawnAreaRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    // Tracks SpawnArea unique IDs that have already spawned
    private HashSet<string> spawnedAreaIds = new HashSet<string>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // When returning to the Main Menu, we assume the run is over.
        // Clear the registry so the next run starts fresh.
        if (scene.name == "Main_Menu" || scene.name == "MainMenu")
        {
            ClearAll();
        }
    }

    /// <summary>
    /// Check if a SpawnArea has already spawned.
    /// </summary>
    public bool HasSpawned(string areaId)
    {
        return spawnedAreaIds.Contains(areaId);
    }

    /// <summary>
    /// Mark a SpawnArea as having spawned.
    /// </summary>
    public void MarkAsSpawned(string areaId)
    {
        if (!spawnedAreaIds.Contains(areaId))
        {
            spawnedAreaIds.Add(areaId);
            Debug.Log($"[SpawnAreaRegistry] Marked '{areaId}' as spawned. Total tracked: {spawnedAreaIds.Count}");
        }
    }

    /// <summary>
    /// Clear a specific SpawnArea's spawned status (for respawning).
    /// </summary>
    public void ClearSpawnedStatus(string areaId)
    {
        if (spawnedAreaIds.Remove(areaId))
        {
            Debug.Log($"[SpawnAreaRegistry] Cleared spawned status for '{areaId}'");
        }
    }

    /// <summary>
    /// Clear all spawned tracking (for game restart/reset).
    /// </summary>
    public void ClearAll()
    {
        spawnedAreaIds.Clear();
        Debug.Log("[SpawnAreaRegistry] Cleared all spawned tracking");
    }

    /// <summary>
    /// Get count of tracked spawn areas.
    /// </summary>
    public int GetSpawnedCount() => spawnedAreaIds.Count;
}
