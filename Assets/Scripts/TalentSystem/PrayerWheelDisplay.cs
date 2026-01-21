using UnityEngine;

/// <summary>
/// Controls prayer wheel display in front of the main camera.
/// Handles visibility toggling with game pause/resume functionality.
/// Positions the prayer wheel relative to a camera transform with configurable offset.
/// </summary>
public class PrayerWheelDisplay : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField, Tooltip("The main camera transform to position wheels in front of. Assign your main camera here.")]
    private Transform cameraTransform;

    [Header("Wheel References")]
    [SerializeField, Tooltip("Reference to Prayer Wheel 1 root transform")]
    private Transform prayerWheel1;
    
    [SerializeField, Tooltip("Reference to Prayer Wheel 2 root transform")]
    private Transform prayerWheel2;

    [Header("Base Object (Optional)")]
    [SerializeField, Tooltip("Prefab for static base that spawns with the wheels")]
    private GameObject wheelBasePrefab;
    
    [SerializeField, Tooltip("X = Horizontal Offset. Y = Vertical offset. Z = Forward/Back (positive = toward camera).")]
    private Vector3 basePositionOffset = new Vector3(0f, -2f, 0f);
    
    [SerializeField, Tooltip("Rotation offset. Y is added to camera's Y rotation. X and Z are absolute tilts.")]
    private Vector3 baseRotationOffset = Vector3.zero;

    [Header("Position Offset")]
    [SerializeField, Tooltip("Distance in front of the camera")]
    private float forwardOffset = 3f;
    
    [SerializeField, Tooltip("Horizontal offset for Wheel 1 (Left)")]
    private float wheel1HorizontalOffset = 1.5f;

    [SerializeField, Tooltip("Horizontal offset for Wheel 2 (Right)")]
    private float wheel2HorizontalOffset = 1.5f;
    
    [SerializeField, Tooltip("Shift both wheels left/right together (positive = right)")]
    private float horizontalShift = 0f;
    
    [SerializeField, Tooltip("Vertical offset (positive = up)")]
    private float verticalOffset = 0f;

    [Header("Settings")]
    [SerializeField, Tooltip("If true, pauses the game when wheels are shown and resumes when hidden")]
    private bool pauseGameWhenVisible = true;
    
    [SerializeField, Tooltip("Layer to set wheels to (use Default = 0 for main camera visibility)")]
    private int wheelLayer = 0;

    [Header("Smooth Time Transition (Archero 2 Style)")]
    [SerializeField, Tooltip("Use smooth slowdown instead of instant freeze. Player can still move during slowdown.")]
    private bool useSmoothSlowdown = true;
    
    [SerializeField, Tooltip("Target time scale during slowdown (0.15 = 15% speed). Full freeze happens at spin complete.")]
    [Range(0.05f, 0.5f)]
    private float slowdownTargetTimeScale = 0.15f;
    
    [SerializeField, Tooltip("Duration of the slowdown transition (longer = more dramatic)")]
    private float slowdownDuration = 1.2f;
    
    [SerializeField, Tooltip("Duration of the resume transition")]
    private float resumeDuration = 0.3f;

    // State
    private bool isVisible = false;
    private float previousTimeScale = 1f;
    private bool isInitialized = false;
    private GameObject spawnedBase; // Spawned instance of the base

    /// <summary>
    /// Returns whether the prayer wheels are currently visible.
    /// </summary>
    public bool IsVisible => isVisible;

    /// <summary>
    /// The camera transform used for positioning.
    /// </summary>
    public Transform CameraTransform
    {
        get => cameraTransform;
        set => cameraTransform = value;
    }

    private void Awake()
    {
        // Try to find main camera if not assigned
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
                Debug.Log("[PrayerWheelDisplay] Auto-assigned main camera transform.");
            }
            else
            {
                Debug.LogWarning("[PrayerWheelDisplay] No camera transform assigned and Camera.main not found!");
            }
        }
    }

    private void Start()
    {
        // Fix layers so main camera can see the wheels
        FixWheelLayers();
        
        // Start hidden - directly set active false, don't use Hide() to avoid time scale issues
        if (prayerWheel1 != null) prayerWheel1.gameObject.SetActive(false);
        if (prayerWheel2 != null) prayerWheel2.gameObject.SetActive(false);
        isVisible = false;
        
        // Mark as initialized - now Show()/Hide() can affect time scale
        isInitialized = true;
        
        // Ensure time scale is normal on start
        Time.timeScale = 1f;
        
        Debug.Log($"[PrayerWheelDisplay] Initialized. Wheel1: {(prayerWheel1 != null ? prayerWheel1.name : "NULL")}, Wheel2: {(prayerWheel2 != null ? prayerWheel2.name : "NULL")}");
    }

    /// <summary>
    /// Continuously updates wheel positions while visible.
    /// This ensures wheels stay in front of camera during smooth slowdown when player/camera is still moving.
    /// </summary>
    private void LateUpdate()
    {
        // Only update positions while wheels are visible
        if (isVisible)
        {
            UpdateWheelPositions();
        }
    }

    private void FixWheelLayers()
    {
        if (prayerWheel1 != null)
        {
            SetLayerRecursive(prayerWheel1.gameObject, wheelLayer);
            Debug.Log($"[PrayerWheelDisplay] Set wheel 1 to layer {wheelLayer}.");
        }

        if (prayerWheel2 != null)
        {
            SetLayerRecursive(prayerWheel2.gameObject, wheelLayer);
            Debug.Log($"[PrayerWheelDisplay] Set wheel 2 to layer {wheelLayer}.");
        }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Toggles the visibility of the prayer wheels.
    /// Shows if hidden, hides if visible.
    /// </summary>
    public void ToggleVisibility()
    {
        if (isVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>
    /// Shows the prayer wheels in front of the camera.
    /// Optionally pauses the game.
    /// </summary>
    public void Show()
    {
        if (isVisible) return;

        isVisible = true;
        
        Debug.Log("[PrayerWheelDisplay] Show() called.");

        // Show wheels first
        SetWheelsVisible(true);

        // Position wheels in front of camera
        UpdateWheelPositions();

        // Start slowdown if configured (only after initialization to avoid startup issues)
        if (pauseGameWhenVisible && isInitialized)
        {
            // Always assume we want to return to full speed (1.0)
            // This prevents issues with double level-ups where the second wheel captures 
            // the slowed time of the first wheel's transition.
            previousTimeScale = 1f;
            
            if (useSmoothSlowdown)
            {
                // Start smooth slowdown to target (not 0 - player can still move during spin)
                StartCoroutine(SmoothTimeTransition(slowdownTargetTimeScale, slowdownDuration));
                Debug.Log($"[PrayerWheelDisplay] Smooth slowdown started (target: {slowdownTargetTimeScale}, duration: {slowdownDuration}s).");
            }
            else
            {
                // Legacy instant freeze
                Time.timeScale = 0f;
                Debug.Log("[PrayerWheelDisplay] Game paused (instant).");
            }
        }

        Debug.Log("[PrayerWheelDisplay] Wheels shown.");
    }

    /// <summary>
    /// Freezes the game completely. Call this when the spin completes and selection UI appears.
    /// </summary>
    public void FreezeCompletely()
    {
        if (useSmoothSlowdown && isInitialized)
        {
            Time.timeScale = 0f;
            Debug.Log("[PrayerWheelDisplay] Game frozen completely for selection.");
        }
    }

    /// <summary>
    /// Hides the prayer wheels.
    /// Resumes the game if it was paused.
    /// </summary>
    public void Hide()
    {
        if (!isVisible) return;

        isVisible = false;

        // Hide wheels
        SetWheelsVisible(false);

        // Resume game if we paused/slowed it (only if initialized to avoid startup issues)
        if (pauseGameWhenVisible && isInitialized)
        {
            if (useSmoothSlowdown)
            {
                // Smooth resume from frozen/slowed state
                StartCoroutine(SmoothTimeTransition(previousTimeScale, resumeDuration));
                Debug.Log($"[PrayerWheelDisplay] Smooth resume started (target: {previousTimeScale}, duration: {resumeDuration}s).");
            }
            else
            {
                // Legacy instant resume
                Time.timeScale = previousTimeScale;
                Debug.Log("[PrayerWheelDisplay] Game resumed (instant).");
            }
        }

        Debug.Log("[PrayerWheelDisplay] Wheels hidden.");
    }

    /// <summary>
    /// Updates the position of the wheels based on the camera transform and offset settings.
    /// Only updates position, does NOT touch rotation (so spin animation is preserved).
    /// </summary>
    public void UpdateWheelPositions()
    {
        if (cameraTransform == null)
        {
            Debug.LogWarning("[PrayerWheelDisplay] Cannot update positions - no camera transform assigned!");
            return;
        }

        // Calculate base position in front of camera
        Vector3 basePosition = cameraTransform.position + cameraTransform.forward * forwardOffset;
        
        // Add vertical offset (using world up, not camera up, for stability)
        basePosition += Vector3.up * verticalOffset;
        
        // Apply horizontal shift (moves both wheels together)
        basePosition += cameraTransform.right * horizontalShift;

        // Position wheel 1 (left side) - only position, NO rotation change
        if (prayerWheel1 != null)
        {
            prayerWheel1.position = basePosition - cameraTransform.right * wheel1HorizontalOffset;
        }

        // Position wheel 2 (right side) - only position, NO rotation change
        if (prayerWheel2 != null)
        {
            prayerWheel2.position = basePosition + cameraTransform.right * wheel2HorizontalOffset;
        }

        // Update base position to stay centered between wheels
        UpdateBasePosition();
    }

    private void UpdateBasePosition()
    {
        if (spawnedBase == null || cameraTransform == null) return;
        
        // Calculate anchor position in front of camera (same as wheels started)
        Vector3 anchorPos = cameraTransform.position + cameraTransform.forward * forwardOffset;
        
        // Apply Global Group Offsets (so base moves with the main controls)
        anchorPos += Vector3.up * verticalOffset;
        anchorPos += cameraTransform.right * horizontalShift;

        // Apply Base-Specific Offsets
        // X = Horizontal Offset for Base (New!)
        anchorPos += cameraTransform.right * basePositionOffset.x;
        // Y = Vertical Tweak for Base
        anchorPos += Vector3.up * basePositionOffset.y;
        // Z = Forward/Back Tweak for Base
        anchorPos += cameraTransform.forward * basePositionOffset.z;

        spawnedBase.transform.position = anchorPos;
        
        // Rotation: match camera's Y rotation (face same direction as camera), then apply offset
        // This keeps the base facing "forward" like the wheels appear to
        float cameraYRotation = cameraTransform.eulerAngles.y;
        spawnedBase.transform.rotation = Quaternion.Euler(
            baseRotationOffset.x,
            cameraYRotation + baseRotationOffset.y,
            baseRotationOffset.z
        );
    }

    /// <summary>
    /// Sets the offset values at runtime and updates positions if visible.
    /// </summary>
    public void SetOffset(float forward, float wheel1Horizontal, float wheel2Horizontal, float vertical)
    {
        forwardOffset = forward;
        wheel1HorizontalOffset = wheel1Horizontal;
        wheel2HorizontalOffset = wheel2Horizontal;
        verticalOffset = vertical;

        if (isVisible)
        {
            UpdateWheelPositions();
        }
    }

    /// <summary>
    /// Gets the current offset values.
    /// Returns (Wheel1Horizontal, Wheel2Horizontal, Vertical). Forward is accessed separately or ignored in this packed return.
    /// </summary>
    public Vector3 GetOffset()
    {
        return new Vector3(wheel1HorizontalOffset, wheel2HorizontalOffset, verticalOffset);
    }

    private void SetWheelsVisible(bool visible)
    {
        Debug.Log($"[PrayerWheelDisplay] SetWheelsVisible({visible})");
        
        if (prayerWheel1 != null)
        {
            prayerWheel1.gameObject.SetActive(visible);
            Debug.Log($"[PrayerWheelDisplay] Wheel1 SetActive({visible})");
        }
        else
        {
            Debug.LogWarning("[PrayerWheelDisplay] prayerWheel1 is null!");
        }

        if (prayerWheel2 != null)
        {
            prayerWheel2.gameObject.SetActive(visible);
            Debug.Log($"[PrayerWheelDisplay] Wheel2 SetActive({visible})");
        }
        else
        {
            Debug.LogWarning("[PrayerWheelDisplay] prayerWheel2 is null!");
        }

        // Spawn or destroy the base object
        if (visible)
        {
            SpawnBase();
        }
        else
        {
            DestroyBase();
        }
    }

    private void SpawnBase()
    {
        if (wheelBasePrefab == null) return;
        if (spawnedBase != null) return; // Already spawned
        
        // Spawn at origin first, UpdateBasePosition will place it correctly
        spawnedBase = Instantiate(wheelBasePrefab, Vector3.zero, Quaternion.identity);
        spawnedBase.name = "PrayerWheelBase(Spawned)";
        
        // Set to same layer as wheels
        SetLayerRecursive(spawnedBase, wheelLayer);
        
        // Position it correctly relative to camera
        UpdateBasePosition();
        
        Debug.Log($"[PrayerWheelDisplay] Spawned base at {spawnedBase.transform.position}");
    }

    private void DestroyBase()
    {
        if (spawnedBase != null)
        {
            Destroy(spawnedBase);
            spawnedBase = null;
            Debug.Log("[PrayerWheelDisplay] Destroyed base.");
        }
    }

    /// <summary>
    /// Assigns the prayer wheel transforms.
    /// </summary>
    public void SetWheelTransforms(Transform wheel1, Transform wheel2)
    {
        prayerWheel1 = wheel1;
        prayerWheel2 = wheel2;
        FixWheelLayers();
    }

    #region Debug Methods
    [ContextMenu("Debug: Show Wheels")]
    public void DebugShow() => Show();

    [ContextMenu("Debug: Hide Wheels")]
    public void DebugHide() => Hide();

    [ContextMenu("Debug: Toggle Visibility")]
    public void DebugToggle() => ToggleVisibility();

    [ContextMenu("Debug: Update Positions")]
    public void DebugUpdatePositions() => UpdateWheelPositions();

    [ContextMenu("Debug: Fix Layers")]
    public void DebugFixLayers() => FixWheelLayers();
    
    [ContextMenu("Debug: Force Show (Ignore State)")]
    public void DebugForceShow()
    {
        isVisible = false; // Reset state so Show() works
        Show();
    }
    #endregion

    #region Smooth Time Transition Fallback
    /// <summary>
    /// Fallback smooth time transition when TimeScaleManager is not available.
    /// Uses unscaledDeltaTime to work correctly during time manipulation.
    /// </summary>
    private System.Collections.IEnumerator SmoothTimeTransition(float targetTimeScale, float duration)
    {
        float startTimeScale = Time.timeScale;
        float elapsed = 0f;
        float originalFixedDelta = 0.02f; // Default fixed timestep

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // Cubic ease-out for natural feel
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            
            Time.timeScale = Mathf.Lerp(startTimeScale, targetTimeScale, easedT);
            Time.fixedDeltaTime = originalFixedDelta * Mathf.Max(Time.timeScale, 0.01f);
            
            yield return null;
        }

        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = originalFixedDelta * Mathf.Max(targetTimeScale, 0.01f);
    }
    #endregion
}
