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
    [Tooltip("UI Image with Type = Filled for health bar")]
    [SerializeField] private Image healthBarFill;
    [Tooltip("Parent container to show/hide the health bar")]
    [SerializeField] private GameObject healthBarContainer;
    [Tooltip("Should health bar face the camera?")]
    [SerializeField] private bool billboardHealthBar = false;

    [Header("Invulnerability Visual")]
    [Tooltip("Child GameObject with invulnerability effect")]
    [SerializeField] private GameObject invulnerabilityEffect;

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
    private Camera mainCam;

    public bool IsInvulnerable => isInvulnerable;
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        mainCam = Camera.main;
        if (invulnerabilityEffect != null)
            invulnerabilityEffect.SetActive(false);
    }

    private void Start()
    {
        UpdateHealthBar();
        NotifyHealthChanged();
    }

    private void LateUpdate()
    {
        // Billboard health bar to face camera
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
        if (isInvulnerable) return;
        if (damage <= 0) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        if (animator != null && !string.IsNullOrEmpty(hitTrigger))
            animator.SetTrigger(hitTrigger);

        OnDamage?.Invoke(damage);
        UpdateHealthBar();
        NotifyHealthChanged();

        if (currentHealth <= 0)
            Die();
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
        StartCoroutine(InvulnerabilityRoutine(duration));
    }

    private IEnumerator InvulnerabilityRoutine(float duration)
    {
        isInvulnerable = true;
        if (invulnerabilityEffect != null)
            invulnerabilityEffect.SetActive(true);

        yield return new WaitForSeconds(duration);

        isInvulnerable = false;
        if (invulnerabilityEffect != null)
            invulnerabilityEffect.SetActive(false);
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill != null && maxHealth > 0)
            healthBarFill.fillAmount = (float)currentHealth / maxHealth;
    }

    private void Die()
    {
        isDead = true;

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        // Hide health bar on death
        if (healthBarContainer != null)
            healthBarContainer.SetActive(false);

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
