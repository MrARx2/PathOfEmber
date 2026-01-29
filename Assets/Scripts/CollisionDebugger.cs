using UnityEngine;

/// <summary>
/// Attach to arrow or enemy to debug collision issues
/// </summary>
public class CollisionDebugger : MonoBehaviour
{
    [SerializeField] private bool debugLog = false;

    private void OnTriggerEnter(Collider other)
    {
        if (debugLog) Debug.Log($"[{gameObject.name}] TRIGGER with {other.name} (Tag: {other.tag})");
        
        var damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();
        
        if (damageable != null)
            if (debugLog) Debug.Log($"  → Found IDamageable: {damageable.GetType().Name}");
        else
            if (debugLog) Debug.Log($"  → NO IDamageable found!");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (debugLog) Debug.Log(collision.collider);
    }
}
