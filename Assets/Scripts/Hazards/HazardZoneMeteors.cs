using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hazards
{
    /// <summary>
    /// Controls the Hazard Zone that prevents player backtracking.
    /// Spawns meteors with intensity based on player depth.
    /// All rates use "per second" format for easy configuration.
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
        private bool expandWhenPlayerInside = false;
        
        [SerializeField, Tooltip("Expansion speed (units per second).")]
        private float expansionSpeed = 1.0f;
        
        [SerializeField, Tooltip("Maximum zone size (original size multiplier).")]
        private float maxExpansionMultiplier = 3.0f;

        [Header("=== EARLY WARNING ===")]
        [SerializeField, Tooltip("Start spawning meteors when player is this close to the zone edge.")]
        private float earlyWarningDistance = 10f;
        
        [SerializeField, Tooltip("Meteors per second during early warning phase.")]
        private float earlyWarningMeteorsPerSecond = 0.5f;

        [Header("=== METEOR SPAWNING (Per Second) ===")]
        [SerializeField, Tooltip("Meteors spawned per second at MINIMUM depth (edge of zone).")]
        private float minMeteorsPerSecond = 0.5f;
        
        [SerializeField, Tooltip("Meteors spawned per second at MAXIMUM depth (deep in zone).")]
        private float maxMeteorsPerSecond = 5f;
        
        [SerializeField, Tooltip("Curve controlling spawn rate based on depth (0=edge, 1=deep).")]
        private AnimationCurve spawnRateCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [SerializeField, Tooltip("Randomness factor for spawn intervals (0 = strict rhythm, 1 = chaotic).")]
        [Range(0f, 1f)]
        private float spawnRandomness = 0.4f;

        [Header("=== SPAWN DISTANCE ===")]
        [SerializeField, Tooltip("Minimum distance from player to spawn meteors.")]
        private float minSpawnDistance = 1.5f;
        
        [SerializeField, Tooltip("Maximum distance from player to spawn meteors.")]
        private float maxSpawnDistance = 12.0f;

        [Header("=== PLAYER TARGETING ===")]
        [SerializeField, Tooltip("Enable targeted spawning toward player.")]
        private bool targetPlayer = true;
        
        [SerializeField, Tooltip("Targeting accuracy (0=random, 1=on player).")]
        [Range(0f, 1f)]
        private float targetingAccuracy = 0.5f;
        
        [SerializeField, Tooltip("Increase accuracy as depth increases.")]
        private bool accuracyScalesWithDepth = true;

        [Header("=== FIRE DAMAGE ===")]
        [SerializeField, Tooltip("Apply fire damage while player is in zone (handled by PlayerHealth).")]
        private bool applyFireDamage = true;
        
        [SerializeField, Tooltip("Fire damage multiplier at maximum depth (passed to PlayerHealth).")]
        private float maxDepthFireDamageMultiplier = 2.0f;
        
        [Header("=== POST PROCESS VOLUME ===")]
        [SerializeField, Tooltip("Post-process volume to control based on depth.")]
        private Volume hazardVolume;
        
        [SerializeField, Tooltip("Volume weight at zone edge (depth = 0).")]
        [Range(0f, 1f)]
        private float volumeWeightMin = 0.3f;
        
        [SerializeField, Tooltip("Volume weight at maximum depth (depth = 1).")]
        [Range(0f, 1f)]
        private float volumeWeightMax = 1.0f;

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
        private bool _playerInZone;
        private bool _playerInEarlyWarning;
        private float _currentDepth;
        private float _currentIntensity;
        private float _advanceTimer;
        private float _totalDistanceMoved;
        private float _nextSpawnTime; // Replaces meteorAccumulator
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
            _nextSpawnTime = 0f;

            advanceDirection = advanceDirection.normalized;

            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    player = playerObj.transform;
                }
            }

            if (player == null)
            {
                Debug.LogError("[HazardZoneMeteors] No player found!");
                return;
            }
            
            // Find PlayerHealth - check self, children, then parent
            _playerHealth = player.GetComponent<PlayerHealth>();
            if (_playerHealth == null)
                _playerHealth = player.GetComponentInChildren<PlayerHealth>();
            if (_playerHealth == null)
                _playerHealth = player.GetComponentInParent<PlayerHealth>();
            
            // Find PlayerAbilities similarly
            _playerAbilities = player.GetComponent<PlayerAbilities>();
            if (_playerAbilities == null)
                _playerAbilities = player.GetComponentInChildren<PlayerAbilities>();
            if (_playerAbilities == null)
                _playerAbilities = player.GetComponentInParent<PlayerAbilities>();
            
            if (_playerHealth == null)
            {
                Debug.LogError("[HazardZoneMeteors] PlayerHealth component not found on player or its hierarchy!");
            }
        }

        private void Update()
        {
            if (player == null) return;

            HandleZoneAdvance();

            // Check player states
            bool wasInZone = _playerInZone;
            bool wasInEarlyWarning = _playerInEarlyWarning;
            
            _playerInZone = IsPositionInZone(player.position);
            _playerInEarlyWarning = !_playerInZone && GetDistanceToZone(player.position) <= earlyWarningDistance;

            // State transitions
            if (_playerInZone && !wasInZone)
            {
                OnPlayerEnterZone();
            }
            else if (!_playerInZone && wasInZone)
            {
                OnPlayerExitZone();
            }

            // Early warning transitions
            if (_playerInEarlyWarning && !wasInEarlyWarning && !_playerInZone)
            {
                OnPlayerEnterEarlyWarning();
            }
            else if (!_playerInEarlyWarning && wasInEarlyWarning && !_playerInZone)
            {
                OnPlayerExitEarlyWarning();
            }

            // Zone expansion
            if (_playerInZone && expandWhenPlayerInside)
            {
                HandleZoneExpansion();
            }

            // Update depth/intensity and fire damage multiplier
            if (_playerInZone)
            {
                _currentDepth = CalculatePlayerDepth();
                _currentIntensity = spawnRateCurve.Evaluate(_currentDepth);
                
                // Update fire damage multiplier based on depth (handled by PlayerHealth)
                if (applyFireDamage && _playerHealth != null)
                {
                    float depthMultiplier = Mathf.Lerp(1f, maxDepthFireDamageMultiplier, _currentIntensity);
                    
                    // Apply hazard resistance
                    float resistanceMultiplier = 1f;
                    if (_playerAbilities != null)
                    {
                        int stacks = _playerAbilities.HazardResistanceStacks;
                        resistanceMultiplier = Mathf.Max(0.25f, 1f - (stacks * 0.25f));
                    }
                    
                    _playerHealth.SetFireDamageMultiplier(depthMultiplier * resistanceMultiplier);
                }
                
                // Update post-process volume weight based on depth
                if (hazardVolume != null)
                {
                    hazardVolume.weight = Mathf.Lerp(volumeWeightMin, volumeWeightMax, _currentDepth);
                }
            }

            // Handle meteor spawning (continuous per-second approach)
            HandleMeteorSpawning();
        }

        #endregion

        #region Zone Movement & Expansion

        private void HandleZoneAdvance()
        {
            if (_advanceTimer > 0)
            {
                _advanceTimer -= Time.deltaTime;
                return;
            }

            float moveAmount = zoneAdvanceSpeed * Time.deltaTime;
            transform.position += advanceDirection * moveAmount;
            _totalDistanceMoved += moveAmount;

            if (debugLog && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[HazardZone] Speed: {zoneAdvanceSpeed} u/s, Moved: {_totalDistanceMoved:F1}u");
            }
        }

        public void SetSpeed(float speed)
        {
            zoneAdvanceSpeed = speed;
            if (debugLog) Debug.Log($"[HazardZone] Speed set to: {speed}");
        }

        private void HandleZoneExpansion()
        {
            if (!expandWhenPlayerInside) return;

            Vector3 currentSize = _zoneCollider.size;
            Vector3 maxSize = _originalSize * maxExpansionMultiplier;
            float expansion = expansionSpeed * Time.deltaTime;

            switch (depthAxis)
            {
                case DepthAxis.Z:
                    if (currentSize.z < maxSize.z)
                        currentSize.z = Mathf.Min(currentSize.z + expansion, maxSize.z);
                    break;
                case DepthAxis.X:
                    if (currentSize.x < maxSize.x)
                        currentSize.x = Mathf.Min(currentSize.x + expansion, maxSize.x);
                    break;
                case DepthAxis.Y:
                    if (currentSize.y < maxSize.y)
                        currentSize.y = Mathf.Min(currentSize.y + expansion, maxSize.y);
                    break;
            }

            _zoneCollider.size = currentSize;
        }

        #endregion

        #region Player Zone State

        private void OnPlayerEnterZone()
        {
            if (debugLog) Debug.Log("[HazardZone] Player ENTERED zone");

            // Fire damage is now handled centrally by PlayerHealth
            if (applyFireDamage && _playerHealth != null)
            {
                _playerHealth.SetOnFire(true);
            }
        }

        private void OnPlayerExitZone()
        {
            if (debugLog) Debug.Log("[HazardZone] Player EXITED zone");

            if (_playerHealth != null)
            {
                _playerHealth.SetOnFire(false);
            }
            
            // Reset volume weight when exiting zone
            if (hazardVolume != null)
            {
                hazardVolume.weight = 0f;
            }
        }

        private void OnPlayerEnterEarlyWarning()
        {
            if (debugLog) Debug.Log("[HazardZone] Player entered EARLY WARNING range");
        }

        private void OnPlayerExitEarlyWarning()
        {
            if (debugLog) Debug.Log("[HazardZone] Player exited early warning range");
        }

        #endregion

        #region Meteor Spawning

        private void HandleMeteorSpawning()
        {
            if (meteorStrikePrefab == null || player == null) return;

            float meteorsPerSecond = 0f;
            bool isActive = false;

            if (_playerInZone)
            {
                // Inside zone: scale with depth
                meteorsPerSecond = Mathf.Lerp(minMeteorsPerSecond, maxMeteorsPerSecond, _currentIntensity);
                isActive = true;
            }
            else if (_playerInEarlyWarning)
            {
                // Early warning phase
                meteorsPerSecond = earlyWarningMeteorsPerSecond;
                isActive = true;
            }
            // else: Not in range

            if (!isActive || meteorsPerSecond <= 0.001f)
            {
                // Push timer into future so we don't spawn instantly upon re-entry
                // Keeps it "ready" but creates a small delay
                _nextSpawnTime = Time.time + (1f / Mathf.Max(minMeteorsPerSecond, 0.1f));
                return;
            }

            // Check if it's time to spawn
            if (Time.time >= _nextSpawnTime)
            {
                SpawnMeteor();

                // Calculate next interval with randomness
                float baseInterval = 1f / meteorsPerSecond;
                
                // Apply randomness: e.g. randomness 0.4 => multiplier [0.6, 1.4]
                float randomFactor = Random.Range(1f - spawnRandomness, 1f + spawnRandomness);
                float nextInterval = baseInterval * randomFactor;

                _nextSpawnTime = Time.time + nextInterval;

                if (debugLog && _playerInZone && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[HazardZone] Depth: {_currentDepth:F2}, Rate: {meteorsPerSecond:F1}/s, Next in: {nextInterval:F3}s");
                }
            }
        }

        private void SpawnMeteor()
        {
            Vector3 spawnPos = GetSpawnPosition();
            
            // Use object pool instead of Instantiate for zero-allocation spawning
            GameObject meteor = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.Get(meteorStrikePrefab, spawnPos, Quaternion.identity)
                : Instantiate(meteorStrikePrefab, spawnPos, Quaternion.identity);

            if (debugLog)
            {
                Debug.Log($"[HazardZone] Spawned meteor at {spawnPos}");
            }
        }

        private Vector3 GetSpawnPosition()
        {
            const int maxAttempts = 10;
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 candidate = GenerateCandidatePosition();
                
                // Validate position is inside zone
                if (IsPositionInZone(candidate))
                {
                    candidate.y = 0f;
                    return candidate;
                }
            }

            // Fallback: random position within zone bounds
            return GetRandomPositionInZone();
        }

        private Vector3 GenerateCandidatePosition()
        {
            float currentAccuracy = targetingAccuracy;
            if (accuracyScalesWithDepth && _playerInZone)
            {
                currentAccuracy = Mathf.Lerp(targetingAccuracy * 0.5f, 1f, _currentIntensity);
            }

            Vector3 spawnPos;

            if (targetPlayer && Random.value < currentAccuracy)
            {
                float offsetRange = Mathf.Lerp(maxSpawnDistance, minSpawnDistance, currentAccuracy);
                Vector2 randomCircle = Random.insideUnitCircle * offsetRange;
                spawnPos = player.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            }
            else
            {
                float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                spawnPos = player.position + offset;
            }

            return spawnPos;
        }

        private Vector3 GetRandomPositionInZone()
        {
            Bounds bounds = _zoneCollider.bounds;
            Vector3 randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                0f,
                Random.Range(bounds.min.z, bounds.max.z)
            );
            return randomPos;
        }

        #endregion

        #region Helpers

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

            if (invertDepthDirection) t = 1f - t;
            return Mathf.Clamp01(t);
        }

        private bool IsPositionInZone(Vector3 worldPosition)
        {
            if (_zoneCollider == null) return false;
            
            // Project to XZ plane (ignore Y) for ground-level zone detection
            // This ensures player is detected even if their Y position differs from collider height
            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            Vector3 center = _zoneCollider.center;
            Vector3 halfSize = _zoneCollider.size * 0.5f;
            
            // Check only X and Z axes
            bool inX = localPos.x >= center.x - halfSize.x && localPos.x <= center.x + halfSize.x;
            bool inZ = localPos.z >= center.z - halfSize.z && localPos.z <= center.z + halfSize.z;
            
            return inX && inZ;
        }

        private float GetDistanceToZone(Vector3 worldPosition)
        {
            if (_zoneCollider == null) return float.MaxValue;
            Vector3 closestPoint = _zoneCollider.ClosestPoint(worldPosition);
            return Vector3.Distance(worldPosition, closestPoint);
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

            // Early warning zone (larger)
            if (earlyWarningDistance > 0)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Vector3 warningSize = _zoneCollider.size + Vector3.one * earlyWarningDistance * 2;
                Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
                Gizmos.DrawWireCube(_zoneCollider.center, warningSize);
                Gizmos.matrix = Matrix4x4.identity;
            }

            // Advance direction
            Gizmos.color = Color.yellow;
            Vector3 center = transform.TransformPoint(_zoneCollider.center);
            Gizmos.DrawRay(center, advanceDirection * 5f);

            // Spawn rings
            if (showSpawnRing && player != null)
            {
                Gizmos.color = Color.green;
                DrawCircle(player.position, minSpawnDistance, 24);
                Gizmos.color = Color.cyan;
                DrawCircle(player.position, maxSpawnDistance, 32);

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
