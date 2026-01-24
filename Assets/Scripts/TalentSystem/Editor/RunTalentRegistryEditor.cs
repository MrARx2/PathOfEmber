using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for RunTalentRegistry with a visible Clear button.
/// </summary>
[CustomEditor(typeof(RunTalentRegistry))]
public class RunTalentRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        RunTalentRegistry registry = (RunTalentRegistry)target;
        
        EditorGUILayout.Space(10);
        
        // Big red clear button
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Clear All Talents", GUILayout.Height(30)))
        {
            registry.Clear();
            EditorUtility.SetDirty(registry);
        }
        GUI.backgroundColor = Color.white;
    }
}
