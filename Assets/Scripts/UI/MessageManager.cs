using UnityEngine;

/// <summary>
/// Manages message panels that appear based on player's Z position.
/// Add trigger points with Z thresholds and associated panels.
/// </summary>
public class MessageManager : MonoBehaviour
{
    [System.Serializable]
    public class MessageTrigger
    {
        [Tooltip("Z position at which to show the message panel")]
        public float triggerZPosition;
        
        [Tooltip("The panel to show (should be deactivated initially)")]
        public GameObject messagePanel;
        
        [Tooltip("If true, this message has already been shown")]
        [HideInInspector] public bool hasTriggered;
    }
    
    [Header("Message Triggers")]
    [SerializeField, Tooltip("Array of message triggers with Z positions and panels")]
    private MessageTrigger[] messageTriggers;
    
    [Header("Player Reference")]
    [SerializeField, Tooltip("The player transform to track (auto-finds if not assigned)")]
    private Transform playerTransform;
    
    [Header("Settings")]
    [SerializeField, Tooltip("If true, pauses the game when a message is shown")]
    private bool pauseOnMessage = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    private void Start()
    {
        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
        
        // Ensure all panels are deactivated at start
        if (messageTriggers != null)
        {
            foreach (var trigger in messageTriggers)
            {
                if (trigger.messagePanel != null)
                {
                    trigger.messagePanel.SetActive(false);
                    trigger.hasTriggered = false;
                }
            }
        }
        
        if (debugLog)
            Debug.Log($"[MessageManager] Initialized with {messageTriggers?.Length ?? 0} triggers");
    }
    
    private void Update()
    {
        if (playerTransform == null || messageTriggers == null) return;
        
        float playerZ = playerTransform.position.z;
        
        foreach (var trigger in messageTriggers)
        {
            // Skip if already triggered or no panel assigned
            if (trigger.hasTriggered || trigger.messagePanel == null) continue;
            
            // Check if player has passed the trigger Z position
            if (playerZ >= trigger.triggerZPosition)
            {
                ShowMessage(trigger);
            }
        }
    }
    
    private void ShowMessage(MessageTrigger trigger)
    {
        trigger.hasTriggered = true;
        trigger.messagePanel.SetActive(true);
        
        if (pauseOnMessage)
            Time.timeScale = 0f;
        
        if (debugLog)
            Debug.Log($"[MessageManager] Showing message panel: {trigger.messagePanel.name} at Z={trigger.triggerZPosition}");
    }
    
    /// <summary>
    /// Called when a message panel is closed (by MessagePanelController).
    /// </summary>
    public void OnMessageClosed()
    {
        if (pauseOnMessage)
            Time.timeScale = 1f;
        
        if (debugLog)
            Debug.Log("[MessageManager] Message closed, game resumed");
    }
    
    /// <summary>
    /// Resets all triggers so messages can be shown again.
    /// </summary>
    public void ResetAllTriggers()
    {
        if (messageTriggers == null) return;
        
        foreach (var trigger in messageTriggers)
        {
            trigger.hasTriggered = false;
            if (trigger.messagePanel != null)
                trigger.messagePanel.SetActive(false);
        }
        
        if (debugLog)
            Debug.Log("[MessageManager] All triggers reset");
    }
    
    #region Debug
    [ContextMenu("Debug: Reset All Triggers")]
    private void DebugResetTriggers() => ResetAllTriggers();
    
    [ContextMenu("Debug: Show First Message")]
    private void DebugShowFirst()
    {
        if (messageTriggers != null && messageTriggers.Length > 0 && messageTriggers[0].messagePanel != null)
        {
            messageTriggers[0].messagePanel.SetActive(true);
            if (pauseOnMessage) Time.timeScale = 0f;
        }
    }
    #endregion
}
