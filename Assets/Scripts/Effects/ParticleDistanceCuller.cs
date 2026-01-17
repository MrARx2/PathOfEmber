using UnityEngine;

/// <summary>
/// Pauses/plays particle systems based on distance from the player.
/// Attach this to any GameObject with ParticleSystem(s) that should be culled by distance.
/// </summary>
public class ParticleDistanceCuller : MonoBehaviour
{
    [Header("Distance Settings")]
    [Tooltip("Distance at which particles will be deactivated")]
    public float cullDistance = 6f;
    
    [Tooltip("How often to check distance (in seconds). Lower = more responsive but more CPU.")]
    public float checkInterval = 0.5f;
    
    [Header("Debug")]
    public bool showGizmo = false;
    
    private ParticleSystem[] particleSystems;
    private Transform playerTransform;
    private float sqrCullDistance;
    private float nextCheckTime;
    private bool isPlaying = true;
    
    private void Awake()
    {
        // Cache all particle systems on this object and children
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        sqrCullDistance = cullDistance * cullDistance;
    }
    
    private void Start()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        
        // Stagger initial check to avoid all emitters checking on the same frame
        nextCheckTime = Time.time + Random.Range(0f, checkInterval);
    }
    
    private void Update()
    {
        if (playerTransform == null || Time.time < nextCheckTime)
            return;
            
        nextCheckTime = Time.time + checkInterval;
        
        // Use sqrMagnitude for performance (avoids sqrt)
        float sqrDistance = (transform.position - playerTransform.position).sqrMagnitude;
        bool shouldPlay = sqrDistance <= sqrCullDistance;
        
        if (shouldPlay != isPlaying)
        {
            isPlaying = shouldPlay;
            SetParticlesActive(isPlaying);
        }
    }
    
    private void SetParticlesActive(bool active)
    {
        foreach (var ps in particleSystems)
        {
            if (ps == null) continue;
            
            if (active)
            {
                if (!ps.isPlaying)
                    ps.Play();
            }
            else
            {
                if (ps.isPlaying)
                    ps.Pause();
            }
        }
    }
    
    private void OnValidate()
    {
        // Update squared distance when inspector value changes
        sqrCullDistance = cullDistance * cullDistance;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        
        Gizmos.color = isPlaying ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, cullDistance);
    }
}
