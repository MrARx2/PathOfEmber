using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject firstChunkPrefab;
    public GameObject lastChunkPrefab;

    [Header("Biome Prefabs")]
    public GameObject[] lavaBiomePrefabs;
    public GameObject[] grassBiomePrefabs;
    public GameObject[] mudBiomePrefabs;

    [Header("Chunk Settings")]
    [Tooltip("Length of each chunk on the Z-axis (forward direction)")]
    public float chunkLength = 10f;

    [Tooltip("Gap distance between chunks")]
    public float chunkGap = 0f;

    [Tooltip("How many chunks to keep loaded ahead and behind the player")]
    public int chunksAhead = 3;
    public int chunksBehind = 1;

    [Header("Initialization")]
    [Tooltip("If true, functionality runs in Start(). If false, waits for Initialize() call.")]
    public bool autoInitialize = true;

    private Dictionary<int, GameObject> activeChunks = new Dictionary<int, GameObject>();
    private int currentChunkIndex = 0;
    private Vector3 firstChunkPosition;
    private readonly List<GameObject> chunkSequence = new List<GameObject>();
    private bool isInitialized = false;

    public System.Action OnInitialGenerationFinished;

    private void BuildChunkSequence()
    {
        chunkSequence.Clear();

        if (firstChunkPrefab == null)
        {
            Debug.LogError("ChunkManager: First chunk prefab is not assigned.");
        }
        else
        {
            // Index 0: starting chunk
            chunkSequence.Add(firstChunkPrefab);
        }

        AddBiomePrefabs(lavaBiomePrefabs, "Lava biome");
        AddBiomePrefabs(grassBiomePrefabs, "Grass biome");
        AddBiomePrefabs(mudBiomePrefabs, "Mud biome");

        if (lastChunkPrefab != null)
        {
            chunkSequence.Add(lastChunkPrefab);
        }
    }

    private void AddBiomePrefabs(GameObject[] prefabs, string biomeName)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null)
            {
                continue;
            }

            chunkSequence.Add(prefab);
        }
    }

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("ChunkManager: Player not assigned!");
            return;
        }

        if (autoInitialize)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Initializes the chunk system. Call this from AssetPreloader.
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;

        BuildChunkSequence();

        if (chunkSequence.Count == 0)
        {
            Debug.LogError("ChunkManager: No chunk prefabs configured in sequence.");
            return;
        }

        // Store the first chunk's position as reference
        firstChunkPosition = transform.position;

        // Load initial chunks
        UpdateChunks();

        isInitialized = true;
        OnInitialGenerationFinished?.Invoke();
    }

    private void Update()
    {
        if (!isInitialized) return;
        if (player == null) return;
        if (chunkSequence.Count == 0) return;

        // Check which chunk the player is in (relative to first chunk position)
        float relativeZ = player.position.z - firstChunkPosition.z;
        int playerChunkIndex = Mathf.FloorToInt(relativeZ / (chunkLength + chunkGap));
        int maxIndex = chunkSequence.Count - 1;
        playerChunkIndex = Mathf.Clamp(playerChunkIndex, 0, maxIndex);

        // Update chunks if player moved to a new chunk
        if (playerChunkIndex != currentChunkIndex)
        {
            currentChunkIndex = playerChunkIndex;
            UpdateChunks();
        }
    }

    private void UpdateChunks()
    {
        if (chunkSequence.Count == 0)
        {
            return;
        }

        int maxIndex = chunkSequence.Count - 1;

        // Determine which chunks should be active
        // Start from chunk 0 (first chunk) and go forward
        int startChunk = Mathf.Max(0, currentChunkIndex - chunksBehind);
        int endChunk = Mathf.Min(currentChunkIndex + chunksAhead, maxIndex);

        // Load new chunks
        for (int i = startChunk; i <= endChunk; i++)
        {
            if (!activeChunks.ContainsKey(i))
            {
                LoadChunk(i);
            }
        }

        // Unload old chunks (including chunk 0 if player is far enough)
        List<int> chunksToRemove = new List<int>();
        foreach (var kvp in activeChunks)
        {
            if (kvp.Key < startChunk || kvp.Key > endChunk)
            {
                chunksToRemove.Add(kvp.Key);
            }
        }

        foreach (int chunkIndex in chunksToRemove)
        {
            UnloadChunk(chunkIndex);
        }
    }

    private void LoadChunk(int chunkIndex)
    {
        if (chunkIndex < 0 || chunkIndex >= chunkSequence.Count)
        {
            return;
        }

        // Use first chunk prefab for chunk 0, otherwise use regular prefab
        GameObject prefabToUse = chunkSequence[chunkIndex];

        // Calculate position along Z-axis, starting from first chunk position
        // Each chunk is placed at: firstChunkPosition + (chunkIndex * (chunkLength + chunkGap))
        float zOffset = chunkIndex * (chunkLength + chunkGap);
        Vector3 position = firstChunkPosition + new Vector3(0, 0, zOffset);

        // Use Instantiate via ObjectPoolManager if available, otherwise fallback
        GameObject chunk;
        if (ObjectPoolManager.Instance != null)
        {
            chunk = ObjectPoolManager.Instance.Get(prefabToUse, position, prefabToUse.transform.rotation);
            // Ensure parenting (PoolManager puts it under itself by default usually, but we want it organized?)
            // Actually ObjectPoolManager keeps them under itself when inactive. When active, we can reparent.
            // But for Chunks, let's keep them clean in hierarchy? 
            // NOTE: ObjectPoolManager.Get sets position/rotation but parent handling is up to us if we want specific hierarchy.
            // But ObjectPoolManager.Get does NOT allow setting parent in arguments.
            // Let's re-parent.
            chunk.transform.SetParent(transform);
        }
        else
        {
            chunk = Instantiate(prefabToUse, position, prefabToUse.transform.rotation, transform);
        }
        
        chunk.name = $"Chunk_{chunkIndex}";

        // Keep original materials; no debug coloring

        activeChunks[chunkIndex] = chunk;
    }

    

    private void UnloadChunk(int chunkIndex)
    {
        if (activeChunks.TryGetValue(chunkIndex, out GameObject chunk))
        {
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(chunk);
            }
            else
            {
                Destroy(chunk);
            }
            activeChunks.Remove(chunkIndex);
        }
    }

    /// <summary>
    /// Returns all unique prefabs used in the biome generation (for prewarming).
    /// </summary>
    public HashSet<GameObject> GetAllBiomePrefabs()
    {
        HashSet<GameObject> unique = new HashSet<GameObject>();
        
        if (firstChunkPrefab != null) unique.Add(firstChunkPrefab);
        if (lastChunkPrefab != null) unique.Add(lastChunkPrefab);
        
        if (lavaBiomePrefabs != null) foreach (var p in lavaBiomePrefabs) if(p) unique.Add(p);
        if (grassBiomePrefabs != null) foreach (var p in grassBiomePrefabs) if(p) unique.Add(p);
        if (mudBiomePrefabs != null) foreach (var p in mudBiomePrefabs) if(p) unique.Add(p);
        
        return unique;
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Vector3 referencePos = Application.isPlaying ? firstChunkPosition : Vector3.zero;

        Gizmos.color = Color.cyan;
        
        // Draw current chunk bounds
        float currentZOffset = currentChunkIndex * (chunkLength + chunkGap);
        Vector3 playerChunkPos = referencePos + new Vector3(0, 0, currentZOffset);
        Gizmos.DrawWireCube(playerChunkPos + Vector3.forward * chunkLength * 0.5f, new Vector3(10, 1, chunkLength));

        // Draw all active chunks
        Gizmos.color = Color.green;
        foreach (var kvp in activeChunks)
        {
            float zOffset = kvp.Key * (chunkLength + chunkGap);
            Vector3 chunkPos = referencePos + new Vector3(0, 0, zOffset);
            Gizmos.DrawWireCube(chunkPos + Vector3.forward * chunkLength * 0.5f, new Vector3(10, 1, chunkLength));
        }
    }
}
