using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Audio; // Required for SoundEvent and AudioManager

namespace Boss
{
    /// <summary>
    /// Titan's left hand summon attack.
    /// Spawns Chaser enemies and ramps material emission during animation.
    /// </summary>
    public class TitanSummonAttack : MonoBehaviour
    {
        [Header("Chaser Spawning")]
        [SerializeField, Tooltip("Chaser enemy prefab to spawn")]
        private GameObject chaserPrefab;
        
        [SerializeField, Tooltip("Spawn points for chasers (Fallback if area not set)")]
        private Transform[] spawnPoints;

        [SerializeField, Tooltip("Area to spawn chasers randomly within")]
        private BoxCollider spawnArea;
        
        [SerializeField, Tooltip("Number of chasers to spawn per execution")]
        private int spawnCount = 3;
        
        [SerializeField, Tooltip("Delay between each chaser spawn")]
        private float spawnDelay = 0.3f;
        
        [Header("Spawn Indicators")]
        [SerializeField, Tooltip("If true, shows indicator decals before spawning enemies")]
        private bool useSpawnIndicator = true;
        
        [SerializeField, Tooltip("Prefab for the indicator decal/VFX")]
        private GameObject indicatorPrefab;
        
        [SerializeField, Tooltip("Delay before enemy spawns while indicator is shown")]
        private float indicatorDuration = 1.5f;
        
        [SerializeField] private Vector3 indicatorPositionOffset = new Vector3(0f, 0.05f, 0f);
        [SerializeField] private Vector3 indicatorRotationOffset = new Vector3(90f, 0f, 0f);
        
        [Header("Spawn VFX")]
        [SerializeField, Tooltip("Optional VFX to play when enemy actually spawns")]
        private GameObject spawnVFXPrefab;
        
        [SerializeField] private Vector3 spawnVFXPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 spawnVFXRotationOffset = new Vector3(-90f, 0f, 0f);
        
        [SerializeField] private Audio.SoundEvent spawnSound;

        [Header("Animation Sync")]
        [SerializeField, Tooltip("Delay after animation trigger before spawning starts")]
        private float startDelay = 0.5f;
        
        [Header("Material Emission")]
        [SerializeField, Tooltip("Material for the hand with emission (assign directly)")]
        private Material handMaterial;
        
        [SerializeField, Tooltip("Emission color when summoning")]
        private Color emissionColor = new Color(1f, 0.5f, 0f, 1f); // Orange
        
        [SerializeField, Tooltip("Maximum emission intensity")]
        private float maxEmissionIntensity = 3f;
        
        [SerializeField, Tooltip("Emission ramp curve over the summon duration")]
        private AnimationCurve emissionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [SerializeField, Tooltip("Total duration of the emission effect")]
        private float emissionDuration = 2f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private Color originalEmission;
        private bool hadEmissionEnabled;
        
        private void Awake()
        {
            // Cache original emission state
            if (handMaterial != null && handMaterial.HasProperty("_EmissionColor"))
            {
                originalEmission = handMaterial.GetColor("_EmissionColor");
                hadEmissionEnabled = handMaterial.IsKeywordEnabled("_EMISSION");
            }
        }

        private IEnumerator Start()
        {
            // Wait for ObjectPoolManager
            while (ObjectPoolManager.Instance == null) yield return null;

            // Pre-warm assets
            if (chaserPrefab != null)
                yield return ObjectPoolManager.Instance.PrewarmAsync(chaserPrefab, spawnCount * 2, 2);
            
            if (indicatorPrefab != null)
                yield return ObjectPoolManager.Instance.PrewarmAsync(indicatorPrefab, spawnCount * 2, 2);
            
            if (spawnVFXPrefab != null)
                yield return ObjectPoolManager.Instance.PrewarmAsync(spawnVFXPrefab, spawnCount * 2, 2);
        }
        
        /// <summary>
        /// Executes the summon attack.
        /// </summary>
        public void Execute()
        {
            // Debug.Log("[TitanSummonAttack] Execute() called!");
            StartCoroutine(SummonSequence());
        }
        
        private IEnumerator SummonSequence()
        {
            // Start emission effect
            StartCoroutine(EmissionRampRoutine());
            
            // Wait for animation sync
            yield return new WaitForSeconds(startDelay);
            
            if (chaserPrefab == null)
            {
                if (debugLog) Debug.LogWarning("[TitanSummonAttack] Chaser prefab is not assigned!");
                yield break;
            }
            
            // 1. Calculate all spawn positions first
            var spawnData = CalculateSpawnPositions();
            
            // 2. Show indicators if enabled
            List<GameObject> activeIndicators = new List<GameObject>();
            if (useSpawnIndicator && indicatorPrefab != null)
            {
                foreach (var pos in spawnData)
                {
                    Vector3 indPos = pos + indicatorPositionOffset;
                    Quaternion indRot = Quaternion.Euler(indicatorRotationOffset);
                    
                    // Use ObjectPool for indicators
                    GameObject indicator = ObjectPoolManager.Instance != null
                        ? ObjectPoolManager.Instance.Get(indicatorPrefab, indPos, indRot)
                        : Instantiate(indicatorPrefab, indPos, indRot);
                    
                    activeIndicators.Add(indicator);
                }
                
                // Wait for indicator duration
                yield return new WaitForSeconds(indicatorDuration);
            }
            
            // 3. Spawn enemies
            for (int i = 0; i < spawnData.Count; i++)
            {
                Vector3 pos = spawnData[i];
                
                // Spawn VFX (pooled)
                if (spawnVFXPrefab != null)
                {
                    Vector3 vfxPos = pos + spawnVFXPositionOffset;
                    Quaternion vfxRot = Quaternion.Euler(spawnVFXRotationOffset);
                    
                    GameObject vfx = ObjectPoolManager.Instance != null
                        ? ObjectPoolManager.Instance.Get(spawnVFXPrefab, vfxPos, vfxRot)
                        : Instantiate(spawnVFXPrefab, vfxPos, vfxRot);
                    
                    // Return pooled VFX after delay, or destroy if not pooled
                    StartCoroutine(ReturnPooledAfterDelay(vfx, 3f));
                }
                
                // Play Sound
                if (spawnSound != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayAtPosition(spawnSound, pos);
                }
                
                // Spawn Enemy (pooled for GC reduction)
                GameObject chaser = ObjectPoolManager.Instance != null
                    ? ObjectPoolManager.Instance.Get(chaserPrefab, pos, Quaternion.identity)
                    : Instantiate(chaserPrefab, pos, Quaternion.identity);
                
                if (debugLog)
                    Debug.Log($"[TitanSummonAttack] Spawned chaser {i+1}/{spawnCount} at {pos}");
                
                // Return pooled indicator and mark as null to prevent double-release
                if (i < activeIndicators.Count && activeIndicators[i] != null)
                {
                    if (ObjectPoolManager.Instance != null)
                        ObjectPoolManager.Instance.Return(activeIndicators[i]);
                    else
                        Destroy(activeIndicators[i]);
                    activeIndicators[i] = null; // Prevent double-release in cleanup
                }
                
                // Stagger delay
                if (spawnDelay > 0 && i < spawnData.Count - 1)
                    yield return new WaitForSeconds(spawnDelay);
            }
            
            // Cleanup any remaining indicators that weren't returned (safety cleanup)
            for (int i = 0; i < activeIndicators.Count; i++)
            {
                if (activeIndicators[i] != null)
                {
                    if (ObjectPoolManager.Instance != null)
                        ObjectPoolManager.Instance.Return(activeIndicators[i]);
                    else
                        Destroy(activeIndicators[i]);
                    activeIndicators[i] = null;
                }
            }
            activeIndicators.Clear();
            
            if (debugLog)
                Debug.Log("[TitanSummonAttack] Summon complete");
        }
        
        private IEnumerator ReturnPooledAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
            {
                if (ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.Return(obj);
                else
                    Destroy(obj);
            }
        }
        
        private System.Collections.Generic.List<Vector3> CalculateSpawnPositions()
        {
            var positions = new System.Collections.Generic.List<Vector3>();
            int spawnIndex = 0;
            
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 pos;
                if (spawnArea != null)
                {
                    // Random position within BoxCollider bounds
                    Bounds bounds = spawnArea.bounds;
                    float rx = Random.Range(bounds.min.x, bounds.max.x);
                    float rz = Random.Range(bounds.min.z, bounds.max.z);
                    pos = new Vector3(rx, bounds.center.y, rz);
                }
                else if (spawnPoints != null && spawnPoints.Length > 0)
                {
                    pos = spawnPoints[spawnIndex % spawnPoints.Length].position;
                    spawnIndex++;
                }
                else
                {
                    // Fallback randomness
                    Vector2 offset = Random.insideUnitCircle * 5f;
                    pos = transform.position + new Vector3(offset.x, 0, offset.y);
                }
                positions.Add(pos);
            }
            return positions;
        }
        
        private IEnumerator EmissionRampRoutine()
        {
            if (handMaterial == null) yield break;
            if (!handMaterial.HasProperty("_EmissionColor")) yield break;
            
            // Enable emission
            handMaterial.EnableKeyword("_EMISSION");
            
            float elapsed = 0f;
            while (elapsed < emissionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / emissionDuration;
                float intensity = emissionCurve.Evaluate(t) * maxEmissionIntensity;
                
                handMaterial.SetColor("_EmissionColor", emissionColor * intensity);
                yield return null;
            }
            
            // Restore original emission
            handMaterial.SetColor("_EmissionColor", originalEmission);
            if (!hadEmissionEnabled)
            {
                handMaterial.DisableKeyword("_EMISSION");
            }
        }
        
        /// <summary>
        /// Called from animation event when summoning.
        /// </summary>
        public void OnSummonPulse()
        {
            // Can be used for per-spawn effects
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            
            if (spawnArea != null)
            {
                Gizmos.matrix = spawnArea.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(spawnArea.center, spawnArea.size);
                return;
            }
            
            if (spawnPoints != null)
            {
                foreach (var point in spawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.5f);
                    }
                }
            }
        }
    }
}
