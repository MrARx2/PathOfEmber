using UnityEngine;
using System.Collections;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 8f;
    [SerializeField, Tooltip("Seconds before auto-destroy")] private float lifetime = 5f;
    [SerializeField, Tooltip("Damage dealt to player on hit")] private int damage = 100;
    [SerializeField, Tooltip("Destroy when hitting the player")] private bool destroyOnHit = true;

    [Header("Optional: Fire Effect (DoT)")]
    [SerializeField, Tooltip("If true, applies damage over time instead of instant damage")]
    private bool applyDoT = false;
    [SerializeField] private int dotDamagePerTick = 50;
    [SerializeField] private float dotTickInterval = 1f;
    [SerializeField] private int dotTotalTicks = 3;

    [Header("VFX")]
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private float hitVFXDuration = 2f;

    [Header("Fade Out Settings")]
    [SerializeField, Tooltip("Duration of the fade-out effect")]
    private float fadeOutDuration = 0.2f;
    [SerializeField, Tooltip("Shrink projectile scale during fade-out")]
    private bool shrinkOnFade = true;
    [SerializeField, Tooltip("Fade out trail width during fade")]
    private bool fadeTrailWidth = true;

    [Header("Trail Enhancement (Optional)")]
    [SerializeField, Tooltip("Auto-configure trail - DISABLE if using custom prefab effects")]
    private bool autoSetupTrail = false;
    [SerializeField, Tooltip("Trail color at head (bright)")]
    private Color trailHeadColor = new Color(1f, 0.9f, 0.4f, 1f); // Bright yellow-white
    [SerializeField, Tooltip("Trail color at tail (faded)")]
    private Color trailTailColor = new Color(1f, 0.3f, 0f, 0f); // Orange to transparent
    [SerializeField, Tooltip("Trail duration in seconds")]
    private float trailTime = 0.15f;
    [SerializeField, Tooltip("Trail width at head")]
    private float trailStartWidth = 0.3f;

    [Header("Collision Layers")]
    [SerializeField, Tooltip("Layers that trigger the projectile impact")] 
    private LayerMask hitLayers;

    [Header("Performance")]
    [SerializeField, Tooltip("Disable shadows on all child renderers for better performance")]
    private bool disableShadows = true;

    private Vector3 moveDir = Vector3.forward;
    private float lifeTimer;
    private Rigidbody rb;
    private bool isFadingOut = false;
    private Renderer[] renderers;
    private TrailRenderer[] trails;
    private Vector3 initialScale;
    private Color[] initialColors;
    private float[] initialTrailWidths;

    public int Damage => damage;

    private void OnEnable()
    {
        lifeTimer = lifetime;
        isFadingOut = false;
        
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        var cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].isTrigger = true;
        }
        
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        // Cache components for fade effect
        renderers = GetComponentsInChildren<Renderer>();
        trails = GetComponentsInChildren<TrailRenderer>();
        initialScale = transform.localScale;
        
        // Cache initial colors
        initialColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                initialColors[i] = renderers[i].material.color;
            }
        }
        
        // Cache initial trail widths
        initialTrailWidths = new float[trails.Length];
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
            {
                initialTrailWidths[i] = trails[i].widthMultiplier;
            }
        }
        
        // Auto-setup beautiful fire trail
        if (autoSetupTrail)
        {
            SetupBeautifulTrail();
        }
        
        // Apply shadow settings to all renderers
        ApplyShadowSettings();
    }
    
    private void ApplyShadowSettings()
    {
        var shadowMode = disableShadows 
            ? UnityEngine.Rendering.ShadowCastingMode.Off 
            : UnityEngine.Rendering.ShadowCastingMode.On;
        
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            rend.shadowCastingMode = shadowMode;
            rend.receiveShadows = !disableShadows;
        }
        
        foreach (var trail in trails)
        {
            if (trail == null) continue;
            trail.shadowCastingMode = shadowMode;
            trail.receiveShadows = !disableShadows;
        }
    }
    
    /// <summary>
    /// Configures TrailRenderer with beautiful fire effect settings.
    /// </summary>
    private void SetupBeautifulTrail()
    {
        foreach (var trail in trails)
        {
            if (trail == null) continue;
            
            // Timing - short and snappy
            trail.time = trailTime;
            trail.minVertexDistance = 0.02f; // Smooth curves
            
            // Width curve - tapered comet tail effect
            trail.widthMultiplier = trailStartWidth;
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 1f);      // Full width at head
            widthCurve.AddKey(0.3f, 0.6f);  // Taper quickly
            widthCurve.AddKey(0.7f, 0.2f);  // Continue tapering
            widthCurve.AddKey(1f, 0f);      // Point at tail
            trail.widthCurve = widthCurve;
            
            // Color gradient - bright head, fading tail
            Gradient colorGradient = new Gradient();
            colorGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(trailHeadColor, 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.3f), // Orange mid
                    new GradientColorKey(trailTailColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.2f),
                    new GradientAlphaKey(0.3f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            trail.colorGradient = colorGradient;
            
            // Ensure shadow casting is off for performance
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
        
        // Update cached widths after setup
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
            {
                initialTrailWidths[i] = trails[i].widthMultiplier;
            }
        }
    }

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude > 1e-6f)
        {
            moveDir = dir.normalized;
            transform.forward = moveDir;
        }
    }
    
    public void SetSpeed(float newSpeed)
    {
        if (newSpeed > 0f)
            speed = newSpeed;
    }

    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }

    public void SetLifetime(float newLifetime)
    {
        if (newLifetime > 0f)
        {
            lifetime = newLifetime;
            lifeTimer = lifetime;
        }
    }
    
    /// <summary>
    /// Sets lifetime so projectile travels exactly maxRange units.
    /// Projectile can hit during entire travel including fade-out.
    /// </summary>
    public void SetMaxRange(float maxRange, float overrideSpeed = 0f)
    {
        float actualSpeed = overrideSpeed > 0 ? overrideSpeed : speed;
        
        if (maxRange > 0f && actualSpeed > 0f)
        {
            // Simple calculation: lifetime = time to travel the range
            // Fade is just visual - projectile can still hit during fade
            lifetime = maxRange / actualSpeed;
            lifeTimer = lifetime;
        }
    }

    private void Update()
    {
        // Keep moving at constant speed (even during fade for clean trail)
        float dt = Time.deltaTime;
        transform.position += moveDir * speed * dt;

        if (!isFadingOut)
        {
            lifeTimer -= dt;
            if (lifeTimer <= 0f)
                StartFadeOut();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        // Note: Projectile can still hit during fade-out!
        
        // 1. Check if the object is in our hit layers
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

        // 2. Ignore enemies specifically (extra safety)
        if (other.CompareTag("Enemy")) return;
        
        // 3. Ignore triggers if they aren't the player
        if (other.isTrigger && !other.CompareTag("Player")) return;

        bool isValidHit = false;

        // Try to damage the player
        if (other.CompareTag("Player"))
        {
            isValidHit = true;
            var playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = other.GetComponentInParent<PlayerHealth>();
            
            if (playerHealth != null)
            {
                if (applyDoT)
                {
                    // Use damage as initial hit, or fallback to first tick damage if damage is 0
                    int initialHitDamage = damage > 0 ? damage : dotDamagePerTick;
                    playerHealth.ApplyDamageOverTime(dotDamagePerTick, dotTickInterval, dotTotalTicks, initialHitDamage);
                }
                else
                {
                    playerHealth.TakeDamage(damage);
                }
            }
        }
        else
        {
            // It matched the layer mask and wasn't filtered out, so it's a valid environment hit
            isValidHit = true;
        }

        if (isValidHit && destroyOnHit)
        {
            if (hitVFXPrefab != null)
            {
                GameObject vfx = Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, hitVFXDuration);
            }
            StartFadeOut();
        }
    }

    private void StartFadeOut()
    {
        if (isFadingOut) return;
        isFadingOut = true;
        
        // NOTE: Colliders stay ENABLED so projectile can still hit during fade-out!
        // The projectile will be destroyed after fadeOutDuration
        
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float elapsed = 0f;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            float fadeValue = 1f - t; // 1 -> 0
            
            // Fade renderer colors/alpha
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].material == null) continue;
                
                Color c = initialColors[i];
                c.a *= fadeValue;
                renderers[i].material.color = c;
            }
            
            // Fade trail width
            if (fadeTrailWidth)
            {
                for (int i = 0; i < trails.Length; i++)
                {
                    if (trails[i] == null) continue;
                    trails[i].widthMultiplier = initialTrailWidths[i] * fadeValue;
                }
            }
            
            // Shrink scale
            if (shrinkOnFade)
            {
                transform.localScale = initialScale * Mathf.Lerp(1f, 0.1f, t);
            }
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
}
