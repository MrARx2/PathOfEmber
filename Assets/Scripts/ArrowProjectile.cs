using UnityEngine;
using UnityEngine.Events;
using System.Collections;

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
    [SerializeField] private int piercingStacks = 0;
    [SerializeField] private int freezeShotStacks = 0;
    [SerializeField] private int venomShotStacks = 0;
    [SerializeField] private float freezeDuration = 1f;
    [SerializeField] private int venomDamagePerSecond = 100;
    [SerializeField] private float venomDuration = 3f;
    
    [Header("AOE Settings")]
    [SerializeField] private GameObject aoeEffectPrefab;
    [SerializeField] private float aoeActivationDelay = 0f; // 0 = instant
    
    [Header("Piercing Lifetime Extension")]
    [SerializeField] private float lifetimeExtensionPerHit = 2f;
    
    [Header("Bouncing Arrows")]
    [SerializeField] private bool hasBouncing = false;
    [SerializeField] private int maxBounces = 2;
    [SerializeField] private LayerMask wallLayers;
    
    [Header("Wall Hit VFX")]
    [SerializeField, Tooltip("VFX prefab to spawn when arrow hits a wall and can't bounce")]
    private GameObject wallHitVFXPrefab;
    [SerializeField, Tooltip("How long the VFX lasts before auto-destroy")]
    private float wallHitVFXDuration = 1f;
    
    [Header("Trail Colors")]
    [SerializeField] private Color freezeTrailColor = new Color(0.2f, 0.8f, 1f, 1f); // Cyan
    [SerializeField] private Color venomTrailColor = new Color(0.6f, 0.2f, 0.8f, 1f); // Purple
    
    private int bounceCount = 0;
    private Vector3 moveDir = Vector3.forward;
    private float lifeTimer;
    private Rigidbody rb;
    private TrailRenderer[] trails;
    private float[] initialTrailWidths;
    private Color[] originalTrailColors;
    
    // Track enemies already hit by this arrow (prevents double-damage from SphereCast + OnTriggerEnter)
    private System.Collections.Generic.HashSet<Collider> alreadyHitEnemies = new System.Collections.Generic.HashSet<Collider>();

    #region Public Properties
    public int Damage => damage;
    
    public bool IsPiercing => piercingStacks > 0;
    
    public int PiercingStacks
    {
        get => piercingStacks;
        set => piercingStacks = value;
    }
    
    public bool HasFreezeEffect => freezeShotStacks > 0;
    
    public int FreezeShotStacks
    {
        get => freezeShotStacks;
        set => freezeShotStacks = value;
    }
    
    public bool HasVenomEffect => venomShotStacks > 0;
    
    public int VenomShotStacks
    {
        get => venomShotStacks;
        set => venomShotStacks = value;
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
        bounceCount = 0;
        alreadyHitEnemies.Clear(); // Reset for pooled arrows
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
        
        // Cache trail renderers for fade effect (only once)
        if (trails == null || trails.Length == 0)
        {
            trails = GetComponentsInChildren<TrailRenderer>();
            initialTrailWidths = new float[trails.Length];
            originalTrailColors = new Color[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null)
                {
                    initialTrailWidths[i] = trails[i].widthMultiplier;
                    originalTrailColors[i] = trails[i].startColor;
                }
            }
        }
        
        // Clear trails for pooled reuse (prevents old trail appearing at new position)
        ClearTrails();
        
        // Reset effect flags for pooled arrows
        piercingStacks = 0;
        freezeShotStacks = 0;
        venomShotStacks = 0;
        hasBouncing = false;
    }
    
    /// <summary>
    /// Clears trail history for pooled arrow reuse.
    /// </summary>
    private void ClearTrails()
    {
        if (trails == null) return;
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
            {
                trails[i].Clear();
                trails[i].widthMultiplier = initialTrailWidths[i];
            }
        }
    }
    
    /// <summary>
    /// Apply trail color based on current effects. Call after setting effects.
    /// </summary>
    public void ApplyTrailColor()
    {
        if (trails == null) return;
        
        // Both effects - create gradient from freeze to venom
        if (HasFreezeEffect && HasVenomEffect)
        {
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null)
                {
                    trails[i].startColor = freezeTrailColor; // Cyan at start
                    trails[i].endColor = new Color(venomTrailColor.r, venomTrailColor.g, venomTrailColor.b, 0.3f); // Purple fading out
                }
            }
            return;
        }
        
        // Single effect
        if (HasFreezeEffect)
        {
            ApplySingleColor(freezeTrailColor);
            return;
        }
        
        if (HasVenomEffect)
        {
            ApplySingleColor(venomTrailColor);
            return;
        }
        
        // No effects - restore original colors
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null && originalTrailColors != null && i < originalTrailColors.Length)
            {
                trails[i].startColor = originalTrailColors[i];
                trails[i].endColor = new Color(originalTrailColors[i].r, originalTrailColors[i].g, originalTrailColors[i].b, 0f);
            }
        }
    }
    
    private void ApplySingleColor(Color color)
    {
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
            {
                trails[i].startColor = color;
                trails[i].endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }
    }

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude > 1e-6f)
        {
            // Flatten to horizontal (keep constant Y)
            dir.y = 0;
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
        
        // Simple raycast for wall detection (much cheaper than SphereCast)
        // Only check walls - enemy detection uses OnTriggerEnter (runs on physics thread)
        if (wallLayers != 0)
        {
            RaycastHit hit;
            float lookAhead = Mathf.Max(moveDistance * 1.5f, 0.3f);
            
            if (Physics.Raycast(transform.position, moveDir, out hit, lookAhead, wallLayers, QueryTriggerInteraction.Ignore))
            {
                // If bouncing is enabled and we have bounces left, bounce
                if (hasBouncing && bounceCount < maxBounces)
                {
                    HandleBounce(hit);
                    return; // Skip normal movement this frame
                }
                else
                {
                    // Spawn AOE on wall hit if freeze/venom stacks >= 2
                    bool freezeAOE = HasFreezeEffect && freezeShotStacks >= 2 && aoeEffectPrefab != null;
                    bool venomAOE = HasVenomEffect && venomShotStacks >= 2 && aoeEffectPrefab != null;
                    
                    if (freezeAOE && venomAOE)
                    {
                        SpawnWallAOE(hit.point, AOEEffectZone.EffectType.Both);
                    }
                    else if (freezeAOE)
                    {
                        SpawnWallAOE(hit.point, AOEEffectZone.EffectType.Freeze);
                    }
                    else if (venomAOE)
                    {
                        SpawnWallAOE(hit.point, AOEEffectZone.EffectType.Venom);
                    }
                    
                    // No bouncing or max bounces reached - spawn VFX and destroy
                    SpawnWallHitVFX(hit.point);
                    GracefulDestroy();
                    return;
                }
            }
        }
        
        // Normal movement
        transform.position += moveDir * moveDistance;

        lifeTimer -= dt;
        if (lifeTimer <= 0f)
            GracefulDestroy();
    }
    
    /// <summary>
    /// Processes damage and effects when hitting an enemy via SphereCast.
    /// Returns true if the arrow should be destroyed.
    /// </summary>
    private bool ProcessEnemyHit(Collider other)
    {
        // Try to find IDamageable on the object or its parent
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();
        
        if (damageable == null)
            return false; // Not an enemy we can damage
        
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

        // Determine AOE mode (combined if both have 2+ stacks)
        bool freezeAOE = HasFreezeEffect && freezeShotStacks >= 2 && aoeEffectPrefab != null;
        bool venomAOE = HasVenomEffect && venomShotStacks >= 2 && aoeEffectPrefab != null;
        
        if (freezeAOE && venomAOE)
        {
            ActivateEnemyAOE(enemyHealth, AOEEffectZone.EffectType.Both);
        }
        else if (freezeAOE)
        {
            ActivateEnemyAOE(enemyHealth, AOEEffectZone.EffectType.Freeze);
        }
        else if (venomAOE)
        {
            ActivateEnemyAOE(enemyHealth, AOEEffectZone.EffectType.Venom);
        }
        else
        {
            // Single target effects for stack 1
            if (HasFreezeEffect && enemyHealth != null)
            {
                enemyHealth.ApplyFreeze(freezeDuration);
            }
            if (HasVenomEffect && enemyHealth != null)
            {
                int totalTicks = Mathf.RoundToInt(venomDuration);
                enemyHealth.ApplyDamageOverTime(venomDamagePerSecond, 1f, totalTicks);
            }
        }
        
        OnHitAnything?.Invoke();
        
        // Piercing: extend lifetime if stacks > 1
        if (IsPiercing && piercingStacks > 1)
        {
            float extension = (piercingStacks - 1) * lifetimeExtensionPerHit;
            lifeTimer += extension;
        }
        
        // Only destroy if not piercing
        if (destroyOnHit && !IsPiercing)
        {
            GracefulDestroy();
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Activates the AOE zone child on an enemy (for enemy hits).
    /// </summary>
    private void ActivateEnemyAOE(EnemyHealth enemy, AOEEffectZone.EffectType effectType)
    {
        if (enemy == null) return;
        
        // Find AOE zone child on the enemy
        AOEEffectZone zone = enemy.GetComponentInChildren<AOEEffectZone>(includeInactive: true);
        if (zone != null)
        {
            float radius = GetCalculatedAOERadius();
            zone.Activate(effectType, radius, freezeDuration, venomDamagePerSecond, venomDuration, aoeActivationDelay);
        }
    }
    
    /// <summary>
    /// Spawns an AOE effect zone for wall hits (no enemy to attach to).
    /// </summary>
    private void SpawnWallAOE(Vector3 position, AOEEffectZone.EffectType effectType)
    {
        if (aoeEffectPrefab == null) return;
        
        GameObject aoe = Instantiate(aoeEffectPrefab, position, Quaternion.identity);
        AOEEffectZone zone = aoe.GetComponent<AOEEffectZone>();
        if (zone != null)
        {
            float radius = GetCalculatedAOERadius();
            zone.ConfigureAndStart(effectType, radius, freezeDuration, venomDamagePerSecond, venomDuration, aoeActivationDelay);
        }
    }
    
    /// <summary>
    /// Calculates AOE radius based on freeze/venom stacks.
    /// Stack 2 = base (0.3), Stack 3 = 0.5, Stack 4 = 0.7, Stack 5 = 1.0
    /// </summary>
    private float GetCalculatedAOERadius()
    {
        // Use the higher of freeze or venom stacks for radius calculation
        int effectiveStacks = Mathf.Max(freezeShotStacks, venomShotStacks);
        
        return effectiveStacks switch
        {
            <= 2 => 0.3f,  // Base radius for stack 2
            3 => 0.5f,     // Stack 3: 0.5
            4 => 0.7f,     // Stack 4: 0.7 (+0.2)
            >= 5 => 1.0f,  // Stack 5: 1.0 (+0.3) - max
        };
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
    }
    


    /// <summary>
    /// Registers a hit on an enemy collider. Returns true if this is the first time hitting this enemy.
    /// Used by ArrowExtensionZone to share hit history.
    /// </summary>
    public bool RegisterHit(Collider enemy)
    {
        if (alreadyHitEnemies.Contains(enemy)) return false;
        alreadyHitEnemies.Add(enemy);
        return true;
    }


    
    private void SpawnWallHitVFX(Vector3 hitPoint)
    {
        if (wallHitVFXPrefab != null)
        {
            GameObject vfx = Instantiate(wallHitVFXPrefab, hitPoint, Quaternion.identity);
            Destroy(vfx, wallHitVFXDuration);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Player")) return; // Don't hit the player who shot this
        
        // Skip if already hit by SphereCast this arrow's lifetime (prevents double damage)
        if (!RegisterHit(other)) return;
        
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

            // Determine AOE mode (combined if both have 2+ stacks)
            bool freezeAOE = HasFreezeEffect && freezeShotStacks >= 2 && aoeEffectPrefab != null;
            bool venomAOE = HasVenomEffect && venomShotStacks >= 2 && aoeEffectPrefab != null;
            
            if (freezeAOE && venomAOE)
            {
                ActivateEnemyAOE(enemyHealth, AOEEffectZone.EffectType.Both);
            }
            else if (freezeAOE)
            {
                ActivateEnemyAOE(enemyHealth, AOEEffectZone.EffectType.Freeze);
            }
            else if (venomAOE)
            {
                ActivateEnemyAOE(enemyHealth, AOEEffectZone.EffectType.Venom);
            }
            else
            {
                // Single target effects for stack 1
                if (HasFreezeEffect && enemyHealth != null)
                {
                    enemyHealth.ApplyFreeze(freezeDuration);
                }
                if (HasVenomEffect && enemyHealth != null)
                {
                    int totalTicks = Mathf.RoundToInt(venomDuration);
                    enemyHealth.ApplyDamageOverTime(venomDamagePerSecond, 1f, totalTicks);
                }
            }
            
            // Piercing: extend lifetime if stacks > 1
            if (IsPiercing && piercingStacks > 1)
            {
                float extension = (piercingStacks - 1) * lifetimeExtensionPerHit;
                lifeTimer += extension;
            }
            
            // Only destroy if not piercing
            if (destroyOnHit && !IsPiercing)
            {
                GracefulDestroy();
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
                GracefulDestroy();
            }
        }
    }
    
    private bool IsWallLayer(int layer)
    {
        return ((1 << layer) & wallLayers) != 0;
    }
    
    /// <summary>
    /// Returns the arrow to the pool for reuse (no allocation).
    /// Falls back to Destroy if pool is not available.
    /// </summary>
    public void GracefulDestroy()
    {
        // Guard against double-destroy (can happen from extension zone + main collider)
        if (!gameObject.activeInHierarchy) return;
        
        // Clear trails before returning to pool
        ClearTrails();
        
        // Return to pool instead of destroying
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Return(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Helper component that fades a detached trail smoothly.
/// </summary>
public class TrailFader : MonoBehaviour
{
    public void StartFade(TrailRenderer trail, float duration)
    {
        StartCoroutine(FadeRoutine(trail, duration));
    }
    
    private IEnumerator FadeRoutine(TrailRenderer trail, float duration)
    {
        if (trail == null)
        {
            Destroy(gameObject);
            yield break;
        }
        
        float initialWidth = trail.widthMultiplier;
        Gradient initialColorGradient = trail.colorGradient;
        
        // Get initial alpha keys
        GradientAlphaKey[] alphaKeys = initialColorGradient.alphaKeys;
        float[] initialAlphas = new float[alphaKeys.Length];
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            initialAlphas[i] = alphaKeys[i].alpha;
        }
        
        float elapsed = 0f;
        
        while (elapsed < duration && trail != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Use smooth easing: fast start, slow finish (feels more natural)
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
            float fadeValue = 1f - easedT;
            
            // Fade width with a nice taper
            trail.widthMultiplier = initialWidth * fadeValue;
            
            // Fade alpha
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha = initialAlphas[i] * fadeValue;
            }
            Gradient newGradient = new Gradient();
            newGradient.SetKeys(initialColorGradient.colorKeys, alphaKeys);
            trail.colorGradient = newGradient;
            
            yield return null;
        }
        
        // Clean up
        Destroy(gameObject);
    }
}
