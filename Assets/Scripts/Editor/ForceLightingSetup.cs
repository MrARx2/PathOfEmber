using UnityEngine;
using UnityEditor;

public class ForceLightingSetup : EditorWindow
{
    [MenuItem("Tools/Path of Ember/Level/Force Lighting Consistency")]
    public static void ForceSettings()
    {
        // Find ALL MeshRenderers, including inactive ones (just in case)
        MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[ForceLighting] Found {allRenderers.Length} total MeshRenderers in scene.");

        int count = 0;

        foreach (var mr in allRenderers)
        {
            // Only affect objects that look like ground/buildings (e.g. have "Chunk" or "Environment" properties, or just everything static)
            // For safety, let's target everything marked as static or intended to be static.
            
            // We'll focus on objects that SHOULD be lightmapped.
            // If it's a MeshRenderer and it's static-ish...
            
            // Unpack prefab if needed (usually not needed for just properties)
            Undo.RecordObject(mr.gameObject, "Force Static Flags");
            GameObjectUtility.SetStaticEditorFlags(mr.gameObject, 
                StaticEditorFlags.ContributeGI | 
                StaticEditorFlags.ReflectionProbeStatic | 
                StaticEditorFlags.OccluderStatic | 
                StaticEditorFlags.BatchingStatic);

            Undo.RecordObject(mr, "Force Lightmap Settings");
            mr.receiveGI = ReceiveGI.Lightmaps;
            mr.scaleInLightmap = 1.0f; // Force uniform scale
            
            // Fix: Ensure shadows are actually enabled!
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            
            EditorUtility.SetDirty(mr);
            EditorUtility.SetDirty(mr.gameObject);
            count++;
        }

        Debug.Log($"✅ Forced Lightmap Settings (Static=On, CastShadows=On, Receive=On, Scale=1) on {count} MeshRenderers.");
    }
}
