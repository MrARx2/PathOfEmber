using UnityEngine;
using Audio;

namespace Audio
{
    /// <summary>
    /// Procedural wind audio that reacts to player movement speed.
    /// Modulates Volume, Pitch, and Low-Pass Filter to create a physical "air rush" sensation.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(AudioLowPassFilter))]
    public class PlayerWindAudio : MonoBehaviour
    {
        [Header("Sound Settings")]
        [SerializeField, Tooltip("Looping wind noise sound event")]
        private SoundEvent windSound;
        
        [Header("Speed Definition")]
        [SerializeField, Tooltip("Speed where wind is weakest (Idle)")]
        private float minSpeed = 0f;
        
        [SerializeField, Tooltip("Speed where wind is strongest (Sprint)")]
        private float maxSpeed = 10f;
        
        [Header("Volume Curve (The Sensation)")]
        [SerializeField, Tooltip("Volume at Min Speed (Background ambience)")]
        private float minVolume = 0.05f;
        
        [SerializeField, Tooltip("Volume at Max Speed (Full rush)")]
        private float maxVolume = 0.2f;
        
        [Header("Responsiveness")]
        [SerializeField, Tooltip("Build-up time (Attack). Keep high (1.0s+) for smooth feeling.")]
        private float attackTime = 1.0f;
        
        [SerializeField, Tooltip("Drop-off time (Release). Keep low (0.1s) for snappy stopping.")]
        private float releaseTime = 0.1f;
        
        [Header("Pitch Modulation")]
        [SerializeField] private float minPitch = 0.9f;
        [SerializeField] private float maxPitch = 1.15f;
        
        [Header("Filter Modulation (The Clean Factor)")]
        [SerializeField, Tooltip("Low cutoff at idle (Muffled/Warm)")]
        private float minCutoff = 800f;
        
        [SerializeField, Tooltip("High cutoff at max speed. keep < 6000 to avoid 'white noise'.")]
        private float maxCutoff = 6000f;
        
        [SerializeField, Tooltip("Filter Resonance (Q). Higher values (2-5) make it 'whistle' more and 'hiss' less.")]
        private float resonance = 1.0f;
        
        [Tooltip("Control the feel of the wind ramp-up")]
        [SerializeField] private AnimationCurve responseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private AudioSource _source;
        private AudioLowPassFilter _filter;
        private Rigidbody _rb;
        private float _currentSpeed = 0f;
        private float _velocityRef; // For SmoothDamp

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _filter = GetComponent<AudioLowPassFilter>();
            _rb = GetComponentInParent<Rigidbody>(); // Usually on parent for player
            
            // Setup Source defaults for 2D ambient
            _source.spatialBlend = 0f; // 2D Sound
            _source.loop = true;
            _source.playOnAwake = false;
        }

        private void Start()
        {
            if (windSound != null && windSound.IsValid)
            {
                _source.clip = windSound.clips[0];
                if (windSound.mixerGroup != null) _source.outputAudioMixerGroup = windSound.mixerGroup;
                
                // Initialize to min volume/pitch/filter immediately to avoid 1-frame "blast" at full volume
                _source.volume = minVolume;
                _source.pitch = minPitch;
                if (_filter != null)
                {
                    _filter.cutoffFrequency = minCutoff;
                    _filter.lowpassResonanceQ = resonance;
                }
                
                _source.Play();
            }
        }

        private void Update()
        {
            // Optimize: Update resonance only when changed in inspector
            if (_filter != null && Mathf.Abs(_filter.lowpassResonanceQ - resonance) > 0.01f)
            {
                _filter.lowpassResonanceQ = resonance;
            }

            // Get raw speed
            float rawSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            
            // Asymmetric Smoothing
            float targetSmooth = (rawSpeed > _currentSpeed) ? attackTime : releaseTime;
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, rawSpeed, ref _velocityRef, targetSmooth);
            
            // Normalize speed 0-1
            float t = Mathf.InverseLerp(minSpeed, maxSpeed, _currentSpeed);
            
            // Apply Response Curve
            float curvedT = responseCurve.Evaluate(t);
            
            // 1. Volume
            float newVol = Mathf.Lerp(minVolume, maxVolume, curvedT);
            if (Mathf.Abs(_source.volume - newVol) > 0.001f)
                _source.volume = newVol;
            
            // 2. Pitch
            float newPitch = Mathf.Lerp(minPitch, maxPitch, curvedT);
            if (Mathf.Abs(_source.pitch - newPitch) > 0.001f)
                _source.pitch = newPitch;
            
            // 3. Filter
            float newCutoff = Mathf.Lerp(minCutoff, maxCutoff, curvedT * curvedT);
            if (Mathf.Abs(_filter.cutoffFrequency - newCutoff) > 10f) // Only update if > 10Hz diff
                _filter.cutoffFrequency = newCutoff;
        }
    }
}
