using UnityEngine;

/// <summary>
/// DEPRECATED - Use ResolutionManager instead.
/// This script is kept for backwards compatibility only.
/// </summary>
[System.Obsolete("Use ResolutionManager instead")]
public class ForcePortraitResolution : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning("[ForcePortraitResolution] This script is deprecated. Use ResolutionManager instead.");
        
        // If ResolutionManager doesn't exist, create one
        if (FindFirstObjectByType<ResolutionManager>() == null)
        {
            gameObject.AddComponent<ResolutionManager>();
        }
        
        Destroy(this);
    }
}
