using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Configuration")]
    [SerializeField] private int maxHealth = 1000;
    
    [Header("State")]
    [SerializeField] private int currentHealth;

    [Header("Events")]
    public UnityEvent<int> OnDamage;
    public UnityEvent<int> OnHeal;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;

    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        NotifyHealthChanged();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (damage <= 0) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        OnDamage?.Invoke(damage);
        NotifyHealthChanged();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        OnHeal?.Invoke(amount);
        NotifyHealthChanged();
    }

    public void IncreaseMaxHealth(float percentage)
    {
        int increase = Mathf.RoundToInt(maxHealth * percentage);
        if (increase > 0)
        {
            maxHealth += increase;
            currentHealth += increase;
            NotifyHealthChanged();
        }
    }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke();
        Debug.Log($"{gameObject.name} died.");
    }

    private void NotifyHealthChanged()
    {
        if (maxHealth > 0)
        {
            float pct = (float)currentHealth / maxHealth;
            OnHealthChanged?.Invoke(pct);
        }
    }

    public Transform GetTransform()
    {
        return transform;
    }

    [ContextMenu("Debug: Take 100 Damage")]
    public void DebugDamage()
    {
        TakeDamage(100);
    }

    [ContextMenu("Debug: Heal 100")]
    public void DebugHeal()
    {
        Heal(100);
    }
}
