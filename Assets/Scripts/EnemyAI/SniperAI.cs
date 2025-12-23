using UnityEngine;
using System.Collections;

namespace EnemyAI
{
    /// <summary>
    /// Sniper enemy - ranged attacker that shoots from distance.
    /// Supports animation events for projectile spawning.
    /// </summary>
    public class SniperAI : EnemyAIBase
    {
        [Header("=== SNIPER SETTINGS ===")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField, Tooltip("Speed override (0 = use prefab default)")]
        private float projectileSpeed = 0f;
        [SerializeField, Tooltip("Damage override (0 = use prefab default)")]
        private int projectileDamage = 0;

        [Header("=== RANGE SETTINGS ===")]
        [SerializeField, Tooltip("Maximum range to engage player")]
        private float maxEngageRange = 8f;
        
        [SerializeField, Tooltip("If player gets closer than this, sniper will try to reposition")]
        private float minComfortRange = 2f;
        
        [SerializeField, Tooltip("Chance to reposition after each shot (0-1)")]
        private float repositionChance = 0.3f;
        
        [SerializeField, Tooltip("How far to reposition")]
        private float repositionDistance = 2f;

        [Header("=== ANIMATION ===")]
        [SerializeField, Tooltip("Name of the Shoot bool/trigger parameter")]
        private string shootParameter = "Shoot";
        
        [SerializeField, Tooltip("Is the shoot parameter a bool (true) or trigger (false)?")]
        private bool shootParameterIsBool = true;
        
        [SerializeField, Tooltip("Use animation event to spawn projectile instead of delay")]
        private bool useAnimationEvent = true;
        
        [SerializeField, Tooltip("Fallback: delay before spawning projectile (if not using anim event)")]
        private float projectileSpawnDelay = 0.2f;
        
        [SerializeField, Tooltip("How long the shoot animation takes")]
        private float shootAnimationDuration = 0.5f;

        private float actionPauseTimer;
        private bool isRepositioning;
        private bool isShooting;
        private bool waitingForAnimEvent;

        protected override void Awake()
        {
            base.Awake();
            
            // Sniper priority: medium
            if (agent != null)
                agent.avoidancePriority = 55;
        }

        protected override void Start()
        {
            base.Start();
            if (projectileSpawnPoint == null)
                projectileSpawnPoint = transform;
        }

        protected override void Update()
        {
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                StopMovement();
                ResetShootAnimation();
                return;
            }

            if (target == null)
            {
                StopMovement();
                ResetShootAnimation();
                return;
            }

            // Handle cooldown
            if (attackCooldown > 0)
                attackCooldown -= Time.deltaTime;

            // Handle pause after actions (includes shooting animation)
            if (actionPauseTimer > 0)
            {
                actionPauseTimer -= Time.deltaTime;
                StopMovement();
                LookAtTarget();
                UpdateAnimator();
                return;
            }

            float distance = Vector3.Distance(VisualPosition, target.position);

            // Always look at player
            LookAtTarget();

            // If repositioning, wait until we reach destination
            if (isRepositioning)
            {
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    isRepositioning = false;
                    StopMovement();
                }
                UpdateAnimator();
                return;
            }

            // PRIORITY 1: If player is too far, move closer
            if (distance > maxEngageRange)
            {
                ChasePlayer();
            }
            // PRIORITY 2: If in range, SHOOT
            else
            {
                StopMovement();
                
                // Try to shoot
                if (attackCooldown <= 0 && !isShooting)
                {
                    StartShootSequence();
                    
                    // Set cooldown
                    float cooldown = 1f / Mathf.Max(0.01f, attacksPerSecond);
                    attackCooldown = cooldown;
                    // Lock movement until animation ends (handled by OnShootAnimationEnd)
                    actionPauseTimer = 999f;
                }
            }

            UpdateAnimator();
        }

        private void StartShootSequence()
        {
            isShooting = true;
            waitingForAnimEvent = useAnimationEvent;
            
            // Start shoot animation
            if (animator != null && !string.IsNullOrEmpty(shootParameter))
            {
                if (shootParameterIsBool)
                    animator.SetBool(shootParameter, true);
                else
                    animator.SetTrigger(shootParameter);
            }

            // If not using animation events, use delay instead
            if (!useAnimationEvent)
            {
                StartCoroutine(DelayedProjectileSpawn());
            }
            
            // Schedule animation end
            StartCoroutine(ShootAnimationTimeout());
        }

        private IEnumerator DelayedProjectileSpawn()
        {
            yield return new WaitForSeconds(projectileSpawnDelay);
            if (isShooting)
            {
                SpawnProjectile();
                
                // Maybe reposition after shot (if not using events)
                float distance = target != null ? Vector3.Distance(VisualPosition, target.position) : 0f;
                if (distance < minComfortRange || Random.value < repositionChance)
                {
                    StartCoroutine(DelayedReposition());
                }
            }
        }

        private IEnumerator ShootAnimationTimeout()
        {
            // If using events, allow a safety buffer (e.g. 2s extra) so we don't cancel early
            // If delay-based, use exact duration
            float delay = useAnimationEvent ? shootAnimationDuration + 2.0f : shootAnimationDuration;
            
            yield return new WaitForSeconds(delay);
            
            if (isShooting)
            {
                if (debugLog) Debug.LogWarning("[SniperAI] Shoot timed out - forcing animation end.");
                OnShootAnimationEnd();
            }
        }

        /// <summary>
        /// Called by EnemyAnimationRelay when animation event fires.
        /// </summary>
        public void FireProjectileFromEvent()
        {
            if (isShooting && waitingForAnimEvent)
            {
                waitingForAnimEvent = false;
                SpawnProjectile();
                
                // Maybe reposition after shot
                float distance = target != null ? Vector3.Distance(VisualPosition, target.position) : 0f;
                if (distance < minComfortRange || Random.value < repositionChance)
                {
                    StartCoroutine(DelayedReposition());
                }
            }
            else if (debugLog)
            {
                Debug.LogWarning($"[SniperAI] FireProjectileFromEvent ignored: isShooting={isShooting}, waiting={waitingForAnimEvent}");
            }
        }

        /// <summary>
        /// Called when shoot animation ends (by animation event or timeout).
        /// </summary>
        public void OnShootAnimationEnd()
        {
            ResetShootAnimation();
            isShooting = false;
            waitingForAnimEvent = false;
            actionPauseTimer = 0f;
        }

        private void ResetShootAnimation()
        {
            if (animator != null && !string.IsNullOrEmpty(shootParameter) && shootParameterIsBool)
            {
                animator.SetBool(shootParameter, false);
            }
        }

        private IEnumerator DelayedReposition()
        {
            yield return new WaitForSeconds(0.1f);
            if (!health.IsDead)
                TryReposition();
        }

        private void TryReposition()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            if (target == null) return;

            // Find a position - mostly sideways movement
            Vector3 awayDir = (VisualPosition - target.position).normalized;
            Vector3 sideDir = Vector3.Cross(awayDir, Vector3.up);
            if (Random.value > 0.5f) sideDir = -sideDir;
            
            Vector3 moveDir = (awayDir * 0.3f + sideDir * 0.7f).normalized;
            Vector3 newPos = transform.position + moveDir * repositionDistance;
            
            // Make sure we don't go too far from player
            float newDistance = Vector3.Distance(newPos, target.position);
            if (newDistance > maxEngageRange)
            {
                newPos = transform.position + sideDir * repositionDistance;
            }

            agent.isStopped = false;
            agent.SetDestination(newPos);
            isRepositioning = true;

            if (debugLog)
                Debug.Log("[SniperAI] Repositioning...");
        }

        private void SpawnProjectile()
        {
            if (projectilePrefab == null || target == null) return;

            Vector3 spawnPos = projectileSpawnPoint.position;
            
            // Keep trajectory flat - aim at target but at spawn height
            Vector3 targetPos = target.position;
            targetPos.y = spawnPos.y;
            
            Vector3 direction = (targetPos - spawnPos).normalized;

            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(direction));
            
            EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
            if (ep != null)
            {
                ep.SetDirection(direction);
                if (projectileSpeed > 0)
                    ep.SetSpeed(projectileSpeed);
                if (projectileDamage > 0)
                    ep.SetDamage(projectileDamage);
            }

            if (debugLog)
                Debug.Log("[SniperAI] Fired projectile!");
        }

        protected override void OnAttack()
        {
            // Not used - we handle shooting via animation events
        }
    }
}
