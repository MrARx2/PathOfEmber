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
    
    private void Start()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            
            // Find PlayerHealth - could be on root or in children
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = player.GetComponentInChildren<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = player.GetComponentInParent<PlayerHealth>();
            
            if (playerHealth == null)
                Debug.LogWarning("[HeartPickup] PlayerHealth component not found on player!");
            
            // Try to find spine bone for better targeting
            Transform spine = FindBoneRecursive(playerTransform, "mixamorig:Spine2");
            if (spine != null)
                playerTransform = spine;
        }
        else
        {
            Debug.LogWarning("[HeartPickup] Player not found!");
        }
        
        // Start explosion
        StartExplosion();
    }
    
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
        
        // Destroy heart
        Destroy(gameObject);
    }
}
