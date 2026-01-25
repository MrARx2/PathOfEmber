using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace Audio
{
    /// <summary>
    /// Manages high-level music logic, switching tracks based on scenes,
    /// and handling adaptive music layering based on player progression.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        /// <summary>
        /// Defines a music track and its behavior relative to player Z progression.
        /// </summary>
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
            [Tooltip("If true, the track keeps playing silently when volume is 0 (good for synchronized layers). If false, it pauses when silent (good for distinct songs).")]
            public bool keepSync = true;

            // Runtime state
            [HideInInspector] public AudioSource source;
            [HideInInspector] public float currentVelocity; // For smooth damping
        }

        /// <summary>
        /// Ensures MusicManager exists in the scene.
        /// </summary>
        public static void EnsureExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("Audio_MusicManager");
                go.AddComponent<MusicManager>();
                Debug.Log("[MusicManager] Created instance via EnsureExists");
            }
        }

        [Header("Scene Configuration")]
        [SerializeField] private SoundEvent mainMenuMusic;
        [SerializeField] private string[] mainMenuScenes = new string[] { "Main_Menu", "MainMenu" };
        [SerializeField] private string[] gameScenes = new string[] { "GameScene", "Game_Scene" };

        [Header("Game Music Configuration")]
        [SerializeField, Tooltip("List of tracks that play/fade based on progression")]
        private List<MusicTrackDef> gameMusicTracks = new List<MusicTrackDef>();

        [SerializeField, Tooltip("How quickly the volume changes (smoothing)")]
        private float volumeSmoothTime = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebug = false;
        
        // Runtime state
        private Transform _playerTransform;
        private bool _isInGameScene;
        private AudioSource _mainMenuSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            // Handle the scene we started in logic
            HandleSceneChange(SceneManager.GetActiveScene().name);
        }

        private void Update()
        {
            if (!_isInGameScene) return;

            // If we are in game but lost the player (e.g., player died/destroyed), try to find them again or just wait
            if (_playerTransform == null)
            {
                return;
            }

            UpdateAdaptiveMusic();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSceneChange(scene.name);
        }

        private void HandleSceneChange(string sceneName)
        {
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
                // Unknown scene - maybe do nothing or stop music?
            }
        }

        private void EnterMainMenu()
        {
            if (showDebug) Debug.Log($"[MusicManager] Entering Main Menu");
            _isInGameScene = false;
            _playerTransform = null;

            // Stop Game Music
            StopAllGameMusic();

            // Play Main Menu Music
            if (AudioManager.Instance != null && mainMenuMusic != null)
            {
                // We use AudioManager for simple BGM
                 AudioManager.Instance.PlayBGM(mainMenuMusic);
            }
        }

        private void EnterGameScene()
        {
            if (showDebug) Debug.Log($"[MusicManager] Entering Game Scene");
            _isInGameScene = true;

            // Stop Main Menu BGM if it's playing via AudioManager's BGM source
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
            }

            // Find Player
            StartCoroutine(FindPlayerRoutine());

            // Pre-initialize KeepSync tracks so they start on time (Volume 0)
            foreach (var track in gameMusicTracks)
            {
                if (track.keepSync && track.music != null)
                {
                    track.source = AudioManager.Instance.PlayAndGetSource(track.music, 0f);
                    if (track.source != null)
                    {
                        track.source.loop = true;
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
                }
            }
        }

        private void UpdateAdaptiveMusic()
        {
            if (AudioManager.Instance == null) return;

            float playerZ = _playerTransform.position.z;

            foreach (var track in gameMusicTracks)
            {
                if (track.music == null) continue;

                // Calculate desired volume weight (0 to 1)
                float targetWeight = CalculateTrackWeight(playerZ, track);
                
                // --- Logic for keeping/creating/destroying the AudioSource ---

                // Case 1: We HAVE a source
                if (track.source != null)
                {
                    // Update Volume
                    float finalTargetVol = targetWeight * track.music.volume;
                    track.source.volume = Mathf.SmoothDamp(track.source.volume, finalTargetVol, ref track.currentVelocity, volumeSmoothTime);

                    // Check if we should stop it (Volume effectively 0 AND not keeping sync)
                    // We use a small threshold like 0.001. If target is 0, we eventually fade to it.
                    // We only stop if target IS 0 and current is very low.
                    if (targetWeight <= 0.0001f && track.source.volume <= 0.001f && !track.keepSync)
                    {
                        track.source.Stop();
                        track.source = null;
                        track.currentVelocity = 0f; // Reset damping velocity
                    }
                }
                // Case 2: We DO NOT have a source, but we need one
                else if (targetWeight > 0.0001f || (track.keepSync && targetWeight <= 0.0001f)) 
                {
                    // Note: If keepSync is true, we initiate it even if weight is 0, so it's ready and synced.
                    // But usually keepSync tracks start at Z=0 global anyway. 
                    // If a keepSync track starts late, we might miss the 'sync' if we start it late. 
                    // To truly 'Keep Sync', it usually should start when the base track starts.
                    // For now, we assume keepSync tracks should always be alive if the manager is active? 
                    // Or only if they have ever been triggered? 
                    // If 'keepSync' is on, let's just create it immediately if it enters range OR on start.
                    // But simply: If weight > 0, we MUST play.
                    
                    if (targetWeight > 0.0001f)
                    {
                        track.source = AudioManager.Instance.PlayAndGetSource(track.music, 0f);
                        if (track.source != null)
                        {
                            track.source.loop = true;
                            // Set immediate volume if we are jumping in (optional, but smooth ramp is better)
                            track.source.volume = 0f; 
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the weight (0-1) of a track based on Z position.
        /// </summary>
        private float CalculateTrackWeight(float z, MusicTrackDef def)
        {
            float fadeInEnd = def.startZ + def.fadeInDistance;
            float fadeOutEnd = def.endZ + def.fadeOutDistance;

            // 1. Before Start or After End of Fade Out -> 0 Volume
            if (z < def.startZ || z > fadeOutEnd) return 0f;

            // 2. Fully Inside (After Fade In finished, Before Fade Out starts) -> 1 Volume
            if (z >= fadeInEnd && z <= def.endZ) return 1f;

            // 3. Fading In
            if (z >= def.startZ && z < fadeInEnd)
            {
                // Prevent divide by zero if distance is 0
                if (def.fadeInDistance <= 0.001f) return 1f; 
                return Mathf.InverseLerp(def.startZ, fadeInEnd, z);
            }

            // 4. Fading Out
            if (z > def.endZ && z <= fadeOutEnd)
            {
                if (def.fadeOutDistance <= 0.001f) return 0f;
                // InverseLerp gives 0 at start, 1 at end. We want 1 at start (endZ), 0 at end (fadeOutEnd).
                // So we do 1.0 - InverseLerp
                return 1.0f - Mathf.InverseLerp(def.endZ, fadeOutEnd, z);
            }

            return 0f;
        }

        private IEnumerator FindPlayerRoutine()
        {
            // Try to find player multiple times in case of initialization order
            int attempts = 0;
            while (_playerTransform == null && attempts < 10 && _isInGameScene)
            {
                // Try finding PlayerMovement first
                var playerMove = FindFirstObjectByType<PlayerMovement>();
                if (playerMove != null)
                {
                    _playerTransform = playerMove.transform;
                    if (showDebug) Debug.Log("[MusicManager] Found Player via PlayerMovement");
                    // Force an immediate update so we don't wait for next frame
                    UpdateAdaptiveMusic();
                    yield break;
                }

                // Fallback to Tag
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _playerTransform = playerObj.transform;
                    if (showDebug) Debug.Log("[MusicManager] Found Player via Tag");
                    UpdateAdaptiveMusic();
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
                attempts++;
            }

            if (_playerTransform == null && showDebug)
            {
                Debug.LogWarning("[MusicManager] Could not find Player after multiple attempts");
            }
        }

        private bool IsMainMenu(string sceneName)
        {
            foreach (var s in mainMenuScenes)
            {
                if (sceneName == s) return true;
            }
            return false;
        }

        private bool IsGameScene(string sceneName)
        {
            foreach (var s in gameScenes)
            {
                if (sceneName == s) return true;
            }
            return false;
        }
    }
}
