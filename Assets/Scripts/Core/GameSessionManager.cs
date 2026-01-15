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
