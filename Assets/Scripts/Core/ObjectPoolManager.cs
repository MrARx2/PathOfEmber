using UnityEngine;
using UnityEngine.Pool;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Centralized object pooling manager using Unity's built-in ObjectPool.
/// Eliminates Instantiate/Destroy overhead for frequently spawned objects.
/// Usage:
///   - ObjectPoolManager.Instance.Get(prefab, position, rotation)
///   - ObjectPoolManager.Instance.Return(instance)
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [Header("Pool Configuration")]
    [SerializeField, Tooltip("Default pool size for new prefabs")]
    private int defaultPoolSize = 10;
    [SerializeField, Tooltip("Maximum pool size before objects are destroyed instead of returned")]
    private int maxPoolSize = 100;

    // Dictionary mapping prefab -> pool
    // We also need to track which prefab each instance came from
    private Dictionary<GameObject, ObjectPool<GameObject>> _pools = new Dictionary<GameObject, ObjectPool<GameObject>>();
    private Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Gets an object from the pool (or creates a new pool if needed).
    /// The object is positioned and activated automatically.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPoolManager] Cannot get null prefab!");
            return null;
        }

        // Get or create pool for this prefab
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = CreatePool(prefab);
            _pools[prefab] = pool;
        }

        // Get from pool
        GameObject instance = pool.Get();
        
        // Position and activate
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        
        // Track which prefab this instance came from
        _instanceToPrefab[instance] = prefab;

        return instance;
    }

    /// <summary>
    /// Returns an object to its pool. The object is deactivated automatically.
    /// </summary>
    public void Return(GameObject instance)
    {
        if (instance == null) return;

        // Find the original prefab
        if (!_instanceToPrefab.TryGetValue(instance, out var prefab))
        {
            // Not from our pool - just destroy it normally
            Destroy(instance);
            return;
        }

        // Get the pool
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            // Pool was somehow removed - destroy
            Destroy(instance);
            _instanceToPrefab.Remove(instance);
            return;
        }

        // Deactivate and return to pool
        instance.SetActive(false);
        pool.Release(instance);
    }

    /// <summary>
    /// Creates a new pool for the given prefab.
    /// </summary>
    private ObjectPool<GameObject> CreatePool(GameObject prefab)
    {
        return new ObjectPool<GameObject>(
            createFunc: () => CreatePooledObject(prefab),
            actionOnGet: OnGetFromPool,
            actionOnRelease: OnReturnToPool,
            actionOnDestroy: OnDestroyPooledObject,
            collectionCheck: true,
            defaultCapacity: defaultPoolSize,
            maxSize: maxPoolSize
        );
    }

    private GameObject CreatePooledObject(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.SetActive(false);
        return instance;
    }

    private void OnGetFromPool(GameObject obj)
    {
        // Object is about to be used - caller will position and activate
    }

    private void OnReturnToPool(GameObject obj)
    {
        // Object is being returned - deactivate
        obj.SetActive(false);
        obj.transform.SetParent(transform); // Keep organized under pool manager
    }

    private void OnDestroyPooledObject(GameObject obj)
    {
        // Pool is at max size - destroy overflow
        if (_instanceToPrefab.ContainsKey(obj))
            _instanceToPrefab.Remove(obj);
        Destroy(obj);
    }

    /// <summary>
    /// Pre-warms a pool by creating instances ahead of time.
    /// Call during loading screens for zero-allocation gameplay.
    /// </summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        // Ensure pool exists
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = CreatePool(prefab);
            _pools[prefab] = pool;
        }

        // Create and immediately return objects to fill the pool
        var tempList = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
        {
            var obj = pool.Get();
            _instanceToPrefab[obj] = prefab;
            tempList.Add(obj);
        }
        
        foreach (var obj in tempList)
        {
            obj.SetActive(false);
            pool.Release(obj);
        }
    }

    /// <summary>
    /// Pre-warms a pool asynchronously (spread over multiple frames).
    /// Prevents lag spikes during loading screens.
    /// </summary>
    public IEnumerator PrewarmAsync(GameObject prefab, int count, int itemsPerFrame)
    {
        if (prefab == null || count <= 0) yield break;

        // Ensure pool exists
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = CreatePool(prefab);
            _pools[prefab] = pool;
        }

        var tempList = new List<GameObject>(itemsPerFrame);
        int totalCreated = 0;

        while (totalCreated < count)
        {
            int batchSize = Mathf.Min(itemsPerFrame, count - totalCreated);
            tempList.Clear();

            for (int i = 0; i < batchSize; i++)
            {
                GameObject obj = pool.Get();
                obj.SetActive(false); // Ensure inactive IMMEDIATELY
                _instanceToPrefab[obj] = prefab;
                tempList.Add(obj);
            }

            // Return immediately
            foreach (var obj in tempList)
            {
                obj.SetActive(false);
                pool.Release(obj);
            }

            totalCreated += batchSize;
            yield return null; // Wait for next frame
        }
    }

    /// <summary>
    /// Clears all pools and destroys all pooled objects. Call when changing scenes.
    /// </summary>
    public void ClearAllPools()
    {
        // Destroy all tracked instances first (prevents memory leak on scene reload)
        foreach (var instance in _instanceToPrefab.Keys)
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
        
        // Now clear the data structures
        foreach (var pool in _pools.Values)
        {
            pool.Clear();
        }
        _pools.Clear();
        _instanceToPrefab.Clear();
    }
}
