using UnityEngine;

/// <summary>
/// Manages player currency (coins and gems) using PlayerPrefs for persistence.
/// </summary>
public class PlayerCurrency : MonoBehaviour
{
    private const string COINS_KEY = "PlayerCoins";
    private const string GEMS_KEY = "PlayerGems";

    private static PlayerCurrency instance;
    public static PlayerCurrency Instance => instance;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    public event System.Action OnCurrencyChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public int Coins
    {
        get => PlayerPrefs.GetInt(COINS_KEY, 0);
        set
        {
            PlayerPrefs.SetInt(COINS_KEY, Mathf.Max(0, value));
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke();
        }
    }

    public int Gems
    {
        get => PlayerPrefs.GetInt(GEMS_KEY, 0);
        set
        {
            PlayerPrefs.SetInt(GEMS_KEY, Mathf.Max(0, value));
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke();
        }
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        if (debugLog) Debug.Log($"[PlayerCurrency] Added {amount} coins. Total: {Coins}");
    }

    public void AddGems(int amount)
    {
        Gems += amount;
        if (debugLog) Debug.Log($"[PlayerCurrency] Added {amount} gems. Total: {Gems}");
    }

    public bool SpendCoins(int amount)
    {
        if (Coins >= amount)
        {
            Coins -= amount;
            if (debugLog) Debug.Log($"[PlayerCurrency] Spent {amount} coins. Remaining: {Coins}");
            return true;
        }
        if (debugLog) Debug.LogWarning($"[PlayerCurrency] Not enough coins! Have {Coins}, need {amount}");
        return false;
    }

    public bool SpendGems(int amount)
    {
        if (Gems >= amount)
        {
            Gems -= amount;
            if (debugLog) Debug.Log($"[PlayerCurrency] Spent {amount} gems. Remaining: {Gems}");
            return true;
        }
        if (debugLog) Debug.LogWarning($"[PlayerCurrency] Not enough gems! Have {Gems}, need {amount}");
        return false;
    }

    #region Debug
    [ContextMenu("Debug: Add 100 Coins")]
    public void DebugAdd100Coins() => AddCoins(100);

    [ContextMenu("Debug: Add 10 Gems")]
    public void DebugAdd10Gems() => AddGems(10);

    [ContextMenu("Debug: Reset Currency")]
    public void DebugResetCurrency()
    {
        Coins = 0;
        Gems = 0;
        if (debugLog) Debug.Log("[PlayerCurrency] Currency reset to 0.");
    }
    #endregion
}
