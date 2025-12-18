using UnityEngine;

/// <summary>
/// Attach to arrow or enemy to debug collision issues
/// </summary>
public class CollisionDebugger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[{gameObject.name}] TRIGGER with {other.name} (Tag: {other.tag})");
        
        var damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();
        
        if (damageable != null)
            Debug.Log($"  → Found IDamageable: {damageable.GetType().Name}");
        else
            Debug.Log($"  → NO IDamageable found!");
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log(collision.collider);
    }
}
