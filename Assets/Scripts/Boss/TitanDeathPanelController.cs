using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Boss
{
    /// <summary>
    /// Controller for the Titan Death Panel shown after defeating the Titan boss.
    /// Displays "Survive" (greyed out placeholder) and "Back to Main Menu" buttons.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TitanDeathPanelController : MonoBehaviour
    {
        [Header("Button References")]
        [SerializeField, Tooltip("Survive button (currently disabled as placeholder)")]
        private Button surviveButton;
        
        [SerializeField, Tooltip("Back to Main Menu button")]
        private Button mainMenuButton;
        
        [Header("Scene Settings")]
        [SerializeField, Tooltip("Name of the main menu scene")]
        private string mainMenuSceneName = "Main_Menu";
        
        [Header("Auto-Connect")]
        [SerializeField, Tooltip("If true, auto-finds TitanBossController and subscribes to OnBossDefeated")]
        private bool autoConnect = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private TitanBossController bossController;
        private CanvasGroup canvasGroup;
        
        private void Awake()
        {
            // Get or add CanvasGroup for visibility control
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
            // Hide panel initially (but keep GameObject active so events work)
            SetVisible(false);
            
            // Setup button listeners
            if (surviveButton != null)
            {
                // Grey out the survive button (placeholder for future feature)
                surviveButton.interactable = false;
            }
            
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
            
            // Auto-connect to boss controller
            if (autoConnect)
            {
                bossController = FindFirstObjectByType<TitanBossController>();
                if (bossController != null)
                {
                    bossController.OnBossDefeated += Show;
                    if (debugLog) Debug.Log("[TitanDeathPanel] Connected to TitanBossController.OnBossDefeated");
                }
                else
                {
                    if (debugLog) Debug.LogWarning("[TitanDeathPanel] TitanBossController not found - auto-connect failed");
                }
            }
        }
        
        private void OnEnable()
        {
            // When activated (e.g., by TitanBossController.SetActive(true)), make visible and trigger pause logic
            if (canvasGroup != null)
            {
                Show();
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from event
            if (bossController != null)
            {
                bossController.OnBossDefeated -= Show;
            }
        }
        
        /// <summary>
        /// Sets panel visibility via CanvasGroup (keeps GameObject active for events).
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        
        /// <summary>
        /// Shows the death panel. Called when Titan is defeated.
        /// </summary>
        public void Show()
        {
            if (debugLog) Debug.Log("[TitanDeathPanel] Showing panel - Titan defeated!");
            
            SetVisible(true);
            
            // Pause the game
            Time.timeScale = 0f;

            // Unlock and show cursor so player can interact with the panel
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        /// <summary>
        /// Hides the death panel.
        /// </summary>
        public void Hide()
        {
            SetVisible(false);
        }
        
        /// <summary>
        /// Called when Back to Main Menu button is clicked.
        /// </summary>
        private void OnMainMenuClicked()
        {
            if (debugLog) Debug.Log("[TitanDeathPanel] Loading Main Menu...");
            
            // Restore time in case it was paused
            Time.timeScale = 1f;
            
            // Load main menu scene
            SceneManager.LoadScene(mainMenuSceneName);
        }
        
        #region Debug
        [ContextMenu("Debug: Show Panel")]
        private void DebugShow() => Show();
        
        [ContextMenu("Debug: Hide Panel")]
        private void DebugHide() => Hide();
        #endregion
    }
}
