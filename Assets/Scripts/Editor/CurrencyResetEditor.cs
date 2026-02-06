using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to reset player currency before building.
/// Accessible via Tools menu in Unity Editor.
/// </summary>
public class CurrencyResetEditor : Editor
{
    private const string COINS_KEY = "PlayerCoins";
    private const string GEMS_KEY = "PlayerGems";

    [MenuItem("Tools/Reset Currency/Reset All Currency")]
    public static void ResetAllCurrency()
    {
        PlayerPrefs.SetInt(COINS_KEY, 0);
        PlayerPrefs.SetInt(GEMS_KEY, 0);
        PlayerPrefs.Save();
        Debug.Log("[CurrencyReset] Coins and Gems reset to 0");
    }

    [MenuItem("Tools/Reset Currency/Reset Coins Only")]
    public static void ResetCoinsOnly()
    {
        PlayerPrefs.SetInt(COINS_KEY, 0);
        PlayerPrefs.Save();
        Debug.Log("[CurrencyReset] Coins reset to 0");
    }

    [MenuItem("Tools/Reset Currency/Reset Gems Only")]
    public static void ResetGemsOnly()
    {
        PlayerPrefs.SetInt(GEMS_KEY, 0);
        PlayerPrefs.Save();
        Debug.Log("[CurrencyReset] Gems reset to 0");
    }

    [MenuItem("Tools/Reset Currency/Show Current Values")]
    public static void ShowCurrentValues()
    {
        int coins = PlayerPrefs.GetInt(COINS_KEY, 0);
        int gems = PlayerPrefs.GetInt(GEMS_KEY, 0);
        Debug.Log($"[CurrencyReset] Current values - Coins: {coins}, Gems: {gems}");
    }
}
