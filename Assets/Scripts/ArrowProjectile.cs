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

    [Header("Power-Up Effects (Set by PlayerShooting)")]
    [SerializeField] private bool isPiercing = false;
    [SerializeField] private bool hasFreezeEffect = false;
    [SerializeField] private bool hasVenomEffect = false;
    [SerializeField] private float freezeDuration = 1f;
    [SerializeField] private int venomDamagePerSecond = 100;
    [SerializeField] private float venomDuration = 3f;
    
    [Header("Bouncing Arrows")]
    [SerializeField] private bool hasBouncing = false;
    [SerializeField] private int maxBounces = 2;
    [SerializeField] private LayerMask wallLayers;
    
    private int bounceCount = 0;
    private Vector3 moveDir = Vector3.forward;
    private float lifeTimer;
    private Rigidbody rb;

    #region Public Properties
    public int Damage => damage;
    
    public bool IsPiercing
    {
        get => isPiercing;
        set => isPiercing = value;
    }
    
    public bool HasFreezeEffect
    {
        get => hasFreezeEffect;
        set => hasFreezeEffect = value;
    }
    
    public bool HasVenomEffect
    {
        get => hasVenomEffect;
        set => hasVenomEffect = value;
    }
    
    public float FreezeDuration
    {
        get => freezeDuration;
        set => freezeDuration = value;
    }
    
    public int VenomDamagePerSecond
    {
        get => venomDamagePerSecond;
        set => venomDamagePerSecond = value;
    }
    
    public float VenomDuration
    {
        get => venomDuration;
        set => venomDuration = value;
    }
    
    public bool HasBouncing
    {
        get => hasBouncing;
        set => hasBouncing = value;
    }
    
    public int MaxBounces
    {
        get => maxBounces;
        set => maxBounces = value;
    }
    
    public LayerMask WallLayers
    {
        get => wallLayers;
        set => wallLayers = value;
    }
    #endregion

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
        float moveDistance = speed * dt;
        
        // Use a larger lookahead distance to prevent tunneling
        float lookAhead = Mathf.Max(moveDistance * 2f, 0.5f);
        
        // Check for wall collision using SphereCast for reliable detection
        if (hasBouncing && wallLayers != 0)
        {
            // Debug: draw raycast
            Debug.DrawRay(transform.position, moveDir * lookAhead, bounceCount < maxBounces ? Color.cyan : Color.red, 0.1f);
            
            RaycastHit hit;
            // Use SphereCast for more reliable detection (catches edges better)
            if (Physics.SphereCast(transform.position, 0.1f, moveDir, out hit, lookAhead, wallLayers, QueryTriggerInteraction.Ignore))
            {
                if (bounceCount < maxBounces)
                {
                    // Hit a wall! Bounce!
                    Debug.Log($"[ArrowProjectile] Wall detected! Bouncing off {hit.collider.name} ({bounceCount + 1}/{maxBounces})");
                    HandleBounce(hit);
                    return; // Skip normal movement this frame
                }
                else
                {
                    // Max bounces reached - destroy on wall hit
                    Debug.Log($"[ArrowProjectile] Max bounces reached. Destroyed on wall {hit.collider.name}");
                    Destroy(gameObject);
                    return;
                }
            }
        }
        else if (hasBouncing && wallLayers == 0)
        {
            Debug.LogWarning("[ArrowProjectile] Bouncing enabled but wallLayers is EMPTY! Set Wall Layers on Arrow prefab.");
        }
        
        // Normal movement
        transform.position += moveDir * moveDistance;

        lifeTimer -= dt;
        if (lifeTimer <= 0f)
            Destroy(gameObject);
    }
    
    private void HandleBounce(RaycastHit hit)
    {
        bounceCount++;
        
        // Calculate reflection using wall normal
        Vector3 newDir = Vector3.Reflect(moveDir, hit.normal);
        newDir.y = 0; // Keep flat (horizontal only)
        newDir.Normalize();
        
        // Apply new direction
        SetDirection(newDir);
        
        // Reset lifetime on bounce
        lifeTimer = lifetime;
        
        // Move to hit point + small offset in reflected direction
        transform.position = hit.point + newDir * 0.1f;
        
        Debug.Log($"[ArrowProjectile] Bounced off {hit.collider.name} ({bounceCount}/{maxBounces})");
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
            // Check if it's an EnemyHealth for knockback support
            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = other.GetComponentInParent<EnemyHealth>();
            
            if (enemyHealth != null)
            {
                // Use knockback-enabled damage with arrow's direction
                enemyHealth.TakeDamageWithKnockback(damage, moveDir);
            }
            else
            {
                // Fallback for other IDamageable types
                damageable.TakeDamage(damage);
            }
            
            OnHitDamageable?.Invoke(damage);
            Debug.Log($"Arrow hit {other.name} for {damage} damage");

            // Apply Freeze Effect (reuse enemyHealth from above)
            if (hasFreezeEffect && enemyHealth != null)
            {
                enemyHealth.ApplyFreeze(freezeDuration);
                Debug.Log($"Arrow applied freeze ({freezeDuration}s) to {other.name}");
            }

            // Apply Venom Effect (DoT)
            if (hasVenomEffect && enemyHealth != null)
            {
                int totalTicks = Mathf.RoundToInt(venomDuration);
                enemyHealth.ApplyDamageOverTime(venomDamagePerSecond, 1f, totalTicks);
                Debug.Log($"Arrow applied venom ({venomDamagePerSecond} dmg/s for {venomDuration}s) to {other.name}");
            }
            
            // Only destroy if not piercing
            if (destroyOnHit && !isPiercing)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        // Hit something non-damageable (wall, etc.)
        OnHitAnything?.Invoke();
        
        // Note: Wall bouncing is handled via raycast in Update(), not here
        // Destroy on wall hit (if not bouncing or max bounces reached)
        if (!other.CompareTag("Enemy") && destroyOnHit)
        {
            // Only destroy if we're not a bouncing arrow that still has bounces left
            if (!hasBouncing || bounceCount >= maxBounces)
            {
                Destroy(gameObject);
            }
        }
    }
    
    private bool IsWallLayer(int layer)
    {
        return ((1 << layer) & wallLayers) != 0;
    }
}
