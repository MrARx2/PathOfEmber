using UnityEngine;
using Audio;

/// <summary>
/// Individual coin behavior: Explosion burst → Hover pause → Attraction to player → Scale pop → Collection.
/// Managed by CoinManager for pooling.
/// </summary>
public class Coin : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 8f;
    [SerializeField] private float explosionDuration = 0.3f;
    [SerializeField] private float upwardBias = 0.5f;
    
    [Header("Hover Pause")]
    [SerializeField, Tooltip("Brief pause before attraction begins")]
    private float hoverDuration = 0.1f;
    
    [Header("Attraction Settings")]
    [SerializeField] private float attractSpeed = 25f;
    [SerializeField] private float accelerationRate = 30f;
    
    [Header("Collection")]
    [SerializeField] private float collectDistance = 0.5f;
    [SerializeField, Tooltip("Scale multiplier at peak of collection pop")]
    private float collectScalePop = 1.5f;
    [SerializeField, Tooltip("Duration of scale pop animation")]
    private float scalePopDuration = 0.08f;
    
    [Header("Sound")]
    [SerializeField] private SoundEvent pickupSound;
    
    // Runtime state
    private Transform playerTransform;
    private Vector3 velocity;
    private float explosionTimer;
    private float hoverTimer;
    private bool isExploding;
    private bool isHovering;
    private bool isAttracting;
    private bool isCollecting;
    private bool isCollected;
    private float currentSpeed;
    private int xpValue;
    private Vector3 originalScale;
    private float scalePopTimer;
    
    private void Awake()
    {
        originalScale = transform.localScale;
    }
    
    /// <summary>
    /// Initialize the coin with XP value and explosion direction.
    /// Called by CoinManager when spawning.
    /// </summary>
    public void Initialize(int xp, Vector3 explosionDirection, Transform player)
    {
        xpValue = xp;
        playerTransform = player;
        
        // Add upward bias to explosion direction
        Vector3 dir = explosionDirection.normalized;
        dir.y += upwardBias;
        dir.Normalize();
        
        velocity = dir * explosionForce;
        explosionTimer = explosionDuration;
        hoverTimer = hoverDuration;
        isExploding = true;
        isHovering = false;
        isAttracting = false;
        isCollecting = false;
        isCollected = false;
        currentSpeed = attractSpeed;
        transform.localScale = originalScale;
        
        gameObject.SetActive(true);
    }
    
    private void Update()
    {
        if (isCollected) return;
        if (playerTransform == null) return;
        
        if (isExploding)
        {
            UpdateExplosion();
        }
        else if (isHovering)
        {
            UpdateHover();
        }
        else if (isAttracting)
        {
            UpdateAttraction();
        }
        else if (isCollecting)
        {
            UpdateCollectionPop();
        }
    }
    
    private void UpdateExplosion()
    {
        // Apply velocity with decay
        transform.position += velocity * Time.deltaTime;
        
        // Decay velocity (ease-out)
        velocity = Vector3.Lerp(velocity, Vector3.zero, 8f * Time.deltaTime);
        
        // Add slight gravity
        velocity.y -= 5f * Time.deltaTime;
        
        explosionTimer -= Time.deltaTime;
        if (explosionTimer <= 0f)
        {
            isExploding = false;
            isHovering = true;
        }
    }
    
    private void UpdateHover()
    {
        // Brief moment of weightlessness
        hoverTimer -= Time.deltaTime;
        if (hoverTimer <= 0f)
        {
            isHovering = false;
            isAttracting = true;
        }
    }
    
    private void UpdateAttraction()
    {
        Vector3 targetPos = playerTransform.position + Vector3.up * 0.5f;
        Vector3 direction = (targetPos - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetPos);
        
        // Accelerate as we get closer
        currentSpeed += accelerationRate * Time.deltaTime;
        
        // Move toward player
        transform.position += direction * currentSpeed * Time.deltaTime;
        
        // Check for collection - start scale pop
        if (distance <= collectDistance)
        {
            StartCollectionPop();
        }
    }
    
    private void StartCollectionPop()
    {
        isAttracting = false;
        isCollecting = true;
        scalePopTimer = 0f;
    }
    
    private void UpdateCollectionPop()
    {
        scalePopTimer += Time.deltaTime;
        float t = scalePopTimer / scalePopDuration;
        
        if (t >= 1f)
        {
            // Pop complete, collect
            Collect();
            return;
        }
        
        // Scale up quickly then down (sine curve for smooth pop)
        float scaleMult = 1f + (collectScalePop - 1f) * Mathf.Sin(t * Mathf.PI);
        transform.localScale = originalScale * scaleMult;
        
        // Continue moving toward player during pop
        if (playerTransform != null)
        {
            Vector3 targetPos = playerTransform.position + Vector3.up * 0.5f;
            Vector3 direction = (targetPos - transform.position).normalized;
            transform.position += direction * currentSpeed * Time.deltaTime;
        }
    }
    
    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;
        
        // Grant XP
        if (XPSystem.Instance != null)
        {
            XPSystem.Instance.AddXP(xpValue);
        }
        
        // Play pickup sound
        if (pickupSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(pickupSound);
        }
        
        // Return to pool
        CoinManager.Instance?.ReturnToPool(this);
    }
    
    /// <summary>
    /// Reset coin state when returned to pool.
    /// </summary>
    public void ResetCoin()
    {
        isExploding = false;
        isHovering = false;
        isAttracting = false;
        isCollecting = false;
        isCollected = false;
        velocity = Vector3.zero;
        transform.localScale = originalScale;
        gameObject.SetActive(false);
    }
}

