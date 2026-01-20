using UnityEngine;

/// <summary>
/// Extension collider for ArrowProjectile that catches close-range enemies and walls.
/// Must be a child of an ArrowProjectile. Inherits damage and effects from parent arrow.
/// Uses raycast to prevent fast-projectile tunneling through walls.
/// Only active for a brief duration at arrow spawn for performance.
/// </summary>
public class ArrowExtensionZone : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Tooltip("How long the extension zone stays active (seconds)")]
    private float activeDuration = 0.15f;
    
    [Header("Wall Detection")]
    [SerializeField, Tooltip("Distance to raycast backward for wall detection")]
    private float backwardRaycastDistance = 1.5f;
    
    [SerializeField, Tooltip("Minimum flight time before backward wall check activates (prevents destroying arrows near spawn)")]
    private float minFlightTimeForWallCheck = 0.05f;

    [SerializeField, Tooltip("Layer for the Player. Used to validate safe shots away from walls.")]
    private LayerMask playerLayer;

    [SerializeField, Tooltip("Layers to check for walls in backward raycast. Leave 'Nothing' to use Parent Arrow's wall layers.")]
    private LayerMask wallCheckLayers;
    
    private ArrowProjectile parentArrow;
    private Collider extensionCollider;
    private float flightTime;
    private bool isActive;
    // Computed mask combining both for the single raycast
    private LayerMask combinedRaycastLayers;
    
    private void Awake()
    {
        // Get parent ArrowProjectile
        parentArrow = GetComponentInParent<ArrowProjectile>();
        extensionCollider = GetComponent<Collider>();
        
        if (parentArrow == null)
        {
            Debug.LogError("[ArrowExtensionZone] Must be a child of an ArrowProjectile!");
            enabled = false;
            return;
        }
        
        // Auto-detect player layer if not set
        if (playerLayer == 0)
        {
            int pLayer = LayerMask.NameToLayer("Player");
            if (pLayer != -1) playerLayer = (1 << pLayer);
        }

        // Auto-detect wall layers source
        LayerMask effectiveWallLayers = wallCheckLayers;
        if (effectiveWallLayers == 0)
        {
            effectiveWallLayers = parentArrow.WallLayers;
        }

        // Combine for the raycast
        combinedRaycastLayers = effectiveWallLayers | playerLayer;
    }
    
    private void OnEnable()
    {
        // Reset state when arrow is spawned/reused from pool
        flightTime = 0f;
        isActive = true;
        
        // Enable collider
        if (extensionCollider != null)
        {
            extensionCollider.enabled = true;
        }
    }
    
    private void Update()
    {
        if (parentArrow == null) return;
        if (!parentArrow.gameObject.activeInHierarchy) return;
        if (!isActive) return;
        
        flightTime += Time.deltaTime;
        
        // Disable after active duration expires
        if (flightTime >= activeDuration)
        {
            Deactivate();
            return;
        }
        
        // Don't check backward for walls until arrow has traveled a bit
        // This prevents destroying arrows when player is standing near a wall but we haven't cleared their collider yet
        if (flightTime < minFlightTimeForWallCheck) return;
        
        // Raycast backward from arrow to detect walls (prevents tunneling)
        Vector3 origin = transform.position;
        Vector3 backwardDir = -parentArrow.transform.forward;
        
        // Check for both walls and player
        if (Physics.Raycast(origin, backwardDir, out RaycastHit hit, backwardRaycastDistance, combinedRaycastLayers))
        {
            // Check if we hit the player layer (valid shot, safe)
            if (((1 << hit.collider.gameObject.layer) & playerLayer) != 0)
            {
                return; // Valid shot, do nothing
            }
            
            // Check if we hit a wall layer (tunneling)
            // Note: We use the effective wall layers here, but since the raycast only hits Combined, and we already filtered Player...
            // It acts as hitting the wall.
            
            // Just double check it is indeed a wall layer we care about
            if (IsWallLayer(hit.collider.gameObject.layer))
            {
                parentArrow.GracefulDestroy();
            }
        }
    }
    
    private void Deactivate()
    {
        isActive = false;
        if (extensionCollider != null)
        {
            extensionCollider.enabled = false;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (parentArrow == null) return;
        if (!isActive) return;
        
        // Skip if arrow already destroyed/pooled
        if (!parentArrow.gameObject.activeInHierarchy) return;
        
        // Check if it's a wall
        if (IsWallLayer(other.gameObject.layer))
        {
            // Arrow hit a wall via extension - destroy the arrow
            parentArrow.GracefulDestroy();
            return;
        }
        
        // Check if it's an enemy
        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = other.GetComponentInParent<EnemyHealth>();
        
        if (enemyHealth != null && !enemyHealth.IsDead)
        {
            // Apply damage with knockback in arrow's forward direction
            enemyHealth.TakeDamageWithKnockback(parentArrow.Damage, transform.forward);
            
            // Apply freeze effect if arrow has it
            if (parentArrow.HasFreezeEffect)
            {
                enemyHealth.ApplyFreeze(parentArrow.FreezeDuration);
            }
            
            // Apply venom effect if arrow has it
            if (parentArrow.HasVenomEffect)
            {
                int totalTicks = Mathf.RoundToInt(parentArrow.VenomDuration);
                enemyHealth.ApplyDamageOverTime(parentArrow.VenomDamagePerSecond, 1f, totalTicks);
            }
            
            // Destroy arrow unless piercing
            if (!parentArrow.IsPiercing)
            {
                parentArrow.GracefulDestroy();
            }
        }
    }
    
    private bool IsWallLayer(int layer)
    {
        // Check against parent arrow's wall layers
        return (parentArrow.WallLayers.value & (1 << layer)) != 0;
    }
}
