using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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

    [SerializeField, Tooltip("Settings button to open sound settings")]
    private Button settingsButton;

    [SerializeField, Tooltip("Sound settings panel object")]
    private GameObject soundSettingsPanel;

    [Header("Scene Names")]
    [SerializeField, Tooltip("Name of the game scene to load")]
    private string gameSceneName = "GameScene";

    // Duplicate removed

    // Currency display has been moved to CurrencyUIController (Persistent UI)

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
        Debug.Log($"[MainMenuUI] Awake - Script Enabled: {enabled}, GameObject Active: {gameObject.activeInHierarchy}");
    }

    private void OnEnable()
    {
        Debug.Log("[MainMenuUI] OnEnable called");
        // Start delayed reset to handle scene load race conditions
        StartCoroutine(ForceResetRoutine());
    }

    private System.Collections.IEnumerator ForceResetRoutine()
    {
        // Wait for end of frame to ensure UI layout and references are stable
        yield return new WaitForEndOfFrame();
        
        Debug.Log("[MainMenuUI] ForceResetRoutine executing after delay");
        ResetTalentDisplay();
    }

    private void Start()
    {
        Debug.Log("[MainMenuUI] Start called");
        
        SetupButtons();
        // Currency is now handled to CurrencyUIController (Persistent UI)
        
        // Debug reference status
        Debug.Log($"[MainMenuUI] defaultCubeSprite is null? {defaultCubeSprite == null}");
        Debug.Log($"[MainMenuUI] talentIcon is null? {talentIcon == null}");
        Debug.Log($"[MainMenuUI] talentDatabase is null? {talentDatabase == null}");
        
        ResetTalentDisplay(); // Show cube initially, no talent selected
    }
    
    private void OnDestroy()
    {
        // No cleanup needed
    }

    private void SetupButtons()
    {
        // Shop button - no auto-setup, user handles manually

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
        
        // Settings Button
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
    }
    
    private void OnSettingsClicked()
    {
        if (soundSettingsPanel != null)
        {
            soundSettingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[MainMenuUI] Sound Settings Panel not assigned!");
        }
    }

    // Currency logic moved to CurrencyUIController

    /// <summary>
    /// Resets the talent display to show the default cube and text.
    /// </summary>
    private void ResetTalentDisplay()
    {
        selectedTalent = null;
        Debug.Log("[MainMenuUI] ResetTalentDisplay called");

        if (talentIcon != null)
        {
            if (defaultCubeSprite != null)
            {
                talentIcon.sprite = defaultCubeSprite;
                talentIcon.enabled = true;
                Debug.Log($"[MainMenuUI] Set talentIcon sprite to {defaultCubeSprite.name}");
            }
            else
            {
                talentIcon.enabled = false;
                Debug.LogWarning("[MainMenuUI] defaultCubeSprite is NULL! Disabling icon.");
            }
        }
        else
        {
            Debug.LogError("[MainMenuUI] talentIcon Image is NULL!");
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
        RollRandomTalent();
        Debug.Log("[MainMenuUI] Roll button clicked - talent rolled, waiting for Play");
    }

    /// <summary>
    /// Called when Play button is clicked.
    /// Starts the game with the currently selected talent.
    /// </summary>
    private void OnPlayClicked()
    {
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
