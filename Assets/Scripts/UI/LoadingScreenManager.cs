using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using Audio;

/// <summary>
/// Persistent singleton that manages scene transitions and the loading screen UI.
/// Usage: LoadingScreenManager.Instance.LoadScene("SceneName");
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float minLoadTime = 1.0f; // Minimum time to show loading screen (prevents flickering)
    
    [Header("Scene Detection")]
    [SerializeField, Tooltip("Scene names that require asset preloading (have AssetPreloader)")]
    private string[] gameScenes = new string[] { "GameScene", "Game_Scene", "GameScene (Updated)" };

    private bool isSceneReady = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Hide initially if scene is already loaded (e.g. dev testing)
        if (loadingCanvas != null) 
            loadingCanvas.SetActive(false);
            
        // Ensure UI stays on top
        if (loadingCanvas != null)
        {
            Canvas c = loadingCanvas.GetComponent<Canvas>();
            if (c != null)
            {
                c.sortingOrder = 999;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
    
    private bool IsGameScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || gameScenes == null) return false;
        foreach (var s in gameScenes)
        {
            if (sceneName == s) return true;
        }
        return false;
    }

    /// <summary>
    /// Loads a scene with a loading screen.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    /// <summary>
    /// Called by AssetPreloader when it has finished all optimization tasks.
    /// </summary>
    public void OnSceneReady()
    {
        isSceneReady = true;
    }

    private float externalProgress = 0f;

    /// <summary>
    /// Update progress from external scripts (e.g. AssetPreloader).
    /// Value should be 0.0 to 1.0.
    /// </summary>
    public void ReportProgress(float value)
    {
        externalProgress = Mathf.Clamp01(value);
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        // Determine if this scene needs preloading
        bool needsPreloading = IsGameScene(sceneName);
        
        // Reset state BEFORE showing the screen
        if (progressBar != null) progressBar.fillAmount = 0f;
        if (loadingText != null) loadingText.text = "Loading Scene...";
        isSceneReady = false;
        externalProgress = 0f;

        // 1. Fade out current music during loading transition
        if (AudioManager.Instance != null && AudioManager.Instance.IsBGMPlaying)
        {
            AudioManager.Instance.FadeOutBGM(fadeDuration);
        }
        
        // 2. Show Loading Screen
        if (loadingCanvas != null) loadingCanvas.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            while (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha += Time.deltaTime / fadeDuration;
                yield return null;
            }
        }

        float startTime = Time.time;

        // 2. Start Async Load (Phase 1: 0% -> varies based on preloading need)
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        
        // If no preloading needed, scene load is 100% of progress
        // If preloading needed, scene load is 20% of progress
        float sceneLoadWeight = needsPreloading ? 0.2f : 1.0f;

        while (!op.isDone)
        {
            // Map scene load (0.0-0.9) to UI (0.0-sceneLoadWeight)
            float sceneProgress = Mathf.Clamp01(op.progress / 0.9f);
            float displayedProgress = sceneProgress * sceneLoadWeight;
            
            if (progressBar != null) 
                progressBar.fillAmount = displayedProgress;

            // Check if loaded (Unity considers 0.9 "loaded" when allowSceneActivation is false)
            if (op.progress >= 0.9f)
            {
                // Allow scene to activate (switch scenes)
                op.allowSceneActivation = true;
            }

            yield return null;
        }
        
        // 3. Wait for Scene Logic / Preloading (Phase 2: 20% -> 100%) - ONLY FOR GAME SCENES
        if (needsPreloading)
        {
            if (loadingText != null) loadingText.text = "Preparing Assets...";
            
            float waitStartTime = Time.time;
            
            while (!isSceneReady) 
            {
                // Map external progress (0.0-1.0) to UI (0.2-1.0)
                float displayedProgress = 0.2f + (externalProgress * 0.8f);
                
                if (progressBar != null) 
                    progressBar.fillAmount = displayedProgress;
                    
                // Update text based on progress
                if (loadingText != null)
                {
                    if (externalProgress < 0.3f) loadingText.text = "Warming Pools...";
                    else if (externalProgress < 0.6f) loadingText.text = "Preparing Projectiles...";
                    else if (externalProgress < 0.9f) loadingText.text = "Loading Audio...";
                    else loadingText.text = "Finalizing...";
                }

                // Safety timeout (15s)
                if (Time.time - waitStartTime > 15f)
                {
                    Debug.LogWarning("[LoadingScreenManager] Timed out waiting for SceneReady!");
                    break;
                }

                yield return null;
            }
        }
        else
        {
            // Non-game scene (e.g. Main Menu) - no preloader to wait for
            if (loadingText != null) loadingText.text = "Almost Ready...";
        }

        // Ensure we hit 100% visually
        if (progressBar != null) progressBar.fillAmount = 1f;

        // Force wait for minimum load time to prevent flickering
        float elapsedTime = Time.time - startTime;
        if (elapsedTime < minLoadTime)
        {
            yield return new WaitForSeconds(minLoadTime - elapsedTime);
        }

        // 4. Hide Loading Screen
        if (canvasGroup != null)
        {
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= Time.deltaTime / fadeDuration;
                yield return null;
            }
        }
        
        if (loadingCanvas != null) loadingCanvas.SetActive(false);
    }
}
