using UnityEngine;
using Audio;

/// <summary>
/// DEPRECATED: Menu music is now handled by MusicManager.
/// This script is kept for backward compatibility but does nothing.
/// If you need to control menu music, configure MusicManager's "Main Menu Music" field.
/// </summary>
public class MainMenuMusic : MonoBehaviour
{
    [Header("DEPRECATED - Use MusicManager instead")]
    [SerializeField, Tooltip("This field is no longer used. Configure MusicManager instead.")]
    private SoundEvent menuMusicEvent;

    [SerializeField]
    private bool debugLog = false;

    private void Start()
    {
        // DO NOTHING - MusicManager handles all music now
        if (debugLog) Debug.Log("[MainMenuMusic] DEPRECATED: Music is now handled by MusicManager");
    }
}
