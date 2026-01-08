using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// Adjusts staff rotation based on animation state.
    /// Attach to the Miniboss and assign the staff transform.
    /// </summary>
    public class MinibossStaffRotation : MonoBehaviour
    {
        [Header("Staff Reference")]
        [SerializeField, Tooltip("The staff transform to rotate")]
        private Transform staffTransform;

        [Header("Rotation Values")]
        [SerializeField, Tooltip("Staff rotation during attack animations")]
        private Vector3 attackRotation = new Vector3(-40f, 100f, 120f);
        
        [SerializeField, Tooltip("Staff rotation during walking/idle")]
        private Vector3 walkingRotation = new Vector3(0f, -90f, 0f);

        [Header("Animation State Names")]
        [SerializeField, Tooltip("Names of attack animation states")]
        private string[] attackStateNames = new string[] 
        { 
            "Attack", 
            "Fireball", 
            "Meteor",
            "RageMode"
        };

        [Header("Settings")]
        [SerializeField, Tooltip("How fast to blend between rotations")]
        private float rotationSpeed = 10f;

        private Animator animator;
        private Vector3 targetRotation;
        private bool isAttacking = false;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            
            if (staffTransform == null)
            {
                Debug.LogWarning("[MinibossStaffRotation] Staff transform not assigned!");
            }
        }

        private void LateUpdate()
        {
            if (animator == null || staffTransform == null) return;

            // Check if currently in an attack state
            isAttacking = IsInAttackState();

            // Set target rotation based on state
            targetRotation = isAttacking ? attackRotation : walkingRotation;

            // Smoothly blend to target rotation using rotationSpeed
            Quaternion target = Quaternion.Euler(targetRotation);
            staffTransform.localRotation = Quaternion.Lerp(
                staffTransform.localRotation, 
                target, 
                rotationSpeed * Time.deltaTime
            );
        }

        private bool IsInAttackState()
        {
            if (animator == null) return false;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            foreach (string stateName in attackStateNames)
            {
                if (stateInfo.IsName(stateName))
                {
                    return true;
                }
            }

            // Also check for any state with "Attack" in the name
            // This is a fallback in case state names don't match exactly
            return false;
        }

        #region Debug
        [ContextMenu("Debug: Set Attack Rotation")]
        public void DebugSetAttackRotation()
        {
            if (staffTransform != null)
            {
                staffTransform.localRotation = Quaternion.Euler(attackRotation);
            }
        }

        [ContextMenu("Debug: Set Walking Rotation")]
        public void DebugSetWalkingRotation()
        {
            if (staffTransform != null)
            {
                staffTransform.localRotation = Quaternion.Euler(walkingRotation);
            }
        }
        #endregion
    }
}
