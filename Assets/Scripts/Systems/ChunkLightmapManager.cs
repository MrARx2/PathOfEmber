using UnityEngine;
using System.Collections.Generic;

public class ChunkLightmapManager : MonoBehaviour
{
    public static ChunkLightmapManager Instance;

    [Header("Baked Lightmaps")]
    public Texture2D[] lightmapColors;
    public Texture2D[] lightmapDirs;
    public Texture2D[] shadowMasks;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        ApplyLightmaps();
    }

    public void ApplyLightmaps()
    {
        if (lightmapColors == null || lightmapColors.Length == 0) return;

        List<LightmapData> lightmaps = new List<LightmapData>();

        for (int i = 0; i < lightmapColors.Length; i++)
        {
            LightmapData data = new LightmapData();
            data.lightmapColor = lightmapColors[i];
            
            if (lightmapDirs != null && i < lightmapDirs.Length)
                data.lightmapDir = lightmapDirs[i];
            
            if (shadowMasks != null && i < shadowMasks.Length)
                data.shadowMask = shadowMasks[i];

            lightmaps.Add(data);
        }

        LightmapSettings.lightmaps = lightmaps.ToArray();
        
        // CRITICAL FORCE: Ensure the shader knows to render lightmaps even if the scene has none of its own
        Shader.EnableKeyword("LIGHTMAP_ON");
        Shader.EnableKeyword("DIRLIGHTMAP_COMBINED");
        
        // DEBUG TEST: Overwrite with WHITE texture to see if pipeline works
        // Remove this if you want the real bake!
        // lightmaps.Clear();
        // var whiteData = new LightmapData();
        // var whiteTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        // whiteTex.name = "DEBUG_WHITE_TEXTURE";
        // for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) whiteTex.SetPixel(x, y, Color.white * 10.0f); // VERY BRIGHT
        // whiteTex.Apply();
        // whiteData.lightmapColor = whiteTex;
        // lightmaps.Add(whiteData);
        
        Debug.Log($"[ChunkLightmapManager] Applied {lightmapColors.Length} lightmap(s).");
        for (int i = 0; i < lightmapColors.Length; i++)
        {
            var tex = lightmapColors[i];
            if (tex != null)
                Debug.Log($"[ChunkLightmapManager] Lightmap[{i}] Color: {tex.name} ({tex.width}x{tex.height}) Format: {tex.format}");
            // ...
            else
                Debug.LogError($"[ChunkLightmapManager] Lightmap[{i}] Color is NULL!");

            if (lightmapDirs != null && i < lightmapDirs.Length) 
            {
                var dir = lightmapDirs[i];
                if (dir != null) Debug.Log($"[ChunkLightmapManager] Lightmap[{i}] Dir: {dir.name} ({dir.width}x{dir.height})");
            }
        }
    }
}
