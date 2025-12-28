using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Auto-bakes NavMesh on the bridge at runtime.
/// Automatically calculates volume from child colliders.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class BridgeNavMesh : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private NavMeshSurface surface;
    
    private void Start()
    {
        surface = GetComponent<NavMeshSurface>();
        
        // Auto-calculate bounds from all child colliders
        Bounds bounds = CalculateBoundsFromColliders();
        
        if (bounds.size.magnitude < 0.1f)
        {
            Debug.LogError($"[BridgeNavMesh] No colliders found or bounds too small!");
            return;
        }
        
        // Configure surface - try RenderMeshes instead
        surface.collectObjects = CollectObjects.Volume;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        
        // Set volume to encompass all colliders (in LOCAL space)
        surface.center = transform.InverseTransformPoint(bounds.center);
        surface.size = bounds.size + Vector3.one * 0.5f; // Add padding
        
        if (showDebugLogs)
        {
            Debug.Log($"[BridgeNavMesh] Auto-detected bounds: center={bounds.center}, size={bounds.size}");
            Debug.Log($"[BridgeNavMesh] Volume set to: center={surface.center}, size={surface.size}");
        }
        
        // Build NavMesh
        surface.BuildNavMesh();
        
        if (showDebugLogs)
        {
            if (surface.navMeshData != null)
            {
                Bounds navBounds = surface.navMeshData.sourceBounds;
                Debug.Log($"[BridgeNavMesh] SUCCESS! Baked bounds: {navBounds.size}");
                
                if (navBounds.size.x < 0.1f || navBounds.size.z < 0.1f)
                {
                    Debug.LogError($"[BridgeNavMesh] WARNING: NavMesh is too thin! X={navBounds.size.x}, Z={navBounds.size.z}");
                }
            }
            else
            {
                Debug.LogError($"[BridgeNavMesh] FAILED to bake!");
            }
        }
    }
    
    private Bounds CalculateBoundsFromColliders()
    {
        // Get ALL colliders in this object and children
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        
        if (colliders.Length == 0)
        {
            Debug.LogError($"[BridgeNavMesh] No colliders found in {gameObject.name}!");
            return new Bounds(transform.position, Vector3.zero);
        }
        
        // Start with first collider's bounds
        Bounds bounds = colliders[0].bounds;
        
        // Expand to include all colliders
        foreach (var col in colliders)
        {
            // Skip triggers for NavMesh purposes
            if (col.isTrigger) continue;
            
            bounds.Encapsulate(col.bounds);
            
            if (showDebugLogs)
                Debug.Log($"[BridgeNavMesh] Found collider: {col.gameObject.name}, bounds={col.bounds.size}, isTrigger={col.isTrigger}");
        }
        
        return bounds;
    }
    
    private void OnDestroy()
    {
        if (surface != null && surface.navMeshData != null)
        {
            surface.RemoveData();
        }
    }
}
