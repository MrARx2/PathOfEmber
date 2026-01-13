using UnityEngine;

/// <summary>
/// Simple pulse animation that smoothly grows and shrinks the attached transform.
/// Attach to any UI element (text, image, etc.) to make it pulse attractively.
/// </summary>
public class PulseAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField, Tooltip("Minimum scale multiplier")]
    private float minScale = 0.95f;
    
    [SerializeField, Tooltip("Maximum scale multiplier")]
    private float maxScale = 1.05f;
    
    [SerializeField, Tooltip("Speed of the pulse animation")]
    private float pulseSpeed = 2f;

    [Header("Pivot Offset")]
    [SerializeField, Tooltip("Y offset to shift the growth origin (positive = grow from lower point, keeping top stable)")]
    private float pivotOffsetY = 0f;

    [Header("Options")]
    [SerializeField, Tooltip("If true, animation starts automatically")]
    private bool playOnStart = true;
    
    [SerializeField, Tooltip("If true, uses unscaled time (works when game is paused)")]
    private bool useUnscaledTime = false;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private bool isPlaying = false;
    private float time;

    private void Awake()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!isPlaying) return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        time += deltaTime * pulseSpeed;

        // Sine wave oscillation between minScale and maxScale
        float scaleMultiplier = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(time) + 1f) / 2f);
        transform.localScale = originalScale * scaleMultiplier;

        // Compensate position based on pivot offset to keep one end stable
        if (pivotOffsetY != 0f)
        {
            float scaleDelta = scaleMultiplier - 1f;
            float positionCompensation = pivotOffsetY * scaleDelta;
            transform.localPosition = originalPosition + new Vector3(0f, positionCompensation, 0f);
        }
    }

    /// <summary>
    /// Starts the pulse animation.
    /// </summary>
    public void Play()
    {
        isPlaying = true;
        time = 0f;
    }

    /// <summary>
    /// Stops the pulse animation and resets to original scale.
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        transform.localScale = originalScale;
    }

    /// <summary>
    /// Pauses the animation without resetting scale.
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>
    /// Resumes a paused animation.
    /// </summary>
    public void Resume()
    {
        isPlaying = true;
    }
}
