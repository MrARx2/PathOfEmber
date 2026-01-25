using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Audio;

/// <summary>
/// Main menu UI controller with Shop, Play, and Talent Roll buttons.
/// Flow: User clicks Roll button to preview random talents (shows cube initially, then talent icon).
/// Play button starts the game with the currently displayed talent.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField, Tooltip("Shop button (will be greyed out/disabled)")]
    private Button shopButton;
    
    [SerializeField, Tooltip("Play button - starts game with currently selected talent")]
    private Button playButton;
    
    [SerializeField, Tooltip("Roll Talent button - rolls a random talent without starting the game")]
    private Button rollTalentButton;

    [Header("Button Sounds")]
    [SerializeField, Tooltip("Sound when Shop button is clicked")]
    private SoundEvent shopButtonSound;
    
    [SerializeField, Tooltip("Sound when Play button is clicked")]
    private SoundEvent playButtonSound;
    
    [SerializeField, Tooltip("Sound when Roll button is clicked")]
    private SoundEvent rollButtonSound;

    [Header("Scene Names")]
    [SerializeField, Tooltip("Name of the game scene to load")]
    private string gameSceneName = "GameScene";

    [Header("Currency Display")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI gemsText;

    [Header("Talent Display")]
    [SerializeField, Tooltip("Image on the roll button - shows cube initially, then talent icon after roll")]
    private Image talentIcon;
    
    [SerializeField, Tooltip("Default cube sprite shown before any talent is rolled")]
    private Sprite defaultCubeSprite;
    
    [SerializeField, Tooltip("Text to show the random talent name (can be on/near the roll button)")]
    private TextMeshProUGUI talentNameText;
    
    [SerializeField, Tooltip("Default text shown before any talent is rolled")]
    private string defaultTalentText = "Roll Talent";
    
    [SerializeField, Tooltip("Reference to the TalentDatabase for random talent selection")]
    private TalentDatabase talentDatabase;

    // The currently selected talent (null until first roll)
    private TalentData selectedTalent;

    // Key used to pass talent to game scene
    public const string QUICK_PLAY_TALENT_KEY = "QuickPlayTalentName";
    public const string QUICK_PLAY_MODE_KEY = "QuickPlayMode";

    private void Awake()
    {
        // Debug: Log button assignments to help diagnose issues
        Debug.Log($"[MainMenuUI] Awake - playButton assigned: {playButton != null}");
        Debug.Log($"[MainMenuUI] Awake - rollTalentButton assigned: {rollTalentButton != null}");
        Debug.Log($"[MainMenuUI] Awake - talentDatabase assigned: {talentDatabase != null}");
    }

    private void Start()
    {
        SetupButtons();
        UpdateCurrencyDisplay();
        ResetTalentDisplay(); // Show cube initially, no talent selected

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
        // Shop button - plays sound when clicked
        if (shopButton != null)
        {
            shopButton.onClick.AddListener(OnShopClicked);
        }

        // Play button - starts game with selected talent
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayClicked);
        }

        // Roll Talent button - only rolls, doesn't start game
        if (rollTalentButton != null)
        {
            rollTalentButton.onClick.AddListener(OnRollTalentClicked);
            Debug.Log("[MainMenuUI] Roll button listener added successfully");
        }
        else
        {
            Debug.LogError("[MainMenuUI] rollTalentButton is NOT assigned in Inspector!");
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

    /// <summary>
    /// Resets the talent display to show the default cube and text.
    /// </summary>
    private void ResetTalentDisplay()
    {
        selectedTalent = null;

        if (talentIcon != null)
        {
            if (defaultCubeSprite != null)
            {
                talentIcon.sprite = defaultCubeSprite;
                talentIcon.enabled = true;
            }
            else
            {
                talentIcon.enabled = false;
            }
        }

        if (talentNameText != null)
        {
            talentNameText.text = defaultTalentText;
        }
    }

    /// <summary>
    /// Rolls a random talent and updates the display.
    /// Does NOT start the game - user must click Play to start.
    /// </summary>
    private void RollRandomTalent()
    {
        if (talentDatabase == null)
        {
            Debug.LogWarning("[MainMenuUI] TalentDatabase not assigned! Cannot roll talent.");
            return;
        }

        // Get all talents and pick a random one
        TalentData[] allTalents = talentDatabase.GetAllTalents();
        if (allTalents == null || allTalents.Length == 0)
        {
            Debug.LogWarning("[MainMenuUI] No talents in database!");
            return;
        }

        selectedTalent = allTalents[Random.Range(0, allTalents.Length)];
        
        // Update UI to show the rolled talent
        if (talentIcon != null && selectedTalent.icon != null)
        {
            talentIcon.sprite = selectedTalent.icon;
            talentIcon.enabled = true;
        }

        if (talentNameText != null)
        {
            talentNameText.text = selectedTalent.talentName;
        }

        Debug.Log($"[MainMenuUI] Rolled talent: {selectedTalent.talentName}");
    }

    /// <summary>
    /// Called when Roll Talent button is clicked.
    /// Rolls a new random talent without starting the game.
    /// </summary>
    private void OnRollTalentClicked()
    {
        // Play roll button sound
        if (rollButtonSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(rollButtonSound);
        
        RollRandomTalent();
        Debug.Log("[MainMenuUI] Roll button clicked - talent rolled, waiting for Play");
    }

    /// <summary>
    /// Called when Shop button is clicked.
    /// Currently just plays sound - shop not implemented yet.
    /// </summary>
    private void OnShopClicked()
    {
        // Play shop button sound
        if (shopButtonSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(shopButtonSound);
        
        Debug.Log("[MainMenuUI] Shop button clicked - shop not implemented yet");
    }

    /// <summary>
    /// Called when Play button is clicked.
    /// Starts the game with the currently selected talent.
    /// </summary>
    private void OnPlayClicked()
    {
        // Play play button sound
        if (playButtonSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(playButtonSound);
        
        // Stop main menu music before loading next scene
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM();
        
        // Ensure GameSessionManager exists before setting data
        GameSessionManager.EnsureExists();
        
        if (selectedTalent != null)
        {
            // Store talent in GameSessionManager (persists across scene load)
            GameSessionManager.Instance.StartingTalent = selectedTalent;
            Debug.Log($"[MainMenuUI] Play clicked with talent: {selectedTalent.talentName}");
        }
        else
        {
            // No talent selected - clear any previous selection
            if (GameSessionManager.Instance != null)
                GameSessionManager.Instance.StartingTalent = null;
            Debug.Log("[MainMenuUI] Play clicked without talent - starting normal mode");
        }
        
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Public method to re-roll the talent (can be called from other scripts if needed)
    /// </summary>
    public void RerollTalent()
    {
        RollRandomTalent();
    }

    /// <summary>
    /// Returns true if a talent has been rolled/selected
    /// </summary>
    public bool HasTalentSelected => selectedTalent != null;

    /// <summary>
    /// Gets the currently selected talent (null if none rolled yet)
    /// </summary>
    public TalentData SelectedTalent => selectedTalent;

    #region Debug
    [ContextMenu("Debug: Roll Random Talent")]
    public void DebugRollRandomTalent() => RollRandomTalent();
    
    [ContextMenu("Debug: Reset Talent Display")]
    public void DebugResetTalentDisplay() => ResetTalentDisplay();
    #endregion
}
