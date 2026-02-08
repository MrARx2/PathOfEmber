using UnityEngine;
using Audio;

/// <summary>
/// Heart pickup behavior: Explosion burst → Hover pause → Attraction to player → Scale pop → Collection.
/// Similar to Coin but heals player instead of granting XP.
/// </summary>
public class HeartPickup : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 6f;
    [SerializeField] private float explosionDuration = 0.25f;
    [SerializeField] private float upwardBias = 0.6f;
    
    [Header("Hover Pause")]
    [SerializeField] private float hoverDuration = 0.15f;
    
    [Header("Attraction Settings")]
    [SerializeField] private float attractSpeed = 20f;
    [SerializeField] private float accelerationRate = 25f;
    
    [Header("Collection")]
    [SerializeField] private float collectDistance = 0.5f;
    [SerializeField] private float collectScalePop = 1.3f;
    [SerializeField] private float scalePopDuration = 0.1f;
    
    [Header("Healing")]
    [SerializeField] private int healAmount = 1;
    
    [Header("Sound")]
    [SerializeField] private SoundEvent pickupSound;
    
    // Cached player references (shared across all hearts)
    private static Transform s_cachedPlayerTransform;
    private static PlayerHealth s_cachedPlayerHealth;
    private static bool s_playerCached = false;
    
    // Runtime state
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private Vector3 velocity;
    private float explosionTimer;
    private float hoverTimer;
    private bool isExploding;
    private bool isHovering;
    private bool isAttracting;
    private bool isCollecting;
    private bool isCollected;
    private float currentSpeed;
    private Vector3 originalScale;
    private float scalePopTimer;
    
    private void Awake()
    {
        originalScale = transform.localScale;
    }
    
    private void OnEnable()
    {
        // Reset state for pooling
        ResetState();
    }
    
    private void ResetState()
    {
        isExploding = false;
        isHovering = false;
        isAttracting = false;
        isCollecting = false;
        isCollected = false;
        velocity = Vector3.zero;
        transform.localScale = originalScale;
        currentSpeed = attractSpeed;
        
        // Use cached player if available
        if (s_playerCached && s_cachedPlayerTransform != null)
        {
            playerTransform = s_cachedPlayerTransform;
            playerHealth = s_cachedPlayerHealth;
        }
        else
        {
            CachePlayer();
        }
        
        // Start explosion animation
        StartExplosion();
    }
    
    private void CachePlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            s_cachedPlayerTransform = player.transform;
            s_cachedPlayerHealth = player.GetComponent<PlayerHealth>();
            if (s_cachedPlayerHealth == null)
                s_cachedPlayerHealth = player.GetComponentInChildren<PlayerHealth>();
            if (s_cachedPlayerHealth == null)
                s_cachedPlayerHealth = player.GetComponentInParent<PlayerHealth>();
            
            // Find spine bone for better targeting
            Transform spine = FindBoneRecursive(s_cachedPlayerTransform, "mixamorig:Spine2");
            if (spine != null)
                s_cachedPlayerTransform = spine;
            
            s_playerCached = true;
            playerTransform = s_cachedPlayerTransform;
            playerHealth = s_cachedPlayerHealth;
        }
    }
    
    // Start() is no longer needed - OnEnable handles initialization for pooling
    
    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName)
            return parent;
        
        foreach (Transform child in parent)
        {
            Transform found = FindBoneRecursive(child, boneName);
            if (found != null)
                return found;
        }
        return null;
    }
    
    private void StartExplosion()
    {
        // Random explosion direction
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
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
        transform.position += velocity * Time.deltaTime;
        velocity = Vector3.Lerp(velocity, Vector3.zero, 6f * Time.deltaTime);
        velocity.y -= 4f * Time.deltaTime;
        
        explosionTimer -= Time.deltaTime;
        if (explosionTimer <= 0f)
        {
            isExploding = false;
            isHovering = true;
        }
    }
    
    private void UpdateHover()
    {
        hoverTimer -= Time.deltaTime;
        if (hoverTimer <= 0f)
        {
            isHovering = false;
            isAttracting = true;
        }
    }
    
    private void UpdateAttraction()
    {
        Vector3 targetPos = playerTransform.position;
        float distance = Vector3.Distance(transform.position, targetPos);
        
        currentSpeed += accelerationRate * Time.deltaTime;
        float moveDistance = currentSpeed * Time.deltaTime;
        
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveDistance);
        
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
            Collect();
            return;
        }
        
        float scaleMult = 1f + (collectScalePop - 1f) * Mathf.Sin(t * Mathf.PI);
        transform.localScale = originalScale * scaleMult;
        
        if (playerTransform != null)
        {
            Vector3 targetPos = playerTransform.position;
            float moveDistance = currentSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveDistance);
        }
    }
    
    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;
        
        // Heal player
        if (playerHealth != null)
        {
            playerHealth.Heal(healAmount);
        }
        
        // Play pickup sound
        if (pickupSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(pickupSound);
        }
        
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
