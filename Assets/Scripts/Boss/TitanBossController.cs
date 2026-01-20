using UnityEngine;
using System.Collections;
using Audio;

namespace Boss
{
    /// <summary>
    /// Titan boss states.
    /// </summary>
    public enum TitanState
    {
        Idle,
        Attacking,
        Rage,
        Death
    }
    
    /// <summary>
    /// Attack types for the Titan.
    /// </summary>
    public enum TitanAttackType
    {
        Fist,       // Right hand - ground crack
        Summon,     // Left hand - spawn chasers
        CoreBlast   // Core - meteors
    }
    
    /// <summary>
    /// Main controller for the Titan final boss.
    /// Manages state machine, attack selection, rage mechanics, and death.
    /// </summary>
    public class TitanBossController : MonoBehaviour
    {
        public static TitanBossController Instance { get; private set; }
        
        [Header("=== HEALTH PARTS ===")]
        [SerializeField] private TitanHealth rightHandHealth;
        [SerializeField] private TitanHealth leftHandHealth;
        [SerializeField] private TitanHealth coreHealth;
        
        [Header("=== TARGET SOCKETS ===")]
        [SerializeField] private TitanTargetSocket rightHandSocket;
        [SerializeField] private TitanTargetSocket leftHandSocket;
        [SerializeField] private TitanTargetSocket coreSocket;
        
        [Header("=== ATTACK COMPONENTS ===")]
        [SerializeField] private TitanFistAttack fistAttack;
        [SerializeField] private TitanSummonAttack summonAttack;
        [SerializeField] private TitanCoreBlast coreBlast;
        
        [Header("=== TIMING ===")]
        [SerializeField, Tooltip("Time between attacks")]
        private float attackCooldown = 4f;
        [SerializeField, Tooltip("Initial delay before first attack")]
        private float initialDelay = 2f;
        [SerializeField, Tooltip("Delay during rage before attacking again")]
        private float rageRecoveryTime = 3f;
        
        [Header("=== ATTACK DURATIONS ===")]
        [SerializeField, Tooltip("How long to wait during Fist attack animation")]
        private float fistAttackDuration = 3f;
        [SerializeField, Tooltip("How long to wait during Summon attack animation")]
        private float summonAttackDuration = 3f;
        [SerializeField, Tooltip("How long to wait during Core Blast attack animation")]
        private float coreBlastAttackDuration = 4f;
        
        [Header("=== ANIMATION ===")]
        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("Name of integer parameter: 0=Fist/Rage, 1=Summon/Rage, 2=CoreBlast")]
        private string attackIndexParam = "Index";
        [SerializeField, Tooltip("Bool param: true=right hand healthy, false=destroyed")]
        private string rightWellParam = "RightWell";
        [SerializeField, Tooltip("Bool param: true=left hand healthy, false=destroyed")]
        private string leftWellParam = "LeftWell";
        [SerializeField, Tooltip("Trigger param for death animation")]
        private string deathParam = "Death";
        
        // NOTE: Sound effects are now handled via animation events in TitanAnimationRelay
        
        [Header("=== CAMERA ===")]
        [SerializeField, Tooltip("Arena center for boss camera mode")]
        private Transform arenaCenterPoint;

        [Header("=== ENVIRONMENT ===")]
        [SerializeField] private Hazards.HazardZoneMeteors hazardZone;
        [SerializeField, Tooltip("Speed to set hazard zone when boss fight starts (usually 0)")]
        private float bossZoneSpeed = 0f;
        [SerializeField] private float normalZoneSpeed = 0.55f;
        [SerializeField, Tooltip("Boundary object to activate when fight starts")]
        private GameObject bossArenaBoundary;
        [SerializeField, Tooltip("Delay before activating boundary to ensure player is inside")]
        private float boundaryActivationDelay = 1.0f;
        
        [Header("=== DEBUG ===")]
        [SerializeField] private bool debugLog = true;
        [SerializeField] private TitanState currentState = TitanState.Idle;

        private void Start()
        {
            if (hazardZone == null)
                hazardZone = FindFirstObjectByType<Hazards.HazardZoneMeteors>();
        }

        // State
        private TitanAttackType currentAttack;
        
        // ... (StartBossFight logic below)

        /// <summary>
        /// Starts the boss fight sequence.
        /// </summary>
        public void StartBossFight()
        {
            if (isBossFightActive) return;
            
            isBossFightActive = true;
            currentState = TitanState.Idle;
            attackTimer = initialDelay;
            
            // Activate Boundary with delay
            if (bossArenaBoundary != null)
            {
                StartCoroutine(ActivateBoundaryRoutine());
            }

            // Pause Hazard Zone (Immediate)
            if (hazardZone != null)
            {
                hazardZone.SetSpeed(bossZoneSpeed);
                if (debugLog) Debug.Log($"[TitanBossController] Hazard Zone speed set to {bossZoneSpeed}");
            }
            
            // Initialize Well bools - both hands start healthy
            if (animator != null)
            {
                animator.SetBool(rightWellHash, rightHandHealth == null || !rightHandHealth.IsDestroyed);
                animator.SetBool(leftWellHash, leftHandHealth == null || !leftHandHealth.IsDestroyed);
                if (debugLog) Debug.Log($"[TitanBossController] Initialized Wells: RightWell={!rightHandHealth?.IsDestroyed ?? true}, LeftWell={!leftHandHealth?.IsDestroyed ?? true}");
            }
            
            // Activate camera boss mode
            if (CameraBossMode.Instance != null && arenaCenterPoint != null)
            {
                CameraBossMode.Instance.EnterBossMode(arenaCenterPoint);
            }
            
            OnBossFightStarted?.Invoke();
            
            if (debugLog)
                Debug.Log("[TitanBossController] Boss fight started!");
            
            StartCoroutine(BossFightLoop());
        }

        private IEnumerator ActivateBoundaryRoutine()
        {
            if (boundaryActivationDelay > 0)
                yield return new WaitForSeconds(boundaryActivationDelay);
                
            if (bossArenaBoundary != null && isBossFightActive)
            {
                bossArenaBoundary.SetActive(true);
                if (debugLog) Debug.Log("[TitanBossController] Boss Arena Boundary ACTIVATED");
            }
        }

        // ... (EndBossFight logic below)

        /// <summary>
        /// Explicitly ends the boss fight (exits camera mode).
        /// Call this via Animation Event at the end of death animation.
        /// </summary>
        public void EndBossFight()
        {
            if (!isBossFightActive) return;
            isBossFightActive = false;
            
            if (debugLog) Debug.Log("[TitanBossController] EndBossFight called - exiting boss mode");
            
            // Stop all targeting (Delay this until here so camera stays locked during death anim)
            if (rightHandSocket != null) rightHandSocket.SetTargetable(false);
            if (leftHandSocket != null) leftHandSocket.SetTargetable(false);
            if (coreSocket != null) coreSocket.SetTargetable(false);
            
            // Resume Hazard Zone
            if (hazardZone != null)
            {
                hazardZone.SetSpeed(normalZoneSpeed);
                if (debugLog) Debug.Log($"[TitanBossController] Hazard Zone speed resumed to {normalZoneSpeed}");
            }

            // Explicitly exit camera boss mode
            if (CameraBossMode.Instance != null)
            {
                CameraBossMode.Instance.ExitBossMode();
            }
            
            OnBossDefeated?.Invoke();
        }
        private TitanAttackType lastAttack = TitanAttackType.CoreBlast;
        private int currentAttackIndex = 0; // 0=Fist, 1=Summon, 2=Blast, cycles
        private float attackTimer;
        private bool isBossFightActive = false;
        private int indexHash;
        private int rightWellHash;
        private int leftWellHash;
        private int deathHash;
        
        // Events
        public event System.Action OnBossFightStarted;
        public event System.Action OnBossDefeated;
        public event System.Action<TitanAttackType> OnAttackStarted;
        public event System.Action OnRageStarted;
        
        public TitanState CurrentState => currentState;
        public Animator Animator => animator;
        public bool IsBossFightActive => isBossFightActive;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Cache animation hashes
            indexHash = Animator.StringToHash(attackIndexParam);
            rightWellHash = Animator.StringToHash(rightWellParam);
            leftWellHash = Animator.StringToHash(leftWellParam);
            deathHash = Animator.StringToHash(deathParam);
            
            Debug.Log($"[TitanBossController] Awake - rightHandHealth: {rightHandHealth != null}, leftHandHealth: {leftHandHealth != null}, coreHealth: {coreHealth != null}, animator: {animator != null}");
            
            // Subscribe to health events
            if (coreHealth != null)
                coreHealth.OnDeath.AddListener(OnCoreDestroyed);
            
            // Subscribe to hand health for Well status
            if (rightHandHealth != null)
            {
                rightHandHealth.OnDeath.AddListener(OnRightHandDestroyed);
                rightHandHealth.OnDamage.AddListener(ApplySharedDamageToCore);
                Debug.Log("[TitanBossController] Subscribed to rightHandHealth events");
            }
            else
            {
                Debug.LogError("[TitanBossController] rightHandHealth is NOT ASSIGNED!");
            }
            
            if (leftHandHealth != null)
            {
                leftHandHealth.OnDeath.AddListener(OnLeftHandDestroyed);
                leftHandHealth.OnDamage.AddListener(ApplySharedDamageToCore);
                Debug.Log("[TitanBossController] Subscribed to leftHandHealth events");
            }
            else
            {
                Debug.LogError("[TitanBossController] leftHandHealth is NOT ASSIGNED!");
            }
        }
        
        private void OnDestroy()
        {
            if (coreHealth != null)
                coreHealth.OnDeath.RemoveListener(OnCoreDestroyed);
                
            if (rightHandHealth != null)
            {
                rightHandHealth.OnDeath.RemoveListener(OnRightHandDestroyed);
                rightHandHealth.OnDamage.RemoveListener(ApplySharedDamageToCore);
            }
                
            if (leftHandHealth != null)
            {
                leftHandHealth.OnDeath.RemoveListener(OnLeftHandDestroyed);
                leftHandHealth.OnDamage.RemoveListener(ApplySharedDamageToCore);
            }
        }
        

        

        
        /// <summary>
        /// Main boss fight loop.
        /// </summary>
        private IEnumerator BossFightLoop()
        {
            if (debugLog)
                Debug.Log("[TitanBossController] Boss fight loop started!");
            
            // Initial delay (use realtime to avoid time-scale issues)
            yield return new WaitForSecondsRealtime(initialDelay);
            
            if (debugLog)
                Debug.Log("[TitanBossController] Initial delay complete, entering attack loop...");
            
            while (isBossFightActive && currentState != TitanState.Death)
            {
                if (debugLog)
                    Debug.Log($"[TitanBossController] Loop iteration start. State: {currentState}, Active: {isBossFightActive}");
                
                // Select and perform attack
                yield return StartCoroutine(PerformNextAttackSafe());
                
                if (debugLog)
                    Debug.Log("[TitanBossController] Attack coroutine returned, waiting for cooldown...");
                
                // Wait for cooldown (use realtime)
                yield return new WaitForSecondsRealtime(attackCooldown);
                
                if (debugLog)
                    Debug.Log("[TitanBossController] Cooldown complete, selecting next attack...");
            }
            
            if (debugLog)
                Debug.Log($"[TitanBossController] Loop exited. State: {currentState}, Active: {isBossFightActive}");
        }
        
        /// <summary>
        /// Safe wrapper for PerformNextAttack that catches exceptions.
        /// </summary>
        private IEnumerator PerformNextAttackSafe()
        {
            bool completed = false;
            System.Exception caughtException = null;
            
            // We can't use try-catch directly in iterator, so we track manually
            var attackRoutine = PerformNextAttack();
            while (true)
            {
                try
                {
                    if (!attackRoutine.MoveNext())
                    {
                        completed = true;
                        break;
                    }
                }
                catch (System.Exception ex)
                {
                    caughtException = ex;
                    break;
                }
                yield return attackRoutine.Current;
            }
            
            if (caughtException != null)
            {
                Debug.LogError($"[TitanBossController] Exception in attack: {caughtException.Message}\n{caughtException.StackTrace}");
            }
            
            if (debugLog && completed)
                Debug.Log("[TitanBossController] PerformNextAttack completed successfully");
        }
        
        /// <summary>
        /// Selects and performs the next attack in the cycle.
        /// Cycle: Fist (0) → Summon (1) → Blast (2) → Fist (0) → ...
        /// Rage is handled automatically in PerformAttack based on Well status.
        /// </summary>
        private IEnumerator PerformNextAttack()
        {
            if (currentState == TitanState.Death) yield break;
            
            // Get current attack from index (fixed cycle, not random)
            currentAttack = GetAttackFromIndex(currentAttackIndex);
            
            if (debugLog)
                Debug.Log($"[TitanBossController] Cycle attack [{currentAttackIndex}]: {currentAttack}");
            
            // Perform the attack - rage is handled automatically if Well is false
            yield return StartCoroutine(PerformAttack(currentAttack));
            
            // Advance cycle for next attack
            currentAttackIndex = (currentAttackIndex + 1) % 3;
        }
        
        /// <summary>
        /// Gets the attack type from the cycle index.
        /// </summary>
        private TitanAttackType GetAttackFromIndex(int index)
        {
            switch (index)
            {
                case 0: return TitanAttackType.Fist;
                case 1: return TitanAttackType.Summon;
                case 2: return TitanAttackType.CoreBlast;
                default: return TitanAttackType.Fist;
            }
        }
        
        /// <summary>
        /// Performs the specified attack. Sets Index and lets Animator decide Fist/Summon or Rage.
        /// Index: 0=Fist/Rage, 1=Summon/Rage, 2=CoreBlast
        /// </summary>
        private IEnumerator PerformAttack(TitanAttackType attackType)
        {
            if (currentState == TitanState.Death) yield break;
            
            currentState = TitanState.Attacking;
            lastAttack = attackType;
            OnAttackStarted?.Invoke(attackType);
            
            // NOTE: Index is now controlled ONLY by animation events (SetIndex0/1/2)
            // via TitanAnimationRelay - no code-based index changes here
            
            float waitDuration = 2f;
            bool isRageNeeded = false;
            
            switch (attackType)
            {
                case TitanAttackType.Fist:
                    isRageNeeded = rightHandHealth != null && rightHandHealth.IsDestroyed;
                    // Sound triggered by animation event via TitanAnimationRelay.OnPlayFistCall
                    waitDuration = fistAttackDuration;
                    break;
                    
                case TitanAttackType.Summon:
                    isRageNeeded = leftHandHealth != null && leftHandHealth.IsDestroyed;
                    // Sound triggered by animation event via TitanAnimationRelay.OnPlaySummonCall
                    waitDuration = summonAttackDuration;
                    break;
                    
                case TitanAttackType.CoreBlast:
                    // Sound triggered by animation event via TitanAnimationRelay.OnPlayCoreBlastRaiser
                    waitDuration = coreBlastAttackDuration;
                    break;
            }
            
            // If rage is needed, set targeting (sound triggered by animation event)
            if (isRageNeeded)
            {
                // Sound triggered by animation event via TitanAnimationRelay.OnPlayRage
                waitDuration = rageRecoveryTime;
                currentState = TitanState.Rage;
                OnRageStarted?.Invoke();
                SetRageTargeting(true);
            }
            
            // Wait for animation
            yield return new WaitForSecondsRealtime(waitDuration);
            
            // CRITICAL CHECK: ensure we didn't die while waiting
            if (currentState == TitanState.Death)
            {
                DisableAllParts();
                yield break;
            }
            
            // If rage was performed, heal the hand and set Well = true
            if (isRageNeeded)
            {
                if (attackType == TitanAttackType.Fist && rightHandHealth != null)
                {
                    rightHandHealth.FullHeal();
                    if (animator != null) animator.SetBool(rightWellHash, true);
                    if (debugLog) Debug.Log("[TitanBossController] Right hand healed, RightWell = true");
                }
                else if (attackType == TitanAttackType.Summon && leftHandHealth != null)
                {
                    leftHandHealth.FullHeal();
                    if (animator != null) animator.SetBool(leftWellHash, true);
                    if (debugLog) Debug.Log("[TitanBossController] Left hand healed, LeftWell = true");
                }
                SetRageTargeting(false);
            }
            
            // Note: Cycle index is incremented in PerformNextAttack after this coroutine returns
            currentState = TitanState.Attacking;
        }
        
        /// <summary>
        /// Called when right hand is destroyed - sets RightWell = false.
        /// </summary>
        private void OnRightHandDestroyed()
        {
            if (currentState == TitanState.Death) return;
            
            Debug.Log($"[TitanBossController] OnRightHandDestroyed called! Animator: {animator != null}, rightWellHash: {rightWellHash}");
            
            if (animator != null)
            {
                animator.SetBool(rightWellHash, false);
                Debug.Log($"[TitanBossController] Set RightWell = false (hash: {rightWellHash})");
            }
            else
            {
                Debug.LogError("[TitanBossController] Animator is NULL! Cannot set RightWell");
            }
            
            // Disable targeting for this hand
            if (rightHandSocket != null) rightHandSocket.SetTargetable(false);
        }
        
        /// <summary>
        /// Called when left hand is destroyed - sets LeftWell = false.
        /// </summary>
        private void OnLeftHandDestroyed()
        {
            if (currentState == TitanState.Death) return;
            
            Debug.Log($"[TitanBossController] OnLeftHandDestroyed called! Animator: {animator != null}, leftWellHash: {leftWellHash}");
            
            if (animator != null)
            {
                animator.SetBool(leftWellHash, false);
                Debug.Log($"[TitanBossController] Set LeftWell = false (hash: {leftWellHash})");
            }
            else
            {
                Debug.LogError("[TitanBossController] Animator is NULL! Cannot set LeftWell");
            }
            
            // Disable targeting for this hand
            if (leftHandSocket != null) leftHandSocket.SetTargetable(false);
        }
        
        /// <summary>
        /// Called by animation event when rage animation completes.
        /// Repairs and heals both hands.
        /// </summary>
        public void RepairHands()
        {
            if (currentState == TitanState.Death) return;
            
            Debug.Log("[TitanBossController] RepairHands called");
            
            // Heal both hands
            if (rightHandHealth != null)
            {
                rightHandHealth.RepairWithFade();
                if (animator != null)
                    animator.SetBool(rightWellHash, true);
            }
            
            if (leftHandHealth != null)
            {
                leftHandHealth.RepairWithFade();
                if (animator != null)
                    animator.SetBool(leftWellHash, true);
            }
            
            // Re-enable hand targeting
            if (rightHandSocket != null) rightHandSocket.SetTargetable(true);
            if (leftHandSocket != null) leftHandSocket.SetTargetable(true);
            
            Debug.Log("[TitanBossController] Both hands repaired, Well bools set to true");
        }
        
        // PerformRage is now handled within PerformAttack based on Well status
        
        /// <summary>
        /// Sets targeting for rage mode.
        /// During rage, hands are disabled so only core can be attacked.
        /// </summary>
        private void SetRageTargeting(bool isRaging)
        {
            if (isRaging)
            {
                // Disable hands during rage - core remains as-is (player can target it if in range)
                if (rightHandSocket != null) rightHandSocket.SetTargetable(false);
                if (leftHandSocket != null) leftHandSocket.SetTargetable(false);
                // Core stays targetable (don't change it)
            }
            else
            {
                // Restore hand targeting after rage
                if (rightHandSocket != null) rightHandSocket.SetTargetable(true);
                if (leftHandSocket != null) leftHandSocket.SetTargetable(true);
            }
        }
        
        /// <summary>
        /// Called when the core is destroyed - triggers death.
        /// </summary>
        private void OnCoreDestroyed()
        {
            StartCoroutine(PerformDeath());
        }
        
        /// <summary>
        /// Performs death sequence.
        /// </summary>
        private IEnumerator PerformDeath()
        {
            if (currentState == TitanState.Death) yield break;
            
            currentState = TitanState.Death;
            
            if (debugLog)
                Debug.Log($"[TitanBossController] TITAN DEFEATED! Triggering death anim: {deathParam}");
            
            // Trigger Death Animation
            if (animator != null)
            {
                animator.SetTrigger(deathHash);
            }
            
            // Wait for death animation to play out (5 seconds safe duration)
            // During this time, the boss is untargetable via normal means (if we want),
            // BUT for now we keep physics active so the animation (if root motion or ragdoll) works.
            // ACTUALLY: The user wants targetability disabled immediately for CORE death.
            // We will do a "Soft Disable" first - cancel logic, stop targeting search.
            // But we MUST NOT disable the collider if it affects the animation/physics ground check.
            
            // However, since we are using "DisableAllParts", which calls "DisableTargeting" on TitanHealth...
            // Let's modify DisableTargeting in TitanHealth to be safer, OR just wait here.
            
            // Strategy: 
            // 1. Immediately stop the boss logic (currentState = Death handles this).
            // 2. Play animation.
            // 3. Wait for animation to finish.
            // 4. THEN cleanup physics/layers.
            
            yield return new WaitForSeconds(5f); // Wait for long death animation
            
            // Permanently disable all parts (Collision, Tags, Layers)
            DisableAllParts();
            
            // Finalize
            EndBossFight();
        }
        
        private void DisableAllParts()
        {
            if (debugLog) Debug.Log("[TitanBossController] Disabling all Titan parts permanently.");
            
            if (rightHandHealth != null) rightHandHealth.DisableTargeting();
            if (leftHandHealth != null) leftHandHealth.DisableTargeting();
            if (coreHealth != null) coreHealth.DisableTargeting();
        }

        /// <summary>
        /// Applies 25% of damage taken by hands to the core.
        /// </summary>
        private void ApplySharedDamageToCore(int damage)
        {
            if (currentState == TitanState.Death) return;
            if (coreHealth == null || coreHealth.IsDestroyed) return;
            
            // Prevent recursive loop if core somehow triggers hand damage (unlikely but safe)
            // Core damage logic is separate.
            
            int sharedDamage = damage / 4;
            if (sharedDamage > 0)
            {
                coreHealth.TakeDamage(sharedDamage);
            }
        }
        
        #region Debug
        [ContextMenu("Debug: Start Boss Fight")]
        private void DebugStartFight() => StartBossFight();
        
        [ContextMenu("Debug: Force Fist Attack")]
        private void DebugFistAttack() => StartCoroutine(PerformAttack(TitanAttackType.Fist));
        
        [ContextMenu("Debug: Force Summon Attack")]
        private void DebugSummonAttack() => StartCoroutine(PerformAttack(TitanAttackType.Summon));
        
        [ContextMenu("Debug: Force Core Blast")]
        private void DebugCoreBlast() => StartCoroutine(PerformAttack(TitanAttackType.CoreBlast));
        
        [ContextMenu("Debug: Force Rage (Fist)")]
        private void DebugRage()
        {
            // Force right hand destroyed to trigger rage on next fist attack
            if (rightHandHealth != null && animator != null)
            {
                animator.SetBool(rightWellHash, false);
                StartCoroutine(PerformAttack(TitanAttackType.Fist));
            }
        }
        
        [ContextMenu("Debug: Kill Core")]
        private void DebugKillCore()
        {
            if (coreHealth != null)
                coreHealth.TakeDamage(coreHealth.CurrentHealth);
        }
        #endregion
    }
}

