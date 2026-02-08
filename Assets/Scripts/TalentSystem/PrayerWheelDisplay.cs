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
    
    [Header("Mobile Aspect Ratio Compensation")]
    [SerializeField, Tooltip("Enable automatic vertical adjustment for taller screens (mobile)")]
    private bool compensateForAspectRatio = true;
    
    [SerializeField, Tooltip("Reference aspect ratio (width/height). 1080/1920 = 0.5625 for portrait")]
    private float referenceAspectRatio = 0.5625f;
    
    [SerializeField, Tooltip("Vertical offset per unit of aspect ratio difference (positive = move down on taller screens)")]
    private float aspectRatioVerticalMultiplier = 2.0f;
    
    [SerializeField, Tooltip("Z (forward) offset per unit of aspect ratio difference (positive = move closer on taller screens)")]
    private float aspectRatioZMultiplier = 1.0f;

    [Header("Smoothing (Jitter Fix)")]
    [SerializeField, Tooltip("Smooth time for position following (0.05 = tight, 0.2 = loose). Uses unscaled time to fix slow-mo jitter.")]
    private float positionSmoothTime = 0.05f;
    
    [SerializeField, Tooltip("Smooth time for base rotation (0.1 = smooth, 0.05 = snappy).")]
    private float rotationSmoothTime = 0.1f;

    [SerializeField, Tooltip("Distance threshold to snap immediately (e.g. on teleport).")]
    private float snapDistanceThreshold = 10f;

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

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;
    
    // Smoothing velocities
    private Vector3 wheel1Velocity;
    private Vector3 wheel2Velocity;
    private Vector3 baseVelocity;
    private float baseRotationVelocity; // For SmoothDampAngle
    
    // Cached center position for UI
    private Vector3 lastCalculatedBasePosition;

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
        // Auto-configure Aspect Ratio Compensation based on active Resolution Manager
        // If MobileResolutionManager is active, we need compensation because it likely scales FOV while keeping width constant.
        // If PC ResolutionManager is active, it likely enforces a fixed viewport (pillarbox), so we don't need logic to compensate.
        
        var mobileResManager = FindFirstObjectByType<MobileResolutionManager>();
        var pcResManager = FindFirstObjectByType<ResolutionManager>();
        
        if (mobileResManager != null && mobileResManager.enabled)
        {
            compensateForAspectRatio = true;
            if (debugLog) Debug.Log("[PrayerWheelDisplay] Auto-Enabling Aspect Ratio Compensation (MobileResolutionManager detected).");
        }
        else if (pcResManager != null && pcResManager.enabled)
        {
            compensateForAspectRatio = false;
            if (debugLog) Debug.Log("[PrayerWheelDisplay] Auto-Disabling Aspect Ratio Compensation (ResolutionManager detected - fixed viewport).");
        }

        // Try to find main camera if not assigned
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
                if (debugLog) Debug.Log("[PrayerWheelDisplay] Auto-assigned main camera transform.");
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
        
        // Pre-warm the base object (Spawn it, then hide it) so the first spin doesn't lag
        if (wheelBasePrefab != null)
        {
            SpawnBase();  // Instantiates it
            DestroyBase(); // Hides it (SetActive false)
        }
        
        if (debugLog) Debug.Log($"[PrayerWheelDisplay] Initialized. Wheel1: {(prayerWheel1 != null ? prayerWheel1.name : "NULL")}, Wheel2: {(prayerWheel2 != null ? prayerWheel2.name : "NULL")}");
    }

    private void OnDestroy()
    {
        if (spawnedBase != null)
        {
            Destroy(spawnedBase);
        }
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
            if (debugLog) Debug.Log($"[PrayerWheelDisplay] Set wheel 1 to layer {wheelLayer}.");
        }

        if (prayerWheel2 != null)
        {
            SetLayerRecursive(prayerWheel2.gameObject, wheelLayer);
            if (debugLog) Debug.Log($"[PrayerWheelDisplay] Set wheel 2 to layer {wheelLayer}.");
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
        
        if (debugLog) Debug.Log("[PrayerWheelDisplay] Show() called.");

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
                if (debugLog) Debug.Log($"[PrayerWheelDisplay] Smooth slowdown started (target: {slowdownTargetTimeScale}, duration: {slowdownDuration}s).");
            }
            else
            {
                // Legacy instant freeze
                Time.timeScale = 0f;
                if (debugLog) Debug.Log("[PrayerWheelDisplay] Game paused (instant).");
            }
        }

        if (debugLog) Debug.Log("[PrayerWheelDisplay] Wheels shown.");
    }

    /// <summary>
    /// Freezes the game completely. Call this when the spin completes and selection UI appears.
    /// </summary>
    public void FreezeCompletely()
    {
        if (useSmoothSlowdown && isInitialized)
        {
            Time.timeScale = 0f;
            if (debugLog) Debug.Log("[PrayerWheelDisplay] Game frozen completely for selection.");
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
                // Check if game is externally paused (Time.timeScale == 0) before trying to resume.
                // If paused, we just set the target, but don't force the transition yet, 
                // or we rely on the Menu Controller to restore time when IT closes.
                // However, if we just finished the Wheel, we probably want to return to 1.0 eventually.
                
                // Safe Resume: Only resume if we are not totally paused by something else (like Menu)
                // BUT: We just came from a frozen state (Show->Freeze), so Time relies on us.
                // The issue is if Menu opened on TOP of us.
                
                if (Time.timeScale > 0.001f || !useSmoothSlowdown)
                {
                    StartCoroutine(SmoothTimeTransition(previousTimeScale, resumeDuration));
                }
                else
                {
                    // If time is 0 (Paused by Menu?), just set the scale instantly to what it should be
                    // so when Menu unpauses, it goes back to normal.
                    Time.timeScale = previousTimeScale;
                }
                if (debugLog) Debug.Log($"[PrayerWheelDisplay] Smooth resume started (target: {previousTimeScale}, duration: {resumeDuration}s).");
            }
            else
            {
                // Legacy instant resume
                Time.timeScale = previousTimeScale;
                if (debugLog) Debug.Log("[PrayerWheelDisplay] Game resumed (instant).");
            }
        }

        if (debugLog) Debug.Log("[PrayerWheelDisplay] Wheels hidden.");
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
        float adjustedForwardOffset = forwardOffset;
        float totalVerticalOffset = verticalOffset;
        
        // Compensate for different aspect ratios (mobile screens are taller)
        if (compensateForAspectRatio)
        {
            float currentAspect = (float)Screen.width / Screen.height;
            float aspectDifference = currentAspect - referenceAspectRatio;
            // Positive multiplier moves down when aspect is smaller (taller screen)
            totalVerticalOffset += aspectDifference * aspectRatioVerticalMultiplier;
            // Positive Z multiplier moves closer (reduce forward offset) on taller screens
            adjustedForwardOffset += aspectDifference * aspectRatioZMultiplier;
        }
        
        Vector3 basePosition = cameraTransform.position + cameraTransform.forward * adjustedForwardOffset;
        basePosition += Vector3.up * totalVerticalOffset;
        
        // Apply horizontal shift (moves both wheels together)
        basePosition += cameraTransform.right * horizontalShift;

        // Update base position to stay centered between wheels
        // Note: UpdateBasePosition handles its own smoothing now
        UpdateBasePosition(basePosition);
        
        // Cache for UI access
        lastCalculatedBasePosition = basePosition;

        // --- Smooth Follow Implementation ---
        // We use SmoothDamp with Time.unscaledDeltaTime to ensure smooth movement 
        // even when the game is in slow motion (Time.timeScale < 1).
        
        // Position wheel 1 (left side)
        if (prayerWheel1 != null)
        {
            Vector3 targetPos1 = basePosition - cameraTransform.right * wheel1HorizontalOffset;
            
            // Ref check: if distance is huge (teleport), snap immediately
            if (Vector3.Distance(prayerWheel1.position, targetPos1) > snapDistanceThreshold)
            {
                prayerWheel1.position = targetPos1;
                wheel1Velocity = Vector3.zero;
            }
            else
            {
                prayerWheel1.position = Vector3.SmoothDamp(
                    prayerWheel1.position, 
                    targetPos1, 
                    ref wheel1Velocity, 
                    positionSmoothTime, 
                    float.MaxValue, 
                    Time.unscaledDeltaTime
                );
            }
        }

        // Position wheel 2 (right side)
        if (prayerWheel2 != null)
        {
            Vector3 targetPos2 = basePosition + cameraTransform.right * wheel2HorizontalOffset;
             
            if (Vector3.Distance(prayerWheel2.position, targetPos2) > snapDistanceThreshold)
            {
                prayerWheel2.position = targetPos2;
                wheel2Velocity = Vector3.zero;
            }
            else
            {
                prayerWheel2.position = Vector3.SmoothDamp(
                    prayerWheel2.position, 
                    targetPos2, 
                    ref wheel2Velocity, 
                    positionSmoothTime, 
                    float.MaxValue, 
                    Time.unscaledDeltaTime
                );
            }
        }
    }

    private void UpdateBasePosition(Vector3 wheelCenterAnchor)
    {
        if (spawnedBase == null || cameraTransform == null) return;
        
        // Use the passed anchor (which is centered between wheels + offsets)
        // Add Base-Specific Offsets
        Vector3 targetPos = wheelCenterAnchor;
        
        // X = Horizontal Offset for Base
        targetPos += cameraTransform.right * basePositionOffset.x;
        // Y = Vertical Tweak for Base
        targetPos += Vector3.up * basePositionOffset.y;
        // Z = Forward/Back Tweak for Base
        targetPos += cameraTransform.forward * basePositionOffset.z;

        // Smooth Move Base
        if (Vector3.Distance(spawnedBase.transform.position, targetPos) > snapDistanceThreshold)
        {
            spawnedBase.transform.position = targetPos;
            baseVelocity = Vector3.zero;
        }
        else
        {
            spawnedBase.transform.position = Vector3.SmoothDamp(
                spawnedBase.transform.position,
                targetPos,
                ref baseVelocity,
                positionSmoothTime,
                float.MaxValue,
                Time.unscaledDeltaTime
            );
        }
        
        // Rotation: match camera's Y rotation (face same direction as camera), then apply offset
        // We smooth this too because if the camera is hard-locked to the player physics, it will also jitter in slow-mo.
        float currentY = spawnedBase.transform.eulerAngles.y;
        float targetY = cameraTransform.eulerAngles.y + baseRotationOffset.y;
        
        float smoothedY = Mathf.SmoothDampAngle(
            currentY,
            targetY,
            ref baseRotationVelocity,
            rotationSmoothTime,
            float.MaxValue,
            Time.unscaledDeltaTime
        );

        spawnedBase.transform.rotation = Quaternion.Euler(
            baseRotationOffset.x,
            smoothedY,
            baseRotationOffset.z
        );
    }

    [System.Obsolete("Use UpdateBasePosition(Vector3) instead")]
    private void UpdateBasePosition() { } // Legacy stub to satisfy compiler if needed, but we removed call site

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
        if (debugLog) Debug.Log($"[PrayerWheelDisplay] SetWheelsVisible({visible})");
        
        if (prayerWheel1 != null)
        {
            prayerWheel1.gameObject.SetActive(visible);
            if (debugLog) Debug.Log($"[PrayerWheelDisplay] Wheel1 SetActive({visible})");
        }
        else
        {
            Debug.LogWarning("[PrayerWheelDisplay] prayerWheel1 is null!");
        }

        if (prayerWheel2 != null)
        {
            prayerWheel2.gameObject.SetActive(visible);
            if (debugLog) Debug.Log($"[PrayerWheelDisplay] Wheel2 SetActive({visible})");
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
        
        // REUSE: If already exists, just enable it and move it to start position
        if (spawnedBase != null) 
        {
            spawnedBase.SetActive(true);
            
            // Re-snap to position (same logic as below)
            if (cameraTransform != null)
            {
                // FORCE SNAP to current camera view so we don't see it fly in from old position
                MoveBaseToCameraTarget(true);
            }
            return; 
        }
        
        // Spawn at origin first
        spawnedBase = Instantiate(wheelBasePrefab, Vector3.zero, Quaternion.identity);
        spawnedBase.name = "PrayerWheelBase(Spawned)";
        
        // Set to same layer as wheels
        SetLayerRecursive(spawnedBase, wheelLayer);
        
        if (cameraTransform != null)
        {
           MoveBaseToCameraTarget(true);
        }
        
        if (debugLog) Debug.Log($"[PrayerWheelDisplay] Spawned base at {spawnedBase.transform.position}");
    }

    private void MoveBaseToCameraTarget(bool hardSnap)
    {
        if (cameraTransform == null || spawnedBase == null) return;

        Vector3 targetPos = cameraTransform.position + cameraTransform.forward * forwardOffset;
        targetPos += Vector3.up * verticalOffset;
        targetPos += cameraTransform.right * horizontalShift;
        
        // Add Base-Specific Offsets
        targetPos += cameraTransform.right * basePositionOffset.x;
        targetPos += Vector3.up * basePositionOffset.y;
        targetPos += cameraTransform.forward * basePositionOffset.z;
        
        // HARD SNAP Position
        spawnedBase.transform.position = targetPos;
        
        // HARD SNAP Rotation
        float cameraYRotation = cameraTransform.eulerAngles.y;
        spawnedBase.transform.rotation = Quaternion.Euler(
            baseRotationOffset.x,
            cameraYRotation + baseRotationOffset.y,
            baseRotationOffset.z
        );
        
        if (hardSnap)
        {
            // RESET Velocities so SmoothDamp doesn't try to continue from a previous state or 0
            baseVelocity = Vector3.zero;
            baseRotationVelocity = 0f;
        }
    }

    private void DestroyBase()
    {
        // REUSE: Just disable instead of destroying
        if (spawnedBase != null)
        {
            spawnedBase.SetActive(false);
            if (debugLog) Debug.Log("[PrayerWheelDisplay] Disabled base.");
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

    /// <summary>
    /// Returns the calculated center position between the two wheels (world space).
    /// Used by PrayerWheelUI for stable text positioning regardless of wheel rotation.
    /// </summary>
    public Vector3 GetWheelCenterPosition()
    {
        // If position hasn't been calculated yet, return default logic
        if (lastCalculatedBasePosition == Vector3.zero && cameraTransform != null)
        {
             Vector3 basePos = cameraTransform.position + cameraTransform.forward * forwardOffset;
             basePos += Vector3.up * verticalOffset;
             return basePos;
        }
        return lastCalculatedBasePosition;
    }
}
