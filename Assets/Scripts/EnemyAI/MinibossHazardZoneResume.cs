using UnityEngine;
using UnityEngine.Events;

namespace EnemyAI
{
    /// <summary>
    /// Attach this to the Miniboss prefab. When this enemy dies, it resumes the Hazard Zone.
    /// This is a backup/alternative to MinibossArenaTrigger for simpler setups.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class MinibossHazardZoneResume : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Tooltip("Speed to resume hazard zone to when miniboss dies")]
        private float normalZoneSpeed = 0.55f;

        [SerializeField, Tooltip("Optional: Pause hazard zone when miniboss spawns/activates")]
        private bool pauseOnSpawn = true;

        [Header("Debug")]
        [SerializeField, Tooltip("Enable debug logging")]
        private bool debugLog = false;

        private Hazards.HazardZoneMeteors hazardZone;
        private EnemyHealth health;
        private bool hasDied = false;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void Start()
        {
            // Find hazard zone
            hazardZone = FindFirstObjectByType<Hazards.HazardZoneMeteors>();

            if (hazardZone == null)
            {
                if (debugLog) Debug.LogWarning("[MinibossHazardZoneResume] Could not find HazardZoneMeteors in scene!");
                return;
            }

            if (debugLog) Debug.Log($"[MinibossHazardZoneResume] Initialized on '{gameObject.name}'. HazardZone found.");

            // Pause hazard zone when miniboss is active
            if (pauseOnSpawn)
            {
                hazardZone.SetSpeed(0f);
                if (debugLog) Debug.Log("[MinibossHazardZoneResume] Hazard Zone PAUSED (speed set to 0)");
            }

            // Subscribe to death
            if (health != null)
            {
                health.OnDeath.AddListener(OnDeath);
            }
        }

        private void OnDeath()
        {
            if (hasDied) return;
            hasDied = true;

            if (debugLog) Debug.Log("[MinibossHazardZoneResume] Miniboss DIED! Resuming hazard zone...");

            // Resume hazard zone
            if (hazardZone != null)
            {
                hazardZone.SetSpeed(normalZoneSpeed);
                if (debugLog) Debug.Log($"[MinibossHazardZoneResume] Hazard Zone RESUMED (speed set to {normalZoneSpeed})");
            }
            else
            {
                // Try to find again
                hazardZone = FindFirstObjectByType<Hazards.HazardZoneMeteors>();
                if (hazardZone != null)
                {
                    hazardZone.SetSpeed(normalZoneSpeed);
                    if (debugLog) Debug.Log($"[MinibossHazardZoneResume] Hazard Zone found and RESUMED (speed set to {normalZoneSpeed})");
                }
                else
                {
                    Debug.LogError("[MinibossHazardZoneResume] ERROR: Could not find HazardZoneMeteors!");
                }
            }
        }

        private void OnDestroy()
        {
            // Safety: If destroyed before death event fires, still try to resume
            if (!hasDied && hazardZone != null)
            {
                hazardZone.SetSpeed(normalZoneSpeed);
                if (debugLog) Debug.Log($"[MinibossHazardZoneResume] OnDestroy - Hazard Zone RESUMED (speed set to {normalZoneSpeed})");
            }
        }
    }
}
