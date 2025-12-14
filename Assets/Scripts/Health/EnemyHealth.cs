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

    private bool isDead = false;
    private Coroutine dotCoroutine;

    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
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
        for (int i = 0; i < totalTicks; i++)
        {
            if (isDead) yield break;
            TakeDamage(damagePerTick);
            yield return new WaitForSeconds(tickInterval);
        }
        dotCoroutine = null;
    }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke();
        Debug.Log($"{gameObject.name} died.");
    }

    public Transform GetTransform() => transform;

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

    [ContextMenu("Debug: Venom DoT (10 x 3)")]
    public void DebugVenomDoT() => ApplyDamageOverTime(10, 1f, 3);

    [ContextMenu("Debug: Fire DoT (20 x 5)")]
    public void DebugFireDoT() => ApplyDamageOverTime(20, 1f, 5);

    [ContextMenu("Debug: Kill Enemy")]
    public void DebugKill() => TakeDamage(currentHealth);
}
