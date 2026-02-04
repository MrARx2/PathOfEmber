using UnityEngine;
using Unity.AI.Navigation;
using System.Collections;

/// <summary>
/// Automatically bakes the NavMeshSurface component at startup.
/// Attach this script to the same GameObject that has the NavMeshSurface component.
/// </summary>
public class AutoBakeNavMesh : MonoBehaviour
{
    [Tooltip("Delay in seconds before baking the NavMesh")]
    public float bakeDelay = 2f;
    
    private NavMeshSurface navMeshSurface;

    void OnEnable()
    {
        navMeshSurface = GetComponent<NavMeshSurface>();
        
        if (navMeshSurface != null)
        {
            StartCoroutine(DelayedBake());
        }
        else
        {
            Debug.LogWarning($"[AutoBakeNavMesh] No NavMeshSurface component found on {gameObject.name}");
        }
    }

    void OnDisable()
    {
        // Cancel any pending bake if disabled early
        StopAllCoroutines();
        
        // Optional: clear data to save memory when pooled?
        // navMeshSurface.RemoveData(); 
        // Keeping data might be better if we return to the exact same spot? 
        // But for random chunks, we always move. Safer to clean or just overwrite.
        // BuildNavMesh overwrites, so it's fine.
    }
    
    private IEnumerator DelayedBake()
    {
        Debug.Log($"[AutoBakeNavMesh] Waiting {bakeDelay}s before baking NavMesh on {gameObject.name}...");
        yield return new WaitForSeconds(bakeDelay);
        
        navMeshSurface.BuildNavMesh();
        Debug.Log($"[AutoBakeNavMesh] NavMesh baked successfully on {gameObject.name}");
    }
}
