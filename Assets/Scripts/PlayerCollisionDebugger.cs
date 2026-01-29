using UnityEngine;

/// <summary>
/// Debug utility to log all collisions and triggers on the player.
/// Attach to Player GameObject to see what the player is hitting.
/// </summary>
public class PlayerCollisionDebugger : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool logCollisions = false;
    [SerializeField] private bool logTriggers = false;
    [SerializeField] private bool logStay = false; // Usually too spammy
    [SerializeField] private bool showColliderInfo = true;
    [SerializeField] private bool showLayerInfo = true;
    [SerializeField] private bool showTagInfo = true;

    // ========== COLLISION EVENTS ==========
    
    private void OnCollisionEnter(Collision collision)
    {
        if (!debugLog || !logCollisions) return;
        Debug.Log($"[COLLISION ENTER] {FormatInfo(collision.gameObject)}", collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!debugLog || !logCollisions || !logStay) return;
        Debug.Log($"[COLLISION STAY] {FormatInfo(collision.gameObject)}", collision.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!debugLog || !logCollisions) return;
        Debug.Log($"[COLLISION EXIT] {FormatInfo(collision.gameObject)}", collision.gameObject);
    }

    // ========== TRIGGER EVENTS ==========
    
    private void OnTriggerEnter(Collider other)
    {
        if (!debugLog || !logTriggers) return;
        Debug.Log($"[TRIGGER ENTER] {FormatInfo(other.gameObject)}", other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!debugLog || !logTriggers || !logStay) return;
        Debug.Log($"[TRIGGER STAY] {FormatInfo(other.gameObject)}", other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!debugLog || !logTriggers) return;
        Debug.Log($"[TRIGGER EXIT] {FormatInfo(other.gameObject)}", other.gameObject);
    }

    // ========== FORMATTING ==========
    
    private string FormatInfo(GameObject obj)
    {
        string info = $"'{obj.name}'";

        if (showTagInfo && !string.IsNullOrEmpty(obj.tag) && obj.tag != "Untagged")
        {
            info += $" [Tag: {obj.tag}]";
        }

        if (showLayerInfo)
        {
            info += $" [Layer: {LayerMask.LayerToName(obj.layer)}]";
        }

        if (showColliderInfo)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
            {
                string colType = col.GetType().Name.Replace("Collider", "");
                info += $" [{colType}{(col.isTrigger ? " Trigger" : "")}]";
            }
        }

        return info;
    }
}
