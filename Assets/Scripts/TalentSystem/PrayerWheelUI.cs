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

    [Header("Selection Buttons (6 Total)")]
    // Common
    [SerializeField] private Button commonButtonLeft;
    [SerializeField] private Button commonButtonRight;
    // Rare
    [SerializeField] private Button rareButtonLeft;
    [SerializeField] private Button rareButtonRight;
    // Legendary
    [SerializeField] private Button legendaryButtonLeft;
    [SerializeField] private Button legendaryButtonRight;

    [Header("Rarity Indicators")]
    [SerializeField] private Image wheel1RarityGlow;
    [SerializeField] private Image wheel2RarityGlow;
    [SerializeField] private Color commonColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color rareColor = new Color(0.2f, 0.5f, 1f);
    [SerializeField] private Color legendaryColor = new Color(1f, 0.8f, 0.2f);

    [Header("Events")]
    public UnityEvent<TalentData> OnTalentSelected;

    [Header("3D Wheel Camera")]
    [SerializeField, Tooltip("Reference to camera setup for 3D wheels (auto-found if null)")]
    private PrayerWheelCameraSetup cameraSetup;

    private TalentData currentTalent1;
    private TalentData currentTalent2;
    private PrayerWheelController wheelController;

    private void Awake()
    {
        // Setup button listeners
        if (commonButtonLeft) commonButtonLeft.onClick.AddListener(() => SelectTalent(currentTalent1));
        if (commonButtonRight) commonButtonRight.onClick.AddListener(() => SelectTalent(currentTalent2));
        
        if (rareButtonLeft) rareButtonLeft.onClick.AddListener(() => SelectTalent(currentTalent1));
        if (rareButtonRight) rareButtonRight.onClick.AddListener(() => SelectTalent(currentTalent2));

        if (legendaryButtonLeft) legendaryButtonLeft.onClick.AddListener(() => SelectTalent(currentTalent1));
        if (legendaryButtonRight) legendaryButtonRight.onClick.AddListener(() => SelectTalent(currentTalent2));

        // Find wheel controller
        wheelController = FindObjectOfType<PrayerWheelController>();
        if (wheelController != null)
        {
            wheelController.OnSpinComplete += OnSpinComplete;
        }

        // Find camera setup if not assigned
        if (cameraSetup == null)
        {
            cameraSetup = FindObjectOfType<PrayerWheelCameraSetup>();
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
    /// Shows the wheel UI and starts spinning.
    /// </summary>
    public void ShowAndSpin()
    {
        if (wheelPanel != null) wheelPanel.SetActive(true);
        if (spinningPanel != null) spinningPanel.SetActive(true);
        if (selectionPanel != null) selectionPanel.SetActive(false);

        // Show 3D wheels
        if (cameraSetup != null)
        {
            cameraSetup.ShowWheels();
        }

        // Pause game
        Time.timeScale = 0f;

        // Start the spin
        if (wheelController != null)
        {
            wheelController.StartSpin();
        }
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
        if (cameraSetup != null && cameraSetup.WheelCamera != null && mainCanvas != null)
        {
            Camera cam = cameraSetup.WheelCamera;
            Camera uiCam = (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : mainCanvas.worldCamera;
            
            Vector3 screenPosLeft = cam.WorldToScreenPoint(worldPosLeft);
            Vector3 screenPosRight = cam.WorldToScreenPoint(worldPosRight);
            
            // Check rarity to activate correct buttons
            TalentData.TalentRarity rarity = talent1 != null ? talent1.rarity : TalentData.TalentRarity.Common;
            
            Button btnLeft = null;
            Button btnRight = null;

            switch (rarity)
            {
                case TalentData.TalentRarity.Common:
                    btnLeft = commonButtonLeft; btnRight = commonButtonRight;
                    break;
                case TalentData.TalentRarity.Rare:
                    btnLeft = rareButtonLeft; btnRight = rareButtonRight;
                    break;
                case TalentData.TalentRarity.Legendary:
                    btnLeft = legendaryButtonLeft; btnRight = legendaryButtonRight;
                    break;
            }

            // Position and Activate Left
            if (btnLeft != null)
            {
                RectTransform btnRect = btnLeft.GetComponent<RectTransform>();
                RectTransform parentRect = btnLeft.transform.parent as RectTransform;

                if (parentRect != null && btnRect != null)
                {
                    btnLeft.gameObject.SetActive(true);
                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosLeft, uiCam, out localPoint))
                    {
                        btnRect.localPosition = localPoint;
                        Debug.Log($"[PrayerWheelUI] BtnLeft World: {worldPosLeft}, Screen: {screenPosLeft}, Local: {localPoint} (Rel to {parentRect.name})");
                    }
                }
            }

            // Position and Activate Right
            if (btnRight != null)
            {
                RectTransform btnRect = btnRight.GetComponent<RectTransform>();
                RectTransform parentRect = btnRight.transform.parent as RectTransform;
                
                if (parentRect != null && btnRect != null)
                {
                    btnRight.gameObject.SetActive(true);
                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosRight, uiCam, out localPoint))
                    {
                        btnRect.localPosition = localPoint;
                        Debug.Log($"[PrayerWheelUI] BtnRight World: {worldPosRight}, Screen: {screenPosRight}, Local: {localPoint} (Rel to {parentRect.name})");
                    }
                }
            }
        }
        else
        {
             Debug.LogWarning("[PrayerWheelUI] Canvas or Helper Camera missing, cannot snap buttons.");
        }

        // Update Name Texts
        if (wheel1NameText != null) wheel1NameText.text = talent1 != null ? talent1.talentName : "";
        if (wheel2NameText != null) wheel2NameText.text = talent2 != null ? talent2.talentName : "";
        
        Debug.Log("[PrayerWheelUI] UI Updated and Buttons Positioned");
    }

    private void SetButtonsActive(bool active)
    {
        if (commonButtonLeft) commonButtonLeft.gameObject.SetActive(active);
        if (commonButtonRight) commonButtonRight.gameObject.SetActive(active);
        if (rareButtonLeft) rareButtonLeft.gameObject.SetActive(active);
        if (rareButtonRight) rareButtonRight.gameObject.SetActive(active);
        if (legendaryButtonLeft) legendaryButtonLeft.gameObject.SetActive(active);
        if (legendaryButtonRight) legendaryButtonRight.gameObject.SetActive(active);
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

        Debug.Log($"[PrayerWheelUI] Talent selected: {talent.talentName}");

        // Fire event
        OnTalentSelected?.Invoke(talent);

        // Hide UI
        HideAll();

        // Resume game
        Time.timeScale = 1f;
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

        // Hide 3D wheels
        if (cameraSetup != null)
        {
            cameraSetup.HideWheels();
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
}
