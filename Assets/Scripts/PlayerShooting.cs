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

    [Header("Head Look")]
    [SerializeField] private Transform headLookTarget;
    [SerializeField] private float headLookLerpSpeed = 10f;
    [SerializeField, Tooltip("If true, LookAt snaps exactly to enemy position; if false, it smoothly follows using headLookLerpSpeed")] private bool headLookSnapToTarget = true;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isShootingBoolName = "IsShooting";
    [SerializeField] private Animator bowAnimator;
    [SerializeField] private string bowShootBoolName = "BowShoot";
    [Space]
    [SerializeField, Tooltip("If enabled, this script will drive the weight of an upper-body layer mask based on isShooting")] private bool controlUpperLayer = true;
    [SerializeField, Tooltip("Animator layer name for upper-body masked layer")] private string upperLayerName = "UpperLayer";
    [SerializeField, Tooltip("Fallback layer index if name is empty or not found (-1 disables)")] private int upperLayerIndex = -1;
    [SerializeField, Tooltip("Speed to blend Animator layer weight (units/sec) between 0 and 1")] private float upperLayerBlendSpeed = 10f;

    [Header("Shooting State")]
    [SerializeField] private float shootAfterStopDelay = 0.2f;

    [Header("Tempo")]
    [SerializeField, Tooltip("Global shooting tempo multiplier that scales fire rate and draw animation speeds")] private float shootTempo = 1f;
    [SerializeField, Tooltip("Float parameter on main Animator used to scale standing draw arrow animation speed (optional)")] private string characterDrawSpeedParam = "";
    [SerializeField, Tooltip("Float parameter on bow Animator used to scale DrawingBow animation speed (optional)")] private string bowDrawSpeedParam = "";

    [Header("Layers (Optional)")]
    [SerializeField, Tooltip("Name of the Player layer to ignore vs projectile")] private string playerLayerName = "Player";
    [SerializeField, Tooltip("Name of the Projectile layer")] private string projectileLayerName = "Projectile";

    private PlayerMovement movement;
    private float fireCooldown;
    private float targetRefreshTimer;
    private Transform currentTarget;
    private Vector3 headLookInitialLocalPos;
    private float stationaryTime;
    private int isShootingHash;
    private int bowShootHash;
    private int characterDrawSpeedHash;
    private int bowDrawSpeedHash;
    private bool isShooting;
    private int resolvedUpperLayerIndex = -1;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        if (headLookTarget != null)
        {
            headLookInitialLocalPos = transform.InverseTransformPoint(headLookTarget.position);
        }

        if (animator != null && !string.IsNullOrEmpty(isShootingBoolName))
        {
            isShootingHash = Animator.StringToHash(isShootingBoolName);
        }

        if (bowAnimator != null && !string.IsNullOrEmpty(bowShootBoolName))
        {
            bowShootHash = Animator.StringToHash(bowShootBoolName);
        }

        if (animator != null && !string.IsNullOrEmpty(characterDrawSpeedParam))
        {
            characterDrawSpeedHash = Animator.StringToHash(characterDrawSpeedParam);
        }

        if (bowAnimator != null && !string.IsNullOrEmpty(bowDrawSpeedParam))
        {
            bowDrawSpeedHash = Animator.StringToHash(bowDrawSpeedParam);
        }

        // Resolve and initialize upper layer weight
        if (animator != null && controlUpperLayer)
        {
            int idx = -1;
            if (!string.IsNullOrEmpty(upperLayerName))
            {
                idx = animator.GetLayerIndex(upperLayerName);
            }
            if (idx < 0 && upperLayerIndex >= 0 && upperLayerIndex < animator.layerCount)
            {
                idx = upperLayerIndex;
            }
            resolvedUpperLayerIndex = idx;
            if (resolvedUpperLayerIndex >= 0)
            {
                animator.SetLayerWeight(resolvedUpperLayerIndex, 0f);
            }
        }

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

        bool isMoving = movement != null && movement.IsMoving;

        if (isMoving)
        {
            stationaryTime = 0f;
            if (fireCooldown > 0f)
                fireCooldown -= Time.deltaTime;
            UpdateHeadLook(currentTarget);
            SetShootingState(false);
            UpdateAnimatorLayers();
            UpdateAnimationTempo();
            return;
        }

        stationaryTime += Time.deltaTime;

        UpdateHeadLook(currentTarget);

        bool canShoot = stationaryTime >= shootAfterStopDelay;

        if (currentTarget != null && rotateTarget != null)
        {
            Vector3 aimDir = currentTarget.position - rotateTarget.position;
            aimDir.y = 0f;
            RotateVisualTowards(aimDir);
        }

        bool hasTargetInRange = false;

        if (currentTarget != null)
        {
            float distSqr = (currentTarget.position - transform.position).sqrMagnitude;
            hasTargetInRange = distSqr <= attackRange * attackRange;
            if (canShoot && hasTargetInRange)
            {
                TryFireAt(currentTarget.position);
            }
        }

        SetShootingState(canShoot && hasTargetInRange);

        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        UpdateAnimatorLayers();
        UpdateAnimationTempo();
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

        // Initialize projectile direction (if component present on root or children)
        var projectile = proj.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = proj.GetComponentInChildren<Projectile>();
        }
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

        // Reset cooldown, scaled by global shoot tempo (fireRate is base shots/sec at tempo=1)
        float effectiveRate = fireRate * Mathf.Max(0.01f, shootTempo);
        fireCooldown = effectiveRate > 0f ? (1f / effectiveRate) : 0f;
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

    private void UpdateHeadLook(Transform target)
    {
        if (headLookTarget == null) return;
        float t = headLookLerpSpeed <= 0f ? 1f : Mathf.Clamp01(headLookLerpSpeed * Time.deltaTime);
        Vector3 defaultPos = transform.TransformPoint(headLookInitialLocalPos);
        if (target != null)
        {
            if (headLookSnapToTarget)
            {
                headLookTarget.position = target.position;
            }
            else
            {
                headLookTarget.position = Vector3.Lerp(headLookTarget.position, target.position, t);
            }
        }
        else
        {
            headLookTarget.position = Vector3.Lerp(headLookTarget.position, defaultPos, t);
        }
    }

    private void SetShootingState(bool value)
    {
        if (isShooting == value) return;
        isShooting = value;
        if (animator != null && isShootingHash != 0)
        {
            animator.SetBool(isShootingHash, value);
        }

        if (bowAnimator != null && bowShootHash != 0)
        {
            bowAnimator.SetBool(bowShootHash, value);
        }
    }

    private void UpdateAnimatorLayers()
    {
        if (!controlUpperLayer) return;
        if (animator == null) return;
        if (resolvedUpperLayerIndex < 0 || resolvedUpperLayerIndex >= animator.layerCount) return;

        float current = animator.GetLayerWeight(resolvedUpperLayerIndex);
        float target = isShooting ? 1f : 0f;
        if (Mathf.Approximately(current, target)) return;
        float next = Mathf.MoveTowards(current, target, Mathf.Max(0f, upperLayerBlendSpeed) * Time.deltaTime);
        animator.SetLayerWeight(resolvedUpperLayerIndex, next);
    }

    private void UpdateAnimationTempo()
    {
        float tempo = shootTempo;

        if (animator != null && characterDrawSpeedHash != 0)
        {
            animator.SetFloat(characterDrawSpeedHash, tempo);
        }

        if (bowAnimator != null && bowDrawSpeedHash != 0)
        {
            bowAnimator.SetFloat(bowDrawSpeedHash, tempo);
        }
    }

    private void RotateVisualTowards(Vector3 direction)
    {
        if (rotateTarget == null) return;
        if (direction.sqrMagnitude <= 1e-6f) return;
        direction.y = 0f;
        direction.Normalize();
        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        if (movement != null)
        {
            float yawOffset = movement.VisualYawOffsetDegrees;
            if (Mathf.Abs(yawOffset) > 0.001f)
            {
                targetRot *= Quaternion.Euler(0f, yawOffset, 0f);
            }
        }
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
