using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

public class PrefabLightmapBaker : EditorWindow
{
    [Header("References")]
    public Transform allChunksParent;
    public ChunkLightmapManager lightmapManagerPrefab;

    // Renamed menu item to force refresh
    [MenuItem("Tools/Path of Ember/Level/Bake Lightmaps to Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<PrefabLightmapBaker>("Lightmap Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Prefab Lightmap Baker", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use this AFTER baking lighting in your baking scene (where you aligned all chunks).", MessageType.Info);

        GUILayout.Label("1. Setup", EditorStyles.boldLabel);
        allChunksParent = (Transform)EditorGUILayout.ObjectField("All Chunks Parent", allChunksParent, typeof(Transform), true);
        lightmapManagerPrefab = (ChunkLightmapManager)EditorGUILayout.ObjectField("Lightmap Manager (In Scene)", lightmapManagerPrefab, typeof(ChunkLightmapManager), true);

        EditorGUILayout.Space(10);

        GUILayout.Label("2. Bake Preparation", EditorStyles.boldLabel);
        if (GUILayout.Button("Auto-Fix Static Flags & Scale", GUILayout.Height(30)))
        {
             FixStaticFlags();
        }
        EditorGUILayout.HelpBox("Click this, then 'Generate Lighting' in Unity, THEN Step 3.", MessageType.Warning);
        EditorGUILayout.Space(10);

        GUILayout.Label("3. Transfer Data", EditorStyles.boldLabel);
        if (GUILayout.Button("Transfer Lightmaps to Prefabs", GUILayout.Height(30)))
        {
            try
            {
                BakeToPrefabs();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PrefabLightmapBaker] Error during bake: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    private void FixStaticFlags()
    {
        if (allChunksParent == null) return;
        
        int count = 0;
        var renderers = allChunksParent.GetComponentsInChildren<MeshRenderer>(true);
        Undo.RecordObjects(renderers, "Fix Static Flags");

        foreach (var r in renderers)
        {
            // 1. Set Static Flags
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, 
                StaticEditorFlags.ContributeGI | 
                StaticEditorFlags.OccluderStatic | 
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic);

            // 2. Ensure Scale in Lightmap is valid
            SerializedObject so = new SerializedObject(r);
            SerializedProperty scaleProp = so.FindProperty("m_ScaleInLightmap");
            if (scaleProp != null && scaleProp.floatValue <= 0.01f)
            {
                scaleProp.floatValue = 1.0f;
            }
            
            // 3. Ensure ReceiveGI is Lightmaps
             SerializedProperty giProp = so.FindProperty("m_ReceiveGI");
             if (giProp != null) giProp.intValue = 1; // Lightmaps
             
             so.ApplyModifiedProperties();
             count++;
        }
        Debug.Log($"[PrefabLightmapBaker] Fixed flags and settings for {count} renderers. NOW CLICK 'GENERATE LIGHTING'!");
    }

    private void BakeToPrefabs()
    {
        if (allChunksParent == null)
        {
            EditorUtility.DisplayDialog("Error", "Assign 'All Chunks Parent' first!", "OK");
            return;
        }

        if (lightmapManagerPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Assign a 'ChunkLightmapManager' in the scene to store the texture references!", "OK");
            return;
        }

        int updatedCount = 0;

        // 1. Process each chunk in the scene
        for (int i = 0; i < allChunksParent.childCount; i++)
        {
            Transform chunkInstance = allChunksParent.GetChild(i);
            
            // Find the original prefab
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(chunkInstance.gameObject);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"Skipping {chunkInstance.name}: Not a prefab instance.");
                continue;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            
            // Load the prefab asset for editing
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            // Ensure it has the data component
            PrefabLightmapData dataComp = prefabRoot.GetComponent<PrefabLightmapData>();
            if (dataComp == null)
            {
                dataComp = prefabRoot.AddComponent<PrefabLightmapData>();
            }

            var sceneRenderers = chunkInstance.GetComponentsInChildren<Renderer>(true);
            List<PrefabLightmapData.RendererInfo> infoList = new List<PrefabLightmapData.RendererInfo>();
            
            // DEBUG: Log first chunk details
            bool isFirstChunk = (i == 0);
            if (isFirstChunk) Debug.Log($"[PrefabLightmapBaker] Debugging first chunk: {chunkInstance.name}. Renderers found: {sceneRenderers.Length}");

            foreach (var sceneR in sceneRenderers)
            {
                string path = GetTransformPath(sceneR.transform, chunkInstance);
                Transform prefabTransform = string.IsNullOrEmpty(path) ? prefabRoot.transform : prefabRoot.transform.Find(path);
                
                if (isFirstChunk)
                {
                    string status = "";
                    if (prefabTransform == null) status = "Prefab Transform NOT found";
                    else if (prefabTransform.GetComponent<Renderer>() == null) status = "Prefab Renderer NOT found";
                    else if (sceneR.lightmapIndex == -1) status = "Lightmap Index is -1 (Not Baked)";
                    else status = "OK";
                    
                    Debug.Log($"[PrefabLightmapBaker] Renderer: {sceneR.name}, Path: {path}, Index: {sceneR.lightmapIndex}, Status: {status}");
                }

                if (prefabTransform != null)
                {
                    Renderer matchingPrefabR = prefabTransform.GetComponent<Renderer>();
                    if (matchingPrefabR != null && sceneR.lightmapIndex != -1)
                    {
                        // FORCE LIGHTMAPS ON PREFAB RENDERER
                        SerializedObject rendererSO = new SerializedObject(matchingPrefabR);
                        SerializedProperty giProp = rendererSO.FindProperty("m_ReceiveGI");
                        if (giProp != null)
                        {
                           // 1 = Lightmaps, 2 = LightProbes
                           if (giProp.intValue != 1)
                           {
                               giProp.intValue = 1;
                               rendererSO.ApplyModifiedProperties();
                               // Debug.Log($"[PrefabLightmapBaker] Updated ReceiveGI to Lightmaps for {matchingPrefabR.name} in {prefabAsset.name}");
                           }
                        }
                        
                        PrefabLightmapData.RendererInfo info = new PrefabLightmapData.RendererInfo();
                        info.renderer = matchingPrefabR;
                        info.lightmapIndex = sceneR.lightmapIndex;
                        info.lightmapScaleOffset = sceneR.lightmapScaleOffset;
                        infoList.Add(info);
                    }
                }
            }

            if (infoList.Count == 0)
            {
                Debug.LogWarning($"[PrefabLightmapBaker] {chunkInstance.name}: Found 0 renderers with baked lightmaps! Did you forget to click 'Generate Lighting'? (Objects must be Static and Baked)");
            }
            else
            {
                Debug.Log($"[PrefabLightmapBaker] {chunkInstance.name}: Found {infoList.Count} baked renderers. Updating prefab...");
            }

            // Serialize this data into the prefab
            SerializedObject so = new SerializedObject(dataComp);
            SerializedProperty prop = so.FindProperty("m_RendererInfo");
            
            prop.ClearArray();
            prop.arraySize = infoList.Count;

            for (int k = 0; k < infoList.Count; k++)
            {
                SerializedProperty element = prop.GetArrayElementAtIndex(k);
                element.FindPropertyRelative("renderer").objectReferenceValue = infoList[k].renderer;
                element.FindPropertyRelative("lightmapIndex").intValue = infoList[k].lightmapIndex;
                element.FindPropertyRelative("lightmapScaleOffset").vector4Value = infoList[k].lightmapScaleOffset;
            }

            so.ApplyModifiedProperties();
            
            // Explicitly save the prefab asset
            PrefabUtility.SavePrefabAsset(prefabRoot);
            updatedCount++;
        }

        // 2. Save Lightmap Textures to Manager
        SerializedObject managerSO = new SerializedObject(lightmapManagerPrefab);
        
        // Colors
        SerializedProperty colorProp = managerSO.FindProperty("lightmapColors");
        colorProp.ClearArray();
        colorProp.arraySize = LightmapSettings.lightmaps.Length;
        for(int j=0; j<LightmapSettings.lightmaps.Length; j++)
        {
            if (LightmapSettings.lightmaps[j].lightmapColor != null)
                colorProp.GetArrayElementAtIndex(j).objectReferenceValue = LightmapSettings.lightmaps[j].lightmapColor;
        }

        // Dirs (if any)
        SerializedProperty dirProp = managerSO.FindProperty("lightmapDirs");
        dirProp.ClearArray();
        dirProp.arraySize = LightmapSettings.lightmaps.Length;
        for (int j = 0; j < LightmapSettings.lightmaps.Length; j++)
        {
             if (LightmapSettings.lightmaps[j].lightmapDir != null)
                dirProp.GetArrayElementAtIndex(j).objectReferenceValue = LightmapSettings.lightmaps[j].lightmapDir;
        }
        
        // Masks (if any)
        SerializedProperty maskProp = managerSO.FindProperty("shadowMasks");
        maskProp.ClearArray();
        maskProp.arraySize = LightmapSettings.lightmaps.Length;
        for (int j = 0; j < LightmapSettings.lightmaps.Length; j++)
        {
             if (LightmapSettings.lightmaps[j].shadowMask != null)
                maskProp.GetArrayElementAtIndex(j).objectReferenceValue = LightmapSettings.lightmaps[j].shadowMask;
        }

        managerSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(lightmapManagerPrefab);

        AssetDatabase.SaveAssets();
        Debug.Log($"[PrefabLightmapBaker] SUCCESS: Updated {updatedCount} prefabs and saved lightmaps to Manager.");
    }

    private string GetTransformPath(Transform t, Transform root)
    {
        if (t == root) return "";
        string path = t.name;
        while (t.parent != null && t.parent != root)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private Renderer FindRendererByPath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path)) return root.GetComponent<Renderer>();
        Transform result = root.Find(path);
        if (result != null) return result.GetComponent<Renderer>();
        return null;
    }
}
