using UnityEngine;

/// <summary>
/// Persistent singleton that holds session data across scene loads.
/// Used to pass data from Main Menu to GameScene (e.g., starting talent).
/// </summary>
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Header("Session Data")]
    [Tooltip("The talent selected in the Main Menu (set before loading GameScene)")]
    public TalentData StartingTalent;

    [Header("UI References")]
    [Tooltip("The Canvas or Layout group containing the persistent UI (PlayerBox, GemBox, etc)")]
    public GameObject persistentUIRoot;
    
    [Tooltip("Names of scenes where the persistent UI should be visible (e.g. Main_Menu)")]
    public string[] menuSceneNames = new string[] { "Main_Menu", "MainMenu" };

    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Subscribe to scene events
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        UpdateUIVisibility(scene.name);
    }

    private void UpdateUIVisibility(string sceneName)
    {
        if (persistentUIRoot == null) return;

        bool shouldShow = false;
        foreach (string menuName in menuSceneNames)
        {
            if (sceneName == menuName)
            {
                shouldShow = true;
                break;
            }
        }

        persistentUIRoot.SetActive(shouldShow);
        Debug.Log($"[GameSessionManager] Scene loaded: {sceneName}. Persistent UI visible: {shouldShow}");
    }

    /// <summary>
    /// Clears session data. Call when returning to Main Menu or starting a new run.
    /// </summary>
    public void ClearSession()
    {
        StartingTalent = null;
    }

    /// <summary>
    /// Ensures GameSessionManager exists. Creates one if needed.
    /// Call this from Main Menu before setting session data.
    /// </summary>
    public static void EnsureExists()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("GameSessionManager");
            go.AddComponent<GameSessionManager>();
        }
    }
}
