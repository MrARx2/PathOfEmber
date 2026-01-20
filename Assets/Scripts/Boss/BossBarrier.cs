using UnityEngine;

namespace Boss
{
    /// <summary>
    /// Invisible barrier that blocks the player but allows projectiles to pass through.
    /// Uses layer-based collision filtering (configure in Physics settings).
    /// Optionally disables when the boss is defeated.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossBarrier : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Disable this barrier when the boss is defeated?")]
        private bool disableOnBossDefeat = false;
        
        [SerializeField, Tooltip("Delay before disabling after boss defeat (for death animation)")]
        private float disableDelay = 0f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private Collider _collider;
        
        private void Awake()
        {
            _collider = GetComponent<Collider>();
            
            // Ensure it's NOT a trigger (we want physical blocking)
            if (_collider != null && _collider.isTrigger)
            {
                Debug.LogWarning($"[BossBarrier] {gameObject.name} collider should NOT be a trigger for physical blocking.");
            }
        }
        
        private void OnEnable()
        {
            if (disableOnBossDefeat && TitanBossController.Instance != null)
            {
                TitanBossController.Instance.OnBossDefeated += HandleBossDefeated;
                if (debugLog) Debug.Log($"[BossBarrier] {gameObject.name} subscribed to OnBossDefeated");
            }
        }
        
        private void OnDisable()
        {
            if (TitanBossController.Instance != null)
            {
                TitanBossController.Instance.OnBossDefeated -= HandleBossDefeated;
            }
        }
        
        private void HandleBossDefeated()
        {
            if (debugLog) Debug.Log($"[BossBarrier] {gameObject.name} - Boss defeated, disabling barrier");
            
            if (disableDelay > 0f)
            {
                Invoke(nameof(DisableBarrier), disableDelay);
            }
            else
            {
                DisableBarrier();
            }
        }
        
        private void DisableBarrier()
        {
            if (_collider != null)
                _collider.enabled = false;
            
            // Optionally disable the whole GameObject
            // gameObject.SetActive(false);
            
            if (debugLog) Debug.Log($"[BossBarrier] {gameObject.name} disabled");
        }
        
        /// <summary>
        /// Manually enable/disable the barrier.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (_collider != null)
                _collider.enabled = enabled;
        }
    }
}
