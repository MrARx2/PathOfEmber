using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Yatai Shop UI (CartPanel).
/// Handles purchasing logic: Deduct XP -> Set Guaranteed Rarity -> Spin Prayer Wheel.
/// Pauses game while open.
/// </summary>
public class YataiShopUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject cartPanel;
    [SerializeField] private Button btnCommon;
    [SerializeField] private Button btnRare;
    [SerializeField] private Button btnLegendary;
    [SerializeField] private Button btnBack;
    
    // Optional: Text to show "Cost: 300 XP" etc.
    [SerializeField] private TextMeshProUGUI txtCommonCost; 
    [SerializeField] private TextMeshProUGUI txtRareCost;
    [SerializeField] private TextMeshProUGUI txtLegendaryCost;

    [Header("Cost Configuration (% of Max XP)")]
    [SerializeField, Range(0f, 1f)] private float costPctCommon = 0.3f;    // 30%
    [SerializeField, Range(0f, 1f)] private float costPctRare = 0.6f;      // 60%
    [SerializeField, Range(0f, 1f)] private float costPctLegendary = 0.9f; // 90%

    [Header("Button Colors")]
    [SerializeField] private Color colorCommon = Color.white;
    [SerializeField] private Color colorRare = new Color(0.2f, 0.4f, 1f); // Blue-ish
    [SerializeField] private Color colorLegendary = new Color(1f, 0.8f, 0f); // Gold
    [SerializeField] private Color colorDisabled = Color.gray;

    [Header("System References")]
    [SerializeField] private XPSystem xpSystem;
    [SerializeField] private PrayerWheelController wheelController;
    // [SerializeField] private PrayerWheelUI wheelUI; // Removed as requested
    [SerializeField] private TimeScaleManager timeScaleManager;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        // Auto-find references
        if (xpSystem == null) xpSystem = XPSystem.Instance;
        if (wheelController == null) wheelController = FindFirstObjectByType<PrayerWheelController>();
        if (timeScaleManager == null) timeScaleManager = TimeScaleManager.Instance;

        // Button Listeners
        if (btnCommon) btnCommon.onClick.AddListener(() => TryBuy(TalentData.TalentRarity.Common, costPctCommon));
        if (btnRare) btnRare.onClick.AddListener(() => TryBuy(TalentData.TalentRarity.Rare, costPctRare));
        if (btnLegendary) btnLegendary.onClick.AddListener(() => TryBuy(TalentData.TalentRarity.Legendary, costPctLegendary));
        if (btnBack) btnBack.onClick.AddListener(CloseShop);

        // Hide initially
        if (cartPanel != null) cartPanel.SetActive(false);
    }

    public void OpenShop()
    {
        if (cartPanel == null) return;

        IsOpen = true;
        cartPanel.SetActive(true);

        // Pause Game
        if (timeScaleManager != null)
            timeScaleManager.SetTimeScaleImmediate(0f);
        else
            Time.timeScale = 0f;

        UpdateButtons();
    }

    public void CloseShop()
    {
        IsOpen = false;
        if (cartPanel != null) cartPanel.SetActive(false);

        // Resume Game
        if (timeScaleManager != null)
            timeScaleManager.SetTimeScaleImmediate(1f);
        else
            Time.timeScale = 1f;
    }

    private void Update()
    {
        if (IsOpen)
        {
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        if (xpSystem == null) return;

        int maxXP = xpSystem.MaxXP;
        int currentXP = xpSystem.CurrentXP;

        // Calculate costs
        int costCommon = Mathf.RoundToInt(maxXP * costPctCommon);
        int costRare = Mathf.RoundToInt(maxXP * costPctRare);
        int costLegendary = Mathf.RoundToInt(maxXP * costPctLegendary);

        // Common
        bool canAffordCommon = currentXP >= costCommon;
        SetButtonState(btnCommon, canAffordCommon, colorCommon);
        if (txtCommonCost) txtCommonCost.text = $"{costCommon} XP (30%)";

        // Rare
        bool canAffordRare = currentXP >= costRare;
        SetButtonState(btnRare, canAffordRare, colorRare);
        if (txtRareCost) txtRareCost.text = $"{costRare} XP (60%)";

        // Legendary
        bool canAffordLegendary = currentXP >= costLegendary;
        SetButtonState(btnLegendary, canAffordLegendary, colorLegendary);
        if (txtLegendaryCost) txtLegendaryCost.text = $"{costLegendary} XP (90%)";
    }

    private void SetButtonState(Button btn, bool interactable, Color activeColor)
    {
        if (btn == null) return;
        btn.interactable = interactable;
        
        // Change Image Color
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = interactable ? activeColor : colorDisabled;
        }
    }

    private void TryBuy(TalentData.TalentRarity rarity, float costPct)
    {
        if (xpSystem == null || wheelController == null)
        {
            Debug.LogError("[YataiShopUI] Missing references!");
            return;
        }

        int cost = Mathf.RoundToInt(xpSystem.MaxXP * costPct);

        if (xpSystem.ConsumeXP(cost))
        {
            Debug.Log($"[YataiShop] Bought {rarity} for {cost} XP.");

            // 1. Force Rarity
            wheelController.SetGuaranteedRarity(rarity);

            // 2. Close Shop (Resumes time slightly)
            CloseShop(); 
            
            // 3. Trigger Spin via XP System Event
            // This tells TalentSelectionManager (or whoever is listening) to start the wheel sequence.
            // This mimics a natural level up but with our forced rarity.
            xpSystem.OnXPFilled?.Invoke();
        }
        else
        {
            Debug.LogWarning("[YataiShop] Purchase failed (Not enough XP) - Logic error in button state?");
        }
    }
}
