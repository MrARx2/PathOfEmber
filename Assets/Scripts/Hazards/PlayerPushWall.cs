using UnityEngine;

/// <summary>
/// Pushes the player forward when they hit this trigger.
/// Place behind the hazard zone as a last-resort boundary.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerPushWall : MonoBehaviour
{
    [Header("Push Settings")]
    [SerializeField, Tooltip("Force applied to push player forward")]
    private float pushForce = 15f;
    
    [SerializeField, Tooltip("Direction to push (usually forward/+Z)")]
    private Vector3 pushDirection = Vector3.forward;
    
    [SerializeField, Tooltip("Optional: Deal damage on contact")]
    private bool dealDamage = true;
    
    [SerializeField, Tooltip("Damage dealt when player touches wall")]
    private int contactDamage = 100;
    
    [Header("Cooldown")]
    [SerializeField, Tooltip("Time before wall can push again (prevents spam)")]
    private float pushCooldown = 0.5f;
    
    private float lastPushTime;
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time - lastPushTime < pushCooldown) return;
        
        lastPushTime = Time.time;
        
        // Try to find CharacterController or Rigidbody
        CharacterController cc = other.GetComponent<CharacterController>();
        Rigidbody rb = other.GetComponent<Rigidbody>();
        
        // Apply push - different methods based on what the player has
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(pushDirection.normalized * pushForce, ForceMode.Impulse);
        }
        else
        {
            // For CharacterController: apply velocity directly via movement script
            // You may need to expose a method on your player movement script
            PlayerMovement movement = other.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                // If your PlayerMovement has a knockback method, call it here
                // movement.ApplyKnockback(pushDirection.normalized * pushForce);
            }
            
            // Fallback: teleport nudge
            other.transform.position += pushDirection.normalized * 2f;
        }
        
        // Deal damage
        if (dealDamage)
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(contactDamage);
            }
        }
        
        Debug.Log("[PlayerPushWall] Pushed player forward!");
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
        }
    }
}
