using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for individual message panels.
/// Handles the close button functionality.
/// </summary>
public class MessagePanelController : MonoBehaviour
{
    [Header("Button Reference")]
    [SerializeField, Tooltip("The button that closes this panel")]
    private Button closeButton;
    
    [Header("Settings")]
    [SerializeField, Tooltip("If true, notifies MessageManager when closed")]
    private bool notifyManager = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    private MessageManager messageManager;
    
    private void Awake()
    {
        // Auto-find close button if not assigned
        if (closeButton == null)
            closeButton = GetComponentInChildren<Button>();
        
        // Setup button listener
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
        
        // Find MessageManager
        messageManager = FindFirstObjectByType<MessageManager>();
    }
    
    /// <summary>
    /// Called when close button is clicked.
    /// </summary>
    public void OnCloseClicked()
    {
        if (debugLog)
            Debug.Log($"[MessagePanelController] Closing panel: {gameObject.name}");
        
        // Notify manager first (to unpause)
        if (notifyManager && messageManager != null)
        {
            messageManager.OnMessageClosed();
        }
        else
        {
            // If no manager, restore time ourselves
            Time.timeScale = 1f;
        }
        
        // Deactivate this panel
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Shows this panel (can be called externally).
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Hides this panel (same as close).
    /// </summary>
    public void Hide()
    {
        OnCloseClicked();
    }
    
    #region Debug
    [ContextMenu("Debug: Close Panel")]
    private void DebugClose() => OnCloseClicked();
    #endregion
}
