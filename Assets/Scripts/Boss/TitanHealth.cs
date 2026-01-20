using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using Audio;

namespace Boss
{
    /// <summary>
    /// Identifies which body part this health component represents.
    /// </summary>
    public enum TitanBodyPart
    {
        RightHand,
        LeftHand,
        Core
    }
    /// <summary>
    /// Health component for Titan body parts (sockets).
    /// Based on EnemyHealth with Titan-specific modifications.
    /// Add this to each Titan socket (RightHand, LeftHand, Core).
    /// </summary>
    public class TitanHealth : MonoBehaviour, IDamageable
    {
        [Header("Configuration")]
        [SerializeField] private TitanBodyPart bodyPart = TitanBodyPart.Core;
        [SerializeField] private int maxHealth = 500;
        
        [SerializeField, Tooltip("Optional: Assign the model child transform for accurate effect positioning")]
        private Transform modelTransform;
        
        [Header("State")]
        [SerializeField] private int currentHealth;

        [Header("Health Bar UI")]
        [Tooltip("Optional: Assign a transform to anchor the health bar to (e.g. above socket)")]
        public Transform HealthBarPoint;
        
        [SerializeField, Tooltip("Time in seconds before health bar hides after being shown")]
        private float healthBarHideDelay = 2f;

        [Header("Events")]
        public UnityEvent<int> OnDamage;
        public UnityEvent OnDeath;
        public UnityEvent<float> OnHealthChanged;
        public UnityEvent OnHealed;

        [Header("Regeneration")]
        [SerializeField, Tooltip("Can this part regenerate (via Rage)?")]
        private bool canRegenerate = true;

        [Header("Hit Flash Settings")]
        [SerializeField, Tooltip("Enable flash effect when hit")]
        private bool enableHitFlash = true;
        [SerializeField, Tooltip("Emission color when hit")]
        private Color hitFlashEmissionColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField, Tooltip("Emission intensity multiplier")]
        private float hitFlashEmissionIntensity = 2f;
        [SerializeField, Tooltip("Duration of hit flash in seconds")]
        private float hitFlashDuration = 0.1f;
        [SerializeField, Tooltip("Optional VFX prefab to spawn when hit")]
        private GameObject hitVFXPrefab;

        [Header("Damage Popup Settings")]
        [SerializeField, Tooltip("Enable floating damage numbers")]
        private bool showDamagePopup = true;
        [SerializeField, Tooltip("Offset from socket position for popup spawn")]
        private Vector3 damagePopupOffset = new Vector3(0, 1.5f, 0);

        [Header("Sound Effects")]
        [SerializeField, Tooltip("Sound when socket is hit")]
        private SoundEvent hitSound;
        [SerializeField, Tooltip("Sound when part is destroyed")]
        private SoundEvent destroyedSound;
        
        [Header("Destruction Tint")]
        [SerializeField, Tooltip("Optional: Assign the specific material to tint (if not set, auto-finds child renderers)")]
        private Material tintMaterial;
        [SerializeField, Tooltip("Normal emission color (healthy state)")]
        private Color minEmission = Color.black;
        [SerializeField, Tooltip("Destroyed emission color (damaged state)")]
        private Color maxEmission = new Color(0.5f, 0.2f, 0.2f, 1f);
        [SerializeField, Tooltip("Duration of smooth fade in/out")]
        private float tintFadeDuration = 0.5f;

        private bool isDestroyed = false;
        private Coroutine hitFlashCoroutine;
        private Coroutine healthBarHideCoroutine;
        private Coroutine destructionTintCoroutine;
        private Material[] tintMaterials;
        private Color[] originalEmissionColors;
        private bool[] hadEmissionEnabled;

        /// <summary>
        /// Returns the visual center position.
        /// </summary>
        public Vector3 VisualCenter => modelTransform != null ? modelTransform.position : transform.position;

        public TitanBodyPart BodyPart => bodyPart;
        public bool IsDestroyed => isDestroyed;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        public bool CanRegenerate => canRegenerate;

        private void Awake()
        {
            currentHealth = maxHealth;
            
            // Use assigned material OR auto-find from child renderers
            if (tintMaterial != null)
            {
                // Use the single assigned material
                tintMaterials = new Material[] { tintMaterial };
            }
            else
            {
                // Auto-find renderers for hit flash
                var allRenderers = GetComponentsInChildren<Renderer>();
                var validMaterials = new System.Collections.Generic.List<Material>();
                foreach (var r in allRenderers)
                {
                    if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    {
                        if (r.material != null)
                        {
                            validMaterials.Add(r.material);
                        }
                    }
                }
                tintMaterials = validMaterials.ToArray();
            }
            
            // Cache original emission colors
            originalEmissionColors = new Color[tintMaterials.Length];
            hadEmissionEnabled = new bool[tintMaterials.Length];
            
            for (int i = 0; i < tintMaterials.Length; i++)
            {
                if (tintMaterials[i].HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[i] = tintMaterials[i].GetColor("_EmissionColor");
                    hadEmissionEnabled[i] = tintMaterials[i].IsKeywordEnabled("_EMISSION");
                }
            }
        }

        private void Start()
        {
            // Register with EnemyRegistry for player targeting
            EnemyRegistry.Register(transform);
            
            // Register with HealthBarManager for UI health bar
            if (HealthBarManager.Instance != null)
            {
                HealthBarManager.Instance.Register(this);
            }
            
            // Enforce initial healthy state emission
            SetEmissionColor(minEmission);
        }

        private void OnDisable()
        {
            EnemyRegistry.Unregister(transform);
            
            // Clean up health bar
            if (HealthBarManager.Instance != null)
            {
                HealthBarManager.Instance.Unregister(this);
            }
        }

        // IDamageable implementation
        void IDamageable.TakeDamage(int damage) => TakeDamage(damage);

        public void TakeDamage(int damage)
        {
            if (isDestroyed) return;
            if (damage <= 0) return;

            currentHealth -= damage;
            if (currentHealth < 0) currentHealth = 0;
            
            // Show health bar when damaged
            ShowHealthBar();

            // Hit flash effect
            if (enableHitFlash)
            {
                if (hitFlashCoroutine != null)
                    StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
            }
            
            // Spawn hit VFX
            if (hitVFXPrefab != null)
            {
                GameObject vfx = Instantiate(hitVFXPrefab, VisualCenter, Quaternion.identity);
                Destroy(vfx, 2f);
            }
            
            // Damage popup
            if (showDamagePopup && PopupManager.Instance != null)
            {
                Vector3 popupPos = VisualCenter + damagePopupOffset;
                PopupManager.Instance.ShowDamage(damage, popupPos);
            }

            // Play hit sound
            if (hitSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(hitSound, VisualCenter);

            OnDamage?.Invoke(damage);
            OnHealthChanged?.Invoke(HealthPercent);

            if (currentHealth <= 0)
                OnPartDestroyed();
        }

        private void OnPartDestroyed()
        {
            if (isDestroyed) return;
            isDestroyed = true;
            
            Debug.Log($"[TitanHealth] {bodyPart} OnPartDestroyed called!");
            
            // Stop any hit flash coroutine so it doesn't clear our destruction tint
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            
            // Play destroyed sound
            if (destroyedSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(destroyedSound, VisualCenter);
            
            // Apply destruction tint (stays until animation event clears it)
            destructionTintCoroutine = StartCoroutine(DestructionTintRoutine());
            
            Debug.Log($"[TitanHealth] {bodyPart} invoking OnDeath event");
            OnDeath?.Invoke();
            
            Debug.Log($"[TitanHealth] {bodyPart} DESTROYED!");
        }
        
        private IEnumerator DestructionTintRoutine()
        {
            Debug.Log($"[TitanHealth] {bodyPart} starting smooth destruction tint (min→max)");
            
            // Smooth fade from minEmission to maxEmission
            float elapsed = 0f;
            
            while (elapsed < tintFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / tintFadeDuration;
                Color currentColor = Color.Lerp(minEmission, maxEmission, t);
                SetEmissionColor(currentColor);
                yield return null;
            }
            SetEmissionColor(maxEmission);
            Debug.Log($"[TitanHealth] {bodyPart} destruction tint complete (at maxEmission)");
            
            // Hide health bar when fully tinted
            HideHealthBar();
            
            // Keep tint applied indefinitely - animation event will clear it via RepairWithFade
            // Failsafe timeout in case animation event never fires
            yield return new WaitForSeconds(30f);
            
            // Failsafe: clear if still tinted after 30 seconds
            Debug.LogWarning($"[TitanHealth] {bodyPart} destruction tint failsafe triggered - clearing after 30s");
            SetEmissionColor(minEmission);
            destructionTintCoroutine = null;
        }
        
        private Color GetCurrentEmissionColor()
        {
            if (tintMaterials != null && tintMaterials.Length > 0 && tintMaterials[0] != null)
            {
                if (tintMaterials[0].HasProperty("_EmissionColor"))
                    return tintMaterials[0].GetColor("_EmissionColor");
            }
            return Color.black;
        }
        
        private void SetEmissionColor(Color color)
        {
            if (tintMaterials == null) return;
            for (int i = 0; i < tintMaterials.Length; i++)
            {
                if (tintMaterials[i] != null && tintMaterials[i].HasProperty("_EmissionColor"))
                {
                    tintMaterials[i].EnableKeyword("_EMISSION");
                    tintMaterials[i].SetColor("_EmissionColor", color);
                }
            }
        }

        /// <summary>
        /// Fully heals this body part (used during Rage).
        /// </summary>
        public void FullHeal()
        {
            Debug.Log($"[TitanHealth] {bodyPart} fully healed! Was destroyed: {isDestroyed}");
            
            currentHealth = maxHealth;
            isDestroyed = false;
            
            // Enforce minEmission (healthy state)
            SetEmissionColor(minEmission);
            
            // Show health bar briefly when healed
            ShowHealthBar();
            
            OnHealed?.Invoke();
            OnHealthChanged?.Invoke(HealthPercent);
        }
        
        /// <summary>
        /// Called by animation event - heals with smooth tint fade.
        /// </summary>
        public void RepairWithFade()
        {
            Debug.Log($"[TitanHealth] {bodyPart} RepairWithFade called");
            
            // Stop the destruction tint coroutine if still running
            if (destructionTintCoroutine != null)
            {
                StopCoroutine(destructionTintCoroutine);
                destructionTintCoroutine = null;
            }
            
            // Restore health immediately
            currentHealth = maxHealth;
            isDestroyed = false;
            
            // Start smooth tint fade
            StartCoroutine(SmoothTintFadeRoutine());
            
            // Show health bar (make it visible again after repair)
            ShowHealthBar();
            
            OnHealed?.Invoke();
            OnHealthChanged?.Invoke(HealthPercent);
        }
        
        private IEnumerator SmoothTintFadeRoutine()
        {
            if (tintMaterials == null || tintMaterials.Length == 0) yield break;
            
            Debug.Log($"[TitanHealth] {bodyPart} starting smooth tint fade-out (current→min)");
            
            // Get actual current emission color
            Color startColor = GetCurrentEmissionColor();
            
            // Target is minEmission (Enforced healthy state)
            Color targetColor = minEmission;
            
            float elapsed = 0f;
            
            while (elapsed < tintFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / tintFadeDuration;
                
                // Smooth lerp from current to minEmission
                Color currentColor = Color.Lerp(startColor, targetColor, t);
                SetEmissionColor(currentColor);
                
                yield return null;
            }
            
            // Ensure fully set to minEmission at end
            SetEmissionColor(minEmission);
            Debug.Log($"[TitanHealth] {bodyPart} tint fade-out complete (at minEmission)");
        }

        /// <summary>
        /// Heals a specific amount.
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            
            int oldHealth = currentHealth;
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
            
            if (currentHealth > 0)
                isDestroyed = false;
            
            if (currentHealth != oldHealth)
            {
                OnHealed?.Invoke();
                OnHealthChanged?.Invoke(HealthPercent);
            }
        }

        /// <summary>
        /// Resets health to max.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDestroyed = false;
            OnHealthChanged?.Invoke(HealthPercent);
        }

        public Transform GetTransform() => transform;

        #region Hit Flash
        private IEnumerator HitFlashRoutine()
        {
            ApplyEmission(hitFlashEmissionColor, hitFlashEmissionIntensity);
            yield return new WaitForSeconds(hitFlashDuration);
            ClearEmission();
            hitFlashCoroutine = null;
        }

        private void ApplyEmission(Color emissionColor, float intensity)
        {
            if (tintMaterials == null) return;
            Color finalEmission = emissionColor * intensity;
            for (int i = 0; i < tintMaterials.Length; i++)
            {
                if (tintMaterials[i] != null)
                {
                    if (tintMaterials[i].HasProperty("_EmissionColor"))
                    {
                        tintMaterials[i].EnableKeyword("_EMISSION");
                        tintMaterials[i].SetColor("_EmissionColor", finalEmission);
                    }
                }
            }
        }

        private void ClearEmission()
        {
            if (tintMaterials == null || originalEmissionColors == null || hadEmissionEnabled == null) return;
            for (int i = 0; i < tintMaterials.Length; i++)
            {
                if (tintMaterials[i] != null && i < originalEmissionColors.Length)
                {
                    if (tintMaterials[i].HasProperty("_EmissionColor"))
                    {
                        tintMaterials[i].SetColor("_EmissionColor", originalEmissionColors[i]);
                        if (!hadEmissionEnabled[i])
                        {
                            tintMaterials[i].DisableKeyword("_EMISSION");
                        }
                    }
                }
            }
        }
        #endregion

        #region Health Bar Visibility
        /// <summary>
        /// Shows the health bar for this part. Auto-hides after delay.
        /// Called when damaged or when player aims at this part.
        /// </summary>
        public void ShowHealthBar()
        {
            Debug.Log($"[TitanHealth] {bodyPart} ShowHealthBar called, HealthBarManager exists: {HealthBarManager.Instance != null}");
            
            if (HealthBarManager.Instance != null)
            {
                HealthBarManager.Instance.ShowBar(this);
                Debug.Log($"[TitanHealth] {bodyPart} called HealthBarManager.ShowBar");
            }
            
            // Reset hide timer
            if (healthBarHideCoroutine != null)
                StopCoroutine(healthBarHideCoroutine);
            healthBarHideCoroutine = StartCoroutine(HideHealthBarAfterDelay());
        }
        
        /// <summary>
        /// Immediately hides the health bar for this part.
        /// </summary>
        public void HideHealthBar()
        {
            if (healthBarHideCoroutine != null)
            {
                StopCoroutine(healthBarHideCoroutine);
                healthBarHideCoroutine = null;
            }
            
            if (HealthBarManager.Instance != null)
            {
                HealthBarManager.Instance.HideBar(this);
            }
        }
        
        private IEnumerator HideHealthBarAfterDelay()
        {
            yield return new WaitForSeconds(healthBarHideDelay);
            
            if (HealthBarManager.Instance != null)
            {
                HealthBarManager.Instance.HideBar(this);
            }
            
            healthBarHideCoroutine = null;
        }
        #endregion

        #region Debug
        [ContextMenu("Debug: Take 100 Damage")]
        private void DebugDamage100() => TakeDamage(100);
        
        [ContextMenu("Debug: Destroy Part")]
        private void DebugDestroy() => TakeDamage(currentHealth);
        
        [ContextMenu("Debug: Full Heal")]
        private void DebugFullHeal() => FullHeal();
        #endregion
    }
}
