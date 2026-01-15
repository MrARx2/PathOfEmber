using UnityEngine;
using Audio;

namespace Audio
{
    /// <summary>
    /// Volumetric ambient sound zone.
    /// Uses a single audio source that follows the player, with volume calculated based on
    /// how deep the player is inside the zone.
    /// </summary>
    public class AmbientSoundZone : MonoBehaviour
    {
        [Header("Sound")]
        [SerializeField, Tooltip("The ambient sound to play (should be looping)")]
        private SoundEvent ambientSound;
        
        [SerializeField, Range(0f, 1f), Tooltip("Max volume at the center of the zone")]
        private float maxVolume = 1f;
        
        [SerializeField, Tooltip("Fade in/out duration in seconds")]
        private float fadeDuration = 0.5f;
        
        [Header("Zone Size")]
        [SerializeField, Tooltip("Width of the zone (X axis)")]
        private float width = 20f;
        
        [SerializeField, Tooltip("Height of the zone (Y axis)")]
        private float height = 10f;
        
        [SerializeField, Tooltip("Depth of the zone (Z axis - player progression)")]
        private float depth = 30f;
        
        [Header("Player")]
        [SerializeField, Tooltip("Drag your player here (or leave empty to auto-find)")]
        private Transform player;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(0f, 0.8f, 1f, 0.3f);
        
        private AudioSource _audioSource;
        private float _targetVolume = 0f;
        private float _currentVolume = 0f;
        private bool _isInZone = false;
        
        private void Start()
        {
            // Find player if not assigned
            if (player == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    // Look for "Main Character" child (has correct world position)
                    var mainChar = playerObj.transform.Find("Main Character");
                    player = mainChar != null ? mainChar : playerObj.transform;
                }
            }
            
            CreateAudioSource();
        }
        
        private void CreateAudioSource()
        {
            if (ambientSound == null || !ambientSound.IsValid) return;
            
            // Create a child object for the sound that will follow the player
            GameObject sourceObj = new GameObject("AmbientSource_Follower");
            sourceObj.transform.parent = transform;
            sourceObj.transform.localPosition = Vector3.zero;
            
            _audioSource = sourceObj.AddComponent<AudioSource>();
            _audioSource.clip = ambientSound.clips[0];
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0f;
            
            // We use spatial blend from the event, but since we follow the player,
            // 3D attenuation effectively won't happen. We control volume manually.
            _audioSource.spatialBlend = ambientSound.spatialBlend; 
            _audioSource.minDistance = 1f; 
            _audioSource.maxDistance = 500f; // Large logical distance
            
            if (ambientSound.mixerGroup != null)
                _audioSource.outputAudioMixerGroup = ambientSound.mixerGroup;
        }

        private float _debugTimer;

        private void Update()
        {
            if (_audioSource == null || player == null) return;
            
            // 1. Move AudioSource to match Player position (Volumetric effect)
            _audioSource.transform.position = player.position;
            
            // 2. Calculate Zone Logic
            Vector3 center = transform.position;
            Vector3 pos = player.position;
            
            // Calculate distance from center per axis (0 = center, 1 = edge)
            float distFactorX = Mathf.Abs(pos.x - center.x) / (width / 2f);
            float distFactorY = Mathf.Abs(pos.y - center.y) / (height / 2f);
            float distFactorZ = Mathf.Abs(pos.z - center.z) / (depth / 2f);
            
            // Check if inside
            bool isInside = distFactorX <= 1f && distFactorY <= 1f && distFactorZ <= 1f;
            
            // State tracking
            if (isInside && !_isInZone)
            {
                _isInZone = true;
                if (!_audioSource.isPlaying) _audioSource.Play();
            }
            else if (!isInside && _isInZone)
            {
                _isInZone = false;
            }
            
            // 3. Calculate Volume Intensity
            // User request: "based on the depth only"
            // We ignore X and Y factors for intensity (they just act as bounds checks)
            // Volume is max at Z-center, fades to 0 at Z-edges.
            
            if (_isInZone)
            {
                // Simple linear falloff based ONLY on Depth (Z)
                float volumeIntensity = Mathf.Clamp01(1f - distFactorZ);
                
                // Square it for a smoother curve (stay louder longer in middle)
                // volumeIntensity = volumeIntensity * volumeIntensity; 
                
                _targetVolume = volumeIntensity;
            }
            else
            {
                _targetVolume = 0f;
            }
            
            // 4. Smooth Fade
            if (Mathf.Abs(_currentVolume - _targetVolume) > 0.001f)
            {
                float speed = fadeDuration > 0 ? 1f / fadeDuration : 100f;
                _currentVolume = Mathf.MoveTowards(_currentVolume, _targetVolume, speed * Time.unscaledDeltaTime);
            }
            
            // Apply
            _audioSource.volume = _currentVolume * ambientSound.volume * maxVolume;
            
            // Stop if silent
            if (_currentVolume <= 0.001f && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            
            // Debug
            if (debugLog)
            {
                _debugTimer += Time.unscaledDeltaTime;
                if (_debugTimer >= 1f)
                {
                    _debugTimer = 0f;
                    Debug.Log($"[AmbientSoundZone] {gameObject.name}: InZone={isInside}, Vol={_currentVolume:F2}, Target={_targetVolume:F2}");
                }
            }
        }
        
        private void OnDisable()
        {
            if (_audioSource != null) _audioSource.Stop();
            _isInZone = false;
        }
        
        #region Gizmos
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            DrawZone(false);
        }
        
        private void OnDrawGizmosSelected()
        {
            DrawZone(true);
        }
        
        private void DrawZone(bool selected)
        {
            Vector3 size = new Vector3(width, height, depth);
            
            // Logic visualize
            Color fillColor = gizmoColor;
            
            // Check player visually
            #if UNITY_EDITOR
            if (Application.isPlaying && player != null)
            {
                Vector3 center = transform.position;
                Vector3 pos = player.position;
                float distFactorX = Mathf.Abs(pos.x - center.x) / (width / 2f);
                float distFactorY = Mathf.Abs(pos.y - center.y) / (height / 2f);
                float distFactorZ = Mathf.Abs(pos.z - center.z) / (depth / 2f);
                bool inside = distFactorX <= 1f && distFactorY <= 1f && distFactorZ <= 1f;
                
                if (inside)
                {
                    float maxDist = Mathf.Max(distFactorX, Mathf.Max(distFactorY, distFactorZ));
                    float intensity = Mathf.Clamp01(1f - maxDist);
                    // Green gets brighter as intensity increases
                    fillColor = Color.Lerp(gizmoColor, Color.green, intensity * 0.8f);
                }
            }
            #endif
            
            Gizmos.color = fillColor;
            Gizmos.DrawCube(transform.position, size);
            
            Gizmos.color = selected ? Color.white : gizmoColor * 2f;
            Gizmos.DrawWireCube(transform.position, size);
            
            #if UNITY_EDITOR
            string label = $"{gameObject.name}\n{width}x{height}x{depth}";
            if (Application.isPlaying)
                label += $"\nVol: {_currentVolume:F2}";
                
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (height/2 + 1), label, style);
            #endif
        }
        #endregion
        
        #region Context Menu
        [ContextMenu("Force Play")]
        public void ForcePlay()
        {
            if (_audioSource != null && !_audioSource.isPlaying)
            {
                _audioSource.Play();
                _currentVolume = 1f;
            }
        }
        
        [ContextMenu("Force Stop")]
        public void ForceStop()
        {
            if (_audioSource != null) _audioSource.Stop();
            _currentVolume = 0f;
        }
        #endregion
    }
}
