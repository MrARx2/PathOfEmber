using UnityEngine;

/// <summary>
/// Optional helper for spawn points. Just use empty GameObjects if you prefer.
/// This adds a nice gizmo in the editor.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Preview")]
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private float gizmoSize = 0.5f;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoSize);
        
        // Draw forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * gizmoSize * 2);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoSize);
    }
}
