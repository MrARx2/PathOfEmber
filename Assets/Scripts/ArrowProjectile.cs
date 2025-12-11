using UnityEngine;
using UnityEngine.Events;

public class ArrowProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField, Tooltip("Seconds before auto-destroy")] private float lifetime = 3f;
    [SerializeField, Tooltip("Damage dealt on hit")] private int damage = 50;
    [SerializeField] private bool destroyOnHit = true;

    [Header("Events")]
    [Tooltip("Fires when arrow hits something damageable - use for UI effects")]
    public UnityEvent<int> OnHitDamageable;
    public UnityEvent OnHitAnything;

    private Vector3 moveDir = Vector3.forward;
    private float lifeTimer;
    private Rigidbody rb;

    public int Damage => damage;

    private void OnEnable()
    {
        lifeTimer = lifetime;
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        var cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].isTrigger = true;
        }
        
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude > 1e-6f)
        {
            moveDir = dir.normalized;
            transform.forward = moveDir;
        }
    }
    
    public void SetSpeed(float newSpeed)
    {
        if (newSpeed > 0f)
            speed = newSpeed;
    }

    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        transform.position += moveDir * speed * dt;

        lifeTimer -= dt;
        if (lifeTimer <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Player")) return; // Don't hit the player who shot this
        
        // Try to find IDamageable on the object or its parent
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();
        
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            OnHitDamageable?.Invoke(damage);
            Debug.Log($"Arrow hit {other.name} for {damage} damage");
            
            if (destroyOnHit)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        // Hit something non-damageable (wall, etc.)
        OnHitAnything?.Invoke();
    }
}
