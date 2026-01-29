using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using Audio;

/// <summary>
/// UI controller for displaying prayer wheel results and handling talent selection.
/// </summary>
public class PrayerWheelUI : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField, Tooltip("The main canvas containing these panels")]
    private Canvas mainCanvas;

    [SerializeField, Tooltip("Root panel that contains all prayer wheel UI")]
    private GameObject wheelPanel;
    
    [SerializeField, Tooltip("Panel shown during spinning")]
    private GameObject spinningPanel;
    
    [SerializeField, Tooltip("Panel shown after spin completes for selection")]
    private GameObject selectionPanel;

    [Header("Wheel 1 Display")]
    [SerializeField] private Image wheel1TalentIcon; // Keep for now if used elsewhere, but maybe redundant
    [SerializeField] private TextMeshProUGUI wheel1NameText; // Textbox above wheel
    
    [Header("Wheel 2 Display")]
    [SerializeField] private Image wheel2TalentIcon;
    [SerializeField] private TextMeshProUGUI wheel2NameText; // Textbox above wheel

    [Header("Selection Buttons (2 Total)")]
    [SerializeField, Tooltip("Left wheel selection button")]
    private Button buttonLeft;
    [SerializeField, Tooltip("Right wheel selection button")]
    private Button buttonRight;

    [Header("Talent Name Text Boxes")]
    [SerializeField, Tooltip("Text showing left wheel talent name (positioned above wheel)")]
    private TextMeshProUGUI talentNameLeft;
    [SerializeField, Tooltip("Text showing right wheel talent name (positioned above wheel)")]
    private TextMeshProUGUI talentNameRight;

    [Header("Rarity Indicators")]
    [SerializeField] private Image wheel1RarityGlow;
    [SerializeField] private Image wheel2RarityGlow;
    [SerializeField] private Color commonColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color rareColor = new Color(0.2f, 0.5f, 1f);
    [SerializeField] private Color legendaryColor = new Color(1f, 0.8f, 0.2f);

    [Header("Events")]
    public UnityEvent<TalentData> OnTalentSelected;

    [Header("Prayer Wheel Display")]
    [SerializeField, Tooltip("Reference to prayer wheel display controller (auto-found if null)")]
    private PrayerWheelDisplay prayerWheelDisplay;

    [Header("UI Position Offsets")]
    [SerializeField, Tooltip("Y offset for button positioning (positive = up)")]
    private float buttonYOffset = 100f;

    [Header("Other UI to Hide")]
    [SerializeField, Tooltip("Canvas to hide during prayer wheel (e.g., enemy health bars, damage numbers)")]
    private Canvas systemsCanvasToHide;

    [Header("Canvas Priority (Input Blocking)")]
    [SerializeField, Tooltip("Sort order to use when Prayer Wheel is active (higher = receives input first)")]
    private int activeSortOrder = 100;

    [Header("Sound Effects")]
    [SerializeField] private SoundEvent selectionSound;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    private TalentData currentTalent1;
    private TalentData currentTalent2;
    private PrayerWheelController wheelController;
    private bool isInSelectionMode = false; // Track if buttons need continuous positioning
    private int originalCanvasSortOrder = 0; // Store original sort order to restore later

    private void Awake()
    {
        // Setup button listeners
        if (buttonLeft) buttonLeft.onClick.AddListener(() => SelectTalent(currentTalent1));
        if (buttonRight) buttonRight.onClick.AddListener(() => SelectTalent(currentTalent2));

        // Find wheel controller
        wheelController = FindFirstObjectByType<PrayerWheelController>();
        if (wheelController != null)
        {
            wheelController.OnSpinComplete += OnSpinComplete;
        }

        // Find prayer wheel display if not assigned
        if (prayerWheelDisplay == null)
        {
            prayerWheelDisplay = FindFirstObjectByType<PrayerWheelDisplay>();
        }

        // Hide panels initially
        HideAll();
    }

    private void OnDestroy()
    {
        if (wheelController != null)
        {
            wheelController.OnSpinComplete -= OnSpinComplete;
        }
    }

    /// <summary>
    /// Continuously update button positions to track 3D wheel positions.
    /// This ensures buttons stay aligned during smooth slowdown when camera is moving.
    /// </summary>
    private void LateUpdate()
    {
        if (isInSelectionMode && wheelController != null)
        {
            UpdateButtonPositions();
        }
    }

    /// <summary>
    /// Shows the wheel UI with talents already placed on slots.
    /// Call Spin() separately to start the spinning animation.
    /// </summary>
    public void Show()
    {
        if (debugLog) Debug.Log("[PrayerWheelUI] Show() called!");
        
        if (wheelPanel != null) wheelPanel.SetActive(true);
        if (spinningPanel != null) spinningPanel.SetActive(true);
        if (selectionPanel != null) selectionPanel.SetActive(false);

        // Show 3D wheels (this also handles game pause)
        if (prayerWheelDisplay != null)
        {
            prayerWheelDisplay.Show();
        }
        else
        {
            if (debugLog) Debug.LogWarning("[PrayerWheelUI] prayerWheelDisplay is NULL!");
        }

        // Prepare talents on slots immediately so player sees them
        if (wheelController != null)
        {
            wheelController.PrepareTalents();
        }
        else
        {
            if (debugLog) Debug.LogWarning("[PrayerWheelUI] wheelController is NULL! Cannot prepare talents.");
        }

        // Hide systems canvas (enemy health bars, damage numbers, etc.)
        if (systemsCanvasToHide != null)
        {
            systemsCanvasToHide.gameObject.SetActive(false);
        }
        
        // Boost this canvas's sort order so it receives input priority over joystick
        // This allows player to still move (joystick works) but Prayer Wheel buttons take priority
        if (mainCanvas != null)
        {
            originalCanvasSortOrder = mainCanvas.sortingOrder;
            mainCanvas.sortingOrder = activeSortOrder;
        }

        if (debugLog) Debug.Log("[PrayerWheelUI] Wheels shown with talents. Waiting for spin trigger.");
    }

    /// <summary>
    /// Starts the spin animation. Call after Show().
    /// </summary>
    public void Spin()
    {
        if (wheelController != null)
        {
            wheelController.StartSpin();
        }
    }

    /// <summary>
    /// Shows the wheel UI and immediately starts spinning (legacy method).
    /// </summary>
    public void ShowAndSpin()
    {
        Show();
        Spin();
    }


    private void OnSpinComplete(TalentData talent1, TalentData talent2)
    {
        currentTalent1 = talent1;
        currentTalent2 = talent2;

        // Freeze the game completely now that spin is done and selection appears
        if (prayerWheelDisplay != null)
        {
            prayerWheelDisplay.FreezeCompletely();
        }

        // Ensure root panel is visible (in case spin was triggered via Debug)
        if (wheelPanel != null) wheelPanel.SetActive(true);

        // Switch to selection panel
        if (spinningPanel != null) spinningPanel.SetActive(false);
        if (selectionPanel != null) selectionPanel.SetActive(true);

        // Activate buttons (positions will be updated in LateUpdate)
        if (buttonLeft != null) buttonLeft.gameObject.SetActive(true);
        if (buttonRight != null) buttonRight.gameObject.SetActive(true);

        // Show Talent Name Text Boxes (positioned in Unity Editor, not dynamically)
        if (talentNameLeft != null)
        {
            talentNameLeft.gameObject.SetActive(true);
            talentNameLeft.text = talent1 != null ? talent1.talentName.Trim() : "";
        }

        if (talentNameRight != null)
        {
            talentNameRight.gameObject.SetActive(true);
            talentNameRight.text = talent2 != null ? talent2.talentName.Trim() : "";
        }

        // Update legacy Name Texts (if still used)
        if (wheel1NameText != null) wheel1NameText.text = talent1 != null ? talent1.talentName.Trim() : "";
        if (wheel2NameText != null) wheel2NameText.text = talent2 != null ? talent2.talentName.Trim() : "";

        // Enable continuous position updates
        isInSelectionMode = true;
        
        // Initial position update
        UpdateButtonPositions();
        
        if (debugLog) Debug.Log("[PrayerWheelUI] UI Updated and Elements Positioned");
    }

    /// <summary>
    /// Updates button positions to match the 3D wheel socket positions.
    /// Text stays in fixed canvas position (game is paused so no movement issues).
    /// </summary>
    private void UpdateButtonPositions()
    {
        if (wheelController == null) return;

        // Get winning positions from 3D world (for buttons)
        Vector3 worldPosLeft = wheelController.GetWinningSocketPosition(1);
        Vector3 worldPosRight = wheelController.GetWinningSocketPosition(2);
        
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCanvas != null)
        {
            Camera uiCam = (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : mainCanvas.worldCamera;
            
            // Get raw screen positions from camera
            Vector3 screenPosLeft = mainCam.WorldToScreenPoint(worldPosLeft);
            Vector3 screenPosRight = mainCam.WorldToScreenPoint(worldPosRight);
            
            // Fix: Adjust for camera viewport offset/scale (pillarboxing from ResolutionManager)
            // When viewport is full (Editor), this returns unchanged coordinates
            Rect viewport = mainCam.rect;
            screenPosLeft = ViewportToFullScreenPoint(screenPosLeft, viewport);
            screenPosRight = ViewportToFullScreenPoint(screenPosRight, viewport);
            
            // Position Left Button
            if (buttonLeft != null && buttonLeft.gameObject.activeSelf)
            {
                RectTransform btnRect = buttonLeft.GetComponent<RectTransform>();
                RectTransform parentRect = buttonLeft.transform.parent as RectTransform;

                if (parentRect != null && btnRect != null)
                {
                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosLeft, uiCam, out localPoint))
                    {
                        localPoint.y += buttonYOffset;
                        btnRect.localPosition = localPoint;
                    }
                }
            }

            // Position Right Button
            if (buttonRight != null && buttonRight.gameObject.activeSelf)
            {
                RectTransform btnRect = buttonRight.GetComponent<RectTransform>();
                RectTransform parentRect = buttonRight.transform.parent as RectTransform;
                
                if (parentRect != null && btnRect != null)
                {
                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosRight, uiCam, out localPoint))
                    {
                        localPoint.y += buttonYOffset;
                        btnRect.localPosition = localPoint;
                    }
                }
            }
            
            // Text stays in fixed canvas position (no dynamic tracking needed since game is paused)
        }
    }

    /// <summary>
    /// Converts a screen point from camera viewport space to full screen space.
    /// Required when camera viewport is not full screen (pillarboxing/letterboxing).
    /// When viewport is full (x=0, y=0, width=1, height=1), returns unchanged coordinates.
    /// </summary>
    private Vector3 ViewportToFullScreenPoint(Vector3 viewportScreenPoint, Rect cameraViewport)
    {
        // WorldToScreenPoint returns coordinates relative to the viewport, not full screen
        // We need to scale and offset to get actual screen coordinates
        return new Vector3(
            viewportScreenPoint.x * cameraViewport.width + cameraViewport.x * Screen.width,
            viewportScreenPoint.y * cameraViewport.height + cameraViewport.y * Screen.height,
            viewportScreenPoint.z
        );
    }

    private void SetButtonsActive(bool active)
    {
        if (buttonLeft) buttonLeft.gameObject.SetActive(active);
        if (buttonRight) buttonRight.gameObject.SetActive(active);
        if (talentNameLeft) talentNameLeft.gameObject.SetActive(active);
        if (talentNameRight) talentNameRight.gameObject.SetActive(active);
    }

    private void UpdateTalentDisplay(TalentData talent, Image icon, TextMeshProUGUI nameText, 
        TextMeshProUGUI descText, Image rarityGlow)
    {
        if (talent == null)
        {
            if (icon != null) icon.enabled = false;
            if (nameText != null) nameText.text = "Empty";
            if (descText != null) descText.text = "";
            return;
        }

        if (icon != null)
        {
            icon.sprite = talent.icon;
            icon.enabled = talent.icon != null;
        }

        if (nameText != null)
        {
            nameText.text = talent.talentName;
        }

        if (descText != null)
        {
            descText.text = talent.description;
        }

        if (rarityGlow != null)
        {
            rarityGlow.color = talent.rarity switch
            {
                TalentData.TalentRarity.Common => commonColor,
                TalentData.TalentRarity.Rare => rareColor,
                TalentData.TalentRarity.Legendary => legendaryColor,
                _ => commonColor
            };
        }
    }

    private void SelectTalent(TalentData talent)
    {
        if (talent == null)
        {
            Debug.LogWarning("[PrayerWheelUI] Attempted to select null talent!");
            return;
        }

        if (debugLog)
        {
            Debug.Log("========================================");
            Debug.Log($"[PrayerWheelUI] TALENT SELECTED: {talent.talentName}");
            Debug.Log($"[PrayerWheelUI] Rarity: {talent.rarity}");
            Debug.Log($"[PrayerWheelUI] Description: {talent.description}");
            Debug.Log("========================================");
        }

        // Play selection sound
        if (selectionSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(selectionSound);

        // Fire event
        OnTalentSelected?.Invoke(talent);

        // Hide UI (this also handles game resume via PrayerWheelDisplay.Hide())
        HideAll();
    }

    /// <summary>
    /// Hides all prayer wheel UI panels.
    /// </summary>
    public void HideAll()
    {
        // Stop continuous position updates
        isInSelectionMode = false;
        
        SetButtonsActive(false); // Explicitly hide separate buttons
        
        if (wheelPanel != null) wheelPanel.SetActive(false);
        if (spinningPanel != null) spinningPanel.SetActive(false);
        if (selectionPanel != null) selectionPanel.SetActive(false);

        // Hide 3D wheels (this also handles game resume)
        if (prayerWheelDisplay != null)
        {
            prayerWheelDisplay.Hide();
        }

        // Show systems canvas again
        if (systemsCanvasToHide != null)
        {
            systemsCanvasToHide.gameObject.SetActive(true);
        }
        
        // Restore original canvas sort order
        if (mainCanvas != null)
        {
            mainCanvas.sortingOrder = originalCanvasSortOrder;
        }
    }

    /// <summary>
    /// Cancels selection and resumes game (e.g., if player dies).
    /// </summary>
    public void CancelSelection()
    {
        HideAll();
        
        // Use smooth resume if TimeScaleManager is available
        if (TimeScaleManager.Instance != null && TimeScaleManager.Instance.IsSlowed)
        {
            TimeScaleManager.Instance.SmoothResume();
        }
        else
        {
            // Fallback to instant resume
            Time.timeScale = 1f;
        }
    }

    #region Debug Methods
    [ContextMenu("Debug: Show (With Talents)")]
    public void DebugShow() => Show();

    [ContextMenu("Debug: Spin")]
    public void DebugSpin() => Spin();

    [ContextMenu("Debug: Show And Spin")]
    public void DebugShowAndSpin() => ShowAndSpin();

    [ContextMenu("Debug: Hide All")]
    public void DebugHideAll() => HideAll();
    #endregion
}
