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

            // Spawn lingering fire pool (returns itself via coroutine)
            SpawnFirePool();

            // IMPORTANT: MeteorStrike must stay active while child effects (Fire, Cracks, BurnMarks) play out.
            // We wait for the longest duration among all possible effects to ensure their return coroutines finish.
            float maxDuration = Mathf.Max(explosionDuration, burnMarkDuration, cracksDuration, firePoolDuration);
            
            // Add a small buffer to be safe
            yield return new WaitForSeconds(maxDuration + 0.5f);

            // Return to object pool instead of destroying (or destroy if no pool)
            ReturnToPool();
        }

        #endregion

        #region Stage Methods

        private void SpawnWarning()
        {
            if (warningPrefab == null) return;

            Quaternion groundRotation = Quaternion.Euler(90f, 0f, 0f);
            
            // Pool Check
            if (ObjectPoolManager.Instance != null)
                _warningInstance = ObjectPoolManager.Instance.Get(warningPrefab, _impactPosition, groundRotation);
            else
                _warningInstance = Instantiate(warningPrefab, _impactPosition, groundRotation);

            if (debugLog) Debug.Log($"[MeteorStrike] Warning spawned at {_impactPosition}");
        }

        private void SpawnFallingMeteor(float travelDuration)
        {
            if (meteorPrefab == null) return;

            Vector3 spawnPosition = _impactPosition + Vector3.up * fallHeight;
            Quaternion lookDown = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            // Pool Check
            if (ObjectPoolManager.Instance != null)
                _meteorInstance = ObjectPoolManager.Instance.Get(meteorPrefab, spawnPosition, lookDown);
            else
                _meteorInstance = Instantiate(meteorPrefab, spawnPosition, lookDown);

            MeteorProjectile projectile = _meteorInstance.GetComponent<MeteorProjectile>();
            if (projectile == null) projectile = _meteorInstance.AddComponent<MeteorProjectile>();

            projectile.Initialize(_impactPosition, travelDuration);

            if (debugLog) Debug.Log($"[MeteorStrike] Meteor spawned at {spawnPosition}, falling to {_impactPosition} over {travelDuration}s");
        }

        private void CleanupPreImpact()
        {
            if (_warningInstance != null)
            {
                if (ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.Return(_warningInstance);
                else Destroy(_warningInstance);
                _warningInstance = null;
            }

            if (_meteorInstance != null)
            {
                if (ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.Return(_meteorInstance);
                else Destroy(_meteorInstance);
                _meteorInstance = null;
            }
        }

        private void SpawnImpactEffects()
        {
            // Explosion
            if (explosionPrefab != null)
            {
                GameObject explosion = SpawnPooled(explosionPrefab, _impactPosition, Quaternion.identity);
                StartCoroutine(ReturnPooledDelayed(explosion, explosionDuration));
            }

            // Burn Mark
            if (burnMarkPrefab != null)
            {
                float randomY = Random.Range(0f, 360f);
                Quaternion rot = Quaternion.Euler(90f, randomY, 0f);
                GameObject burnMark = SpawnPooled(burnMarkPrefab, _impactPosition, rot);
                
                // Random scale
                float scaleVariance = Random.Range(-0.3f, 0.3f);
                burnMark.transform.localScale = Vector3.one * (1f + scaleVariance); // Reset scale first if pooled? No, assume prefab scale * variance. 
                // Careful: Pooled objects keep modified scale. We should reset it or use multiplier.
                // Better to capture original scale? For now, we assume Vector3.one base or just strict set.
                // burnMark.transform.localScale *= (1f + scaleVariance); // dangerous on pooled obj
                
                StartCoroutine(ReturnPooledDelayed(burnMark, burnMarkDuration));
            }

            // Cracks
            if (cracksPrefab != null)
            {
                float randomY = Random.Range(0f, 360f);
                Quaternion rot = Quaternion.Euler(90f, randomY, 0f);
                GameObject cracks = SpawnPooled(cracksPrefab, _impactPosition, rot);
                StartCoroutine(ReturnPooledDelayed(cracks, cracksDuration));
            }
        }

        private void SpawnFirePool()
        {
            if (firePoolPrefab == null) return;

            GameObject firePool = SpawnPooled(firePoolPrefab, _impactPosition, Quaternion.identity);
            StartCoroutine(ReturnPooledDelayed(firePool, firePoolDuration));

            CameraShakeManager.Shake(CameraShakePreset.Meteor);
        }
        
        /// <summary>
        /// Helper to return objects to pool after delay.
        /// </summary>
        private IEnumerator ReturnPooledDelayed(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
            {
                 if (ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.Return(obj);
                 else Destroy(obj);
            }
        }
        
        /// <summary>
        /// Helper to Spawn from pool or instantiate
        /// </summary>
        private GameObject SpawnPooled(GameObject prefab, Vector3 pos, Quaternion rot)
        {
             if (ObjectPoolManager.Instance != null) return ObjectPoolManager.Instance.Get(prefab, pos, rot);
             return Instantiate(prefab, pos, rot);
        }

        private void ReturnToPool()
        {
            // Do NOT StopAllCoroutines, we are waiting for children to return!
            // StopAllCoroutines(); 
            
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Return(gameObject);
            else
                Destroy(gameObject);
        }
        
        // IMPORTANT: We need to modify StrikeSequence to wait for children before returning itself!


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
