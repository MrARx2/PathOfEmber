using UnityEngine;
using TMPro;

/// <summary>
/// Displays FPS counter that can be toggled on/off.
/// Attach to InGameMenu or any persistent UI object.
/// </summary>
public class FPSDisplay : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField, Tooltip("TMP_Text component to display FPS. If null, will create one automatically.")]
    private TMP_Text fpsText;
    
    [Header("Settings")]
    [SerializeField, Tooltip("How often to update the FPS display (in seconds)")]
    private float updateInterval = 0.5f;
    
    [SerializeField, Tooltip("Start with FPS display enabled")]
    private bool showOnStart = true;
    
    [Header("Styling")]
    [SerializeField, Tooltip("Font size for the FPS text")]
    private float fontSize = 24f;
    
    [SerializeField, Tooltip("Color for good FPS (60+)")]
    private Color goodColor = Color.green;
    
    [SerializeField, Tooltip("Color for medium FPS (30-59)")]
    private Color mediumColor = Color.yellow;
    
    [SerializeField, Tooltip("Color for bad FPS (<30)")]
    private Color badColor = Color.red;
    
    // Internal
    private float deltaTime = 0f;
    private float timer = 0f;
    private bool isVisible = true;
    private GameObject fpsContainer;
    
    private void Start()
    {
        // Create UI if not assigned
        if (fpsText == null)
        {
            CreateFPSUI();
        }
        
        isVisible = showOnStart;
        UpdateVisibility();
    }
    
    private void Update()
    {
        if (!isVisible) return;
        
        // Calculate delta time using unscaled time (works even when game is paused)
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        
        timer += Time.unscaledDeltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            UpdateFPSText();
        }
    }
    
    /// <summary>
    /// Toggles the FPS display on/off.
    /// </summary>
    public void Toggle()
    {
        isVisible = !isVisible;
        UpdateVisibility();
    }
    
    /// <summary>
    /// Sets the FPS display visibility.
    /// </summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        UpdateVisibility();
    }
    
    /// <summary>
    /// Returns whether the FPS display is currently visible.
    /// </summary>
    public bool IsVisible => isVisible;
    
    private void UpdateVisibility()
    {
        if (fpsText != null)
        {
            fpsText.gameObject.SetActive(isVisible);
        }
        
        if (fpsContainer != null)
        {
            fpsContainer.SetActive(isVisible);
        }
    }
    
    private void UpdateFPSText()
    {
        if (fpsText == null) return;
        
        float fps = 1f / deltaTime;
        int fpsInt = Mathf.RoundToInt(fps);
        
        fpsText.text = $"FPS: {fpsInt}";
        
        // Color based on FPS
        if (fps >= 60f)
            fpsText.color = goodColor;
        else if (fps >= 30f)
            fpsText.color = mediumColor;
        else
            fpsText.color = badColor;
    }
    
    /// <summary>
    /// Creates the FPS UI automatically if no text component is assigned.
    /// </summary>
    private void CreateFPSUI()
    {
        // Find or create a canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }
        
        if (canvas == null)
        {
            Debug.LogError("[FPSDisplay] No Canvas found! Please assign fpsText manually or add this to a Canvas.");
            return;
        }
        
        // Create container
        fpsContainer = new GameObject("FPS_Container");
        fpsContainer.transform.SetParent(canvas.transform, false);
        
        var containerRect = fpsContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1); // Top-left
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.anchoredPosition = new Vector2(10, -10);
        containerRect.sizeDelta = new Vector2(150, 40);
        
        // Create text
        var textGO = new GameObject("FPS_Text");
        textGO.transform.SetParent(fpsContainer.transform, false);
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        fpsText = textGO.AddComponent<TextMeshProUGUI>();
        fpsText.fontSize = fontSize;
        fpsText.fontStyle = FontStyles.Bold;
        fpsText.alignment = TextAlignmentOptions.TopLeft;
        fpsText.text = "FPS: --";
        fpsText.color = goodColor;
        
        // Add outline for visibility
        fpsText.outlineWidth = 0.2f;
        fpsText.outlineColor = Color.black;
    }
}
