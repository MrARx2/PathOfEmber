using UnityEngine;
using System.Collections;

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
        
        [SerializeField, Tooltip("Spawn points for chasers")]
        private Transform[] spawnPoints;
        
        [SerializeField, Tooltip("Number of chasers to spawn per execution")]
        private int spawnCount = 3;
        
        [SerializeField, Tooltip("Delay between each chaser spawn")]
        private float spawnDelay = 0.3f;
        
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
        
        /// <summary>
        /// Executes the summon attack.
        /// </summary>
        public void Execute()
        {
            Debug.Log("[TitanSummonAttack] Execute() called!");
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
                Debug.LogWarning("[TitanSummonAttack] Chaser prefab is not assigned!");
                yield break;
            }
            
            // Spawn chasers
            int spawned = 0;
            int spawnIndex = 0;
            
            while (spawned < spawnCount)
            {
                // Get spawn position (cycle through spawn points or use random)
                Vector3 spawnPos;
                if (spawnPoints != null && spawnPoints.Length > 0)
                {
                    spawnPos = spawnPoints[spawnIndex % spawnPoints.Length].position;
                    spawnIndex++;
                }
                else
                {
                    // Random position around the boss
                    Vector2 offset = Random.insideUnitCircle * 5f;
                    spawnPos = transform.position + new Vector3(offset.x, 0, offset.y);
                }
                
                // Spawn chaser
                GameObject chaser = Instantiate(chaserPrefab, spawnPos, Quaternion.identity);
                spawned++;
                
                if (debugLog)
                    Debug.Log($"[TitanSummonAttack] Spawned chaser {spawned}/{spawnCount} at {spawnPos}");
                
                yield return new WaitForSeconds(spawnDelay);
            }
            
            if (debugLog)
                Debug.Log("[TitanSummonAttack] Summon complete");
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
            if (spawnPoints == null) return;
            
            Gizmos.color = Color.magenta;
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
