using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Editor tool to quickly toggle Cast Shadows on static objects.
/// Useful for baked lighting optimization.
/// </summary>
public class ShadowCastingToggle : EditorWindow
{
    [MenuItem("Tools/Path of Ember/Level/Shadow Casting Toggle")]
    public static void ShowWindow()
    {
        GetWindow<ShadowCastingToggle>("Shadow Toggle");
    }

    private bool includeChildren = true;
    private bool onlyStaticObjects = true;

    private void OnGUI()
    {
        GUILayout.Label("🌑 Shadow Casting Toggle", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Options
        includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);
        onlyStaticObjects = EditorGUILayout.Toggle("Only Static Objects", onlyStaticObjects);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Selected Objects", EditorStyles.boldLabel);

        // Selected objects controls
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Disable Shadows", GUILayout.Height(30)))
        {
            SetShadowsOnSelected(ShadowCastingMode.Off);
        }
        if (GUILayout.Button("Enable Shadows", GUILayout.Height(30)))
        {
            SetShadowsOnSelected(ShadowCastingMode.On);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("All Chunks in Scene", EditorStyles.boldLabel);

        // Scene-wide controls
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("Disable ALL Chunk Shadows", GUILayout.Height(35)))
        {
            SetShadowsOnAllChunks(ShadowCastingMode.Off);
        }
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Enable ALL Chunk Shadows", GUILayout.Height(35)))
        {
            SetShadowsOnAllChunks(ShadowCastingMode.On);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Prefab Operations", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Disable on Selected Prefabs", GUILayout.Height(30)))
        {
            SetShadowsOnSelectedPrefabs(ShadowCastingMode.Off);
        }
        if (GUILayout.Button("Enable on Selected Prefabs", GUILayout.Height(30)))
        {
            SetShadowsOnSelectedPrefabs(ShadowCastingMode.On);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Tip: Disable shadows on static objects after baking. " +
            "Their shadows are already in the lightmap textures.",
            MessageType.Info);
    }

    private void SetShadowsOnSelected(ShadowCastingMode mode)
    {
        int count = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            count += SetShadowsRecursive(go, mode);
        }
        Debug.Log($"[ShadowToggle] Set {mode} on {count} renderers.");
    }

    private int SetShadowsRecursive(GameObject go, ShadowCastingMode mode)
    {
        int count = 0;

        Renderer[] renderers = includeChildren 
            ? go.GetComponentsInChildren<Renderer>() 
            : go.GetComponents<Renderer>();

        foreach (var r in renderers)
        {
            if (onlyStaticObjects && !go.isStatic) continue;
            
            Undo.RecordObject(r, "Toggle Shadow Casting");
            r.shadowCastingMode = mode;
            EditorUtility.SetDirty(r);
            count++;
        }

        return count;
    }

    private void SetShadowsOnAllChunks(ShadowCastingMode mode)
    {
        // Find AllChunks parent
        GameObject allChunks = GameObject.Find("AllChunks");
        if (allChunks == null)
        {
            Debug.LogError("[ShadowToggle] Could not find 'AllChunks' in scene.");
            return;
        }

        int count = 0;
        Renderer[] renderers = allChunks.GetComponentsInChildren<Renderer>();
        
        foreach (var r in renderers)
        {
            if (onlyStaticObjects && !r.gameObject.isStatic) continue;
            
            Undo.RecordObject(r, "Toggle Shadow Casting");
            r.shadowCastingMode = mode;
            EditorUtility.SetDirty(r);
            count++;
        }

        Debug.Log($"[ShadowToggle] Set {mode} on {count} renderers in AllChunks.");
    }

    private void SetShadowsOnSelectedPrefabs(ShadowCastingMode mode)
    {
        int prefabCount = 0;
        int rendererCount = 0;

        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                Undo.RecordObject(r, "Toggle Shadow Casting on Prefab");
                r.shadowCastingMode = mode;
                EditorUtility.SetDirty(r);
                rendererCount++;
            }

            PrefabUtility.SavePrefabAsset(prefab);
            prefabCount++;
        }

        Debug.Log($"[ShadowToggle] Updated {rendererCount} renderers in {prefabCount} prefabs.");
        AssetDatabase.SaveAssets();
    }
}
