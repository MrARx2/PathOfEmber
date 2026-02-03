using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to align chunk prefabs for lightmap baking.
/// Positions chunks exactly as ChunkManager does at runtime.
/// </summary>
public class ChunkAlignmentTool : EditorWindow
{
    [Header("References")]
    private Transform player;
    private Transform allChunksParent;
    
    [Header("Chunk Settings (must match ChunkManager)")]
    private float chunkLength = 10f;
    private float chunkGap = 0f;
    
    private bool preserveOriginalRotation = true;
    
    [MenuItem("Tools/Path of Ember/Level/Chunk Alignment")]
    public static void ShowWindow()
    {
        GetWindow<ChunkAlignmentTool>("Chunk Alignment");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Chunk Alignment for Lightmap Baking", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "This tool aligns chunks exactly as ChunkManager positions them at runtime.\n\n" +
            "1. Place your Player at the starting position\n" +
            "2. Place all chunk prefabs under a single parent (AllChunks)\n" +
            "3. Order the children in the Hierarchy as they should appear\n" +
            "4. Click 'Align Chunks'",
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // References
        GUILayout.Label("References", EditorStyles.boldLabel);
        player = (Transform)EditorGUILayout.ObjectField("Player (Start Position)", player, typeof(Transform), true);
        allChunksParent = (Transform)EditorGUILayout.ObjectField("AllChunks Parent", allChunksParent, typeof(Transform), true);
        
        EditorGUILayout.Space(10);
        
        // Settings
        GUILayout.Label("Chunk Settings (match ChunkManager)", EditorStyles.boldLabel);
        chunkLength = EditorGUILayout.FloatField("Chunk Length", chunkLength);
        chunkGap = EditorGUILayout.FloatField("Chunk Gap", chunkGap);
        preserveOriginalRotation = EditorGUILayout.Toggle("Preserve Prefab Rotation", preserveOriginalRotation);
        
        EditorGUILayout.Space(5);
        
        // Quick load from ChunkManager
        if (GUILayout.Button("Load Settings from ChunkManager"))
        {
            LoadFromChunkManager();
        }
        
        EditorGUILayout.Space(15);
        
        // Main action
        GUI.backgroundColor = Color.green;
        EditorGUI.BeginDisabledGroup(player == null || allChunksParent == null);
        if (GUILayout.Button("Align Chunks", GUILayout.Height(40)))
        {
            AlignChunks();
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = Color.white;
        
        if (player == null || allChunksParent == null)
        {
            EditorGUILayout.HelpBox("Assign Player and AllChunks Parent to enable alignment.", MessageType.Warning);
        }
        
        EditorGUILayout.Space(10);
        
        // Utility buttons
        GUILayout.Label("Utilities", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Count Children"))
        {
            if (allChunksParent != null)
            {
                int count = allChunksParent.childCount;
                float totalLength = count * (chunkLength + chunkGap) - chunkGap;
                Debug.Log($"[ChunkAlignmentTool] {count} chunks, Total length: {totalLength}m");
            }
        }
        
        if (GUILayout.Button("Mark All Static (for baking)"))
        {
            MarkAllStatic();
        }
    }
    
    private void LoadFromChunkManager()
    {
        ChunkManager chunkManager = FindFirstObjectByType<ChunkManager>();
        if (chunkManager != null)
        {
            chunkLength = chunkManager.chunkLength;
            chunkGap = chunkManager.chunkGap;
            
            if (chunkManager.player != null && player == null)
            {
                player = chunkManager.player;
            }
            
            Debug.Log($"[ChunkAlignmentTool] Loaded from ChunkManager: Length={chunkLength}, Gap={chunkGap}");
        }
        else
        {
            Debug.LogWarning("[ChunkAlignmentTool] No ChunkManager found in scene!");
        }
    }
    
    private void AlignChunks()
    {
        if (player == null || allChunksParent == null)
        {
            Debug.LogError("[ChunkAlignmentTool] Missing Player or AllChunks Parent!");
            return;
        }
        
        // Start position is the player's position (matching ChunkManager.firstChunkPosition logic)
        Vector3 startPosition = player.position;
        
        int childCount = allChunksParent.childCount;
        
        // Register undo for all chunks
        Undo.RecordObject(allChunksParent, "Align Chunks");
        for (int i = 0; i < childCount; i++)
        {
            Undo.RecordObject(allChunksParent.GetChild(i), "Align Chunks");
        }
        
        // Align each chunk exactly as ChunkManager does
        for (int chunkIndex = 0; chunkIndex < childCount; chunkIndex++)
        {
            Transform chunk = allChunksParent.GetChild(chunkIndex);
            
            // Calculate position along Z-axis (same as ChunkManager.LoadChunk)
            float zOffset = chunkIndex * (chunkLength + chunkGap);
            Vector3 position = startPosition + new Vector3(0, 0, zOffset);
            
            // Set position
            chunk.position = position;
            
            // Optionally reset rotation (ChunkManager uses prefab rotation)
            if (!preserveOriginalRotation)
            {
                chunk.rotation = Quaternion.identity;
            }
            
            // Mark dirty for save
            EditorUtility.SetDirty(chunk.gameObject);
        }
        
        Debug.Log($"[ChunkAlignmentTool] Aligned {childCount} chunks starting from {startPosition}");
        Debug.Log($"[ChunkAlignmentTool] Settings: ChunkLength={chunkLength}, Gap={chunkGap}");
        Debug.Log($"[ChunkAlignmentTool] Last chunk ends at Z={startPosition.z + (childCount - 1) * (chunkLength + chunkGap) + chunkLength}");
        
        // Focus scene view on result
        SceneView.RepaintAll();
    }
    
    private void MarkAllStatic()
    {
        if (allChunksParent == null)
        {
            Debug.LogError("[ChunkAlignmentTool] AllChunks Parent not assigned!");
            return;
        }
        
        int count = 0;
        
        // Get all GameObjects under AllChunks
        var allObjects = allChunksParent.GetComponentsInChildren<Transform>(true);
        
        foreach (var t in allObjects)
        {
            if (!t.gameObject.isStatic)
            {
                Undo.RecordObject(t.gameObject, "Mark Static");
                
                // Set static flags for lightmapping
                GameObjectUtility.SetStaticEditorFlags(
                    t.gameObject,
                    StaticEditorFlags.ContributeGI | 
                    StaticEditorFlags.OccluderStatic | 
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.BatchingStatic
                );
                
                count++;
            }
        }
        
        Debug.Log($"[ChunkAlignmentTool] Marked {count} objects as Static (ContributeGI enabled for baking)");
    }
}
