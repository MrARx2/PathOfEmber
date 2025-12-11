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

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Enemy")) return;
        
        // Try to damage the player
        if (other.CompareTag("Player"))
        {
            var playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = other.GetComponentInParent<PlayerHealth>();
            
            if (playerHealth != null)
            {
                if (applyDoT)
                {
                    playerHealth.ApplyDamageOverTime(dotDamagePerTick, dotTickInterval, dotTotalTicks);
                    Debug.Log($"Enemy projectile applied DoT to {other.name}");
                }
                else
                {
                    playerHealth.TakeDamage(damage);
                    Debug.Log($"Enemy projectile hit {other.name} for {damage} damage");
                }
            }

            if (destroyOnHit)
                Destroy(gameObject);
        }
    }
}
