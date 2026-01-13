using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Main menu UI controller with Shop, Play, Quick Play buttons and currency displays.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField, Tooltip("Shop button (will be greyed out/disabled)")]
    private Button shopButton;
    
    [SerializeField, Tooltip("Play button - loads story scene")]
    private Button playButton;
    
    [SerializeField, Tooltip("Quick Play button - starts with random talent")]
    private Button quickPlayButton;

    [Header("Scene Names")]
    [SerializeField, Tooltip("Name of the story/main game scene")]
    private string storySceneName = "GameScene";
    
    [SerializeField, Tooltip("Name of the quick play scene (can be same as story)")]
    private string quickPlaySceneName = "GameScene";

    [Header("Currency Display")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI gemsText;

    [Header("Quick Play - Random Talent Display")]
    [SerializeField, Tooltip("Image to show the random talent icon")]
    private Image talentIcon;
    
    [SerializeField, Tooltip("Text to show the random talent name")]
    private TextMeshProUGUI talentNameText;
    
    [SerializeField, Tooltip("Reference to the TalentDatabase for random talent selection")]
    private TalentDatabase talentDatabase;

    // The randomly selected talent for quick play
    private TalentData selectedQuickPlayTalent;

    // Key used to pass talent to game scene
    public const string QUICK_PLAY_TALENT_KEY = "QuickPlayTalentName";
    public const string QUICK_PLAY_MODE_KEY = "QuickPlayMode";

    private void Start()
    {
        SetupButtons();
        UpdateCurrencyDisplay();
        SelectRandomTalent();

        // Subscribe to currency changes
        if (PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.OnCurrencyChanged += UpdateCurrencyDisplay;
        }
    }

    private void OnDestroy()
    {
        if (PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.OnCurrencyChanged -= UpdateCurrencyDisplay;
        }
    }

    private void SetupButtons()
    {
        // Shop button - no auto-setup, user handles manually

        // Play button
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayClicked);
        }

        // Quick Play button
        if (quickPlayButton != null)
        {
            quickPlayButton.onClick.AddListener(OnQuickPlayClicked);
        }
    }

    private void UpdateCurrencyDisplay()
    {
        if (PlayerCurrency.Instance != null)
        {
            if (coinsText != null)
                coinsText.text = PlayerCurrency.Instance.Coins.ToString();
            
            if (gemsText != null)
                gemsText.text = PlayerCurrency.Instance.Gems.ToString();
        }
        else
        {
            // Fallback if PlayerCurrency isn't set up yet
            if (coinsText != null) coinsText.text = "0";
            if (gemsText != null) gemsText.text = "0";
        }
    }

    private void SelectRandomTalent()
    {
        if (talentDatabase == null)
        {
            Debug.LogWarning("[MainMenuUI] TalentDatabase not assigned! Cannot select random talent.");
            return;
        }

        // Get all talents and pick a random one
        TalentData[] allTalents = talentDatabase.GetAllTalents();
        if (allTalents == null || allTalents.Length == 0)
        {
            Debug.LogWarning("[MainMenuUI] No talents in database!");
            return;
        }

        selectedQuickPlayTalent = allTalents[Random.Range(0, allTalents.Length)];
        
        // Update UI
        if (talentIcon != null && selectedQuickPlayTalent.icon != null)
        {
            talentIcon.sprite = selectedQuickPlayTalent.icon;
            talentIcon.enabled = true;
        }

        if (talentNameText != null)
        {
            talentNameText.text = selectedQuickPlayTalent.talentName;
        }

        Debug.Log($"[MainMenuUI] Random talent selected: {selectedQuickPlayTalent.talentName}");
    }

    private void OnPlayClicked()
    {
        Debug.Log("[MainMenuUI] Play clicked - loading story scene");
        PlayerPrefs.SetInt(QUICK_PLAY_MODE_KEY, 0); // Not quick play mode
        PlayerPrefs.Save();
        SceneManager.LoadScene(storySceneName);
    }

    private void OnQuickPlayClicked()
    {
        if (selectedQuickPlayTalent == null)
        {
            Debug.LogError("[MainMenuUI] No talent selected for quick play!");
            return;
        }

        Debug.Log($"[MainMenuUI] Quick Play clicked with talent: {selectedQuickPlayTalent.talentName}");
        
        // Store talent name to apply in game scene
        PlayerPrefs.SetString(QUICK_PLAY_TALENT_KEY, selectedQuickPlayTalent.talentName);
        PlayerPrefs.SetInt(QUICK_PLAY_MODE_KEY, 1); // Quick play mode
        PlayerPrefs.Save();
        
        SceneManager.LoadScene(quickPlaySceneName);
    }

    /// <summary>
    /// Call this to re-roll the random talent (e.g., from a "Reroll" button)
    /// </summary>
    public void RerollTalent()
    {
        SelectRandomTalent();
    }

    #region Debug
    [ContextMenu("Debug: Select Random Talent")]
    public void DebugSelectRandomTalent() => SelectRandomTalent();
    #endregion
}
