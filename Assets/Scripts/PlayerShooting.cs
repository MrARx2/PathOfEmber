using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab; // Assign in Inspector
    [SerializeField, Tooltip("Projectiles per second when stationary")] private float fireRate = 1.5f;
    [SerializeField, Tooltip("Max auto-aim range to target enemies")] private float attackRange = 12f;
    [SerializeField, Tooltip("Optional muzzle/fire point; defaults to player position if null")] private Transform firePoint;
    [SerializeField, Tooltip("Projectile travel speed (units/second)")] private float projectileSpeed = 18f;
    [SerializeField, Tooltip("Spawn offset in front of firePoint along aim dir (prevents overlap with player)")] private float muzzleOffset = 0.25f;
    [SerializeField, Tooltip("Optional: rotate this visual transform to face target (keeps physics root untouched)")] private Transform rotateTarget;

    [Header("Targeting")] 
    [SerializeField, Tooltip("Tag used to identify enemies")] private string enemyTag = "Enemy";
    [SerializeField, Tooltip("How often (seconds) to refresh nearest target")] private float targetRefreshInterval = 0.2f;
    [SerializeField, Tooltip("How quickly the visual (rotateTarget) turns toward the current enemy while idle")] private float aimRotationLerpSpeed = 18f;

    [Header("Layers (Optional)")]
    [SerializeField, Tooltip("Name of the Player layer to ignore vs projectile")] private string playerLayerName = "Player";
    [SerializeField, Tooltip("Name of the Projectile layer")] private string projectileLayerName = "Projectile";

    private PlayerMovement movement;
    private float fireCooldown;
    private float targetRefreshTimer;
    private Transform currentTarget;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        // Optional global layer ignore to harden against matrix misconfig
        int pLayer = LayerMask.NameToLayer(playerLayerName);
        int projLayer = LayerMask.NameToLayer(projectileLayerName);
        if (pLayer >= 0 && projLayer >= 0)
        {
            Physics.IgnoreLayerCollision(pLayer, projLayer, true);
        }
    }

    private void Update()
    {
        // Update target periodically
        targetRefreshTimer -= Time.deltaTime;
        if (targetRefreshTimer <= 0f)
        {
            targetRefreshTimer = targetRefreshInterval;
            currentTarget = FindNearestEnemyInRange();
        }

        // Stop-to-shoot: only fire while stationary
        if (movement != null && movement.IsMoving)
        {
            fireCooldown = Mathf.Max(fireCooldown - Time.deltaTime, 0f);
            return;
        }

        if (currentTarget != null && rotateTarget != null)
        {
            Vector3 aimDir = currentTarget.position - rotateTarget.position;
            aimDir.y = 0f;
            RotateVisualTowards(aimDir);
        }

        // If we have a target and are in range, attempt to fire
        if (currentTarget != null)
        {
            float distSqr = (currentTarget.position - transform.position).sqrMagnitude;
            if (distSqr <= attackRange * attackRange)
            {
                TryFireAt(currentTarget.position);
            }
        }

        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;
    }

    private void TryFireAt(Vector3 targetPos)
    {
        if (projectilePrefab == null) return;
        if (fireCooldown > 0f) return;

        // Compute direction from fire point directly to target (XZ plane)
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 dir = targetPos - origin;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-6f)
        {
            dir.Normalize();
            // Face target by rotating visual-only transform to avoid affecting physics body
            if (rotateTarget != null)
            {
                RotateVisualTowards(dir);
            }
            // If rotateTarget is not set, we intentionally do NOT rotate the root to avoid physics depenetration nudges
        }

        // Spawn projectile slightly ahead to avoid any overlap with player colliders
        Vector3 spawnPos = origin + (dir.sqrMagnitude > 1e-6f ? dir : transform.forward) * Mathf.Max(0f, muzzleOffset);
        Quaternion spawnRot = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : transform.forward, Vector3.up);
        GameObject proj = Instantiate(projectilePrefab, spawnPos, spawnRot);

        // Enforce projectile layer recursively
        int projLayer = LayerMask.NameToLayer(projectileLayerName);
        if (projLayer >= 0)
        {
            SetLayerRecursive(proj.transform, projLayer);
        }

        // Initialize projectile direction (if component present)
        var projectile = proj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.SetDirection(spawnRot * Vector3.forward);
            projectile.SetSpeed(projectileSpeed);
        }

        // Ensure projectile doesn't collide with the player (supports nested colliders)
        var projCols = proj.GetComponentsInChildren<Collider>();
        if (projCols != null && projCols.Length > 0)
        {
            var playerCols = GetComponentsInChildren<Collider>();
            for (int p = 0; p < projCols.Length; p++)
            {
                var pc = projCols[p];
                if (pc == null) continue;
                for (int i = 0; i < playerCols.Length; i++)
                {
                    if (playerCols[i] != null)
                        Physics.IgnoreCollision(pc, playerCols[i], true);
                }
            }
        }

        // Reset cooldown
        fireCooldown = fireRate > 0f ? (1f / fireRate) : 0f;
    }

    private Transform FindNearestEnemyInRange()
    {
        GameObject[] enemies;
        try
        {
            enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        }
        catch
        {
            return null;
        }

        Transform nearest = null;
        float bestSqr = float.MaxValue;
        Vector3 p = transform.position;
        float rangeSqr = attackRange * attackRange;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            Vector3 d = e.transform.position - p;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < bestSqr && s <= rangeSqr)
            {
                bestSqr = s;
                nearest = e.transform;
            }
        }
        return nearest;
    }

    private void RotateVisualTowards(Vector3 direction)
    {
        if (rotateTarget == null) return;
        if (direction.sqrMagnitude <= 1e-6f) return;
        direction.y = 0f;
        direction.Normalize();
        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        float lerpFactor = aimRotationLerpSpeed <= 0f ? 1f : aimRotationLerpSpeed * Time.deltaTime;
        rotateTarget.rotation = Quaternion.Slerp(rotateTarget.rotation, targetRot, Mathf.Clamp01(lerpFactor));
    }

    private void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursive(root.GetChild(i), layer);
        }
    }
}
