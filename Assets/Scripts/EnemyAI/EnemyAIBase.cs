using UnityEngine;
using UnityEngine.AI;

namespace EnemyAI
{
    /// <summary>
    /// Base class for all enemy AI behaviors.
    /// Simple, foolproof implementation that always chases the player.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    public abstract class EnemyAIBase : MonoBehaviour
    {
        #region Serialized Fields

        [Header("=== TARGET ===")]
        [SerializeField, Tooltip("Target to chase. Auto-finds player if not set.")]
        protected Transform target;

        [Header("=== MODEL REFERENCE ===")]
        [SerializeField, Tooltip("Optional: Model child transform for accurate positioning")]
        protected Transform modelTransform;

        [Header("=== MOVEMENT ===")]
        [SerializeField, Tooltip("Movement speed (units per second).")]
        protected float moveSpeed = 3.5f;
        
        [SerializeField, Tooltip("Range at which enemy stops to attack.")]
        protected float attackRange = 2f;

        [Header("=== NAVMESH TUNING ===")]
        [SerializeField, Tooltip("How close to get before stopping (multiplier of attack range). Smaller = closer to target.")]
        protected float stoppingDistanceMultiplier = 0.2f;
        
        [SerializeField, Tooltip("Agent collision radius for pathfinding. Larger = more stable paths.")]
        protected float agentRadius = 0.25f;
        
        [SerializeField, Tooltip("How fast the agent accelerates. Lower = less overshooting.")]
        protected float agentAcceleration = 4f;

        [Header("=== ATTACK TIMING ===")]
        [SerializeField, Tooltip("Attacks per second.")]
        protected float attacksPerSecond = 1f;
        
        [SerializeField, Tooltip("Pause movement during attack (seconds).")]
        protected float attackPauseDuration = 0.5f;

        [Header("=== CONTACT DAMAGE ===")]
        [SerializeField, Tooltip("Deals damage when touching player?")]
        protected bool enableContactDamage = true;
        [SerializeField] protected int contactDamageAmount = 10;
        [SerializeField, Tooltip("How often to deal contact damage (seconds)")]
        protected float contactDamageRate = 1.0f;
        [SerializeField, Tooltip("Distance considered 'touching'")]
        protected float contactDistance = 1.2f;


        [Header("=== ANIMATION ===")]
        [SerializeField] protected Animator animator;
        [SerializeField] protected string speedParameter = "Speed";
        [SerializeField] protected string attackTrigger = "Attack";

        [Header("=== DEBUG ===")]
        [SerializeField] protected bool debugLog = false;

        #endregion

        #region Protected State

        protected NavMeshAgent agent;
        protected EnemyHealth health;
        protected float attackCooldown;
        protected float contactDamageCooldown;
        protected bool isAttacking;

        /// <summary>
        /// Returns the visual center position (modelTransform if set, otherwise this transform).
        /// </summary>
        protected Vector3 VisualPosition => modelTransform != null ? modelTransform.position : transform.position;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            // FORCE proper NavMeshAgent settings
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange * stoppingDistanceMultiplier; // Get close before stopping
            agent.angularSpeed = 360f;
            agent.acceleration = agentAcceleration;
            
            // NavMesh agent setup for small-scale game
            agent.radius = agentRadius;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.autoBraking = true;
        }

        protected virtual void Start()
        {
            // Auto-find player
            if (target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    target = playerObj.transform;
            }

            if (target == null)
                Debug.LogError($"[{GetType().Name}] No player found! Make sure player has 'Player' tag.");
        }

        protected virtual void Update()
        {
            // Skip if dead or frozen
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                StopMovement();
                return;
            }

            // No target? Do nothing
            if (target == null)
            {
                StopMovement();
                return;
            }

            // Calculate distance to player using visual position
            float distanceToPlayer = Vector3.Distance(VisualPosition, target.position);

            // Attack cooldown
            if (attackCooldown > 0)
            {
                attackCooldown -= Time.deltaTime;
                StopMovement();
                LookAtTarget();
                return;
            }

            // CONTACT DAMAGE CHECK
            if (enableContactDamage && target != null)
            {
                if (contactDamageCooldown > 0)
                    contactDamageCooldown -= Time.deltaTime;

                if (distanceToPlayer <= contactDistance && contactDamageCooldown <= 0)
                {
                    IDamageable playerHealth = target.GetComponent<IDamageable>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(contactDamageAmount);
                        contactDamageCooldown = contactDamageRate;
                        if (debugLog) Debug.Log($"[{GetType().Name}] Dealt {contactDamageAmount} contact damage.");
                    }
                }
            }

            // MAIN LOGIC: Simple and clear
            if (distanceToPlayer <= attackRange)
            {
                // Close enough - ATTACK
                StopMovement();
                LookAtTarget();
                TryAttack();
            }
            else
            {
                // Too far - CHASE
                ChasePlayer();
            }

            // Update animation
            UpdateAnimator();
        }

        #endregion

        #region Core Behavior

        protected virtual void ChasePlayer()
        {
            if (agent == null || target == null) return;
            
            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"[{GetType().Name}] Enemy is NOT on NavMesh!");
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(target.position);

            if (debugLog && Time.frameCount % 60 == 0)
            {
                string pathStatus = agent.pathStatus.ToString();
                bool hasPath = agent.hasPath;
                Debug.Log($"[{GetType().Name}] Chasing: distance={Vector3.Distance(transform.position, target.position):F1}, pathStatus={pathStatus}, hasPath={hasPath}");
            }
        }

        protected virtual void StopMovement()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        protected virtual void LookAtTarget()
        {
            if (target == null) return;

            Vector3 lookDir = (target.position - transform.position);
            lookDir.y = 0;
            
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 720f * Time.deltaTime);
            }
        }

        protected virtual void TryAttack()
        {
            if (attackCooldown > 0) return;

            // Set cooldown
            float cooldown = 1f / Mathf.Max(0.01f, attacksPerSecond);
            attackCooldown = cooldown;

            // Trigger animation
            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            {
                animator.SetTrigger(attackTrigger);
            }

            // Call derived attack
            OnAttack();

            if (debugLog)
                Debug.Log($"[{GetType().Name}] Attacking! Next attack in {cooldown:F2}s");
        }

        protected abstract void OnAttack();

        protected virtual void UpdateAnimator()
        {
            if (animator == null) return;
            float speed = agent != null ? agent.velocity.magnitude : 0f;
            animator.SetFloat(speedParameter, speed);
        }

        #endregion

        #region Gizmos

        protected virtual void OnDrawGizmosSelected()
        {
            // Attack range (red)
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Line to target
            if (target != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position + Vector3.up, target.position + Vector3.up);
            }
        }

        #endregion
    }
}
