using UnityEngine;
using Audio;

/// <summary>
/// Plays main menu background music on scene load.
/// Attach to any GameObject in the Main Menu scene.
/// </summary>
public class MainMenuMusic : MonoBehaviour
{
    [Header("Music Settings")]
    [SerializeField, Tooltip("The SoundEvent for main menu music (should be set to loop)")]
    private SoundEvent menuMusicEvent;

    [SerializeField, Tooltip("Delay before starting music (in seconds)")]
    private float startDelay = 0f;

    private void Start()
    {
        if (startDelay > 0f)
        {
            Invoke(nameof(PlayMenuMusic), startDelay);
        }
        else
        {
            PlayMenuMusic();
        }
    }

    private void PlayMenuMusic()
    {
        if (menuMusicEvent == null)
        {
            Debug.LogWarning("[MainMenuMusic] No music SoundEvent assigned!");
            return;
        }

        if (AudioManager.Instance == null)
        {
            Debug.LogError("[MainMenuMusic] AudioManager not found! Make sure AudioManager is in the scene or is a DontDestroyOnLoad singleton.");
            return;
        }

        // Stop any currently playing BGM first
        if (AudioManager.Instance.IsBGMPlaying)
        {
            AudioManager.Instance.StopBGM();
        }

        AudioManager.Instance.PlayBGM(menuMusicEvent);
        Debug.Log($"[MainMenuMusic] Started playing: {menuMusicEvent.name}");
    }

    private void OnDestroy()
    {
        // Optionally stop music when leaving the menu
        // Uncomment if you want music to stop when transitioning to game scene:
        // if (AudioManager.Instance != null && AudioManager.Instance.IsBGMPlaying)
        // {
        //     AudioManager.Instance.StopBGM();
        // }
    }

    #region Debug
    [ContextMenu("Debug: Play Menu Music")]
    public void DebugPlayMusic() => PlayMenuMusic();

    [ContextMenu("Debug: Stop Music")]
    public void DebugStopMusic()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM();
        }
    }
    #endregion
}
