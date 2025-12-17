using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Configuration")]
    [SerializeField] private int maxHealth = 1000;
    
    [Header("State")]
    [SerializeField] private int currentHealth;
    [SerializeField] private bool isInvulnerable = false;

    [Header("Health Bar UI")]
    [Tooltip("Optional: Anchor point for the floating health bar (default is above head)")]
    public Transform HealthBarPoint;

    [Header("Invulnerability Visual")]
    [Tooltip("Child GameObject with invulnerability effect")]
    [SerializeField] private GameObject invulnerabilityEffect;

    [Header("One-Time Shield")]
    [SerializeField] private bool hasOneTimeShield = false;
    [SerializeField, Tooltip("Visual effect for active shield")]
    private GameObject shieldEffect;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string healTrigger = "Heal";
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string deathTrigger = "Death";

    [Header("Events")]
    public UnityEvent<int> OnDamage;
    public UnityEvent<int> OnHeal;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;

    private bool isDead = false;
    private Coroutine dotCoroutine;

    public bool IsInvulnerable => isInvulnerable;
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (invulnerabilityEffect != null)
            invulnerabilityEffect.SetActive(false);
    }

    private void Start()
    {
        // Register with global manager for floating bar
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.Register(this);
        }

        NotifyHealthChanged();
    }

    private void LateUpdate()
    {
        // No billboard logic needed for HUD
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (isInvulnerable) return;
        if (damage <= 0) return;

        // Check for one-time shield
        if (hasOneTimeShield)
        {
            hasOneTimeShield = false;
            if (shieldEffect != null)
                shieldEffect.SetActive(false);
            
            // Notify PlayerAbilities if present
            var abilities = GetComponent<PlayerAbilities>();
            if (abilities != null)
                abilities.OnShieldConsumed();
            
            Debug.Log("[PlayerHealth] One-Time Shield blocked damage!");
            return;
        }

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        Debug.Log($"[PlayerHealth] Took {damage} damage. Current: {currentHealth}/{maxHealth}");

        if (animator != null && !string.IsNullOrEmpty(hitTrigger))
            animator.SetTrigger(hitTrigger);

        OnDamage?.Invoke(damage);
        UpdateHealthBar();
        NotifyHealthChanged();

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>
    /// Sets the one-time shield state (used by PlayerAbilities).
    /// </summary>
    public void SetOneTimeShield(bool enabled)
    {
        hasOneTimeShield = enabled;
        if (shieldEffect != null)
            shieldEffect.SetActive(enabled);
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        if (animator != null && !string.IsNullOrEmpty(healTrigger))
            animator.SetTrigger(healTrigger);

        OnHeal?.Invoke(amount);
        UpdateHealthBar();
        NotifyHealthChanged();
    }

    public void IncreaseMaxHealth(float percentage)
    {
        int increase = Mathf.RoundToInt(maxHealth * percentage);
        if (increase > 0)
        {
            maxHealth += increase;
            currentHealth += increase;
            UpdateHealthBar();
            NotifyHealthChanged();
        }
    }

    public void ApplyDamageOverTime(int damagePerTick, float tickInterval, int totalTicks)
    {
        if (dotCoroutine != null)
            StopCoroutine(dotCoroutine);
        dotCoroutine = StartCoroutine(DamageOverTimeRoutine(damagePerTick, tickInterval, totalTicks));
    }

    private IEnumerator DamageOverTimeRoutine(int damagePerTick, float tickInterval, int totalTicks)
    {
        for (int i = 0; i < totalTicks; i++)
        {
            if (isDead) yield break;
            TakeDamage(damagePerTick);
            yield return new WaitForSeconds(tickInterval);
        }
        dotCoroutine = null;
    }

    public void SetInvulnerable(float duration)
    {
        Debug.Log($"[PlayerHealth] SetInvulnerable called for {duration}s");
        StartCoroutine(InvulnerabilityRoutine(duration));
    }

    private IEnumerator InvulnerabilityRoutine(float duration)
    {
        isInvulnerable = true;
        SetInvulnerabilityEffectActive(true);
        Debug.Log($"[PlayerHealth] Invulnerability started for {duration}s");

        yield return new WaitForSeconds(duration);

        isInvulnerable = false;
        SetInvulnerabilityEffectActive(false);
        Debug.Log("[PlayerHealth] Invulnerability ended");
    }

    private void SetInvulnerabilityEffectActive(bool active)
    {
        if (invulnerabilityEffect == null)
        {
            Debug.LogWarning("[PlayerHealth] invulnerabilityEffect is null!");
            return;
        }

        // Make sure the GameObject is active/inactive
        invulnerabilityEffect.SetActive(active);
        Debug.Log($"[PlayerHealth] invulnerabilityEffect.SetActive({active}), activeInHierarchy={invulnerabilityEffect.activeInHierarchy}");
        
        // Also check if there's a parent being disabled
        if (active && !invulnerabilityEffect.activeInHierarchy)
        {
            Debug.LogWarning("[PlayerHealth] invulnerabilityEffect is not active in hierarchy despite SetActive(true). Check parent GameObjects!");
        }
    }

    private void OnDisable()
    {
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.Unregister(this);
        }
    }

    private void UpdateHealthBar()
    {
        // Handled by HealthBarManager now
    }

    private void Die()
    {
        isDead = true;

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        OnDeath?.Invoke();
        Debug.Log($"{gameObject.name} died.");
    }

    private void NotifyHealthChanged()
    {
        if (maxHealth > 0)
            OnHealthChanged?.Invoke((float)currentHealth / maxHealth);
    }

    public Transform GetTransform() => transform;

    // ========== DEBUG ==========
    [ContextMenu("Debug: Take 100 Damage")]
    public void DebugDamage100() => TakeDamage(100);

    [ContextMenu("Debug: Take 250 Damage")]
    public void DebugDamage250() => TakeDamage(250);

    [ContextMenu("Debug: Heal 100")]
    public void DebugHeal100() => Heal(100);

    [ContextMenu("Debug: Heal Full")]
    public void DebugHealFull() => Heal(maxHealth);

    [ContextMenu("Debug: Fire DoT (50 x 5)")]
    public void DebugFireDoT() => ApplyDamageOverTime(50, 1f, 5);

    [ContextMenu("Debug: Venom DoT (100 x 3)")]
    public void DebugVenomDoT() => ApplyDamageOverTime(100, 1f, 3);

    [ContextMenu("Debug: Invulnerable 2s")]
    public void DebugInvulnerable2() => SetInvulnerable(2f);

    [ContextMenu("Debug: +10% Max HP")]
    public void DebugIncreaseMaxHP() => IncreaseMaxHealth(0.1f);

    [ContextMenu("Debug: Kill Player")]
    public void DebugKill() => TakeDamage(currentHealth);
}
