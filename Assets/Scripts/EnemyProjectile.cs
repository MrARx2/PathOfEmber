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
    [SerializeField, Tooltip("VFX spawned when hitting a wall (explosion)")]
    private GameObject wallHitVFXPrefab;
    [SerializeField] private float wallHitVFXDuration = 1f;

    [Header("Fade Out Settings")]
    [SerializeField, Tooltip("Duration of the fade-out effect")]
    private float fadeOutDuration = 0.2f;
    [SerializeField, Tooltip("Shrink projectile scale during fade-out")]
    private bool shrinkOnFade = true;
    [SerializeField, Tooltip("Specific child to shrink (leave empty to shrink whole projectile)")]
    private Transform shrinkTarget;
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

    [SerializeField, Tooltip("Layers that trigger the projectile impact")] 
    private LayerMask hitLayers;
    [SerializeField, Tooltip("Layers that block the projectile (walls)")] 
    private LayerMask wallLayers;

    [Header("Wall Bounce Settings")]
    [SerializeField, Tooltip("Maximum number of wall bounces (0 = no bounce, destroy on hit)")]
    private int maxBounces = 0;
    private int currentBounces = 0;
    private bool bounceEnabled = false;

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
    private Vector3 shrinkTargetInitialScale;
    private Renderer shrinkTargetRenderer;
    private Color shrinkTargetInitialEmission;
    private Color[] initialColors;
    private float[] initialTrailWidths;

    public int Damage => damage;
    
    // Cached flag to prevent re-initialization
    private bool _isInitialized = false;

    private void Awake()
    {
        // One-time initialization - cache all components
        if (_isInitialized) return;
        _isInitialized = true;
        
        rb = GetComponent<Rigidbody>();
        
        // Cache colliders once
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

        // Cache renderers and trails ONCE (expensive GetComponentsInChildren)
        renderers = GetComponentsInChildren<Renderer>();
        trails = GetComponentsInChildren<TrailRenderer>();
        initialScale = transform.localScale;
        
        // Cache shrink target
        if (shrinkTarget != null)
        {
            shrinkTargetInitialScale = shrinkTarget.localScale;
            shrinkTargetRenderer = shrinkTarget.GetComponent<Renderer>();
            
            if (shrinkTargetRenderer != null && shrinkTargetRenderer.material.HasProperty("_EmissionColor"))
            {
                shrinkTargetInitialEmission = shrinkTargetRenderer.material.GetColor("_EmissionColor");
            }
        }
        
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
        
        // Apply shadow settings
        ApplyShadowSettings();
    }

    private void OnEnable()
    {
        // Reset state for pooled reuse (components already cached in Awake)
        lifeTimer = lifetime;
        isFadingOut = false;
        currentBounces = 0;
        
        // Ensure Awake ran (in case OnEnable is called before Awake due to pooling)
        if (!_isInitialized)
        {
            Awake();
        }
        
        // Reset scale and colors to initial values
        transform.localScale = initialScale;
        if (shrinkTarget != null)
        {
            shrinkTarget.localScale = shrinkTargetInitialScale;
            if (shrinkTargetRenderer != null && shrinkTargetRenderer.material.HasProperty("_EmissionColor"))
            {
                shrinkTargetRenderer.material.SetColor("_EmissionColor", shrinkTargetInitialEmission);
            }
        }
        
        // Reset renderer colors
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                renderers[i].material.color = initialColors[i];
            }
        }
        
        // Reset trail widths
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
            {
                trails[i].widthMultiplier = initialTrailWidths[i];
                trails[i].Clear(); // Clear old trail points from previous use
            }
        }
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
            // Flatten to horizontal (keep constant Y height)
            dir.y = 0;
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
    
    /// <summary>
    /// Enables wall bouncing for this projectile.
    /// </summary>
    /// <param name="bounces">Number of times the projectile can bounce off walls.</param>
    public void EnableBounce(int bounces)
    {
        bounceEnabled = true;
        maxBounces = bounces;
        currentBounces = 0;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float moveDistance = speed * dt;
        
        // Check for wall collision using simple Raycast (much cheaper than SphereCast)
        if (!isFadingOut && wallLayers != 0)
        {
            float lookAhead = Mathf.Max(moveDistance * 2f, 0.5f);
            
            RaycastHit hit;
            if (Physics.Raycast(transform.position, moveDir, out hit, lookAhead, wallLayers, QueryTriggerInteraction.Ignore))
            {
                // Check if we can bounce
                if (bounceEnabled && currentBounces < maxBounces)
                {
                    // Reflect direction off the wall surface
                    Vector3 reflectDir = Vector3.Reflect(moveDir, hit.normal);
                    reflectDir.y = 0; // Keep horizontal
                    reflectDir.Normalize();
                    
                    SetDirection(reflectDir);
                    currentBounces++;
                    
                    // Move slightly away from wall to prevent double-bounce
                    transform.position = hit.point + hit.normal * 0.2f;
                    return;
                }
                
                // No more bounces - spawn explosion VFX and destroy
                if (wallHitVFXPrefab != null)
                {
                    GameObject vfx = Instantiate(wallHitVFXPrefab, hit.point, Quaternion.identity);
                    Destroy(vfx, wallHitVFXDuration);
                }
                
                GracefulDestroy();
                return;
            }
        }
        
        // Keep moving at constant speed (even during fade for clean trail)
        transform.position += moveDir * moveDistance;

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
        
        // Always allow hitting Player (safety fallback even if layer mask is wrong)
        bool isPlayer = other.CompareTag("Player");
        
        // 1. Check if the object is in our hit layers OR is the player
        if (!isPlayer && ((1 << other.gameObject.layer) & hitLayers) == 0) return;

        // 2. Ignore enemies specifically (extra safety)
        if (other.CompareTag("Enemy")) return;
        
        // 3. Ignore non-player triggers
        if (other.isTrigger && !isPlayer) return;

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
                // During fade, no trail = no DoT, just impact damage
                if (applyDoT && !isFadingOut)
                {
                    // Use damage as initial hit, or fallback to first tick damage if damage is 0
                    int initialHitDamage = damage > 0 ? damage : dotDamagePerTick;
                    playerHealth.ApplyDamageOverTime(dotDamagePerTick, dotTickInterval, dotTotalTicks, initialHitDamage);
                    
                    // Set player on fire for the duration of the DoT
                    playerHealth.SetOnFire(true, dotTickInterval * dotTotalTicks);
                }
                else
                {
                    // Impact damage only (or fading out)
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
        // NOTE: Trail detachment removed for performance (was allocating new GameObject every fade)
        // Trails now simply shrink in place which looks just as good
        
        float elapsed = 0f;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            
            // Use cubic easing for smooth taper (fast start, slow finish)
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            float fadeValue = 1f - easedT;
            
            // Fade renderer colors/alpha
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].material == null) continue;
                
                Color c = initialColors[i];
                c.a *= fadeValue;
                renderers[i].material.color = c;
            }
            
            // Fade trail width (attached trails shrink in place)
            if (fadeTrailWidth && trails != null)
            {
                for (int i = 0; i < trails.Length; i++)
                {
                    if (trails[i] == null) continue;
                    trails[i].widthMultiplier = initialTrailWidths[i] * fadeValue;
                }
            }
            
            // Shrink scale with sexy easing (back ease - slight overshoot then shrink)
            if (shrinkOnFade)
            {
                // Back ease out: slight overshoot at start, then smooth shrink
                // Creates a "pop and shrink" effect
                float backEase;
                if (easedT < 0.2f)
                {
                    // Quick initial pop (slight scale up)
                    float popT = easedT / 0.2f;
                    backEase = 1f + 0.1f * Mathf.Sin(popT * Mathf.PI);
                }
                else
                {
                    // Smooth shrink to final size
                    float shrinkT = (easedT - 0.2f) / 0.8f;
                    backEase = Mathf.Lerp(1.1f, 0f, shrinkT * shrinkT); // Quadratic ease for smooth finish
                }
                
                // Use specific shrink target if assigned, otherwise shrink whole projectile
                if (shrinkTarget != null)
                {
                    // Non-uniform shrink: Z shrinks faster for "squash" effect
                    float zShrink = backEase * backEase; // Z shrinks quadratically (faster)
                    Vector3 newScale = new Vector3(
                        shrinkTargetInitialScale.x * backEase,
                        shrinkTargetInitialScale.y * backEase,
                        shrinkTargetInitialScale.z * zShrink  // Flattens on Z
                    );
                    shrinkTarget.localScale = newScale;
                    
                    // Fade emission to 0 (fireball dying)
                    if (shrinkTargetRenderer != null && shrinkTargetRenderer.material.HasProperty("_EmissionColor"))
                    {
                        Color fadedEmission = shrinkTargetInitialEmission * backEase;
                        shrinkTargetRenderer.material.SetColor("_EmissionColor", fadedEmission);
                    }
                }
                else
                {
                    transform.localScale = initialScale * backEase;
                }
            }
            
            yield return null;
        }
        
        GracefulDestroy();
    }
    
    /// <summary>
    /// Returns projectile to pool instead of destroying.
    /// Falls back to Destroy if pool is not available.
    /// </summary>
    private void GracefulDestroy()
    {
        // Stop any running coroutines
        StopAllCoroutines();
        
        // Return to pool instead of destroying
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Return(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Helper component that fades a detached enemy projectile trail.
/// </summary>
public class EnemyTrailFader : MonoBehaviour
{
    public void StartFade(TrailRenderer trail, float duration)
    {
        StartCoroutine(FadeRoutine(trail, duration));
    }
    
    private IEnumerator FadeRoutine(TrailRenderer trail, float duration)
    {
        if (trail == null)
        {
            Destroy(gameObject);
            yield break;
        }
        
        float initialWidth = trail.widthMultiplier;
        Gradient initialGradient = trail.colorGradient;
        
        // Cache initial alpha values
        GradientAlphaKey[] alphaKeys = initialGradient.alphaKeys;
        float[] initialAlphas = new float[alphaKeys.Length];
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            initialAlphas[i] = alphaKeys[i].alpha;
        }
        
        float elapsed = 0f;
        
        while (elapsed < duration && trail != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Cubic ease out for natural feel
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            float fadeValue = 1f - easedT;
            
            // Fade width
            trail.widthMultiplier = initialWidth * fadeValue;
            
            // Fade alpha
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha = initialAlphas[i] * fadeValue;
            }
            Gradient newGradient = new Gradient();
            newGradient.SetKeys(initialGradient.colorKeys, alphaKeys);
            trail.colorGradient = newGradient;
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
}
