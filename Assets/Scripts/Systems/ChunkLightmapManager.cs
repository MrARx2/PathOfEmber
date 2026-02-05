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
        
        // Ensure the shader knows to render lightmaps
        Shader.EnableKeyword("LIGHTMAP_ON");
        Shader.EnableKeyword("DIRLIGHTMAP_COMBINED");
    }
}
