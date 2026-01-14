using UnityEngine;
using Audio;

namespace Audio
{
    /// <summary>
    /// Plays ambient sounds when player enters the zone.
    /// Attach to any GameObject with a trigger collider.
    /// Good for: rivers, lava, wind zones, fire areas.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AmbientSoundZone : MonoBehaviour
    {
        [Header("Sound")]
        [SerializeField, Tooltip("Ambient sound to play (should be a looping clip)")]
        private SoundEvent ambientSound;
        
        [SerializeField, Tooltip("Volume multiplier for this zone")]
        [Range(0f, 1f)]
        private float volumeMultiplier = 1f;
        
        [Header("Fade Settings")]
        [SerializeField, Tooltip("Fade in/out duration when entering/exiting")]
        private float fadeDuration = 0.5f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private AudioSource _audioSource;
        private float _targetVolume = 0f;
        private float _currentVolume = 0f;
        
        private void Start()
        {
            // Ensure collider is set to trigger
            var collider = GetComponent<Collider>();
            if (!collider.isTrigger)
            {
                if (debugLog) Debug.LogWarning($"[AmbientSoundZone] Collider on {gameObject.name} should be a trigger!");
                collider.isTrigger = true;
            }
            
            // Create dedicated AudioSource for this zone
            if (ambientSound != null && ambientSound.IsValid)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.clip = ambientSound.clips[0];
                _audioSource.loop = true;
                _audioSource.playOnAwake = false;
                _audioSource.volume = 0f;
                _audioSource.spatialBlend = ambientSound.spatialBlend;
                _audioSource.minDistance = ambientSound.minDistance;
                _audioSource.maxDistance = ambientSound.maxDistance;
                
                // Assign mixer group if set
                if (ambientSound.mixerGroup != null)
                    _audioSource.outputAudioMixerGroup = ambientSound.mixerGroup;
            }
        }
        
        private void Update()
        {
            if (_audioSource == null) return;
            
            // Smooth fade
            if (Mathf.Abs(_currentVolume - _targetVolume) > 0.001f)
            {
                float fadeSpeed = (fadeDuration > 0) ? (1f / fadeDuration) : 100f;
                _currentVolume = Mathf.MoveTowards(_currentVolume, _targetVolume, fadeSpeed * Time.deltaTime);
                _audioSource.volume = _currentVolume * ambientSound.volume * volumeMultiplier;
            }
            
            // Stop audio when fully faded out
            if (_currentVolume <= 0.001f && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            _targetVolume = 1f;
            
            // Start playing if not already
            if (_audioSource != null && !_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
            
            if (debugLog) Debug.Log($"[AmbientSoundZone] Player entered {gameObject.name}");
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            _targetVolume = 0f;
            
            if (debugLog) Debug.Log($"[AmbientSoundZone] Player exited {gameObject.name}");
        }
        
        private void OnDisable()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
        }
    }
}
