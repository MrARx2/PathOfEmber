using UnityEngine;

/// <summary>
/// Clamps the position that Cinemachine follows to keep camera within bounds.
/// Attach to an empty GameObject, set this as Cinemachine's Follow target.
/// This object follows the player but clamps X position.
/// </summary>
public class CameraFollowTarget : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("The actual player to follow")]
    private Transform player;

    [Header("Horizontal Bounds (X)")]
    [SerializeField, Tooltip("Minimum X position camera will follow to")]
    private float minX = -3f;
    [SerializeField, Tooltip("Maximum X position camera will follow to")]
    private float maxX = 3f;

    [Header("Following")]
    [SerializeField, Tooltip("Follow Y position of player")]
    private bool followY = true;
    [SerializeField, Tooltip("Follow Z position of player")]
    private bool followZ = true;
    [SerializeField, Tooltip("Smoothing speed (0 = instant, higher = smoother)")]
    private float smoothSpeed = 0f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField, Tooltip("Z depth of the gizmo visualization box")]
    private float gizmoZDepth = 1f;

    private Vector3 velocity;

    private void Start()
    {
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (player != null)
        {
            // Start at player position (clamped)
            transform.position = GetTargetPosition();
        }
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPos = GetTargetPosition();

        if (smoothSpeed > 0f)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, 1f / smoothSpeed);
        }
        else
        {
            transform.position = targetPos;
        }
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 pos = transform.position;
        
        // Clamp X within bounds
        pos.x = Mathf.Clamp(player.position.x, minX, maxX);
        
        if (followY) pos.y = player.position.y;
        if (followZ) pos.z = player.position.z;

        return pos;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw the allowed X bounds as vertical lines
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position;
        float halfZ = gizmoZDepth / 2f;
        
        // Left bound
        Vector3 leftLine = new Vector3(minX, center.y, center.z);
        Gizmos.DrawLine(leftLine + Vector3.up * 5, leftLine + Vector3.down * 5);
        Gizmos.DrawLine(leftLine + Vector3.forward * halfZ, leftLine + Vector3.back * halfZ);
        
        // Right bound
        Vector3 rightLine = new Vector3(maxX, center.y, center.z);
        Gizmos.DrawLine(rightLine + Vector3.up * 5, rightLine + Vector3.down * 5);
        Gizmos.DrawLine(rightLine + Vector3.forward * halfZ, rightLine + Vector3.back * halfZ);

        // Draw safe zone
        Gizmos.color = new Color(0, 1, 0, 0.1f);
        Vector3 safeCenter = new Vector3((minX + maxX) / 2f, center.y, center.z);
        Vector3 safeSize = new Vector3(maxX - minX, 10f, gizmoZDepth);
        Gizmos.DrawCube(safeCenter, safeSize);
    }
}
