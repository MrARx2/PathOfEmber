using UnityEngine;
using System.Collections;

namespace EnemyAI
{
    /// <summary>
    /// Bomber enemy - fast chaser that explodes when reaching the player.
    /// </summary>
    public class BomberAI : EnemyAIBase
    {
        [Header("=== BOMBER SETTINGS ===")]
        [SerializeField] private float detonationRange = 2f;
        [SerializeField] private int explosionDamage = 50;
        [SerializeField] private float explosionRadius = 4f;
        [SerializeField] private GameObject explosionVFX;
        [SerializeField] private LayerMask damageLayer;

        [Header("=== SPEED ===")]
        [SerializeField] private float speedMultiplier = 1.5f;

        [Header("=== EXPLOSION EFFECT ===")]
        [SerializeField] private float detonationDelay = 0.5f;
        [SerializeField] private float explosionEmissionIntensity = 5f;
        [SerializeField] private Color emissionColor = new Color(1f, 0.5f, 0f);
        [SerializeField] private string emissionProperty = "_EmissionColor";
        [SerializeField] private string explosionTrigger = "Explode";

        private bool hasExploded = false;
        private bool isDetonating = false;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;

        protected override void Awake()
        {
            base.Awake();
            
            // Bomber is faster
            if (agent != null)
            {
                agent.speed = moveSpeed * speedMultiplier;
                agent.avoidancePriority = 15; // Highest priority
            }

            renderers = GetComponentsInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
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
                SetEmissionIntensity(Mathf.Lerp(0f, explosionEmissionIntensity, t));
                yield return null;
            }

            Explode();
        }

        private void SetEmissionIntensity(float intensity)
        {
            if (renderers == null) return;
            Color emission = emissionColor * intensity;

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

            if (explosionVFX != null)
            {
                GameObject vfx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
                Destroy(vfx, 3f);
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageLayer);
            foreach (Collider hit in hits)
            {
                if (hit.transform == transform) continue;

                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable == null)
                    damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable != null && damageable != health)
                {
                    damageable.TakeDamage(explosionDamage);
                }
            }

            Destroy(gameObject);
        }

        protected override void OnAttack() { } // Bomber doesn't use normal attacks
    }
}
