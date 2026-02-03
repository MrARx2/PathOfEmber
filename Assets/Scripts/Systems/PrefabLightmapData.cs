using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PrefabLightmapData : MonoBehaviour
{
    [System.Serializable]
    public struct RendererInfo
    {
        public Renderer renderer;
        public int lightmapIndex;
        public Vector4 lightmapScaleOffset;
    }

    [SerializeField]
    private RendererInfo[] m_RendererInfo;

    void OnEnable()
    {
        ApplyLightmaps();
    }

    [ContextMenu("Force Apply Lightmaps")]
    public void ApplyLightmaps()
    {
        if (m_RendererInfo == null || m_RendererInfo.Length == 0) 
        {
             // Silent return for empty data, or log warning
             return;
        }

        int count = 0;
        foreach (var info in m_RendererInfo)
        {
            if (info.renderer != null)
            {
                #if UNITY_EDITOR
                if (info.renderer is MeshRenderer meshRenderer)
                {
                    meshRenderer.receiveGI = ReceiveGI.Lightmaps;
                }
                #endif
                
                // FORCE Apply directly. 
                // Note: Instantiated prefabs are NOT part of static batch at runtime unless you call StaticBatchingUtility.Combine.
                // So we can safely set these.
                info.renderer.lightmapIndex = info.lightmapIndex;
                info.renderer.lightmapScaleOffset = info.lightmapScaleOffset;
            
                // CRITICAL FAILSAFE: Use MaterialPropertyBlock to force the ST (Scale/Translate)
                // This fixes cases where Unity ignores the renderer property
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                info.renderer.GetPropertyBlock(block);
                block.SetVector("unity_LightmapST", info.lightmapScaleOffset);
                info.renderer.SetPropertyBlock(block);

                // FINAL FORCE: Enable the keywords on the material itself
                // This is needed if the material was loaded in a scene without lightmaps
                if (info.renderer.sharedMaterial != null)
                {
                    if (!info.renderer.sharedMaterial.IsKeywordEnabled("LIGHTMAP_ON"))
                        info.renderer.material.EnableKeyword("LIGHTMAP_ON");
                        
                    // Re-enable Directional if using Directional mode
                    if (!info.renderer.sharedMaterial.IsKeywordEnabled("DIRLIGHTMAP_COMBINED"))
                        info.renderer.material.EnableKeyword("DIRLIGHTMAP_COMBINED");
                }

                count++;
            }
        }
        // Debug.Log($"[PrefabLightmapData] {name}: Applied lightmaps to {count} renderers.");
    }

#if UNITY_EDITOR
    public void SaveLightmapData()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        var infoList = new List<RendererInfo>();

        foreach (var r in renderers)
        {
            if (r.lightmapIndex != -1)
            {
                RendererInfo info = new RendererInfo();
                info.renderer = r;
                info.lightmapIndex = r.lightmapIndex;
                info.lightmapScaleOffset = r.lightmapScaleOffset;
                infoList.Add(info);
            }
        }

        m_RendererInfo = infoList.ToArray();
    }
#endif
}
