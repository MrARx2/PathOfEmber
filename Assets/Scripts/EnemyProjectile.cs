using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 8f;
    [SerializeField, Tooltip("Seconds before auto-destroy")] private float lifetime = 5f;
    [SerializeField, Tooltip("Damage dealt to player on hit")] private int damage = 100;
    [SerializeField, Tooltip("Destroy when hitting the player")] private bool destroyOnHit = true;

    [Header("Optional: Fire Effect (DoT)")]
    [SerializeField, Tooltip("If true, applies damage over time instead of instant damage")]
    private bool applyDoT = false;
    [SerializeField] private int dotDamagePerTick = 50;
    [SerializeField] private float dotTickInterval = 1f;
    [SerializeField] private int dotTotalTicks = 3;

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

    [Header("VFX")]
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private float hitVFXDuration = 2f;

    [Header("Collision Layers")]
    [SerializeField, Tooltip("Layers that trigger the projectile impact")] 
    private LayerMask hitLayers;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        
        // 1. Check if the object is in our hit layers
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

        // 2. Ignore enemies specifically (extra safety)
        if (other.CompareTag("Enemy")) return;
        
        // 3. Ignore triggers if they aren't the player
        if (other.isTrigger && !other.CompareTag("Player")) return;

        bool isValidHit = false;

        // Try to damage the player
        if (other.CompareTag("Player"))
        {
            isValidHit = true;
            var playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = other.GetComponentInParent<PlayerHealth>();
            
            if (playerHealth != null)
            {
                if (applyDoT)
                {
                    // Use damage as initial hit, or fallback to first tick damage if damage is 0
                    int initialHitDamage = damage > 0 ? damage : dotDamagePerTick;
                    playerHealth.ApplyDamageOverTime(dotDamagePerTick, dotTickInterval, dotTotalTicks, initialHitDamage);
                }
                else
                {
                    playerHealth.TakeDamage(damage);
                }
            }
        }
        else
        {
            // It matched the layer mask and wasn't filtered out, so it's a valid environment hit
            isValidHit = true;
        }

        if (isValidHit && destroyOnHit)
        {
            if (hitVFXPrefab != null)
            {
                GameObject vfx = Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, hitVFXDuration);
            }
            Destroy(gameObject);
        }
    }
}
