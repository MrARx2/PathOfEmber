using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for the Death Popup panel.
/// Handles Restart and Back to Menu button functionality.
/// </summary>
public class DeathPopupController : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    
    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "Main_Menu";
    
    private void Start()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }
    
    /// <summary>
    /// Shows the death popup.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        
        // Pause game
        Time.timeScale = 0f;
    }
    
    /// <summary>
    /// Hides the death popup.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
    
    private void OnRestartClicked()
    {
        // Restore time before reload
        Time.timeScale = 1f;
        
        // Clear spawn registry so enemies can spawn again
        if (SpawnAreaRegistry.Instance != null)
        {
            SpawnAreaRegistry.Instance.ClearAll();
        }

        // Reload current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    private void OnMainMenuClicked()
    {
        // Restore time before loading
        Time.timeScale = 1f;
        
        // Load main menu
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
