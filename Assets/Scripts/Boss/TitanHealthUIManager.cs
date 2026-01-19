using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Boss
{
    /// <summary>
    /// Centralized manager for Titan health UI. Uses a single canvas with
    /// health bars that position themselves over their respective body parts.
    /// </summary>
    public class TitanHealthUIManager : MonoBehaviour
    {
        public static TitanHealthUIManager Instance { get; private set; }
        
        [Header("References")]
        [SerializeField, Tooltip("The single canvas containing all health bars")]
        private Canvas uiCanvas;
        
        [SerializeField, Tooltip("Camera for world-to-screen conversion")]
        private Camera mainCamera;
        
        [Header("Health Bar Elements")]
        [SerializeField] private PartHealthBar rightHandBar;
        [SerializeField] private PartHealthBar leftHandBar;
        [SerializeField] private PartHealthBar coreBar;
        
        [Header("Settings")]
        [SerializeField, Tooltip("Time before bar hides after last damage")]
        private float hideDelay = 2f;
        
        [SerializeField, Tooltip("Screen offset from world position")]
        private Vector2 screenOffset = new Vector2(0, 50);
        
        [Header("Colors")]
        [SerializeField] private Color healthColor = Color.green;
        [SerializeField] private Color damagedColor = Color.red;
        [SerializeField] private Color destroyedColor = Color.gray;
        
        private Dictionary<TitanBodyPart, PartHealthBar> bars = new Dictionary<TitanBodyPart, PartHealthBar>();
        
        private void Awake()
        {
            Instance = this;
            
            if (mainCamera == null)
                mainCamera = Camera.main;
            
            // Map bars
            if (rightHandBar.container != null)
                bars[TitanBodyPart.RightHand] = rightHandBar;
            if (leftHandBar.container != null)
                bars[TitanBodyPart.LeftHand] = leftHandBar;
            if (coreBar.container != null)
                bars[TitanBodyPart.Core] = coreBar;
            
            // Hide all initially
            foreach (var bar in bars.Values)
            {
                if (bar.container != null)
                    bar.container.SetActive(false);
            }
        }
        
        private void LateUpdate()
        {
            // Update positions and timers
            foreach (var kvp in bars)
            {
                var bar = kvp.Value;
                if (bar.container == null || !bar.container.activeSelf) continue;
                
                // Update position
                if (bar.worldTarget != null && mainCamera != null)
                {
                    Vector3 screenPos = mainCamera.WorldToScreenPoint(bar.worldTarget.position);
                    
                    // Only show if in front of camera
                    if (screenPos.z > 0)
                    {
                        bar.rectTransform.position = screenPos + (Vector3)screenOffset;
                    }
                    else
                    {
                        bar.container.SetActive(false);
                    }
                }
                
                // Update hide timer
                bar.hideTimer -= Time.deltaTime;
                if (bar.hideTimer <= 0f)
                {
                    bar.container.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// Register a health part with this UI manager.
        /// </summary>
        public void RegisterPart(TitanHealth healthPart, Transform worldTarget)
        {
            if (!bars.TryGetValue(healthPart.BodyPart, out var bar)) return;
            
            bar.healthPart = healthPart;
            bar.worldTarget = worldTarget;
            
            // Subscribe to events
            healthPart.OnDamage.AddListener((damage) => OnPartDamaged(healthPart.BodyPart, damage));
            healthPart.OnHealthChanged.AddListener((percent) => OnHealthChanged(healthPart.BodyPart, percent));
            healthPart.OnDeath.AddListener(() => OnPartDestroyed(healthPart.BodyPart));
            healthPart.OnHealed.AddListener(() => OnPartHealed(healthPart.BodyPart));
            
            // Set name
            if (bar.nameText != null)
                bar.nameText.text = GetPartName(healthPart.BodyPart);
        }
        
        private void OnPartDamaged(TitanBodyPart part, int damage)
        {
            if (!bars.TryGetValue(part, out var bar)) return;
            
            ShowBar(bar);
            
            // Flash damage color
            if (bar.fillImage != null)
            {
                bar.fillImage.color = damagedColor;
                StartCoroutine(ResetColorAfterDelay(bar, 0.1f));
            }
        }
        
        private void OnHealthChanged(TitanBodyPart part, float percent)
        {
            if (!bars.TryGetValue(part, out var bar)) return;
            
            if (bar.fillImage != null)
                bar.fillImage.fillAmount = percent;
        }
        
        private void OnPartDestroyed(TitanBodyPart part)
        {
            if (!bars.TryGetValue(part, out var bar)) return;
            
            if (bar.fillImage != null)
            {
                bar.fillImage.fillAmount = 0f;
                bar.fillImage.color = destroyedColor;
            }
        }
        
        private void OnPartHealed(TitanBodyPart part)
        {
            if (!bars.TryGetValue(part, out var bar)) return;
            
            ShowBar(bar);
            
            if (bar.fillImage != null && bar.healthPart != null)
            {
                bar.fillImage.fillAmount = bar.healthPart.HealthPercent;
                bar.fillImage.color = healthColor;
            }
        }
        
        private void ShowBar(PartHealthBar bar)
        {
            if (bar.container != null)
                bar.container.SetActive(true);
            
            bar.hideTimer = hideDelay;
            
            if (bar.fillImage != null)
                bar.fillImage.color = healthColor;
        }
        
        private System.Collections.IEnumerator ResetColorAfterDelay(PartHealthBar bar, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (bar.fillImage != null && bar.healthPart != null && !bar.healthPart.IsDestroyed)
                bar.fillImage.color = healthColor;
        }
        
        private string GetPartName(TitanBodyPart part)
        {
            switch (part)
            {
                case TitanBodyPart.RightHand: return "Right Hand";
                case TitanBodyPart.LeftHand: return "Left Hand";
                case TitanBodyPart.Core: return "Core";
                default: return part.ToString();
            }
        }
        
        [System.Serializable]
        public class PartHealthBar
        {
            [Tooltip("The container to show/hide")]
            public GameObject container;
            
            [Tooltip("RectTransform for positioning")]
            public RectTransform rectTransform;
            
            [Tooltip("Fill image (Image Type = Filled)")]
            public Image fillImage;
            
            [Tooltip("Optional name text")]
            public TMPro.TextMeshProUGUI nameText;
            
            // Runtime
            [HideInInspector] public TitanHealth healthPart;
            [HideInInspector] public Transform worldTarget;
            [HideInInspector] public float hideTimer;
        }
    }
}
