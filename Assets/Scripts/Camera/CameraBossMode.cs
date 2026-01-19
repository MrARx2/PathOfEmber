using UnityEngine;
using System.Collections;
#if UNITY_2022_2_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

/// <summary>
/// Controls camera transitions for boss arena encounters.
/// Smoothly transitions from player-following to a fixed overhead position and back.
/// </summary>
public class CameraBossMode : MonoBehaviour
{
    public static CameraBossMode Instance { get; private set; }
    
    [Header("Boss Camera Settings")]
    [SerializeField, Tooltip("Height above the arena center")]
    private float bossViewHeight = 18f;
    
    [SerializeField, Tooltip("Horizontal offset (left/right from arena center)")]
    private float offsetX = 0f;
    
    [SerializeField, Tooltip("Forward/backward offset from arena center")]
    private float offsetZ = 0f;
    
    [SerializeField, Tooltip("Angle to look down at arena (0 = horizontal, 90 = straight down)")]
    private float lookDownAngle = 65f;
    
    [SerializeField, Tooltip("Distance behind the arena center (for angled view)")]
    private float cameraDistance = 8f;
    
    [Header("Transition Settings")]
    [SerializeField, Tooltip("Duration of camera transition in seconds")]
    private float transitionDuration = 1.5f;
    
    [SerializeField, Tooltip("Easing curve for smooth transition")]
    private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("References")]
    [SerializeField, Tooltip("The CameraFollowTarget that follows the player")]
    private CameraFollowTarget cameraFollowTarget;
    
    [SerializeField, Tooltip("The Cinemachine Virtual Camera")]
#if UNITY_2022_2_OR_NEWER
    private CinemachineCamera virtualCamera;
#else
    private CinemachineVirtualCamera virtualCamera;
#endif
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    // State
    private bool isInBossMode = false;
    private Coroutine transitionCoroutine;
    private Transform currentArenaCenter;
    
    // Cached original camera settings
    private Transform originalFollowTarget;
    private Transform originalLookAtTarget;
    
    // Fixed position for boss mode
    private GameObject bossViewTarget;
    
    public bool IsInBossMode => isInBossMode;
    
    // Events
    public event System.Action OnBossModeEntered;
    public event System.Action OnBossModeExited;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Create a reusable target object for boss view
        bossViewTarget = new GameObject("BossViewTarget");
        bossViewTarget.transform.SetParent(transform);
        bossViewTarget.SetActive(false);
        
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
    
    /// <summary>
    /// Enter boss mode - transition camera to fixed position above the arena.
    /// </summary>
    /// <param name="arenaCenter">Transform at the center of the boss arena</param>
    public void EnterBossMode(Transform arenaCenter)
    {
        if (isInBossMode)
        {
            if (debugLog) Debug.Log("[CameraBossMode] Already in boss mode");
            return;
        }
        
        if (arenaCenter == null)
        {
            Debug.LogError("[CameraBossMode] arenaCenter is null!");
            return;
        }
        
        currentArenaCenter = arenaCenter;
        
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);
        
        transitionCoroutine = StartCoroutine(TransitionToBossMode());
    }
    
    /// <summary>
    /// Exit boss mode - transition camera back to player-following behavior.
    /// </summary>
    public void ExitBossMode()
    {
        if (!isInBossMode)
        {
            if (debugLog) Debug.Log("[CameraBossMode] Not in boss mode");
            return;
        }
        
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);
        
        transitionCoroutine = StartCoroutine(TransitionToNormalMode());
    }
    
    private IEnumerator TransitionToBossMode()
    {
        if (debugLog) Debug.Log("[CameraBossMode] Transitioning to boss mode...");
        
        // Calculate target position for boss view
        Vector3 targetPosition = CalculateBossViewPosition();
        Quaternion targetRotation = CalculateBossViewRotation();
        
        // Store original camera follow/look targets
        if (virtualCamera != null)
        {
            originalFollowTarget = virtualCamera.Follow;
            originalLookAtTarget = virtualCamera.LookAt;
        }
        
        // Position the boss view target
        bossViewTarget.transform.position = targetPosition;
        bossViewTarget.transform.rotation = targetRotation;
        bossViewTarget.SetActive(true);
        
        // Get starting position (current camera position via follow target)
        Vector3 startPosition = cameraFollowTarget != null ? 
            cameraFollowTarget.transform.position : 
            Camera.main.transform.position;
        Quaternion startRotation = Camera.main != null ? 
            Camera.main.transform.rotation : 
            Quaternion.identity;
        
        // Animate transition
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            
            // Move the boss view target (which camera will follow)
            bossViewTarget.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            bossViewTarget.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            
            // Update virtual camera to follow boss target
            if (virtualCamera != null && elapsed < transitionDuration * 0.1f)
            {
                virtualCamera.Follow = bossViewTarget.transform;
                virtualCamera.LookAt = currentArenaCenter;
            }
            
            yield return null;
        }
        
        // Finalize
        bossViewTarget.transform.position = targetPosition;
        bossViewTarget.transform.rotation = targetRotation;
        
        if (virtualCamera != null)
        {
            virtualCamera.Follow = bossViewTarget.transform;
            virtualCamera.LookAt = currentArenaCenter;
        }
        
        isInBossMode = true;
        transitionCoroutine = null;
        
        OnBossModeEntered?.Invoke();
        
        if (debugLog) Debug.Log("[CameraBossMode] Boss mode entered!");
    }
    
    private IEnumerator TransitionToNormalMode()
    {
        if (debugLog) Debug.Log("[CameraBossMode] Transitioning to normal mode...");
        
        // Get current position
        Vector3 startPosition = bossViewTarget.transform.position;
        Quaternion startRotation = bossViewTarget.transform.rotation;
        
        // Get player follow target position
        Vector3 targetPosition = cameraFollowTarget != null ? 
            cameraFollowTarget.transform.position : 
            (originalFollowTarget != null ? originalFollowTarget.position : startPosition);
        Quaternion targetRotation = Camera.main != null ? 
            Quaternion.LookRotation(Camera.main.transform.forward) : 
            startRotation;
        
        // Animate transition
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            
            // Update target position as player might be moving
            if (cameraFollowTarget != null)
                targetPosition = cameraFollowTarget.transform.position;
            
            bossViewTarget.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            
            yield return null;
        }
        
        // Restore original camera behavior
        if (virtualCamera != null)
        {
            virtualCamera.Follow = originalFollowTarget;
            virtualCamera.LookAt = originalLookAtTarget;
        }
        
        bossViewTarget.SetActive(false);
        isInBossMode = false;
        currentArenaCenter = null;
        transitionCoroutine = null;
        
        OnBossModeExited?.Invoke();
        
        if (debugLog) Debug.Log("[CameraBossMode] Normal mode restored!");
    }
    
    private Vector3 CalculateBossViewPosition()
    {
        if (currentArenaCenter == null) return Vector3.zero;
        
        // Position: above and offset from the arena center
        // offsetX = left/right, bossViewHeight = up, offsetZ - cameraDistance = forward/back
        Vector3 offset = new Vector3(offsetX, bossViewHeight, offsetZ - cameraDistance);
        return currentArenaCenter.position + offset;
    }
    
    private Quaternion CalculateBossViewRotation()
    {
        // Rotation: looking down at the arena
        return Quaternion.Euler(lookDownAngle, 0, 0);
    }
    
    /// <summary>
    /// Updates the boss view position if the arena center moves.
    /// </summary>
    public void UpdateBossViewPosition()
    {
        if (!isInBossMode || currentArenaCenter == null) return;
        
        bossViewTarget.transform.position = CalculateBossViewPosition();
        bossViewTarget.transform.rotation = CalculateBossViewRotation();
    }
    
    #region Debug
    [ContextMenu("Debug: Enter Boss Mode (Use Selected Object)")]
    private void DebugEnterBossMode()
    {
#if UNITY_EDITOR
        if (UnityEditor.Selection.activeTransform != null)
        {
            EnterBossMode(UnityEditor.Selection.activeTransform);
        }
        else
        {
            Debug.LogWarning("[CameraBossMode] Select a GameObject in the scene to use as arena center");
        }
#endif
    }
    
    [ContextMenu("Debug: Exit Boss Mode")]
    private void DebugExitBossMode()
    {
        ExitBossMode();
    }
    #endregion
}
