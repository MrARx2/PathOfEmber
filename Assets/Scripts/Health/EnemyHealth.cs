using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Health component for enemies with built-in UI health bar support.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Configuration")]
    [SerializeField] private int maxHealth = 100;
    
    [SerializeField, Tooltip("Optional: Assign the model child transform for accurate effect positioning")]
    private Transform modelTransform;
    
    [Header("State")]
    [SerializeField] private int currentHealth;

    [Header("Health Bar UI")]
    [Tooltip("Optional: Assign a transform to anchor the health bar to (e.g. above head)")]
    public Transform HealthBarPoint;

    [Header("Events")]
    public UnityEvent<int> OnDamage;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;

    [Header("Death Settings")]
    [SerializeField, Tooltip("Automatically destroy the enemy after death")]
    private bool destroyOnDeath = true;
    [SerializeField, Tooltip("Delay before destroying (for death animations/effects)")]
    private float destroyDelay = 0.5f;
    [SerializeField, Tooltip("Optional VFX prefab to spawn on death")]
    private GameObject deathVFXPrefab;

    [Header("Freeze Settings")]
    [SerializeField, Tooltip("Time enemy cannot be frozen again after unfreeze")]
    private float freezeImmunityDuration = 5f;
    [SerializeField, Tooltip("Tint color when frozen")]
    private Color freezeTintColor = new Color(0f, 1f, 1f, 1f); // Cyan

    [Header("Venom Settings")]
    [SerializeField, Tooltip("Tint color when poisoned")]
    private Color venomTintColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple

    [Header("Hit Flash Settings")]
    [SerializeField, Tooltip("Enable flash effect when hit")]
    private bool enableHitFlash = true;
    [SerializeField, Tooltip("Emission color when hit (uses emission for visibility through lighting)")]
    private Color hitFlashEmissionColor = new Color(1f, 0.3f, 0.3f, 1f); // Bright red-orange
    [SerializeField, Tooltip("Emission intensity multiplier")]
    private float hitFlashEmissionIntensity = 2f;
    [SerializeField, Tooltip("Duration of hit flash in seconds")]
    private float hitFlashDuration = 0.1f;
    [SerializeField, Tooltip("Optional VFX prefab to spawn when hit")]
    private GameObject hitVFXPrefab;

    [Header("Damage Popup Settings")]
    [SerializeField, Tooltip("Enable floating damage numbers (requires DamagePopupManager in scene)")]
    private bool showDamagePopup = true;
    [SerializeField, Tooltip("Offset from enemy position for popup spawn")]
    private Vector3 damagePopupOffset = new Vector3(0, 1.5f, 0);

    [Header("Stagger & Knockback")]
    [SerializeField, Tooltip("Enable stagger effect when hit")]
    private bool enableStagger = true;
    [SerializeField, Tooltip("Duration of stagger (enemy stops moving)")]
    private float staggerDuration = 0.15f;
    [SerializeField, Tooltip("Enable knockback effect when hit")]
    private bool enableKnockback = true;
    [SerializeField, Tooltip("Knockback force applied")]
    private float knockbackForce = 2f;
    [SerializeField, Tooltip("How quickly knockback decays")]
    private float knockbackDecay = 10f;
    [SerializeField, Tooltip("Layers that block knockback (walls, obstacles)")]
    private LayerMask knockbackBlockLayers;
    [SerializeField, Tooltip("Radius for wall collision check")]
    private float knockbackCollisionRadius = 0.5f;

    private bool isDead = false;
    private Coroutine dotCoroutine;
    private Coroutine freezeCoroutine;
    private Coroutine hitFlashCoroutine;
    private bool isFrozen = false;
    private bool isVenomed = false;
    private float freezeImmunityTimer = 0f;
    private Animator animator;
    private UnityEngine.AI.NavMeshAgent navAgent;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Color[] originalEmissionColors;
    private bool[] hadEmissionEnabled;
    private bool isStaggered = false;
    private Vector3 knockbackVelocity = Vector3.zero;

    /// <summary>
    /// Returns the visual center position (modelTransform if set, otherwise this transform).
    /// </summary>
    public Vector3 VisualCenter => modelTransform != null ? modelTransform.position : transform.position;

    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsFrozen => isFrozen;
    public bool IsVenomed => isVenomed;

    private void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponentInChildren<Animator>();
        navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        
        // Cache only MeshRenderers and SkinnedMeshRenderers for tinting/emission
        // Exclude ParticleSystemRenderer, TrailRenderer, LineRenderer, etc.
        var allRenderers = GetComponentsInChildren<Renderer>();
        var validRenderers = new System.Collections.Generic.List<Renderer>();
        foreach (var r in allRenderers)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                if (r.material != null)
                {
                    validRenderers.Add(r);
                }
            }
        }
        
        renderers = validRenderers.ToArray();
        originalColors = new Color[renderers.Length];
        originalEmissionColors = new Color[renderers.Length];
        hadEmissionEnabled = new bool[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
            // Cache original emission state
            if (renderers[i].material.HasProperty("_EmissionColor"))
            {
                originalEmissionColors[i] = renderers[i].material.GetColor("_EmissionColor");
                hadEmissionEnabled[i] = renderers[i].material.IsKeywordEnabled("_EMISSION");
            }
        }
    }

    private void Update()
    {
        if (freezeImmunityTimer > 0f)
        {
            freezeImmunityTimer -= Time.deltaTime;
        }
        
        // Apply knockback movement with wall collision check
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 movement = knockbackVelocity * Time.deltaTime;
            
            // Check for walls in knockback direction
            if (!IsBlockedByWall(movement))
            {
                transform.position += movement;
            }
            else
            {
                // Hit a wall, stop knockback
                knockbackVelocity = Vector3.zero;
            }
            
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }
    }
    
    private bool IsBlockedByWall(Vector3 movement)
    {
        if (knockbackBlockLayers == 0)
        {
            Debug.LogWarning($"[EnemyHealth] {gameObject.name} - Knockback Block Layers not set! Set it to 'Walls' layer.");
            return false;
        }
        
        float distance = movement.magnitude;
        if (distance < 0.001f) return false;
        
        Vector3 direction = movement.normalized;
        float moveDist = movement.magnitude;
        Vector3 origin = transform.position + Vector3.up * 0.1f; // Low to detect short walls
        
        // Check ahead using the configured collision radius
        RaycastHit hit;
        bool blocked = Physics.SphereCast(origin, knockbackCollisionRadius, direction, out hit, moveDist + knockbackCollisionRadius, knockbackBlockLayers, QueryTriggerInteraction.Ignore);
        
        // Debug: Draw the check in scene view
        Debug.DrawRay(origin, direction * (moveDist + knockbackCollisionRadius), blocked ? Color.red : Color.green, 0.1f);
        
        if (blocked)
        {
            // Calculate how far we can move before hitting the wall
            float safeDistance = Mathf.Max(0, hit.distance - knockbackCollisionRadius);
            
            if (safeDistance > 0.01f)
            {
                // Allow partial movement up to the wall
                transform.position += direction * safeDistance;
            }
            
            // Stop remaining knockback
            knockbackVelocity = Vector3.zero;
            return true;
        }
        
        return false;
    }
    
    // Debug: Show knockback collision sphere
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawWireSphere(origin, knockbackCollisionRadius);
    }

    private void Start()
    {
        // Register with global manager
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.Register(this);
        }
    }

    private void OnDisable()
    {
        // Clean up
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.Unregister(this);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (damage <= 0) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        // Trigger hit flash effect
        if (enableHitFlash && !isFrozen && !isVenomed)
        {
            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }
        
        // Spawn hit VFX at visual center
        if (hitVFXPrefab != null)
        {
            GameObject vfx = Instantiate(hitVFXPrefab, VisualCenter, Quaternion.identity);
            Destroy(vfx, 2f); // Auto-cleanup
        }
        
        // Spawn damage popup via manager
        if (showDamagePopup && DamagePopupManager.Instance != null)
        {
            Vector3 popupPos = VisualCenter + damagePopupOffset;
            DamagePopupManager.Instance.ShowDamage(damage, popupPos);
        }

        OnDamage?.Invoke(damage);
        OnHealthChanged?.Invoke((float)currentHealth / maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>
    /// Takes damage and applies knockback in hit direction.
    /// Call this from projectiles/attacks that have directional impact.
    /// </summary>
    public void TakeDamageWithKnockback(int damage, Vector3 hitDirection)
    {
        TakeDamage(damage);
        
        if (isDead) return;
        
        // Apply knockback
        if (enableKnockback && hitDirection.sqrMagnitude > 0.01f)
        {
            ApplyKnockback(hitDirection.normalized);
        }
        
        // Apply stagger
        if (enableStagger && !isFrozen)
        {
            StartCoroutine(StaggerRoutine());
        }
    }

    /// <summary>
    /// Applies knockback velocity in the given direction.
    /// </summary>
    public void ApplyKnockback(Vector3 direction)
    {
        if (isDead || isFrozen) return;
        
        // Ensure horizontal knockback only
        direction.y = 0;
        knockbackVelocity = direction.normalized * knockbackForce;
    }

    private Coroutine staggerCoroutine;
    
    private IEnumerator StaggerRoutine()
    {
        if (isStaggered) yield break;
        isStaggered = true;
        
        // Stop NavMeshAgent briefly
        bool wasAgentEnabled = false;
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            wasAgentEnabled = true;
            navAgent.isStopped = true;
        }
        
        yield return new WaitForSeconds(staggerDuration);
        
        // Resume movement
        if (navAgent != null && wasAgentEnabled && !isFrozen && !isDead)
        {
            navAgent.isStopped = false;
        }
        
        isStaggered = false;
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        OnHealthChanged?.Invoke((float)currentHealth / maxHealth);
    }

    public void ApplyDamageOverTime(int damagePerTick, float tickInterval, int totalTicks)
    {
        if (dotCoroutine != null)
            StopCoroutine(dotCoroutine);
        dotCoroutine = StartCoroutine(DamageOverTimeRoutine(damagePerTick, tickInterval, totalTicks));
    }

    private IEnumerator HitFlashRoutine()
    {
        ApplyEmission(hitFlashEmissionColor, hitFlashEmissionIntensity);
        yield return new WaitForSeconds(hitFlashDuration);
        ClearEmission();
        
        hitFlashCoroutine = null;
    }

    private IEnumerator DamageOverTimeRoutine(int damagePerTick, float tickInterval, int totalTicks)
    {
        // Apply venom tint
        isVenomed = true;
        ApplyTint(venomTintColor);
        Debug.Log($"[EnemyHealth] {gameObject.name} venom tint applied");

        for (int i = 0; i < totalTicks; i++)
        {
            if (isDead) 
            {
                isVenomed = false;
                ClearTint();
                dotCoroutine = null;
                yield break;
            }
            TakeDamage(damagePerTick);
            yield return new WaitForSeconds(tickInterval);
        }
        
        // Clear venom tint
        isVenomed = false;
        if (!isFrozen) // Don't clear if still frozen
        {
            ClearTint();
        }
        else
        {
            ApplyTint(freezeTintColor); // Restore freeze tint
        }
        Debug.Log($"[EnemyHealth] {gameObject.name} venom effect ended");
        dotCoroutine = null;
    }

    /// <summary>
    /// Applies freeze effect to the enemy for the specified duration.
    /// Enemy cannot be frozen again for freezeImmunityDuration after unfreeze.
    /// </summary>
    public void ApplyFreeze(float duration)
    {
        if (isDead) return;
        if (isFrozen) return;
        if (freezeImmunityTimer > 0f)
        {
            Debug.Log($"[EnemyHealth] {gameObject.name} is immune to freeze for {freezeImmunityTimer:F1}s");
            return;
        }

        if (freezeCoroutine != null)
            StopCoroutine(freezeCoroutine);
        freezeCoroutine = StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        isFrozen = true;

        // Apply freeze tint
        ApplyTint(freezeTintColor);
        Debug.Log($"[EnemyHealth] {gameObject.name} freeze tint applied");

        // Stop animator
        float originalSpeed = 1f;
        if (animator != null)
        {
            originalSpeed = animator.speed;
            animator.speed = 0f;
        }

        // Stop NavMeshAgent
        bool wasEnabled = false;
        if (navAgent != null)
        {
            wasEnabled = navAgent.enabled;
            navAgent.isStopped = true;
        }

        Debug.Log($"[EnemyHealth] {gameObject.name} frozen for {duration}s");

        yield return new WaitForSeconds(duration);

        // Restore animator
        if (animator != null)
        {
            animator.speed = originalSpeed;
        }

        // Restore NavMeshAgent
        if (navAgent != null && wasEnabled)
        {
            navAgent.isStopped = false;
        }

        isFrozen = false;
        freezeImmunityTimer = freezeImmunityDuration;
        freezeCoroutine = null;

        // Clear freeze tint (restore venom tint if still poisoned)
        if (isVenomed)
        {
            ApplyTint(venomTintColor);
        }
        else
        {
            ClearTint();
        }

        Debug.Log($"[EnemyHealth] {gameObject.name} unfrozen, immune for {freezeImmunityDuration}s");
    }

    private void Die()
    {
        if (isDead) return; // Prevent double death
        isDead = true;
        
        // Stop all coroutines (DoT, Freeze, etc.)
        StopAllCoroutines();
        dotCoroutine = null;
        freezeCoroutine = null;
        
        // Clear any visual effects
        ClearTint();
        
        // Disable AI/movement immediately
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }
        
        // Invoke death event (for external listeners like HealthBarManager)
        OnDeath?.Invoke();
        
        Debug.Log($"[EnemyHealth] {gameObject.name} died.");
        
        // Spawn death VFX if assigned
        if (deathVFXPrefab != null)
        {
            GameObject vfx = Instantiate(deathVFXPrefab, VisualCenter, Quaternion.identity);
            Destroy(vfx, 3f); // Auto-cleanup VFX after 3 seconds
        }
        
        // Destroy the enemy
        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    public Transform GetTransform() => transform;

    #region Tint Helpers
    /// <summary>
    /// Applies a color tint to all renderers on this enemy.
    /// </summary>
    private void ApplyTint(Color tintColor)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                renderers[i].material.color = tintColor;
            }
        }
    }

    /// <summary>
    /// Restores all renderers to their original colors.
    /// </summary>
    private void ClearTint()
    {
        if (renderers == null || originalColors == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null && i < originalColors.Length)
            {
                renderers[i].material.color = originalColors[i];
            }
        }
    }
    #endregion

    #region Emission Helpers
    /// <summary>
    /// Applies emission to all renderers for hit flash effect (visible through lighting).
    /// </summary>
    private void ApplyEmission(Color emissionColor, float intensity)
    {
        if (renderers == null) return;
        Color finalEmission = emissionColor * intensity;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                Material mat = renderers[i].material;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", finalEmission);
                }
            }
        }
    }

    /// <summary>
    /// Clears emission from all renderers, restoring original emission state.
    /// </summary>
    private void ClearEmission()
    {
        if (renderers == null || originalEmissionColors == null || hadEmissionEnabled == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null && i < originalEmissionColors.Length)
            {
                Material mat = renderers[i].material;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", originalEmissionColors[i]);
                    if (!hadEmissionEnabled[i])
                    {
                        mat.DisableKeyword("_EMISSION");
                    }
                }
            }
        }
    }
    #endregion

    // ========== DEBUG MENU ==========
    [ContextMenu("Debug: Take 10 Damage")]
    public void DebugDamage10() => TakeDamage(10);

    [ContextMenu("Debug: Take 25 Damage")]
    public void DebugDamage25() => TakeDamage(25);

    [ContextMenu("Debug: Take 50 Damage")]
    public void DebugDamage50() => TakeDamage(50);

    [ContextMenu("Debug: Heal 20")]
    public void DebugHeal20() => Heal(20);

    [ContextMenu("Debug: Heal Full")]
    public void DebugHealFull() => Heal(maxHealth);

    [ContextMenu("Debug: Freeze 2s")]
    public void DebugFreeze2s() => ApplyFreeze(2f);

    [ContextMenu("Debug: Venom DoT (100 x 3)")]
    public void DebugVenomDoT() => ApplyDamageOverTime(100, 1f, 3);

    [ContextMenu("Debug: Fire DoT (20 x 5)")]
    public void DebugFireDoT() => ApplyDamageOverTime(20, 1f, 5);

    [ContextMenu("Debug: Kill Enemy")]
    public void DebugKill() => TakeDamage(currentHealth);
}
