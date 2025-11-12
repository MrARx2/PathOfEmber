using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject firstChunkPrefab;
    public GameObject chunkPrefab;
    
    [Header("Chunk Settings")]
    [Tooltip("Length of each chunk on the Z-axis (forward direction)")]
    public float chunkLength = 10f;
    
    [Tooltip("Gap distance between chunks")]
    public float chunkGap = 0f;
    
    [Tooltip("How many chunks to keep loaded ahead and behind the player")]
    public int chunksAhead = 3;
    public int chunksBehind = 1;

    private Dictionary<int, GameObject> activeChunks = new Dictionary<int, GameObject>();
    private int currentChunkIndex = 0;
    private Vector3 firstChunkPosition;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("ChunkManager: Player not assigned!");
            return;
        }

        if (chunkPrefab == null)
        {
            Debug.LogError("ChunkManager: Chunk prefab not assigned!");
            return;
        }

        // Store the first chunk's position as reference
        if (firstChunkPrefab != null)
        {
            firstChunkPosition = firstChunkPrefab.transform.position;
        }
        else
        {
            firstChunkPosition = Vector3.zero;
        }

        // Load initial chunks
        UpdateChunks();
    }

    private void Update()
    {
        if (player == null) return;

        // Check which chunk the player is in (relative to first chunk position)
        float relativeZ = player.position.z - firstChunkPosition.z;
        int playerChunkIndex = Mathf.FloorToInt(relativeZ / (chunkLength + chunkGap));
        
        // Update chunks if player moved to a new chunk
        if (playerChunkIndex != currentChunkIndex)
        {
            currentChunkIndex = playerChunkIndex;
            UpdateChunks();
        }
    }

    private void UpdateChunks()
    {
        // Determine which chunks should be active
        // Start from chunk 0 (first chunk) and go forward
        int startChunk = Mathf.Max(0, currentChunkIndex - chunksBehind);
        int endChunk = currentChunkIndex + chunksAhead;

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
        // Use first chunk prefab for chunk 0, otherwise use regular prefab
        GameObject prefabToUse = (chunkIndex == 0 && firstChunkPrefab != null) ? firstChunkPrefab : chunkPrefab;

        // Calculate position along Z-axis, starting from first chunk position
        // Each chunk is placed at: firstChunkPosition + (chunkIndex * (chunkLength + chunkGap))
        float zOffset = chunkIndex * (chunkLength + chunkGap);
        Vector3 position = firstChunkPosition + new Vector3(0, 0, zOffset);

        // Spawn chunk with prefab's original rotation
        GameObject chunk = Instantiate(prefabToUse, position, prefabToUse.transform.rotation, transform);
        chunk.name = $"Chunk_{chunkIndex}";

        // Add debug color to each chunk (different color per chunk)
        AddDebugColor(chunk, chunkIndex);

        activeChunks[chunkIndex] = chunk;
    }

    private void AddDebugColor(GameObject chunk, int chunkIndex)
    {
        // Get all renderers in the chunk
        Renderer[] renderers = chunk.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length == 0) return;

        // Generate a unique color based on chunk index
        Color debugColor = GetDebugColor(chunkIndex);

        // Apply color to all renderers
        foreach (Renderer renderer in renderers)
        {
            // Create a new material instance to avoid modifying the prefab
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].color = debugColor;
            }
            renderer.materials = materials;
        }
    }

    private Color GetDebugColor(int chunkIndex)
    {
        // Generate different colors for each chunk
        // Use HSV to get evenly distributed colors
        float hue = (chunkIndex * 0.1f) % 1f; // Different hue for each chunk
        return Color.HSVToRGB(hue, 0.7f, 0.9f);
    }

    private void UnloadChunk(int chunkIndex)
    {
        if (activeChunks.TryGetValue(chunkIndex, out GameObject chunk))
        {
            Destroy(chunk);
            activeChunks.Remove(chunkIndex);
        }
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
