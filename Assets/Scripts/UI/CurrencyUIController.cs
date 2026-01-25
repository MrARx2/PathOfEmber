using UnityEngine;
using TMPro;

/// <summary>
/// dedicated controller for the Persistent Currency UI (Coins/Gems).
/// Attach this to the "Persistent UI Root" or the object holding the Coin/Gem texts.
/// This ensures currency updates work regardless of the Main Menu state.
/// </summary>
public class CurrencyUIController : MonoBehaviour
{
    [Header("Currency Texts")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI gemsText;

    private void Start()
    {
        UpdateDisplay();
        
        // Subscribe to currency changes
        if (PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.OnCurrencyChanged += UpdateDisplay;
        }
    }

    private void OnDestroy()
    {
        if (PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.OnCurrencyChanged -= UpdateDisplay;
        }
    }

    private void UpdateDisplay()
    {
        if (PlayerCurrency.Instance == null) return;

        if (coinsText != null)
            coinsText.text = PlayerCurrency.Instance.Coins.ToString();
        
        if (gemsText != null)
            gemsText.text = PlayerCurrency.Instance.Gems.ToString();
    }
}
