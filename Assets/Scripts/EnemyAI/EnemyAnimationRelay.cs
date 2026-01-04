using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// Relay script for animation events.
    /// Place this on the same GameObject as the Animator (usually the model child).
    /// Animation events call methods on this script, which forwards to the AI.
    /// </summary>
    public class EnemyAnimationRelay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Parent AI component (auto-finds if not set)")]
        private EnemyAIBase enemyAI;

        private SniperAI sniperAI;
        private ChaserAI chaserAI;
        private MinibossAI minibossAI;

        private void Awake()
        {
            // Try to find AI in parent if not assigned
            if (enemyAI == null)
                enemyAI = GetComponentInParent<EnemyAIBase>();

            // Cache specific AI types
            sniperAI = enemyAI as SniperAI;
            chaserAI = enemyAI as ChaserAI;
            minibossAI = enemyAI as MinibossAI;

            if (enemyAI == null)
                Debug.LogWarning($"[EnemyAnimationRelay] No EnemyAI found in parent of {gameObject.name}");
        }

        #region Sniper Events

        /// <summary>
        /// Called by animation event when sniper should fire projectile.
        /// Add this as animation event at the frame where projectile should spawn.
        /// </summary>
        public void OnFireProjectile()
        {
            if (sniperAI != null)
            {
                sniperAI.FireProjectileFromEvent();
            }
        }

        /// <summary>
        /// Called by animation event when sniper attack animation ends.
        /// </summary>
        public void OnAttackEnd()
        {
            if (sniperAI != null)
            {
                sniperAI.OnShootAnimationEnd();
            }
        }
        
        /// <summary>
        /// Called by animation event at the start of Aiming phase.
        /// Useful for locking rotation to player.
        /// </summary>
        public void OnAimStart()
        {
            if (sniperAI != null)
            {
                sniperAI.FacePlayer();
            }
        }

        /// <summary>
        /// Called by animation event at the end of the shooting sequence.
        /// Signals the AI to resume movement.
        /// </summary>
        public void OnAimEnd()
        {
            if (sniperAI != null)
            {
                sniperAI.OnShootAnimationEnd();
            }
        }

        #endregion

        #region Chaser Events

        /// <summary>
        /// Called by animation event when attack should deal damage (melee).
        /// </summary>
        public void OnDealDamage()
        {
            if (chaserAI != null)
            {
                chaserAI.DealDamageFromEvent();
            }
        }

        #endregion

        #region Miniboss Events

        /// <summary>
        /// Called by animation event when miniboss should start aiming (show aim line).
        /// Add this at the START of the Shoot_FireBall animation.
        /// </summary>
        public void OnStartAiming()
        {
            if (minibossAI != null)
            {
                minibossAI.StartAimingFromEvent();
            }
        }

        /// <summary>
        /// Called by animation event when miniboss should fire the fireball.
        /// Add this at the frame in the Shoot_FireBall animation where the projectile spawns.
        /// </summary>
        public void OnFireFireball()
        {
            if (minibossAI != null)
            {
                minibossAI.FireFireballFromEvent();
            }
        }

        /// <summary>
        /// Called by animation event when miniboss should spawn the meteor.
        /// Add this at the frame in the Summon_Meteor animation where the meteor appears.
        /// </summary>
        public void OnSummonMeteor()
        {
            if (minibossAI != null)
            {
                minibossAI.SummonMeteorFromEvent();
            }
        }

        /// <summary>
        /// Called by animation event when miniboss attack animation ends.
        /// Add this at the END of both Shoot_FireBall and Summon_Meteor animations.
        /// </summary>
        public void OnMinibossAttackEnd()
        {
            if (minibossAI != null)
            {
                minibossAI.OnAttackAnimationEnd();
            }
        }
        
        /// <summary>
        /// Called by animation event when rage mode charge starts.
        /// Add this at the START of the RageMode_Charge animation.
        /// </summary>
        public void OnRageModeStart()
        {
            if (minibossAI != null)
            {
                minibossAI.RageModeStartFromEvent();
            }
        }
        
        /// <summary>
        /// Called by animation event when rage mode should fire the 360° burst.
        /// Add this at the frame in the RageMode_Charge animation where fireballs spawn.
        /// </summary>
        public void OnRageModeFire()
        {
            if (minibossAI != null)
            {
                minibossAI.RageModeFireFromEvent();
            }
        }
        
        /// <summary>
        /// Called by animation event when rage mode animation ends.
        /// Add this at the END of the RageMode_Charge animation.
        /// </summary>
        public void OnRageModeEnd()
        {
            if (minibossAI != null)
            {
                minibossAI.RageModeEndFromEvent();
            }
        }

        #endregion
    }
}

