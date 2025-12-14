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
    
    [Tooltip("Offset from the target's position (in World Space) to float the health bar")]
    [SerializeField] private Vector3 heightOffset = new Vector3(0, 2.0f, 0);

    // Private class to track active bars
    private class ActiveBar
    {
        public Component Owner; // The Health Component (EnemyHealth or PlayerHealth)
        public Transform TargetTransform; // What we follow (e.g. HealthBarPoint)
        public RectTransform Rect;
        public Image FillImage;
        public GameObject GameObject;
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
                if (!bar.GameObject.activeSelf) bar.GameObject.SetActive(true);
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
            enemy.OnHealthChanged, enemy.OnDeath, isPlayer: false);
    }

    public void Register(PlayerHealth player)
    {
        RegisterInternal(player, player.HealthBarPoint, player.CurrentHealth, player.MaxHealth, 
            player.OnHealthChanged, player.OnDeath, isPlayer: true);
    }

    private void RegisterInternal(Component owner, Transform point, int current, int max, 
        UnityEngine.Events.UnityEvent<float> healthEvent, UnityEngine.Events.UnityEvent deathEvent, bool isPlayer)
    {
        GameObject prefabToUse = (isPlayer && playerHealthBarPrefab != null) ? playerHealthBarPrefab : healthBarPrefab;

        if (prefabToUse == null)
        {
            Debug.LogWarning($"[HealthBarManager] No Prefab assigned for {(isPlayer ? "Player" : "Enemy")}!");
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
            TargetTransform = point != null ? point : null, // If null, we use Owner.transform in generic update but we can't save it easily here without logic change.
                                                            // Actually Update logic handles null TargetTransform by checking Owner.transform + offset.
            Rect = rect,
            FillImage = fill,
            GameObject = go
        };

        // Callbacks
        healthEvent.AddListener((pct) => UpdateFill(newBar, pct));
        deathEvent.AddListener(() => RemoveBar(owner));

        // Initial State
        float initialPct = (float)current / max;
        UpdateFill(newBar, initialPct);

        activeBars.Add(newBar);
    }

    public void Unregister(Component owner)
    {
        RemoveBar(owner);
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
