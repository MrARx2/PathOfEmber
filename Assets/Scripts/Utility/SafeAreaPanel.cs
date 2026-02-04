using UnityEngine;

/// <summary>
/// Adjusts a RectTransform to fit within the device's safe area.
/// Attach this to a panel that contains all interactive UI elements.
/// 
/// The safe area excludes notches, rounded corners, and home indicators.
/// Background elements should be parented OUTSIDE this panel to cover the full screen.
/// 
/// Usage:
/// 1. Create an empty GameObject under your Canvas named "SafeArea"
/// 2. Set its RectTransform to stretch (anchors: 0,0 to 1,1, all offsets 0)
/// 3. Attach this script
/// 4. Parent all interactive UI under this SafeArea
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaPanel : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Tooltip("Update safe area every frame (for orientation changes)")]
    private bool continuousUpdate = false;
    
    [SerializeField, Tooltip("Apply safe area in Editor for testing")]
    private bool applyInEditor = false;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    private RectTransform rectTransform;
    private Rect lastSafeArea = Rect.zero;
    private Vector2Int lastScreenSize = Vector2Int.zero;
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }
    
    private void Update()
    {
        if (continuousUpdate)
        {
            // Check if screen size or orientation changed
            if (Screen.width != lastScreenSize.x || 
                Screen.height != lastScreenSize.y ||
                Screen.orientation != lastOrientation)
            {
                ApplySafeArea();
            }
        }
    }
    
    /// <summary>
    /// Applies the safe area to this RectTransform.
    /// </summary>
    public void ApplySafeArea()
    {
        // Only apply on mobile platforms, or in Editor if applyInEditor is enabled
        bool isMobile = Application.platform == RuntimePlatform.Android || 
                        Application.platform == RuntimePlatform.IPhonePlayer;
        if (!isMobile && !applyInEditor) return;
        
        Rect safeArea = Screen.safeArea;
        
        // Skip if nothing changed
        if (safeArea == lastSafeArea && 
            Screen.width == lastScreenSize.x && 
            Screen.height == lastScreenSize.y)
        {
            return;
        }
        
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;
        
        // Convert safe area from screen coordinates to normalized anchor coordinates
        Vector2 anchorMin = new Vector2(
            safeArea.x / Screen.width,
            safeArea.y / Screen.height
        );
        
        Vector2 anchorMax = new Vector2(
            (safeArea.x + safeArea.width) / Screen.width,
            (safeArea.y + safeArea.height) / Screen.height
        );
        
        // Apply to RectTransform
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        
        // Reset offsets (anchors handle positioning)
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        if (debugLog)
        {
            Debug.Log($"[SafeAreaPanel] Screen: {Screen.width}x{Screen.height}");
            Debug.Log($"[SafeAreaPanel] Safe Area: {safeArea}");
            Debug.Log($"[SafeAreaPanel] Anchors: min={anchorMin}, max={anchorMax}");
        }
    }
    
    /// <summary>
    /// Returns whether the current device has a notch or cutout.
    /// </summary>
    public bool HasNotch()
    {
        Rect safeArea = Screen.safeArea;
        return safeArea.x > 0 || safeArea.y > 0 || 
               safeArea.width < Screen.width || 
               safeArea.height < Screen.height;
    }
    
    /// <summary>
    /// Gets the safe area insets in pixels.
    /// </summary>
    public (float top, float bottom, float left, float right) GetInsets()
    {
        Rect safeArea = Screen.safeArea;
        return (
            top: Screen.height - (safeArea.y + safeArea.height),
            bottom: safeArea.y,
            left: safeArea.x,
            right: Screen.width - (safeArea.x + safeArea.width)
        );
    }
    
#if UNITY_EDITOR
    [ContextMenu("Force Apply Safe Area")]
    private void EditorForceApply()
    {
        rectTransform = GetComponent<RectTransform>();
        lastSafeArea = Rect.zero; // Force update
        ApplySafeArea();
    }
    
    [ContextMenu("Reset To Full Screen")]
    private void EditorResetToFullScreen()
    {
        rectTransform = GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
    
    [ContextMenu("Log Safe Area Info")]
    private void EditorLogInfo()
    {
        Rect safeArea = Screen.safeArea;
        var insets = GetInsets();
        
        Debug.Log($"[SafeAreaPanel] Screen Size: {Screen.width}x{Screen.height}");
        Debug.Log($"[SafeAreaPanel] Safe Area: {safeArea}");
        Debug.Log($"[SafeAreaPanel] Has Notch: {HasNotch()}");
        Debug.Log($"[SafeAreaPanel] Insets - Top: {insets.top}, Bottom: {insets.bottom}, Left: {insets.left}, Right: {insets.right}");
    }
#endif
}
