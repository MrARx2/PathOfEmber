using UnityEngine;

namespace Boss
{
    /// <summary>
    /// Targetable socket on a Titan rig bone.
    /// Has a stretched collider for reliable arrow hits and routes damage to its health part.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    /// <summary>
    /// Targetable socket on a Titan rig bone.
    /// Has a stretched collider for reliable arrow hits and routes damage to its health part.
    /// Implement IDamageable so projectiles hitting this collider find a valid target immediately.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TitanTargetSocket : MonoBehaviour, IDamageable
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("The health component this socket belongs to")]
        private TitanHealth healthPart;
        
        [SerializeField, Tooltip("Optional: transform to use as visual center for effects")]
        private Transform visualCenter;
        
        [Header("Targeting")]
        [SerializeField, Tooltip("Is this socket currently targetable by the player?")]
        private bool isTargetable = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        public TitanHealth HealthPart => healthPart;
        // Key fix: Removed "!healthPart.IsDestroyed" check. 
        // Targeting is now fully controlled by SetTargetable(), which TitanBossController manages.
        // This keeps the Core targetable (and camera locked) during death animation.
        public bool IsTargetable => isTargetable && healthPart != null;
        public Vector3 VisualCenter => visualCenter != null ? visualCenter.position : transform.position;
        
        private Quaternion lockedRotation;
        
        private void Awake()
        {
            // Lock the initial rotation
            lockedRotation = transform.rotation;
            
            // Ensure this has the Enemy tag for player targeting
            if (!gameObject.CompareTag("Enemy"))
            {
                if (debugLog)
                    Debug.LogWarning($"[TitanTargetSocket] {gameObject.name} should have 'Enemy' tag for player targeting!");
            }
            
            // Auto-find health component if not assigned
            if (healthPart == null)
            {
                healthPart = GetComponentInParent<TitanHealth>();
            }

            // FORCE KINEMATIC: essential for colliders attached to animated bones!
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
        
        private void LateUpdate()
        {
            // Keep rotation locked (don't follow bone rotation)
            transform.rotation = lockedRotation;
        }
        
        private void Start()
        {
            // Register with EnemyRegistry for player targeting
            if (isTargetable)
            {
                EnemyRegistry.Register(transform);
            }
        }
        
        private void OnDisable()
        {
            EnemyRegistry.Unregister(transform);
        }
        
        private void OnDestroy()
        {
            EnemyRegistry.Unregister(transform);
        }
        
        /// <summary>
        /// Enable/disable targeting for this socket.
        /// </summary>
        public void SetTargetable(bool targetable)
        {
            if (isTargetable == targetable) return;
            
            isTargetable = targetable;
            
            if (targetable)
            {
                EnemyRegistry.Register(transform);
            }
            else
            {
                EnemyRegistry.Unregister(transform);
            }
            
            if (debugLog)
                Debug.Log($"[TitanTargetSocket] {gameObject.name} targetable: {targetable}");
        }
        
        // IDamageable Implementation
        public void TakeDamage(int damage)
        {
            RouteDamage(damage);
        }

        public Transform GetTransform()
        {
            return transform;
        }

        /// <summary>
        /// Routes damage to the associated health part.
        /// Called by ArrowProjectile when it hits this collider.
        /// </summary>
        public void RouteDamage(int damage)
        {
            if (!isTargetable) return;
            if (healthPart == null) return;
            
            healthPart.TakeDamage(damage);
            
            if (debugLog)
                Debug.Log($"[TitanTargetSocket] Routed {damage} damage to {healthPart.BodyPart}");
        }
    }
}
