using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Audio;

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
        
        [SerializeField, Tooltip("How often to force path recalculation when stuck (seconds).")]
        protected float pathRecalculationInterval = 0.5f;
        
        [Header("=== OBSTACLE AVOIDANCE ===")]
        [SerializeField, Tooltip("Enable proactive obstacle detection using NavMesh raycasting")]
        protected bool enableObstacleRaycast = true;
        
        [SerializeField, Tooltip("Height offset for line-of-sight raycast (0.1 = near ground)")]
        protected float raycastHeight = 0.1f;
        
        [SerializeField, Tooltip("How far to the side to search for alternate paths (units)")]
        protected float obstacleAvoidanceDistance = 3f;
        
        [SerializeField, Tooltip("Number of angles to try when finding alternate path")]
        protected int obstacleAvoidanceRays = 8;
        
        [SerializeField, Tooltip("Layers to check for physical obstacles (walls, etc). Default: Everything")]
        protected LayerMask obstacleLayerMask = ~0; // Default to all layers

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

        [Header("=== PROXIMITY AMBIENT SOUND ===")]
        [SerializeField, Tooltip("Optional sound that plays periodically when near the player (growls, hisses, etc.)")]
        protected SoundEvent ambientSound;
        
        [SerializeField, Tooltip("Maximum distance from player for ambient sound to play")]
        protected float ambientSoundDistance = 5f;
        
        [SerializeField, Tooltip("Minimum time between ambient sounds (seconds)")]
        protected float ambientSoundMinInterval = 3f;
        
        [SerializeField, Tooltip("Maximum time between ambient sounds (seconds)")]
        protected float ambientSoundMaxInterval = 8f;

        #endregion

        #region Protected State

        protected NavMeshAgent agent;
        protected EnemyHealth health;
        protected float attackCooldown;
        protected float contactDamageCooldown;
        protected bool isAttacking;
        
        // Path tracking for obstacle avoidance
        protected float lastPathRecalcTime;
        protected Vector3 lastPosition;
        protected float stuckCheckTimer;
        protected const float STUCK_CHECK_INTERVAL = 0.3f;
        protected const float STUCK_THRESHOLD = 0.1f; // Min distance moved in interval
        
        // Alternate waypoint for obstacle avoidance
        protected Vector3 alternateWaypoint;
        protected bool hasAlternateWaypoint;
        protected float alternateWaypointTimeout;
        
        // Ambient sound timing
        protected float ambientSoundTimer;
        
        // Route memory - remembers successful waypoints for re-evaluation
        protected struct RememberedRoute
        {
            public Vector3 waypoint;
            public float lastScore;
            public float timestamp;
            public bool wasSuccessful; // Did we reach the waypoint without getting stuck?
        }
        protected List<RememberedRoute> routeMemory = new List<RememberedRoute>();
        protected const int MAX_REMEMBERED_ROUTES = 5;
        protected const float ROUTE_MEMORY_DURATION = 10f; // Forget routes after 10 seconds

        /// <summary>
        /// Returns the visual center position (modelTransform if set, otherwise this transform).
        /// </summary>
        protected Vector3 VisualPosition => modelTransform != null ? modelTransform.position : transform.position;
        
        // Performance: Cached NavMeshPath objects to avoid per-frame allocation
        private NavMeshPath _cachedPathToWaypoint;
        private NavMeshPath _cachedPathToTarget;
        
        // Performance: Frame throttling for expensive operations
        private int _frameOffset; // Stagger enemies to spread CPU load
        private const int LOS_CHECK_INTERVAL = 5; // Check line of sight every N frames
        private bool _lastLineOfSightBlocked = false; // Cache the result between checks

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
            
            // NavMesh agent setup for obstacle avoidance
            agent.radius = agentRadius;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.autoBraking = true;
            agent.autoRepath = true; // Automatically recalculate path when blocked
            
            // Initialize stuck detection
            lastPosition = transform.position;
            
            // Performance: Pre-allocate NavMesh paths
            _cachedPathToWaypoint = new NavMeshPath();
            _cachedPathToTarget = new NavMeshPath();
            
            // Performance: Stagger frame offset so enemies don't all update on same frame
            _frameOffset = GetInstanceID() % LOS_CHECK_INTERVAL;
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
            
            // Initialize ambient sound timer with random offset so all enemies don't sound at once
            ambientSoundTimer = Random.Range(0f, ambientSoundMaxInterval);
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
            
            // Update ambient sound
            UpdateAmbientSound(distanceToPlayer);
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
            
            // === LINE OF SIGHT CHECK ===
            // Performance: Only check every N frames (expensive raycasts), use cached result otherwise
            bool lineOfSightBlocked = _lastLineOfSightBlocked;
            
            if ((Time.frameCount + _frameOffset) % LOS_CHECK_INTERVAL == 0)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
                Vector3 toTarget = target.position - transform.position;
                float distToTarget = toTarget.magnitude;
                Vector3 rayDir = toTarget.normalized;
                
                RaycastHit physicsHit;
                lineOfSightBlocked = Physics.Raycast(rayOrigin, rayDir, out physicsHit, 
                    distToTarget, obstacleLayerMask, QueryTriggerInteraction.Ignore);
                
                // Check if what we hit is actually an obstacle (not the player)
                if (lineOfSightBlocked)
                {
                    if (physicsHit.collider.gameObject == target.gameObject || 
                        physicsHit.collider.CompareTag("Player"))
                    {
                        lineOfSightBlocked = false;
                    }
                }
                
                _lastLineOfSightBlocked = lineOfSightBlocked;
            }
            
            // === DECISION LOGIC ===
            
            if (!lineOfSightBlocked)
            {
                // CLEAR LINE OF SIGHT - go directly to player!
                // Clear any alternate waypoint since we can see the player now
                if (hasAlternateWaypoint)
                {
                    hasAlternateWaypoint = false;
                    if (debugLog)
                        Debug.Log($"[{GetType().Name}] Line of sight restored, going direct to player");
                }
                
                agent.SetDestination(target.position);
            }
            else
            {
                // LINE OF SIGHT BLOCKED - need to navigate around obstacle
                
                // If we already have an alternate waypoint, check if we should keep using it
                if (hasAlternateWaypoint)
                {
                    alternateWaypointTimeout -= Time.deltaTime;
                    float distToWaypoint = Vector3.Distance(transform.position, alternateWaypoint);
                    
                    // Reached waypoint or timed out?
                    if (distToWaypoint < 1.5f || alternateWaypointTimeout <= 0)
                    {
                        // Mark as successful if we actually reached it (not timed out)
                        if (distToWaypoint < 1.5f)
                        {
                            MarkRouteSuccessful();
                        }
                        
                        hasAlternateWaypoint = false;
                        if (debugLog)
                            Debug.Log($"[{GetType().Name}] Reached alternate waypoint, recalculating...");
                    }
                    else
                    {
                        // Keep going to alternate waypoint
                        agent.SetDestination(alternateWaypoint);
                        return;
                    }
                }
                
                // Stuck detection
                stuckCheckTimer += Time.deltaTime;
                if (stuckCheckTimer >= STUCK_CHECK_INTERVAL)
                {
                    float distMoved = Vector3.Distance(transform.position, lastPosition);
                    bool isStuck = distMoved < STUCK_THRESHOLD && agent.velocity.sqrMagnitude < 0.1f;
                    
                    if (isStuck)
                    {
                        // Force new waypoint search
                        hasAlternateWaypoint = false;
                        if (debugLog)
                            Debug.Log($"[{GetType().Name}] Stuck! Finding new path...");
                    }
                    
                    lastPosition = transform.position;
                    stuckCheckTimer = 0f;
                }
                
                // Find alternate waypoint if we don't have one
                if (!hasAlternateWaypoint && Time.time - lastPathRecalcTime > pathRecalculationInterval)
                {
                    Vector3 alternatePos;
                    if (FindAlternateWaypoint(out alternatePos))
                    {
                        alternateWaypoint = alternatePos;
                        hasAlternateWaypoint = true;
                        alternateWaypointTimeout = 3f; // Try for max 3 seconds
                        agent.ResetPath();
                        agent.SetDestination(alternateWaypoint);
                        lastPathRecalcTime = Time.time;
                        
                        if (debugLog)
                            Debug.Log($"[{GetType().Name}] Found alternate waypoint");
                        return;
                    }
                }
                
                // Fallback: try direct path anyway (NavMesh might find a way)
                agent.SetDestination(target.position);
            }
        }
        
        /// <summary>
        /// Finds an alternate waypoint to navigate around an obstacle.
        /// Uses actual NavMesh path length for accurate scoring.
        /// Remembers and re-evaluates successful routes.
        /// </summary>
        protected virtual bool FindAlternateWaypoint(out Vector3 waypoint)
        {
            waypoint = Vector3.zero;
            
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0;
            float directDistToTarget = toTarget.magnitude;
            
            if (directDistToTarget < 0.5f)
                return false; // Too close, no need for alternate
            
            Vector3 dirToTarget = toTarget.normalized;
            
            // Clean up old route memories
            CleanRouteMemory();
            
            float bestScore = float.MaxValue; // Lower is better (shortest path)
            Vector3 bestWaypoint = Vector3.zero;
            bool foundAny = false;
            
            // === PHASE 1: Evaluate new waypoint candidates ===
            float[] searchAngles = { 90f, -90f, 70f, -70f, 110f, -110f, 50f, -50f, 130f, -130f, 45f, -45f };
            
            for (int i = 0; i < Mathf.Min(searchAngles.Length, obstacleAvoidanceRays); i++)
            {
                float angle = searchAngles[i];
                
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 searchDir = rotation * dirToTarget;
                Vector3 candidatePos = transform.position + searchDir * obstacleAvoidanceDistance;
                
                float score;
                if (EvaluateWaypoint(candidatePos, out score))
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestWaypoint = candidatePos;
                        foundAny = true;
                        
                        if (debugLog)
                            Debug.Log($"[{GetType().Name}] New waypoint at angle {angle}, total path: {score:F1} units");
                    }
                }
            }
            
            // === PHASE 2: Re-evaluate remembered successful routes ===
            for (int i = 0; i < routeMemory.Count; i++)
            {
                RememberedRoute route = routeMemory[i];
                
                // Only re-evaluate routes that were successful before
                if (!route.wasSuccessful) continue;
                
                float score;
                if (EvaluateWaypoint(route.waypoint, out score))
                {
                    // Successful routes get a small bonus (slightly lower score = preferred)
                    score *= 0.9f;
                    
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestWaypoint = route.waypoint;
                        foundAny = true;
                        
                        if (debugLog)
                            Debug.Log($"[{GetType().Name}] Remembered route still good, total path: {score:F1} units");
                    }
                }
            }
            
            if (foundAny)
            {
                waypoint = bestWaypoint;
                
                // Remember this route
                RememberRoute(bestWaypoint, bestScore);
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Evaluates a waypoint and returns a score based on total path length to reach player.
        /// Lower score = better (shorter total path).
        /// Uses cached NavMeshPath objects to avoid per-call allocation.
        /// </summary>
        protected virtual bool EvaluateWaypoint(Vector3 candidatePos, out float score)
        {
            score = float.MaxValue;
            
            // Sample valid NavMesh position
            NavMeshHit sampleHit;
            if (!NavMesh.SamplePosition(candidatePos, out sampleHit, obstacleAvoidanceDistance * 0.5f, NavMesh.AllAreas))
                return false;
            
            Vector3 waypointPos = sampleHit.position;
            
            // CHECK 1: Is there a physical obstacle between us and the waypoint?
            Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
            Vector3 toWaypoint = waypointPos - transform.position;
            float distToWaypoint = toWaypoint.magnitude;
            
            RaycastHit physicsHit;
            bool pathBlocked = Physics.Raycast(rayOrigin, toWaypoint.normalized, out physicsHit, 
                distToWaypoint, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            
            if (pathBlocked)
                return false;
            
            // CHECK 2: Can NavMesh path to waypoint? (use cached path object)
            _cachedPathToWaypoint.ClearCorners();
            if (!agent.CalculatePath(waypointPos, _cachedPathToWaypoint) || 
                _cachedPathToWaypoint.status != NavMeshPathStatus.PathComplete)
                return false;
            
            // CHECK 3: Can NavMesh path from waypoint to target? (use cached path object)
            _cachedPathToTarget.ClearCorners();
            if (!NavMesh.CalculatePath(waypointPos, target.position, NavMesh.AllAreas, _cachedPathToTarget) ||
                _cachedPathToTarget.status != NavMeshPathStatus.PathComplete)
                return false;
            
            // Calculate actual path lengths
            float pathLengthToWaypoint = CalculatePathLength(_cachedPathToWaypoint);
            float pathLengthToTarget = CalculatePathLength(_cachedPathToTarget);
            
            // Total travel distance = path to waypoint + path from waypoint to target
            float totalPathLength = pathLengthToWaypoint + pathLengthToTarget;
            
            // Penalty for paths that are much longer than direct distance
            float directDist = Vector3.Distance(transform.position, target.position);
            float efficiencyPenalty = (totalPathLength / directDist) - 1f; // 0 = perfect, higher = worse
            
            // Final score: total path length + efficiency penalty
            score = totalPathLength + (efficiencyPenalty * 2f);
            
            return true;
        }
        
        /// <summary>
        /// Calculates the actual length of a NavMesh path by summing corner distances.
        /// </summary>
        protected float CalculatePathLength(NavMeshPath path)
        {
            if (path.corners.Length < 2)
                return 0f;
            
            float length = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            }
            return length;
        }
        
        /// <summary>
        /// Remembers a route for future re-evaluation.
        /// </summary>
        protected void RememberRoute(Vector3 waypointPos, float score)
        {
            // Check if we already have this waypoint
            for (int i = 0; i < routeMemory.Count; i++)
            {
                if (Vector3.Distance(routeMemory[i].waypoint, waypointPos) < 1f)
                {
                    // Update existing entry
                    RememberedRoute updated = routeMemory[i];
                    updated.lastScore = score;
                    updated.timestamp = Time.time;
                    routeMemory[i] = updated;
                    return;
                }
            }
            
            // Add new entry
            if (routeMemory.Count >= MAX_REMEMBERED_ROUTES)
            {
                // Remove oldest
                routeMemory.RemoveAt(0);
            }
            
            routeMemory.Add(new RememberedRoute
            {
                waypoint = waypointPos,
                lastScore = score,
                timestamp = Time.time,
                wasSuccessful = false // Will be marked true when we reach it
            });
        }
        
        /// <summary>
        /// Marks the current waypoint as successfully reached.
        /// </summary>
        protected void MarkRouteSuccessful()
        {
            for (int i = 0; i < routeMemory.Count; i++)
            {
                if (Vector3.Distance(routeMemory[i].waypoint, alternateWaypoint) < 1.5f)
                {
                    RememberedRoute updated = routeMemory[i];
                    updated.wasSuccessful = true;
                    routeMemory[i] = updated;
                    
                    if (debugLog)
                        Debug.Log($"[{GetType().Name}] Marked route as successful");
                    return;
                }
            }
        }
        
        /// <summary>
        /// Removes old routes from memory.
        /// </summary>
        protected void CleanRouteMemory()
        {
            for (int i = routeMemory.Count - 1; i >= 0; i--)
            {
                if (Time.time - routeMemory[i].timestamp > ROUTE_MEMORY_DURATION)
                {
                    routeMemory.RemoveAt(i);
                }
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

        /// <summary>
        /// Updates proximity-based ambient sound (growls, hisses, etc.).
        /// Plays periodically when within ambientSoundDistance of the player.
        /// </summary>
        protected virtual void UpdateAmbientSound(float distanceToPlayer)
        {
            // Skip if no ambient sound assigned
            if (ambientSound == null || AudioManager.Instance == null) return;
            
            // Decrement timer
            ambientSoundTimer -= Time.deltaTime;
            
            // Check if it's time to potentially play a sound
            if (ambientSoundTimer <= 0f)
            {
                // Reset timer to random interval
                ambientSoundTimer = Random.Range(ambientSoundMinInterval, ambientSoundMaxInterval);
                
                // Only play if within range of player
                if (distanceToPlayer <= ambientSoundDistance)
                {
                    AudioManager.Instance.PlayAtPosition(ambientSound, VisualPosition);
                    
                    if (debugLog)
                        Debug.Log($"[{GetType().Name}] Playing ambient sound (dist: {distanceToPlayer:F1})");
                }
            }
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
            
            // Show alternate waypoint if active
            if (hasAlternateWaypoint)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(alternateWaypoint, 0.5f);
                Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, alternateWaypoint + Vector3.up * 0.5f);
            }
        }

        #endregion
    }
}
