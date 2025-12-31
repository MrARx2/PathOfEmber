using UnityEngine;
using UnityEngine.Rendering.Universal;
using System;
using System.Reflection;
using System.Linq;

/// <summary>
/// Sets up a dedicated camera for rendering the 3D prayer wheels.
/// Automatically positions wheels in camera view and handles show/hide.
/// </summary>
public class PrayerWheelCameraSetup : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField, Tooltip("Existing UI camera to use (if null, one will be created)")]
    private Camera wheelCamera;
    
    [SerializeField, Tooltip("Layer for prayer wheel objects")]
    private string wheelLayerName = "PrayerWheelUI";
    
    [SerializeField, Tooltip("Camera depth (higher renders on top)")]
    private int cameraDepth = 10;
    
    [SerializeField, Tooltip("Camera distance from wheels")]
    private float cameraDistance = 5f;
    
    [SerializeField, Tooltip("Field of view for wheel camera")]
    private float fieldOfView = 40f;

    [Header("Wheel Positioning")]
    [SerializeField, Tooltip("If true, script will force position and rotation. If false, it respects your manual placement.")]
    private bool autoPositionWheels = true;

    [SerializeField, Tooltip("Reference to Prayer Wheel 1 root transform")]
    private Transform prayerWheel1;
    
    [SerializeField, Tooltip("Reference to Prayer Wheel 2 root transform")]
    private Transform prayerWheel2;
    
    [SerializeField, Tooltip("Horizontal offset from center for each wheel")]
    private float wheelHorizontalOffset = 1.5f;
    
    [SerializeField, Tooltip("Vertical offset (positive = up)")]
    private float wheelVerticalOffset = 0f;
    
    [SerializeField, Tooltip("Distance in front of camera")]
    private float wheelForwardDistance = 3f;

    [SerializeField, Tooltip("Default rotation for the wheels (e.g. 0, 180, 0 if they face backwards)")]
    private Vector3 wheelDefaultRotation = Vector3.zero;

    [Header("Animation")]
    [SerializeField, Tooltip("Duration for fade in/out")]
    private float fadeDuration = 0.3f;

    private Camera mainCamera;
    private GameObject wheelCameraObject;
    private CanvasGroup fadeCanvasGroup; // Optional for screen fade
    private bool isVisible = false;
    private int wheelLayer;

    // Public getter for UI conversion
    public Camera WheelCamera => wheelCamera;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        mainCamera = Camera.main;
        SetupWheelLayer();
        SetupCamera();
        SetupWheelPositions();
        
        // Start hidden
        SetWheelsActive(false);
    }

    private void SetupWheelLayer()
    {
        // Try to get the layer, create suggestion if it doesn't exist
        wheelLayer = LayerMask.NameToLayer(wheelLayerName);
        
        if (wheelLayer == -1)
        {
            Debug.LogWarning($"[PrayerWheelCameraSetup] Layer '{wheelLayerName}' not found! " +
                           $"Please create it in Edit > Project Settings > Tags and Layers. " +
                           $"Using Default layer for now.");
            wheelLayer = 0; // Default layer
        }
    }

    private void SetupCamera()
    {
        if (wheelCamera == null)
        {
            // Create a dedicated camera for the wheels
            wheelCameraObject = new GameObject("PrayerWheelCamera");
            wheelCameraObject.transform.SetParent(transform);
            
            wheelCamera = wheelCameraObject.AddComponent<Camera>();
            wheelCamera.clearFlags = CameraClearFlags.Depth;
            // Render "Wheel Layer" AND the "UI" Layer (Layer 5) so it sees the canvas
            wheelCamera.cullingMask = (1 << wheelLayer) | (1 << LayerMask.NameToLayer("UI"));
            wheelCamera.depth = cameraDepth; // Render after main camera
            wheelCamera.fieldOfView = fieldOfView;
            wheelCamera.nearClipPlane = 0.1f;
            wheelCamera.farClipPlane = 100f;
            
            // Position camera to look at the wheel area
            wheelCameraObject.transform.localPosition = new Vector3(0, 0, -cameraDistance);
            wheelCameraObject.transform.localRotation = Quaternion.identity;
            
            Debug.Log("[PrayerWheelCameraSetup] Created dedicated wheel camera");
        }
        else
        {
            // Configure existing camera
            wheelCamera.clearFlags = CameraClearFlags.Depth;
            wheelCamera.cullingMask = 1 << wheelLayer;
            wheelCamera.depth = cameraDepth;
        }

        // Exclude wheel layer from main camera
        if (mainCamera != null && wheelLayer != 0)
        {
            mainCamera.cullingMask &= ~(1 << wheelLayer);
        }
    }

    private void SetupWheelPositions()
    {
        if (prayerWheel1 != null)
        {
            // Parent to this object so they move with the camera setup
            if (prayerWheel1.parent != transform)
                prayerWheel1.SetParent(transform);

            if (autoPositionWheels)
            {
                prayerWheel1.localPosition = new Vector3(
                    -wheelHorizontalOffset, 
                    wheelVerticalOffset, 
                    wheelForwardDistance
                );
                prayerWheel1.localRotation = Quaternion.Euler(wheelDefaultRotation);
            }
            
            // Set layer recursively (Always do this!)
            SetLayerRecursive(prayerWheel1.gameObject, wheelLayer);
            
            if (autoPositionWheels)
                Debug.Log($"[PrayerWheelCameraSetup] Auto-Positioned wheel 1 at {prayerWheel1.localPosition}");
        }

        if (prayerWheel2 != null)
        {
            if (prayerWheel2.parent != transform)
                prayerWheel2.SetParent(transform);

            if (autoPositionWheels)
            {
                prayerWheel2.localPosition = new Vector3(
                    wheelHorizontalOffset, 
                    wheelVerticalOffset, 
                    wheelForwardDistance
                );
                prayerWheel2.localRotation = Quaternion.Euler(wheelDefaultRotation);
            }
            
            SetLayerRecursive(prayerWheel2.gameObject, wheelLayer);
            
            if (autoPositionWheels)
                Debug.Log($"[PrayerWheelCameraSetup] Auto-Positioned wheel 2 at {prayerWheel2.localPosition}");
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
    /// Shows the prayer wheels with optional animation.
    /// </summary>
    public void ShowWheels()
    {
        if (isVisible) return;
        
        isVisible = true;
        SetWheelsActive(true);
        
        if (wheelCamera != null)
        {
            wheelCamera.enabled = true;
        }

        // Reset wheel rotations for fresh spin
        ResetWheelRotations();
        
        // Try URP Setup
        SetupURP();

        Debug.Log("[PrayerWheelCameraSetup] Wheels shown");
    }

    /// <summary>
    /// Hides the prayer wheels.
    /// </summary>
    public void HideWheels()
    {
        if (!isVisible) return;
        
        isVisible = false;
        SetWheelsActive(false);
        
        if (wheelCamera != null)
        {
            wheelCamera.enabled = false;
        }
        
        CleanupURP();

        Debug.Log("[PrayerWheelCameraSetup] Wheels hidden");
    }

    private void SetWheelsActive(bool active)
    {
        if (prayerWheel1 != null)
        {
            prayerWheel1.gameObject.SetActive(active);
        }
        if (prayerWheel2 != null)
        {
            prayerWheel2.gameObject.SetActive(active);
        }
        if (wheelCamera != null)
        {
            wheelCamera.enabled = active;
        }
    }

    private void ResetWheelRotations()
    {
        // If we aren't auto-positioning, we shouldn't auto-reset children either.
        // This preserves manual prefab rotations.
        if (!autoPositionWheels) return;

        if (prayerWheel1 != null)
        {
            // Reset each floor's rotation if you want fresh starts
            foreach (Transform child in prayerWheel1)
            {
                child.localRotation = Quaternion.identity;
            }
        }
        if (prayerWheel2 != null)
        {
            foreach (Transform child in prayerWheel2)
            {
                child.localRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// Adjusts wheel positions at runtime.
    /// </summary>
    public void SetWheelOffset(float horizontal, float vertical, float forward)
    {
        wheelHorizontalOffset = horizontal;
        wheelVerticalOffset = vertical;
        wheelForwardDistance = forward;
        
        if (prayerWheel1 != null)
        {
            prayerWheel1.localPosition = new Vector3(-horizontal, vertical, forward);
        }
        if (prayerWheel2 != null)
        {
            prayerWheel2.localPosition = new Vector3(horizontal, vertical, forward);
        }
    }

    /// <summary>
    /// Gets the camera used for rendering wheels.
    /// </summary>
    public Camera GetWheelCamera() => wheelCamera;

    private void SetupURP()
    {
        if (mainCamera == null || wheelCamera == null) return;

        var mainCamData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        var wheelCamData = wheelCamera.GetComponent<UniversalAdditionalCameraData>();

        if (mainCamData != null && wheelCamData != null)
        {
            // 1. Set Wheel Camera to Overlay
            wheelCamData.renderType = CameraRenderType.Overlay;
            
            // 2. Add to Main Camera Stack
            if (!mainCamData.cameraStack.Contains(wheelCamera))
            {
                mainCamData.cameraStack.Add(wheelCamera);
                Debug.Log("[PrayerWheelCameraSetup] Native URP: Added wheel camera to stack.");
            }
        }
    }

    private void CleanupURP()
    {
        if (mainCamera == null || wheelCamera == null) return;
        
        var mainCamData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (mainCamData != null)
        {
            if (mainCamData.cameraStack.Contains(wheelCamera))
            {
                mainCamData.cameraStack.Remove(wheelCamera);
            }
        }
    }

    #region Debug
    [ContextMenu("Debug: Show Wheels")]
    public void DebugShowWheels() => ShowWheels();

    [ContextMenu("Debug: Hide Wheels")]
    public void DebugHideWheels() => HideWheels();

    [ContextMenu("Debug: Toggle Wheels")]
    public void DebugToggleWheels()
    {
        if (isVisible) HideWheels();
        else ShowWheels();
    }
    #endregion
}
