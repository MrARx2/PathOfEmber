using UnityEngine;
using TMPro;

/// <summary>
/// Floating damage number that pops up and fades away.
/// Attach this to a prefab with a TextMeshPro component.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    // NOTE: Animation/Movement fields removed - PopupManager handles all animation.
    // These SerializedFields are kept on the prefab for PopupManager to reference if needed,
    // but the logic is in PopupManager, not here.

    private TextMeshPro textMesh;
    private TextMeshProUGUI textMeshUI;
    private Color originalColor;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        textMeshUI = GetComponent<TextMeshProUGUI>();
    }

    // NOTE: Start and Update methods removed to prevent conflict with PopupManager.
    // PopupManager handles position updates, animations, and object lifecycle (pooling).

    /// <summary>
    /// Set up the damage popup with the damage value.
    /// </summary>
    public void Setup(int damage, Color? color = null)
    {
        string text = damage.ToString();
        
        if (textMesh != null)
        {
            textMesh.text = text;
            if (color.HasValue)
            {
                textMesh.color = color.Value;
                originalColor = color.Value;
            }
        }
        else if (textMeshUI != null)
        {
            textMeshUI.text = text;
            if (color.HasValue)
            {
                textMeshUI.color = color.Value;
                originalColor = color.Value;
            }
        }
    }

    /// <summary>
    /// Set up with custom text (for "CRIT!", "MISS", etc.)
    /// </summary>
    public void Setup(string text, Color? color = null)
    {
        if (textMesh != null)
        {
            textMesh.text = text;
            if (color.HasValue)
            {
                textMesh.color = color.Value;
                originalColor = color.Value;
            }
        }
        else if (textMeshUI != null)
        {
            textMeshUI.text = text;
            if (color.HasValue)
            {
                textMeshUI.color = color.Value;
                originalColor = color.Value;
            }
        }
    }
}
