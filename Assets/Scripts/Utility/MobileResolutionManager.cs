using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mobile-friendly resolution manager for portrait games.
/// 
/// Strategy: Fixed HORIZONTAL view, variable VERTICAL view.
/// - On taller phones (20:9, 21:9): Player sees more above/below
/// - On wider phones (16:9): Player sees the "designed" vertical view
/// - Horizontal view stays constant so player never sees outside map boundaries
/// 
/// For PERSPECTIVE cameras, this adjusts the vertical FOV to maintain
/// a constant horizontal FOV across all aspect ratios.
/// 
/// Usage:
/// - Enable this for mobile builds
/// - Disable ResolutionManager (PC version) when building for mobile
/// - Add gameplay scenes to includedScenes list (exclude menus with composed views)
/// </summary>
public class MobileResolutionManager : MonoBehaviour
{
    private static MobileResolutionManager instance;
    
    [Header("Design Settings")]
    [SerializeField, Tooltip("Design aspect ratio width (e.g., 9 for 9:16)")]
    private float designAspectWidth = 9f;
    
    [SerializeField, Tooltip("Design aspect ratio height (e.g., 16 for 9:16)")]
    private float designAspectHeight = 16f;
    
    [SerializeField, Tooltip("Design vertical FOV at reference aspect ratio")]
    private float designVerticalFOV = 18.5f;
    
    [Header("Scene Filtering")]
    [SerializeField, Tooltip("Only adjust FOV in these scenes. Leave empty to apply to ALL scenes.")]
    private string[] includedScenes = new string[] { "Game" };
    
    [Header("Runtime Options")]
    [SerializeField, Tooltip("Apply FOV adjustment even in Editor (for testing)")]
    private bool applyInEditor = true;
    
    [SerializeField, Tooltip("Minimum vertical FOV clamp (prevents too wide view on very tall screens)")]
    private float minVerticalFOV = 15f;
    
    [SerializeField, Tooltip("Maximum vertical FOV clamp (prevents too narrow view on wide screens)")]
    private float maxVerticalFOV = 30f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    // Calculated design horizontal FOV (constant across all devices)
    private float designHorizontalFOV;
    private Camera mainCamera;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool isActiveInCurrentScene = false;
    
    /// <summary>
    /// Returns true if this manager should be active on the current platform.
    /// </summary>
    public static bool ShouldBeActiveOnPlatform
    {
        get
        {
            // Always active on mobile platforms
            if (Application.platform == RuntimePlatform.Android || 
                Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return true;
            }
            
            // In Editor or other platforms, check the applyInEditor setting
            return instance != null && instance.applyInEditor;
        }
    }
    
    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Calculate design horizontal FOV from design vertical FOV and design aspect ratio
        float designAspect = designAspectWidth / designAspectHeight;
        designHorizontalFOV = CalculateHorizontalFOV(designVerticalFOV, designAspect);
        
        if (debugLog)
        {
            Debug.Log($"[MobileResolutionManager] Design aspect: {designAspect:F4} ({designAspectWidth}:{designAspectHeight})");
            Debug.Log($"[MobileResolutionManager] Design vertical FOV: {designVerticalFOV}°");
            Debug.Log($"[MobileResolutionManager] Calculated design horizontal FOV: {designHorizontalFOV:F2}°");
        }
        
        // Subscribe to scene loads to re-apply settings
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Check if current scene is included
        isActiveInCurrentScene = IsSceneIncluded(SceneManager.GetActiveScene().name);
        
        // Initial application (only if in an included scene)
        if (isActiveInCurrentScene)
        {
            ApplyResolutionSettings();
        }
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Check if this scene is in the included scenes list
        isActiveInCurrentScene = IsSceneIncluded(scene.name);
        
        if (debugLog)
        {
            Debug.Log($"[MobileResolutionManager] Scene loaded: {scene.name}, Active: {isActiveInCurrentScene}");
        }
        
        // Re-find camera and apply settings after scene load
        mainCamera = null;
        if (isActiveInCurrentScene)
        {
            Invoke(nameof(ApplyResolutionSettings), 0.1f);
        }
    }
    
    /// <summary>
    /// Checks if the given scene name is in the included scenes list.
    /// </summary>
    private bool IsSceneIncluded(string sceneName)
    {
        // If no scenes specified, apply to all scenes
        if (includedScenes == null || includedScenes.Length == 0)
            return true;
        
        foreach (string included in includedScenes)
        {
            if (string.Equals(sceneName, included, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
    
    private void Update()
    {
        // Only check for resolution changes if active in current scene
        if (!isActiveInCurrentScene) return;
        
        // Check for resolution/orientation changes
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            ApplyResolutionSettings();
        }
    }
    
    /// <summary>
    /// Applies resolution settings to the main camera.
    /// </summary>
    public void ApplyResolutionSettings()
    {
        if (!ShouldBeActiveOnPlatform) return;
        if (!isActiveInCurrentScene) return;
        
        // Find main camera if not cached
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                if (debugLog) Debug.LogWarning("[MobileResolutionManager] No main camera found!");
                return;
            }
        }
        
        // Skip if camera is orthographic (should use orthographicSize instead)
        if (mainCamera.orthographic)
        {
            ApplyOrthographicSettings();
            return;
        }
        
        // Calculate current screen aspect ratio
        float screenAspect = (float)Screen.width / Screen.height;
        
        // Calculate required vertical FOV to maintain design horizontal FOV
        float requiredVerticalFOV = CalculateVerticalFOV(designHorizontalFOV, screenAspect);
        
        // Clamp to reasonable bounds
        requiredVerticalFOV = Mathf.Clamp(requiredVerticalFOV, minVerticalFOV, maxVerticalFOV);
        
        // Apply to camera
        mainCamera.fieldOfView = requiredVerticalFOV;
        
        if (debugLog)
        {
            Debug.Log($"[MobileResolutionManager] Screen: {Screen.width}x{Screen.height}, Aspect: {screenAspect:F4}");
            Debug.Log($"[MobileResolutionManager] Applied vertical FOV: {requiredVerticalFOV:F2}° (design: {designVerticalFOV}°)");
        }
    }
    
    /// <summary>
    /// Applies settings for orthographic cameras (if used).
    /// </summary>
    private void ApplyOrthographicSettings()
    {
        float designAspect = designAspectWidth / designAspectHeight;
        float screenAspect = (float)Screen.width / Screen.height;
        
        // Calculate what horizontal world width we show at design aspect
        // orthoSize = half-height, so width = orthoSize * 2 * aspect
        float designOrthoSize = mainCamera.orthographicSize;
        float designWorldWidth = designOrthoSize * 2f * designAspect;
        
        // Calculate required ortho size to maintain that width at current aspect
        float requiredOrthoSize = designWorldWidth / (2f * screenAspect);
        
        mainCamera.orthographicSize = requiredOrthoSize;
        
        if (debugLog)
        {
            Debug.Log($"[MobileResolutionManager] Orthographic mode - Applied ortho size: {requiredOrthoSize:F2}");
        }
    }
    
    /// <summary>
    /// Calculates horizontal FOV from vertical FOV and aspect ratio.
    /// </summary>
    private float CalculateHorizontalFOV(float verticalFOV, float aspectRatio)
    {
        float verticalFOVRad = verticalFOV * Mathf.Deg2Rad;
        float horizontalFOVRad = 2f * Mathf.Atan(Mathf.Tan(verticalFOVRad / 2f) * aspectRatio);
        return horizontalFOVRad * Mathf.Rad2Deg;
    }
    
    /// <summary>
    /// Calculates vertical FOV from horizontal FOV and aspect ratio.
    /// </summary>
    private float CalculateVerticalFOV(float horizontalFOV, float aspectRatio)
    {
        float horizontalFOVRad = horizontalFOV * Mathf.Deg2Rad;
        float verticalFOVRad = 2f * Mathf.Atan(Mathf.Tan(horizontalFOVRad / 2f) / aspectRatio);
        return verticalFOVRad * Mathf.Rad2Deg;
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Force Apply Settings")]
    private void EditorForceApply()
    {
        float designAspect = designAspectWidth / designAspectHeight;
        designHorizontalFOV = CalculateHorizontalFOV(designVerticalFOV, designAspect);
        mainCamera = Camera.main;
        ApplyResolutionSettings();
    }
    
    [ContextMenu("Log Current State")]
    private void EditorLogState()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.Log("[MobileResolutionManager] No main camera found");
            return;
        }
        
        float screenAspect = (float)Screen.width / Screen.height;
        float currentHFOV = CalculateHorizontalFOV(cam.fieldOfView, screenAspect);
        
        Debug.Log($"[MobileResolutionManager] Current State:");
        Debug.Log($"  Screen: {Screen.width}x{Screen.height}");
        Debug.Log($"  Aspect: {screenAspect:F4}");
        Debug.Log($"  Vertical FOV: {cam.fieldOfView:F2}°");
        Debug.Log($"  Horizontal FOV: {currentHFOV:F2}°");
        Debug.Log($"  Design H-FOV: {designHorizontalFOV:F2}°");
    }
    #endif
}
