using UnityEngine;

namespace Boss
{
    /// <summary>
    /// Relay script for Titan animation events.
    /// Attach this to the same GameObject that has the Animator.
    /// Animation events call methods on this script, which forwards to attack components.
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
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        /// <summary>
        /// Called by animation event on Fist animation.
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
        
        /// <summary>
        /// Called by animation event when Rage animation completes and hands are repaired.
        /// Restores both hands to full health with smooth tint fade.
        /// </summary>
        public void OnHandsRepaired()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnHandsRepaired event received");
            
            if (TitanBossController.Instance != null)
            {
                TitanBossController.Instance.RepairHands();
            }
        }
        
        /// <summary>
        /// Called by animation event at the end of the Death animation.
        /// Manually ends the boss fight and exits camera mode.
        /// </summary>
        public void OnBossDeathComplete()
        {
            if (debugLog) Debug.Log("[TitanAnimationRelay] OnBossDeathComplete event received");
            
            if (TitanBossController.Instance != null)
            {
                TitanBossController.Instance.EndBossFight();
            }
        }
        
        /// <summary>
        /// Called by animation event to spawn shockwave VFX at the configured point.
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
            
            // Spawn flat on ground (rotated 90 degrees on X)
            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
            
            Instantiate(shockwavePrefab, spawnPos, rotation);
            
            if (debugLog) Debug.Log($"[TitanAnimationRelay] Shockwave spawned at {spawnPos}");
        }
    }
}
