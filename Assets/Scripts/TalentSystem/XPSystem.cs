using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Manages player XP and triggers the Prayer Wheel when XP bar fills.
/// </summary>
public class XPSystem : MonoBehaviour
{
    public static XPSystem Instance { get; private set; }

    [Header("XP Configuration")]
    [SerializeField] private int currentXP = 0;
    [SerializeField] private int maxXP = 100;
    [SerializeField] private int level = 1;
    [SerializeField, Tooltip("XP required increases by this percentage each level")]
    private float xpScalingPercent = 15f;

    [Header("UI References")]
    [SerializeField, Tooltip("XP bar fill image (Image.fillAmount)")]
    private Image xpBarFill;
    [SerializeField, Tooltip("Optional: Text showing current XP")]
    private TMPro.TextMeshProUGUI xpText;
    [SerializeField, Tooltip("Optional: Text showing current level")]
    private TMPro.TextMeshProUGUI levelText;

    [Header("Events")]
    [Tooltip("Fired when XP bar fills completely")]
    public UnityEvent OnXPFilled;
    [Tooltip("Fired when XP changes, passes normalized value 0-1")]
    public UnityEvent<float> OnXPChanged;

    public int CurrentXP => currentXP;
    public int MaxXP => maxXP;
    public int Level => level;
    public float NormalizedXP => maxXP > 0 ? (float)currentXP / maxXP : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        UpdateUI();
    }

    /// <summary>
    /// Adds XP to the player. Called by enemies on death, pickups, etc.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        currentXP += amount;
        Debug.Log($"[XPSystem] Added {amount} XP. Current: {currentXP}/{maxXP}");

        // Check for level up (can level up multiple times if huge XP gain)
        while (currentXP >= maxXP)
        {
            currentXP -= maxXP;
            LevelUp();
        }

        UpdateUI();
    }

    private void LevelUp()
    {
        level++;
        
        // Scale XP requirement for next level
        maxXP = Mathf.RoundToInt(maxXP * (1f + xpScalingPercent / 100f));
        
        Debug.Log($"[XPSystem] Level Up! Now level {level}. Next level requires {maxXP} XP.");
        
        // Trigger the prayer wheel
        OnXPFilled?.Invoke();
    }

    private void UpdateUI()
    {
        float normalized = NormalizedXP;
        
        if (xpBarFill != null)
        {
            xpBarFill.fillAmount = normalized;
        }

        if (xpText != null)
        {
            xpText.text = $"{currentXP} / {maxXP}";
        }

        if (levelText != null)
        {
            levelText.text = $"Lv. {level}";
        }

        OnXPChanged?.Invoke(normalized);
    }

    /// <summary>
    /// Resets XP system to initial state.
    /// </summary>
    public void ResetXP()
    {
        currentXP = 0;
        level = 1;
        maxXP = 100;
        UpdateUI();
    }

    #region Debug
    [ContextMenu("Debug: Add 25 XP")]
    public void DebugAdd25XP() => AddXP(25);

    [ContextMenu("Debug: Add 50 XP")]
    public void DebugAdd50XP() => AddXP(50);

    [ContextMenu("Debug: Add 100 XP")]
    public void DebugAdd100XP() => AddXP(100);

    [ContextMenu("Debug: Fill XP Bar")]
    public void DebugFillBar() => AddXP(maxXP - currentXP);

    [ContextMenu("Debug: Reset XP")]
    public void DebugReset() => ResetXP();
    #endregion
}
