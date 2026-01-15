using UnityEngine;

public enum EnemyType
{
    Chaser,
    Bomber,
    Sniper,
    Miniboss,
    Custom
}

/// <summary>
/// Optional helper for spawn points. Just use empty GameObjects if you prefer.
/// This adds a nice gizmo in the editor with color-coding based on enemy type.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Enemy Type")]
    [SerializeField, Tooltip("Select enemy type to auto-color the gizmo")]
    private EnemyType enemyType = EnemyType.Chaser;
    
    [Header("Preview")]
    [SerializeField] private GameObject previewPrefab;
    [SerializeField, Tooltip("Only used when enemyType is Custom")]
    private Color customGizmoColor = Color.yellow;
    [SerializeField] private float gizmoSize = 0.5f;

    /// <summary>
    /// Returns the gizmo color based on enemy type.
    /// Chaser = Green, Bomber = Red, Sniper = Blue, Miniboss = Magenta
    /// </summary>
    private Color GetEnemyTypeColor()
    {
        switch (enemyType)
        {
            case EnemyType.Chaser:
                return Color.green;
            case EnemyType.Bomber:
                return Color.red;
            case EnemyType.Sniper:
                return Color.blue;
            case EnemyType.Miniboss:
                return Color.magenta;
            case EnemyType.Custom:
            default:
                return customGizmoColor;
        }
    }

    private void OnDrawGizmos()
    {
        Color enemyColor = GetEnemyTypeColor();
        
        // Draw outer wire sphere with enemy color
        Gizmos.color = enemyColor;
        Gizmos.DrawWireSphere(transform.position, gizmoSize);
        
        // Draw semi-transparent fill
        Gizmos.color = new Color(enemyColor.r, enemyColor.g, enemyColor.b, 0.3f);
        Gizmos.DrawSphere(transform.position, gizmoSize * 0.8f);
        
        // Draw forward direction (cyan for visibility)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * gizmoSize * 2);
        
        // NOTE: Labels moved to OnDrawGizmosSelected for MASSIVE performance gain
        // Handles.Label is extremely expensive and was causing 30+ FPS drops
    }

    private void OnDrawGizmosSelected()
    {
        Color enemyColor = GetEnemyTypeColor();
        Gizmos.color = enemyColor;
        Gizmos.DrawSphere(transform.position, gizmoSize);
        
        #if UNITY_EDITOR
        // Draw enemy type label ONLY when selected (Handles.Label is very expensive!)
        UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoSize + 0.3f), enemyType.ToString());
        #endif
    }
    
    /// <summary>
    /// Gets the enemy type assigned to this spawn point.
    /// </summary>
    public EnemyType GetEnemyType() => enemyType;
}
