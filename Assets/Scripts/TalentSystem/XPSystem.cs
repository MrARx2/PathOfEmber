using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

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
    
    // Total XP tracking
    private int totalXPEarned = 0;

    [Header("UI References")]
    [SerializeField, Tooltip("XP bar fill image (Image.fillAmount)")]
    private Image xpBarFill;
    [SerializeField, Tooltip("Optional: Text showing current XP")]
    private TMPro.TextMeshProUGUI xpText;
    [SerializeField, Tooltip("Optional: Text showing current level")]
    private TMPro.TextMeshProUGUI levelText;
    [SerializeField, Tooltip("Optional: Text showing TOTAL XP/Coins collected")]
    private TMPro.TextMeshProUGUI totalXPText;
    
    [Header("XP Gain Display")]
    [SerializeField, Tooltip("Text showing +XP gained (e.g., '+15')")]
    private TMPro.TextMeshProUGUI xpGainText;
    [SerializeField, Tooltip("How long the gain text stays visible after last coin")]
    private float gainDisplayDuration = 1f;
    [SerializeField, Tooltip("Fade out duration")]
    private float gainFadeDuration = 0.3f;
    
    [Header("Gain Text Animation")]
    [SerializeField, Tooltip("Scale multiplier when text pops")]
    private float gainTextScalePop = 1.3f;
    [SerializeField, Tooltip("Duration of scale pop")]
    private float gainTextPopDuration = 0.1f;
    
    [Header("XP Bar Glow")]
    [SerializeField, Tooltip("Optional: Glow/highlight image overlay on XP bar")]
    private Image xpBarGlow;
    [SerializeField, Tooltip("Glow color when coins collected")]
    private Color glowColor = new Color(1f, 0.9f, 0.4f, 0.8f);
    [SerializeField, Tooltip("Duration of glow pulse")]
    private float glowDuration = 0.2f;

    [Header("Events")]
    [Tooltip("Fired when XP bar fills completely")]
    public UnityEvent OnXPFilled;
    [Tooltip("Fired when XP changes, passes normalized value 0-1")]
    public UnityEvent<float> OnXPChanged;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    public int CurrentXP => currentXP;
    public int MaxXP => maxXP;
    public int Level => level;
    public int TotalXPEarned => totalXPEarned;
    public float NormalizedXP => maxXP > 0 ? (float)currentXP / maxXP : 0f;

    // XP gain accumulation
    private int accumulatedGain = 0;
    private float lastGainTime;
    private Coroutine gainDisplayCoroutine;
    private Coroutine gainTextPopCoroutine;
    private Coroutine glowCoroutine;
    private bool isShowingGain = false;
    private Vector3 gainTextOriginalScale;
    
    // Level-up queue (for multiple level-ups in quick succession)
    private int pendingLevelUps = 0;
    
    /// <summary>
    /// Number of pending level-up claims (prayer wheel spins owed to the player).
    /// </summary>
    public int PendingLevelUps => pendingLevelUps;

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
        
        // Hide gain text initially
        if (xpGainText != null)
        {
            gainTextOriginalScale = xpGainText.transform.localScale;
            xpGainText.gameObject.SetActive(false);
        }
        
        // Hide glow initially
        if (xpBarGlow != null)
        {
            Color c = xpBarGlow.color;
            c.a = 0f;
            xpBarGlow.color = c;
        }
    }

    /// <summary>
    /// Adds XP to the player. Called by coins on collection.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        currentXP += amount;
        totalXPEarned += amount;

        // Check for level up (can level up multiple times if huge XP gain)
        while (currentXP >= maxXP)
        {
            currentXP -= maxXP;
            LevelUp();
        }

        UpdateUI();
        
        // Accumulate and show gain with animations
        ShowGain(amount);
        PulseGlow();
    }

    /// <summary>
    /// Consumes XP for purchases (e.g., Yatai Shop).
    /// Returns true if successful, false if not enough XP.
    /// </summary>
    public bool ConsumeXP(int amount)
    {
        if (currentXP < amount) return false;

        currentXP -= amount;
        UpdateUI();
        return true;
    }

    private void ShowGain(int amount)
    {
        if (xpGainText == null) return;
        
        // Skip visual effects if GameObject is inactive (e.g., during prayer wheel)
        // XP is still added, we just don't show the gain text animation
        if (!gameObject.activeInHierarchy) return;
        
        // Accumulate gain
        accumulatedGain += amount;
        lastGainTime = Time.time;
        
        // Update text
        xpGainText.text = $"+{accumulatedGain}";
        
        // Show if hidden
        if (!isShowingGain)
        {
            isShowingGain = true;
            xpGainText.gameObject.SetActive(true);
            SetGainTextAlpha(1f);
            xpGainText.transform.localScale = gainTextOriginalScale;
        }
        
        // Trigger scale pop
        if (gainTextPopCoroutine != null)
            StopCoroutine(gainTextPopCoroutine);
        gainTextPopCoroutine = StartCoroutine(GainTextPopRoutine());
        
        // Restart the hide timer
        if (gainDisplayCoroutine != null)
            StopCoroutine(gainDisplayCoroutine);
        gainDisplayCoroutine = StartCoroutine(GainDisplayRoutine());
    }
    
    private IEnumerator GainTextPopRoutine()
    {
        float timer = 0f;
        while (timer < gainTextPopDuration)
        {
            timer += Time.deltaTime;
            float t = timer / gainTextPopDuration;
            
            // Scale up then back down (sine curve)
            float scaleMult = 1f + (gainTextScalePop - 1f) * Mathf.Sin(t * Mathf.PI);
            xpGainText.transform.localScale = gainTextOriginalScale * scaleMult;
            
            yield return null;
        }
        xpGainText.transform.localScale = gainTextOriginalScale;
        gainTextPopCoroutine = null;
    }
    
    private void PulseGlow()
    {
        if (xpBarGlow == null) return;
        
        // Skip visual effects if GameObject is inactive
        if (!gameObject.activeInHierarchy) return;
        
        if (glowCoroutine != null)
            StopCoroutine(glowCoroutine);
        glowCoroutine = StartCoroutine(GlowRoutine());
    }
    
    private IEnumerator GlowRoutine()
    {
        float timer = 0f;
        
        while (timer < glowDuration)
        {
            timer += Time.deltaTime;
            float t = timer / glowDuration;
            
            // Fade in then out (sine curve)
            float alpha = glowColor.a * Mathf.Sin(t * Mathf.PI);
            Color c = glowColor;
            c.a = alpha;
            xpBarGlow.color = c;
            
            yield return null;
        }
        
        // Ensure fully transparent at end
        Color final = xpBarGlow.color;
        final.a = 0f;
        xpBarGlow.color = final;
        glowCoroutine = null;
    }
    
    private IEnumerator GainDisplayRoutine()
    {
        // Wait for display duration (resets each time new XP added)
        while (Time.time - lastGainTime < gainDisplayDuration)
        {
            yield return null;
        }
        
        // Fade out
        float fadeTimer = 0f;
        while (fadeTimer < gainFadeDuration)
        {
            fadeTimer += Time.deltaTime;
            float alpha = 1f - (fadeTimer / gainFadeDuration);
            SetGainTextAlpha(alpha);
            yield return null;
        }
        
        // Hide and reset
        xpGainText.gameObject.SetActive(false);
        accumulatedGain = 0;
        isShowingGain = false;
        gainDisplayCoroutine = null;
    }
    
    private void SetGainTextAlpha(float alpha)
    {
        if (xpGainText == null) return;
        Color c = xpGainText.color;
        c.a = alpha;
        xpGainText.color = c;
    }

    private void LevelUp()
    {
        level++;
        
        // Scale XP requirement for next level
        maxXP = Mathf.RoundToInt(maxXP * (1f + xpScalingPercent / 100f));
        
        // Queue a level-up (don't fire event immediately - let TalentSelectionManager claim it)
        pendingLevelUps++;
        if (debugLog) Debug.Log($"[XPSystem] Level up queued! Level: {level}, Pending: {pendingLevelUps}");
    }
    
    /// <summary>
    /// Returns true if there are pending level-ups to claim.
    /// </summary>
    public bool HasPendingLevelUp()
    {
        return pendingLevelUps > 0;
    }
    
    /// <summary>
    /// Claims one pending level-up and fires OnXPFilled.
    /// Call this after the previous prayer wheel selection is complete.
    /// </summary>
    public void ClaimNextLevelUp()
    {
        if (pendingLevelUps > 0)
        {
            pendingLevelUps--;
            if (debugLog) Debug.Log($"[XPSystem] Level-up claimed! Remaining: {pendingLevelUps}");
            OnXPFilled?.Invoke();
        }
    }
    
    /// <summary>
    /// Triggers the first pending level-up if any exist.
    /// Called from Update to ensure first level-up is processed.
    /// </summary>
    private void Update()
    {
        // Auto-trigger first pending level-up (subsequent ones handled by TalentSelectionManager)
        if (pendingLevelUps > 0 && !isProcessingLevelUp)
        {
            isProcessingLevelUp = true;
            ClaimNextLevelUp();
        }
    }
    
    private bool isProcessingLevelUp = false;
    
    /// <summary>
    /// Called by TalentSelectionManager when talent selection is complete.
    /// This allows the next queued level-up to be processed.
    /// </summary>
    public void OnTalentSelectionComplete()
    {
        isProcessingLevelUp = false;
        if (debugLog) Debug.Log($"[XPSystem] Talent selection complete. Pending level-ups: {pendingLevelUps}");
        // Next Update() will trigger the next level-up if any pending
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
        
        if (totalXPText != null)
        {
            totalXPText.text = $"{totalXPEarned}";
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
        accumulatedGain = 0;
        totalXPEarned = 0;
        
        UpdateUI();
        
        if (xpGainText != null)
            xpGainText.gameObject.SetActive(false);
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

    [ContextMenu("Debug: Double Level-Up (Test Queue)")]
    public void DebugDoubleLevelUp()
    {
        // Adds enough XP to level up twice (tests the queue system)
        int xpForTwoLevels = maxXP + Mathf.RoundToInt(maxXP * (1f + xpScalingPercent / 100f));
        if (debugLog) Debug.Log($"[XPSystem] Debug: Adding {xpForTwoLevels} XP for double level-up");
        AddXP(xpForTwoLevels);
    }

    [ContextMenu("Debug: Reset XP")]
    public void DebugReset() => ResetXP();
    #endregion
}


