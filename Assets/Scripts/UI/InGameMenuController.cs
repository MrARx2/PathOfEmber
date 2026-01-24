using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Runtime controller for the In-Game Pause Menu.
/// Handles hamburger toggle, time pause/resume, abilities grid population.
/// </summary>
public class InGameMenuController : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField, Tooltip("The main menu panel (hidden by default)")]
    private GameObject menuPanel;
    
    [Header("Button References")]
    [SerializeField, Tooltip("Hamburger button that opens the menu")]
    private Button hamburgerButton;
    
    [SerializeField, Tooltip("Continue button - closes menu and resumes")]
    private Button continueButton;
    
    [SerializeField, Tooltip("Sound settings button")]
    private Button soundSettingsButton;
    
    [SerializeField, Tooltip("Back to main menu button")]
    private Button backToMainMenuButton;
    
    [Header("Abilities Grid")]
    [SerializeField, Tooltip("Content transform of the abilities grid (GridLayoutGroup parent)")]
    private Transform abilitiesContent;
    
    [SerializeField, Tooltip("Prefab for talent icon display")]
    private GameObject talentIconPrefab;
    
    [SerializeField, Tooltip("RunTalentRegistry to read acquired talents from")]
    private RunTalentRegistry runTalentRegistry;
    
    [Header("Scene Settings")]
    [SerializeField, Tooltip("Main menu scene name to load")]
    private string mainMenuSceneName = "Main_Menu";
    
    [Header("Other Canvases to Hide")]
    [SerializeField, Tooltip("Canvases/GameObjects to disable when menu is open")]
    private GameObject[] canvasesToHide;
    
    [Header("Rarity Colors")]
    [SerializeField, Tooltip("Frame color for Common rarity talents")]
    private Color commonColor = new Color(0.5f, 0.5f, 0.5f);
    
    [SerializeField, Tooltip("Frame color for Rare rarity talents")]
    private Color rareColor = new Color(0.2f, 0.5f, 1f);
    
    [SerializeField, Tooltip("Frame color for Legendary rarity talents")]
    private Color legendaryColor = new Color(1f, 0.8f, 0.2f);
    
    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = true;
    
    // Time management
    private float previousTimeScale = 1f;
    private bool isOpen = false;
    
    // Pooled icons for abilities grid
    private List<GameObject> spawnedIcons = new List<GameObject>();
    
    private void Awake()
    {
        // Ensure menu is hidden on start
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
    }
    
    private void Start()
    {
        SetupButtonListeners();
    }
    
    private void SetupButtonListeners()
    {
        if (hamburgerButton != null)
        {
            hamburgerButton.onClick.AddListener(ToggleMenu);
        }
        else
        {
            Debug.LogWarning("[InGameMenuController] HamburgerButton not assigned!");
        }
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(CloseMenu);
        }
        
        if (soundSettingsButton != null)
        {
            soundSettingsButton.onClick.AddListener(OnSoundSettingsClicked);
        }
        
        if (backToMainMenuButton != null)
        {
            backToMainMenuButton.onClick.AddListener(OnBackToMainMenuClicked);
        }
    }
    
    /// <summary>
    /// Toggles the menu open/closed.
    /// </summary>
    public void ToggleMenu()
    {
        if (isOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }
    
    /// <summary>
    /// Opens the menu and pauses the game.
    /// </summary>
    public void OpenMenu()
    {
        if (isOpen) return;
        
        isOpen = true;
        
        // Store current time scale and pause
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        
        if (debugLog)
            Debug.Log($"[InGameMenuController] Menu opened, stored timeScale={previousTimeScale}");
        
        // Hide other canvases
        SetOtherCanvasesActive(false);
        
        // Populate abilities grid
        PopulateAbilitiesGrid();
        
        // Show panel
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Closes the menu and resumes the game.
    /// </summary>
    public void CloseMenu()
    {
        if (!isOpen) return;
        
        isOpen = false;
        
        // Restore time scale
        Time.timeScale = previousTimeScale;
        
        if (debugLog)
            Debug.Log($"[InGameMenuController] Menu closed, restored timeScale={previousTimeScale}");
        
        // Hide panel
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        
        // Restore other canvases
        SetOtherCanvasesActive(true);
    }
    
    /// <summary>
    /// Shows or hides other canvases (joystick, systems, prayer wheel).
    /// </summary>
    private void SetOtherCanvasesActive(bool active)
    {
        if (canvasesToHide == null) return;
        
        foreach (var canvas in canvasesToHide)
        {
            if (canvas != null)
            {
                canvas.SetActive(active);
                if (debugLog)
                    Debug.Log($"[InGameMenuController] Set {canvas.name} active={active}");
            }
        }
    }
    
    /// <summary>
    /// Populates the abilities grid with acquired talents from RunTalentRegistry.
    /// </summary>
    private void PopulateAbilitiesGrid()
    {
        if (abilitiesContent == null)
        {
            Debug.LogError("[InGameMenuController] AbilitiesContent not assigned! Drag the Content object from AbilitiesGridPanel > Viewport > Content");
            return;
        }
        
        if (runTalentRegistry == null)
        {
            Debug.LogError("[InGameMenuController] RunTalentRegistry not assigned! Drag your RunTalentRegistry asset here.");
            return;
        }
        
        // Clear existing spawned icons (not the preview placeholders)
        ClearAbilitiesGrid();
        
        // Also clear any existing children in Content (preview icons from wizard)
        for (int i = abilitiesContent.childCount - 1; i >= 0; i--)
        {
            var child = abilitiesContent.GetChild(i);
            if (debugLog)
                Debug.Log($"[InGameMenuController] Destroying preview child: {child.name}");
            Destroy(child.gameObject);
        }
        
        // Get all acquired talents
        var entries = runTalentRegistry.Entries;
        
        Debug.Log($"[InGameMenuController] RunTalentRegistry.Entries count: {entries.Length}");
        Debug.Log($"[InGameMenuController] RunTalentRegistry.UniqueTalentCount: {runTalentRegistry.UniqueTalentCount}");
        
        if (entries.Length == 0)
        {
            Debug.LogWarning("[InGameMenuController] No talents in registry! Grid will be empty.");
            return;
        }
        
        // Create icon for each talent
        foreach (var entry in entries)
        {
            if (entry.talent == null)
            {
                Debug.LogWarning("[InGameMenuController] Found null talent entry, skipping");
                continue;
            }
            
            Debug.Log($"[InGameMenuController] Creating icon for: {entry.talent.talentName} (stacks: {entry.stacks})");
            CreateTalentIcon(entry.talent, entry.stacks);
        }
        
        Debug.Log($"[InGameMenuController] Created {spawnedIcons.Count} talent icons");
    }
    
    /// <summary>
    /// Creates a talent icon in the grid.
    /// Supports TalentDisplay prefab with:
    /// - TalentFrame (Image, colored by rarity)
    /// - TalentImage (Image, shows talent.icon)
    /// - StacksNumber (TMP_Text, shows stack count)
    /// </summary>
    private void CreateTalentIcon(TalentData talent, int stacks)
    {
        GameObject iconGO;
        
        if (talentIconPrefab != null)
        {
            iconGO = Instantiate(talentIconPrefab, abilitiesContent);
            iconGO.name = $"TalentDisplay_{talent.talentName}";
            
            Debug.Log($"[InGameMenuController] === Creating icon for {talent.talentName} ===");
            Debug.Log($"[InGameMenuController] Talent icon sprite: {(talent.icon != null ? talent.icon.name : "NULL")}");
            
            // Find TalentFrame by name recursively
            var frameTransform = FindChildRecursive(iconGO.transform, "TalentFrame");
            if (frameTransform != null)
            {
                var frameImage = frameTransform.GetComponent<Image>();
                if (frameImage != null)
                {
                    frameImage.enabled = true;
                    // Clear any sprite so the color shows as solid
                    frameImage.sprite = null;
                    
                    Color rarityColor = GetRarityColor(talent.rarity);
                    frameImage.color = rarityColor;
                    
                    Debug.Log($"[InGameMenuController] {talent.talentName} rarity={talent.rarity}, color=({rarityColor.r:F2},{rarityColor.g:F2},{rarityColor.b:F2})");
                }
            }
            else
            {
                Debug.LogWarning($"[InGameMenuController] TalentFrame not found!");
            }
            
            // Find TalentImage by name recursively
            var imageTransform = FindChildRecursive(iconGO.transform, "TalentImage");
            if (imageTransform != null)
            {
                var talentImage = imageTransform.GetComponent<Image>();
                if (talentImage != null)
                {
                    talentImage.enabled = true;
                    
                    if (talent.icon != null)
                    {
                        talentImage.sprite = talent.icon;
                        talentImage.preserveAspect = true;
                        talentImage.color = Color.white;
                        Debug.Log($"[InGameMenuController] SUCCESS: Set TalentImage sprite to {talent.icon.name}");
                    }
                    else
                    {
                        Debug.LogError($"[InGameMenuController] FAILED: Talent {talent.talentName} has NULL icon!");
                    }
                }
                else
                {
                    Debug.LogWarning($"[InGameMenuController] TalentImage found but no Image component!");
                }
            }
            else
            {
                Debug.LogError($"[InGameMenuController] TalentImage NOT FOUND in prefab hierarchy!");
                // List all children for debugging
                ListAllChildren(iconGO.transform, 0);
            }
            
            // Find StacksNumber text and set stacks
            var stacksTransform = FindChildRecursive(iconGO.transform, "StacksNumber");
            if (stacksTransform != null)
            {
                var stacksText = stacksTransform.GetComponent<TMPro.TMP_Text>();
                if (stacksText != null)
                {
                    if (stacks > 1)
                    {
                        stacksText.text = $"x{stacks}";
                        stacksText.gameObject.SetActive(true);
                        Debug.Log($"[InGameMenuController] Set stacks text: x{stacks}");
                    }
                    else
                    {
                        // Hide stacks badge if only 1 stack
                        stacksText.gameObject.SetActive(false);
                    }
                }
            }
        }
        else
        {
            // Fallback: create simple icon if no prefab assigned
            iconGO = new GameObject($"TalentIcon_{talent.talentName}");
            iconGO.transform.SetParent(abilitiesContent, false);
            
            var rect = iconGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 100);
            
            var newImage = iconGO.AddComponent<Image>();
            newImage.raycastTarget = true;
            newImage.enabled = true;
            
            if (talent.icon != null)
            {
                newImage.sprite = talent.icon;
                newImage.preserveAspect = true;
                newImage.color = Color.white;
            }
            else
            {
                newImage.color = GetRarityColor(talent.rarity);
            }
        }
        
        // Ensure the icon is active
        iconGO.SetActive(true);
        
        spawnedIcons.Add(iconGO);
    }
    
    /// <summary>
    /// Recursively finds a child by name.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            var found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
    
    /// <summary>
    /// Debug helper: lists all children recursively.
    /// </summary>
    private void ListAllChildren(Transform parent, int depth)
    {
        string indent = new string(' ', depth * 2);
        foreach (Transform child in parent)
        {
            Debug.Log($"{indent}- {child.name}");
            ListAllChildren(child, depth + 1);
        }
    }
    
    /// <summary>
    /// Gets a color based on talent rarity.
    /// Uses configurable colors from Inspector.
    /// </summary>
    private Color GetRarityColor(TalentData.TalentRarity rarity)
    {
        return rarity switch
        {
            TalentData.TalentRarity.Common => commonColor,
            TalentData.TalentRarity.Rare => rareColor,
            TalentData.TalentRarity.Legendary => legendaryColor,
            _ => Color.white
        };
    }
    
    /// <summary>
    /// Clears all spawned icons from the grid.
    /// </summary>
    private void ClearAbilitiesGrid()
    {
        foreach (var icon in spawnedIcons)
        {
            if (icon != null)
            {
                Destroy(icon);
            }
        }
        spawnedIcons.Clear();
    }
    
    /// <summary>
    /// Called when Sound Settings button is clicked.
    /// </summary>
    private void OnSoundSettingsClicked()
    {
        if (debugLog)
            Debug.Log("[InGameMenuController] Sound Settings clicked - not implemented yet");
        
        // TODO: Show sound settings panel
    }
    
    /// <summary>
    /// Called when Back To Main Menu button is clicked.
    /// </summary>
    private void OnBackToMainMenuClicked()
    {
        if (debugLog)
            Debug.Log($"[InGameMenuController] Loading {mainMenuSceneName}");
        
        // Restore time before loading (important!)
        Time.timeScale = 1f;
        
        // Load main menu
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    /// <summary>
    /// Returns true if the menu is currently open.
    /// </summary>
    public bool IsOpen => isOpen;
    
    #region Debug
    [ContextMenu("Debug: Open Menu")]
    public void DebugOpenMenu() => OpenMenu();
    
    [ContextMenu("Debug: Close Menu")]
    public void DebugCloseMenu() => CloseMenu();
    
    [ContextMenu("Debug: Populate Grid")]
    public void DebugPopulateGrid() => PopulateAbilitiesGrid();
    #endregion
}
