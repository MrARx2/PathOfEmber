using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolution-independent display manager for portrait games on PC.
/// 
/// This script ensures the game displays correctly at ANY resolution by:
/// 1. Enforcing exact 9:16 aspect ratio via pillarboxing
/// 2. Scaling internal rendering appropriately
/// 3. Persisting across scene loads
/// 
/// Works with both windowed and fullscreen modes.
/// </summary>
public class ResolutionManager : MonoBehaviour
{
    private static ResolutionManager instance;
    
    [Header("Design Resolution")]
    [Tooltip("The resolution your game was designed for")]
    [SerializeField] private int designWidth = 1080;
    [SerializeField] private int designHeight = 1920;
    
    [Header("Display Mode")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.FullscreenPillarbox;
    
    [Header("Pillarbox Settings")]
    [SerializeField] private Color pillarboxColor = Color.black;
    
    public enum DisplayMode
    {
        FullscreenPillarbox,    // Fullscreen with black bars (recommended for presentation)
        WindowedExact,          // Windowed at exact design resolution (may be too tall)
        WindowedScaled          // Windowed at scaled resolution (fits screen)
    }
    
    // Calculated values
    private float targetAspect;
    private Camera[] allCameras;
    
    public static float TargetAspect => instance != null ? instance.targetAspect : 9f/16f;
    
    private void Awake()
    {
        // Singleton
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        targetAspect = (float)designWidth / designHeight;
        
        ApplyDisplayMode();
        
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-apply to new scene's cameras after a brief delay
        Invoke(nameof(ApplyCameraViewports), 0.1f);
    }
    
    private void ApplyDisplayMode()
    {
        switch (displayMode)
        {
            case DisplayMode.FullscreenPillarbox:
                // Use native resolution in fullscreen, we'll handle aspect via viewport
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                ApplyCameraViewports();
                break;
                
            case DisplayMode.WindowedExact:
                // Exact design resolution (may not fit on screen)
                Screen.SetResolution(designWidth, designHeight, FullScreenMode.Windowed);
                break;
                
            case DisplayMode.WindowedScaled:
                // Scale to fit screen while maintaining aspect ratio
                int screenHeight = Screen.currentResolution.height - 100; // Leave room for window border
                int windowHeight = Mathf.Min(screenHeight, designHeight);
                int windowWidth = Mathf.RoundToInt(windowHeight * targetAspect);
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
                break;
        }
    }
    
    private void ApplyCameraViewports()
    {
        // Find all cameras and apply viewport constraint
        allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        
        float windowAspect = (float)Screen.width / Screen.height;
        
        foreach (var cam in allCameras)
        {
            if (cam == null) continue;
            
            // Skip UI cameras (they should render full screen)
            if (cam.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
            
            ApplyViewportToCamera(cam, windowAspect);
        }
        
        // Create pillarbox background if needed
        if (displayMode == DisplayMode.FullscreenPillarbox)
        {
            CreatePillarboxBackground();
        }
    }
    
    private void ApplyViewportToCamera(Camera cam, float windowAspect)
    {
        float scaleHeight = windowAspect / targetAspect;
        
        Rect rect = cam.rect;
        
        if (scaleHeight < 1.0f)
        {
            // Window is taller than target - letterbox (bars on top/bottom)
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
        }
        else
        {
            // Window is wider than target - pillarbox (bars on sides)
            float scaleWidth = 1.0f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
        }
        
        cam.rect = rect;
    }
    
    private void CreatePillarboxBackground()
    {
        // Find or create background camera for pillarbox color
        const string bgCamName = "_PillarboxCamera";
        GameObject bgCamObj = GameObject.Find(bgCamName);
        
        if (bgCamObj == null)
        {
            bgCamObj = new GameObject(bgCamName);
            bgCamObj.transform.SetParent(transform);
            
            Camera bgCam = bgCamObj.AddComponent<Camera>();
            bgCam.depth = -100; // Render first (behind everything)
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = pillarboxColor;
            bgCam.cullingMask = 0; // Don't render anything, just clear to color
            bgCam.rect = new Rect(0, 0, 1, 1); // Full screen
        }
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Apply Display Mode")]
    private void EditorApply()
    {
        targetAspect = (float)designWidth / designHeight;
        ApplyDisplayMode();
    }
    #endif
}
