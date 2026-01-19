using System.Collections;
using UnityEngine;
using Hazards;
using Audio;

/// <summary>
/// Simplified meteor for potion talents. No warning phase.
/// Spawns falling meteor → Impact → Applies Freeze/Venom effect.
/// </summary>
public class EffectMeteor : MonoBehaviour
{
    public enum MeteorEffectType { Freeze, Venom }
    
    [Header("Effect Type")]
    [SerializeField] private MeteorEffectType effectType = MeteorEffectType.Freeze;
    
    [Header("Damage Configuration")]
    [SerializeField] private int impactDamage = 50;
    [SerializeField] private float impactRadius = 2f;
    [SerializeField] private LayerMask damageLayer;
    
    [Header("Freeze Effect")]
    [SerializeField] private float freezeDuration = 2f;
    
    [Header("Venom Effect")]
    [SerializeField] private int venomDamagePerSecond = 100;
    [SerializeField] private float venomDuration = 3f;
    
    [Header("Falling Meteor")]
    [SerializeField] private GameObject meteorPrefab;
    [SerializeField] private float fallHeight = 15f;
    [SerializeField] private float fallDuration = 0.5f;
    
    [Header("Impact Visuals")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionDuration = 2f;
    
    [Header("Color Tints")]
    [SerializeField] private Color freezeColor = new Color(0.2f, 0.8f, 1f, 1f); // Cyan
    [SerializeField] private Color venomColor = new Color(0.6f, 0.2f, 0.8f, 1f); // Purple
    
    [Header("Sound Effects")]
    [SerializeField, Tooltip("Sound when meteor impacts (always plays on hit)")]
    private SoundEvent impactSound;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    private Vector3 impactPosition;
    private GameObject meteorInstance;
    
    private void Start()
    {
        impactPosition = transform.position;
        impactPosition.y = 0f;
        transform.position = impactPosition;
        
        StartCoroutine(MeteorSequence());
    }
    
    /// <summary>
    /// Call this to set the effect type before the sequence starts.
    /// </summary>
    public void SetEffectType(MeteorEffectType type)
    {
        effectType = type;
        if (debugLog)
            Debug.Log($"[EffectMeteor] Effect type set to {type}");
    }
    
    private IEnumerator MeteorSequence()
    {
        if (debugLog) Debug.Log($"[EffectMeteor] Starting {effectType} meteor at {impactPosition}");
        
        // Spawn falling meteor immediately (no warning phase)
        SpawnFallingMeteor();
        
        // Wait for fall
        yield return new WaitForSeconds(fallDuration);
        
        // Impact
        CleanupMeteor();
        SpawnImpactEffects();
        ApplyImpactDamageAndEffect();
        
        // Camera shake
        CameraShakeManager.Shake(CameraShakePreset.Meteor);
        
        // Clean up
        Destroy(gameObject, 0.1f);
    }
    
    private void SpawnFallingMeteor()
    {
        if (meteorPrefab == null) return;
        
        Vector3 spawnPos = impactPosition + Vector3.up * fallHeight;
        Quaternion lookDown = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        
        meteorInstance = Instantiate(meteorPrefab, spawnPos, lookDown);
        
        // Apply color tint to meteor
        ApplyColorTint(meteorInstance, effectType == MeteorEffectType.Freeze ? freezeColor : venomColor);
        
        // Initialize meteor projectile
        MeteorProjectile projectile = meteorInstance.GetComponent<MeteorProjectile>();
        if (projectile == null)
        {
            projectile = meteorInstance.AddComponent<MeteorProjectile>();
        }
        projectile.Initialize(impactPosition, fallDuration);
        
        if (debugLog) Debug.Log($"[EffectMeteor] Meteor spawned at {spawnPos}");
    }
    
    private void CleanupMeteor()
    {
        if (meteorInstance != null)
        {
            Destroy(meteorInstance);
            meteorInstance = null;
        }
    }
    
    private void SpawnImpactEffects()
    {
        if (explosionPrefab == null) return;
        
        GameObject explosion = Instantiate(explosionPrefab, impactPosition, Quaternion.identity);
        
        // Apply color tint to explosion
        ApplyColorTint(explosion, effectType == MeteorEffectType.Freeze ? freezeColor : venomColor);
        
        Destroy(explosion, explosionDuration);
        
        if (debugLog) Debug.Log($"[EffectMeteor] Explosion spawned with {effectType} tint");
    }
    
    private void ApplyImpactDamageAndEffect()
    {
        // Play impact sound first (always plays, even if enemy dies from hit)
        if (impactSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAtPosition(impactSound, impactPosition);
        }
        
        Collider[] hits = Physics.OverlapSphere(impactPosition, impactRadius, damageLayer);
        
        foreach (Collider hit in hits)
        {
            // Get EnemyHealth for effect application
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            
            if (enemyHealth != null)
            {
                // Apply impact damage
                enemyHealth.TakeDamage(impactDamage);
                
                // Apply effect based on type
                switch (effectType)
                {
                    case MeteorEffectType.Freeze:
                        enemyHealth.ApplyFreeze(freezeDuration);
                        if (debugLog) Debug.Log($"[EffectMeteor] Applied {freezeDuration}s freeze to {hit.name}");
                        break;
                        
                    case MeteorEffectType.Venom:
                        int totalTicks = Mathf.RoundToInt(venomDuration);
                        enemyHealth.ApplyDamageOverTime(venomDamagePerSecond, 1f, totalTicks);
                        if (debugLog) Debug.Log($"[EffectMeteor] Applied venom ({venomDamagePerSecond}/s) to {hit.name}");
                        break;
                }
            }
        }
    }
    
    private void ApplyColorTint(GameObject obj, Color color)
    {
        // Try to apply color to all renderers
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r.material != null)
            {
                // Try different property names for color
                if (r.material.HasProperty("_Color"))
                    r.material.SetColor("_Color", color);
                if (r.material.HasProperty("_EmissionColor"))
                    r.material.SetColor("_EmissionColor", color * 2f);
                if (r.material.HasProperty("_TintColor"))
                    r.material.SetColor("_TintColor", color);
            }
        }
        
        // Also try particle systems
        ParticleSystem[] particles = obj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particles)
        {
            var main = ps.main;
            main.startColor = color;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Vector3 pos = Application.isPlaying ? impactPosition : transform.position;
        pos.y = 0f;
        
        // Impact radius
        Gizmos.color = effectType == MeteorEffectType.Freeze ? 
            new Color(0.2f, 0.8f, 1f, 0.5f) : // Cyan
            new Color(0.6f, 0.2f, 0.8f, 0.5f); // Purple
        Gizmos.DrawWireSphere(pos, impactRadius);
        
        // Fall path
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos + Vector3.up * fallHeight);
    }
}
