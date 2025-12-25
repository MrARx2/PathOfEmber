using UnityEngine;
using System.Collections;

namespace EnemyAI
{
    /// <summary>
    /// Bomber enemy - fast chaser that explodes when reaching the player.
    /// Logic tuned for emission ramping and optional scale swelling directly in code.
    /// </summary>
    public class BomberAI : EnemyAIBase
    {
        [Header("=== BOMBER SETTINGS ===")]
        [SerializeField, Tooltip("Distance to player to trigger detonation")]
        private float detonationRange = 2.5f;
        
        [SerializeField] private int explosionDamage = 50;
        
        [SerializeField, Tooltip("Radius of the explosion damage")]
        private float explosionRadius = 3.5f;
        
        [SerializeField] private GameObject explosionVFX;
        
        [SerializeField, Tooltip("Layers to damage (e.g. Player)")]
        private LayerMask damageLayer;

        [Header("=== SPEED ===")]
        [SerializeField] private float speedMultiplier = 1.5f;

        [Header("=== EXPLOSION EFFECT ===")]
        [SerializeField, Tooltip("Time before explosion after reaching range")]
        private float detonationDelay = 1.0f;
        
        [SerializeField, Tooltip("Target emission intensity")]
        private float explosionEmissionIntensity = 10f;
        
        [SerializeField] private Color emissionColor = new Color(1f, 0.2f, 0f); // Orange-Red
        [SerializeField] private string emissionProperty = "_EmissionColor";
        
        [SerializeField, Tooltip("Optional animator trigger")]
        private string explosionTrigger = "Explode";

        [Header("=== VISUAL SWELL ===")]
        [SerializeField, Tooltip("Swell up before exploding?")]
        private bool enableSwellEffect = true;
        [SerializeField] private float maxSwellScale = 1.5f;

        private bool hasExploded = false;
        private bool isDetonating = false;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 initialScale;

        protected override void Awake()
        {
            base.Awake();
            
            // Bomber is faster
            if (agent != null)
            {
                agent.speed = moveSpeed * speedMultiplier;
                // agent.avoidancePriority = 15; // Highest priority (REMOVED: Use spawn default for equality)
            }

            renderers = GetComponentsInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            initialScale = transform.localScale;

            // Auto-setup damage layer if not set
            if (damageLayer == 0)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer != -1)
                    damageLayer = 1 << playerLayer;
            }
        }

        protected override void Update()
        {
            if (hasExploded || isDetonating) return;
            
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                StopMovement();
                return;
            }

            if (target == null)
            {
                StopMovement();
                return;
            }

            float distance = Vector3.Distance(VisualPosition, target.position);

            // Using slightly larger range to start detonation so it feels fairer
            if (distance <= detonationRange)
            {
                StartDetonation();
            }
            else
            {
                ChasePlayer();
            }

            UpdateAnimator();
        }

        private void StartDetonation()
        {
            if (isDetonating || hasExploded) return;
            isDetonating = true;

            StopMovement();

            if (animator != null && !string.IsNullOrEmpty(explosionTrigger))
                animator.SetTrigger(explosionTrigger);

            StartCoroutine(DetonationSequence());
        }

        private IEnumerator DetonationSequence()
        {
            float elapsed = 0f;

            while (elapsed < detonationDelay)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / detonationDelay;
                
                // 1. Ramp emission
                SetEmissionIntensity(Mathf.Lerp(0f, explosionEmissionIntensity, t * t)); // Quadratic for more dramatic ramp
                
                // 2. Swell effect (jittery scale)
                if (enableSwellEffect)
                {
                    float swellVar = 1f + (maxSwellScale - 1f) * t;
                    // Add a little vibration
                    float shake = Mathf.Sin(elapsed * 50f) * 0.1f * t; 
                    transform.localScale = initialScale * (swellVar + shake);
                }

                yield return null;
            }

            Explode();
        }

        private void SetEmissionIntensity(float intensity)
        {
            if (renderers == null) return;
            
            // Standard Shader emission usually needs the color to be multiplied by intensity (HDR)
            Color emission = emissionColor * Mathf.LinearToGammaSpace(intensity);

            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;
                rend.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(emissionProperty, emission);
                rend.SetPropertyBlock(propertyBlock);
            }
        }

        private void Explode()
        {
            if (hasExploded) return;
            hasExploded = true;

            // Spawn VFX
            if (explosionVFX != null)
            {
                GameObject vfx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
                Destroy(vfx, 3f);
            }

            // Deal Damage
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageLayer);
            foreach (Collider hit in hits)
            {
                // Verify line of sight or just raw distance? Explosion usually hits through walls logic varies
                // Simple radius check for now
                if (hit.transform == transform) continue;

                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable == null)
                    damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable != null && damageable != health)
                {
                    damageable.TakeDamage(explosionDamage);
                    if (debugLog) Debug.Log($"[BomberAI] Exploded hitting {hit.name}");
                }
            }

            // Destroy self
            Destroy(gameObject);
        }

        protected override void OnAttack() { } // Not used

        protected void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detonationRange);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
