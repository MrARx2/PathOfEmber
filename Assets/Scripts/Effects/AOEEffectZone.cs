using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

/// <summary>
/// AOE effect zone that applies freeze/venom to enemies in radius.
/// Can be placed as a child of enemies (activated on hit) or spawned for wall hits.
/// Features fade in/out animations and optional pulse effect.
/// </summary>
public class AOEEffectZone : MonoBehaviour
{
    public enum EffectType { Freeze, Venom, Both }
    
    [Header("Effect Configuration")]
    [SerializeField] private EffectType effectType = EffectType.Freeze;
    [SerializeField] private float radius = 2f;
    [SerializeField] private float activationDelay = 0f;
    [SerializeField] private LayerMask enemyLayers = -1;
    
    [Header("Freeze Settings")]
    [SerializeField] private float freezeDuration = 2f;
    
    [Header("Venom Settings")]
    [SerializeField] private int venomDamagePerSecond = 100;
    [SerializeField] private float venomDuration = 3f;
    
    [Header("Visual")]
    [SerializeField] private float indicatorDuration = 0.5f;
    [SerializeField] private Color freezeColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color venomColor = new Color(0.6f, 0.2f, 0.8f, 1f);
    [SerializeField] private Color bothColor = new Color(0.4f, 0.5f, 0.9f, 1f);
    
    [Header("Opacity & Fade Animation")]
    [SerializeField, Range(0f, 1f), Tooltip("Maximum opacity the decal will reach")]
    private float targetOpacity = 0.8f;
    [SerializeField, Tooltip("Duration of fade in animation")]
    private float fadeInDuration = 0.15f;
    [SerializeField, Tooltip("Duration of fade out animation")]
    private float fadeOutDuration = 0.25f;
    
    [Header("Pulse Animation")]
    [SerializeField, Tooltip("Enable gentle pulse while active")]
    private bool enablePulse = true;
    [SerializeField, Tooltip("How fast the pulse oscillates")]
    private float pulseSpeed = 3f;
    [SerializeField, Range(0f, 0.5f), Tooltip("How much opacity varies during pulse (0.2 = ±20%)")]
    private float pulseAmount = 0.15f;
    
    [Header("Shader Property Names")]
    [SerializeField] private string decalColorProperty = "Color";
    [SerializeField] private string vfxColorProperty = "Color";
    [SerializeField] private string vfxRadiusProperty = "Radius";
    
    private VisualEffect vfx;
    private MeshRenderer meshRenderer;
    private UnityEngine.Rendering.Universal.DecalProjector decalProjector;
    private Material decalMaterialInstance;
    private Coroutine activeRoutine;
    private bool isSpawnedInstance;
    
    private void Awake()
    {
        CacheComponents();
    }
    
    private void CacheComponents()
    {
        if (vfx == null) vfx = GetComponent<VisualEffect>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        
        if (decalProjector == null)
        {
            decalProjector = GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
            if (decalProjector == null)
                decalProjector = GetComponentInChildren<UnityEngine.Rendering.Universal.DecalProjector>();
        }
        
        if (decalProjector != null && decalProjector.material != null && decalMaterialInstance == null)
        {
            decalMaterialInstance = Instantiate(decalProjector.material);
            decalProjector.material = decalMaterialInstance;
        }
    }
    
    private void OnDestroy()
    {
        if (decalMaterialInstance != null)
        {
            Destroy(decalMaterialInstance);
        }
    }
    
    /// <summary>
    /// Activates the AOE effect. For enemy children - disables after use.
    /// </summary>
    public void Activate(EffectType type, float aoeRadius, float freezeDur, int venomDps, float venomDur, float delay = 0f)
    {
        CacheComponents();
        
        effectType = type;
        radius = aoeRadius;
        freezeDuration = freezeDur;
        venomDamagePerSecond = venomDps;
        venomDuration = venomDur;
        activationDelay = delay;
        isSpawnedInstance = false;
        
        // Scale decal to match physics radius
        ScaleDecalToRadius();
        
        // Start invisible
        SetDecalOpacity(0f);
        
        gameObject.SetActive(true);
        ApplyVisualColor();
        
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ActivateRoutine());
    }
    
    /// <summary>
    /// Configures and starts the AOE for spawned instances (wall hits). Destroys after use.
    /// </summary>
    public void ConfigureAndStart(EffectType type, float aoeRadius, float freezeDur, int venomDps, float venomDur, float delay = 0f)
    {
        CacheComponents();
        
        effectType = type;
        radius = aoeRadius;
        freezeDuration = freezeDur;
        venomDamagePerSecond = venomDps;
        venomDuration = venomDur;
        activationDelay = delay;
        isSpawnedInstance = true;
        
        // Scale decal to match physics radius
        ScaleDecalToRadius();
        
        // Start invisible
        SetDecalOpacity(0f);
        
        ApplyVisualColor();
        
        // Ensure object is active before starting coroutine
        gameObject.SetActive(true);
        
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ActivateRoutine());
    }
    
    private void SetDecalOpacity(float opacity)
    {
        if (decalProjector != null)
        {
            decalProjector.fadeFactor = opacity;
        }
    }
    
    /// <summary>
    /// Scales the DecalProjector to match the physics radius.
    /// Based on calibration: radius 0.3 = decal size 1.0
    /// </summary>
    private void ScaleDecalToRadius()
    {
        if (decalProjector != null)
        {
            // Calibration: 0.3 radius = 1.0 decal size
            // So multiplier = 1.0 / 0.3 = 3.333
            float decalSize = radius / 0.3f;
            decalProjector.size = new Vector3(decalSize, decalSize, decalProjector.size.z);
        }
    }
    
    private void ApplyVisualColor()
    {
        Color targetColor = effectType switch
        {
            EffectType.Freeze => freezeColor,
            EffectType.Venom => venomColor,
            EffectType.Both => bothColor,
            _ => freezeColor
        };
        
        // Apply to DecalProjector material
        if (decalMaterialInstance != null)
        {
            bool colorApplied = false;
            
            if (!string.IsNullOrEmpty(decalColorProperty))
            {
                string propWithUnderscore = "_" + decalColorProperty;
                
                if (decalMaterialInstance.HasProperty(decalColorProperty))
                {
                    decalMaterialInstance.SetColor(decalColorProperty, targetColor);
                    colorApplied = true;
                }
                else if (decalMaterialInstance.HasProperty(propWithUnderscore))
                {
                    decalMaterialInstance.SetColor(propWithUnderscore, targetColor);
                    colorApplied = true;
                }
            }
            
            if (!colorApplied)
            {
                string[] fallbackProps = { "_BaseColor", "_Color", "_Tint", "_EmissionColor" };
                foreach (string prop in fallbackProps)
                {
                    if (decalMaterialInstance.HasProperty(prop))
                    {
                        decalMaterialInstance.SetColor(prop, targetColor);
                        break;
                    }
                }
            }
        }
        
        // Apply to VFX
        if (vfx != null)
        {
            if (vfx.HasVector4(vfxColorProperty))
                vfx.SetVector4(vfxColorProperty, targetColor);
            if (vfx.HasFloat(vfxRadiusProperty))
                vfx.SetFloat(vfxRadiusProperty, radius);
        }
        
        // Apply to mesh (fallback)
        if (meshRenderer != null)
        {
            targetColor.a = 0.5f;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", targetColor);
            block.SetColor("_EmissionColor", targetColor * 2f);
            meshRenderer.SetPropertyBlock(block);
        }
    }
    
    private IEnumerator ActivateRoutine()
    {
        // === FADE IN ===
        if (fadeInDuration > 0)
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                // Ease out for smooth fade in
                float easedT = 1f - (1f - t) * (1f - t);
                SetDecalOpacity(easedT * targetOpacity);
                yield return null;
            }
        }
        SetDecalOpacity(targetOpacity);
        
        // === ACTIVATION DELAY ===
        if (activationDelay > 0)
            yield return new WaitForSeconds(activationDelay);
        
        // === APPLY EFFECTS ===
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayers);
        
        foreach (Collider hit in hits)
        {
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            
            if (enemyHealth != null)
            {
                if (effectType == EffectType.Freeze || effectType == EffectType.Both)
                    enemyHealth.ApplyFreeze(freezeDuration);
                
                if (effectType == EffectType.Venom || effectType == EffectType.Both)
                {
                    int totalTicks = Mathf.RoundToInt(venomDuration);
                    enemyHealth.ApplyDamageOverTime(venomDamagePerSecond, 1f, totalTicks);
                }
            }
        }
        
        // === PULSE WHILE VISIBLE ===
        float pulseTime = indicatorDuration;
        if (enablePulse && pulseTime > 0)
        {
            float elapsed = 0f;
            while (elapsed < pulseTime)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
                float currentOpacity = Mathf.Clamp01(targetOpacity + pulse * targetOpacity);
                SetDecalOpacity(currentOpacity);
                yield return null;
            }
        }
        else if (pulseTime > 0)
        {
            yield return new WaitForSeconds(pulseTime);
        }
        
        // === FADE OUT ===
        if (fadeOutDuration > 0)
        {
            float elapsed = 0f;
            float startOpacity = decalProjector != null ? decalProjector.fadeFactor : targetOpacity;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                // Ease in for smooth fade out
                float easedT = t * t;
                SetDecalOpacity(Mathf.Lerp(startOpacity, 0f, easedT));
                yield return null;
            }
        }
        SetDecalOpacity(0f);
        
        // === CLEANUP ===
        if (isSpawnedInstance)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
        
        activeRoutine = null;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = effectType == EffectType.Freeze ? Color.cyan : 
                       effectType == EffectType.Venom ? Color.magenta : Color.blue;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
