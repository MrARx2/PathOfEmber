using UnityEngine;
using System.Collections;

/// <summary>
/// Manages smooth time scale transitions for game feel.
/// Provides Archero 2-style smooth slowdown instead of instant freezing.
/// Singleton pattern for easy global access.
/// </summary>
public class TimeScaleManager : MonoBehaviour
{
    public static TimeScaleManager Instance { get; private set; }

    [Header("Slowdown Settings")]
    [SerializeField, Tooltip("Duration of the slowdown transition in real seconds")]
    private float slowdownDuration = 0.4f;
    
    [SerializeField, Tooltip("Duration of the resume transition in real seconds")]
    private float resumeDuration = 0.25f;
    
    [SerializeField, Tooltip("Target time scale when slowed (0 = full pause, 0.02 = subtle motion)")]
    [Range(0f, 0.1f)]
    private float targetSlowedTimeScale = 0f;

    [Header("Easing")]
    [SerializeField, Tooltip("Easing type for transitions")]
    private EasingType easingType = EasingType.CubicEaseOut;

    public enum EasingType
    {
        Linear,
        CubicEaseOut,      // Fast start, gentle end (recommended for slowdown)
        CubicEaseIn,       // Slow start, fast end
        CubicEaseInOut,    // Smooth both ends
        QuadraticEaseOut,
        ExponentialEaseOut
    }

    // State tracking
    private Coroutine activeTransition;
    private float originalFixedDeltaTime;
    private float previousTimeScale = 1f;
    private bool isSlowed = false;

    /// <summary>
    /// Returns true if the game is currently in a slowed state.
    /// </summary>
    public bool IsSlowed => isSlowed;

    /// <summary>
    /// Returns the current target time scale when slowed.
    /// </summary>
    public float TargetSlowedTimeScale => targetSlowedTimeScale;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Store original fixed delta time (typically 0.02)
        originalFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Smoothly slows down time to the target time scale.
    /// </summary>
    /// <param name="onComplete">Optional callback when slowdown completes.</param>
    public void SmoothSlowdown(System.Action onComplete = null)
    {
        SmoothSlowdown(targetSlowedTimeScale, slowdownDuration, onComplete);
    }

    /// <summary>
    /// Smoothly slows down time with custom parameters.
    /// </summary>
    /// <param name="targetTimeScale">Target time scale (0-1).</param>
    /// <param name="duration">Transition duration in real seconds.</param>
    /// <param name="onComplete">Optional callback when slowdown completes.</param>
    public void SmoothSlowdown(float targetTimeScale, float duration, System.Action onComplete = null)
    {
        if (isSlowed) return;

        // Store current time scale before slowing
        previousTimeScale = Time.timeScale;
        isSlowed = true;

        // Stop any active transition
        if (activeTransition != null)
        {
            StopCoroutine(activeTransition);
        }

        activeTransition = StartCoroutine(TransitionTimeScale(targetTimeScale, duration, onComplete));
        Debug.Log($"[TimeScaleManager] Starting smooth slowdown: {Time.timeScale} → {targetTimeScale} over {duration}s");
    }

    /// <summary>
    /// Smoothly resumes time to normal (1.0) or previous time scale.
    /// </summary>
    /// <param name="onComplete">Optional callback when resume completes.</param>
    public void SmoothResume(System.Action onComplete = null)
    {
        SmoothResume(previousTimeScale, resumeDuration, onComplete);
    }

    /// <summary>
    /// Smoothly resumes time with custom parameters.
    /// </summary>
    /// <param name="targetTimeScale">Target time scale to resume to.</param>
    /// <param name="duration">Transition duration in real seconds.</param>
    /// <param name="onComplete">Optional callback when resume completes.</param>
    public void SmoothResume(float targetTimeScale, float duration, System.Action onComplete = null)
    {
        if (!isSlowed) return;

        isSlowed = false;

        // Stop any active transition
        if (activeTransition != null)
        {
            StopCoroutine(activeTransition);
        }

        activeTransition = StartCoroutine(TransitionTimeScale(targetTimeScale, duration, onComplete));
        Debug.Log($"[TimeScaleManager] Starting smooth resume: {Time.timeScale} → {targetTimeScale} over {duration}s");
    }

    /// <summary>
    /// Immediately sets time scale without transition (for emergencies/resets).
    /// </summary>
    public void SetTimeScaleImmediate(float timeScale)
    {
        if (activeTransition != null)
        {
            StopCoroutine(activeTransition);
            activeTransition = null;
        }

        Time.timeScale = timeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * Mathf.Max(timeScale, 0.01f);
        isSlowed = timeScale < 0.5f;
        
        Debug.Log($"[TimeScaleManager] Immediate time scale set to: {timeScale}");
    }

    /// <summary>
    /// Forces reset to normal time (for scene changes, game over, etc.)
    /// </summary>
    public void ForceResetTime()
    {
        SetTimeScaleImmediate(1f);
        previousTimeScale = 1f;
        isSlowed = false;
    }

    private IEnumerator TransitionTimeScale(float targetTimeScale, float duration, System.Action onComplete)
    {
        float startTimeScale = Time.timeScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // CRITICAL: Use unscaledDeltaTime since timeScale is changing!
            elapsed += Time.unscaledDeltaTime;
            
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = ApplyEasing(t);

            // Lerp time scale
            float newTimeScale = Mathf.Lerp(startTimeScale, targetTimeScale, easedT);
            Time.timeScale = newTimeScale;
            
            // Scale fixed delta time to keep physics consistent
            // Use a minimum value to avoid division issues when timeScale is 0
            Time.fixedDeltaTime = originalFixedDeltaTime * Mathf.Max(newTimeScale, 0.01f);

            yield return null;
        }

        // Ensure we hit exact target
        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * Mathf.Max(targetTimeScale, 0.01f);
        
        activeTransition = null;
        onComplete?.Invoke();
        
        Debug.Log($"[TimeScaleManager] Transition complete. TimeScale: {Time.timeScale}");
    }

    private float ApplyEasing(float t)
    {
        return easingType switch
        {
            EasingType.Linear => t,
            EasingType.CubicEaseOut => 1f - Mathf.Pow(1f - t, 3f),
            EasingType.CubicEaseIn => t * t * t,
            EasingType.CubicEaseInOut => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f,
            EasingType.QuadraticEaseOut => 1f - (1f - t) * (1f - t),
            EasingType.ExponentialEaseOut => t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t),
            _ => t
        };
    }

    #region Debug Methods
    [ContextMenu("Debug: Test Smooth Slowdown")]
    public void DebugTestSlowdown()
    {
        if (!isSlowed)
            SmoothSlowdown();
        else
            SmoothResume();
    }

    [ContextMenu("Debug: Force Reset Time")]
    public void DebugForceReset() => ForceResetTime();
    #endregion
}
