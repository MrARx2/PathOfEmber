using UnityEngine;

public class RuntimeLightmapInspector : MonoBehaviour
{
    private int lastIndex = -99;

    [ContextMenu("Inspect Renderer")]
    public void Inspect()
    {
        var r = GetComponent<Renderer>();
        if (r == null)
        {
            // Debug.LogError("No Renderer found on " + name); 
            // Silence this error for root objects to avoid spam, unless explicit inspect
            return;
        }

        // Only log if something changed or it's the first time
        if (r.lightmapIndex != lastIndex)
        {
            lastIndex = r.lightmapIndex;
            LogState(r);
        }
    }

    void Start() { InvokeRepeating(nameof(Inspect), 0.1f, 1.0f); } // Check every second automatically

    void LogState(Renderer r)
    {
        Debug.Log($"--- INSPECTING {name} ---");
        Debug.Log($"Renderer Enabled: {r.enabled}");
        Debug.Log($"Lightmap Index: {r.lightmapIndex}");
        Debug.Log($"Lightmap ScaleOffset: {r.lightmapScaleOffset}");
        Debug.Log($"Is Static: {r.gameObject.isStatic}");
        
        #if UNITY_EDITOR
        if (r is MeshRenderer mr)
        {
            Debug.Log($"Receive GI: {mr.receiveGI}");
        }
        else
        {
            Debug.Log("Receive GI: N/A (Not MeshRenderer)");
        }
        #endif
        
        // Check Global Settings
        var maps = LightmapSettings.lightmaps;
        Debug.Log($"Global Lightmap Count: {maps.Length}");
        if (r.lightmapIndex >= 0 && r.lightmapIndex < maps.Length)
        {
            var map = maps[r.lightmapIndex];
            Debug.Log($"Lightmap [{r.lightmapIndex}] Color: {(map.lightmapColor != null ? map.lightmapColor.name : "NULL")}");
            Debug.Log($"Lightmap [{r.lightmapIndex}] Dir: {(map.lightmapDir != null ? map.lightmapDir.name : "NULL")}");
        }
        else if (r.lightmapIndex != -1)
        {
            Debug.LogError("❌ Lightmap Index is OUT OF BOUNDS of global settings!");
        }

        // Check Keywords
        Debug.Log($"Global LIGHTMAP_ON: {Shader.IsKeywordEnabled("LIGHTMAP_ON")}");
        Debug.Log($"Global DIRLIGHTMAP_COMBINED: {Shader.IsKeywordEnabled("DIRLIGHTMAP_COMBINED")}");
        
        if (r.sharedMaterial != null)
        {
            Debug.Log($"Material: {r.sharedMaterial.name}");
            Debug.Log($"Mat LIGHTMAP_ON: {r.sharedMaterial.IsKeywordEnabled("LIGHTMAP_ON")}");
            Debug.Log($"Mat DIRLIGHTMAP_COMBINED: {r.sharedMaterial.IsKeywordEnabled("DIRLIGHTMAP_COMBINED")}");
        }
        
        Debug.Log("---------------------------");
    }
}
