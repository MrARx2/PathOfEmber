using UnityEngine;
using System.Collections;
using Audio;

namespace EnemyAI
{
    /// <summary>
    /// Sniper enemy - ranged attacker with "Archero Skeleton" style movement.
    /// Alternates between Axis-Locked Movement and Shooting.
    /// - Moves only in cardinal directions (Up, Down, Left, Right).
    /// - Body rotation is locked to movement direction while moving.
    /// - Looks at player only when shooting.
    /// </summary>
    public class SniperAI : EnemyAIBase
    {
        private enum SniperState { Idle, Moving, Shooting }

        [Header("=== SNIPER SETTINGS ===")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField, Tooltip("Speed override (0 = use prefab default)")]
        private float projectileSpeed = 0f;
        [SerializeField, Tooltip("Damage override (0 = use prefab default)")]
        private int projectileDamage = 0;

        [Header("=== MOVEMENT SETTINGS ===")]
        [SerializeField, Tooltip("Stopping distance tolerance")]
        private float moveTolerance = 0.5f;

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

        [Header("=== AIM LINE INDICATOR ===")]
        [SerializeField, Tooltip("Show aim line during aiming phase")]
        private bool showAimLine = true;
        [SerializeField, Tooltip("Starting color (when aiming begins)")]
        private Color aimLineStartColor = Color.white;
        [SerializeField, Tooltip("End color (when about to fire)")]
        private Color aimLineEndColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField, Tooltip("Aim line width")]
        private float aimLineWidth = 0.05f;
        [SerializeField, Tooltip("How far the aim line extends")]
        private float aimLineLength = 20f;
        [SerializeField, Tooltip("How long the aiming phase lasts (for color transition)")]
        private float aimDuration = 0.5f;

        [Header("=== SOUND EFFECTS ===")]
        [SerializeField] private SoundEvent aimSound;
        [SerializeField] private SoundEvent shootSound;

        // State Machine
        private SniperState currentState = SniperState.Idle;
        private Vector3 moveDirection;
        private bool waitingForAnimEvent;
        private bool isShooting; // Added back for ShootRoutine
        private float stateTimer;

        // Cache
        private Coroutine shootCoroutine;
        private LineRenderer aimLine;
        
        // Performance: Shared material to prevent allocation per sniper
        private static Material _sharedAimMaterial;

        protected override void Awake()
        {
            base.Awake();
            
            if (agent != null)
            {
                // Removed manual priority override to allow consistent swarm behavior
                // agent.avoidancePriority = 55;
                // Important: We control rotation manually
                agent.updateRotation = false; 
                // Fix for infinite walking: Ensure we try to reach the EXACT point
                agent.stoppingDistance = 0f; 
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (projectileSpawnPoint == null)
                projectileSpawnPoint = transform;
            
            // Setup aim line (or re-enable it reset)
            SetupAimLine();
            
            // Reset state
            moveDirection = Vector3.zero;
            waitingForAnimEvent = false;
            isShooting = false;
            if (shootCoroutine != null) StopCoroutine(shootCoroutine);
            
            // Start behavior
            SwitchState(SniperState.Idle);
        }
        
        private void SetupAimLine()
        {
            if (!showAimLine) return;
            if (aimLine != null) return; // Prevent duplicates on respawn
            
            // Create LineRenderer for aim indicator
            GameObject lineObj = new GameObject("AimLine");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;
            
            aimLine = lineObj.AddComponent<LineRenderer>();
            aimLine.positionCount = 2;
            aimLine.startWidth = aimLineWidth;
            // Don't go too thin - prevents jagged appearance
            aimLine.endWidth = Mathf.Max(aimLineWidth * 0.4f, 0.02f);
            aimLine.useWorldSpace = true;
            
            // Smoothing - reduces jagged edges
            aimLine.numCornerVertices = 4;
            aimLine.numCapVertices = 4;
            aimLine.textureMode = LineTextureMode.Stretch;
            
            // Create simple material for the line
            // Create simple material for the line
            if (_sharedAimMaterial == null)
            {
                _sharedAimMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            aimLine.material = _sharedAimMaterial;
            // Initial color (will be updated in routine)
            aimLine.startColor = aimLineStartColor;
            aimLine.endColor = new Color(aimLineStartColor.r, aimLineStartColor.g, aimLineStartColor.b, 0f);
            
            // Performance
            aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aimLine.receiveShadows = false;
            
            // Hide by default
            aimLine.enabled = false;
        }

        protected override void Update()
        {
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                // Hide aim line if interrupted
                HideAimLine();
                return;
            }

            if (target == null) return;

            // CONTACT DAMAGE CHECK (from base class logic)
            if (enableContactDamage && target != null)
            {
                if (contactDamageCooldown > 0)
                    contactDamageCooldown -= Time.deltaTime;

                float distanceToPlayer = Vector3.Distance(VisualPosition, target.position);
                
                // Debug every second
                if (debugLog && Time.frameCount % 60 == 0)
                    Debug.Log($"[SniperAI] Contact check: dist={distanceToPlayer:F2}, threshold={contactDistance:F2}, cooldown={contactDamageCooldown:F2}");
                
                if (distanceToPlayer <= contactDistance && contactDamageCooldown <= 0)
                {
                    IDamageable playerHealth = target.GetComponent<IDamageable>();
                    if (playerHealth == null)
                        playerHealth = target.GetComponentInParent<IDamageable>();
                    
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(contactDamageAmount);
                        contactDamageCooldown = contactDamageRate;
                        if (debugLog) Debug.Log($"[SniperAI] Dealt {contactDamageAmount} contact damage!");
                    }
                }
            }
            else if (!enableContactDamage && debugLog && Time.frameCount % 300 == 0)
            {
                Debug.Log("[SniperAI] Contact damage is DISABLED in Inspector!");
            }

            // State Machine Update
            switch (currentState)
            {
                case SniperState.Idle:
                    UpdateIdle();
                    break;
                case SniperState.Moving:
                    UpdateMoving();
                    break;
                case SniperState.Shooting:
                    // Logic handled in coroutine/events
                    break;
            }

            // Animation (Locomotion)
            if (animator != null)
            {
                // If moving, set speed param, otherwise 0
                float speed = (currentState == SniperState.Moving && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
                // If using 'Speed' param in base class
                if (!string.IsNullOrEmpty(speedParameter)) 
                    animator.SetFloat(speedParameter, speed);
            }
        }

        private void SwitchState(SniperState newState)
        {
            if (currentState == newState) return;
            
            currentState = newState;
            stateTimer = 0f;

            switch (newState)
            {
                case SniperState.Idle:
                    if (agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero; // Stop sliding!
                    }
                    stateTimer = 0.5f; // Brief pause before deciding next move
                    break;

                case SniperState.Moving:
                    if (agent.isOnNavMesh) agent.isStopped = false;
                    PickCardinalMove();
                    break;

                case SniperState.Shooting:
                    if (agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero; // Stop sliding!
                    }
                    // Look at player immediately at start of Aiming phase
                    LookAtTarget(); 
                    StartShootSequence();
                    break;
            }
        }

        // --- IDLE STATE ---
        private void UpdateIdle()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                // Decide: Move or Shoot?
                // For simplicity: Move -> Shoot -> Move -> Shoot loop
                // Or check cooldown?
                // Archero skeletons usually move then shoot.
                // Let's go to Moving first.
                SwitchState(SniperState.Moving);
            }
        }

        [Header("=== TIMING ===")]
        [SerializeField, Tooltip("Max time to move before forcing a shot (even if destination not reached)")]
        private float moveTimeout = 1.5f;

        // --- MOVING STATE ---
        private void UpdateMoving()
        {
            stateTimer += Time.deltaTime; // Reusing stateTimer to track duration

            // Maintain facing direction (locked to move axis)
            if (moveDirection != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 15f);
            }

            // Check if reached destination OR Timed out
            bool reachedDest = !agent.pathPending && agent.remainingDistance <= moveTolerance;
            bool timedOut = stateTimer >= moveTimeout;

            if (reachedDest || timedOut)
            {


                // Reached destination (or gave up) -> Shoot
                SwitchState(SniperState.Shooting);
            }
            
            // Failsafe: if path is invalid or stuck
            if (agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
            {
                SwitchState(SniperState.Shooting);
            }
        }

        private void PickCardinalMove()
        {
            if (!agent.isOnNavMesh)
            {
                SwitchState(SniperState.Shooting);
                return;
            }

            // Simplified Logic: Pick a RANDOM cardinal direction.
            // 0: +X (Right), 1: -X (Left), 2: +Z (Forward), 3: -Z (Back)
            
            // We try random directions until we find a valid one
            // To prevent infinite loops, we limit attempts.
            
            Vector3[] directions = new Vector3[] 
            {
                new Vector3(1, 0, 0),  // Right
                new Vector3(-1, 0, 0), // Left
                new Vector3(0, 0, 1),  // Forward relative to world
                new Vector3(0, 0, -1)  // Back relative to world
            };
            
            // Shuffle directions to try them in random order
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 temp = directions[i];
                int randomIndex = Random.Range(i, directions.Length);
                directions[i] = directions[randomIndex];
                directions[randomIndex] = temp;
            }

            // Calculate distance based on TIME
            float calculatedDistance = moveTimeout * agent.speed * 1.2f;

            foreach (Vector3 dir in directions)
            {
                Vector3 dest = transform.position + dir * calculatedDistance;
                
                // Validate if this point is on NavMesh
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(dest, out hit, calculatedDistance, UnityEngine.AI.NavMesh.AllAreas))
                {
                    // Additional check: Raycast on NavMesh to ensure we can actually walk there in a straight line?
                    // NavMeshAgent.CalculatePath can check connectivity.
                    UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                    agent.CalculatePath(hit.position, path);
                    
                    if (path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    {
                        agent.SetDestination(hit.position);
                        moveDirection = dir;
                        if (debugLog) Debug.Log($"[SniperAI] Picked Random Dir: {dir} -> {hit.position}");
                        return; // Found a valid move!
                    }
                }
            }

            // If we fall through here, NO valid move found (stuck in corner?)
            // Just face player and shoot immediately
            // if (debugLog) Debug.Log("[SniperAI] No valid move found. Skipping to Shoot.");
            moveDirection = (target.position - transform.position).normalized;
            moveDirection.y = 0;
            SwitchState(SniperState.Shooting);
        }

        // --- SHOOTING STATE ---
        private void StartShootSequence()
        {
            if (shootCoroutine != null) StopCoroutine(shootCoroutine);
            shootCoroutine = StartCoroutine(ShootRoutine());
        }

        private IEnumerator ShootRoutine()
        {
            isShooting = true;
            waitingForAnimEvent = useAnimationEvent;

            // Trigger Animation
            if (animator != null && !string.IsNullOrEmpty(shootParameter))
            {
                if (shootParameterIsBool)
                    animator.SetBool(shootParameter, true);
                else
                    animator.SetTrigger(shootParameter);
            }

            // Wait for event or fallback
            if (!useAnimationEvent)
            {
                yield return new WaitForSeconds(projectileSpawnDelay);
                SpawnProjectile();
            }

            // Wait for animation end (Safety timeout)
            float timeout = shootAnimationDuration + (useAnimationEvent ? 2.0f : 0.1f);
            float elapsed = 0f;
            
            // Wait until isShooting becomes false (via Event -> OnShootAnimationEnd)
            // or timeout
            while (elapsed < timeout && isShooting)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // If timed out, force reset
            if (isShooting)
            {
                OnShootAnimationEnd();
            }

            // Interaction finished. Go back to Idle.
            SwitchState(SniperState.Idle);
        }

        public void FireProjectileFromEvent()
        {
            // Hide aim line when firing
            HideAimLine();
            
            // Just check if we are in shooting state, don't strict check 'waitingForAnimEvent' 
            // incase of race conditions where coroutine hasn't set it yet or it was cleared.
            // But we do want to avoid double firing if possible. 
            // Let's trust the event.
            if (currentState == SniperState.Shooting)
            {
                waitingForAnimEvent = false;
                SpawnProjectile();
            }
            else
            {
                 // Debug.LogWarning($"[SniperAI] Event fired but state is {currentState} (Expected Shooting)");
                 // Fallback: If we are not in Shooting, maybe we should fire anyway? 
                 // No, that risks firing while walking.
            }
        }

        public void OnShootAnimationEnd()
        {
            if (animator != null && !string.IsNullOrEmpty(shootParameter) && shootParameterIsBool)
            {
                animator.SetBool(shootParameter, false);
            }
            isShooting = false;
            waitingForAnimEvent = false;
        }

        // Method to look at player instantly - can be called via Animation Event "FacePlayer" if needed
        public void FacePlayer()
        {
            if (target != null)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(dir);
            }
            
            // Show aim line when facing player (aiming phase)
            ShowAimLine();
        }
        
        // --- AIM LINE CONTROL ---
        private void ShowAimLine()
        {
            if (!showAimLine || aimLine == null) return;
            aimLine.enabled = true;
            StartCoroutine(UpdateAimLineRoutine());
            
            // Play aim sound
            if (aimSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(aimSound, transform.position);
        }
        
        private void HideAimLine()
        {
            if (aimLine == null) return;
            aimLine.enabled = false;
        }
        
        private System.Collections.IEnumerator UpdateAimLineRoutine()
        {
            float elapsed = 0f;
            // Timeout: 22 frames at 30fps = ~0.73 seconds
            float maxAimTime = 0.75f;
            
            while (aimLine != null && aimLine.enabled && target != null && elapsed < maxAimTime)
            {
                // Check for interruption (frozen, dead, state changed)
                if (health != null && (health.IsDead || health.IsFrozen))
                {
                    HideAimLine();
                    yield break;
                }
                
                // If we left shooting state somehow, hide line
                if (currentState != SniperState.Shooting)
                {
                    HideAimLine();
                    yield break;
                }
                
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / aimDuration);
                
                // Lerp color from start (white) to end (red)
                Color currentColor = Color.Lerp(aimLineStartColor, aimLineEndColor, t);
                aimLine.startColor = currentColor;
                aimLine.endColor = new Color(currentColor.r, currentColor.g, currentColor.b, 0f);
                
                // Update line positions
                Vector3 startPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
                Vector3 direction = (target.position - startPos).normalized;
                direction.y = 0; // Keep flat
                
                Vector3 endPos = startPos + direction * aimLineLength;
                
                aimLine.SetPosition(0, startPos);
                aimLine.SetPosition(1, endPos);
                
                yield return null;
            }
            
            // Auto-hide on timeout
            HideAimLine();
        }

        private void SpawnProjectile()
        {
            if (projectilePrefab == null || target == null) return;

            // Play shoot sound
            if (shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPosition(shootSound, transform.position);

            Vector3 spawnPos = projectileSpawnPoint.position;
            // Keep trajectory flat
            Vector3 targetPos = target.position;
            targetPos.y = spawnPos.y;
            Vector3 direction = (targetPos - spawnPos).normalized;

            // Use object pool instead of Instantiate for zero-allocation shooting
            GameObject proj = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.Get(projectilePrefab, spawnPos, Quaternion.LookRotation(direction))
                : Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(direction));
            
            EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
            if (ep != null)
            {
                ep.SetDirection(direction);
                if (projectileSpeed > 0) ep.SetSpeed(projectileSpeed);
                if (projectileDamage > 0) ep.SetDamage(projectileDamage);
                
                // Set projectile lifetime based on attack range
                // Pass the speed so calculation is correct
                if (attackRange > 0)
                {
                    ep.SetMaxRange(attackRange, projectileSpeed);
                }
            }
        }

        protected override void OnAttack() { } // Not used
    }
}
