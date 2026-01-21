using UnityEngine;
#if UNITY_2022_2_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

/// <summary>
/// Camera shake preset types for different situations.
/// </summary>
public enum CameraShakePreset
{
    Light,      // Small hits
    Medium,     // Standard enemy damage
    Heavy,      // Explosions (Bomber)
    Meteor      // Environmental impacts (explosion shape)
}

/// <summary>
/// Configurable shake settings for a preset.
/// </summary>
[System.Serializable]
public class ShakeSettings
{
    [Range(0f, 1f)]
    public float intensity = 0.25f;
    
    [Tooltip("If true, uses explosion shape (radiating). If false, uses recoil/bump (directional).")]
    public bool explosionShape = false;
}

/// <summary>
/// Singleton manager for triggering camera shake effects.
/// Uses Cinemachine Impulse with configurable presets.
/// </summary>
public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance { get; private set; }

    [Header("Impulse Source")]
    [SerializeField, Tooltip("Cinemachine Impulse Source. Auto-creates if not assigned.")]
    private CinemachineImpulseSource impulseSource;

    [Header("=== SHAKE PRESETS ===")]
    [SerializeField] private ShakeSettings lightShake = new ShakeSettings { intensity = 0.15f, explosionShape = false };
    [SerializeField] private ShakeSettings mediumShake = new ShakeSettings { intensity = 0.3f, explosionShape = false };
    [SerializeField] private ShakeSettings heavyShake = new ShakeSettings { intensity = 0.5f, explosionShape = false };
    [SerializeField] private ShakeSettings meteorShake = new ShakeSettings { intensity = 0.4f, explosionShape = true };

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-create impulse source if not assigned
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource == null)
            {
                impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
                if (debugLog)
                    Debug.Log("[CameraShakeManager] Created CinemachineImpulseSource component");
            }
        }

        if (debugLog)
            Debug.Log("[CameraShakeManager] Initialized with preset support");
    }

    /// <summary>
    /// Trigger camera shake with a preset.
    /// </summary>
    public static void Shake(CameraShakePreset preset)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CameraShakeManager] Shake called but Instance is null!");
            return;
        }

        ShakeSettings settings = Instance.GetSettings(preset);
        Instance.TriggerShake(settings);
        
        if (Instance.debugLog)
            Debug.Log($"[CameraShakeManager] Shake({preset}) -> intensity={settings.intensity:F2}, explosion={settings.explosionShape}");
    }

    /// <summary>
    /// Trigger camera shake with explicit intensity (recoil shape).
    /// </summary>
    public static void Shake(float intensity)
    {
        if (Instance == null) return;
        Instance.TriggerShake(new ShakeSettings { intensity = intensity, explosionShape = false });
    }

    /// <summary>
    /// Legacy: Trigger with default medium settings.
    /// </summary>
    public static void Shake()
    {
        Shake(CameraShakePreset.Medium);
    }

    /// <summary>
    /// Legacy: Scale shake to damage - now just uses Medium preset.
    /// Use Shake(CameraShakePreset) for consistent results.
    /// </summary>
    public static void ShakeForDamage(int damage)
    {
        // Use medium preset for damage - damage scaling removed for HP scaling compatibility
        Shake(CameraShakePreset.Medium);
    }

    // Backup storage for muting
    private struct ShakeBackup
    {
        public float light;
        public float medium;
        public float heavy;
        public float meteor;
    }
    private ShakeBackup? intensityBackup = null;

    /// <summary>
    /// Mutes (or unmutes) all camera shake presets.
    /// Useful for UI screens where shake is distracting (e.g. Prayer Wheel).
    /// </summary>
    public static void MuteShakes(bool mute)
    {
        if (Instance == null) return;
        Instance.SetMuteState(mute);
    }
    
    private void SetMuteState(bool mute)
    {
        if (mute)
        {
            // Only backup if not already muted
            if (intensityBackup == null)
            {
                intensityBackup = new ShakeBackup
                {
                    light = lightShake.intensity,
                    medium = mediumShake.intensity,
                    heavy = heavyShake.intensity,
                    meteor = meteorShake.intensity
                };
                
                // Zero out intensities
                lightShake.intensity = 0f;
                mediumShake.intensity = 0f;
                heavyShake.intensity = 0f;
                meteorShake.intensity = 0f;
                
                if (debugLog) Debug.Log("[CameraShakeManager] Shakes MUTED");
            }
        }
        else
        {
            // Restore from backup
            if (intensityBackup.HasValue)
            {
                lightShake.intensity = intensityBackup.Value.light;
                mediumShake.intensity = intensityBackup.Value.medium;
                heavyShake.intensity = intensityBackup.Value.heavy;
                meteorShake.intensity = intensityBackup.Value.meteor;
                
                intensityBackup = null;
                
                if (debugLog) Debug.Log("[CameraShakeManager] Shakes UNMUTED (Restored)");
            }
        }
    }

    private ShakeSettings GetSettings(CameraShakePreset preset)
    {
        return preset switch
        {
            CameraShakePreset.Light => lightShake,
            CameraShakePreset.Medium => mediumShake,
            CameraShakePreset.Heavy => heavyShake,
            CameraShakePreset.Meteor => meteorShake,
            _ => mediumShake
        };
    }

    private void TriggerShake(ShakeSettings settings)
    {
        if (impulseSource == null)
        {
            if (debugLog)
                Debug.LogWarning("[CameraShakeManager] No impulse source available");
            return;
        }

        Vector3 direction;
        
        if (settings.explosionShape)
        {
            // Explosion: radiating outward from center (more vertical, multi-directional)
            direction = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(0.5f, 1f),  // Bias upward for explosion feel
                Random.Range(-0.3f, 0.3f)
            ).normalized;
        }
        else
        {
            // Recoil/Bump: horizontal directional shake
            direction = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-0.3f, 0.3f),  // Slight vertical for recoil feel
                0f
            ).normalized;
        }

        impulseSource.GenerateImpulse(direction * settings.intensity);
    }

    // ========== DEBUG ==========
    [ContextMenu("Debug: Light Shake")]
    public void DebugShakeLight() => Shake(CameraShakePreset.Light);

    [ContextMenu("Debug: Medium Shake")]
    public void DebugShakeMedium() => Shake(CameraShakePreset.Medium);

    [ContextMenu("Debug: Heavy Shake")]
    public void DebugShakeHeavy() => Shake(CameraShakePreset.Heavy);

    [ContextMenu("Debug: Meteor Shake")]
    public void DebugShakeMeteor() => Shake(CameraShakePreset.Meteor);
}
