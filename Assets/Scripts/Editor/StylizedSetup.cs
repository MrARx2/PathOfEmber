using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Stylized look setup tool with live sliders for tuning.
/// All-in-one: shadows, lighting, and post-processing.
/// </summary>
public class StylizedSetup : EditorWindow
{
    [MenuItem("Tools/Path of Ember/Visuals/Setup Stylized Look")]
    public static void ShowWindow()
    {
        var window = GetWindow<StylizedSetup>("Stylized Setup");
        window.minSize = new Vector2(350, 500);
    }

    // References
    private VolumeProfile volumeProfile;
    private UniversalRenderPipelineAsset urpAsset;
    private Light mainLight;

    // === SHADOW SETTINGS ===
    [Header("Shadows")]
    private float shadowStrength = 0.5f;
    private float shadowDistance = 50f;
    private int shadowResolution = 2048;
    
    // === LIGHTING SETTINGS ===
    [Header("Lighting")]
    private float lightIntensity = 1.5f;
    private float ambientBrightness = 0.5f;
    
    // === POST-PROCESS SETTINGS ===
    [Header("Colors")]
    private float brightness = 0.2f;
    private float contrast = 10f;
    private float saturation = 15f;
    private float bloomIntensity = 0.3f;
    private float vignetteIntensity = 0.15f;

    private Vector2 scrollPos;

    private void OnEnable()
    {
        // Auto-find assets
        urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                mainLight = light;
                break;
            }
        }
        
        // Try to find volume profile
        string[] guids = AssetDatabase.FindAssets("GlobalPostProcess t:VolumeProfile");
        if (guids.Length > 0)
            volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
        
        // Load current values
        LoadCurrentValues();
    }

    private void LoadCurrentValues()
    {
        if (mainLight != null)
        {
            shadowStrength = mainLight.shadowStrength;
            lightIntensity = mainLight.intensity;
        }
        
        ambientBrightness = RenderSettings.ambientLight.r;
        
        if (volumeProfile != null)
        {
            if (volumeProfile.TryGet<ColorAdjustments>(out var ca))
            {
                brightness = ca.postExposure.value;
                contrast = ca.contrast.value;
                saturation = ca.saturation.value;
            }
            if (volumeProfile.TryGet<Bloom>(out var bloom))
                bloomIntensity = bloom.intensity.value;
            if (volumeProfile.TryGet<Vignette>(out var vig))
                vignetteIntensity = vig.intensity.value;
        }
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        GUILayout.Label("🎨 Stylized Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // === REFERENCES ===
        EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
        volumeProfile = (VolumeProfile)EditorGUILayout.ObjectField("Volume Profile", volumeProfile, typeof(VolumeProfile), false);
        urpAsset = (UniversalRenderPipelineAsset)EditorGUILayout.ObjectField("URP Asset", urpAsset, typeof(UniversalRenderPipelineAsset), false);
        mainLight = (Light)EditorGUILayout.ObjectField("Directional Light", mainLight, typeof(Light), true);
        
        EditorGUILayout.Space(10);
        DrawSeparator();
        
        // === SHADOW CONTROLS ===
        EditorGUILayout.LabelField("🌑 Shadow Settings", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        shadowStrength = EditorGUILayout.Slider("Shadow Softness", shadowStrength, 0f, 1f);
        EditorGUILayout.HelpBox("Lower = softer shadows. 0.5 recommended for stylized.", MessageType.None);
        
        shadowDistance = EditorGUILayout.Slider("Shadow Distance", shadowDistance, 10f, 100f);
        shadowResolution = EditorGUILayout.IntPopup("Shadow Resolution", shadowResolution, 
            new string[] { "512", "1024", "2048", "4096" }, 
            new int[] { 512, 1024, 2048, 4096 });
        
        if (EditorGUI.EndChangeCheck())
            ApplyShadowSettings();
        
        EditorGUILayout.Space(10);
        DrawSeparator();
        
        // === LIGHTING CONTROLS ===
        EditorGUILayout.LabelField("☀️ Lighting", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        lightIntensity = EditorGUILayout.Slider("Light Intensity", lightIntensity, 0.5f, 3f);
        ambientBrightness = EditorGUILayout.Slider("Ambient Fill", ambientBrightness, 0f, 1f);
        EditorGUILayout.HelpBox("Higher ambient = softer look, less harsh shadows.", MessageType.None);
        
        if (EditorGUI.EndChangeCheck())
            ApplyLightingSettings();
        
        EditorGUILayout.Space(10);
        DrawSeparator();
        
        // === COLOR CONTROLS ===
        EditorGUILayout.LabelField("🌈 Colors & Effects", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        brightness = EditorGUILayout.Slider("Brightness", brightness, -1f, 1f);
        contrast = EditorGUILayout.Slider("Contrast", contrast, -50f, 50f);
        saturation = EditorGUILayout.Slider("Saturation", saturation, -50f, 50f);
        
        EditorGUILayout.Space(5);
        bloomIntensity = EditorGUILayout.Slider("Bloom", bloomIntensity, 0f, 1f);
        vignetteIntensity = EditorGUILayout.Slider("Vignette", vignetteIntensity, 0f, 0.5f);
        
        if (EditorGUI.EndChangeCheck())
            ApplyColorSettings();
        
        EditorGUILayout.Space(15);
        DrawSeparator();
        
        // === PRESET BUTTONS ===
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Soft & Bright"))
        {
            ApplyPreset(0.4f, 60f, 1.6f, 0.6f, 0.3f, 12f, 20f, 0.4f, 0.12f);
        }
        if (GUILayout.Button("Balanced"))
        {
            ApplyPreset(0.5f, 50f, 1.5f, 0.5f, 0.2f, 10f, 15f, 0.3f, 0.15f);
        }
        if (GUILayout.Button("Punchy"))
        {
            ApplyPreset(0.7f, 50f, 1.4f, 0.4f, 0.15f, 20f, 25f, 0.5f, 0.2f);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // === ACTION BUTTONS ===
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("✓ Apply All Settings", GUILayout.Height(35)))
        {
            ApplyAllSettings();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reload Current"))
        {
            LoadCurrentValues();
        }
        if (GUILayout.Button("Reset to Defaults"))
        {
            ResetToDefaults();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(3);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        EditorGUILayout.Space(3);
    }

    private void ApplyPreset(float shadow, float shadowDist, float lightInt, float ambient, 
        float bright, float cont, float sat, float bloom, float vig)
    {
        shadowStrength = shadow;
        shadowDistance = shadowDist;
        lightIntensity = lightInt;
        ambientBrightness = ambient;
        brightness = bright;
        contrast = cont;
        saturation = sat;
        bloomIntensity = bloom;
        vignetteIntensity = vig;
        
        ApplyAllSettings();
        Repaint();
    }

    private void ApplyShadowSettings()
    {
        if (mainLight != null)
        {
            mainLight.shadowStrength = shadowStrength;
            EditorUtility.SetDirty(mainLight);
        }
        
        if (urpAsset != null)
        {
            SerializedObject so = new SerializedObject(urpAsset);
            
            var softShadows = so.FindProperty("m_SoftShadowsSupported");
            if (softShadows != null) softShadows.boolValue = true;
            
            var shadowRes = so.FindProperty("m_MainLightShadowmapResolution");
            if (shadowRes != null) shadowRes.intValue = shadowResolution;
            
            var shadowDist = so.FindProperty("m_ShadowDistance");
            if (shadowDist != null) shadowDist.floatValue = shadowDistance;
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urpAsset);
        }
    }

    private void ApplyLightingSettings()
    {
        if (mainLight != null)
        {
            mainLight.color = Color.white;
            mainLight.intensity = lightIntensity;
            mainLight.shadowBias = 0.05f;
            mainLight.shadowNormalBias = 0.4f;
            EditorUtility.SetDirty(mainLight);
        }
        
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(ambientBrightness, ambientBrightness, ambientBrightness);
        RenderSettings.ambientIntensity = 1.0f;
    }

    private void ApplyColorSettings()
    {
        if (volumeProfile == null) return;
        
        // Disable LiftGammaGain (prevents brown tint)
        if (volumeProfile.TryGet<LiftGammaGain>(out var lgg))
            lgg.active = false;
        
        // Color Adjustments
        if (!volumeProfile.TryGet<ColorAdjustments>(out var ca))
            ca = volumeProfile.Add<ColorAdjustments>(true);
        
        ca.active = true;
        ca.postExposure.overrideState = true;
        ca.postExposure.value = brightness;
        ca.contrast.overrideState = true;
        ca.contrast.value = contrast;
        ca.saturation.overrideState = true;
        ca.saturation.value = saturation;
        
        // Bloom
        if (!volumeProfile.TryGet<Bloom>(out var bloom))
            bloom = volumeProfile.Add<Bloom>(true);
        
        bloom.active = true;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = bloomIntensity;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.9f;
        
        // Tonemapping - disabled to preserve colors
        if (!volumeProfile.TryGet<Tonemapping>(out var tm))
            tm = volumeProfile.Add<Tonemapping>(true);
        tm.active = true;
        tm.mode.overrideState = true;
        tm.mode.value = TonemappingMode.None;
        
        // Vignette
        if (!volumeProfile.TryGet<Vignette>(out var vig))
            vig = volumeProfile.Add<Vignette>(true);
        
        vig.active = true;
        vig.intensity.overrideState = true;
        vig.intensity.value = vignetteIntensity;
        
        EditorUtility.SetDirty(volumeProfile);
    }

    private void ApplyAllSettings()
    {
        ApplyShadowSettings();
        ApplyLightingSettings();
        ApplyColorSettings();
        
        AssetDatabase.SaveAssets();
        Debug.Log("[StylizedSetup] Applied all settings!");
    }

    private void ResetToDefaults()
    {
        shadowStrength = 1f;
        shadowDistance = 50f;
        shadowResolution = 2048;
        lightIntensity = 1f;
        ambientBrightness = 0.3f;
        brightness = 0f;
        contrast = 0f;
        saturation = 0f;
        bloomIntensity = 0f;
        vignetteIntensity = 0f;
        
        ApplyAllSettings();
        Repaint();
    }

    // Quick menu access
    [MenuItem("Tools/Quick Apply Stylized Look")]
    private static void QuickApply()
    {
        var window = CreateInstance<StylizedSetup>();
        window.OnEnable();
        window.ApplyPreset(0.4f, 60f, 1.6f, 0.6f, 0.3f, 12f, 20f, 0.4f, 0.12f);
        DestroyImmediate(window);
        Debug.Log("[StylizedSetup] Quick applied 'Soft & Bright' preset!");
    }
}
