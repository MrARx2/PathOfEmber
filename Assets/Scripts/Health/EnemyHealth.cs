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
    [Tooltip("Assign a UI Image with Image Type = Filled")]
    [SerializeField] private Image healthBarFill;
    [Tooltip("Optional: Parent canvas/container to hide when dead")]
    [SerializeField] private GameObject healthBarContainer;
    [Tooltip("Should the health bar face the camera?")]
    [SerializeField] private bool billboardHealthBar = true;

    [Header("Events")]
    public UnityEvent<int> OnDamage;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;

    private bool isDead = false;
    private Camera mainCam;
    private Coroutine dotCoroutine;

    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        mainCam = Camera.main;
    }

    private void Start()
    {
        UpdateHealthBar();
    }

    private void LateUpdate()
    {
        if (billboardHealthBar && healthBarContainer != null && mainCam != null)
        {
            healthBarContainer.transform.LookAt(
                healthBarContainer.transform.position + mainCam.transform.forward
            );
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (damage <= 0) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        OnDamage?.Invoke(damage);
        UpdateHealthBar();
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

        UpdateHealthBar();
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

    private void UpdateHealthBar()
    {
        if (healthBarFill != null && maxHealth > 0)
            healthBarFill.fillAmount = (float)currentHealth / maxHealth;
    }

    private void Die()
    {
        isDead = true;
        
        if (healthBarContainer != null)
            healthBarContainer.SetActive(false);

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
