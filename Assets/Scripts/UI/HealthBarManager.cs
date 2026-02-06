using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Manages screen-space health bars for enemies globally.
/// More performant than having a World Space Canvas on every enemy.
/// </summary>
public class HealthBarManager : MonoBehaviour
{
    public static HealthBarManager Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("The UI Prefab to spawn for enemies.")]
    [SerializeField] private GameObject healthBarPrefab;
    [Tooltip("The UI Prefab to spawn for the player (Optional - distinct style).")]
    [SerializeField] private GameObject playerHealthBarPrefab;
    [Tooltip("The UI Prefab to spawn for Titan hands (Optional - distinct style).")]
    [SerializeField] private GameObject titanHealthBarPrefab;
    [Tooltip("The UI Prefab to spawn for Titan core (Optional - distinct style).")]
    [SerializeField] private GameObject titanCoreHealthBarPrefab;
    
    [Tooltip("Offset from the target's position (in World Space) to float the health bar")]
    [SerializeField] private Vector3 heightOffset = new Vector3(0, 2.0f, 0);

    // Private class to track active bars
    private class ActiveBar
    {
        public Component Owner;
        public Transform TargetTransform;
        public RectTransform Rect;
        public Image FillImage;
        public GameObject GameObject;
        public bool IsTitanBar; // Titan bars have user-controlled visibility
        public bool IsUserVisible; // For Titan bars: tracks if user wants it visible
    }

    private List<ActiveBar> activeBars = new List<ActiveBar>();
    private Camera mainCam;
    private Canvas canvas;

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
        
        // If script is not on the canvas, try to find one
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        // Hook into the canvas render loop to eliminate jitter
        Canvas.willRenderCanvases += UpdateHealthBars;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= UpdateHealthBars;
    }

    private void UpdateHealthBars()
    {
        if (mainCam == null) mainCam = Camera.main;

        // Update positions of all active bars
        for (int i = activeBars.Count - 1; i >= 0; i--)
        {
            var bar = activeBars[i];

            // Safety check: if owner was destroyed
            if (bar.Owner == null)
            {
                DestroyBar(bar);
                activeBars.RemoveAt(i);
                continue;
            }

            // Update Position
            // Use specific target transform (HealthBarPoint) if set, otherwise Owner's transform
            Vector3 worldPos = bar.TargetTransform != null 
                ? bar.TargetTransform.position 
                : bar.Owner.transform.position + heightOffset;

            // Simple Culling
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
            bool isOnScreen = screenPos.z > 0 && 
                              screenPos.x > 0 && screenPos.x < Screen.width && 
                              screenPos.y > 0 && screenPos.y < Screen.height;

            if (isOnScreen)
            {
                // For Titan bars, only show if user explicitly wants it visible
                if (bar.IsTitanBar)
                {
                    if (bar.IsUserVisible && !bar.GameObject.activeSelf)
                        bar.GameObject.SetActive(true);
                    else if (!bar.IsUserVisible && bar.GameObject.activeSelf)
                        bar.GameObject.SetActive(false);
                }
                else
                {
                    // Normal enemy/player bars: show when on screen
                    if (!bar.GameObject.activeSelf) bar.GameObject.SetActive(true);
                }
                bar.Rect.position = screenPos;
            }
            else
            {
                if (bar.GameObject.activeSelf) bar.GameObject.SetActive(false);
            }
        }
    }

    public void Register(EnemyHealth enemy)
    {
        RegisterInternal(enemy, enemy.HealthBarPoint, enemy.CurrentHealth, enemy.MaxHealth, 
            enemy.OnHealthChanged, enemy.OnDeath, HealthBarType.Enemy);
    }

    public void Register(PlayerHealth player)
    {
        RegisterInternal(player, player.HealthBarPoint, player.CurrentHealth, player.MaxHealth, 
            player.OnHealthChanged, player.OnDeath, HealthBarType.Player);
    }

    public void Register(Boss.TitanHealth titanPart)
    {
        // Determine if this is a core or hand part
        bool isCore = titanPart.BodyPart == Boss.TitanBodyPart.Core;
        HealthBarType barType = isCore ? HealthBarType.TitanCore : HealthBarType.TitanHand;
        
        RegisterInternal(titanPart, titanPart.HealthBarPoint, titanPart.CurrentHealth, titanPart.MaxHealth, 
            titanPart.OnHealthChanged, titanPart.OnDeath, barType);
    }

    private enum HealthBarType { Enemy, Player, TitanHand, TitanCore }

    private void RegisterInternal(Component owner, Transform point, int current, int max, 
        UnityEngine.Events.UnityEvent<float> healthEvent, UnityEngine.Events.UnityEvent deathEvent, HealthBarType barType)
    {
        // Select prefab based on type
        GameObject prefabToUse = barType switch
        {
            HealthBarType.Player => playerHealthBarPrefab != null ? playerHealthBarPrefab : healthBarPrefab,
            HealthBarType.TitanHand => titanHealthBarPrefab != null ? titanHealthBarPrefab : healthBarPrefab,
            HealthBarType.TitanCore => titanCoreHealthBarPrefab != null ? titanCoreHealthBarPrefab : healthBarPrefab,
            _ => healthBarPrefab
        };

        if (prefabToUse == null)
        {
            Debug.LogWarning($"[HealthBarManager] No Prefab assigned for {barType}!");
            return;
        }

        GameObject go = Instantiate(prefabToUse, transform);
        go.name = $"HealthBar_{owner.gameObject.name}";
        
        RectTransform rect = go.GetComponent<RectTransform>();
        
        // Find Fill Image
        Image fill = null;
        var fillChild = go.transform.Find("Fill");
        if (fillChild != null) fill = fillChild.GetComponent<Image>();
        if (fill == null) fill = go.GetComponent<Image>();
        if (fill == null) fill = go.GetComponentInChildren<Image>();

        if (fill == null) Debug.LogError("[HealthBarManager] Prefab has no Image component!");

        ActiveBar newBar = new ActiveBar
        {
            Owner = owner,
            TargetTransform = point != null ? point : null,
            Rect = rect,
            FillImage = fill,
            GameObject = go,
            IsTitanBar = (barType == HealthBarType.TitanHand || barType == HealthBarType.TitanCore),
            IsUserVisible = false
        };

        // Callbacks
        healthEvent.AddListener((pct) => UpdateFill(newBar, pct));
        
        // For Titan bars, don't remove on death (they regenerate) - just hide
        // For other types, remove on death
        if (barType == HealthBarType.TitanHand || barType == HealthBarType.TitanCore)
        {
            // Titan bars persist - TitanHealth handles showing/hiding
            // No death listener needed
        }
        else
        {
            deathEvent.AddListener(() => RemoveBar(owner));
        }

        // Initial State
        float initialPct = (float)current / max;
        UpdateFill(newBar, initialPct);
        
        // Titan bars start hidden (visibility controlled by TitanHealth)
        if (barType == HealthBarType.TitanHand || barType == HealthBarType.TitanCore)
        {
            go.SetActive(false);
        }

        activeBars.Add(newBar);
    }

    public void Unregister(Component owner)
    {
        RemoveBar(owner);
    }
    
    /// <summary>
    /// Shows the health bar for a specific owner. Used by TitanHealth.
    /// </summary>
    public void ShowBar(Component owner)
    {
        for (int i = 0; i < activeBars.Count; i++)
        {
            if (activeBars[i].Owner == owner)
            {
                activeBars[i].IsUserVisible = true;
                if (activeBars[i].GameObject != null)
                {
                    activeBars[i].GameObject.SetActive(true);
                    // Debug.Log($"[HealthBarManager] ShowBar: Found and activated bar for {owner.gameObject.name}");
                }
                return;
            }
        }
        // Debug.LogWarning($"[HealthBarManager] ShowBar: No bar found for {owner.gameObject.name}");
    }
    
    /// <summary>
    /// Hides the health bar for a specific owner. Used by TitanHealth.
    /// </summary>
    public void HideBar(Component owner)
    {
        for (int i = 0; i < activeBars.Count; i++)
        {
            if (activeBars[i].Owner == owner)
            {
                activeBars[i].IsUserVisible = false;
                if (activeBars[i].GameObject != null)
                    activeBars[i].GameObject.SetActive(false);
                return;
            }
        }
    }

    private void UpdateFill(ActiveBar bar, float pct)
    {
        if (bar.FillImage != null)
            bar.FillImage.fillAmount = pct;
    }

    private void RemoveBar(Component owner)
    {
        for (int i = 0; i < activeBars.Count; i++)
        {
            if (activeBars[i].Owner == owner)
            {
                DestroyBar(activeBars[i]);
                activeBars.RemoveAt(i);
                return;
            }
        }
    }

    private void DestroyBar(ActiveBar bar)
    {
        if (bar.GameObject != null)
            Destroy(bar.GameObject);
    }
}
