using System.Collections;
using UnityEngine;

namespace Hazards
{
    /// <summary>
    /// Controls the Hazard Zone that prevents player backtracking.
    /// The zone moves forward slowly and expands when player enters.
    /// Spawns meteors with increasing intensity based on depth.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class HazardZoneMeteors : MonoBehaviour
    {
        #region Serialized Fields

        [Header("=== REFERENCES ===")]
        [SerializeField, Tooltip("Reference to the player transform. Auto-finds if not set.")]
        private Transform player;
        
        [SerializeField, Tooltip("The meteor strike prefab to spawn.")]
        private GameObject meteorStrikePrefab;

        [Header("=== ZONE MOVEMENT ===")]
        [SerializeField, Tooltip("Speed at which the zone moves forward (units per second).")]
        private float zoneAdvanceSpeed = 0.5f;
        
        [SerializeField, Tooltip("Direction the zone advances in world space.")]
        private Vector3 advanceDirection = Vector3.forward;
        
        [SerializeField, Tooltip("Delay before zone starts advancing (seconds).")]
        private float advanceStartDelay = 5.0f;

        [Header("=== ZONE EXPANSION ===")]
        [SerializeField, Tooltip("If true, zone expands when player is inside.")]
        private bool expandWhenPlayerInside = true;
        
        [SerializeField, Tooltip("How fast the zone expands when player is inside (units per second).")]
        private float expansionSpeed = 1.0f;
        
        [SerializeField, Tooltip("Maximum zone size (original size multiplier).")]
        private float maxExpansionMultiplier = 3.0f;

        [Header("=== SPAWN TIMING ===")]
        [SerializeField, Tooltip("Minimum time between meteor spawns (at maximum intensity/depth).")]
        private float minSpawnInterval = 0.1f;
        
        [SerializeField, Tooltip("Maximum time between meteor spawns (at minimum intensity/depth).")]
        private float maxSpawnInterval = 3.0f;
        
        [SerializeField, Tooltip("Delay before spawning starts when player enters zone.")]
        private float spawnWarmupDelay = 0.3f;
        
        [SerializeField, Tooltip("Curve controlling intensity based on depth (0=edge, 1=deep).")]
        private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("=== SPAWN DISTANCE ===")]
        [SerializeField, Tooltip("Minimum distance from player to spawn meteors (safe zone).")]
        private float minSpawnDistance = 1.5f;
        
        [SerializeField, Tooltip("Maximum distance from player to spawn meteors.")]
        private float maxSpawnDistance = 12.0f;

        [Header("=== MULTI-SPAWN ===")]
        [SerializeField, Tooltip("Maximum meteors to spawn per wave at max intensity.")]
        private int maxMeteorsPerWave = 6;
        
        [SerializeField, Tooltip("Curve controlling meteors per wave based on intensity.")]
        private AnimationCurve meteorsPerWaveCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);

        [Header("=== PLAYER TARGETING ===")]
        [SerializeField, Tooltip("Enable targeted spawning toward player.")]
        private bool targetPlayer = true;
        
        [SerializeField, Tooltip("How accurately meteors target player (0=random, 1=exact).")]
        [Range(0f, 1f)]
        private float targetingAccuracy = 0.5f;
        
        [SerializeField, Tooltip("Increase accuracy as depth increases.")]
        private bool accuracyScalesWithDepth = true;

        [Header("=== FIRE DAMAGE ===")]
        [SerializeField, Tooltip("Apply fire damage while player is in zone.")]
        private bool applyFireDamage = true;
        
        [SerializeField, Tooltip("Fire damage per tick.")]
        private int fireDamagePerTick = 10;
        
        [SerializeField, Tooltip("Time between fire damage ticks (seconds).")]
        private float fireDamageTickInterval = 0.5f;
        
        [SerializeField, Tooltip("Fire damage multiplier at max depth.")]
        private float maxDepthFireDamageMultiplier = 2.0f;

        [Header("=== ZONE CONFIGURATION ===")]
        [SerializeField, Tooltip("Which axis defines 'depth' into the zone.")]
        private DepthAxis depthAxis = DepthAxis.Z;
        
        [SerializeField, Tooltip("Invert the depth direction.")]
        private bool invertDepthDirection = true;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Show gizmos in Scene view.")]
        private bool showGizmos = true;
        
        [SerializeField, Tooltip("Log debug information to console.")]
        private bool debugLog = false;
        
        [SerializeField, Tooltip("Show spawn ring around player.")]
        private bool showSpawnRing = true;

        #endregion

        #region Private State

        private enum DepthAxis { X, Y, Z }

        private BoxCollider _zoneCollider;
        private Vector3 _originalSize;
        private Coroutine _spawnRoutine;
        private Coroutine _fireDamageRoutine;
        private bool _playerInZone;
        private float _currentDepth;
        private float _currentIntensity;
        private float _advanceTimer;
        private float _totalDistanceMoved; // Debug: track actual movement
        private PlayerHealth _playerHealth;
        private PlayerAbilities _playerAbilities;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _zoneCollider = GetComponent<BoxCollider>();
            _zoneCollider.isTrigger = true;
            _originalSize = _zoneCollider.size;
            _advanceTimer = advanceStartDelay;
            _totalDistanceMoved = 0f;

            // Normalize advance direction
            advanceDirection = advanceDirection.normalized;

            // Auto-find player
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    player = playerObj.transform;
                    _playerHealth = playerObj.GetComponent<PlayerHealth>();
                    _playerAbilities = playerObj.GetComponent<PlayerAbilities>();
                }
            }
            else
            {
                _playerHealth = player.GetComponent<PlayerHealth>();
                _playerAbilities = player.GetComponent<PlayerAbilities>();
            }

            if (player == null)
            {
                Debug.LogError("[HazardZoneMeteors] No player found!");
            }
        }

        private void Update()
        {
            if (player == null) return;

            // Handle zone advancing (independent of player)
            HandleZoneAdvance();

            // Check player in zone
            bool wasInZone = _playerInZone;
            _playerInZone = IsPositionInZone(player.position);

            // Handle state transitions
            if (_playerInZone && !wasInZone)
            {
                OnPlayerEnterZone();
            }
            else if (!_playerInZone && wasInZone)
            {
                OnPlayerExitZone();
            }

            // Handle zone expansion when player is inside
            if (_playerInZone && expandWhenPlayerInside)
            {
                HandleZoneExpansion();
            }

            // Update depth/intensity for gizmos
            if (_playerInZone)
            {
                _currentDepth = CalculatePlayerDepth();
                _currentIntensity = intensityCurve.Evaluate(_currentDepth);
            }
        }

        #endregion

        #region Zone Movement & Expansion

        private void HandleZoneAdvance()
        {
            // Wait for initial delay
            if (_advanceTimer > 0)
            {
                _advanceTimer -= Time.deltaTime;
                if (debugLog) Debug.Log($"[HazardZoneMeteors] Waiting to advance: {_advanceTimer:F1}s remaining");
                return;
            }

            // Move zone forward
            float moveAmount = zoneAdvanceSpeed * Time.deltaTime;
            transform.position += advanceDirection * moveAmount;
            _totalDistanceMoved += moveAmount;

            if (debugLog && Time.frameCount % 60 == 0) // Log every ~1 second
            {
                Debug.Log($"[HazardZoneMeteors] Speed: {zoneAdvanceSpeed} u/s, Total moved: {_totalDistanceMoved:F2}u");
            }
        }

        private void HandleZoneExpansion()
        {
            // Double-check the toggle (safety)
            if (!expandWhenPlayerInside)
            {
                return;
            }

            // Expand the zone (grow the collider size)
            Vector3 currentSize = _zoneCollider.size;
            Vector3 maxSize = _originalSize * maxExpansionMultiplier;

            // Check if already at max
            bool alreadyMax = true;
            switch (depthAxis)
            {
                case DepthAxis.Z:
                    alreadyMax = currentSize.z >= maxSize.z;
                    break;
                case DepthAxis.X:
                    alreadyMax = currentSize.x >= maxSize.x;
                    break;
                case DepthAxis.Y:
                    alreadyMax = currentSize.y >= maxSize.y;
                    break;
            }

            if (alreadyMax) return;

            // Expand in the depth axis direction
            float expansion = expansionSpeed * Time.deltaTime;
            
            switch (depthAxis)
            {
                case DepthAxis.Z:
                    currentSize.z = Mathf.Min(currentSize.z + expansion, maxSize.z);
                    break;
                case DepthAxis.X:
                    currentSize.x = Mathf.Min(currentSize.x + expansion, maxSize.x);
                    break;
                case DepthAxis.Y:
                    currentSize.y = Mathf.Min(currentSize.y + expansion, maxSize.y);
                    break;
            }

            _zoneCollider.size = currentSize;

            if (debugLog && currentSize != _zoneCollider.size)
            {
                Debug.Log($"[HazardZoneMeteors] Zone expanding: {currentSize}");
            }
        }

        #endregion

        #region Player Zone State

        private void OnPlayerEnterZone()
        {
            if (debugLog) Debug.Log("[HazardZoneMeteors] Player ENTERED hazard zone");

            // Start spawning
            if (_spawnRoutine == null)
            {
                _spawnRoutine = StartCoroutine(SpawnRoutine());
            }

            // Start fire damage
            if (applyFireDamage && _fireDamageRoutine == null)
            {
                _fireDamageRoutine = StartCoroutine(FireDamageRoutine());
            }

            // Notify PlayerHealth
            if (_playerHealth != null)
            {
                _playerHealth.SetOnFire(true);
            }
        }

        private void OnPlayerExitZone()
        {
            if (debugLog) Debug.Log("[HazardZoneMeteors] Player EXITED hazard zone");

            // Stop spawning
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            // Stop fire damage
            if (_fireDamageRoutine != null)
            {
                StopCoroutine(_fireDamageRoutine);
                _fireDamageRoutine = null;
            }

            // Notify PlayerHealth
            if (_playerHealth != null)
            {
                _playerHealth.SetOnFire(false);
            }
        }

        #endregion

        #region Spawning

        private IEnumerator SpawnRoutine()
        {
            if (spawnWarmupDelay > 0)
            {
                yield return new WaitForSeconds(spawnWarmupDelay);
            }

            while (_playerInZone && player != null)
            {
                _currentDepth = CalculatePlayerDepth();
                _currentIntensity = intensityCurve.Evaluate(_currentDepth);

                // Spawn interval decreases with intensity
                float spawnInterval = Mathf.Lerp(maxSpawnInterval, minSpawnInterval, _currentIntensity);

                // Meteors per wave increases with intensity
                float meteorsFloat = Mathf.Lerp(1, maxMeteorsPerWave, meteorsPerWaveCurve.Evaluate(_currentIntensity));
                int meteorsThisWave = Mathf.Max(1, Mathf.RoundToInt(meteorsFloat));

                if (debugLog)
                {
                    Debug.Log($"[HazardZoneMeteors] Depth: {_currentDepth:F2}, Intensity: {_currentIntensity:F2}, " +
                              $"Interval: {spawnInterval:F2}s, Meteors: {meteorsThisWave}");
                }

                // Spawn meteors
                for (int i = 0; i < meteorsThisWave; i++)
                {
                    TrySpawnMeteor();
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private void TrySpawnMeteor()
        {
            if (meteorStrikePrefab == null || player == null) return;

            Vector3 spawnPosition = GetSpawnPosition();
            Instantiate(meteorStrikePrefab, spawnPosition, Quaternion.identity);

            if (debugLog)
            {
                Debug.Log($"[HazardZoneMeteors] Spawned meteor at {spawnPosition}");
            }
        }

        private Vector3 GetSpawnPosition()
        {
            // Calculate current accuracy (may scale with depth)
            float currentAccuracy = targetingAccuracy;
            if (accuracyScalesWithDepth)
            {
                currentAccuracy = Mathf.Lerp(targetingAccuracy * 0.5f, 1f, _currentIntensity);
            }

            Vector3 spawnPos;

            if (targetPlayer && Random.value < currentAccuracy)
            {
                // Targeted spawn: closer to player
                float offsetRange = Mathf.Lerp(maxSpawnDistance, minSpawnDistance, currentAccuracy);
                Vector2 randomCircle = Random.insideUnitCircle * offsetRange;
                
                spawnPos = player.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            }
            else
            {
                // Random spawn within ring around player
                float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance
                );

                spawnPos = player.position + offset;
            }

            // Ensure position is in zone, otherwise just use player area
            if (!IsPositionInZone(spawnPos))
            {
                spawnPos = player.position + new Vector3(
                    Random.Range(-minSpawnDistance, minSpawnDistance),
                    0f,
                    Random.Range(-minSpawnDistance, minSpawnDistance)
                );
            }

            spawnPos.y = 0f; // Ground level
            return spawnPos;
        }

        #endregion

        #region Fire Damage

        private IEnumerator FireDamageRoutine()
        {
            while (_playerInZone && _playerHealth != null)
            {
                // Calculate damage with depth scaling
                float depthMultiplier = Mathf.Lerp(1f, maxDepthFireDamageMultiplier, _currentIntensity);
                
                // Calculate resistance reduction
                float resistanceMultiplier = 1f;
                if (_playerAbilities != null)
                {
                    int stacks = _playerAbilities.HazardResistanceStacks;
                    resistanceMultiplier = Mathf.Max(0.25f, 1f - (stacks * 0.25f));
                }

                int finalDamage = Mathf.RoundToInt(fireDamagePerTick * depthMultiplier * resistanceMultiplier);

                if (finalDamage > 0)
                {
                    _playerHealth.TakeDamage(finalDamage);

                    if (debugLog)
                    {
                        Debug.Log($"[HazardZoneMeteors] Fire damage: {finalDamage} " +
                                  $"(depth mult: {depthMultiplier:F2}, resist mult: {resistanceMultiplier:F2})");
                    }
                }

                yield return new WaitForSeconds(fireDamageTickInterval);
            }
        }

        #endregion

        #region Depth Calculation

        private float CalculatePlayerDepth()
        {
            if (player == null) return 0f;

            Vector3 localPos = transform.InverseTransformPoint(player.position);
            Vector3 center = _zoneCollider.center;
            Vector3 size = _zoneCollider.size;

            float t = 0f;

            switch (depthAxis)
            {
                case DepthAxis.Z:
                    t = Mathf.InverseLerp(center.z - size.z * 0.5f, center.z + size.z * 0.5f, localPos.z);
                    break;
                case DepthAxis.X:
                    t = Mathf.InverseLerp(center.x - size.x * 0.5f, center.x + size.x * 0.5f, localPos.x);
                    break;
                case DepthAxis.Y:
                    t = Mathf.InverseLerp(center.y - size.y * 0.5f, center.y + size.y * 0.5f, localPos.y);
                    break;
            }

            if (invertDepthDirection)
            {
                t = 1f - t;
            }

            return Mathf.Clamp01(t);
        }

        private bool IsPositionInZone(Vector3 worldPosition)
        {
            if (_zoneCollider == null) return false;
            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            Bounds bounds = new Bounds(_zoneCollider.center, _zoneCollider.size);
            return bounds.Contains(localPos);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            if (_zoneCollider == null) _zoneCollider = GetComponent<BoxCollider>();
            if (_zoneCollider == null) return;

            // Zone bounds
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
            Gizmos.DrawCube(_zoneCollider.center, _zoneCollider.size);
            Gizmos.color = new Color(1f, 0.3f, 0f, 1f);
            Gizmos.DrawWireCube(_zoneCollider.center, _zoneCollider.size);
            Gizmos.matrix = Matrix4x4.identity;

            // Advance direction arrow
            Gizmos.color = Color.yellow;
            Vector3 center = transform.TransformPoint(_zoneCollider.center);
            Gizmos.DrawRay(center, advanceDirection * 5f);

            // Spawn rings around player
            if (showSpawnRing && player != null)
            {
                Gizmos.color = Color.green;
                DrawCircle(player.position, minSpawnDistance, 24);
                Gizmos.color = Color.cyan;
                DrawCircle(player.position, maxSpawnDistance, 32);

                // Intensity indicator
                if (Application.isPlaying && _playerInZone)
                {
                    Gizmos.color = Color.Lerp(Color.green, Color.red, _currentIntensity);
                    Gizmos.DrawWireSphere(player.position, 0.5f + _currentIntensity);
                }
            }
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        #endregion
    }
}
