using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Editor tool to configure DecalProjector components for consistent terrain projection.
/// Menu: Tools > Decal Projector Tool
/// </summary>
public class DecalProjectorOffsetTool : EditorWindow
{
    // Position offset settings
    private Vector3 offset = new Vector3(0f, 0.5f, 0f);
    private bool applyOffset = true;
    
    // Projection settings for terrain consistency
    private bool applyProjectionSettings = true;
    private float projectionDepth = 8f;           // How far down the decal projects
    private float drawDistance = 100f;            // Max distance to render decal
    private float fadeFactor = 0.5f;              // Fade at edges (0-1)
    private float startAngleFade = 90f;           // Start fading at this angle
    private float endAngleFade = 180f;            // Fully faded at this angle
    private DecalScaleMode scaleMode = DecalScaleMode.ScaleInvariant;
    
    // Prefab settings
    private bool affectPrefabsInProject = false;
    private string prefabSearchFolder = "Assets/vfx-project";
    
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/Decal Projector Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<DecalProjectorOffsetTool>("Decal Projector Tool");
        window.minSize = new Vector2(350, 500);
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        GUILayout.Label("Decal Projector Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        EditorGUILayout.HelpBox(
            "Configure DecalProjectors for consistent projection on uneven terrain.", 
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // ═══════════════════════════════════════════════════════════════
        // POSITION OFFSET SECTION
        // ═══════════════════════════════════════════════════════════════
        GUILayout.Label("Position Offset", EditorStyles.boldLabel);
        applyOffset = EditorGUILayout.Toggle("Apply Offset", applyOffset);
        
        EditorGUI.BeginDisabledGroup(!applyOffset);
        offset = EditorGUILayout.Vector3Field("Offset", offset);
        EditorGUILayout.HelpBox(
            "Raises decal projectors above ground. Y=0.5 is a good starting point.", 
            MessageType.None);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(15);
        
        // ═══════════════════════════════════════════════════════════════
        // PROJECTION SETTINGS SECTION
        // ═══════════════════════════════════════════════════════════════
        GUILayout.Label("Projection Settings", EditorStyles.boldLabel);
        applyProjectionSettings = EditorGUILayout.Toggle("Apply Projection Settings", applyProjectionSettings);
        
        EditorGUI.BeginDisabledGroup(!applyProjectionSettings);
        
        projectionDepth = EditorGUILayout.FloatField(
            new GUIContent("Projection Depth", "How far the decal projects downward (Size.Z). Larger = works on more terrain heights."), 
            projectionDepth);
        
        drawDistance = EditorGUILayout.FloatField(
            new GUIContent("Draw Distance", "Maximum camera distance to render the decal."), 
            drawDistance);
        
        fadeFactor = EditorGUILayout.Slider(
            new GUIContent("Fade Factor", "Controls edge fading. 0 = no fade, 1 = full fade."), 
            fadeFactor, 0f, 1f);
        
        EditorGUILayout.Space(5);
        GUILayout.Label("Angle Fade (prevents stretching on steep surfaces)", EditorStyles.miniLabel);
        
        startAngleFade = EditorGUILayout.Slider(
            new GUIContent("Start Angle Fade", "Decal starts fading at this angle from surface normal."), 
            startAngleFade, 0f, 180f);
        
        endAngleFade = EditorGUILayout.Slider(
            new GUIContent("End Angle Fade", "Decal fully invisible at this angle."), 
            endAngleFade, 0f, 180f);
        
        scaleMode = (DecalScaleMode)EditorGUILayout.EnumPopup(
            new GUIContent("Scale Mode", "How the decal scales with object scale."), 
            scaleMode);
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Recommended for terrain:\n" +
            "• Depth: 5-10 (covers terrain height variation)\n" +
            "• Fade Factor: 0.5 (smooth edges)\n" +
            "• Angle Fade: 90-180 (hides on walls)", 
            MessageType.None);
        
        EditorGUILayout.Space(15);
        
        // ═══════════════════════════════════════════════════════════════
        // SCENE ACTIONS
        // ═══════════════════════════════════════════════════════════════
        GUILayout.Label("Apply to Scene", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Apply to All DecalProjectors in Scene"))
        {
            ApplyToScene();
        }
        
        if (GUILayout.Button("Apply to Selected Objects"))
        {
            ApplyToSelected();
        }
        
        EditorGUILayout.Space(15);
        
        // ═══════════════════════════════════════════════════════════════
        // PREFAB ACTIONS
        // ═══════════════════════════════════════════════════════════════
        GUILayout.Label("Apply to Prefabs", EditorStyles.boldLabel);
        
        affectPrefabsInProject = EditorGUILayout.Toggle("Enable Prefab Modification", affectPrefabsInProject);
        
        if (affectPrefabsInProject)
        {
            prefabSearchFolder = EditorGUILayout.TextField("Search Folder", prefabSearchFolder);
            
            EditorGUILayout.HelpBox(
                "WARNING: This permanently modifies prefab assets.", 
                MessageType.Warning);
            
            if (GUILayout.Button("Apply to All Prefabs with DecalProjector"))
            {
                if (EditorUtility.DisplayDialog("Confirm Prefab Modification",
                    $"This will modify all prefabs containing DecalProjector in:\n{prefabSearchFolder}\n\nContinue?",
                    "Apply", "Cancel"))
                {
                    ApplyToPrefabs();
                }
            }
        }
        
        EditorGUILayout.Space(15);
        
        // ═══════════════════════════════════════════════════════════════
        // UTILITIES
        // ═══════════════════════════════════════════════════════════════
        GUILayout.Label("Utilities", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Find in Scene"))
        {
            FindAllInScene();
        }
        if (GUILayout.Button("Find in Prefabs"))
        {
            FindAllPrefabs();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndScrollView();
    }
    
    private void ApplySettingsToProjector(DecalProjector projector, bool isUndo = true)
    {
        if (isUndo)
        {
            Undo.RecordObject(projector, "Configure DecalProjector");
            Undo.RecordObject(projector.transform, "Configure DecalProjector Transform");
        }
        
        // Apply position offset
        if (applyOffset)
        {
            projector.transform.localPosition += offset;
        }
        
        // Apply projection settings
        if (applyProjectionSettings)
        {
            // Update depth (Size.Z)
            Vector3 size = projector.size;
            size.z = projectionDepth;
            projector.size = size;
            
            // Pivot should be at top so projection goes downward
            projector.pivot = new Vector3(0, 0, 1); // Pivot at top of projection volume
            
            projector.drawDistance = drawDistance;
            projector.fadeFactor = fadeFactor;
            projector.startAngleFade = startAngleFade;
            projector.endAngleFade = endAngleFade;
            projector.scaleMode = scaleMode;
        }
        
        EditorUtility.SetDirty(projector);
        EditorUtility.SetDirty(projector.transform);
    }
    
    private void ApplyToScene()
    {
        DecalProjector[] projectors = FindObjectsByType<DecalProjector>(FindObjectsSortMode.None);
        
        if (projectors.Length == 0)
        {
            EditorUtility.DisplayDialog("No Projectors Found", 
                "No DecalProjector components found in the current scene.", "OK");
            return;
        }
        
        int count = 0;
        foreach (var projector in projectors)
        {
            ApplySettingsToProjector(projector);
            count++;
        }
        
        Debug.Log($"[DecalProjectorTool] Applied settings to {count} DecalProjector(s) in scene.");
    }
    
    private void ApplyToSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", 
                "Please select GameObjects containing DecalProjector components.", "OK");
            return;
        }
        
        int count = 0;
        foreach (var go in selected)
        {
            DecalProjector[] projectors = go.GetComponentsInChildren<DecalProjector>(true);
            foreach (var projector in projectors)
            {
                ApplySettingsToProjector(projector);
                count++;
            }
        }
        
        if (count == 0)
        {
            EditorUtility.DisplayDialog("No Projectors Found", 
                "No DecalProjector components found in the selected objects.", "OK");
            return;
        }
        
        Debug.Log($"[DecalProjectorTool] Applied settings to {count} DecalProjector(s) in selection.");
    }
    
    private void ApplyToPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabSearchFolder });
        
        int modifiedCount = 0;
        int prefabCount = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null) continue;
            
            DecalProjector[] projectors = prefab.GetComponentsInChildren<DecalProjector>(true);
            
            if (projectors.Length == 0) continue;
            
            // Open prefab for editing
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            
            DecalProjector[] prefabProjectors = prefabRoot.GetComponentsInChildren<DecalProjector>(true);
            
            foreach (var projector in prefabProjectors)
            {
                ApplySettingsToProjector(projector, isUndo: false);
                modifiedCount++;
            }
            
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            prefabCount++;
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"[DecalProjectorTool] Modified {modifiedCount} DecalProjector(s) across {prefabCount} prefab(s).");
        EditorUtility.DisplayDialog("Complete", 
            $"Applied settings to {modifiedCount} DecalProjector(s) across {prefabCount} prefab(s).", "OK");
    }
    
    private void FindAllInScene()
    {
        DecalProjector[] projectors = FindObjectsByType<DecalProjector>(FindObjectsSortMode.None);
        
        Debug.Log($"[DecalProjectorTool] Found {projectors.Length} DecalProjector(s) in scene:");
        foreach (var projector in projectors)
        {
            Debug.Log($"  - {GetPath(projector.gameObject)} | Depth: {projector.size.z} | DrawDist: {projector.drawDistance}");
        }
    }
    
    private void FindAllPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabSearchFolder });
        
        int count = 0;
        Debug.Log($"[DecalProjectorTool] Searching in: {prefabSearchFolder}");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null) continue;
            
            DecalProjector[] projectors = prefab.GetComponentsInChildren<DecalProjector>(true);
            
            if (projectors.Length > 0)
            {
                Debug.Log($"  - {path} ({projectors.Length} projector(s))");
                count += projectors.Length;
            }
        }
        
        Debug.Log($"[DecalProjectorTool] Total: {count} DecalProjector(s) found.");
    }
    
    private string GetPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
