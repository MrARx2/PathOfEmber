using System.Collections;
using UnityEngine;
using Audio;

namespace Hazards
{
    /// <summary>
    /// Manages the complete lifecycle of a meteor strike: Warning → Falling → Impact → Fire Pool.
    /// Designed for clean timing and full tunability.
    /// </summary>
    public class MeteorStrike : MonoBehaviour
    {
        #region Serialized Fields

        [Header("=== TIMING CONFIGURATION ===")]
        [SerializeField, Tooltip("Time in seconds between the warning appearing and the meteor impact.")]
        private float warningDuration = 1.0f;

        [Header("=== DAMAGE CONFIGURATION ===")]
        [SerializeField, Tooltip("Damage dealt by the initial meteor impact.")]
        private int impactDamage = 20;
        
        [SerializeField, Tooltip("Radius of the impact damage area.")]
        private float impactRadius = 2.0f;
        
        [SerializeField, Tooltip("Layer mask for detecting damageable entities.")]
        private LayerMask damageLayer;

        [Header("=== FALLING METEOR ===")]
        [SerializeField, Tooltip("Prefab for the falling meteor visual (should have MeteorProjectile component).")]
        private GameObject meteorPrefab;
        
        [SerializeField, Tooltip("Height above the target to spawn the falling meteor.")]
        private float fallHeight = 20f;
        
        [SerializeField, Tooltip("Speed at which the meteor falls (units per second).")]
        private float fallSpeed = 20f;

        [Header("=== WARNING PHASE (Stage 1) ===")]
        [SerializeField, Tooltip("Prefab for the warning indicator on the ground.")]
        private GameObject warningPrefab;

        [Header("=== IMPACT VISUALS (Stage 3 - All Spawn Simultaneously) ===")]
        [SerializeField, Tooltip("Explosion VFX prefab.")]
        private GameObject explosionPrefab;
        
        [SerializeField, Tooltip("How long the explosion VFX lasts before destruction.")]
        private float explosionDuration = 2.0f;
        
        [SerializeField, Tooltip("Burn mark decal prefab.")]
        private GameObject burnMarkPrefab;
        
        [SerializeField, Tooltip("How long the burn mark stays visible.")]
        private float burnMarkDuration = 10.0f;
        
        [SerializeField, Tooltip("Ground cracks decal prefab.")]
        private GameObject cracksPrefab;
        
        [SerializeField, Tooltip("How long the cracks stay visible.")]
        private float cracksDuration = 10.0f;

        [Header("=== FIRE POOL (Lingering Hazard) ===")]
        [SerializeField, Tooltip("Fire pool prefab that damages player over time.")]
        private GameObject firePoolPrefab;
        
        [SerializeField, Tooltip("How long the fire pool remains active.")]
        private float firePoolDuration = 5.0f;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Show gizmos in Scene view.")]
        private bool showGizmos = true;
        
        [SerializeField, Tooltip("Log debug messages to console.")]
        private bool debugLog = false;

        [Header("=== SOUND EFFECTS ===")]
        [SerializeField] private SoundEvent meteorFallSound;
        [SerializeField] private SoundEvent meteorImpactSound;

        #endregion

        #region Private State

        private Vector3 _impactPosition;
        private GameObject _warningInstance;
        private GameObject _meteorInstance;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            StartStrike();
        }
        
        private void OnEnable()
        {
            // Reset state for pooled reuse
            _warningInstance = null;
            _meteorInstance = null;
            
            // When re-enabled from pool, restart the strike sequence
            // Skip on first enable (handled by Start)
            if (_hasStartedOnce)
            {
                StartStrike();
            }
        }
        
        private bool _hasStartedOnce = false;
        
        private void StartStrike()
        {
            _hasStartedOnce = true;
            
            // Determine impact position - use current position, but ensure Y=0 (ground level)
            _impactPosition = transform.position;
            _impactPosition.y = 0f;
            
            // Also update this object's position to ground level
            transform.position = _impactPosition;

            StartCoroutine(StrikeSequence());
        }

        #endregion

        #region Main Sequence

        private IEnumerator StrikeSequence()
        {
            if (debugLog) Debug.Log($"[MeteorStrike] Starting at {_impactPosition}");

            // ============================================
            // STAGE 1: WARNING
            // ============================================
            SpawnWarning();

            // Calculate when to spawn the falling meteor so it arrives exactly on time
            float fallDuration = fallHeight / Mathf.Max(0.1f, fallSpeed);
            float delayBeforeMeteorSpawn = warningDuration - fallDuration;

            if (debugLog) Debug.Log($"[MeteorStrike] Warning duration: {warningDuration}s, Fall duration: {fallDuration}s, Delay before spawn: {delayBeforeMeteorSpawn}s");

            // Wait until it's time to spawn the falling meteor
            if (delayBeforeMeteorSpawn > 0)
            {
                yield return new WaitForSeconds(delayBeforeMeteorSpawn);
            }

            // ============================================
            // STAGE 2: FALLING
            // ============================================
            SpawnFallingMeteor(fallDuration);
            
            // Play falling meteor sound
            if (meteorFallSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(meteorFallSound, _impactPosition);

            // Wait for remaining time until impact
            float remainingTime = Mathf.Max(0, warningDuration - Mathf.Max(0, delayBeforeMeteorSpawn));
            if (remainingTime > 0)
            {
                yield return new WaitForSeconds(remainingTime);
            }

            // ============================================
            // STAGE 3: IMPACT
            // ============================================
            if (debugLog) Debug.Log($"[MeteorStrike] IMPACT!");

            // Clean up warning and meteor visuals
            CleanupPreImpact();

            // Spawn all impact visuals SIMULTANEOUSLY
            SpawnImpactEffects();

            // Play impact sound
            if (meteorImpactSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(meteorImpactSound, _impactPosition);

            // Apply damage to entities in radius
            ApplyImpactDamage();

            // Spawn lingering fire pool
            SpawnFirePool();

            // Return to object pool instead of destroying (or destroy if no pool)
            ReturnToPool();
        }

        #endregion

        #region Stage Methods

        private void SpawnWarning()
        {
            if (warningPrefab == null) return;

            // Warning indicator lies flat on the ground (rotated 90 degrees on X)
            Quaternion groundRotation = Quaternion.Euler(90f, 0f, 0f);
            _warningInstance = Instantiate(warningPrefab, _impactPosition, groundRotation);

            if (debugLog) Debug.Log($"[MeteorStrike] Warning spawned at {_impactPosition}");
        }

        private void SpawnFallingMeteor(float travelDuration)
        {
            if (meteorPrefab == null) return;

            // Spawn position is directly above the impact point
            Vector3 spawnPosition = _impactPosition + Vector3.up * fallHeight;

            // Meteor should face downward
            Quaternion lookDown = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            _meteorInstance = Instantiate(meteorPrefab, spawnPosition, lookDown);

            // Initialize the MeteorProjectile component
            MeteorProjectile projectile = _meteorInstance.GetComponent<MeteorProjectile>();
            if (projectile == null)
            {
                projectile = _meteorInstance.AddComponent<MeteorProjectile>();
            }

            // Initialize with exact travel duration for guaranteed timing
            projectile.Initialize(_impactPosition, travelDuration);

            if (debugLog) Debug.Log($"[MeteorStrike] Meteor spawned at {spawnPosition}, falling to {_impactPosition} over {travelDuration}s");
        }

        private void CleanupPreImpact()
        {
            if (_warningInstance != null)
            {
                Destroy(_warningInstance);
                _warningInstance = null;
            }

            if (_meteorInstance != null)
            {
                Destroy(_meteorInstance);
                _meteorInstance = null;
            }
        }

        private void SpawnImpactEffects()
        {
            // All three effects spawn at the SAME TIME on impact
            
            // 1. Explosion VFX (destroyed after explosionDuration)
            if (explosionPrefab != null)
            {
                GameObject explosion = Instantiate(explosionPrefab, _impactPosition, Quaternion.identity);
                Destroy(explosion, explosionDuration);
                if (debugLog) Debug.Log($"[MeteorStrike] Explosion VFX spawned, will destroy in {explosionDuration}s");
            }

            // 2. Burn Mark (stays for configured duration) - random rotation & scale for variety
            if (burnMarkPrefab != null)
            {
                float randomYRotation = Random.Range(0f, 360f);
                Quaternion decalRotation = Quaternion.Euler(90f, randomYRotation, 0f);
                GameObject burnMark = Instantiate(burnMarkPrefab, _impactPosition, decalRotation);
                
                // Random scale variance ±0.3 from original
                float scaleVariance = Random.Range(-0.3f, 0.3f);
                burnMark.transform.localScale *= (1f + scaleVariance);
                
                Destroy(burnMark, burnMarkDuration);
                if (debugLog) Debug.Log($"[MeteorStrike] Burn mark spawned, will last {burnMarkDuration}s");
            }

            // 3. Cracks (stays for configured duration) - random Y rotation for variety
            if (cracksPrefab != null)
            {
                float randomYRotation = Random.Range(0f, 360f);
                Quaternion decalRotation = Quaternion.Euler(90f, randomYRotation, 0f);
                GameObject cracks = Instantiate(cracksPrefab, _impactPosition, decalRotation);
                Destroy(cracks, cracksDuration);
                if (debugLog) Debug.Log($"[MeteorStrike] Cracks spawned, will last {cracksDuration}s");
            }
        }

        private void SpawnFirePool()
        {
            if (firePoolPrefab == null) return;

            GameObject firePool = Instantiate(firePoolPrefab, _impactPosition, Quaternion.identity);
            Destroy(firePool, firePoolDuration);

            // Camera shake for meteor impact (explosion-style environmental shake)
            CameraShakeManager.Shake(CameraShakePreset.Meteor);

            if (debugLog) Debug.Log($"[MeteorStrike] Fire pool spawned, will last {firePoolDuration}s");
        }
        
        /// <summary>
        /// Returns this MeteorStrike to the object pool for reuse.
        /// Falls back to Destroy if pool is not available.
        /// </summary>
        private void ReturnToPool()
        {
            // Stop any running coroutines before returning to pool
            StopAllCoroutines();
            
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ApplyImpactDamage()
        {
            Collider[] hits = Physics.OverlapSphere(_impactPosition, impactRadius, damageLayer);

            foreach (Collider hit in hits)
            {
                // Check for EnemyHealth first (for hazard XP tracking)
                EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
                if (enemyHealth == null)
                    enemyHealth = hit.GetComponentInParent<EnemyHealth>();
                
                if (enemyHealth != null)
                {
                    // Use hazard damage - grants 50% XP if this is the killing blow
                    enemyHealth.TakeDamageFromHazard(impactDamage);
                    if (debugLog) Debug.Log($"[MeteorStrike] Applied {impactDamage} hazard damage to {hit.name}");
                    continue;
                }
                
                // Fallback to IDamageable for non-enemy targets (player, destructibles, etc.)
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable == null)
                    damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    damageable.TakeDamage(impactDamage);
                    if (debugLog) Debug.Log($"[MeteorStrike] Applied {impactDamage} damage to {hit.name}");
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Vector3 pos = Application.isPlaying ? _impactPosition : transform.position;
            
            // Ensure we show at ground level in editor
            if (!Application.isPlaying)
            {
                pos.y = 0f;
            }

            // Impact radius (red circle)
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            DrawGizmoCircle(pos, impactRadius, 32);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pos, impactRadius);

            // Fall path (blue line from sky to ground)
            Gizmos.color = Color.cyan;
            Vector3 spawnPos = pos + Vector3.up * fallHeight;
            Gizmos.DrawLine(pos, spawnPos);
            Gizmos.DrawWireSphere(spawnPos, 0.5f);

            // Impact center (yellow dot)
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f);
            Gizmos.DrawSphere(pos, 0.3f);

            // Fire pool area (orange circle, slightly larger for visibility)
            if (firePoolPrefab != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                DrawGizmoCircle(pos + Vector3.up * 0.02f, impactRadius * 0.8f, 24);
            }
        }

        private void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }

        #endregion
    }
}
