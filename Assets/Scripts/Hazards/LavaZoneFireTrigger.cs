using UnityEngine;

/// <summary>
/// Simple trigger zone that applies fire state to the player while inside.
/// Uses the "low priority" fire source system:
/// - Fire activates when player enters.
/// - Fire deactivates when player exits ONLY if no other fire sources (projectiles, etc.) are active.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LavaZoneFireTrigger : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField, Tooltip("If enabled, logs debug messages")]
    private bool debugMode = false;

    private PlayerHealth playerInZone;

    private void Start()
    {
        // Ensure collider is a trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[LavaZoneFireTrigger] Collider on '{gameObject.name}' is not a trigger! Setting isTrigger = true.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health == null)
        {
            // Try parent (in case collider is on child)
            health = other.GetComponentInParent<PlayerHealth>();
        }

        if (health != null)
        {
            playerInZone = health;
            health.AddFireSource();

            if (debugMode)
                Debug.Log($"[LavaZoneFireTrigger] Player entered '{gameObject.name}'. Fire source added.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = other.GetComponentInParent<PlayerHealth>();
        }

        if (health != null && health == playerInZone)
        {
            health.RemoveFireSource();
            playerInZone = null;

            if (debugMode)
                Debug.Log($"[LavaZoneFireTrigger] Player exited '{gameObject.name}'. Fire source removed.");
        }
    }

    private void OnDisable()
    {
        // Safety: If the zone is disabled while player is inside, remove the fire source
        if (playerInZone != null)
        {
            playerInZone.RemoveFireSource();
            playerInZone = null;
        }
    }
}
