using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Orchestrates the loading process within the GameScene.
/// Pre-warms object pools and initializes the world before letting the Loading Screen fade out.
/// </summary>
public class AssetPreloader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChunkManager chunkManager;
    
    [Header("Preload Configuration")]
    [SerializeField] private List<PreloadItem> projectilesToPreload;
    [SerializeField] private List<PreloadItem> enemiesToPreload;
    [SerializeField] private List<PreloadItem> vfxToPreload;
    
    [Header("Settings")]
    [SerializeField] private int chunkPrewarmCount = 3; // How many instances of EACH chunk prefab to prewarm
    [SerializeField] private int itemsPerFrame = 5; // Low number to keep loading screen smooth

    [System.Serializable]
    public class PreloadItem
    {
        public GameObject prefab;
        public int count = 20;
    }

    private void Start()
    {
        StartCoroutine(LoadingRoutine());
    }

    private IEnumerator LoadingRoutine()
    {
        // 1. Wait for ObjectPoolManager
        while (ObjectPoolManager.Instance == null)
        {
            yield return null;
        }

        // Calculate total items to preload for progress bar
        int totalItems = 0;
        int processedItems = 0;

        // Chunks (Prefabs * Count)
        var allChunks = (chunkManager != null) ? chunkManager.GetAllBiomePrefabs() : new HashSet<GameObject>();
        totalItems += allChunks.Count * chunkPrewarmCount;

        // Projectiles
        if (projectilesToPreload != null)
            foreach (var item in projectilesToPreload) totalItems += item.count;
            
        // VFX
        if (vfxToPreload != null)
            foreach (var item in vfxToPreload) totalItems += item.count;

        // Enemies
        if (enemiesToPreload != null)
            foreach (var item in enemiesToPreload) totalItems += item.count;

        // Avoid divide by zero
        if (totalItems == 0) totalItems = 1;

        // 2. Prewarm Map Chunks
        if (chunkManager != null)
        {
            chunkManager.autoInitialize = false;
            foreach (var chunkPrefab in allChunks)
            {
                // We preload 'chunkPrewarmCount' instances of this prefab
                // The PrewarmAsync spreads this over frames, but we want to track it
                // Since PrewarmAsync handles the loop internally, we can't get per-item callback easily without modifying it.
                // Simplified approach: Wait for the batch, then increment processed count by batch size.
                yield return ObjectPoolManager.Instance.PrewarmAsync(chunkPrefab, chunkPrewarmCount, itemsPerFrame);
                
                processedItems += chunkPrewarmCount;
                ReportProgress(processedItems, totalItems);
            }
        }
        else
        {
            Debug.LogWarning("[AssetPreloader] ChunkManager reference missing!");
        }

        // 3. Prewarm Projectiles
        if (projectilesToPreload != null)
        {
            foreach (var item in projectilesToPreload)
            {
                if (item.prefab != null)
                {
                    yield return ObjectPoolManager.Instance.PrewarmAsync(item.prefab, item.count, itemsPerFrame);
                    processedItems += item.count;
                    ReportProgress(processedItems, totalItems);
                }
            }
        }

        // 4. Prewarm VFX
        if (vfxToPreload != null)
        {
            foreach (var item in vfxToPreload)
            {
                if (item.prefab != null)
                {
                    yield return ObjectPoolManager.Instance.PrewarmAsync(item.prefab, item.count, itemsPerFrame);
                    processedItems += item.count;
                    ReportProgress(processedItems, totalItems);
                }
            }
        }

        // 4.5. Prewarm Enemies
        if (enemiesToPreload != null)
        {
            foreach (var item in enemiesToPreload)
            {
                if (item.prefab != null)
                {
                    yield return ObjectPoolManager.Instance.PrewarmAsync(item.prefab, item.count, itemsPerFrame);
                    processedItems += item.count;
                    ReportProgress(processedItems, totalItems);
                }
            }
        }

        // 5. Force GC to clear temp lists
        System.GC.Collect();
        yield return null;

        // 6. Initialize World (Construct initial chunks using the now-warmed pool)
        if (chunkManager != null)
        {
            chunkManager.Initialize();
        }

        Debug.Log($"[AssetPreloader] <color=green>Preloading Complete!</color> System is ready. Starting Game.");

        // 7. Signal Loading Screen to Finish
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.OnSceneReady();
        }
        else
        {
            // Fallback for direct scene play in Editor
            Debug.Log("[AssetPreloader] Loading Complete (No LoadingScreenManager found).");
        }
    }

    private void ReportProgress(int current, int total)
    {
        if (LoadingScreenManager.Instance != null)
        {
            float p = (float)current / total;
            LoadingScreenManager.Instance.ReportProgress(p);
        }
    }
}
