using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// dedicated controller for the Persistent Currency UI (Coins/Gems).
/// Attach this to the "Persistent UI Root" or the object holding the Coin/Gem texts.
/// This ensures currency updates work regardless of the Main Menu state.
/// </summary>
public class CurrencyUIController : MonoBehaviour
{
    private const string COINS_KEY = "PlayerCoins";
    private const string GEMS_KEY = "PlayerGems";
    
    [Header("Currency Texts")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI gemsText;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private bool hasUpdatedThisFrame = false;

    private void Start()
    {
        if (debugLog) Debug.Log("[CurrencyUIController] Start called");
        SubscribeToCurrencyChanges();
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(DelayedUpdateDisplay());
    }

    private void OnEnable()
    {
        if (debugLog) Debug.Log("[CurrencyUIController] OnEnable called");
        // Refresh display every time the UI becomes active
        SubscribeToCurrencyChanges();
        StartCoroutine(DelayedUpdateDisplay());
    }

    private void OnDisable()
    {
        UnsubscribeFromCurrencyChanges();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCurrencyChanges();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (debugLog) Debug.Log($"[CurrencyUIController] Scene loaded: {scene.name}");
        StartCoroutine(DelayedUpdateDisplay());
    }

    private void LateUpdate()
    {
        // Ensure we update display at least once per frame when visible
        // This catches any cases where events don't fire properly
        if (!hasUpdatedThisFrame && coinsText != null)
        {
            int currentCoins = PlayerPrefs.GetInt(COINS_KEY, 0);
            string currentText = coinsText.text;
            
            if (int.TryParse(currentText, out int displayedValue))
            {
                if (displayedValue != currentCoins)
                {
                    if (debugLog) Debug.Log($"[CurrencyUIController] LateUpdate fix: {displayedValue} -> {currentCoins}");
                    coinsText.text = currentCoins.ToString();
                }
            }
        }
        hasUpdatedThisFrame = false;
    }

    /// <summary>
    /// Waits for PlayerCurrency to be ready, then updates display.
    /// </summary>
    private IEnumerator DelayedUpdateDisplay()
    {
        // Wait multiple frames to ensure all systems are initialized
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        
        UpdateDisplayFromPlayerPrefs();
        hasUpdatedThisFrame = true;
        
        if (PlayerCurrency.Instance != null)
        {
            SubscribeToCurrencyChanges();
            UpdateDisplay();
            if (debugLog) Debug.Log($"[CurrencyUIController] Display updated via Instance. Coins: {PlayerCurrency.Instance.Coins}");
        }
        else
        {
            if (debugLog) Debug.LogWarning("[CurrencyUIController] PlayerCurrency.Instance is null, using PlayerPrefs directly");
        }
    }

    /// <summary>
    /// Updates display by reading directly from PlayerPrefs (bypass singleton).
    /// This is a fallback for timing issues with singleton initialization.
    /// </summary>
    private void UpdateDisplayFromPlayerPrefs()
    {
        int coins = PlayerPrefs.GetInt(COINS_KEY, 0);
        int gems = PlayerPrefs.GetInt(GEMS_KEY, 0);
        
        if (coinsText != null)
        {
            coinsText.text = coins.ToString();
            if (debugLog) Debug.Log($"[CurrencyUIController] Set coinsText from PlayerPrefs: {coins}");
        }
        
        if (gemsText != null)
        {
            gemsText.text = gems.ToString();
        }
    }

    private void SubscribeToCurrencyChanges()
    {
        if (PlayerCurrency.Instance != null)
        {
            // Avoid duplicate subscriptions
            PlayerCurrency.Instance.OnCurrencyChanged -= UpdateDisplay;
            PlayerCurrency.Instance.OnCurrencyChanged += UpdateDisplay;
        }
    }

    private void UnsubscribeFromCurrencyChanges()
    {
        if (PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.OnCurrencyChanged -= UpdateDisplay;
        }
    }

    private void UpdateDisplay()
    {
        hasUpdatedThisFrame = true;
        
        if (PlayerCurrency.Instance == null)
        {
            if (debugLog) Debug.LogWarning("[CurrencyUIController] UpdateDisplay: Instance null, falling back to PlayerPrefs");
            UpdateDisplayFromPlayerPrefs();
            return;
        }

        if (coinsText != null)
        {
            coinsText.text = PlayerCurrency.Instance.Coins.ToString();
            if (debugLog) Debug.Log($"[CurrencyUIController] Set coinsText to: {coinsText.text}");
        }
        else if (debugLog) Debug.LogWarning("[CurrencyUIController] coinsText is null!");
        
        if (gemsText != null)
            gemsText.text = PlayerCurrency.Instance.Gems.ToString();
    }
    
    /// <summary>
    /// Public method to force a display refresh.
    /// </summary>
    public void RefreshDisplay()
    {
        UpdateDisplayFromPlayerPrefs();
    }
}
