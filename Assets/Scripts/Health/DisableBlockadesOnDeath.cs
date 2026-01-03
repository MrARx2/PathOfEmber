using UnityEngine;

/// <summary>
/// Simple script to disable blockades when this enemy dies.
/// Place this on the Miniboss GameObject (same object that has EnemyHealth).
/// Can assign blockades directly OR find them by tag at runtime.
/// </summary>
public class DisableBlockadesOnDeath : MonoBehaviour
{
    [Header("=== BLOCKADES (Direct Reference) ===")]
    [SerializeField, Tooltip("First blockade to disable when this enemy dies")]
    private GameObject blockade1;
    
    [SerializeField, Tooltip("Second blockade to disable when this enemy dies")]
    private GameObject blockade2;
    
    [Header("=== FIND BY TAG (For Prefabs) ===")]
    [SerializeField, Tooltip("If true, find blockades by tag instead of direct reference")]
    private bool findByTag = true;
    
    [SerializeField, Tooltip("Tag to search for blockades")]
    private string blockadeTag = "BossBlockade";
    
    private EnemyHealth health;
    private GameObject[] foundBlockades;
    private bool hasDisabledBlockades = false;
    
    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }
    
    private void Start()
    {
        // Find blockades by tag if enabled
        if (findByTag)
        {
            foundBlockades = GameObject.FindGameObjectsWithTag(blockadeTag);
        }
        
        // Subscribe to death event
        if (health != null)
        {
            health.OnDeath.AddListener(DisableAllBlockades);
        }
    }
    
    private void Update()
    {
        // BACKUP: Check if health is dead every frame
        if (!hasDisabledBlockades && health != null && health.IsDead)
        {
            DisableAllBlockades();
        }
    }
    
    private void OnDestroy()
    {
        // Cleanup listener
        if (health != null)
        {
            health.OnDeath.RemoveListener(DisableAllBlockades);
        }
    }
    
    /// <summary>
    /// Call this when the boss dies to disable blockades.
    /// </summary>
    public void DisableAllBlockades()
    {
        if (hasDisabledBlockades) return;
        
        hasDisabledBlockades = true;
        
        // Disable blockades found by tag
        if (findByTag && foundBlockades != null)
        {
            foreach (GameObject blockade in foundBlockades)
            {
                if (blockade != null)
                    blockade.SetActive(false);
            }
        }
        
        // Also disable direct references (if assigned)
        if (blockade1 != null)
            blockade1.SetActive(false);
        
        if (blockade2 != null)
            blockade2.SetActive(false);
    }
}
