using UnityEngine;
using System.Collections;
#if UNITY_2022_2_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

/// <summary>
/// Controls camera intro sequence at game start.
/// Positions camera at a start position, holds for a duration, then smoothly transitions to play position.
/// Uses the same offset-based approach as CameraBossMode for consistent camera control.
/// Flow: Start Position (still) -> Lerp -> Play Position
/// </summary>
public class CameraIntroMode : MonoBehaviour
{
    [Header("Intro Camera Settings")]
    [SerializeField, Tooltip("Height above the start point")]
    private float introViewHeight = 18f;
    
    [SerializeField, Tooltip("Horizontal offset (left/right from start point)")]
    private float offsetX = 0f;
    
    [SerializeField, Tooltip("Forward/backward offset from start point")]
    private float offsetZ = 0f;
    
    [SerializeField, Tooltip("Angle to look down (0 = horizontal, 90 = straight down)")]
    private float lookDownAngle = 65f;
    
    [SerializeField, Tooltip("Distance behind the start point (for angled view)")]
    private float cameraDistance = 8f;
    
    [SerializeField, Tooltip("Base position for the intro camera view")]
    private Vector3 introBasePosition = Vector3.zero;
    
    [Header("Timing")]
    [SerializeField, Tooltip("Duration to hold at the start position before transitioning")]
    private float stillDuration = 2f;
    
    [SerializeField, Tooltip("Duration of the camera transition to play position")]
    private float lerpDuration = 1.5f;
    
    [SerializeField, Tooltip("Easing curve for smooth transition")]
    private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("References")]
    [SerializeField, Tooltip("The CameraFollowTarget that follows the player")]
    private CameraFollowTarget cameraFollowTarget;
    
#if UNITY_2022_2_OR_NEWER
    [SerializeField, Tooltip("The Cinemachine Virtual Camera")]
    private CinemachineCamera virtualCamera;
#else
    [SerializeField, Tooltip("The Cinemachine Virtual Camera")]
    private CinemachineVirtualCamera virtualCamera;
#endif
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    // State
    private bool isInIntroMode = false;
    private Coroutine introCoroutine;
    private GameObject introViewTarget;
    
    // Cached original camera settings
    private Transform originalFollowTarget;
    private Transform originalLookAtTarget;
    
    public bool IsInIntroMode => isInIntroMode;
    
    // Events
    public event System.Action OnIntroStarted;
    public event System.Action OnIntroCompleted;
    
    private void Awake()
    {
        // Create a reusable target object for intro view
        introViewTarget = new GameObject("IntroViewTarget");
        introViewTarget.transform.SetParent(transform);
        introViewTarget.SetActive(false);
        
        // Auto-find references if not assigned
        if (cameraFollowTarget == null)
            cameraFollowTarget = FindFirstObjectByType<CameraFollowTarget>();
        
        if (virtualCamera == null)
        {
#if UNITY_2022_2_OR_NEWER
            virtualCamera = FindFirstObjectByType<CinemachineCamera>();
#else
            virtualCamera = FindFirstObjectByType<CinemachineVirtualCamera>();
#endif
        }
    }
    
    private void Start()
    {
        // Start the intro sequence automatically
        StartIntroSequence();
    }
    
    /// <summary>
    /// Calculates the intro camera position based on settings (same approach as CameraBossMode).
    /// </summary>
    private Vector3 CalculateIntroViewPosition()
    {
        // Position: above and offset from the base position
        // offsetX = left/right, introViewHeight = up, offsetZ - cameraDistance = forward/back
        Vector3 offset = new Vector3(offsetX, introViewHeight, offsetZ - cameraDistance);
        return introBasePosition + offset;
    }
    
    /// <summary>
    /// Calculates the intro camera rotation based on settings (same approach as CameraBossMode).
    /// </summary>
    private Quaternion CalculateIntroViewRotation()
    {
        // Rotation: looking down at the intro point
        return Quaternion.Euler(lookDownAngle, 0, 0);
    }
    
    /// <summary>
    /// Starts the intro camera sequence.
    /// </summary>
    public void StartIntroSequence()
    {
        if (isInIntroMode)
        {
            if (debugLog) Debug.Log("[CameraIntroMode] Already in intro mode");
            return;
        }
        
        if (introCoroutine != null)
            StopCoroutine(introCoroutine);
        
        introCoroutine = StartCoroutine(IntroSequence());
    }
    
    /// <summary>
    /// Skip the intro and immediately go to play position.
    /// </summary>
    public void SkipIntro()
    {
        if (!isInIntroMode)
        {
            if (debugLog) Debug.Log("[CameraIntroMode] Not in intro mode");
            return;
        }
        
        if (introCoroutine != null)
            StopCoroutine(introCoroutine);
        
        // Immediately restore normal camera behavior
        RestorePlayMode();
    }
    
    private IEnumerator IntroSequence()
    {
        if (debugLog) Debug.Log("[CameraIntroMode] Starting intro sequence...");
        
        isInIntroMode = true;
        OnIntroStarted?.Invoke();
        
        // Store original camera follow/look targets
        if (virtualCamera != null)
        {
            originalFollowTarget = virtualCamera.Follow;
            originalLookAtTarget = virtualCamera.LookAt;
        }
        
        // Calculate intro view position and rotation
        Vector3 introPosition = CalculateIntroViewPosition();
        Quaternion introRotation = CalculateIntroViewRotation();
        
        // Position the intro view target
        introViewTarget.transform.position = introPosition;
        introViewTarget.transform.rotation = introRotation;
        introViewTarget.SetActive(true);
        
        // Make camera follow our intro target (same approach as CameraBossMode)
        if (virtualCamera != null)
        {
            virtualCamera.Follow = introViewTarget.transform;
            virtualCamera.LookAt = null;
        }
        
        if (debugLog) Debug.Log($"[CameraIntroMode] Camera positioned at: {introPosition}, rotation: {introRotation.eulerAngles}");
        
        // === PHASE 1: Hold at start position for stillDuration ===
        if (stillDuration > 0f)
        {
            yield return new WaitForSeconds(stillDuration);
        }
        
        if (debugLog) Debug.Log("[CameraIntroMode] Still duration complete, transitioning to play position...");
        
        // === PHASE 2: Lerp to play position ===
        Vector3 startPos = introPosition;
        Quaternion startRot = introRotation;
        
        // Get target play position (camera follow target position)
        Vector3 targetPosition = cameraFollowTarget != null ? 
            cameraFollowTarget.transform.position : 
            (originalFollowTarget != null ? originalFollowTarget.position : startPos);
        
        // Get target rotation from the current main camera (this is what normal gameplay uses)
        Quaternion targetRotation = Camera.main != null ? 
            Camera.main.transform.rotation : startRot;
        
        float elapsed = 0f;
        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(Mathf.Clamp01(elapsed / lerpDuration));
            
            // Update target position as camera follow target might be moving
            if (cameraFollowTarget != null)
                targetPosition = cameraFollowTarget.transform.position;
            
            // Move and rotate the intro view target toward play position
            introViewTarget.transform.position = Vector3.Lerp(startPos, targetPosition, t);
            introViewTarget.transform.rotation = Quaternion.Slerp(startRot, targetRotation, t);
            
            yield return null;
        }
        
        // === PHASE 3: Restore normal camera behavior ===
        RestorePlayMode();
        
        if (debugLog) Debug.Log("[CameraIntroMode] Intro sequence complete!");
    }
    
    private void RestorePlayMode()
    {
        // Restore original camera behavior
        if (virtualCamera != null)
        {
            virtualCamera.Follow = originalFollowTarget;
            virtualCamera.LookAt = originalLookAtTarget;
        }
        
        introViewTarget.SetActive(false);
        isInIntroMode = false;
        introCoroutine = null;
        
        OnIntroCompleted?.Invoke();
    }
    
    #region Debug
    [ContextMenu("Debug: Start Intro Sequence")]
    private void DebugStartIntro()
    {
        // Reset state and start intro
        isInIntroMode = false;
        StartIntroSequence();
    }
    
    [ContextMenu("Debug: Skip Intro")]
    private void DebugSkipIntro()
    {
        SkipIntro();
    }
    #endregion
}
