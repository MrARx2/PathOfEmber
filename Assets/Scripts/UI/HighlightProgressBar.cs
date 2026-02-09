using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A highlight progress bar that expands from the center outward using scale animation.
/// Works with a single image - just set the pivot to center (0.5, 0.5).
/// Spawned by PrayerWheelUI to highlight button options.
/// </summary>
public class HighlightProgressBar : MonoBehaviour
{
    [Header("Scale Settings")]
    [SerializeField, Tooltip("The RectTransform to animate (scales X from 0 to 1)")]
    private RectTransform barTransform;
    
    [SerializeField, Tooltip("Duration of the expand animation")]
    private float fillDuration = 1.5f;
    
    [SerializeField, Tooltip("Destroy this GameObject after animation completes")]
    private bool destroyOnComplete = false;
    
    [Header("Optional Glow Effect")]
    [SerializeField, Tooltip("Optional glow image to pulse")]
    private Image glowImage;
    
    [SerializeField, Tooltip("Glow pulse speed")]
    private float glowPulseSpeed = 3f;
    
    [SerializeField, Tooltip("Min glow alpha")]
    private float glowAlphaMin = 0.3f;
    
    [SerializeField, Tooltip("Max glow alpha")]
    private float glowAlphaMax = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private float timer = 0f;
    private bool isAnimating = false;
    private bool fillComplete = false;
    private Vector3 targetScale = Vector3.one; // Default to 1,1,1 as safeguard

    private void Awake()
    {
        // Auto-find transform if not assigned
        if (barTransform == null)
        {
            barTransform = GetComponent<RectTransform>();
        }
        
        // Cache the target scale (full size) - use current scale if valid, otherwise default to 1
        if (barTransform != null)
        {
            Vector3 currentScale = barTransform.localScale;
            // Only use current scale if it's not zero
            if (currentScale.x > 0.01f)
            {
                targetScale = currentScale;
            }
            else
            {
                targetScale = Vector3.one; // Default to 1,1,1
            }
            
            if (debugLog) Debug.Log($"[HighlightProgressBar] Awake - targetScale: {targetScale}");
        }
    }

    private void OnEnable()
    {
        // Start animation when enabled/instantiated
        if (debugLog) Debug.Log($"[HighlightProgressBar] OnEnable called");
        StartFill();
    }

    /// <summary>
    /// Starts the scale animation from 0 to full width (center outward).
    /// </summary>
    public void StartFill()
    {
        timer = 0f;
        fillComplete = false;
        isAnimating = true;
        
        if (barTransform != null)
        {
            // Start with X scale at 0 (invisible width) - animation grows from center
            barTransform.localScale = new Vector3(0f, targetScale.y, targetScale.z);
        }
    }
    
    /// <summary>
    /// Sets the fill duration before animation starts.
    /// </summary>
    public void SetDuration(float duration)
    {
        fillDuration = duration;
    }

    private void Update()
    {
        if (!isAnimating) return;
        
        // Use unscaled time since game might be paused
        timer += Time.unscaledDeltaTime;
        
        float progress = Mathf.Clamp01(timer / fillDuration);
        
        // Use smooth easing for nicer feel
        float easedProgress = EaseOutCubic(progress);
        
        // Scale X from 0 to target (expands from center if pivot is 0.5)
        if (barTransform != null)
        {
            barTransform.localScale = new Vector3(
                targetScale.x * easedProgress,
                targetScale.y,
                targetScale.z
            );
        }
        
        // Animate glow pulse
        if (glowImage != null)
        {
            float glowT = (Mathf.Sin(timer * glowPulseSpeed) + 1f) / 2f;
            float alpha = Mathf.Lerp(glowAlphaMin, glowAlphaMax, glowT);
            Color c = glowImage.color;
            c.a = alpha;
            glowImage.color = c;
        }
        
        if (progress >= 1f && !fillComplete)
        {
            fillComplete = true;
            isAnimating = false;
            OnFillComplete();
        }
    }
    
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
    
    private void OnFillComplete()
    {
        if (destroyOnComplete)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Manually destroys this highlight bar.
    /// </summary>
    public void Remove()
    {
        Destroy(gameObject);
    }
}
