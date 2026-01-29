using UnityEngine;

/// <summary>
/// Initializes the main menu scene by resetting any leftover state from gameplay.
/// Attach this to an empty GameObject in your main menu scene.
/// </summary>
public class MainMenuInit : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    private void Awake()
    {
        // Ensure game is not paused
        Time.timeScale = 1f;
        
        // Unlock cursor for menu navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (debugLog) Debug.Log("[MainMenuInit] Main menu initialized. TimeScale reset to 1.");
    }
}
