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
            CastingMeteor,  // Spawning meteor strike
            RageMode        // Charging and firing 360° burst
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
        [SerializeField, Tooltip("Fixed Y height for fireball spawn (0 = use spawn point height)")]
        private float fireballSpawnHeight = 1.5f;
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
        
        [Header("=== ANIMATION BOOLEANS ===")]
        [SerializeField] private string fireballBool = "Shoot_FireBall";
        [SerializeField] private string meteorBool = "Summon_Meteor";
        [SerializeField] private string walkBool = "Walk";
        [SerializeField] private float attackAnimationDuration = 0.5f;
        
        [Header("=== REPOSITIONING ===")]
        [SerializeField] private float repositionDistance = 4f;
        [SerializeField] private float repositionTimeout = 2f;
        
        [Header("=== RAGE MODE ===")]
        [SerializeField, Tooltip("Enable rage mode at HP thresholds")]
        private bool enableRageMode = true;
        [SerializeField, Tooltip("First rage trigger (0.8 = 80% HP)")]
        [Range(0f, 1f)]
        private float rageThreshold1 = 0.8f;
        [SerializeField, Tooltip("Second rage trigger (0.4 = 40% HP)")]
        [Range(0f, 1f)]
        private float rageThreshold2 = 0.4f;
        [SerializeField, Tooltip("Number of fireballs in 360° burst")]
        private int rageFireballCount = 12;
        [SerializeField, Tooltip("Speed of rage mode fireballs (slower = easier to dodge)")]
        private float rageFireballSpeed = 5f;
        [SerializeField, Tooltip("Number of wall bounces for rage fireballs")]
        private int rageFireballBounces = 1;
        [SerializeField, Tooltip("Duration of charge animation (invulnerable during this)")]
        private float rageChargeDuration = 2f;
        [SerializeField, Tooltip("Optional VFX for invulnerability shield")]
        private GameObject rageShieldVFX;
        [SerializeField, Tooltip("Prefab for bouncing fireballs (uses regular if not set)")]
        private GameObject bouncingFireballPrefab;
        [SerializeField, Tooltip("Animation bool for rage mode")]
        private string rageModeBool = "RageMode_Charge";
        
        [Header("=== BEHAVIOR TUNING ===")]
        [SerializeField, Tooltip("Force reposition after this many consecutive attacks")]
        private int attacksBeforeReposition = 3;
        [SerializeField, Tooltip("Chance to reposition after each attack (0.3 = 30%)")]
        [Range(0f, 1f)]
        private float repositionChanceAfterAttack = 0.3f;
        [SerializeField, Tooltip("Circle-strafe distance from player")]
        private float circleStrafeRadius = 8f;
        [SerializeField, Tooltip("Circle-strafe arc angle")]
        private float circleStrafeAngle = 45f;
        
        #endregion
        
        #region Private State
        
        private MinibossState currentState = MinibossState.Chasing;
        private float fireballCooldownTimer;
        private float meteorCooldownTimer;
        private float stateTimer;
        private float actionPauseTimer;
        
        // Animation tracking - these track the actual animator bool states
        private bool isFireballActive = false;
        private bool isMeteorActive = false;
        
        // Animation event flags - set by animation events to signal when to spawn/end
        private bool pendingAimStart = false;
        private bool pendingFireballSpawn = false;
        private bool pendingMeteorSpawn = false;
        private bool attackAnimationEnded = false;
        
        // Aim line
        private LineRenderer aimLine;
        private Coroutine aimCoroutine;
        
        // Repositioning
        private Vector3 repositionTarget;
        
        // Rage mode tracking
        private bool ragePhase1Triggered = false;
        private bool ragePhase2Triggered = false;
        private int consecutiveAttackCount = 0;
        private bool isRageModeActive = false;
        private bool pendingRageFire = false;
        private bool rageAnimationEnded = false;
        private GameObject activeShieldVFX;
        
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
                case MinibossState.RageMode:
                    // Attack states - ensure movement is stopped (handled by coroutines)
                    StopMovement();
                    LookAtTarget();
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
            
            // Let NavMesh handle rotation during movement (face movement direction)
            if (agent != null && !agent.updateRotation)
            {
                agent.updateRotation = true;
            }
            
            // Check for rage mode trigger (HP thresholds)
            if (CheckRageModeThreshold())
            {
                StartRageMode();
                return;
            }
            
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
            
            // Let NavMesh handle rotation during movement (face movement direction)
            if (agent != null && !agent.updateRotation)
            {
                agent.updateRotation = true;
            }
            
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
            
            // === BOSS COMBAT AI - SHOOT FIRST, THINK LATER ===
            
            // PRIORITY 0: Force reposition after consecutive attacks
            if (consecutiveAttackCount >= attacksBeforeReposition)
            {
                if (debugLog) Debug.Log($"[MinibossAI] Forced reposition after {consecutiveAttackCount} attacks");
                consecutiveAttackCount = 0;
                StartCircleStrafe(); // Use circle strafe for variety
                return;
            }
            
            // PRIORITY 1: Attack based on line of sight
            if (hasLOS)
            {
                // CAN SEE PLAYER → Use Fireball (direct attack)
                if (fireballCooldownTimer <= 0 && fireballPrefab != null)
                {
                    if (debugLog) Debug.Log("[MinibossAI] ==> DECISION: Has LOS, cooldown ready - Fireball!");
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
            
            // Check if too close - occasionally reposition
            if (distance < minRange && Random.value < repositionChance)
            {
                if (debugLog) Debug.Log($"[MinibossAI] Too close ({distance:F1}), backing up!");
                StartRepositioning();
                return;
            }
            
            // PRIORITY 2: Both attacks on cooldown - circle-strafe to stay active
            // Don't just stand still - keep moving to feel more dynamic
            StartCircleStrafe();
        }
        
        /// <summary>
        /// Called after each attack completes to handle attack rhythm.
        /// 70% continue attacking, 30% reposition.
        /// </summary>
        private void OnAttackComplete()
        {
            consecutiveAttackCount++;
            
            // 30% chance to reposition after attack (unless forced by counter)
            if (consecutiveAttackCount < attacksBeforeReposition && 
                Random.value < repositionChanceAfterAttack)
            {
                if (debugLog) Debug.Log("[MinibossAI] Post-attack reposition (30% chance)");
                StartRepositioning();
            }
            // Otherwise, continue to next attack (70% chance - handled by returning to Chasing)
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
                    if (agent != null) agent.updateRotation = true; // Face movement direction
                    break;
                    
                case MinibossState.Repositioning:
                    PickRepositionTarget();
                    stateTimer = repositionTimeout;
                    if (agent != null) agent.updateRotation = true; // Face movement direction
                    break;
                    
                case MinibossState.Aiming:
                case MinibossState.Shooting:
                case MinibossState.CastingMeteor:
                case MinibossState.RageMode:
                    StopMovement();
                    if (agent != null) agent.updateRotation = false; // Let LookAtTarget control facing
                    break;
            }
        }
        
        #endregion
        
        #region Fireball Attack
        
        private void StartFireballAttack()
        {
            Debug.Log("[MinibossAI] ==> StartFireballAttack() called!");
            SwitchState(MinibossState.Aiming);
            StartCoroutine(FireballAttackRoutine());
        }
        
        private IEnumerator FireballAttackRoutine()
        {
            Debug.Log("[MinibossAI] ==> FireballAttackRoutine() STARTED");
            
            // Stop and face player
            StopMovement();
            LookAtTarget();
            
            // Reset event flags
            pendingAimStart = false;
            pendingFireballSpawn = false;
            attackAnimationEnded = false;
            
            // Set attack animation bool - this triggers the attack animation
            isFireballActive = true;
            if (animator != null)
            {
                animator.SetBool(fireballBool, true);
                animator.SetBool(walkBool, false);
            }
            
            // Wait for OnStartAiming event (or timeout)
            float timeout = 3f;
            float timer = 0f;
            while (!pendingAimStart && timer < timeout)
            {
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    HideAimLine();
                    ResetAttackState();
                    SwitchState(MinibossState.Chasing);
                    yield break;
                }
                LookAtTarget();
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Show aim line (triggered by event or timeout)
            if (pendingAimStart)
            {
                Debug.Log("[MinibossAI] ==> Aiming started from animation event");
            }
            else
            {
                Debug.LogWarning("[MinibossAI] ==> Aiming started via timeout - add OnStartAiming animation event!");
            }
            ShowAimLine();
            
            // Wait for OnFireFireball event (or timeout) 
            timer = 0f;
            while (!pendingFireballSpawn && timer < timeout)
            {
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    HideAimLine();
                    ResetAttackState();
                    SwitchState(MinibossState.Chasing);
                    yield break;
                }
                LookAtTarget();
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Hide aim line and spawn fireball
            HideAimLine();
            SwitchState(MinibossState.Shooting);
            
            if (pendingFireballSpawn)
            {
                Debug.Log("[MinibossAI] ==> Fireball spawned from animation event!");
            }
            else
            {
                Debug.LogWarning("[MinibossAI] ==> Fireball spawned via timeout - add OnFireFireball animation event!");
            }
            SpawnFireball();
            
            // Reset cooldown
            fireballCooldownTimer = fireballCooldown;
            
            // Wait for attack animation end event (or timeout)
            timer = 0f;
            while (!attackAnimationEnded && timer < timeout)
            {
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    ResetAttackState();
                    SwitchState(MinibossState.Chasing);
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Reset attack state
            ResetAttackState();
            
            if (debugLog) Debug.Log("[MinibossAI] ==> Fireball attack complete");
            
            // Brief pause after attack
            actionPauseTimer = 0.3f;
            
            // Handle attack rhythm (70% attack, 30% reposition)
            OnAttackComplete();
            
            // Return to chasing
            SwitchState(MinibossState.Chasing);
        }
        
        private void SpawnFireball()
        {
            Debug.Log($"[MinibossAI] SpawnFireball() called - fireballPrefab={fireballPrefab}, target={target}, projectileSpawnPoint={projectileSpawnPoint}");
            
            if (fireballPrefab == null)
            {
                Debug.LogError("[MinibossAI] SpawnFireball FAILED - fireballPrefab is NULL!");
                return;
            }
            if (target == null)
            {
                Debug.LogError("[MinibossAI] SpawnFireball FAILED - target is NULL!");
                return;
            }
            
            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            
            // Override Y with configurable spawn height (if set)
            if (fireballSpawnHeight > 0)
            {
                spawnPos.y = fireballSpawnHeight;
            }
            
            // Keep trajectory flat (horizontal only)
            Vector3 targetPos = target.position;
            targetPos.y = spawnPos.y;
            Vector3 direction = (targetPos - spawnPos).normalized;
            
            if (debugLog) Debug.Log($"[MinibossAI] Spawning fireball at height {spawnPos.y}");
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
            
            // Set attack animation bool - triggers attack animation
            isMeteorActive = true;
            pendingMeteorSpawn = false;
            attackAnimationEnded = false;
            
            if (animator != null)
            {
                animator.SetBool(meteorBool, true);
                animator.SetBool(walkBool, false);
                if (debugLog) Debug.Log($"[MinibossAI] ANIM: Summon_Meteor=TRUE, Walk=FALSE - waiting for animation event...");
            }
            
            // Wait for animation event to spawn meteor
            float timeout = 3f; // Safety timeout
            float timer = 0f;
            while (!pendingMeteorSpawn && timer < timeout)
            {
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    ResetAttackState();
                    SwitchState(MinibossState.Chasing);
                    yield break;
                }
                LookAtTarget(); // Keep facing player during cast
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Spawn the meteor (either from event or timeout)
            if (pendingMeteorSpawn)
            {
                SpawnMeteor();
                if (debugLog) Debug.Log($"[MinibossAI] Meteor spawned from animation event!");
            }
            else if (timer >= timeout)
            {
                SpawnMeteor();
                if (debugLog) Debug.LogWarning($"[MinibossAI] Meteor spawned via timeout - add OnSummonMeteor animation event!");
            }
            
            // Reset cooldown
            meteorCooldownTimer = meteorCooldown;
            
            // Wait for attack animation end event
            timer = 0f;
            while (!attackAnimationEnded && timer < timeout)
            {
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    ResetAttackState();
                    SwitchState(MinibossState.Chasing);
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Reset attack state
            ResetAttackState();
            
            if (debugLog) Debug.Log($"[MinibossAI] Meteor attack complete");
            
            // Pause after casting
            actionPauseTimer = 0.5f;
            
            // Handle attack rhythm (70% attack, 30% reposition)
            OnAttackComplete();
            
            // Return to chasing
            SwitchState(MinibossState.Chasing);
        }
        
        /// <summary>
        /// Resets all attack-related state flags and animator bools.
        /// </summary>
        private void ResetAttackState()
        {
            isFireballActive = false;
            isMeteorActive = false;
            pendingAimStart = false;
            pendingFireballSpawn = false;
            pendingMeteorSpawn = false;
            attackAnimationEnded = false;
            
            if (animator != null)
            {
                // Reset attack bools
                animator.SetBool(fireballBool, false);
                animator.SetBool(meteorBool, false);
                // Force Walk = true briefly to ensure Animator transitions out of attack state
                animator.SetBool(walkBool, true);
            }
            
            Debug.Log("[MinibossAI] ResetAttackState: All attack bools reset, Walk=true");
        }
        
        #endregion
        
        #region Animation Event Callbacks
        
        /// <summary>
        /// Called by EnemyAnimationRelay when the aiming phase should start.
        /// This triggers showing the aim line.
        /// </summary>
        public void StartAimingFromEvent()
        {
            Debug.Log("[MinibossAI] Animation event: StartAimingFromEvent()");
            pendingAimStart = true;
        }
        
        /// <summary>
        /// Called by EnemyAnimationRelay when the fireball animation event fires.
        /// This triggers the actual fireball spawn at the perfect animation frame.
        /// </summary>
        public void FireFireballFromEvent()
        {
            Debug.Log("[MinibossAI] Animation event: FireFireballFromEvent()");
            pendingFireballSpawn = true;
        }
        
        /// <summary>
        /// Called by EnemyAnimationRelay when the meteor animation event fires.
        /// This triggers the actual meteor spawn at the perfect animation frame.
        /// </summary>
        public void SummonMeteorFromEvent()
        {
            if (debugLog) Debug.Log("[MinibossAI] Animation event: SummonMeteorFromEvent()");
            pendingMeteorSpawn = true;
        }
        
        /// <summary>
        /// Called by EnemyAnimationRelay when the attack animation ends.
        /// This signals the AI to return to normal behavior.
        /// </summary>
        public void OnAttackAnimationEnd()
        {
            if (debugLog) Debug.Log("[MinibossAI] Animation event: OnAttackAnimationEnd()");
            attackAnimationEnded = true;
        }
        
        /// <summary>
        /// Called by EnemyAnimationRelay at the START of attack animations.
        /// Instantly snaps the miniboss to face the player for precise aiming.
        /// </summary>
        public void LockOntoPlayerFromEvent()
        {
            if (target == null) return;
            
            // Instantly snap to face player
            Vector3 lookDir = (target.position - transform.position);
            lookDir.y = 0;
            
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
            
            if (debugLog) Debug.Log("[MinibossAI] Animation event: LockOntoPlayer - snapped to face player");
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
        
        /// <summary>
        /// Circle-strafe movement around the player at fixed distance.
        /// </summary>
        private void StartCircleStrafe()
        {
            if (!agent.isOnNavMesh || target == null) return;
            
            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0;
            float currentDist = toPlayer.magnitude;
            
            // Pick random direction (left or right)
            float strafeDir = Random.value > 0.5f ? 1f : -1f;
            float angle = circleStrafeAngle * strafeDir;
            
            // Calculate strafe position around player
            Vector3 toUs = -toPlayer.normalized;
            Vector3 rotated = Quaternion.AngleAxis(angle, Vector3.up) * toUs;
            Vector3 strafePos = target.position + rotated * circleStrafeRadius;
            
            // Check NavMesh validity
            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(strafePos, out navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
                repositionTarget = navHit.position;
                SwitchState(MinibossState.Repositioning);
                if (debugLog) Debug.Log($"[MinibossAI] Circle-strafe to {navHit.position}");
            }
            else
            {
                // Failed - use normal reposition
                StartRepositioning();
            }
        }
        
        #endregion
        
        #region Rage Mode
        
        /// <summary>
        /// Checks if HP has crossed a rage mode threshold.
        /// Returns true ONLY when HP drops to or below 80% or 40% (configurable).
        /// Each threshold can only trigger ONCE per fight.
        /// </summary>
        private bool CheckRageModeThreshold()
        {
            if (!enableRageMode || health == null) return false;
            
            // Don't check if already in rage mode
            if (isRageModeActive) return false;
            
            float hpPercent = (float)health.CurrentHealth / health.MaxHealth;
            
            // Check first threshold (default 80% HP) - must be BELOW threshold
            if (!ragePhase1Triggered && hpPercent <= rageThreshold1 && hpPercent < 1.0f)
            {
                ragePhase1Triggered = true;
                Debug.Log($"[MinibossAI] RAGE MODE THRESHOLD 1 HIT! HP={hpPercent:P0}, threshold={rageThreshold1:P0}");
                return true;
            }
            
            // Check second threshold (default 40% HP) - must be BELOW threshold AND phase 1 must be done
            if (ragePhase1Triggered && !ragePhase2Triggered && hpPercent <= rageThreshold2)
            {
                ragePhase2Triggered = true;
                Debug.Log($"[MinibossAI] RAGE MODE THRESHOLD 2 HIT! HP={hpPercent:P0}, threshold={rageThreshold2:P0}");
                return true;
            }
            
            return false;
        }
        
        private void StartRageMode()
        {
            // Safety: Don't start if already in rage mode
            if (isRageModeActive || currentState == MinibossState.RageMode)
            {
                if (debugLog) Debug.LogWarning("[MinibossAI] StartRageMode called but already in rage mode!");
                return;
            }
            
            Debug.Log($"[MinibossAI] STARTING RAGE MODE! Phase1={ragePhase1Triggered}, Phase2={ragePhase2Triggered}");
            SwitchState(MinibossState.RageMode);
            StartCoroutine(RageModeRoutine());
        }
        
        private IEnumerator RageModeRoutine()
        {
            if (debugLog) Debug.Log("[MinibossAI] === RAGE MODE STARTED ===");
            
            // Stop and face player
            StopMovement();
            LookAtTarget();
            
            // Reset event flags
            pendingRageFire = false;
            rageAnimationEnded = false;
            isRageModeActive = true;
            
            // Set animation bool
            if (animator != null)
            {
                animator.SetBool(rageModeBool, true);
                animator.SetBool(walkBool, false);
            }
            
            // Show shield VFX (invulnerability indicator)
            if (rageShieldVFX != null)
            {
                activeShieldVFX = Instantiate(rageShieldVFX, transform.position, Quaternion.identity, transform);
            }
            
            // Wait for charge duration (invulnerable during this)
            float timer = 0f;
            float timeout = rageChargeDuration + 1f;
            while (!pendingRageFire && timer < timeout)
            {
                if (health != null && health.IsDead)
                {
                    EndRageMode();
                    yield break;
                }
                LookAtTarget();
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Fire 360° fireball burst!
            Spawn360FireballBurst();
            
            // Wait for animation to end
            timer = 0f;
            while (!rageAnimationEnded && timer < 2f)
            {
                if (health != null && health.IsDead)
                {
                    EndRageMode();
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
            
            // End rage mode
            EndRageMode();
            
            if (debugLog) Debug.Log("[MinibossAI] === RAGE MODE ENDED ===");
            
            // Reposition to cover after rage (flee behavior)
            consecutiveAttackCount = 0; // Reset counter
            StartRepositioning();
        }
        
        private void Spawn360FireballBurst()
        {
            if (debugLog) Debug.Log($"[MinibossAI] Spawning {rageFireballCount} fireballs in 360° pattern!");
            
            GameObject prefabToUse = bouncingFireballPrefab != null ? bouncingFireballPrefab : fireballPrefab;
            if (prefabToUse == null) return;
            
            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            float angleStep = 360f / rageFireballCount;
            
            for (int i = 0; i < rageFireballCount; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                direction.y = 0;
                direction.Normalize();
                
                GameObject proj = Instantiate(prefabToUse, spawnPos, Quaternion.LookRotation(direction));
                
                EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
                if (ep != null)
                {
                    ep.SetDirection(direction);
                    ep.SetSpeed(rageFireballSpeed); // Use slower rage fireball speed
                    ep.SetDamage(fireballDamage);
                    
                    // Enable bouncing with configurable count
                    ep.EnableBounce(rageFireballBounces);
                }
            }
        }
        
        private void EndRageMode()
        {
            isRageModeActive = false;
            
            // Clear animation
            if (animator != null)
            {
                animator.SetBool(rageModeBool, false);
            }
            
            // Destroy shield VFX
            if (activeShieldVFX != null)
            {
                Destroy(activeShieldVFX);
                activeShieldVFX = null;
            }
        }
        
        // Animation event callbacks for rage mode
        public void RageModeStartFromEvent()
        {
            if (debugLog) Debug.Log("[MinibossAI] Animation event: RageModeStartFromEvent()");
            // Could trigger additional VFX here
        }
        
        public void RageModeFireFromEvent()
        {
            if (debugLog) Debug.Log("[MinibossAI] Animation event: RageModeFireFromEvent()");
            pendingRageFire = true;
        }
        
        public void RageModeEndFromEvent()
        {
            if (debugLog) Debug.Log("[MinibossAI] Animation event: RageModeEndFromEvent()");
            rageAnimationEnded = true;
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
            Debug.Log($"[MinibossAI] ShowAimLine() - aimLine.enabled = {aimLine.enabled}");
            // Start coroutine to update aim line (like Sniper does)
            StartCoroutine(UpdateAimLineRoutine());
        }
        
        private void HideAimLine()
        {
            if (aimLine == null) return;
            aimLine.enabled = false;
        }
        
        /// <summary>
        /// Coroutine that updates aim line positions while it's enabled.
        /// Matches Sniper's UpdateAimLineRoutine approach.
        /// </summary>
        private IEnumerator UpdateAimLineRoutine()
        {
            float elapsed = 0f;
            float maxAimTime = aimDuration + 0.5f; // Safety timeout
            
            while (aimLine != null && aimLine.enabled && target != null && elapsed < maxAimTime)
            {
                // Check for interruption
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    HideAimLine();
                    yield break;
                }
                
                // Exit if we left aiming state
                if (currentState != MinibossState.Aiming)
                {
                    yield break; // Don't hide - might be transitioning to shooting
                }
                
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / aimDuration);
                
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
                
                // Debug first call
                if (t < 0.05f)
                {
                    Debug.Log($"[MinibossAI] UpdateAimLineRoutine: start={startPos}, end={endPos}, direction={direction}");
                }
                
                yield return null;
            }
        }
        
        #endregion
        
        #region Animation
        
        protected override void UpdateAnimator()
        {
            if (animator == null) return;
            
            // USER LOGIC: Walk = true ONLY when NOT attacking
            // If either attack bool is active, Walk must be false
            bool shouldWalk = !isFireballActive && !isMeteorActive && 
                              agent != null && agent.velocity.magnitude > 0.1f;
            
            animator.SetBool(walkBool, shouldWalk);
            
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
