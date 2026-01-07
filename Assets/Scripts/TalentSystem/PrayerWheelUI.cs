using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

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

    private TalentData currentTalent1;
    private TalentData currentTalent2;
    private PrayerWheelController wheelController;

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
    /// Shows the wheel UI with talents already placed on slots.
    /// Call Spin() separately to start the spinning animation.
    /// </summary>
    public void Show()
    {
        Debug.Log("[PrayerWheelUI] Show() called!");
        
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
            Debug.LogWarning("[PrayerWheelUI] prayerWheelDisplay is NULL!");
        }

        // Prepare talents on slots immediately so player sees them
        if (wheelController != null)
        {
            wheelController.PrepareTalents();
        }
        else
        {
            Debug.LogWarning("[PrayerWheelUI] wheelController is NULL! Cannot prepare talents.");
        }

        // Hide systems canvas (enemy health bars, damage numbers, etc.)
        if (systemsCanvasToHide != null)
        {
            systemsCanvasToHide.gameObject.SetActive(false);
        }

        Debug.Log("[PrayerWheelUI] Wheels shown with talents. Waiting for spin trigger.");
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

        // Ensure root panel is visible (in case spin was triggered via Debug)
        if (wheelPanel != null) wheelPanel.SetActive(true);

        // Switch to selection panel
        if (spinningPanel != null) spinningPanel.SetActive(false);
        if (selectionPanel != null) selectionPanel.SetActive(true);

        // Hide all selection buttons first
        SetButtonsActive(false);

        // Get winning positions from 3D world
        Vector3 worldPosLeft = wheelController.GetWinningSocketPosition(1);
        Vector3 worldPosRight = wheelController.GetWinningSocketPosition(2);
        
        // Convert to Screen Space
        // Note: Assuming buttons are children of a Screen Space - Overlay canvas or similar.
        // If Camera is null, we can't position dynamically.
        // For button positioning, use main camera since we're no longer using a separate wheel camera
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCanvas != null)
        {
            Camera uiCam = (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : mainCanvas.worldCamera;
            
            Vector3 screenPosLeft = mainCam.WorldToScreenPoint(worldPosLeft);
            Vector3 screenPosRight = mainCam.WorldToScreenPoint(worldPosRight);
            
            // Position and Activate Left Button
            if (buttonLeft != null)
            {
                RectTransform btnRect = buttonLeft.GetComponent<RectTransform>();
                RectTransform parentRect = buttonLeft.transform.parent as RectTransform;

                if (parentRect != null && btnRect != null)
                {
                    buttonLeft.gameObject.SetActive(true);
                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosLeft, uiCam, out localPoint))
                    {
                        // Apply Y offset
                        localPoint.y += buttonYOffset;
                        btnRect.localPosition = localPoint;
                    }
                }
            }

            // Position and Activate Right Button
            if (buttonRight != null)
            {
                RectTransform btnRect = buttonRight.GetComponent<RectTransform>();
                RectTransform parentRect = buttonRight.transform.parent as RectTransform;
                
                if (parentRect != null && btnRect != null)
                {
                    buttonRight.gameObject.SetActive(true);
                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosRight, uiCam, out localPoint))
                    {
                        // Apply Y offset
                        localPoint.y += buttonYOffset;
                        btnRect.localPosition = localPoint;
                    }
                }
            }

            // Show Talent Name Text Boxes (fixed positions - just update text)
            if (talentNameLeft != null)
            {
                talentNameLeft.gameObject.SetActive(true);
                talentNameLeft.text = talent1 != null ? talent1.talentName : "";
            }

            if (talentNameRight != null)
            {
                talentNameRight.gameObject.SetActive(true);
                talentNameRight.text = talent2 != null ? talent2.talentName : "";
            }
        }
        else
        {
             Debug.LogWarning("[PrayerWheelUI] Canvas or Camera missing, cannot position UI elements.");
        }

        // Update legacy Name Texts (if still used)
        if (wheel1NameText != null) wheel1NameText.text = talent1 != null ? talent1.talentName : "";
        if (wheel2NameText != null) wheel2NameText.text = talent2 != null ? talent2.talentName : "";
        
        Debug.Log("[PrayerWheelUI] UI Updated and Elements Positioned");
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

        Debug.Log("========================================");
        Debug.Log($"[PrayerWheelUI] TALENT SELECTED: {talent.talentName}");
        Debug.Log($"[PrayerWheelUI] Rarity: {talent.rarity}");
        Debug.Log($"[PrayerWheelUI] Description: {talent.description}");
        Debug.Log("========================================");

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
    }

    /// <summary>
    /// Cancels selection and resumes game (e.g., if player dies).
    /// </summary>
    public void CancelSelection()
    {
        HideAll();
        Time.timeScale = 1f;
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
