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
    
    [Header("State")]
    [SerializeField] private int currentHealth;

    [Header("Health Bar UI")]
    [Tooltip("Optional: Assign a transform to anchor the health bar to (e.g. above head)")]
    public Transform HealthBarPoint;

    [Header("Events")]
    public UnityEvent<int> OnDamage;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;

    [Header("Freeze Settings")]
    [SerializeField, Tooltip("Time enemy cannot be frozen again after unfreeze")]
    private float freezeImmunityDuration = 5f;
    [SerializeField, Tooltip("Tint color when frozen")]
    private Color freezeTintColor = new Color(0f, 1f, 1f, 1f); // Cyan

    [Header("Venom Settings")]
    [SerializeField, Tooltip("Tint color when poisoned")]
    private Color venomTintColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple

    private bool isDead = false;
    private Coroutine dotCoroutine;
    private Coroutine freezeCoroutine;
    private bool isFrozen = false;
    private bool isVenomed = false;
    private float freezeImmunityTimer = 0f;
    private Animator animator;
    private UnityEngine.AI.NavMeshAgent navAgent;
    private Renderer[] renderers;
    private Color[] originalColors;

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
        
        // Cache all renderers for tinting
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                originalColors[i] = renderers[i].material.color;
            }
        }
    }

    private void Update()
    {
        if (freezeImmunityTimer > 0f)
        {
            freezeImmunityTimer -= Time.deltaTime;
        }
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

        OnDamage?.Invoke(damage);
        OnHealthChanged?.Invoke((float)currentHealth / maxHealth);

        if (currentHealth <= 0)
            Die();
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
        isDead = true;
        ClearTint(); // Clear any tints on death
        OnDeath?.Invoke();
        Debug.Log($"{gameObject.name} died.");
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
