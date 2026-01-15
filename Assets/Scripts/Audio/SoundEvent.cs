using UnityEngine;
using UnityEngine.Audio;

namespace Audio
{
    /// <summary>
    /// ScriptableObject representing a single sound event.
    /// Create via: Assets > Create > Audio > Sound Event
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundEvent", menuName = "Audio/Sound Event")]
    public class SoundEvent : ScriptableObject
    {
        [Header("Audio Clips")]
        [Tooltip("One or more audio clips. If multiple, one is chosen randomly.")]
        public AudioClip[] clips;
        
        [Header("Volume & Pitch")]
        [Range(0f, 1f)]
        [Tooltip("Base volume (0-1)")]
        public float volume = 1f;
        
        [Tooltip("Add slight random volume variation for variety")]
        public bool randomizeVolume = false;
        
        [Range(0f, 0.3f)]
        [Tooltip("Volume variation range (±)")]
        public float volumeVariation = 0.1f;
        
        [Range(0.5f, 2f)]
        [Tooltip("Base pitch (1 = normal)")]
        public float pitch = 1f;
        
        [Tooltip("Add slight random pitch variation for variety")]
        public bool randomizePitch = false;
        
        [Range(0f, 1f)]
        [Tooltip("Pitch variation range (±)")]
        public float pitchVariation = 0.1f;
        
        [Header("Playback")]
        [Tooltip("Should this sound loop continuously?")]
        public bool loop = false;
        
        [Header("Mixer")]
        [Tooltip("Which mixer group to route through (SFX, BGM, etc.)")]
        public AudioMixerGroup mixerGroup;
        
        [Header("Polyphony Control")]
        [Tooltip("Maximum simultaneous instances (0 = unlimited)")]
        [Range(0, 10)]
        public int maxInstances = 3;
        
        [Tooltip("What happens when max instances reached")]
        public StealMode stealMode = StealMode.StealOldest;
        
        [Tooltip("Minimum time between plays (prevents spam)")]
        [Range(0f, 1f)]
        public float cooldown = 0f;
        
        [Header("3D Sound Settings")]
        [Tooltip("0 = 2D (no position), 1 = fully 3D")]
        [Range(0f, 1f)]
        public float spatialBlend = 0f;
        
        [Tooltip("Distance where sound is at full volume")]
        public float minDistance = 1f;
        
        [Tooltip("Distance where sound fades to silence")]
        public float maxDistance = 50f;
        
        [Header("Time Effects")]
        [Tooltip("If true, pitch slows down during time manipulation (enemy sounds). If false, stays normal (ambient world sounds like rivers, fire, wind).")]
        public bool affectedByTimeSlowdown = true;
        
        /// <summary>
        /// Polyphony steal modes.
        /// </summary>
        public enum StealMode
        {
            DontPlay,      // If at limit, don't play new sound
            StealOldest,   // Stop oldest instance, play new
            StealQuietest  // Stop quietest instance, play new
        }
        
        /// <summary>
        /// Returns true if this event has at least one clip.
        /// </summary>
        public bool IsValid => clips != null && clips.Length > 0 && clips[0] != null;
        
        /// <summary>
        /// Gets a random clip from the clips array.
        /// </summary>
        public AudioClip GetClip()
        {
            if (!IsValid) return null;
            if (clips.Length == 1) return clips[0];
            return clips[Random.Range(0, clips.Length)];
        }
        
        /// <summary>
        /// Gets the volume value, optionally randomized.
        /// </summary>
        public float GetVolume()
        {
            if (randomizeVolume)
            {
                return Mathf.Clamp01(volume + Random.Range(-volumeVariation, volumeVariation));
            }
            return volume;
        }
        
        /// <summary>
        /// Gets the pitch value, optionally randomized.
        /// </summary>
        public float GetPitch()
        {
            if (randomizePitch)
            {
                return pitch + Random.Range(-pitchVariation, pitchVariation);
            }
            return pitch;
        }
    }
}
