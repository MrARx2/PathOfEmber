using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Audio
{
    /// <summary>
    /// UI-specific audio manager for button click sounds.
    /// Place as child of AudioManager object to share DontDestroyOnLoad.
    /// Automatically finds and hooks into all buttons in the scene.
    /// </summary>
    public class UIAudioManager : MonoBehaviour
    {
        public static UIAudioManager Instance { get; private set; }
        
        [Header("Button Sounds")]
        [SerializeField, Tooltip("Default sound for button clicks")]
        private SoundEvent buttonClickSound;
        
        [SerializeField, Tooltip("Sound for confirm/play buttons")]
        private SoundEvent confirmSound;
        
        [SerializeField, Tooltip("Sound for cancel/back buttons")]
        private SoundEvent cancelSound;
        
        [SerializeField, Tooltip("Sound for hover events (optional)")]
        private SoundEvent hoverSound;
        
        [Header("Settings")]
        [SerializeField, Tooltip("Auto-hook all buttons in scene on start and after scene load")]
        private bool autoHookButtons = false;
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private void Awake()
        {
            // Singleton pattern - child of AudioManager so shares DontDestroyOnLoad
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void OnEnable()
        {
            // Subscribe to scene loaded event to re-hook buttons after scene transitions
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (autoHookButtons)
            {
                // Small delay to ensure all UI is initialized
                Invoke(nameof(HookAllButtons), 0.1f);
                if (debugLog) Debug.Log($"[UIAudioManager] Scene '{scene.name}' loaded, will re-hook buttons");
            }
        }
        
        private void Start()
        {
            if (autoHookButtons)
                HookAllButtons();
        }
        
        /// <summary>
        /// Plays the default button click sound.
        /// </summary>
        public void PlayButtonClick()
        {
            if (buttonClickSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(buttonClickSound);
                if (debugLog) Debug.Log("[UIAudioManager] Button click sound played");
            }
        }
        
        /// <summary>
        /// Plays the confirm/play sound.
        /// </summary>
        public void PlayConfirm()
        {
            if (confirmSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(confirmSound);
                if (debugLog) Debug.Log("[UIAudioManager] Confirm sound played");
            }
        }
        
        /// <summary>
        /// Plays the cancel/back sound.
        /// </summary>
        public void PlayCancel()
        {
            if (cancelSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(cancelSound);
                if (debugLog) Debug.Log("[UIAudioManager] Cancel sound played");
            }
        }
        
        /// <summary>
        /// Plays the hover sound.
        /// </summary>
        public void PlayHover()
        {
            if (hoverSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(hoverSound);
                if (debugLog) Debug.Log("[UIAudioManager] Hover sound played");
            }
        }
        
        /// <summary>
        /// Plays a custom sound event.
        /// </summary>
        public void PlaySound(SoundEvent sound)
        {
            if (sound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(sound);
            }
        }
        
        /// <summary>
        /// Hooks click sounds to all buttons in the current scene.
        /// Call this after loading a new scene if autoHookButtons is off.
        /// </summary>
        public void HookAllButtons()
        {
            Button[] allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var button in allButtons)
            {
                // Add click listener (uses closure to capture button reference)
                button.onClick.AddListener(PlayButtonClick);
            }
            
            if (debugLog)
                Debug.Log($"[UIAudioManager] Hooked {allButtons.Length} buttons");
        }
        
        /// <summary>
        /// Hooks a specific button to play click sound.
        /// </summary>
        public void HookButton(Button button)
        {
            if (button != null)
            {
                button.onClick.AddListener(PlayButtonClick);
            }
        }
    }
}
