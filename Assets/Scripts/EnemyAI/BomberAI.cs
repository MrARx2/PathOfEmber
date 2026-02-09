using UnityEngine;
using System.Collections;
using Audio;

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
        
        [Header("=== VISUAL SWELL ===")]
        [SerializeField, Tooltip("Swell up before exploding?")]
        private bool enableSwellEffect = true;
        [SerializeField] private float maxSwellScale = 1.5f;

        [Header("=== WARNING INDICATOR ===")]
        [SerializeField, Tooltip("Warning indicator prefab (spawns on ground during detonation)")]
        private GameObject warningPrefab;

        [Header("=== SOUND EFFECTS ===")]
        [SerializeField] private SoundEvent bipSound;
        [SerializeField] private SoundEvent explosionSound;

        private bool hasExploded = false;
        private bool isDetonating = false;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 initialScale;
        private GameObject warningInstance;

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
        
        private void OnDestroy()
        {
            CleanupWarningDecal();
        }
        
        private void OnDisable()
        {
            // Clean up when returned to pool (pooled objects disable, not destroy)
            CleanupWarningDecal();
        }
        
        private void CleanupWarningDecal()
        {
            if (warningInstance != null)
            {
                // Return to pool instead of destroying
                if (ObjectPoolManager.Instance != null)
                {
                    ObjectPoolManager.Instance.Return(warningInstance);
                }
                else
                {
                    Destroy(warningInstance);
                }
                warningInstance = null;
            }
        }

        protected override void Update()
        {
            if (hasExploded || isDetonating) return;
            
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                StopMovement();
                CleanupWarningDecal(); // Clean up decal if we die before exploding
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
                // SMART UPDATE: Check for walls before chasing
                // If we are about to hit a wall, steer away first
                if (!AvoidObstacles())
                {
                    ChasePlayer();
                }
            }

            UpdateAnimator();
        }

        private void StartDetonation()
        {
            if (isDetonating || hasExploded) return;
            isDetonating = true;

            StopMovement();

            StartCoroutine(DetonationSequence());
        }

        private IEnumerator DetonationSequence()
        {
            // Spawn warning indicator on ground
            SpawnWarningIndicator();
            
            // Play bip sound at start of detonation
            if (bipSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(bipSound, transform.position);
            
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

            // Clean up warning before explosion
            if (warningInstance != null)
            {
                Destroy(warningInstance);
                warningInstance = null;
            }
            
            Explode();
        }

        private void SpawnWarningIndicator()
        {
            if (warningPrefab == null) return;
            
            // Spawn at ground level below bomber
            Vector3 groundPos = transform.position;
            groundPos.y = 0f;
            
            // Rotated to lie flat on ground
            Quaternion groundRot = Quaternion.Euler(90f, 0f, 0f);
            
            // Use pool for warning indicator
            if (ObjectPoolManager.Instance != null)
            {
                warningInstance = ObjectPoolManager.Instance.Get(warningPrefab, groundPos, groundRot);
            }
            else
            {
                warningInstance = Instantiate(warningPrefab, groundPos, groundRot);
                // Safety timer only for non-pooled
                Destroy(warningInstance, detonationDelay + 0.5f);
            }
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

            // Spawn VFX via pool
            if (explosionVFX != null)
            {
                if (ObjectPoolManager.Instance != null)
                {
                    GameObject vfx = ObjectPoolManager.Instance.Get(explosionVFX, transform.position, Quaternion.identity);
                    // Return to pool after 3 seconds
                    StartCoroutine(ReturnToPoolDelayed(vfx, 3f));
                }
                else
                {
                    GameObject vfx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
                    Destroy(vfx, 3f);
                }
            }

            // Camera shake for explosion impact (even if player not hit)
            CameraShakeManager.Shake(CameraShakePreset.Heavy);

            // Play explosion sound
            if (explosionSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(explosionSound, transform.position);

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

                if (damageable != null && damageable != (object)health)
                {
                    damageable.TakeDamage(explosionDamage);
                    if (debugLog) Debug.Log($"[BomberAI] Exploded hitting {hit.name}");
                }
            }

            // Award 50% XP for self-destruct (partial reward even if player didn't kill bomber)
            if (XPSystem.Instance != null && health != null)
            {
                int partialXP = Mathf.RoundToInt(health.XpReward * 0.5f);
                if (partialXP > 0)
                {
                    XPSystem.Instance.AddXP(partialXP);
                    if (debugLog) Debug.Log($"[BomberAI] Self-destruct: Awarded {partialXP} XP (50% of {health.XpReward})");
                }
            }

            // Clean up health bar before destroying/returning to pool
            if (health != null && HealthBarManager.Instance != null)
            {
                HealthBarManager.Instance.Unregister(health);
            }

            // Return self to pool or destroy
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private System.Collections.IEnumerator ReturnToPoolDelayed(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(obj);
            }
        }

        protected override void OnAttack() { } // Not used

        protected override void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detonationRange);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
            
            // Draw whiskers
            Gizmos.color = Color.yellow;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            
            Gizmos.DrawLine(origin, origin + (forward + right * 0.5f).normalized * 1.5f);
            Gizmos.DrawLine(origin, origin + (forward - right * 0.5f).normalized * 1.5f);
        }
        
        /// <summary>
        /// Proactive wall detection. Returns true if an obstacle is detected and avoidance is active.
        /// </summary>
        private bool AvoidObstacles()
        {
            if (agent == null) return false;
            
            // Only check occasionally to save CPU, unless we are already avoiding
            if ((Time.frameCount + GetInstanceID()) % 5 != 0) return false;

            // Whiskers: Cast rays forward-left and forward-right
            // Slightly wider than agent radius to catch corners
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            float detectionDist = 1.5f; // Look ahead
            
            bool hitLeft = Physics.Raycast(origin, (transform.forward - transform.right * 0.5f).normalized, out RaycastHit hitL, detectionDist, obstacleLayerMask);
            bool hitRight = Physics.Raycast(origin, (transform.forward + transform.right * 0.5f).normalized, out RaycastHit hitR, detectionDist, obstacleLayerMask);
            
            // Validate hits (ignore player, ignore ground)
            if (hitLeft && (hitL.collider.CompareTag("Player") || hitL.collider.CompareTag("Enemy"))) hitLeft = false;
            if (hitRight && (hitR.collider.CompareTag("Player") || hitR.collider.CompareTag("Enemy"))) hitRight = false;
            
            if (hitLeft || hitRight)
            {
                // Wall detected!
                Vector3 avoidanceDir = Vector3.zero;
                
                if (hitLeft && hitRight)
                {
                    // Hit both sides? Wall likely straight ahead. Turn around or pick random side.
                    avoidanceDir = -transform.forward; 
                }
                else if (hitLeft)
                {
                    // Hit left, steer right
                    avoidanceDir = transform.right;
                }
                else if (hitRight)
                {
                    // Hit right, steer left
                    avoidanceDir = -transform.right;
                }
                
                // Project avoidance direction onto a valid NavMesh point
                Vector3 targetPos = transform.position + avoidanceDir * 2.0f;
                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.SetDestination(navHit.position);
                    return true; // We are handling movement, Base logic should skip
                }
            }
            
            return false;
        }
    }
}
