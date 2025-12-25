using UnityEngine;
using System.Collections;

namespace EnemyAI
{
    /// <summary>
    /// Chaser enemy - melee attacker that chases the player and punches.
    /// Supports upper body layer for attack animations.
    /// </summary>
    public class ChaserAI : EnemyAIBase
    {
        [Header("=== CHASER SETTINGS ===")]
        [SerializeField, Tooltip("Damage dealt per punch.")]
        private int punchDamage = 20;

        [Header("=== UPPER BODY ANIMATION ===")]
        [SerializeField, Tooltip("Index of the upper body layer in the Animator (usually 1)")]
        private int upperBodyLayerIndex = 1;
        
        [SerializeField, Tooltip("Weight to set when attacking")]
        private float attackLayerWeight = 1f;
        
        [SerializeField, Tooltip("How fast to blend layer weight")]
        private float layerBlendSpeed = 10f;
        
        [SerializeField, Tooltip("How long to keep attack weight before blending back")]
        private float attackWeightDuration = 0.5f;

        private float targetLayerWeight = 0f;
        private float currentLayerWeight = 0f;
        private Coroutine attackWeightCoroutine;

        protected override void Awake()
        {
            base.Awake();
            
            // Chaser priority: medium-high (aggressive)
            // if (agent != null)
            //     agent.avoidancePriority = 35; // REMOVED
        }

        protected override void Update()
        {
            base.Update();
            
            // Smoothly blend layer weight
            if (animator != null && upperBodyLayerIndex > 0)
            {
                currentLayerWeight = Mathf.MoveTowards(currentLayerWeight, targetLayerWeight, layerBlendSpeed * Time.deltaTime);
                animator.SetLayerWeight(upperBodyLayerIndex, currentLayerWeight);
            }
        }

        protected override void OnAttack()
        {
            // For now, we keep the immediate attack here.
            // If using Animation Events, you should clear this method and use DealDamageFromEvent only.
            DealDamageFromEvent();
        }

        /// <summary>
        /// Called by EnemyAnimationRelay via Animation Event.
        /// </summary>
        public void DealDamageFromEvent()
        {
            if (target == null) return;

            // Check if still in range
            float distance = Vector3.Distance(VisualPosition, target.position);
            if (distance > attackRange * 1.5f) return; // Allow some leeway

            // Set upper body layer weight for attack animation
            if (attackWeightCoroutine != null)
                StopCoroutine(attackWeightCoroutine);
            attackWeightCoroutine = StartCoroutine(AttackWeightRoutine());

            // Deal damage
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = target.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(punchDamage);
                if (debugLog)
                    Debug.Log($"[ChaserAI] Punched for {punchDamage} damage!");
            }
        }

        private IEnumerator AttackWeightRoutine()
        {
            // Ramp up weight
            targetLayerWeight = attackLayerWeight;
            
            // Wait for attack duration
            yield return new WaitForSeconds(attackWeightDuration);
            
            // Ramp down weight
            targetLayerWeight = 0f;
            attackWeightCoroutine = null;
        }
    }
}
