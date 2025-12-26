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
    private float fadeOutDuration = 0.3f;
    [SerializeField, Tooltip("Shrink scale during fade-out")]
    private bool shrinkOnFade = true;
    [SerializeField, Tooltip("Slow down during fade-out")]
    private bool slowDownOnFade = true;

    [Header("Speed-Based Trail")]
    [SerializeField, Tooltip("Scale trail width based on speed during flight (optional)")]
    private bool scaleTrailWithSpeed = false;
    [SerializeField, Tooltip("Reference speed for base trail width (only used if scaleTrailWithSpeed is true)")]
    private float baseSpeedForTrail = 8f;

    [Header("Collision Layers")]
    [SerializeField, Tooltip("Layers that trigger the projectile impact")] 
    private LayerMask hitLayers;

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
    
    private float initialSpeed;
    private float currentSpeed;

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
        
        // Cache initial speed
        initialSpeed = speed;
        currentSpeed = speed;
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

    private void Update()
    {
        if (isFadingOut) return; // Movement handled in fade routine
        
        float dt = Time.deltaTime;
        transform.position += moveDir * currentSpeed * dt;
        
        // Scale trail width based on current speed
        UpdateTrailWidth();

        lifeTimer -= dt;
        if (lifeTimer <= 0f)
            StartFadeOut();
    }
    
    private void UpdateTrailWidth()
    {
        if (!scaleTrailWithSpeed || trails == null) return;
        
        float speedRatio = currentSpeed / Mathf.Max(0.1f, baseSpeedForTrail);
        
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] == null) continue;
            trails[i].widthMultiplier = initialTrailWidths[i] * speedRatio;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || isFadingOut) return;
        
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
        
        // Disable collisions during fade
        var cols = GetComponentsInChildren<Collider>();
        foreach (var col in cols)
        {
            if (col != null) col.enabled = false;
        }
        
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float elapsed = 0f;
        float startSpeed = currentSpeed;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            float fadeValue = 1f - t; // 1 -> 0
            
            // Slow down
            if (slowDownOnFade)
            {
                currentSpeed = Mathf.Lerp(startSpeed, 0f, t);
                // Keep moving while slowing
                transform.position += moveDir * currentSpeed * Time.deltaTime;
            }
            
            // Fade renderer colors/alpha
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].material == null) continue;
                
                Color c = initialColors[i];
                c.a *= fadeValue;
                renderers[i].material.color = c;
            }
            
            // Shrink trails based on current speed
            float speedRatio = currentSpeed / Mathf.Max(0.1f, initialSpeed);
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                trails[i].widthMultiplier = initialTrailWidths[i] * speedRatio * fadeValue;
            }
            
            // Shrink scale
            if (shrinkOnFade)
            {
                transform.localScale = initialScale * Mathf.Lerp(1f, 0.2f, t);
            }
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
}
