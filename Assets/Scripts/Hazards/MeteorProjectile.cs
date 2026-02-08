using UnityEngine;

namespace Hazards
{
    /// <summary>
    /// A clean, reliable falling meteor projectile.
    /// Moves from spawn position toward a target position over a specified duration.
    /// Automatically cleans up on arrival.
    /// </summary>
    public class MeteorProjectile : MonoBehaviour
    {
        [Header("Visual Options")]
        [SerializeField, Tooltip("If true, the meteor will rotate while falling.")]
        private bool rotateWhileFalling = true;
        
        [SerializeField, Tooltip("Rotation speed in degrees per second.")]
        private float rotationSpeed = 360f;
        
        [SerializeField, Tooltip("Axis to rotate around (local space).")]
        private Vector3 rotationAxis = Vector3.right;

        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;

        // Runtime state
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _travelDuration;
        private float _elapsedTime;
        private bool _isInitialized;
        private bool _hasArrived;
        
        // Cached components for pooling
        private TrailRenderer[] _trails;
        private ParticleSystem[] _particles;
        
        private void Awake()
        {
            // Cache trail and particle components for pooling
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }
        
        private void OnEnable()
        {
            // Reset state for pooling
            _isInitialized = false;
            _hasArrived = false;
            _elapsedTime = 0f;
            
            // Clear all trails to prevent visual streaks from previous position
            if (_trails != null)
            {
                foreach (var trail in _trails)
                {
                    if (trail != null) trail.Clear();
                }
            }
            
            // Stop and clear all particle systems
            if (_particles != null)
            {
                foreach (var ps in _particles)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
        }

        /// <summary>
        /// Initialize the meteor with its target and travel time.
        /// </summary>
        /// <param name="targetPosition">World position to fall toward (should be ground level).</param>
        /// <param name="travelDuration">Time in seconds to reach the target.</param>
        public void Initialize(Vector3 targetPosition, float travelDuration)
        {
            _startPosition = transform.position;
            _targetPosition = targetPosition;
            _travelDuration = Mathf.Max(0.01f, travelDuration);
            _elapsedTime = 0f;
            _isInitialized = true;
            _hasArrived = false;

            // Clear trails again after position is set (in case OnEnable ran before positioning)
            if (_trails != null)
            {
                foreach (var trail in _trails)
                {
                    if (trail != null) trail.Clear();
                }
            }
            
            // Start particle systems (they were stopped in OnEnable)
            if (_particles != null)
            {
                foreach (var ps in _particles)
                {
                    if (ps != null) ps.Play();
                }
            }
        }

        /// <summary>
        /// Alternative initialization with speed instead of duration.
        /// Calculates duration based on distance and speed.
        /// </summary>
        /// <param name="targetPosition">World position to fall toward.</param>
        /// <param name="speed">Speed in units per second.</param>
        /// <param name="useSpeed">Set to true to use speed-based calculation.</param>
        public void Initialize(Vector3 targetPosition, float speed, bool useSpeed)
        {
            if (!useSpeed)
            {
                Initialize(targetPosition, speed);
                return;
            }

            float distance = Vector3.Distance(transform.position, targetPosition);
            float duration = distance / Mathf.Max(0.1f, speed);
            Initialize(targetPosition, duration);
        }

        private void Update()
        {
            if (!_isInitialized || _hasArrived) return;

            _elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsedTime / _travelDuration);

            // Smooth interpolation toward target
            transform.position = Vector3.Lerp(_startPosition, _targetPosition, t);

            // Optional rotation for visual effect
            if (rotateWhileFalling)
            {
                transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
            }

            // Check for arrival
            if (t >= 1f)
            {
                _hasArrived = true;
                OnArrival();
            }
        }

        /// <summary>
        /// Called when the meteor reaches its target.
        /// Override in subclass for custom behavior, or listen for this via events.
        /// </summary>
        protected virtual void OnArrival()
        {
            // Default behavior: just mark as arrived
            // The MeteorStrike will handle destruction
        }

        /// <summary>
        /// Check if the meteor has reached its target.
        /// </summary>
        public bool HasArrived => _hasArrived;

        /// <summary>
        /// Get the target position this meteor is falling toward.
        /// </summary>
        public Vector3 TargetPosition => _targetPosition;

        /// <summary>
        /// Get normalized progress (0 = just spawned, 1 = arrived).
        /// </summary>
        public float Progress => _isInitialized ? Mathf.Clamp01(_elapsedTime / _travelDuration) : 0f;

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            if (_isInitialized)
            {
                // Draw path from start to target
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_startPosition, _targetPosition);
                Gizmos.DrawWireSphere(_targetPosition, 0.3f);

                // Draw current position
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
            else
            {
                // Before initialization, show down direction
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, Vector3.down * 5f);
            }
        }
    }
}
