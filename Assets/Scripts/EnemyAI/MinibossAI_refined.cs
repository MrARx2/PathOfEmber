using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Audio;

namespace EnemyAI
{
    /// <summary>
    /// Refined Miniboss AI - Clean, performance-optimized implementation.
    /// 
    /// Behavior:
    /// - Has LOS → Fireball attack (with aim line white→red)
    /// - No LOS → Meteor attack
    /// - 40%/80% HP → Rage burst (12 bouncing fireballs)
    /// - Hit 3x in 1s OR 5x total → Force reposition
    /// - 0.2s pause before repositioning for player fairness
    /// </summary>
    public class MinibossAI_refined : MonoBehaviour
    {
        #region Enums
        
        private enum State
        {
            Idle,           // Waiting for player reference
            Repositioning,  // Moving to a new position
            Attacking,      // Executing attack (fireball or meteor)
            RageBurst,      // 360° fireball burst
            Cooldown        // Brief pause, deciding next action
        }
        
        #endregion
        
        #region Serialized Fields
        
        [Header("=== REFERENCES ===")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private Transform modelTransform;
        
        [Header("=== PREFABS ===")]
        [SerializeField] private GameObject fireballPrefab;
        [SerializeField] private GameObject meteorStrikePrefab;
        [SerializeField] private GameObject bouncingFireballPrefab;
        [SerializeField] private GameObject rageShieldVFX;
        
        [Header("=== COMBAT SETTINGS ===")]
        [SerializeField] private float preferredRange = 10f;
        [SerializeField] private float fireballCooldown = 2f;
        [SerializeField] private float meteorCooldown = 8f;
        [SerializeField] private float preRepositionDelay = 0.2f;
        
        [Header("=== FIREBALL ===")]
        [SerializeField] private float fireballSpeed = 15f;
        [SerializeField] private int fireballDamage = 30;
        [SerializeField] private float aimDuration = 1f;
        [SerializeField] private float aimLineLength = 25f;
        [SerializeField] private float aimLineWidth = 0.08f;
        [SerializeField] private Color aimLineStartColor = Color.white;
        [SerializeField] private Color aimLineEndColor = new Color(1f, 0.3f, 0f, 1f);
        
        [Header("=== RAGE BURST ===")]
        [SerializeField] private float rageThreshold1 = 0.8f;
        [SerializeField] private float rageThreshold2 = 0.4f;
        [SerializeField] private int rageFireballCount = 12;
        [SerializeField] private float rageFireballSpeed = 5f;
        [SerializeField] private int rageFireballBounces = 3;
        [SerializeField] private float rageChargeDuration = 2f;
        
        [Header("=== MOVEMENT ===")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float repositionDistance = 5f;
        [SerializeField] private float minWallDistance = 2f;
        [SerializeField] private float wallCheckRange = 8f;
        
        [Header("=== HIT REACTION ===")]
        [SerializeField] private int hitsForQuickReposition = 3;
        [SerializeField] private float quickHitWindow = 1f;
        [SerializeField] private int hitsForSlowReposition = 5;
        
        [Header("=== ANIMATION ===")]
        [SerializeField] private string walkBool = "Walk";
        [SerializeField] private string fireballBool = "Shoot_FireBall";
        [SerializeField] private string meteorBool = "Summon_Meteor";
        [SerializeField] private string rageBool = "RageMode_Charge";
        
        [Header("=== SOUND ===")]
        [SerializeField] private SoundEvent fireballSound;
        [SerializeField] private SoundEvent rageChargeSound;
        [SerializeField] private SoundEvent rageBurstSound;
        
        [Header("=== DEBUG ===")]
        [SerializeField] private bool debugLog = false;
        
        #endregion
        
        #region Private State
        
        private State currentState = State.Idle;
        private NavMeshAgent agent;
        private Animator animator;
        private EnemyHealth health;
        private LineRenderer aimLine;
        
        // Cooldowns
        private float fireballCooldownTimer;
        private float meteorCooldownTimer;
        
        // Rage tracking
        private bool ragePhase1Triggered = false;
        private bool ragePhase2Triggered = false;
        private GameObject activeShieldVFX;
        
        // Hit tracking (for reposition trigger)
        private List<float> hitTimestamps = new List<float>();
        
        // Animation event flags
        private bool pendingFireballSpawn = false;
        private bool pendingMeteorSpawn = false;
        private bool attackAnimationEnded = false;
        
        // === PERFORMANCE CACHES ===
        private int _frameOffset;
        private const int LOS_CHECK_INTERVAL = 5;
        private const int WALL_CHECK_INTERVAL = 15;
        private bool _cachedHasLOS = false;
        
        // Wall distance cache (4 cardinal directions)
        private float _wallDistForward = float.MaxValue;
        private float _wallDistBack = float.MaxValue;
        private float _wallDistLeft = float.MaxValue;
        private float _wallDistRight = float.MaxValue;
        
        // Obstacle layer for raycasts
        private LayerMask obstacleLayer;
        
        #endregion
        
        #region Properties
        
        private Vector3 VisualPosition => modelTransform != null ? modelTransform.position : transform.position;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            health = GetComponent<EnemyHealth>();
            
            // Configure NavMeshAgent
            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.stoppingDistance = 0.5f;
                agent.updateRotation = true;
            }
            
            // Randomize frame offset to spread CPU load
            _frameOffset = Random.Range(0, Mathf.Max(LOS_CHECK_INTERVAL, WALL_CHECK_INTERVAL));
            
            // Default obstacle layer
            obstacleLayer = LayerMask.GetMask("Default", "Environment", "Obstacles");
            
            SetupAimLine();
        }
        
        private void Start()
        {
            // Find player if not assigned
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            
            if (projectileSpawnPoint == null)
                projectileSpawnPoint = transform;
            
            // Start with partial cooldowns
            fireballCooldownTimer = fireballCooldown * 0.5f;
            meteorCooldownTimer = meteorCooldown * 0.5f;
            
            // Subscribe to damage events for hit tracking
            if (health != null)
            {
                health.OnDamage.AddListener(OnTakeDamage);
            }
            
            // Begin AI
            if (target != null)
                SwitchState(State.Cooldown);
        }
        
        private void OnDestroy()
        {
            if (health != null)
                health.OnDamage.RemoveListener(OnTakeDamage);
        }
        
        private void Update()
        {
            // Early exits
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                StopMovement();
                HideAimLine();
                return;
            }
            
            if (target == null) return;
            
            // Update cooldowns
            if (fireballCooldownTimer > 0) fireballCooldownTimer -= Time.deltaTime;
            if (meteorCooldownTimer > 0) meteorCooldownTimer -= Time.deltaTime;
            
            // Throttled checks
            UpdateLOSCheck();
            UpdateWallAwareness();
            
            // State machine
            switch (currentState)
            {
                case State.Idle:
                    // Wait for target
                    if (target != null) SwitchState(State.Cooldown);
                    break;
                    
                case State.Repositioning:
                    // Handled by RepositioningRoutine coroutine
                    // Just ensure we keep looking at target while moving
                    LookAtTarget();
                    break;
                    
                case State.Attacking:
                case State.RageBurst:
                    // Handled by coroutines - ensure we're stopped
                    StopMovement();
                    LookAtTarget();
                    break;
                    
                case State.Cooldown:
                    // Will be handled by coroutine
                    LookAtTarget();
                    break;
            }
            
            UpdateAnimator();
        }
        
        #endregion
        
        #region State Machine
        
        private void SwitchState(State newState)
        {
            if (debugLog) Debug.Log($"[MinibossAI_refined] {currentState} → {newState}");
            currentState = newState;
            
            switch (newState)
            {
                case State.Repositioning:
                    StartCoroutine(RepositioningRoutine());
                    break;
                case State.Cooldown:
                    StartCoroutine(CooldownRoutine());
                    break;
            }
        }
        
        private IEnumerator CooldownRoutine()
        {
            // Pre-reposition delay for player fairness
            yield return new WaitForSeconds(preRepositionDelay);
            
            // Check for rage mode
            if (CheckRageThreshold())
            {
                StartCoroutine(RageBurstRoutine());
                yield break;
            }
            
            // Check for hit-reaction reposition
            if (ShouldForceReposition())
            {
                ClearHitTracking();
                SwitchState(State.Repositioning);
                yield break;
            }
            
            // Decide attack based on LOS
            if (_cachedHasLOS && fireballCooldownTimer <= 0)
            {
                StartCoroutine(FireballAttackRoutine());
            }
            else if (!_cachedHasLOS && meteorCooldownTimer <= 0)
            {
                StartCoroutine(MeteorAttackRoutine());
            }
            else
            {
                // Both on cooldown - reposition
                SwitchState(State.Repositioning);
            }
        }
        
        private IEnumerator RepositioningRoutine()
        {
            // Pick a smart reposition target
            Vector3 targetPos = PickRepositionTarget();
            
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(targetPos);
            }
            
            float timeout = 3f;
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                // Check if reached destination
                if (agent != null && !agent.pathPending)
                {
                    if (agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                    {
                        break;
                    }
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            StopMovement();
            SwitchState(State.Cooldown);
        }
        
        #endregion
        
        #region Attacks
        
        private IEnumerator FireballAttackRoutine()
        {
            currentState = State.Attacking;
            StopMovement();
            
            // Reset animation flags
            pendingFireballSpawn = false;
            attackAnimationEnded = false;
            
            // Show aim line and start animation
            ShowAimLine();
            if (animator != null) animator.SetBool(fireballBool, true);
            
            // Aim phase - lerp line color
            float aimTimer = 0f;
            while (aimTimer < aimDuration)
            {
                aimTimer += Time.deltaTime;
                float t = aimTimer / aimDuration;
                UpdateAimLine(Color.Lerp(aimLineStartColor, aimLineEndColor, t));
                LookAtTarget();
                yield return null;
            }
            
            // Wait for animation event OR timeout
            float eventTimeout = 2f;
            float eventTimer = 0f;
            while (!pendingFireballSpawn && eventTimer < eventTimeout)
            {
                eventTimer += Time.deltaTime;
                LookAtTarget();
                yield return null;
            }
            
            // Spawn fireball
            SpawnFireball();
            HideAimLine();
            
            // Play sound
            if (fireballSound != null)
                AudioManager.Instance?.PlayAtPosition(fireballSound, transform.position);
            
            // Wait for animation to end
            while (!attackAnimationEnded && eventTimer < eventTimeout + 1f)
            {
                eventTimer += Time.deltaTime;
                yield return null;
            }
            
            if (animator != null) animator.SetBool(fireballBool, false);
            
            fireballCooldownTimer = fireballCooldown;
            SwitchState(State.Cooldown);
        }
        
        private IEnumerator MeteorAttackRoutine()
        {
            currentState = State.Attacking;
            StopMovement();
            
            pendingMeteorSpawn = false;
            attackAnimationEnded = false;
            
            if (animator != null) animator.SetBool(meteorBool, true);
            
            // Wait for animation event
            float timeout = 3f;
            float timer = 0f;
            while (!pendingMeteorSpawn && timer < timeout)
            {
                timer += Time.deltaTime;
                LookAtTarget();
                yield return null;
            }
            
            // Spawn meteor at player position
            SpawnMeteor();
            
            // Wait for animation end
            while (!attackAnimationEnded && timer < timeout + 1f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
            
            if (animator != null) animator.SetBool(meteorBool, false);
            
            meteorCooldownTimer = meteorCooldown;
            SwitchState(State.Repositioning); // Always reposition after meteor
        }
        
        private IEnumerator RageBurstRoutine()
        {
            currentState = State.RageBurst;
            StopMovement();
            
            // Become invulnerable
            if (health != null) health.SetInvulnerable(true);
            
            // Show shield VFX
            if (rageShieldVFX != null)
            {
                Vector3 shieldPos = VisualPosition + Vector3.up * 1f;
                activeShieldVFX = Instantiate(rageShieldVFX, shieldPos, Quaternion.identity, transform);
            }
            
            // Play charge sound
            if (rageChargeSound != null)
                AudioManager.Instance?.PlayAtPosition(rageChargeSound, transform.position);
            
            if (animator != null) animator.SetBool(rageBool, true);
            
            // Charge duration
            yield return new WaitForSeconds(rageChargeDuration);
            
            // Fire 360° burst
            Spawn360Burst();
            
            // Play burst sound
            if (rageBurstSound != null)
                AudioManager.Instance?.PlayAtPosition(rageBurstSound, transform.position);
            
            // End invulnerability
            if (health != null) health.SetInvulnerable(false);
            
            // Destroy shield VFX
            if (activeShieldVFX != null)
            {
                Destroy(activeShieldVFX);
                activeShieldVFX = null;
            }
            
            if (animator != null) animator.SetBool(rageBool, false);
            
            yield return new WaitForSeconds(0.5f);
            SwitchState(State.Repositioning);
        }
        
        #endregion
        
        #region Spawning
        
        private void SpawnFireball()
        {
            if (fireballPrefab == null || target == null) return;
            
            Vector3 spawnPos = projectileSpawnPoint != null 
                ? projectileSpawnPoint.position 
                : VisualPosition + Vector3.up * 1.5f;
            
            Vector3 direction = (target.position - spawnPos).normalized;
            direction.y = 0; // Flat trajectory
            
            GameObject proj = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.Get(fireballPrefab, spawnPos, Quaternion.LookRotation(direction))
                : Instantiate(fireballPrefab, spawnPos, Quaternion.LookRotation(direction));
            
            EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
            if (ep != null)
            {
                ep.SetDirection(direction);
                ep.SetSpeed(fireballSpeed);
                ep.SetDamage(fireballDamage);
            }
        }
        
        private void SpawnMeteor()
        {
            if (meteorStrikePrefab == null || target == null) return;
            
            Vector3 spawnPos = target.position;
            
            GameObject meteor = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.Get(meteorStrikePrefab, spawnPos, Quaternion.identity)
                : Instantiate(meteorStrikePrefab, spawnPos, Quaternion.identity);
        }
        
        private void Spawn360Burst()
        {
            GameObject prefab = bouncingFireballPrefab != null ? bouncingFireballPrefab : fireballPrefab;
            if (prefab == null) return;
            
            Vector3 spawnPos = VisualPosition + Vector3.up * 0.3f;
            float angleStep = 360f / rageFireballCount;
            
            for (int i = 0; i < rageFireballCount; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                
                GameObject proj = ObjectPoolManager.Instance != null
                    ? ObjectPoolManager.Instance.Get(prefab, spawnPos, Quaternion.LookRotation(direction))
                    : Instantiate(prefab, spawnPos, Quaternion.LookRotation(direction));
                
                EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
                if (ep != null)
                {
                    ep.SetDirection(direction);
                    ep.SetSpeed(rageFireballSpeed);
                    ep.SetDamage(fireballDamage);
                    ep.EnableBounce(rageFireballBounces);
                }
            }
        }
        
        #endregion
        
        #region Line of Sight & Wall Awareness
        
        private void UpdateLOSCheck()
        {
            if ((Time.frameCount + _frameOffset) % LOS_CHECK_INTERVAL != 0) return;
            if (target == null) return;
            
            Vector3 origin = VisualPosition + Vector3.up * 1f;
            Vector3 toTarget = target.position - origin;
            toTarget.y = 0; // Ignore Y
            float dist = toTarget.magnitude;
            
            RaycastHit hit;
            if (Physics.Raycast(origin, toTarget.normalized, out hit, dist, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                // Hit something - check if it's the player
                _cachedHasLOS = hit.collider.CompareTag("Player") || hit.collider.transform == target;
            }
            else
            {
                _cachedHasLOS = true; // Nothing blocking
            }
            
            if (debugLog) Debug.Log($"[MinibossAI_refined] LOS: {_cachedHasLOS}");
        }
        
        private void UpdateWallAwareness()
        {
            if ((Time.frameCount + _frameOffset) % WALL_CHECK_INTERVAL != 0) return;
            
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            
            // 4 cardinal directions (XZ plane)
            _wallDistForward = CastWallRay(origin, transform.forward);
            _wallDistBack = CastWallRay(origin, -transform.forward);
            _wallDistLeft = CastWallRay(origin, -transform.right);
            _wallDistRight = CastWallRay(origin, transform.right);
            
            if (debugLog)
            {
                Debug.Log($"[MinibossAI_refined] Walls: F={_wallDistForward:F1} B={_wallDistBack:F1} L={_wallDistLeft:F1} R={_wallDistRight:F1}");
            }
        }
        
        private float CastWallRay(Vector3 origin, Vector3 direction)
        {
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, wallCheckRange, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                return hit.distance;
            }
            return float.MaxValue;
        }
        
        #endregion
        
        #region Repositioning
        
        private Vector3 PickRepositionTarget()
        {
            // Find the direction with most space
            float maxDist = 0f;
            Vector3 bestDir = -transform.forward; // Default: back away
            
            if (_wallDistForward > maxDist && _wallDistForward > minWallDistance)
            {
                maxDist = _wallDistForward;
                bestDir = transform.forward;
            }
            if (_wallDistBack > maxDist && _wallDistBack > minWallDistance)
            {
                maxDist = _wallDistBack;
                bestDir = -transform.forward;
            }
            if (_wallDistLeft > maxDist && _wallDistLeft > minWallDistance)
            {
                maxDist = _wallDistLeft;
                bestDir = -transform.right;
            }
            if (_wallDistRight > maxDist && _wallDistRight > minWallDistance)
            {
                maxDist = _wallDistRight;
                bestDir = transform.right;
            }
            
            // Calculate target position (don't go all the way to the wall)
            float moveDist = Mathf.Min(repositionDistance, maxDist - minWallDistance);
            moveDist = Mathf.Max(moveDist, 1f); // At least 1 unit
            
            Vector3 targetPos = transform.position + bestDir * moveDist;
            
            // Validate on NavMesh
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(targetPos, out navHit, 3f, NavMesh.AllAreas))
            {
                return navHit.position;
            }
            
            return transform.position; // Stay put if no valid position
        }
        
        #endregion
        
        #region Hit Tracking
        
        private void OnTakeDamage(int damage)
        {
            hitTimestamps.Add(Time.time);
            
            // Cleanup old timestamps
            hitTimestamps.RemoveAll(t => Time.time - t > quickHitWindow * 2f);
        }
        
        private bool ShouldForceReposition()
        {
            // Count hits in quick window
            int quickHits = 0;
            foreach (float t in hitTimestamps)
            {
                if (Time.time - t <= quickHitWindow)
                    quickHits++;
            }
            
            if (quickHits >= hitsForQuickReposition)
                return true;
            
            // Total hits (slower pace)
            if (hitTimestamps.Count >= hitsForSlowReposition)
                return true;
            
            return false;
        }
        
        private void ClearHitTracking()
        {
            hitTimestamps.Clear();
        }
        
        #endregion
        
        #region Rage Mode
        
        private bool CheckRageThreshold()
        {
            if (health == null) return false;
            
            float hpPercent = (float)health.CurrentHealth / health.MaxHealth;
            
            if (!ragePhase1Triggered && hpPercent <= rageThreshold1)
            {
                ragePhase1Triggered = true;
                return true;
            }
            
            if (!ragePhase2Triggered && hpPercent <= rageThreshold2)
            {
                ragePhase2Triggered = true;
                return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Aim Line
        
        private void SetupAimLine()
        {
            aimLine = gameObject.AddComponent<LineRenderer>();
            aimLine.positionCount = 2;
            aimLine.startWidth = aimLineWidth;
            aimLine.endWidth = aimLineWidth * 0.5f;
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.enabled = false;
        }
        
        private void ShowAimLine()
        {
            if (aimLine != null) aimLine.enabled = true;
        }
        
        private void HideAimLine()
        {
            if (aimLine != null) aimLine.enabled = false;
        }
        
        private void UpdateAimLine(Color color)
        {
            if (aimLine == null || target == null) return;
            
            Vector3 start = projectileSpawnPoint != null 
                ? projectileSpawnPoint.position 
                : VisualPosition + Vector3.up * 1.5f;
            
            Vector3 direction = (target.position - start).normalized;
            direction.y = 0;
            
            Vector3 end = start + direction * aimLineLength;
            
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, end);
            aimLine.startColor = color;
            aimLine.endColor = color * 0.5f;
        }
        
        #endregion
        
        #region Helpers
        
        private void StopMovement()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }
        
        private void LookAtTarget()
        {
            if (target == null) return;
            
            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0;
            
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f * Time.deltaTime);
            }
        }
        
        private void UpdateAnimator()
        {
            if (animator == null) return;
            
            bool isWalking = currentState == State.Repositioning && agent != null && agent.velocity.sqrMagnitude > 0.1f;
            animator.SetBool(walkBool, isWalking);
        }
        
        #endregion
        
        #region Animation Events
        
        // Called by animation event when fireball should spawn
        public void OnFireFireball()
        {
            pendingFireballSpawn = true;
        }
        
        // Called by animation event when meteor should spawn
        public void OnSpawnMeteor()
        {
            pendingMeteorSpawn = true;
        }
        
        // Called when attack animation ends
        public void OnAttackAnimationEnd()
        {
            attackAnimationEnded = true;
        }
        
        // Called when aiming begins (optional)
        public void OnStartAiming()
        {
            ShowAimLine();
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            // Draw wall detection rays
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            
            Gizmos.color = Color.red;
            Gizmos.DrawRay(origin, transform.forward * Mathf.Min(_wallDistForward, wallCheckRange));
            Gizmos.DrawRay(origin, -transform.forward * Mathf.Min(_wallDistBack, wallCheckRange));
            Gizmos.DrawRay(origin, -transform.right * Mathf.Min(_wallDistLeft, wallCheckRange));
            Gizmos.DrawRay(origin, transform.right * Mathf.Min(_wallDistRight, wallCheckRange));
            
            // Draw preferred range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, preferredRange);
        }
        
        #endregion
    }
}
