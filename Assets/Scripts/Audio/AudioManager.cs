using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace Audio
{
    /// <summary>
    /// Central audio manager for all game sounds.
    /// Uses SoundEvent ScriptableObjects for easy tuning.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        
        [Header("Audio Mixer")]
        [SerializeField, Tooltip("Main audio mixer asset")]
        private AudioMixer audioMixer;
        
        [SerializeField, Tooltip("Default mixer group for SFX")]
        private AudioMixerGroup defaultSFXGroup;
        
        [SerializeField, Tooltip("Default mixer group for BGM")]
        private AudioMixerGroup defaultBGMGroup;
        
        [Header("Exposed Parameter Names")]
        [SerializeField] private string masterVolumeParam = "MasterVolume";
        [SerializeField] private string bgmVolumeParam = "BGMVolume";
        [SerializeField] private string sfxVolumeParam = "SFXVolume";
        [SerializeField] private string ambientVolumeParam = "AmbientVolume";
        
        [Header("Pool Settings")]
        [SerializeField, Tooltip("Number of pooled audio sources")]
        private int poolSize = 20;
        
        [Header("Volume Settings (0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float masterVolume = 1f;
        
        [SerializeField, Range(0f, 1f)]
        private float bgmVolume = 1f;
        
        [SerializeField, Range(0f, 1f)]
        private float sfxVolume = 1f;
        
        [SerializeField, Range(0f, 1f)]
        private float ambientVolume = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        [Header("Time-Based Pitch")]
        [SerializeField, Tooltip("Apply pitch slowdown when time slows (for cinematic effect)")]
        private bool applyTimeBasedPitch = true;
        
        [SerializeField, Tooltip("Intensity of pitch slowdown (0.5 = half as much slowdown as time, 1.0 = match time exactly)")]
        [Range(0.1f, 1f)]
        private float pitchSlowdownIntensity = 0.5f;
        
        [SerializeField, Tooltip("Minimum pitch value (prevents sounds from going too low)")]
        [Range(0.3f, 0.8f)]
        private float minimumPitch = 0.5f;
        
        // BGM source (separate, always available)
        private AudioSource _bgmSource;
        
        // Pooled sources for SFX
        private List<AudioSource> _sourcePool;
        
        // Track playing instances per SoundEvent for polyphony control
        private Dictionary<SoundEvent, List<AudioSource>> _activeInstances;
        
        // Track cooldowns per SoundEvent
        private Dictionary<SoundEvent, float> _lastPlayTime;
        
        // PlayerPrefs keys
        private const string PREFS_MASTER = "Audio_Master";
        private const string PREFS_BGM = "Audio_BGM";
        private const string PREFS_SFX = "Audio_SFX";
        private const string PREFS_AMBIENT = "Audio_Ambient";
        
        // dB conversion
        private const float MIN_DB = -80f;
        
        // Store original pitches for time-based adjustment
        private Dictionary<AudioSource, float> _originalPitches;
        
        // Track which sources are affected by time-based pitch
        private HashSet<AudioSource> _timeAffectedSources;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            Initialize();
        }
        
        private void Initialize()
        {
            LoadVolumeSettings();
            
            // Create BGM source
            _bgmSource = CreateAudioSource("BGM");
            _bgmSource.loop = true;
            _bgmSource.outputAudioMixerGroup = defaultBGMGroup;
            
            // Create source pool
            _sourcePool = new List<AudioSource>(poolSize);
            for (int i = 0; i < poolSize; i++)
            {
                var source = CreateAudioSource($"SFX_{i}");
                source.outputAudioMixerGroup = defaultSFXGroup;
                _sourcePool.Add(source);
            }
            
            // Initialize tracking
            _activeInstances = new Dictionary<SoundEvent, List<AudioSource>>();
            _lastPlayTime = new Dictionary<SoundEvent, float>();
            _originalPitches = new Dictionary<AudioSource, float>();
            _timeAffectedSources = new HashSet<AudioSource>();
            
            // Apply volumes to mixer
            ApplyAllVolumes();
            
            if (debugLog) Debug.Log($"[AudioManager] Initialized with {poolSize} pooled sources");
        }
        
        private AudioSource CreateAudioSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }
        
        private void Update()
        {
            // Clean up finished instances from tracking
            CleanupFinishedInstances();
            
            // Apply time-based pitch adjustment
            if (applyTimeBasedPitch)
            {
                UpdatePitchForTimeScale();
            }
        }
        
        /// <summary>
        /// Adjusts pitch of playing audio sources based on current time scale.
        /// Only affects sources marked as affectedByTimeSlowdown (enemy sounds, not world ambient).
        /// </summary>
        private void UpdatePitchForTimeScale()
        {
            float timeScale = Time.timeScale;
            
            // Calculate target pitch using intensity slider
            // If timeScale = 0.15 and intensity = 0.5, pitch = 1 - (1 - 0.15) * 0.5 = 0.575
            float targetPitch = 1f - (1f - timeScale) * pitchSlowdownIntensity;
            targetPitch = Mathf.Max(targetPitch, minimumPitch);
            
            // Apply to affected pooled sources only
            foreach (var source in _sourcePool)
            {
                if (source != null && source.isPlaying)
                {
                    // Get original pitch or assume 1.0
                    if (!_originalPitches.TryGetValue(source, out float originalPitch))
                    {
                        originalPitch = 1f;
                    }
                    
                    // Only apply slowdown if this source was marked as affected
                    if (_timeAffectedSources.Contains(source))
                    {
                        source.pitch = originalPitch * targetPitch;
                    }
                    else
                    {
                        // Restore original pitch for non-affected sources (ambient sounds)
                        source.pitch = originalPitch;
                    }
                }
            }
            
            // BGM: Apply slowdown (music sounds good slowed)
            if (_bgmSource != null && _bgmSource.isPlaying && _timeAffectedSources.Contains(_bgmSource))
            {
                if (!_originalPitches.TryGetValue(_bgmSource, out float bgmOriginalPitch))
                {
                    bgmOriginalPitch = 1f;
                }
                _bgmSource.pitch = bgmOriginalPitch * targetPitch;
            }
        }
        
        private void CleanupFinishedInstances()
        {
            foreach (var kvp in _activeInstances)
            {
                kvp.Value.RemoveAll(s => s == null || !s.isPlaying);
            }
        }
        
        #endregion
        
        #region Play Methods
        
        /// <summary>
        /// Plays a SoundEvent (2D).
        /// </summary>
        public void Play(SoundEvent soundEvent)
        {
            if (soundEvent == null || !soundEvent.IsValid) return;
            
            PlayInternal(soundEvent, Vector3.zero, false);
        }
        
        /// <summary>
        /// Plays a SoundEvent with volume override.
        /// </summary>
        public void Play(SoundEvent soundEvent, float volumeMultiplier)
        {
            if (soundEvent == null || !soundEvent.IsValid) return;
            
            PlayInternal(soundEvent, Vector3.zero, false, volumeMultiplier);
        }
        
        /// <summary>
        /// Plays a SoundEvent and returns the AudioSource for direct control.
        /// Useful when you need to modify volume during playback.
        /// </summary>
        public AudioSource PlayAndGetSource(SoundEvent soundEvent, float initialVolume = 1f)
        {
            if (soundEvent == null || !soundEvent.IsValid) return null;
            
            return PlayInternalWithReturn(soundEvent, Vector3.zero, false, initialVolume);
        }
        
        /// <summary>
        /// Plays a SoundEvent at a world position (3D).
        /// </summary>
        public void PlayAtPosition(SoundEvent soundEvent, Vector3 position)
        {
            if (soundEvent == null || !soundEvent.IsValid) return;
            
            PlayInternal(soundEvent, position, true);
        }
        
        /// <summary>
        /// Plays a SoundEvent at a world position with volume override.
        /// </summary>
        public void PlayAtPosition(SoundEvent soundEvent, Vector3 position, float volumeMultiplier)
        {
            if (soundEvent == null || !soundEvent.IsValid) return;
            
            PlayInternal(soundEvent, position, true, volumeMultiplier);
        }
        
        private void PlayInternal(SoundEvent soundEvent, Vector3 position, bool use3D, float volumeMultiplier = 1f)
        {
            // Check cooldown
            if (soundEvent.cooldown > 0)
            {
                if (_lastPlayTime.TryGetValue(soundEvent, out float lastTime))
                {
                    if (Time.time - lastTime < soundEvent.cooldown)
                    {
                        if (debugLog) Debug.Log($"[AudioManager] {soundEvent.name} on cooldown");
                        return;
                    }
                }
            }
            
            // Check polyphony limit
            AudioSource source = null;
            if (soundEvent.maxInstances > 0)
            {
                if (!_activeInstances.ContainsKey(soundEvent))
                {
                    _activeInstances[soundEvent] = new List<AudioSource>();
                }
                
                var instances = _activeInstances[soundEvent];
                
                // Remove finished instances
                instances.RemoveAll(s => s == null || !s.isPlaying);
                
                if (instances.Count >= soundEvent.maxInstances)
                {
                    // At limit - apply steal mode
                    switch (soundEvent.stealMode)
                    {
                        case SoundEvent.StealMode.DontPlay:
                            if (debugLog) Debug.Log($"[AudioManager] {soundEvent.name} at max instances, not playing");
                            return;
                            
                        case SoundEvent.StealMode.StealOldest:
                            source = instances[0];
                            instances.RemoveAt(0);
                            source.Stop();
                            break;
                            
                        case SoundEvent.StealMode.StealQuietest:
                            source = GetQuietestSource(instances);
                            instances.Remove(source);
                            source.Stop();
                            break;
                    }
                }
            }
            
            // Get source from pool if not stealing
            if (source == null)
            {
                source = GetAvailableSource();
                if (source == null)
                {
                    if (debugLog) Debug.LogWarning("[AudioManager] No available audio sources in pool!");
                    return;
                }
            }
            
            // Configure source
            AudioClip clip = soundEvent.GetClip();
            source.clip = clip;
            source.volume = soundEvent.GetVolume() * volumeMultiplier;
            source.pitch = soundEvent.GetPitch();
            source.loop = soundEvent.loop;
            source.spatialBlend = use3D ? soundEvent.spatialBlend : 0f;
            source.minDistance = soundEvent.minDistance;
            source.maxDistance = soundEvent.maxDistance;
            source.outputAudioMixerGroup = soundEvent.mixerGroup != null ? soundEvent.mixerGroup : defaultSFXGroup;
            
            if (use3D)
            {
                source.transform.position = position;
            }
            
            source.Play();
            
            // Store original pitch for time-based adjustment
            _originalPitches[source] = soundEvent.GetPitch();
            
            // Track if this source should be affected by time slowdown
            if (soundEvent.affectedByTimeSlowdown)
            {
                _timeAffectedSources.Add(source);
            }
            else
            {
                _timeAffectedSources.Remove(source);
            }
            
            // Track instance
            if (soundEvent.maxInstances > 0)
            {
                if (!_activeInstances.ContainsKey(soundEvent))
                {
                    _activeInstances[soundEvent] = new List<AudioSource>();
                }
                _activeInstances[soundEvent].Add(source);
            }
            
            // Track cooldown
            _lastPlayTime[soundEvent] = Time.time;
            
            if (debugLog) Debug.Log($"[AudioManager] Playing: {soundEvent.name} ({clip.name})");
        }
        
        /// <summary>
        /// Same as PlayInternal but returns the AudioSource for caller control.
        /// </summary>
        private AudioSource PlayInternalWithReturn(SoundEvent soundEvent, Vector3 position, bool use3D, float volumeMultiplier = 1f)
        {
            // Skip cooldown check for this version (caller manages timing)
            
            // Get source from pool
            AudioSource source = GetAvailableSource();
            if (source == null)
            {
                if (debugLog) Debug.LogWarning("[AudioManager] No available audio sources in pool!");
                return null;
            }
            
            // Configure source
            AudioClip clip = soundEvent.GetClip();
            source.clip = clip;
            source.volume = soundEvent.GetVolume() * volumeMultiplier;
            source.pitch = soundEvent.GetPitch();
            source.loop = soundEvent.loop;
            source.spatialBlend = use3D ? soundEvent.spatialBlend : 0f;
            source.minDistance = soundEvent.minDistance;
            source.maxDistance = soundEvent.maxDistance;
            source.outputAudioMixerGroup = soundEvent.mixerGroup != null ? soundEvent.mixerGroup : defaultSFXGroup;
            
            if (use3D)
            {
                source.transform.position = position;
            }
            
            source.Play();
            
            // Store original pitch for time-based adjustment
            _originalPitches[source] = soundEvent.GetPitch();
            
            // Track if this source should be affected by time slowdown
            if (soundEvent.affectedByTimeSlowdown)
            {
                _timeAffectedSources.Add(source);
            }
            else
            {
                _timeAffectedSources.Remove(source);
            }
            
            // Track instance
            if (soundEvent.maxInstances > 0)
            {
                if (!_activeInstances.ContainsKey(soundEvent))
                {
                    _activeInstances[soundEvent] = new List<AudioSource>();
                }
                _activeInstances[soundEvent].Add(source);
            }
            
            if (debugLog) Debug.Log($"[AudioManager] Playing (with return): {soundEvent.name} ({clip.name})");
            
            return source;
        }
        
        private AudioSource GetAvailableSource()
        {
            foreach (var source in _sourcePool)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }
            
            // All sources busy - steal oldest non-looping
            foreach (var source in _sourcePool)
            {
                if (!source.loop)
                {
                    source.Stop();
                    return source;
                }
            }
            
            return null;
        }
        
        private AudioSource GetQuietestSource(List<AudioSource> sources)
        {
            AudioSource quietest = sources[0];
            float lowestVolume = quietest.volume;
            
            foreach (var source in sources)
            {
                if (source.volume < lowestVolume)
                {
                    lowestVolume = source.volume;
                    quietest = source;
                }
            }
            
            return quietest;
        }
        
        /// <summary>
        /// Stops all instances of a SoundEvent.
        /// </summary>
        public void Stop(SoundEvent soundEvent)
        {
            if (soundEvent == null) return;
            
            if (_activeInstances.TryGetValue(soundEvent, out var instances))
            {
                foreach (var source in instances)
                {
                    if (source != null) source.Stop();
                }
                instances.Clear();
            }
        }
        
        /// <summary>
        /// Stops all sounds.
        /// </summary>
        public void StopAll()
        {
            foreach (var source in _sourcePool)
            {
                source.Stop();
            }
            _activeInstances.Clear();
        }
        
        #endregion
        
        #region BGM Methods
        
        /// <summary>
        /// Plays background music from a SoundEvent.
        /// </summary>
        public void PlayBGM(SoundEvent soundEvent)
        {
            if (soundEvent == null || !soundEvent.IsValid) return;
            
            _bgmSource.clip = soundEvent.GetClip();
            _bgmSource.volume = soundEvent.volume;
            _bgmSource.pitch = soundEvent.GetPitch();
            _bgmSource.outputAudioMixerGroup = soundEvent.mixerGroup != null ? soundEvent.mixerGroup : defaultBGMGroup;
            _bgmSource.Play();
            
            if (debugLog) Debug.Log($"[AudioManager] Playing BGM: {soundEvent.name}");
        }
        
        /// <summary>
        /// Plays BGM from a clip directly.
        /// </summary>
        public void PlayBGM(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            
            _bgmSource.clip = clip;
            _bgmSource.volume = volume;
            _bgmSource.pitch = 1f;
            _bgmSource.Play();
        }
        
        public void StopBGM() => _bgmSource.Stop();
        public void PauseBGM() => _bgmSource.Pause();
        public void ResumeBGM() => _bgmSource.UnPause();
        public bool IsBGMPlaying => _bgmSource.isPlaying;
        
        #endregion
        
        #region Volume Control
        
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            SetMixerVolume(masterVolumeParam, masterVolume);
            SaveVolumeSettings();
        }
        
        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            SetMixerVolume(bgmVolumeParam, bgmVolume);
            SaveVolumeSettings();
        }
        
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            SetMixerVolume(sfxVolumeParam, sfxVolume);
            SaveVolumeSettings();
        }
        
        public void SetAmbientVolume(float volume)
        {
            ambientVolume = Mathf.Clamp01(volume);
            SetMixerVolume(ambientVolumeParam, ambientVolume);
            SaveVolumeSettings();
        }
        
        public float GetMasterVolume() => masterVolume;
        public float GetBGMVolume() => bgmVolume;
        public float GetSFXVolume() => sfxVolume;
        public float GetAmbientVolume() => ambientVolume;
        
        private void SetMixerVolume(string parameter, float linearVolume)
        {
            if (audioMixer == null) return;
            
            float dB = linearVolume > 0.0001f 
                ? Mathf.Log10(linearVolume) * 20f 
                : MIN_DB;
            
            audioMixer.SetFloat(parameter, Mathf.Clamp(dB, MIN_DB, 0f));
        }
        
        private void ApplyAllVolumes()
        {
            SetMixerVolume(masterVolumeParam, masterVolume);
            SetMixerVolume(bgmVolumeParam, bgmVolume);
            SetMixerVolume(sfxVolumeParam, sfxVolume);
            SetMixerVolume(ambientVolumeParam, ambientVolume);
        }
        
        private void SaveVolumeSettings()
        {
            PlayerPrefs.SetFloat(PREFS_MASTER, masterVolume);
            PlayerPrefs.SetFloat(PREFS_BGM, bgmVolume);
            PlayerPrefs.SetFloat(PREFS_SFX, sfxVolume);
            PlayerPrefs.SetFloat(PREFS_AMBIENT, ambientVolume);
            PlayerPrefs.Save();
        }
        
        private void LoadVolumeSettings()
        {
            masterVolume = PlayerPrefs.GetFloat(PREFS_MASTER, 1f);
            bgmVolume = PlayerPrefs.GetFloat(PREFS_BGM, 1f);
            sfxVolume = PlayerPrefs.GetFloat(PREFS_SFX, 1f);
            ambientVolume = PlayerPrefs.GetFloat(PREFS_AMBIENT, 1f);
        }
        
        #endregion
    }
}
