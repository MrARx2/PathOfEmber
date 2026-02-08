using UnityEngine;
using System.Collections;

namespace Boss
{
    /// <summary>
    /// Titan's right hand fist attack.
    /// Spawns a crack prefab that moves with Y position controlled by a ramp curve.
    /// </summary>
    public class TitanFistAttack : MonoBehaviour
    {
        [Header("Crack Configuration")]
        [SerializeField, Tooltip("Crack prefab with MeshCollider trigger")]
        private GameObject crackPrefab;
        
        [SerializeField, Tooltip("Where the crack spawns (fist impact point)")]
        private Transform spawnPoint;
        
        [Header("Animation")]
        [SerializeField, Tooltip("Duration in seconds for the crack animation")]
        private float length = 2f;
        
        [SerializeField, Tooltip("Ramp curve controlling Y position over time (0-1). Y value = actual Y position of crack.")]
        private AnimationCurve ramp = new AnimationCurve(
            new Keyframe(0f, 0f),      // Start at ground
            new Keyframe(0.25f, 1f),   // Rise up
            new Keyframe(0.5f, 0f),    // Back to ground
            new Keyframe(0.75f, 1f),   // Rise up again
            new Keyframe(1f, 0f)       // End at ground
        );
        
        [SerializeField, Tooltip("Maximum Y position value (curve Y=1 means this height)")]
        private float maxYPosition = 2f;
        
        [SerializeField, Tooltip("Minimum Y position (curve Y=0 means this height, usually ground level)")]
        private float minYPosition = 0.05f;
        
        [SerializeField, Tooltip("How long the crack stays visible after animation ends")]
        private float lingerDuration = 1f;
        
        [Header("Damage")]
        [SerializeField] private int damageAmount = 50;
        [SerializeField] private LayerMask playerLayer;
        
        [Header("Animation Sync")]
        [SerializeField, Tooltip("Delay after Execute before crack spawns")]
        private float spawnDelay = 0f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private GameObject currentCrack;

        private IEnumerator Start()
        {
            // Wait for ObjectPoolManager
            while (ObjectPoolManager.Instance == null) yield return null;
            
            // Pre-warm assets
            if (crackPrefab != null)
                yield return ObjectPoolManager.Instance.PrewarmAsync(crackPrefab, 5, 1);
        }
        
        /// <summary>
        /// Executes the fist attack.
        /// </summary>
        public void Execute()
        {
            StartCoroutine(FistAttackSequence());
        }
        
        private IEnumerator FistAttackSequence()
        {
            // Wait for spawn delay if any
            if (spawnDelay > 0f)
                yield return new WaitForSeconds(spawnDelay);
            
            if (crackPrefab == null)
            {
                Debug.LogWarning("[TitanFistAttack] Crack prefab is not assigned!");
                yield break;
            }
            
            // Determine spawn position
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            
            // Set initial Y from ramp curve at t=0
            float initialY = Mathf.Lerp(minYPosition, maxYPosition, ramp.Evaluate(0f));
            spawnPos.y = initialY;
            
            // Spawn crack with 180 degree Y rotation offset
            Quaternion spawnRotation = Quaternion.Euler(0f, 180f, 0f);
            currentCrack = Instantiate(crackPrefab, spawnPos, spawnRotation);
            
            // Setup damage component
            TitanCrackDamage crackDamage = currentCrack.GetComponent<TitanCrackDamage>();
            if (crackDamage == null)
            {
                crackDamage = currentCrack.AddComponent<TitanCrackDamage>();
            }
            crackDamage.Initialize(damageAmount, playerLayer);
            
            if (debugLog)
                Debug.Log($"[TitanFistAttack] Crack spawned, animating for {length}s");
            
            // Animate Y position using ramp over the length duration
            float elapsed = 0f;
            Vector3 pos = currentCrack.transform.position;
            
            while (elapsed < length && currentCrack != null)
            {
                elapsed += Time.deltaTime;
                
                // Calculate normalized progress (0 to 1)
                float t = Mathf.Clamp01(elapsed / length);
                
                // Get Y position from ramp curve
                float rampValue = ramp.Evaluate(t);
                float yPos = Mathf.Lerp(minYPosition, maxYPosition, rampValue);
                
                // Apply Y position (X and Z stay the same)
                pos.y = yPos;
                currentCrack.transform.position = pos;
                
                yield return null;
            }
            
            // Linger then destroy
            if (currentCrack != null)
            {
                yield return new WaitForSeconds(lingerDuration);
                Destroy(currentCrack);
                currentCrack = null;
            }
            
            if (debugLog)
                Debug.Log("[TitanFistAttack] Crack complete");
        }
        
        /// <summary>
        /// Called from animation event when fist hits ground.
        /// </summary>
        public void OnFistImpact()
        {
            Execute();
        }
        
        private void OnDrawGizmosSelected()
        {
            Vector3 start = spawnPoint != null ? spawnPoint.position : transform.position;
            start.y = minYPosition;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(start, 0.5f);
            
            // Draw height line
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(start, start + Vector3.up * maxYPosition);
        }
    }
    
    /// <summary>
    /// Damage component attached to the crack prefab.
    /// Deals damage when player enters the trigger.
    /// </summary>
    public class TitanCrackDamage : MonoBehaviour
    {
        private int damage;
        private LayerMask targetLayer;
        private bool hasDealtDamage = false;
        private bool debugLog = false;
        
        public void Initialize(int damageAmount, LayerMask layer, bool enableDebug = false)
        {
            damage = damageAmount;
            targetLayer = layer;
            hasDealtDamage = false;
            debugLog = enableDebug;
            if (debugLog) Debug.Log($"[TitanCrackDamage] Initialized with damage={damage}, layer={layer.value}");
            
            // Verify we have a trigger collider
            Collider col = GetComponent<Collider>();
            if (col == null) col = GetComponentInChildren<Collider>();
            if (col == null)
                Debug.LogError("[TitanCrackDamage] NO COLLIDER FOUND on crack prefab!");
            else if (!col.isTrigger)
                Debug.LogWarning($"[TitanCrackDamage] Collider {col.GetType().Name} is NOT a trigger! Setting it now.");
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (debugLog) Debug.Log($"[TitanCrackDamage] OnTriggerEnter: {other.name} (layer {other.gameObject.layer})");
            
            if (hasDealtDamage) return;
            
            // Check if it's the player
            int otherLayerMask = 1 << other.gameObject.layer;
            if ((otherLayerMask & targetLayer) == 0)
            {
                if (debugLog) Debug.Log($"[TitanCrackDamage] Layer mismatch: object layer mask {otherLayerMask}, target layer mask {targetLayer.value}");
                return;
            }
            
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = other.GetComponentInParent<IDamageable>();
            
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                hasDealtDamage = true;
                if (debugLog) Debug.Log($"[TitanCrackDamage] SUCCESS! Dealt {damage} damage to {other.name}");
            }
            else
            {
                Debug.LogWarning($"[TitanCrackDamage] No IDamageable found on {other.name}");
            }
        }
        
        private void OnTriggerStay(Collider other)
        {
            // Fallback in case player is already inside when crack spawns
            if (!hasDealtDamage)
            {
                OnTriggerEnter(other);
            }
        }
    }
}
