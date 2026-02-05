using UnityEngine;

/// <summary>
/// Simple shockwave VFX that scales up a ring sprite while fading it out.
/// Optionally deals damage to players caught in the expanding ring.
/// Destroys itself when the animation completes.
/// </summary>
public class ShockwaveVFX : MonoBehaviour
{
    [Header("Sprite")]
    [SerializeField, Tooltip("The ring sprite to use (assign via SpriteRenderer)")]
    private SpriteRenderer spriteRenderer;
    
    [Header("Scale")]
    [SerializeField, Tooltip("Starting scale of the ring")]
    private Vector3 startScale = Vector3.one;
    
    [SerializeField, Tooltip("Ending scale of the ring")]
    private Vector3 endScale = Vector3.one * 5f;
    
    [Header("Tint/Color")]
    [SerializeField, Tooltip("Starting color/tint of the ring")]
    private Color startTint = Color.white;
    
    [SerializeField, Tooltip("Ending color/tint of the ring (alpha=0 for full fade)")]
    private Color endTint = new Color(1f, 1f, 1f, 0f);
    
    [Header("Timing")]
    [SerializeField, Tooltip("Duration of the shockwave animation in seconds")]
    private float duration = 0.5f;
    
    [SerializeField, Tooltip("Easing curve for the animation (optional)")]
    private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Damage")]
    [SerializeField, Tooltip("Enable damage dealing")]
    private bool dealsDamage = true;
    
    [SerializeField, Tooltip("Damage amount to deal")]
    private int damageAmount = 20;
    
    [SerializeField, Tooltip("Layer mask for targets (e.g., Player)")]
    private LayerMask targetLayer;
    
    [SerializeField, Tooltip("Collider radius multiplier relative to scale")]
    private float colliderRadiusMultiplier = 0.5f;
    
    [Header("Options")]
    [SerializeField, Tooltip("Destroy the GameObject when animation completes")]
    private bool destroyOnComplete = true;
    
    [SerializeField, Tooltip("Play automatically on Start")]
    private bool playOnStart = true;
    
    [SerializeField, Tooltip("Show collider gizmo in Scene view")]
    private bool showColliderGizmo = true;
    
    [SerializeField, Tooltip("Gizmo color")]
    private Color gizmoColor = new Color(1f, 0f, 0f, 0.5f);
    
    private float _elapsed;
    private bool _isPlaying;
    private SphereCollider _damageCollider;
    private System.Collections.Generic.HashSet<Collider> _alreadyHit = new System.Collections.Generic.HashSet<Collider>();
    
    private void Awake()
    {
        // Auto-find SpriteRenderer if not assigned
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        // Create damage collider if needed
        if (dealsDamage)
        {
            _damageCollider = gameObject.AddComponent<SphereCollider>();
            _damageCollider.isTrigger = true;
            _damageCollider.radius = startScale.x * colliderRadiusMultiplier;
            // Debug.Log($"[ShockwaveVFX] Created damage collider, targetLayer={targetLayer.value}, damage={damageAmount}");
            
            if (targetLayer.value == 0)
                Debug.LogWarning("[ShockwaveVFX] Target Layer is NOTHING! Set Player layer on prefab.");
        }
    }
    
    private void OnEnable()
    {
        if (playOnStart)
            Play();
    }

    private void Start()
    {
        // Handled in OnEnable now
    }
    
    /// <summary>
    /// Starts the shockwave animation.
    /// </summary>
    public void Play()
    {
        if (spriteRenderer == null)
            return;
        
        _elapsed = 0f;
        _isPlaying = true;
        _alreadyHit.Clear();
        
        // Set initial state
        transform.localScale = startScale;
        spriteRenderer.color = startTint;
        
        if (_damageCollider != null)
            _damageCollider.radius = startScale.x * colliderRadiusMultiplier;
    }
    
    /// <summary>
    /// Initializes parameters at runtime (useful when spawning from code).
    /// </summary>
    public void Initialize(Sprite sprite, Color startColor, Color endColor, 
                          Vector3 scaleStart, Vector3 scaleEnd, float animDuration,
                          int damage = 0, LayerMask? layer = null)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
        
        startTint = startColor;
        endTint = endColor;
        startScale = scaleStart;
        endScale = scaleEnd;
        duration = animDuration;
        
        if (damage > 0)
        {
            dealsDamage = true;
            damageAmount = damage;
        }
        
        if (layer.HasValue)
            targetLayer = layer.Value;
    }
    
    private void Update()
    {
        if (!_isPlaying) return;
        
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / duration);
        float eased = easingCurve.Evaluate(t);
        
        // Lerp scale
        Vector3 currentScale = Vector3.Lerp(startScale, endScale, eased);
        transform.localScale = currentScale;
        
        // Update collider radius to match scale
        if (_damageCollider != null)
            _damageCollider.radius = currentScale.x * colliderRadiusMultiplier;
        
        // Lerp color/tint
        if (spriteRenderer != null)
            spriteRenderer.color = Color.Lerp(startTint, endTint, eased);
        
        // Check completion
        if (t >= 1f)
        {
            _isPlaying = false;
            
            if (destroyOnComplete)
            {
                if (ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.Return(gameObject);
                else
                    Destroy(gameObject);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other);
    }
    
    private void OnTriggerStay(Collider other)
    {
        // Also check on stay - catches players already inside when collider scales up
        TryDealDamage(other);
    }
    
    private void TryDealDamage(Collider other)
    {
        if (!dealsDamage || !_isPlaying) return;
        if (_alreadyHit.Contains(other)) return;
        
        // Check layer
        int otherMask = 1 << other.gameObject.layer;
        if ((otherMask & targetLayer) == 0) return;
        
        // Find damageable
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();
        
        if (damageable != null)
        {
            damageable.TakeDamage(damageAmount);
            _alreadyHit.Add(other);
            // Debug.Log($"[ShockwaveVFX] Dealt {damageAmount} damage to {other.name}");
        }
    }
    
    #region Public Setters (for runtime configuration)
    public void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null)
            spriteRenderer.sprite = sprite;
    }
    
    public void SetStartTint(Color color) => startTint = color;
    public void SetEndTint(Color color) => endTint = color;
    public void SetStartScale(Vector3 scale) => startScale = scale;
    public void SetEndScale(Vector3 scale) => endScale = scale;
    public void SetDuration(float time) => duration = time;
    public void SetDamage(int amount) => damageAmount = amount;
    public void SetTargetLayer(LayerMask layer) => targetLayer = layer;
    #endregion
    
    #region Debug Visualization
    private void OnDrawGizmos()
    {
        if (!showColliderGizmo) return;
        
        Gizmos.color = gizmoColor;
        
        // Calculate current radius based on play state
        float radius;
        if (_isPlaying && _damageCollider != null)
        {
            radius = _damageCollider.radius;
        }
        else
        {
            // In edit mode, show start scale
            radius = startScale.x * colliderRadiusMultiplier;
        }
        
        // Draw wire sphere
        Gizmos.DrawWireSphere(transform.position, radius);
        
        // Draw filled sphere (semi-transparent)
        Color fillColor = gizmoColor;
        fillColor.a *= 0.3f;
        Gizmos.color = fillColor;
        Gizmos.DrawSphere(transform.position, radius);
    }
    #endregion
}
