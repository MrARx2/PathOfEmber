using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace Audio
{
    /// <summary>
    /// Manages high-level music logic, switching tracks based on scenes,
    /// and handling adaptive music layering based on player progression.
    /// BULLETPROOF VERSION - Handles all edge cases for builds.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [System.Serializable]
        public class MusicTrackDef
        {
            [Tooltip("The sound event to play")]
            public SoundEvent music;

            [Space]
            [Header("Fade Progress Settings")]
            [Tooltip("At what Z position does this track START playing?")]
            public float startZ = 0f;

            [Tooltip("How many meters does it take to fade in to full volume?")]
            public float fadeInDistance = 20f;

            [Tooltip("At what Z position does this track START fading out?")]
            public float endZ = 100f;

            [Tooltip("How many meters does it take to fade out to silence?")]
            public float fadeOutDistance = 20f;

            [Space]
            [Header("Behavior")]
            [Tooltip("If true, the track keeps playing silently when volume is 0.")]
            public bool keepSync = true;

            [HideInInspector] public AudioSource source;
            [HideInInspector] public float currentVelocity;
        }

        [Header("Scene Configuration")]
        [SerializeField] private SoundEvent mainMenuMusic;
        [SerializeField] private string[] mainMenuScenes = new string[] { "Main_Menu", "MainMenu" };
        [SerializeField] private string[] gameScenes = new string[] { "GameScene", "Game_Scene", "GameScene (Updated)" };

        [Header("Game Music Configuration")]
        [SerializeField] private List<MusicTrackDef> gameMusicTracks = new List<MusicTrackDef>();
        [SerializeField] private float volumeSmoothTime = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebug = false;
        
        // State tracking
        private Transform _playerTransform;
        private bool _isInGameScene;
        private float _lastKnownPlayerZ = 0f;
        private bool _isInitialized = false;
        private string _lastHandledScene = "";
        private bool _isBeingDestroyed = false;

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                Instance = FindFirstObjectByType<MusicManager>();
            }
        }

        private void Awake()
        {
            // BULLETPROOF SINGLETON
            if (Instance != null && Instance != this)
            {
                _isBeingDestroyed = true;
                if (showDebug) Debug.Log("[MusicManager] Duplicate detected, destroying self");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
            _isInitialized = true;
            
            if (showDebug) Debug.Log("[MusicManager] Singleton initialized");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void Start()
        {
            // Don't run if we're a duplicate being destroyed
            if (_isBeingDestroyed || !_isInitialized) return;
            
            // Handle initial scene
            string currentScene = SceneManager.GetActiveScene().name;
            if (showDebug) Debug.Log($"[MusicManager] Start() - Current scene: {currentScene}");
            HandleSceneChange(currentScene);
        }

        private void Update()
        {
            // Don't run if destroyed or not in game
            if (_isBeingDestroyed || !_isInitialized) return;
            if (!_isInGameScene) return;

            UpdateAdaptiveMusic();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_isBeingDestroyed || !_isInitialized) return;
            
            if (showDebug) Debug.Log($"[MusicManager] OnSceneLoaded: {scene.name}");
            HandleSceneChange(scene.name);
        }

        private void HandleSceneChange(string sceneName)
        {
            // Prevent duplicate handling of same scene
            if (sceneName == _lastHandledScene)
            {
                if (showDebug) Debug.Log($"[MusicManager] Skipping duplicate scene handle: {sceneName}");
                return;
            }
            _lastHandledScene = sceneName;
            
            if (showDebug) Debug.Log($"[MusicManager] Handling scene change: {sceneName}");
            
            if (IsMainMenu(sceneName))
            {
                EnterMainMenu();
            }
            else if (IsGameScene(sceneName))
            {
                EnterGameScene();
            }
            else
            {
                if (showDebug) Debug.Log($"[MusicManager] Unknown scene type: {sceneName}");
            }
        }

        private void EnterMainMenu()
        {
            if (showDebug) Debug.Log("[MusicManager] === ENTERING MAIN MENU ===");
            
            _isInGameScene = false;
            _playerTransform = null;
            _lastKnownPlayerZ = 0f;

            // Stop all game music tracks
            StopAllGameMusic();

            // Play menu music immediately (no coroutine overhead)
            PlayMenuMusicDirect();
        }

        /// <summary>
        /// Direct, synchronous menu music playback. More reliable than coroutines.
        /// </summary>
        private void PlayMenuMusicDirect()
        {
            if (mainMenuMusic == null)
            {
                if (showDebug) Debug.LogWarning("[MusicManager] No main menu music assigned!");
                return;
            }

            if (!mainMenuMusic.IsValid)
            {
                if (showDebug) Debug.LogWarning("[MusicManager] Main menu music has no valid clips!");
                return;
            }

            // Try to play immediately
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(mainMenuMusic);
                if (showDebug) Debug.Log("[MusicManager] Menu music started via PlayBGM");
            }
            else
            {
                // AudioManager not ready yet - start a retry coroutine
                if (showDebug) Debug.Log("[MusicManager] AudioManager not ready, starting retry...");
                StartCoroutine(PlayMenuMusicRetry());
            }
        }

        private IEnumerator PlayMenuMusicRetry()
        {
            float timeout = 3f;
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                if (_isBeingDestroyed) yield break;
                if (_isInGameScene) yield break; // Scene changed, abort
                
                if (AudioManager.Instance != null && mainMenuMusic != null && mainMenuMusic.IsValid)
                {
                    AudioManager.Instance.PlayBGM(mainMenuMusic);
                    if (showDebug) Debug.Log("[MusicManager] Menu music started (after retry)");
                    yield break;
                }
                
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            Debug.LogError("[MusicManager] FAILED to start menu music after 3 seconds!");
        }

        private void EnterGameScene()
        {
            if (showDebug) Debug.Log("[MusicManager] === ENTERING GAME SCENE ===");
            
            _isInGameScene = true;
            _playerTransform = null;
            _lastKnownPlayerZ = 0f;

            // Stop menu BGM
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
                if (showDebug) Debug.Log("[MusicManager] Stopped menu BGM");
            }

            // Start looking for player
            StartCoroutine(FindPlayerRoutine());

            // Initialize game music tracks
            StartCoroutine(InitializeGameMusicRoutine());
        }

        private IEnumerator InitializeGameMusicRoutine()
        {
            // Wait for AudioManager
            float timeout = 3f;
            float elapsed = 0f;
            
            while (AudioManager.Instance == null && elapsed < timeout)
            {
                if (_isBeingDestroyed || !_isInGameScene) yield break;
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (AudioManager.Instance == null)
            {
                Debug.LogError("[MusicManager] AudioManager not available after 3 seconds!");
                yield break;
            }

            if (showDebug) Debug.Log("[MusicManager] Initializing game music tracks...");

            // Initialize keepSync tracks (they start at volume 0 but need to be playing)
            foreach (var track in gameMusicTracks)
            {
                if (track.keepSync && track.music != null && track.music.IsValid)
                {
                    track.source = AudioManager.Instance.PlayAndGetSource(track.music, 0f);
                    if (track.source != null)
                    {
                        track.source.loop = true;
                        track.source.volume = 0f;
                        if (showDebug) Debug.Log($"[MusicManager] Initialized keepSync track: {track.music.name}");
                    }
                }
            }
        }

        private void StopAllGameMusic()
        {
            foreach (var track in gameMusicTracks)
            {
                if (track.source != null)
                {
                    track.source.Stop();
                    track.source = null;
                    track.currentVelocity = 0f;
                }
            }
        }

        private void UpdateAdaptiveMusic()
        {
            if (AudioManager.Instance == null) return;

            // Get player Z position with fallback
            float playerZ;
            if (_playerTransform != null)
            {
                playerZ = _playerTransform.position.z;
                _lastKnownPlayerZ = playerZ;
            }
            else
            {
                playerZ = _lastKnownPlayerZ;
            }

            foreach (var track in gameMusicTracks)
            {
                if (track.music == null || !track.music.IsValid) continue;

                float targetWeight = CalculateTrackWeight(playerZ, track);
                
                // Has source - update volume
                if (track.source != null)
                {
                    float finalTargetVol = targetWeight * track.music.volume;
                    track.source.volume = Mathf.SmoothDamp(track.source.volume, finalTargetVol, ref track.currentVelocity, volumeSmoothTime);

                    // Stop if silent and not keeping sync
                    if (targetWeight <= 0.0001f && track.source.volume <= 0.001f && !track.keepSync)
                    {
                        track.source.Stop();
                        track.source = null;
                        track.currentVelocity = 0f;
                    }
                }
                // Needs source - create one
                else if (targetWeight > 0.0001f)
                {
                    track.source = AudioManager.Instance.PlayAndGetSource(track.music, 0f);
                    if (track.source != null)
                    {
                        track.source.loop = true;
                        track.source.volume = 0f;
                    }
                }
            }
        }

        private float CalculateTrackWeight(float z, MusicTrackDef def)
        {
            float fadeInEnd = def.startZ + def.fadeInDistance;
            float fadeOutEnd = def.endZ + def.fadeOutDistance;

            if (z < def.startZ || z > fadeOutEnd) return 0f;
            if (z >= fadeInEnd && z <= def.endZ) return 1f;

            // Fading In
            if (z >= def.startZ && z < fadeInEnd)
            {
                if (def.fadeInDistance <= 0.001f) return 1f; 
                return Mathf.InverseLerp(def.startZ, fadeInEnd, z);
            }

            // Fading Out
            if (z > def.endZ && z <= fadeOutEnd)
            {
                if (def.fadeOutDistance <= 0.001f) return 0f;
                return 1.0f - Mathf.InverseLerp(def.endZ, fadeOutEnd, z);
            }

            return 0f;
        }

        private IEnumerator FindPlayerRoutine()
        {
            int attempts = 0;
            while (_playerTransform == null && attempts < 30 && _isInGameScene)
            {
                if (_isBeingDestroyed) yield break;
                
                // Try PlayerMovement
                var playerMove = FindFirstObjectByType<PlayerMovement>();
                if (playerMove != null)
                {
                    _playerTransform = playerMove.transform;
                    if (showDebug) Debug.Log("[MusicManager] Found Player via PlayerMovement");
                    yield break;
                }

                // Fallback to Tag
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _playerTransform = playerObj.transform;
                    if (showDebug) Debug.Log("[MusicManager] Found Player via Tag");
                    yield break;
                }

                yield return new WaitForSeconds(0.2f);
                attempts++;
            }

            if (_playerTransform == null && showDebug)
            {
                Debug.LogWarning("[MusicManager] Could not find Player after 6 seconds - using Z=0 fallback");
            }
        }

        private bool IsMainMenu(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            foreach (var s in mainMenuScenes)
            {
                if (sceneName == s) return true;
            }
            return false;
        }

        private bool IsGameScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            foreach (var s in gameScenes)
            {
                if (sceneName == s) return true;
            }
            return false;
        }
    }
}
