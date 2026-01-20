using UnityEngine;
using Audio;

namespace Boss
{
    /// <summary>
    /// Relay script for Titan animation events.
    /// Attach this to the same GameObject that has the Animator.
    /// Animation events call methods on this script, which forwards to attack components.
    /// ALL sounds are triggered via animation events for precise timing.
    /// </summary>
    public class TitanAnimationRelay : MonoBehaviour
    {
        [Header("Attack References")]
        [SerializeField] private TitanFistAttack fistAttack;
        [SerializeField] private TitanSummonAttack summonAttack;
        [SerializeField] private TitanCoreBlast coreBlast;
        
        [Header("Shockwave VFX")]
        [SerializeField, Tooltip("Shockwave prefab with ShockwaveVFX component")]
        private GameObject shockwavePrefab;
        
        [SerializeField, Tooltip("Transform where shockwave spawns (fist impact point)")]
        private Transform shockwaveSpawnPoint;
        
        [Header("Boss Barriers")]
        [SerializeField, Tooltip("Forward barrier to disable after boss death")]
        private GameObject forwardBarrier;
        
        [Header("Volcano Emission Fade")]
        [SerializeField, Tooltip("Volcano material to fade emission on death")]
        private Material volcanoMaterial;
        
        [SerializeField, Tooltip("Duration of emission fade")]
        private float volcanoFadeDuration = 2f;
        
        [SerializeField, Tooltip("Starting emission color"), ColorUsage(true, true)]
        private Color volcanoFadeStartColor = Color.red;
        
        [SerializeField, Tooltip("Target emission color at end of fade"), ColorUsage(true, true)]
        private Color volcanoFadeEndColor = Color.black;
        
        [Header("=== SOUND EFFECTS (All Animation Event Triggered) ===")]
        [SerializeField, Tooltip("Fist attack call/windup sound")]
        private SoundEvent fistCallSound;
        
        [SerializeField, Tooltip("Fist impact sound (when fist hits ground)")]
        private SoundEvent fistImpactSound;
        
        [SerializeField, Tooltip("Ground crack sound (during fist attack)")]
        private SoundEvent groundCrackSound;
        
        [SerializeField, Tooltip("Summon call sound")]
        private SoundEvent summonCallSound;
        
        [SerializeField, Tooltip("Core blast raiser/charge sound")]
        private SoundEvent coreBlastRaiserSound;
        
        [SerializeField, Tooltip("Core blast fire sound")]
        private SoundEvent coreBlastBlastSound;
        
        [SerializeField, Tooltip("Rage sound")]
        private SoundEvent rageSound;
        
        [SerializeField, Tooltip("Death sound")]
        private SoundEvent deathSound;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private void Start()
        {
            // Apply volcano start emission on game start
            if (volcanoMaterial != null && volcanoMaterial.HasProperty("_EmissionColor"))
            {
                volcanoMaterial.EnableKeyword("_EMISSION");
                volcanoMaterial.SetColor("_EmissionColor", volcanoFadeStartColor);
                if (debugLog) Debug.Log("[TitanAnimationRelay] Applied volcano start emission");
            }
        }
        
        #region Index Animation Events
        /// <summary>
        /// Set animator Index to 0 (Fist). Call via animation event.
        /// </summary>
        public void SetIndex0()
        {
            if (TitanBossController.Instance != null && TitanBossController.Instance.Animator != null)
            {
                TitanBossController.Instance.Animator.SetInteger("Index", 0);
                if (debugLog) Debug.Log("[TitanAnimationRelay] SetIndex0 (Fist)");
            }
        }
        
        /// <summary>
        /// Set animator Index to 1 (Summon). Call via animation event.
        /// </summary>
        public void SetIndex1()
        {
            if (TitanBossController.Instance != null && TitanBossController.Instance.Animator != null)
            {
                TitanBossController.Instance.Animator.SetInteger("Index", 1);
                if (debugLog) Debug.Log("[TitanAnimationRelay] SetIndex1 (Summon)");
            }
        }
        
        /// <summary>
        /// Set animator Index to 2 (CoreBlast). Call via animation event.
        /// </summary>
        public void SetIndex2()
        {
            if (TitanBossController.Instance != null && TitanBossController.Instance.Animator != null)
            {
                TitanBossController.Instance.Animator.SetInteger("Index", 2);
                if (debugLog) Debug.Log("[TitanAnimationRelay] SetIndex2 (CoreBlast)");
            }
        }
        #endregion
        
        #region Attack Execution Events
        /// <summary>
        /// Called by animation event on Fist animation impact moment.
        /// </summary>
        public void OnFistImpact()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnFistImpact event received");
            if (fistAttack != null) fistAttack.Execute();
        }
        
        /// <summary>
        /// Called by animation event on Summon animation.
        /// </summary>
        public void OnSummonPulse()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnSummonPulse event received");
            if (summonAttack != null) summonAttack.Execute();
        }
        
        /// <summary>
        /// Called by animation event on CoreBlast animation.
        /// </summary>
        public void OnCoreBlast()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnCoreBlast event received");
            if (coreBlast != null) coreBlast.Execute();
        }
        
        /// <summary>
        /// Alternative event names for flexibility.
        /// </summary>
        public void TriggerFist() => OnFistImpact();
        public void TriggerSummon() => OnSummonPulse();
        public void TriggerCoreBlast() => OnCoreBlast();
        #endregion
        
        #region Sound Events (All 7 Sounds)
        /// <summary>
        /// Play Fist Call sound (windup).
        /// </summary>
        public void OnPlayFistCall()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlayFistCall");
            if (fistCallSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(fistCallSound);
        }
        
        /// <summary>
        /// Play Fist Impact sound.
        /// </summary>
        public void OnPlayFistImpact()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlayFistImpact");
            if (fistImpactSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(fistImpactSound);
        }
        
        /// <summary>
        /// Play Ground Crack sound.
        /// </summary>
        public void OnPlayGroundCrack()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlayGroundCrack");
            if (groundCrackSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(groundCrackSound);
        }
        
        /// <summary>
        /// Play Summon Call sound.
        /// </summary>
        public void OnPlaySummonCall()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlaySummonCall");
            if (summonCallSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(summonCallSound);
        }
        
        /// <summary>
        /// Play Core Blast Raiser sound (charge up).
        /// </summary>
        public void OnPlayCoreBlastRaiser()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlayCoreBlastRaiser");
            if (coreBlastRaiserSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(coreBlastRaiserSound);
        }
        
        /// <summary>
        /// Play Core Blast Blast sound (fire).
        /// </summary>
        public void OnPlayCoreBlastBlast()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlayCoreBlastBlast");
            if (coreBlastBlastSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(coreBlastBlastSound);
        }
        
        /// <summary>
        /// Play Rage sound.
        /// </summary>
        public void OnPlayRage()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlayRage");
            if (rageSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(rageSound);
        }
        
        /// <summary>
        /// Play Death sound.
        /// </summary>
        public void OnPlayDeath()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnPlayDeath");
            if (deathSound != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(deathSound);
        }
        #endregion
        
        #region Other Animation Events
        /// <summary>
        /// Called by animation event when Rage animation completes and hands are repaired.
        /// </summary>
        public void OnHandsRepaired()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnHandsRepaired event received");
            if (TitanBossController.Instance != null)
                TitanBossController.Instance.RepairHands();
        }
        
        /// <summary>
        /// Called by animation event at the end of the Death animation.
        /// </summary>
        public void OnBossDeathComplete()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnBossDeathComplete event received");
            if (TitanBossController.Instance != null)
                TitanBossController.Instance.EndBossFight();
        }
        
        /// <summary>
        /// Called by animation event to disable the forward barrier (allows player to proceed).
        /// </summary>
        public void OnDisableForwardBarrier()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnDisableForwardBarrier event received");
            if (forwardBarrier != null)
                forwardBarrier.SetActive(false);
        }
        
        /// <summary>
        /// Called by animation event to start fading volcano emission to 0.
        /// </summary>
        public void OnFadeVolcanoEmission()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnFadeVolcanoEmission event received");
            if (volcanoMaterial != null)
                StartCoroutine(FadeVolcanoEmissionRoutine());
        }
        
        private System.Collections.IEnumerator FadeVolcanoEmissionRoutine()
        {
            if (!volcanoMaterial.HasProperty("_EmissionColor"))
            {
                Debug.LogWarning("[TitanAnimationRelay] Volcano material has no _EmissionColor property");
                yield break;
            }
            
            // Enable emission keyword
            volcanoMaterial.EnableKeyword("_EMISSION");
            
            float elapsed = 0f;
            while (elapsed < volcanoFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / volcanoFadeDuration;
                
                Color currentEmission = Color.Lerp(volcanoFadeStartColor, volcanoFadeEndColor, t);
                volcanoMaterial.SetColor("_EmissionColor", currentEmission);
                
                yield return null;
            }
            
            // Ensure final value
            volcanoMaterial.SetColor("_EmissionColor", volcanoFadeEndColor);
            
            // Only disable keyword if fading to complete black
            if (volcanoFadeEndColor == Color.black)
                volcanoMaterial.DisableKeyword("_EMISSION");
            
            if (debugLog) Debug.Log("[TitanAnimationRelay] Volcano emission fade complete");
        }
        
        /// <summary>
        /// Called by animation event to spawn shockwave VFX.
        /// </summary>
        public void OnSpawnShockwave()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnSpawnShockwave event received");
            
            if (shockwavePrefab == null)
            {
                Debug.LogWarning("[TitanAnimationRelay] Shockwave prefab not assigned!");
                return;
            }
            
            Vector3 spawnPos = shockwaveSpawnPoint != null ? 
                shockwaveSpawnPoint.position : 
                transform.position;
            
            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
            Instantiate(shockwavePrefab, spawnPos, rotation);
            
            if (debugLog) Debug.Log($"[TitanAnimationRelay] Shockwave spawned at {spawnPos}");
        }
        #endregion
    }
}
