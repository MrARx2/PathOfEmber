using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using Audio;

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

    [Header("Heal VFX")]
    [SerializeField, Tooltip("Child GameObject with heal VFX particles (activate/deactivate)")]
    private GameObject healVFX;
    [SerializeField, Tooltip("Duration to show heal VFX")]
    private float healVFXDuration = 2f;

    [Header("Fire State")]
    [SerializeField, Tooltip("Is the player currently on fire?")]
    private bool isOnFire = false;
    [SerializeField, Tooltip("Visual effect shown when player is on fire")]
    private GameObject fireEffect;
    
    [Header("Fire Damage (Centralized)")]
    [SerializeField, Tooltip("Base fire damage per second while on fire")]
    private int fireDamagePerSecond = 20;
    [SerializeField, Tooltip("Fire damage tick interval (1 second recommended for sound effects)")]
    private float fireTickInterval = 1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string healTrigger = "Heal";
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string deathTrigger = "Die";
    
    [Header("Death Sequence")]
    [SerializeField, Tooltip("Duration to wait for death animation before showing popup")]
    private float deathAnimationDuration = 4f;
    [SerializeField, Tooltip("Death popup panel to show after animation")]
    private DeathPopupController deathPopup;

    [Header("Hit Flash Settings")]
    [SerializeField, Tooltip("Enable flash effect when hit")]
    private bool enableHitFlash = true;
    [SerializeField, Tooltip("Emission color when hit (uses emission for visibility through lighting)")]
    private Color hitFlashEmissionColor = new Color(1f, 0.3f, 0.3f, 1f); // Bright red
    [SerializeField, Tooltip("Emission intensity multiplier")]
    private float hitFlashEmissionIntensity = 2f;
    [SerializeField, Tooltip("Duration of hit flash in seconds")]
    private float hitFlashDuration = 0.1f;

    [Header("Camera Shake Settings")]
    [SerializeField, Tooltip("Enable camera shake when hit")]
    private bool enableCameraShake = true;

    [Header("Sound Effects")]
    [SerializeField] private SoundEvent hurtSound;
    [SerializeField] private SoundEvent fireHurtSound;
    [SerializeField] private SoundEvent healSound;
    [SerializeField] private SoundEvent deathSound;
    [SerializeField] private SoundEvent invulnerabilitySound;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    [Header("Events")]
    public UnityEvent<int> OnDamage;
    public UnityEvent<int> OnHeal;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;

    private bool isDead = false;
    private Coroutine dotCoroutine;
    private Coroutine hitFlashCoroutine;
    private Coroutine fireDamageCoroutine;
    private Coroutine healVFXCoroutine;
    private float fireDamageMultiplier = 1f; // External sources can modify (e.g., hazard zone depth)
    private int permanentFireSources = 0; // Count of permanent fire sources (HazardZone, Lava)
    private float fireEndTime = 0f; // Time.time when timed fire effects expire
    private Renderer[] renderers;
    private Color[] originalEmissionColors;
    private bool[] hadEmissionEnabled;
    private bool hasHitTrigger = false;
    private bool hasHealTrigger = false;

    public bool IsInvulnerable => isInvulnerable;
    public bool IsDead => isDead;
    public bool IsOnFire => isOnFire;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (invulnerabilityEffect != null)
            invulnerabilityEffect.SetActive(false);
        if (fireEffect != null)
            fireEffect.SetActive(false);
        if (healVFX != null)
            healVFX.SetActive(false);
        
        // Cache only MeshRenderers and SkinnedMeshRenderers for emission flash
        // Exclude ParticleSystemRenderer, TrailRenderer, LineRenderer, etc.
        var allRenderers = GetComponentsInChildren<Renderer>();
        var validRenderers = new System.Collections.Generic.List<Renderer>();
        foreach (var r in allRenderers)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                // Also check if material has emission support
                if (r.material != null && r.material.HasProperty("_EmissionColor"))
                {
                    validRenderers.Add(r);
                }
            }
        }
        
        renderers = validRenderers.ToArray();
        originalEmissionColors = new Color[renderers.Length];
        hadEmissionEnabled = new bool[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            originalEmissionColors[i] = renderers[i].material.GetColor("_EmissionColor");
            hadEmissionEnabled[i] = renderers[i].material.IsKeywordEnabled("_EMISSION");
        }
    }

    private void Start()
    {
        // Register with global manager for floating bar
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.Register(this);
        }

        NotifyHealthChanged();
        
        // Check if animator has hit trigger parameter (to avoid warning spam)
        if (animator != null && !string.IsNullOrEmpty(hitTrigger))
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == hitTrigger && param.type == AnimatorControllerParameterType.Trigger)
                {
                    hasHitTrigger = true;
                    break;
                }
            }
        }
        
        // Check if animator has heal trigger parameter
        if (animator != null && !string.IsNullOrEmpty(healTrigger))
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == healTrigger && param.type == AnimatorControllerParameterType.Trigger)
                {
                    hasHealTrigger = true;
                    break;
                }
            }
        }
    }

    private void LateUpdate()
    {
        // No billboard logic needed for HUD
    }

    public void TakeDamage(int damage)
    {
        TakeDamageInternal(damage, triggerShake: true);
    }

    /// <summary>
    /// Internal damage method with control over camera shake.
    /// </summary>
    private void TakeDamageInternal(int damage, bool triggerShake)
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
            
            return;
        }

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        // Trigger camera shake effect (only for direct hits, not DoT)
        if (enableCameraShake && triggerShake)
        {
            CameraShakeManager.Shake(CameraShakePreset.Medium);
        }

        // Trigger hit flash effect
        if (enableHitFlash)
        {
            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        // Only trigger animation if parameter exists (checked at Start)
        if (hasHitTrigger)
            animator.SetTrigger(hitTrigger);

        // Play hurt sound
        if (hurtSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(hurtSound);

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

        // Only trigger animation if parameter exists
        if (hasHealTrigger)
            animator.SetTrigger(healTrigger);

        // Play heal sound
        if (healSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(healSound);

        // Activate heal VFX
        if (healVFX != null)
        {
            if (healVFXCoroutine != null)
                StopCoroutine(healVFXCoroutine);
            healVFXCoroutine = StartCoroutine(HealVFXRoutine());
        }

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

    /// <summary>
    /// Adds a permanent fire source (e.g., HazardZone, Lava).
    /// Fire stays on while any permanent source is active.
    /// </summary>
    public void AddFireSource()
    {
        permanentFireSources++;
        UpdateFireState();
    }
    
    /// <summary>
    /// Removes a permanent fire source.
    /// Fire stops only when all sources are removed AND timed effects expire.
    /// </summary>
    public void RemoveFireSource()
    {
        permanentFireSources = Mathf.Max(0, permanentFireSources - 1);
        UpdateFireState();
    }
    
    /// <summary>
    /// Adds a timed fire effect (e.g., fire projectile).
    /// Uses max duration tracking - extends fire if this duration is longer.
    /// </summary>
    public void AddTimedFire(float duration)
    {
        if (duration <= 0) return;
        
        float newEndTime = Time.time + duration;
        // Keep the longest remaining duration
        if (newEndTime > fireEndTime)
        {
            fireEndTime = newEndTime;
        }
        UpdateFireState();
        
        // Start checking for timed fire expiry
        if (fireEffectCoroutine != null)
            StopCoroutine(fireEffectCoroutine);
        fireEffectCoroutine = StartCoroutine(CheckTimedFireExpiry());
    }
    
    /// <summary>
    /// Sets fire damage multiplier (e.g., for hazard zone depth scaling).
    /// </summary>
    public void SetFireDamageMultiplier(float multiplier)
    {
        fireDamageMultiplier = Mathf.Max(0f, multiplier);
    }
    
    /// <summary>
    /// Legacy method for backward compatibility. Prefer AddFireSource/RemoveFireSource.
    /// </summary>
    public void SetOnFire(bool onFire)
    {
        if (onFire)
            AddFireSource();
        else
            RemoveFireSource();
    }
    
    /// <summary>
    /// Legacy method for backward compatibility. Now uses AddTimedFire.
    /// </summary>
    public void SetOnFire(bool onFire, float duration)
    {
        if (onFire && duration > 0)
        {
            AddTimedFire(duration);
        }
        else if (!onFire)
        {
            // Timed effects don't call RemoveFireSource - they expire naturally
        }
    }
    
    private Coroutine fireEffectCoroutine;
    
    /// <summary>
    /// Updates the fire state based on all active sources.
    /// </summary>
    private void UpdateFireState()
    {
        bool shouldBeOnFire = permanentFireSources > 0 || Time.time < fireEndTime;
        
        if (shouldBeOnFire && !isOnFire)
        {
            // Turn fire ON
            isOnFire = true;
            if (fireEffect != null)
                fireEffect.SetActive(true);
            
            if (fireDamageCoroutine == null)
            {
                fireDamageCoroutine = StartCoroutine(FireDamageRoutine());
            }
        }
        else if (!shouldBeOnFire && isOnFire)
        {
            // Turn fire OFF
            isOnFire = false;
            if (fireEffect != null)
                fireEffect.SetActive(false);
            
            if (fireDamageCoroutine != null)
            {
                StopCoroutine(fireDamageCoroutine);
                fireDamageCoroutine = null;
            }
            fireDamageMultiplier = 1f;
        }
    }
    
    /// <summary>
    /// Coroutine to check when timed fire effects expire.
    /// </summary>
    private IEnumerator CheckTimedFireExpiry()
    {
        while (Time.time < fireEndTime)
        {
            yield return new WaitForSeconds(0.1f);
        }
        fireEffectCoroutine = null;
        UpdateFireState();
    }
    
    /// <summary>
    /// Centralized fire damage routine. Ticks at fireTickInterval (default 1 second).
    /// </summary>
    private IEnumerator FireDamageRoutine()
    {
        while (isOnFire && !isDead)
        {
            // Calculate damage with multiplier
            int damage = Mathf.RoundToInt(fireDamagePerSecond * fireTickInterval * fireDamageMultiplier);
            
            if (damage > 0 && !isInvulnerable)
            {
                TakeDamageInternal(damage, triggerShake: false);
                
                // Play fire hurt sound
                if (fireHurtSound != null && AudioManager.Instance != null)
                    AudioManager.Instance.Play(fireHurtSound);
            }
            
            yield return new WaitForSeconds(fireTickInterval);
        }
        
        fireDamageCoroutine = null;
    }

    /// <summary>
    /// Applies damage over time. If initialDamage > 0, that amount is applied immediately
    /// with camera shake, then the DoT ticks follow without shake.
    /// If one-time shield blocks the initial hit, no DoT is applied.
    /// </summary>
    /// <returns>True if damage was applied, false if blocked by shield/invulnerability.</returns>
    public bool ApplyDamageOverTime(int damagePerTick, float tickInterval, int totalTicks, int initialDamage = 0)
    {
        // If invulnerable, no damage or DoT
        if (isInvulnerable) return false;
        
        // If one-time shield is active, it will block this entire attack (including DoT)
        if (hasOneTimeShield)
        {
            hasOneTimeShield = false;
            if (shieldEffect != null)
                shieldEffect.SetActive(false);
            
            var abilities = GetComponent<PlayerAbilities>();
            if (abilities != null)
                abilities.OnShieldConsumed();
            
            return false; // Shield consumed, no damage or DoT applied
        }
        
        // Apply initial impact damage with camera shake
        if (initialDamage > 0)
        {
            TakeDamageInternal(initialDamage, triggerShake: true);
        }
        
        if (dotCoroutine != null)
            StopCoroutine(dotCoroutine);
        dotCoroutine = StartCoroutine(DamageOverTimeRoutine(damagePerTick, tickInterval, totalTicks));
        
        return true; // Damage was applied
    }

    private IEnumerator DamageOverTimeRoutine(int damagePerTick, float tickInterval, int totalTicks)
    {
        for (int i = 0; i < totalTicks; i++)
        {
            if (isDead) yield break;
            TakeDamageInternal(damagePerTick, triggerShake: false); // DoT skips camera shake
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
        SetInvulnerabilityEffectActive(true);
        
        // Play invulnerability sound
        if (invulnerabilitySound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(invulnerabilitySound);

        yield return new WaitForSeconds(duration);

        isInvulnerable = false;
        SetInvulnerabilityEffectActive(false);
    }

    private void SetInvulnerabilityEffectActive(bool active)
    {
        if (invulnerabilityEffect == null)
        {
            if (debugLog) Debug.LogWarning("[PlayerHealth] invulnerabilityEffect is null!");
            return;
        }

        // Make sure the GameObject is active/inactive
        invulnerabilityEffect.SetActive(active);
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
        if (isDead) return; // Prevent multiple calls
        isDead = true;

        if (debugLog) Debug.Log("[PlayerHealth] Die() called!");

        // Disable player controls
        DisablePlayerControls();

        // Destroy all potions in the scene immediately
        // (colliders are disabled so potions can never be collected normally)
        DestroyAllPotions();

        // Play death sound
        if (deathSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(deathSound);

        // Force-clear shooting state before death animation
        // This ensures upper body layer doesn't keep playing the shooting pose
        if (animator != null)
            animator.SetBool("IsShooting", false);

        // Trigger death animation
        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
        {
            if (debugLog) Debug.Log($"[PlayerHealth] Setting trigger '{deathTrigger}' on animator: {animator.name}");
            animator.SetTrigger(deathTrigger);
        }
        else
        {
            if (debugLog) Debug.LogWarning($"[PlayerHealth] Cannot trigger animation! Animator: {(animator != null ? animator.name : "NULL")}, deathTrigger: '{deathTrigger}'");
        }

        OnDeath?.Invoke();

        // Start death sequence coroutine
        StartCoroutine(DeathSequenceRoutine());
    }
    
    private System.Collections.IEnumerator DeathSequenceRoutine()
    {
        // Wait for death animation to complete
        yield return new WaitForSeconds(deathAnimationDuration);
        
        // Show death popup
        if (deathPopup != null)
        {
            deathPopup.Show();
        }
        else
        {
            if (debugLog) Debug.LogWarning("[PlayerHealth] Death popup not assigned! Assign DeathPopupPanel to PlayerHealth.");
        }
    }
    
    private void DisablePlayerControls()
    {
        // Disable movement
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;
        
        // Stop all shooting and targeting
        var shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.StopAllShooting();
            shooting.enabled = false;
        }
        
        // Disable all colliders to prevent enemies from pushing the corpse
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
        
        // Also disable rigidbody if present (prevent physics forces)
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// Destroys all MagneticPotion instances in the scene.
    /// Called on death to prevent potions from spinning in place
    /// (since player colliders are disabled and can't trigger collection).
    /// </summary>
    private void DestroyAllPotions()
    {
        MagneticPotion[] potions = Object.FindObjectsByType<MagneticPotion>(FindObjectsSortMode.None);
        for (int i = 0; i < potions.Length; i++)
        {
            if (potions[i] != null)
                Destroy(potions[i].gameObject);
        }
        
        if (debugLog && potions.Length > 0)
            Debug.Log($"[PlayerHealth] Destroyed {potions.Length} potions on death");
    }

    private void NotifyHealthChanged()
    {
        if (maxHealth > 0)
            OnHealthChanged?.Invoke((float)currentHealth / maxHealth);
    }

    public Transform GetTransform() => transform;

    private IEnumerator HealVFXRoutine()
    {
        healVFX.SetActive(true);
        yield return new WaitForSeconds(healVFXDuration);
        healVFX.SetActive(false);
        healVFXCoroutine = null;
    }

    #region Emission Helpers
    private IEnumerator HitFlashRoutine()
    {
        ApplyEmission(hitFlashEmissionColor, hitFlashEmissionIntensity);
        yield return new WaitForSeconds(hitFlashDuration);
        ClearEmission();
        hitFlashCoroutine = null;
    }

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

    public void DebugTestHitFlash()
    {
        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }
}
