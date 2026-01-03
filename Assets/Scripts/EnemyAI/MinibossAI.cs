using UnityEngine;
using System.Collections;

namespace EnemyAI
{
    /// <summary>
    /// Enhanced Miniboss AI with:
    /// - Chaser AI pathing (from EnemyAIBase with obstacle avoidance)
    /// - Sniper-style fireball attack with aim line
    /// - Meteor strike attack with warning indicator
    /// - 70/30 shoot vs reposition decision logic
    /// </summary>
    public class MinibossAI : EnemyAIBase
    {
        #region Enums
        
        private enum MinibossState
        {
            Chasing,        // Moving toward player (uses base ChasePlayer with obstacle avoidance)
            Repositioning,  // Moving away/sideways for better position
            Aiming,         // Showing aim line, preparing to fire
            Shooting,       // Firing fireball
            CastingMeteor   // Spawning meteor strike
        }
        
        #endregion
        
        #region Serialized Fields
        
        [Header("=== COMBAT BEHAVIOR ===")]
        [SerializeField, Tooltip("Preferred combat distance from player")]
        private float preferredRange = 10f;
        
        [SerializeField, Tooltip("If closer than this, may reposition (not always)")]
        private float minRange = 2f;
        
        [SerializeField, Tooltip("Chance to reposition when too close (0.15 = 15%)")]
        [Range(0f, 1f)]
        private float repositionChance = 0.15f;
        
        [SerializeField, Tooltip("Chance to move to a new position while fighting")]
        [Range(0f, 1f)]
        private float roamChance = 0.1f;
        
        [Header("=== FIREBALL ATTACK ===")]
        [SerializeField] private GameObject fireballPrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float fireballSpeed = 15f;
        [SerializeField] private int fireballDamage = 30;
        [SerializeField] private float fireballCooldown = 2f;
        
        [Header("=== METEOR ATTACK ===")]
        [SerializeField] private GameObject meteorStrikePrefab;
        [SerializeField] private float meteorCooldown = 8f;
        
        [Header("=== AIM LINE INDICATOR ===")]
        [SerializeField] private bool showAimLine = true;
        [SerializeField] private Color aimLineStartColor = Color.white;
        [SerializeField] private Color aimLineEndColor = new Color(1f, 0.3f, 0f, 1f); // Orange-red
        [SerializeField] private float aimLineWidth = 0.08f;
        [SerializeField] private float aimLineLength = 25f;
        [SerializeField] private float aimDuration = 1f;
        
        [Header("=== ANIMATION TRIGGERS ===")]
        [SerializeField] private string fireballTrigger = "Shoot_FireBall";
        [SerializeField] private string meteorTrigger = "Summon_Meteor";
        [SerializeField] private string walkBool = "Walk";
        
        [Header("=== REPOSITIONING ===")]
        [SerializeField] private float repositionDistance = 4f;
        [SerializeField] private float repositionTimeout = 2f;
        
        #endregion
        
        #region Private State
        
        private MinibossState currentState = MinibossState.Chasing;
        private float fireballCooldownTimer;
        private float meteorCooldownTimer;
        private float stateTimer;
        private float actionPauseTimer;
        
        // Aim line
        private LineRenderer aimLine;
        private Coroutine aimCoroutine;
        
        // Repositioning
        private Vector3 repositionTarget;
        
        #endregion
        
        #region Unity Lifecycle
        
        protected override void Awake()
        {
            base.Awake();
            
            // Miniboss controls rotation manually during aiming
            if (agent != null)
            {
                agent.updateRotation = true; // Let NavMesh handle rotation normally
            }
        }
        
        protected override void Start()
        {
            base.Start();
            
            if (projectileSpawnPoint == null)
                projectileSpawnPoint = transform;
            
            SetupAimLine();
            
            // Start with some cooldown so attacks don't fire immediately
            fireballCooldownTimer = fireballCooldown * 0.5f;
            meteorCooldownTimer = meteorCooldown * 0.5f;
        }
        
        protected override void Update()
        {
            // Handle death/frozen
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                StopMovement();
                HideAimLine();
                return;
            }
            
            if (target == null)
            {
                StopMovement();
                return;
            }
            
            // Update cooldowns
            if (fireballCooldownTimer > 0) fireballCooldownTimer -= Time.deltaTime;
            if (meteorCooldownTimer > 0) meteorCooldownTimer -= Time.deltaTime;
            if (actionPauseTimer > 0) actionPauseTimer -= Time.deltaTime;
            
            // Handle action pause (after attacks)
            if (actionPauseTimer > 0)
            {
                StopMovement();
                LookAtTarget();
                UpdateAnimator();
                return;
            }
            
            // State machine
            switch (currentState)
            {
                case MinibossState.Chasing:
                    UpdateChasing();
                    break;
                case MinibossState.Repositioning:
                    UpdateRepositioning();
                    break;
                case MinibossState.Aiming:
                case MinibossState.Shooting:
                case MinibossState.CastingMeteor:
                    // These are handled by coroutines
                    break;
            }
            
            UpdateAnimator();
            HandleContactDamage();
        }
        
        #endregion
        
        #region State Updates
        
        private void UpdateChasing()
        {
            float distance = Vector3.Distance(VisualPosition, target.position);
            
            // Check if in combat range
            if (distance <= preferredRange + 2f)
            {
                // In range - make combat decision
                MakeCombatDecision(distance);
            }
            else
            {
                // Too far - chase with obstacle avoidance
                ChasePlayer();
            }
        }
        
        private void UpdateRepositioning()
        {
            stateTimer -= Time.deltaTime;
            
            // Check if reached destination or timed out
            bool reached = !agent.pathPending && agent.remainingDistance <= 1f;
            bool timedOut = stateTimer <= 0;
            
            // Stuck detection - if we're supposed to be moving but aren't
            float distMoved = Vector3.Distance(transform.position, lastPosition);
            stuckCheckTimer += Time.deltaTime;
            if (stuckCheckTimer >= STUCK_CHECK_INTERVAL)
            {
                if (distMoved < STUCK_THRESHOLD && agent.velocity.sqrMagnitude < 0.1f)
                {
                    if (debugLog)
                        Debug.Log("[MinibossAI] Stuck during reposition! Cancelling.");
                    timedOut = true; // Force exit
                }
                lastPosition = transform.position;
                stuckCheckTimer = 0f;
            }
            
            if (reached || timedOut)
            {
                // Done repositioning - go back to chasing (will make new decision)
                SwitchState(MinibossState.Chasing);
            }
        }
        
        #endregion
        
        #region Combat Decision
        
        private void MakeCombatDecision(float distance)
        {
            bool hasLOS = HasLineOfSightToPlayer();
            
            // === BOSS COMBAT AI - AGGRESSIVE, HOLDS POSITION ===
            
            // Check if too close - OCCASIONALLY reposition (not always!)
            if (distance < minRange)
            {
                // Only reposition sometimes - boss is willing to fight up close
                if (Random.value < repositionChance)
                {
                    if (debugLog) Debug.Log($"[MinibossAI] Too close ({distance:F1}), backing up!");
                    StartRepositioning();
                    return;
                }
                // Otherwise, keep fighting!
            }
            
            // PRIORITY 1: Attack based on line of sight
            if (hasLOS)
            {
                // CAN SEE PLAYER → Use Fireball (direct attack)
                if (fireballCooldownTimer <= 0 && fireballPrefab != null)
                {
                    if (debugLog) Debug.Log("[MinibossAI] Has LOS - Fireball attack!");
                    StartFireballAttack();
                    return;
                }
                
                // Fireball on cooldown - maybe use meteor anyway if it's ready
                if (meteorCooldownTimer <= 0 && meteorStrikePrefab != null)
                {
                    if (debugLog) Debug.Log("[MinibossAI] Fireball on CD, using Meteor!");
                    StartMeteorAttack();
                    return;
                }
            }
            else
            {
                // BLOCKED BY WALL → Use Meteor (indirect attack)
                if (meteorCooldownTimer <= 0 && meteorStrikePrefab != null)
                {
                    if (debugLog) Debug.Log("[MinibossAI] No LOS - Meteor attack!");
                    StartMeteorAttack();
                    return;
                }
                
                // No LOS and meteor on cooldown - need to reposition to get LOS
                if (Random.value < 0.4f) // 40% chance to try repositioning for LOS
                {
                    if (debugLog) Debug.Log("[MinibossAI] No LOS, seeking new position");
                    StartRepositioning();
                    return;
                }
            }
            
            // PRIORITY 2: Both attacks on cooldown - hold position or roam
            if (Random.value < roamChance)
            {
                // Occasionally roam to utilize the arena
                if (debugLog) Debug.Log("[MinibossAI] Roaming to new position");
                StartRepositioning();
            }
            else
            {
                // DEFAULT: Stand and face the player, wait for cooldowns
                StopMovement();
                LookAtTarget();
            }
        }
        
        private bool HasLineOfSightToPlayer()
        {
            Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
            Vector3 toTarget = target.position - transform.position;
            float dist = toTarget.magnitude;
            
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, toTarget.normalized, out hit, dist, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                // Hit something - check if it's the player
                return hit.collider.gameObject == target.gameObject || hit.collider.CompareTag("Player");
            }
            
            // Nothing in the way
            return true;
        }
        
        #endregion
        
        #region State Transitions
        
        private void SwitchState(MinibossState newState)
        {
            if (currentState == newState) return;
            
            currentState = newState;
            stateTimer = 0f;
            
            if (debugLog)
                Debug.Log($"[MinibossAI] State: {newState}");
            
            switch (newState)
            {
                case MinibossState.Chasing:
                    if (agent.isOnNavMesh) agent.isStopped = false;
                    break;
                    
                case MinibossState.Repositioning:
                    PickRepositionTarget();
                    stateTimer = repositionTimeout;
                    break;
                    
                case MinibossState.Aiming:
                case MinibossState.Shooting:
                case MinibossState.CastingMeteor:
                    StopMovement();
                    break;
            }
        }
        
        #endregion
        
        #region Fireball Attack
        
        private void StartFireballAttack()
        {
            SwitchState(MinibossState.Aiming);
            StartCoroutine(FireballAttackRoutine());
        }
        
        private IEnumerator FireballAttackRoutine()
        {
            // Stop and face player
            StopMovement();
            LookAtTarget();
            
            // Show aim line
            ShowAimLine();
            
            // Aiming phase - wait for aim duration
            float elapsed = 0f;
            while (elapsed < aimDuration)
            {
                // Check for interruption
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    HideAimLine();
                    SwitchState(MinibossState.Chasing);
                    yield break;
                }
                
                LookAtTarget();
                UpdateAimLine(elapsed / aimDuration);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Fire!
            HideAimLine();
            SwitchState(MinibossState.Shooting);
            
            // Trigger animation
            if (animator != null)
                animator.SetTrigger(fireballTrigger);
            
            // Spawn projectile
            SpawnFireball();
            
            // Reset cooldown
            fireballCooldownTimer = fireballCooldown;
            
            // Brief pause after attack
            actionPauseTimer = 0.5f;
            
            // Return to chasing
            SwitchState(MinibossState.Chasing);
        }
        
        private void SpawnFireball()
        {
            if (fireballPrefab == null || target == null) return;
            
            Vector3 spawnPos = projectileSpawnPoint.position;
            Vector3 targetPos = target.position + Vector3.up * 1f;
            Vector3 direction = (targetPos - spawnPos).normalized;
            
            GameObject proj = Instantiate(fireballPrefab, spawnPos, Quaternion.LookRotation(direction));
            
            EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
            if (ep != null)
            {
                ep.SetDirection(direction);
                ep.SetSpeed(fireballSpeed);
                ep.SetDamage(fireballDamage);
            }
            
            if (debugLog)
                Debug.Log($"[MinibossAI] Fired fireball!");
        }
        
        #endregion
        
        #region Meteor Attack
        
        private void StartMeteorAttack()
        {
            SwitchState(MinibossState.CastingMeteor);
            StartCoroutine(MeteorAttackRoutine());
        }
        
        private IEnumerator MeteorAttackRoutine()
        {
            // Stop and face player
            StopMovement();
            LookAtTarget();
            
            // Trigger animation
            if (animator != null)
                animator.SetTrigger(meteorTrigger);
            
            // Brief cast time
            yield return new WaitForSeconds(0.3f);
            
            // Spawn meteor strike at player's current position
            SpawnMeteor();
            
            // Reset cooldown
            meteorCooldownTimer = meteorCooldown;
            
            // Pause after casting
            actionPauseTimer = 1f;
            
            // Return to chasing
            SwitchState(MinibossState.Chasing);
        }
        
        private void SpawnMeteor()
        {
            if (meteorStrikePrefab == null || target == null) return;
            
            // Spawn at player's current position (meteor has its own warning)
            Vector3 spawnPos = target.position;
            spawnPos.y = 0f;
            
            Instantiate(meteorStrikePrefab, spawnPos, Quaternion.identity);
            
            if (debugLog)
                Debug.Log($"[MinibossAI] Summoned meteor at {spawnPos}!");
        }
        
        #endregion
        
        #region Repositioning
        
        private void StartRepositioning()
        {
            SwitchState(MinibossState.Repositioning);
        }
        
        private void PickRepositionTarget()
        {
            if (!agent.isOnNavMesh) return;
            
            // Get direction away from player
            Vector3 awayFromPlayer = (transform.position - target.position).normalized;
            awayFromPlayer.y = 0;
            
            // Try multiple directions: backward variants, then sideways
            Vector3[] directions = new Vector3[]
            {
                awayFromPlayer,                                          // Straight back
                Quaternion.AngleAxis(30f, Vector3.up) * awayFromPlayer,  // Back-right
                Quaternion.AngleAxis(-30f, Vector3.up) * awayFromPlayer, // Back-left
                Quaternion.AngleAxis(60f, Vector3.up) * awayFromPlayer,  // Side-right
                Quaternion.AngleAxis(-60f, Vector3.up) * awayFromPlayer, // Side-left
                Quaternion.AngleAxis(90f, Vector3.up) * awayFromPlayer,  // Pure right
                Quaternion.AngleAxis(-90f, Vector3.up) * awayFromPlayer  // Pure left
            };
            
            Vector3 bestPosition = Vector3.zero;
            float bestScore = float.MinValue;
            bool foundAny = false;
            
            foreach (Vector3 dir in directions)
            {
                Vector3 candidatePos = transform.position + dir * repositionDistance;
                
                // CHECK 1: Is the path physically clear? (no walls)
                Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
                RaycastHit wallHit;
                bool wallBlocked = Physics.Raycast(rayOrigin, dir, out wallHit, 
                    repositionDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore);
                
                if (wallBlocked)
                {
                    if (debugLog)
                        Debug.Log($"[MinibossAI] Reposition direction {dir} blocked by {wallHit.collider.name}");
                    continue;
                }
                
                // CHECK 2: Is there valid NavMesh at the target?
                UnityEngine.AI.NavMeshHit navHit;
                if (!UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out navHit, repositionDistance * 0.5f, UnityEngine.AI.NavMesh.AllAreas))
                    continue;
                
                // CHECK 3: Can we path there?
                UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                if (!agent.CalculatePath(navHit.position, path) || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    continue;
                
                // CHECK 4: Score this position - prefer positions that give LOS to player
                float score = 0f;
                
                // Would we have LOS from this new position?
                Vector3 toPlayerFromNew = target.position - navHit.position;
                RaycastHit losHit;
                bool hasLOSFromNew = !Physics.Raycast(navHit.position + Vector3.up * raycastHeight, 
                    toPlayerFromNew.normalized, out losHit, toPlayerFromNew.magnitude, 
                    obstacleLayerMask, QueryTriggerInteraction.Ignore);
                
                if (hasLOSFromNew)
                    score += 10f; // Strong bonus for positions with LOS
                
                // Prefer positions further from player (safer)
                float distFromPlayer = Vector3.Distance(navHit.position, target.position);
                score += distFromPlayer * 0.5f;
                
                // Prefer positions in "backward" direction
                float backwardDot = Vector3.Dot(dir, awayFromPlayer);
                score += backwardDot * 3f;
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPosition = navHit.position;
                    foundAny = true;
                    
                    if (debugLog)
                        Debug.Log($"[MinibossAI] Good reposition at {navHit.position}, score={score:F1}, hasLOS={hasLOSFromNew}");
                }
            }
            
            if (foundAny)
            {
                agent.SetDestination(bestPosition);
                repositionTarget = bestPosition;
                if (debugLog)
                    Debug.Log($"[MinibossAI] Repositioning to {bestPosition}");
            }
            else
            {
                // No valid position found - stay put
                if (debugLog)
                    Debug.Log("[MinibossAI] No valid reposition target found!");
                SwitchState(MinibossState.Chasing);
            }
        }
        
        #endregion
        
        #region Aim Line
        
        private void SetupAimLine()
        {
            if (!showAimLine) return;
            
            GameObject lineObj = new GameObject("AimLine");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;
            
            aimLine = lineObj.AddComponent<LineRenderer>();
            aimLine.positionCount = 2;
            aimLine.startWidth = aimLineWidth;
            aimLine.endWidth = Mathf.Max(aimLineWidth * 0.3f, 0.02f);
            aimLine.useWorldSpace = true;
            
            aimLine.numCornerVertices = 4;
            aimLine.numCapVertices = 4;
            aimLine.textureMode = LineTextureMode.Stretch;
            
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.startColor = aimLineStartColor;
            aimLine.endColor = new Color(aimLineStartColor.r, aimLineStartColor.g, aimLineStartColor.b, 0f);
            
            aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aimLine.receiveShadows = false;
            
            aimLine.enabled = false;
        }
        
        private void ShowAimLine()
        {
            if (!showAimLine || aimLine == null) return;
            aimLine.enabled = true;
        }
        
        private void HideAimLine()
        {
            if (aimLine == null) return;
            aimLine.enabled = false;
        }
        
        private void UpdateAimLine(float t)
        {
            if (aimLine == null || !aimLine.enabled) return;
            
            // Lerp color
            Color currentColor = Color.Lerp(aimLineStartColor, aimLineEndColor, t);
            aimLine.startColor = currentColor;
            aimLine.endColor = new Color(currentColor.r, currentColor.g, currentColor.b, 0f);
            
            // Update positions
            Vector3 startPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            Vector3 direction = (target.position - startPos).normalized;
            direction.y = 0; // Keep flat
            
            Vector3 endPos = startPos + direction * aimLineLength;
            
            aimLine.SetPosition(0, startPos);
            aimLine.SetPosition(1, endPos);
        }
        
        #endregion
        
        #region Animation
        
        protected override void UpdateAnimator()
        {
            if (animator == null) return;
            
            // Walking animation based on movement
            bool isWalking = agent != null && agent.velocity.magnitude > 0.1f;
            animator.SetBool(walkBool, isWalking);
            
            // Speed parameter from base class
            if (!string.IsNullOrEmpty(speedParameter))
            {
                float speed = agent != null ? agent.velocity.magnitude : 0f;
                animator.SetFloat(speedParameter, speed);
            }
        }
        
        #endregion
        
        #region Contact Damage
        
        private void HandleContactDamage()
        {
            if (!enableContactDamage || target == null) return;
            
            if (contactDamageCooldown > 0)
            {
                contactDamageCooldown -= Time.deltaTime;
                return;
            }
            
            float distance = Vector3.Distance(VisualPosition, target.position);
            if (distance <= contactDistance)
            {
                IDamageable playerHealth = target.GetComponent<IDamageable>();
                if (playerHealth == null)
                    playerHealth = target.GetComponentInParent<IDamageable>();
                
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(contactDamageAmount);
                    contactDamageCooldown = contactDamageRate;
                    
                    if (debugLog)
                        Debug.Log($"[MinibossAI] Dealt {contactDamageAmount} contact damage!");
                }
            }
        }
        
        #endregion
        
        #region Gizmos
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // Preferred range (green)
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, preferredRange);
            
            // Min range (yellow)
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, minRange);
            
            // Reposition target (if repositioning)
            if (currentState == MinibossState.Repositioning)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(repositionTarget, 0.5f);
                Gizmos.DrawLine(transform.position, repositionTarget);
            }
        }
        
        #endregion
        
        // Base class attack not used - we have custom attack system
        protected override void OnAttack() { }
    }
}
