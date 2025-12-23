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

        private void Awake()
        {
            // Try to find AI in parent if not assigned
            if (enemyAI == null)
                enemyAI = GetComponentInParent<EnemyAIBase>();

            // Cache specific AI types
            sniperAI = enemyAI as SniperAI;
            chaserAI = enemyAI as ChaserAI;

            if (enemyAI == null)
                Debug.LogWarning($"[EnemyAnimationRelay] No EnemyAI found in parent of {gameObject.name}");
        }

        /// <summary>
        /// Called by animation event when sniper should fire projectile.
        /// Add this as animation event at the frame where projectile should spawn.
        /// </summary>
        public void OnFireProjectile()
        {
            Debug.Log($"[EnemyAnimationRelay] EVENT RECEIVED: OnFireProjectile on {gameObject.name}");
            if (sniperAI != null)
            {
                sniperAI.FireProjectileFromEvent();
            }
            else
            {
                Debug.LogWarning($"[EnemyAnimationRelay] MISSING SniperAI referece on {gameObject.name}");
            }
        }

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

        /// <summary>
        /// Called by animation event when attack animation ends.
        /// </summary>
        public void OnAttackEnd()
        {
            if (sniperAI != null)
            {
                sniperAI.OnShootAnimationEnd();
                Debug.Log($"[EnemyAnimationRelay] OnAttackEnd event received on {gameObject.name}");
            }
        }
    }
}
