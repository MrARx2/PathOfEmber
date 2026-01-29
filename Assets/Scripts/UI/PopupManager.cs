using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Animation styles for popup text.
/// All are clean variations of float-up with different flair.
/// </summary>
public enum PopupAnimationStyle
{
    FloatUp,        // Classic clean float upward (benchmark)
    FloatUpPop,     // Float up with satisfying scale pop at start
    FloatUpDrift,   // Float up with gentle horizontal drift
    FloatUpSway,    // Float up with subtle side-to-side sway
    FloatUpBurst    // Quick burst up then gentle settle
}

/// <summary>
/// Manages screen-space popups for damage and XP globally.
/// Same approach as HealthBarManager - one canvas, no per-enemy overhead.
/// Toggle damage/XP popups independently via Inspector.
/// </summary>
public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("Popup Toggles")]
    [SerializeField, Tooltip("Enable damage number popups when enemies are hit")]
    private bool showDamagePopups = true;
    
    [SerializeField, Tooltip("Enable XP popups when enemies die")]
    private bool showXPPopups = true;

    [Header("Prefabs")]
    [SerializeField, Tooltip("Prefab for damage popup (needs RectTransform + TextMeshProUGUI)")]
    private GameObject damagePopupPrefab;
    
    [SerializeField, Tooltip("Prefab for XP popup (can be same as damage, or different style)")]
    private GameObject xpPopupPrefab;

    [Header("Damage Popup Settings")]
    [SerializeField, Tooltip("Animation style for damage popups")]
    private PopupAnimationStyle damageAnimationStyle = PopupAnimationStyle.FloatUp;
    [SerializeField, Tooltip("World offset for damage popups")]
    private Vector3 damageWorldOffset = new Vector3(0, 1.5f, 0);
    [SerializeField] private float damageFloatDistance = 50f;
    [SerializeField] private float damageLifetime = 1f;
    [SerializeField] private float damageFadeStartPercent = 0.5f;
    [SerializeField] private float damageScalePopAmount = 1.3f;
    [SerializeField] private float damageScalePopDuration = 0.1f;
    [SerializeField] private float damageRandomHorizontalOffset = 20f;

    [Header("Damage Colors")]
    [SerializeField] private Color normalDamageColor = Color.white;
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color healColor = new Color(0.3f, 1f, 0.3f);
    [SerializeField] private int criticalThreshold = 50;

    [Header("XP Popup Settings")]
    [SerializeField, Tooltip("Animation style for XP popups")]
    private PopupAnimationStyle xpAnimationStyle = PopupAnimationStyle.FloatUpBurst;
    [SerializeField, Tooltip("World offset for XP popups")]
    private Vector3 xpWorldOffset = new Vector3(0, 0.5f, 0);
    [SerializeField] private float xpFloatDistance = 80f;
    [SerializeField] private float xpLifetime = 1.5f;
    [SerializeField] private float xpFadeStartPercent = 0.6f;
    [SerializeField] private float xpScalePopAmount = 1.5f;
    [SerializeField] private float xpScalePopDuration = 0.15f;
    [SerializeField] private float xpRandomHorizontalOffset = 10f;
    
    [Header("XP Colors")]
    [SerializeField] private Color xpColor = new Color(1f, 0.85f, 0f); // Gold
    [SerializeField] private string xpPrefix = "+";
    [SerializeField] private string xpSuffix = " XP";

    [Header("Animation Fine-Tuning")]
    [SerializeField, Tooltip("Pop: Extra scale multiplier for the pop effect")]
    private float popScaleBoost = 1.2f;
    [SerializeField, Tooltip("Drift: Maximum horizontal drift distance (pixels)")]
    private float driftAmount = 30f;
    [SerializeField, Tooltip("Sway: How far the sway moves side-to-side (pixels)")]
    private float swayAmount = 15f;
    [SerializeField, Tooltip("Sway: Speed of the sway oscillation")]
    private float swaySpeed = 4f;
    [SerializeField, Tooltip("Burst: Initial speed multiplier for burst")]
    private float burstSpeed = 2.5f;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    // Active popup tracking
    private class ActivePopup
    {
        public RectTransform Rect;
        public TextMeshProUGUI Text;
        public GameObject GameObject;
        public Vector3 WorldStartPos;
        public Vector3 ScreenStartPos;
        public float Timer;
        public float RandomOffsetX;
        public float DriftDirection;  // -1 or 1 for left/right drift
        public Color OriginalColor;
        public bool IsXP;
        public PopupAnimationStyle AnimationStyle;
        
        // Settings (cached per popup type)
        public float FloatDistance;
        public float Lifetime;
        public float FadeStartPercent;
        public float ScalePopAmount;
        public float ScalePopDuration;
    }

    private List<ActivePopup> activePopups = new List<ActivePopup>();
    private Queue<GameObject> damagePool = new Queue<GameObject>();
    private Queue<GameObject> xpPool = new Queue<GameObject>();
    private Camera mainCam;
    private Canvas canvas;
    private RectTransform canvasRect;

    // Public properties
    public bool ShowDamagePopups 
    { 
        get => showDamagePopups; 
        set => showDamagePopups = value; 
    }
    
    public bool ShowXPPopups 
    { 
        get => showXPPopups; 
        set => showXPPopups = value; 
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        mainCam = Camera.main;
        canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();
        
        // Fallback: if XP prefab not set, use damage prefab
        if (xpPopupPrefab == null)
            xpPopupPrefab = damagePopupPrefab;
    }

    private void Update()
    {
        if (mainCam == null) mainCam = Camera.main;
        UpdatePopups();
    }

    private void UpdatePopups()
    {
        for (int i = activePopups.Count - 1; i >= 0; i--)
        {
            var popup = activePopups[i];
            popup.Timer += Time.deltaTime;
            
            float normalizedTime = popup.Timer / popup.Lifetime;

            // Lifetime expired
            if (popup.Timer >= popup.Lifetime)
            {
                ReturnToPool(popup);
                activePopups.RemoveAt(i);
                continue;
            }

            // Update position - convert world to screen first
            Vector3 worldPos = popup.WorldStartPos;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
            
            // Check if behind camera
            if (screenPos.z < 0)
            {
                popup.GameObject.SetActive(false);
                continue;
            }
            else if (!popup.GameObject.activeSelf)
            {
                popup.GameObject.SetActive(true);
            }

            // Apply animation based on style
            Vector2 animOffset = CalculateAnimationOffset(popup, normalizedTime);
            screenPos.x += animOffset.x;
            screenPos.y += animOffset.y;
            
            popup.Rect.position = screenPos;

            // Scale pop effect
            float scale = 1f;
            if (popup.Timer < popup.ScalePopDuration)
            {
                float t = popup.Timer / popup.ScalePopDuration;
                scale = Mathf.Lerp(1f, popup.ScalePopAmount, t);
            }
            else if (popup.Timer < popup.ScalePopDuration * 2f)
            {
                float t = (popup.Timer - popup.ScalePopDuration) / popup.ScalePopDuration;
                scale = Mathf.Lerp(popup.ScalePopAmount, 1f, t);
            }
            popup.Rect.localScale = Vector3.one * scale;

            // Fade out
            float fadeStart = popup.Lifetime * popup.FadeStartPercent;
            if (popup.Timer > fadeStart)
            {
                float fadeT = (popup.Timer - fadeStart) / (popup.Lifetime - fadeStart);
                Color c = popup.OriginalColor;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                popup.Text.color = c;
            }
        }
    }

    /// <summary>
    /// Calculates screen-space offset based on animation style.
    /// </summary>
    private Vector2 CalculateAnimationOffset(ActivePopup popup, float t)
    {
        float x = popup.RandomOffsetX;
        float y = 0f;

        switch (popup.AnimationStyle)
        {
            case PopupAnimationStyle.FloatUp:
                // Classic clean float upward with easing
                y = EaseOutQuad(t) * popup.FloatDistance;
                break;

            case PopupAnimationStyle.FloatUpPop:
                // Float up with extra satisfying pop at start
                // Uses popScaleBoost for enhanced scale effect
                y = EaseOutQuad(t) * popup.FloatDistance;
                // Slightly faster initial movement with boost
                if (t < 0.2f)
                    y *= popScaleBoost;
                break;

            case PopupAnimationStyle.FloatUpDrift:
                // Float up with gentle horizontal drift in one direction
                y = EaseOutQuad(t) * popup.FloatDistance;
                // Smooth drift that eases out
                x += popup.DriftDirection * EaseOutQuad(t) * driftAmount;
                break;

            case PopupAnimationStyle.FloatUpSway:
                // Float up with subtle, smooth side-to-side sway
                y = EaseOutQuad(t) * popup.FloatDistance;
                // Gentle sine wave sway that reduces over time
                float swayFade = 1f - (t * 0.7f); // Sway reduces as it floats up
                x += Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * swayAmount * swayFade;
                break;

            case PopupAnimationStyle.FloatUpBurst:
                // Quick burst upward, then gentle settle
                // Uses burstSpeed for initial velocity multiplier
                float burstT = EaseOutCubic(t);
                y = burstT * popup.FloatDistance * burstSpeed / 2f;
                // Clamp to not overshoot too much
                y = Mathf.Min(y, popup.FloatDistance);
                break;
        }

        return new Vector2(x, y);
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
    
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    #region Damage Popups
    
    /// <summary>
    /// Show a damage popup at the specified world position.
    /// </summary>
    public void ShowDamage(int damage, Vector3 worldPosition)
    {
        if (!showDamagePopups) return;
        Color color = damage >= criticalThreshold ? criticalDamageColor : normalDamageColor;
        SpawnDamagePopup(damage.ToString(), worldPosition, color);
    }

    /// <summary>
    /// Show a damage popup with custom color.
    /// </summary>
    public void ShowDamage(int damage, Vector3 worldPosition, Color color)
    {
        if (!showDamagePopups) return;
        SpawnDamagePopup(damage.ToString(), worldPosition, color);
    }

    /// <summary>
    /// Show a heal popup.
    /// </summary>
    public void ShowHeal(int amount, Vector3 worldPosition)
    {
        if (!showDamagePopups) return;
        SpawnDamagePopup("+" + amount.ToString(), worldPosition, healColor);
    }

    /// <summary>
    /// Show custom text popup (for "CRIT!", "MISS", "BLOCKED", etc.)
    /// </summary>
    public void ShowText(string text, Vector3 worldPosition, Color color)
    {
        if (!showDamagePopups) return;
        SpawnDamagePopup(text, worldPosition, color);
    }

    private void SpawnDamagePopup(string text, Vector3 worldPosition, Color color)
    {
        if (damagePopupPrefab == null)
        {
            if (debugLog) Debug.LogWarning("[PopupManager] No damage popup prefab assigned!");
            return;
        }

        GameObject go = GetFromPool(damagePool, damagePopupPrefab);
        RectTransform rect = go.GetComponent<RectTransform>();
        TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        
        if (tmp == null)
        {
            Debug.LogError("[PopupManager] Damage prefab needs TextMeshProUGUI component!");
            ReturnToPoolDirect(go, damagePool);
            return;
        }

        // Setup text and color
        tmp.text = text;
        tmp.color = color;

        // Calculate initial position
        Vector3 worldPos = worldPosition + damageWorldOffset;
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
        rect.position = screenPos;
        rect.localScale = Vector3.one;

        // Random values for animations
        float randomX = Random.Range(-damageRandomHorizontalOffset, damageRandomHorizontalOffset);
        float driftDir = Random.value > 0.5f ? 1f : -1f;

        ActivePopup popup = new ActivePopup
        {
            Rect = rect,
            Text = tmp,
            GameObject = go,
            WorldStartPos = worldPos,
            ScreenStartPos = screenPos,
            Timer = 0f,
            RandomOffsetX = randomX,
            DriftDirection = driftDir,
            OriginalColor = color,
            IsXP = false,
            AnimationStyle = damageAnimationStyle,
            FloatDistance = damageFloatDistance,
            Lifetime = damageLifetime,
            FadeStartPercent = damageFadeStartPercent,
            ScalePopAmount = damageScalePopAmount,
            ScalePopDuration = damageScalePopDuration
        };

        activePopups.Add(popup);
        go.SetActive(true);
    }
    
    #endregion

    #region XP Popups
    
    /// <summary>
    /// Show an XP popup at the specified world position.
    /// Called by EnemyHealth on death.
    /// </summary>
    public void ShowXP(int xpAmount, Vector3 worldPosition)
    {
        if (!showXPPopups) return;
        if (xpAmount <= 0) return;
        
        string text = $"{xpPrefix}{xpAmount}{xpSuffix}";
        SpawnXPPopup(text, worldPosition, xpColor);
    }

    private void SpawnXPPopup(string text, Vector3 worldPosition, Color color)
    {
        if (xpPopupPrefab == null)
        {
            if (debugLog) Debug.LogWarning("[PopupManager] No XP popup prefab assigned!");
            return;
        }

        GameObject go = GetFromPool(xpPool, xpPopupPrefab);
        RectTransform rect = go.GetComponent<RectTransform>();
        TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        
        if (tmp == null)
        {
            Debug.LogError("[PopupManager] XP prefab needs TextMeshProUGUI component!");
            ReturnToPoolDirect(go, xpPool);
            return;
        }

        // Setup text and color
        tmp.text = text;
        tmp.color = color;

        // Calculate initial position
        Vector3 worldPos = worldPosition + xpWorldOffset;
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
        rect.position = screenPos;
        rect.localScale = Vector3.one;

        // Random values for animations
        float randomX = Random.Range(-xpRandomHorizontalOffset, xpRandomHorizontalOffset);
        float driftDir = Random.value > 0.5f ? 1f : -1f;

        ActivePopup popup = new ActivePopup
        {
            Rect = rect,
            Text = tmp,
            GameObject = go,
            WorldStartPos = worldPos,
            ScreenStartPos = screenPos,
            Timer = 0f,
            RandomOffsetX = randomX,
            DriftDirection = driftDir,
            OriginalColor = color,
            IsXP = true,
            AnimationStyle = xpAnimationStyle,
            FloatDistance = xpFloatDistance,
            Lifetime = xpLifetime,
            FadeStartPercent = xpFadeStartPercent,
            ScalePopAmount = xpScalePopAmount,
            ScalePopDuration = xpScalePopDuration
        };

        activePopups.Add(popup);
        go.SetActive(true);
    }
    
    #endregion

    #region Pool Management
    
    private GameObject GetFromPool(Queue<GameObject> pool, GameObject prefab)
    {
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        
        GameObject go = Instantiate(prefab, transform);
        return go;
    }

    private void ReturnToPool(ActivePopup popup)
    {
        if (popup.GameObject != null)
        {
            popup.GameObject.SetActive(false);
            
            if (popup.IsXP)
                xpPool.Enqueue(popup.GameObject);
            else
                damagePool.Enqueue(popup.GameObject);
        }
    }

    private void ReturnToPoolDirect(GameObject go, Queue<GameObject> pool)
    {
        if (go != null)
        {
            go.SetActive(false);
            pool.Enqueue(go);
        }
    }
    
    #endregion

    #region Debug
    
    [ContextMenu("Debug: Toggle Damage Popups")]
    private void DebugToggleDamage()
    {
        showDamagePopups = !showDamagePopups;
        Debug.Log($"[PopupManager] Damage popups: {(showDamagePopups ? "ON" : "OFF")}");
    }
    
    [ContextMenu("Debug: Toggle XP Popups")]
    private void DebugToggleXP()
    {
        showXPPopups = !showXPPopups;
        Debug.Log($"[PopupManager] XP popups: {(showXPPopups ? "ON" : "OFF")}");
    }
    
    [ContextMenu("Debug: Test Damage Popup (100)")]
    private void DebugTestDamage()
    {
        ShowDamage(100, transform.position + Vector3.up * 2f);
    }
    
    [ContextMenu("Debug: Test XP Popup (25)")]
    private void DebugTestXP()
    {
        ShowXP(25, transform.position + Vector3.up * 2f);
    }
    
    [ContextMenu("Debug: Test All Animation Styles")]
    private void DebugTestAllStyles()
    {
        Vector3 basePos = transform.position + Vector3.up * 2f;
        
        // Save current style
        var originalStyle = damageAnimationStyle;
        
        // Test each style with offset
        damageAnimationStyle = PopupAnimationStyle.FloatUp;
        ShowDamage(10, basePos + Vector3.left * 2f);
        
        damageAnimationStyle = PopupAnimationStyle.FloatUpPop;
        ShowDamage(20, basePos + Vector3.left * 1f);
        
        damageAnimationStyle = PopupAnimationStyle.FloatUpDrift;
        ShowDamage(30, basePos);
        
        damageAnimationStyle = PopupAnimationStyle.FloatUpSway;
        ShowDamage(40, basePos + Vector3.right * 1f);
        
        damageAnimationStyle = PopupAnimationStyle.FloatUpBurst;
        ShowDamage(50, basePos + Vector3.right * 2f);
        
        // Restore
        damageAnimationStyle = originalStyle;
        
        Debug.Log("[PopupManager] Tested all 5 animation styles!");
    }
    
    #endregion
}
