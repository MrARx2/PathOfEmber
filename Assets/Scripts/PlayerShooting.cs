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
    [SerializeField, Tooltip("Global shooting tempo controlling both bow and character shooting animations")] private float shootingTempo = 1f;
    [SerializeField, Tooltip("Float parameter name used on both Animators to scale shooting speed (optional)")] private string shootingTempoParam = "ShootingTempo";

    [Header("Power-Up Abilities")]
    [SerializeField, Tooltip("If enabled, fires 2x the projectile count")] private bool multishotEnabled = false;
    [SerializeField, Tooltip("Delay between multishot bursts in seconds")] private float multishotDelay = 0.1f;
    [SerializeField, Tooltip("If enabled, fires 3 projectiles in a spread")] private bool tripleshotEnabled = false;
    [SerializeField, Tooltip("Angle spread for triple shot")] private float tripleshotAngle = 25f;

    [Header("Layers (Optional)")]
    [SerializeField, Tooltip("Name of the Player layer to ignore vs projectile")] private string playerLayerName = "Player";
    [SerializeField, Tooltip("Name of the Projectile layer")] private string projectileLayerName = "Projectile";

    private PlayerMovement movement;
    private PlayerAbilities abilities;
    private float fireCooldown;
    private float targetRefreshTimer;
    private Transform currentTarget;
    private Vector3 headLookInitialLocalPos;
    private float stationaryTime;
    private int isShootingHash;
    private int bowShootHash;
    private int characterShootingTempoHash;
    private int bowShootingTempoHash;
    private bool isShooting;
    private int resolvedUpperLayerIndex = -1;
    private bool awaitingRelease;
    private Vector3 preparedTargetPos;
    private float baseShootingTempo;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        abilities = GetComponent<PlayerAbilities>();
        baseShootingTempo = shootingTempo;
        Debug.Log($"[PlayerShooting] Awake: shootingTempo={shootingTempo}, baseShootingTempo={baseShootingTempo}");
        if (animator == null)
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            Animator best = null;
            int bestDepth = int.MaxValue;
            for (int i = 0; i < animators.Length; i++)
            {
                var a = animators[i];
                if (a == null) continue;
                if (bowAnimator != null && a == bowAnimator) continue;
                if (bowAnimator != null && a.transform.IsChildOf(bowAnimator.transform)) continue;
                // Prefer the animator closest to this component in the hierarchy (smallest depth)
                int depth = 0;
                Transform t = a.transform;
                while (t != null && t != transform)
                {
                    t = t.parent;
                    depth++;
                }
                if (t == transform && depth < bestDepth)
                {
                    best = a;
                    bestDepth = depth;
                }
            }
            if (best == null && animators.Length > 0)
            {
                // Fallback: first animator if we couldn't disambiguate
                best = animators[0];
            }
            animator = best;
        }
        if (rotateTarget == null && animator != null)
        {
            rotateTarget = animator.transform;
        }

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

        if (!string.IsNullOrEmpty(shootingTempoParam))
        {
            int hash = Animator.StringToHash(shootingTempoParam);
            if (animator != null)
            {
                characterShootingTempoHash = HasFloatParameter(animator, hash) ? hash : 0;
            }
            if (bowAnimator != null)
            {
                bowShootingTempoHash = HasFloatParameter(bowAnimator, hash) ? hash : 0;
            }
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
            awaitingRelease = false;
            SetShootingState(false);
            UpdateAnimatorLayers();
            UpdateAnimationTempo();
            return;
        }

        stationaryTime += Time.deltaTime;

        UpdateHeadLook(currentTarget);

        bool canShoot = stationaryTime >= shootAfterStopDelay;

        var visTarget = GetEffectiveRotateTarget();
        if (currentTarget != null && visTarget != null)
        {
            Vector3 aimDir = currentTarget.position - visTarget.position;
            aimDir.y = 0f;
            RotateVisualTowards(aimDir);
        }

        bool hasTargetInRange = false;

        if (currentTarget != null)
        {
            float distSqr = (currentTarget.position - transform.position).sqrMagnitude;
            hasTargetInRange = distSqr <= attackRange * attackRange;
            if (canShoot && hasTargetInRange && fireCooldown <= 0f && !awaitingRelease)
            {
                preparedTargetPos = currentTarget.position;
                StartSynchronizedShot();
            }
        }

        SetShootingState((canShoot && hasTargetInRange) || awaitingRelease);

        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        UpdateAnimatorLayers();
        UpdateAnimationTempo();
    }

    private void LateUpdate()
    {
        // Apply visual aim after Animator updates to ensure rotation sticks
        if (movement != null && movement.IsMoving) return;
        var vis = GetEffectiveRotateTarget();
        if (vis == null) return;
        if (currentTarget == null) return;
        Vector3 aimDir = currentTarget.position - vis.position;
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude > 1e-6f)
        {
            RotateVisualTowards(aimDir);
        }
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
            if (GetEffectiveRotateTarget() != null)
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
        var projectile = proj.GetComponent<ArrowProjectile>();
        if (projectile == null)
        {
            projectile = proj.GetComponentInChildren<ArrowProjectile>();
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
        float effectiveRate = fireRate * Mathf.Max(0.01f, shootingTempo);
        fireCooldown = effectiveRate > 0f ? (1f / effectiveRate) : 0f;
    }

    public void ReleaseArrow()
    {
        ReleasePreparedShot();
    }

    public void AnimationEvent_Fire()
    {
        ReleasePreparedShot();
    }

    public void AE_Fire()
    {
        ReleasePreparedShot();
    }

    public void ShootArrow()
    {
        ReleasePreparedShot();
    }

    private void ReleasePreparedShot()
    {
        if (!awaitingRelease) return;
        if (projectilePrefab == null) { awaitingRelease = false; return; }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 targetPos = currentTarget != null ? currentTarget.position : preparedTargetPos;
        Vector3 dir = targetPos - origin;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 1e-6f)
        {
            var vis = GetEffectiveRotateTarget();
            dir = vis != null ? vis.forward : transform.forward;
        }
        else
        {
            dir.Normalize();
        }

        if (GetEffectiveRotateTarget() != null && dir.sqrMagnitude > 1e-6f)
        {
            RotateVisualTowards(dir);
        }

        // Fire first burst immediately
        FireBurst(origin, dir);

        // If multishot is enabled, fire second burst after delay
        if (multishotEnabled)
        {
            StartCoroutine(MultishotDelayRoutine(origin, dir));
        }

        float effectiveRate = fireRate * Mathf.Max(0.01f, shootingTempo);
        fireCooldown = effectiveRate > 0f ? (1f / effectiveRate) : 0f;
        awaitingRelease = false;
        SetShootingState(false);
    }

    private System.Collections.IEnumerator MultishotDelayRoutine(Vector3 origin, Vector3 dir)
    {
        yield return new WaitForSeconds(multishotDelay);
        FireBurst(origin, dir);
    }

    private void FireBurst(Vector3 origin, Vector3 dir)
    {
        if (tripleshotEnabled)
        {
            // Fire 3 projectiles: center, left, right
            FireProjectile(origin, dir);
            FireProjectile(origin, Quaternion.Euler(0, -tripleshotAngle, 0) * dir);
            FireProjectile(origin, Quaternion.Euler(0, tripleshotAngle, 0) * dir);
        }
        else
        {
            FireProjectile(origin, dir);
        }
    }

    private void FireProjectile(Vector3 origin, Vector3 direction)
    {
        Vector3 spawnPos = origin + (direction.sqrMagnitude > 1e-6f ? direction : transform.forward) * Mathf.Max(0f, muzzleOffset);
        Quaternion spawnRot = Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : transform.forward, Vector3.up);
        GameObject proj = Instantiate(projectilePrefab, spawnPos, spawnRot);

        int projLayer = LayerMask.NameToLayer(projectileLayerName);
        if (projLayer >= 0)
        {
            SetLayerRecursive(proj.transform, projLayer);
        }

        var projectile = proj.GetComponent<ArrowProjectile>();
        if (projectile == null)
        {
            projectile = proj.GetComponentInChildren<ArrowProjectile>();
        }
        if (projectile != null)
        {
            projectile.SetDirection(spawnRot * Vector3.forward);
            projectile.SetSpeed(projectileSpeed);

            // Apply ability effects from PlayerAbilities
            if (abilities != null)
            {
                projectile.IsPiercing = abilities.HasPiercing;
                projectile.HasBouncing = abilities.HasBouncingArrows;
                projectile.HasFreezeEffect = abilities.HasFreezeShot;
                projectile.HasVenomEffect = abilities.HasVenomShot;
                projectile.FreezeDuration = abilities.FreezeDuration;
                projectile.VenomDamagePerSecond = abilities.VenomDamagePerSecond;
                projectile.VenomDuration = abilities.VenomDuration;
                
                // Apply trail color based on freeze/venom effects
                projectile.ApplyTrailColor();
            }
        }

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

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            Vector3 d = e.transform.position - p;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < bestSqr)
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
            // Always smoothly follow the enemy position for intuitive head motion
            headLookTarget.position = Vector3.Lerp(headLookTarget.position, target.position, t);
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

    private bool RotateTargetConflictsWithAnimator()
    {
        if (rotateTarget == null) return false;
        if (bowAnimator != null)
        {
            var bowRoot = bowAnimator.transform;
            if (rotateTarget != bowRoot && rotateTarget.IsChildOf(bowRoot)) return true;
        }
        return false;
    }

    private void UpdateAnimationTempo()
    {
        float tempo = shootingTempo;

        if (animator != null && characterShootingTempoHash != 0)
        {
            animator.SetFloat(characterShootingTempoHash, tempo);
        }

        if (bowAnimator != null && bowShootingTempoHash != 0)
        {
            bowAnimator.SetFloat(bowShootingTempoHash, tempo);
        }
    }

    private bool HasFloatParameter(Animator targetAnimator, int hash)
    {
        if (targetAnimator == null || hash == 0) return false;
        var parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.type != AnimatorControllerParameterType.Float) continue;
            if (p.nameHash == hash) return true;
        }
        return false;
    }

    private void RotateVisualTowards(Vector3 direction)
    {
        Transform target = GetEffectiveRotateTarget();
        if (target == null) return;
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
        target.rotation = Quaternion.Slerp(target.rotation, targetRot, Mathf.Clamp01(lerpFactor));
    }

    private Transform GetEffectiveRotateTarget()
    {
        if (rotateTarget == null)
        {
            if (animator != null) return animator.transform;
            return null;
        }
        if (bowAnimator != null)
        {
            var bowRoot = bowAnimator.transform;
            if (rotateTarget != bowRoot && rotateTarget.IsChildOf(bowRoot))
            {
                if (animator != null) return animator.transform;
            }
        }
        return rotateTarget;
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

    private void StartSynchronizedShot()
    {
        awaitingRelease = true;
        // Animation events now handle the shot timing
    }

    #region Public API for PlayerAbilities
    /// <summary>
    /// Sets the shooting tempo multiplier (used by PlayerAbilities for Attack Speed+ power-up).
    /// </summary>
    public void SetTempoMultiplier(float multiplier)
    {
        // Safety: only apply if base tempo is initialized
        if (baseShootingTempo <= 0f)
        {
            Debug.LogWarning($"[PlayerShooting] SetTempoMultiplier called but baseShootingTempo not initialized ({baseShootingTempo}). Ignoring.");
            return;
        }
        
        if (multiplier > 0f)
        {
            float newTempo = baseShootingTempo * multiplier;
            Debug.Log($"[PlayerShooting] SetTempoMultiplier({multiplier:F2}): base={baseShootingTempo}, new={newTempo}");
            shootingTempo = newTempo;
        }
    }

    /// <summary>
    /// Enables or disables multishot (fires 2x projectiles).
    /// </summary>
    public void SetMultishotEnabled(bool enabled)
    {
        multishotEnabled = enabled;
    }

    /// <summary>
    /// Enables or disables triple shot (fires 3 projectiles in a spread).
    /// </summary>
    public void SetTripleShotEnabled(bool enabled, float angle = 25f)
    {
        tripleshotEnabled = enabled;
        tripleshotAngle = angle;
    }
    #endregion
}
