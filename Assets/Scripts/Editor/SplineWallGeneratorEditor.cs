using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for SplineWallGenerator with generation buttons and validation.
/// </summary>
[CustomEditor(typeof(SplineWallGenerator))]
public class SplineWallGeneratorEditor : Editor
{
    private SerializedProperty wallHeightProp;
    private SerializedProperty wallThicknessProp;
    private SerializedProperty segmentsProp;
    private SerializedProperty colliderTagProp;
    private SerializedProperty colliderLayerProp;
    private SerializedProperty isTriggerProp;
    private SerializedProperty generateMeshProp;
    private SerializedProperty wallMaterialProp;
    private SerializedProperty autoRegenerateProp;
    
    private void OnEnable()
    {
        wallHeightProp = serializedObject.FindProperty("wallHeight");
        wallThicknessProp = serializedObject.FindProperty("wallThickness");
        segmentsProp = serializedObject.FindProperty("segments");
        colliderTagProp = serializedObject.FindProperty("colliderTag");
        colliderLayerProp = serializedObject.FindProperty("colliderLayer");
        isTriggerProp = serializedObject.FindProperty("isTrigger");
        generateMeshProp = serializedObject.FindProperty("generateMesh");
        wallMaterialProp = serializedObject.FindProperty("wallMaterial");
        autoRegenerateProp = serializedObject.FindProperty("autoRegenerate");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        SplineWallGenerator generator = (SplineWallGenerator)target;
        
        // Title
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spline Wall Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Wall Dimensions
        EditorGUILayout.LabelField("Wall Dimensions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(wallHeightProp, new GUIContent("Height", "Height of the wall"));
        EditorGUILayout.PropertyField(wallThicknessProp, new GUIContent("Thickness", "Thickness of the wall"));
        EditorGUILayout.PropertyField(segmentsProp, new GUIContent("Segments", "Number of segments (higher = smoother)"));
        
        EditorGUILayout.Space();
        
        // Collider Settings
        EditorGUILayout.LabelField("Collider Settings", EditorStyles.boldLabel);
        
        // Tag dropdown
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent("Collider Tag", "Tag to assign to the collider object"));
        
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        int currentTagIndex = System.Array.IndexOf(tags, colliderTagProp.stringValue);
        if (currentTagIndex < 0) currentTagIndex = 0;
        
        int newTagIndex = EditorGUILayout.Popup(currentTagIndex, tags);
        if (newTagIndex != currentTagIndex || colliderTagProp.stringValue != tags[newTagIndex])
        {
            colliderTagProp.stringValue = tags[newTagIndex];
        }
        EditorGUILayout.EndHorizontal();
        
        // Layer dropdown
        colliderLayerProp.intValue = EditorGUILayout.LayerField(
            new GUIContent("Collider Layer", "Layer for the collider object"),
            colliderLayerProp.intValue);
        
        EditorGUILayout.PropertyField(isTriggerProp, new GUIContent("Is Trigger", "Make the collider a trigger"));
        
        if (isTriggerProp.boolValue)
        {
            EditorGUILayout.HelpBox("Trigger colliders require convex mesh colliders. Complex curved walls may not work correctly as triggers.", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        
        // Mesh Settings
        EditorGUILayout.LabelField("Mesh Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(generateMeshProp, new GUIContent("Generate Mesh", "Generate a visual mesh (disable for invisible walls)"));
        
        if (generateMeshProp.boolValue)
        {
            EditorGUILayout.PropertyField(wallMaterialProp, new GUIContent("Material", "Material for the wall mesh"));
            
            if (wallMaterialProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("No material assigned. A default gray material will be used.", MessageType.Info);
            }
        }
        
        EditorGUILayout.Space();
        
        // Generation Settings
        EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(autoRegenerateProp, new GUIContent("Auto Regenerate", "Automatically regenerate when spline changes"));
        
        EditorGUILayout.Space();
        
        // Buttons
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Generate Wall", GUILayout.Height(30)))
        {
            generator.GenerateWall();
            EditorUtility.SetDirty(generator);
        }
        
        GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
        if (GUILayout.Button("Clear Wall", GUILayout.Height(30)))
        {
            generator.ClearWall();
            EditorUtility.SetDirty(generator);
        }
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Validate tag exists
        if (!TagExists(colliderTagProp.stringValue))
        {
            EditorGUILayout.HelpBox($"Tag '{colliderTagProp.stringValue}' does not exist! Create it in Edit > Project Settings > Tags and Layers.", MessageType.Warning);
            
            if (GUILayout.Button("Open Tags and Layers"))
            {
                SettingsService.OpenProjectSettings("Project/Tags and Layers");
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private bool TagExists(string tag)
    {
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        return System.Array.Exists(tags, t => t == tag);
    }
}
