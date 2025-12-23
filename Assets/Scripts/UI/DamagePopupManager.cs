using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages screen-space damage popups globally.
/// Same approach as HealthBarManager - one canvas, no per-enemy overhead.
/// </summary>
public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField, Tooltip("Prefab for damage popup (needs RectTransform + TextMeshProUGUI)")]
    private GameObject popupPrefab;
    
    [SerializeField, Tooltip("Offset from world position")]
    private Vector3 worldOffset = new Vector3(0, 1.5f, 0);

    [Header("Animation")]
    [SerializeField] private float floatDistance = 50f; // Pixels
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float fadeStartPercent = 0.5f;
    [SerializeField] private float scalePopAmount = 1.3f;
    [SerializeField] private float scalePopDuration = 0.1f;
    [SerializeField] private float randomHorizontalOffset = 20f; // Pixels

    [Header("Colors")]
    [SerializeField] private Color normalDamageColor = Color.white;
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.8f, 0f); // Gold
    [SerializeField] private Color healColor = new Color(0.3f, 1f, 0.3f); // Green
    [SerializeField] private int criticalThreshold = 50; // Damage >= this shows as crit color

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
        public Color OriginalColor;
    }

    private List<ActivePopup> activePopups = new List<ActivePopup>();
    private Queue<GameObject> pool = new Queue<GameObject>();
    private Camera mainCam;
    private Canvas canvas;
    private RectTransform canvasRect;

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
            
            float normalizedTime = popup.Timer / lifetime;

            // Lifetime expired
            if (popup.Timer >= lifetime)
            {
                ReturnToPool(popup);
                activePopups.RemoveAt(i);
                continue;
            }

            // Update position - follow world position with float up
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

            // Float upward in screen space
            float floatOffset = normalizedTime * floatDistance;
            screenPos.x += popup.RandomOffsetX;
            screenPos.y += floatOffset;
            
            popup.Rect.position = screenPos;

            // Scale pop effect
            float scale = 1f;
            if (popup.Timer < scalePopDuration)
            {
                float t = popup.Timer / scalePopDuration;
                scale = Mathf.Lerp(1f, scalePopAmount, t);
            }
            else if (popup.Timer < scalePopDuration * 2f)
            {
                float t = (popup.Timer - scalePopDuration) / scalePopDuration;
                scale = Mathf.Lerp(scalePopAmount, 1f, t);
            }
            popup.Rect.localScale = Vector3.one * scale;

            // Fade out
            float fadeStart = lifetime * fadeStartPercent;
            if (popup.Timer > fadeStart)
            {
                float fadeT = (popup.Timer - fadeStart) / (lifetime - fadeStart);
                Color c = popup.OriginalColor;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                popup.Text.color = c;
            }
        }
    }

    /// <summary>
    /// Show a damage popup at the specified world position.
    /// </summary>
    public void ShowDamage(int damage, Vector3 worldPosition)
    {
        Color color = damage >= criticalThreshold ? criticalDamageColor : normalDamageColor;
        SpawnPopup(damage.ToString(), worldPosition, color);
    }

    /// <summary>
    /// Show a damage popup with custom color.
    /// </summary>
    public void ShowDamage(int damage, Vector3 worldPosition, Color color)
    {
        SpawnPopup(damage.ToString(), worldPosition, color);
    }

    /// <summary>
    /// Show a heal popup.
    /// </summary>
    public void ShowHeal(int amount, Vector3 worldPosition)
    {
        SpawnPopup("+" + amount.ToString(), worldPosition, healColor);
    }

    /// <summary>
    /// Show custom text popup (for "CRIT!", "MISS", "BLOCKED", etc.)
    /// </summary>
    public void ShowText(string text, Vector3 worldPosition, Color color)
    {
        SpawnPopup(text, worldPosition, color);
    }

    private void SpawnPopup(string text, Vector3 worldPosition, Color color)
    {
        if (popupPrefab == null)
        {
            Debug.LogWarning("[DamagePopupManager] No popup prefab assigned!");
            return;
        }

        GameObject go = GetFromPool();
        RectTransform rect = go.GetComponent<RectTransform>();
        TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        
        if (tmp == null)
        {
            Debug.LogError("[DamagePopupManager] Prefab needs TextMeshProUGUI component!");
            ReturnToPoolDirect(go);
            return;
        }

        // Setup text and color
        tmp.text = text;
        tmp.color = color;

        // Calculate initial position
        Vector3 worldPos = worldPosition + worldOffset;
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
        rect.position = screenPos;
        rect.localScale = Vector3.one;

        ActivePopup popup = new ActivePopup
        {
            Rect = rect,
            Text = tmp,
            GameObject = go,
            WorldStartPos = worldPos,
            ScreenStartPos = screenPos,
            Timer = 0f,
            RandomOffsetX = Random.Range(-randomHorizontalOffset, randomHorizontalOffset),
            OriginalColor = color
        };

        activePopups.Add(popup);
        go.SetActive(true);
    }

    private GameObject GetFromPool()
    {
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        
        GameObject go = Instantiate(popupPrefab, transform);
        return go;
    }

    private void ReturnToPool(ActivePopup popup)
    {
        if (popup.GameObject != null)
        {
            popup.GameObject.SetActive(false);
            pool.Enqueue(popup.GameObject);
        }
    }

    private void ReturnToPoolDirect(GameObject go)
    {
        if (go != null)
        {
            go.SetActive(false);
            pool.Enqueue(go);
        }
    }
}
